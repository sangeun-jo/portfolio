using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using SmartFarmDemo.Contracts;

namespace SmartFarmDemo.Daemon;

public sealed class AgentService
{
    private const string SystemPrompt = """
        You are AgentCrew, an assistant controlling a miniature smart farm.
        The farm has temperature, humidity and illuminance sensors, a grow light
        relay, and a camera. Use the provided tools to answer questions and
        perform actions. Always call get_environment before answering questions
        about current conditions. Keep replies short and friendly (Korean OK).
        """;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly FarmSimulator _farm;
    private readonly ChatClient _chat;
    private readonly List<OpenAI.Chat.ChatMessage> _history = [];

    private static readonly ChatTool GetEnvironmentTool = ChatTool.CreateFunctionTool(
        functionName: "get_environment",
        functionDescription: "Get the latest temperature, humidity and illuminance readings plus the grow light state.");

    private static readonly ChatTool GetHistoryTool = ChatTool.CreateFunctionTool(
        functionName: "get_history",
        functionDescription: "Get recent sensor history for trend questions.",
        functionParameters: BinaryData.FromBytes("""
            { "type": "object", "properties": { "limit": { "type": "integer", "description": "Number of recent readings, default 20" } } }
            """u8.ToArray()));

    private static readonly ChatTool SetLightTool = ChatTool.CreateFunctionTool(
        functionName: "set_light",
        functionDescription: "Turn the grow light on or off.",
        functionParameters: BinaryData.FromBytes("""
            { "type": "object", "properties": { "on": { "type": "boolean", "description": "true = light on, false = light off" } }, "required": ["on"] }
            """u8.ToArray()));

    private static readonly ChatTool CapturePhotoTool = ChatTool.CreateFunctionTool(
        functionName: "capture_photo",
        functionDescription: "Capture a photo of the farm with the camera.");

    private static readonly ChatCompletionOptions Options = new()
    {
        Tools = { GetEnvironmentTool, GetHistoryTool, SetLightTool, CapturePhotoTool },
        Temperature = 0.7f,
    };

    public AgentService(FarmSimulator farm, IConfiguration config)
    {
        _farm = farm;
        var apiKey = config["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var model = config["OPENAI_MODEL"] ?? "gpt-4o-mini";
        _chat = new OpenAIClient(apiKey ?? throw new InvalidOperationException("OPENAI_API_KEY is not set")).GetChatClient(model);
        _history.Add(new SystemChatMessage(SystemPrompt));
    }

    public async Task<ChatResponse> HandleAsync(string userText, CancellationToken ct = default)
    {
        _history.Add(new UserChatMessage(userText));
        var trace = new List<string>();
        string? image = null;

        for (var i = 0; i < 8; i++)
        {
            var completion = (await _chat.CompleteChatAsync(_history, Options, ct)).Value;

            if (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                _history.Add(new AssistantChatMessage(completion));
                foreach (var call in completion.ToolCalls)
                {
                    var (json, summary) = ExecuteTool(call.FunctionName, call.FunctionArguments);
                    trace.Add($"{call.FunctionName}: {summary}");
                    if (call.FunctionName == "capture_photo" && _farm.LatestPhotoBase64 is { } photo)
                        image = photo;
                    _history.Add(new ToolChatMessage(call.Id, call.FunctionName, json));
                }
                continue;
            }

            var reply = completion.Content.Count > 0 ? completion.Content[0].Text : "(응답 없음)";
            _history.Add(new AssistantChatMessage(reply));
            return new ChatResponse(reply, trace, image);
        }

        return new ChatResponse("도구 호출이 너무 많아 중단했습니다.", trace, image);
    }

    private (string json, string summary) ExecuteTool(string name, BinaryData args)
    {
        try
        {
            return name switch
            {
                "get_environment" => EnvJson(),
                "get_history" => HistoryJson(args),
                "set_light" => SetLightJson(args),
                "capture_photo" => PhotoJson(),
                _ => (JsonSerializer.Serialize(new { error = $"unknown tool {name}" }), "error"),
            };
        }
        catch (Exception ex)
        {
            return (JsonSerializer.Serialize(new { error = ex.Message }), ex.Message);
        }
    }

    private (string json, string summary) EnvJson()
    {
        var s = _farm.GetStatus();
        var l = s.Latest;
        var json = JsonSerializer.Serialize(new
        {
            temperatureC = l.TemperatureC,
            humidityPercent = l.HumidityPercent,
            illuminanceLux = l.IlluminanceLux,
            light = s.Light.ToString().ToLowerInvariant(),
            timestamp = l.Timestamp,
        }, JsonOpts);
        return (json, $"{l.TemperatureC}°C / {l.HumidityPercent}% / {l.IlluminanceLux}lx, light={s.Light}");
    }

    private (string json, string summary) HistoryJson(BinaryData args)
    {
        var limit = 20;
        try
        {
            using var doc = JsonDocument.Parse(args);
            if (doc.RootElement.TryGetProperty("limit", out var el) && el.TryGetInt32(out var v))
                limit = v;
        }
        catch { /* default */ }

        var rows = _farm.GetHistory(limit)
            .Select(r => new { r.TemperatureC, r.HumidityPercent, r.IlluminanceLux, r.Timestamp })
            .ToList();
        var json = JsonSerializer.Serialize(rows, JsonOpts);
        return (json, $"{rows.Count} readings");
    }

    private (string json, string summary) SetLightJson(BinaryData args)
    {
        var on = true;
        try
        {
            using var doc = JsonDocument.Parse(args);
            if (doc.RootElement.TryGetProperty("on", out var el))
                on = el.GetBoolean();
        }
        catch { /* default */ }

        var state = _farm.SetLight(on);
        var json = JsonSerializer.Serialize(new { light = state.ToString().ToLowerInvariant() }, JsonOpts);
        return (json, $"light -> {state}");
    }

    private (string json, string summary) PhotoJson()
    {
        _farm.CapturePhoto();
        var json = JsonSerializer.Serialize(new { captured = true }, JsonOpts);
        return (json, "photo captured");
    }
}
