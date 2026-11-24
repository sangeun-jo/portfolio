using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SmartFarmDemo.Contracts;
using SmartFarmDemo.Daemon;
using SmartFarmDemo.Daemon.Mcp;

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<FarmSimulator>();
builder.Services.AddSingleton<AgentService>();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<FarmTools>();
builder.Services.AddHttpClient("openai", c => c.BaseAddress = new Uri("https://api.openai.com"));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapMcp("/mcp");

app.MapGet("/api/status", (FarmSimulator farm) => farm.GetStatus());
app.MapGet("/api/history", (FarmSimulator farm, int? limit) => farm.GetHistory(limit ?? 120));
app.MapGet("/api/photo", (FarmSimulator farm) =>
    farm.LatestPhotoBase64 is { } p ? Results.Text(p, "text/plain") : Results.NotFound());
app.MapPost("/api/chat", async (ChatRequest req, AgentService agent, CancellationToken ct) =>
    await agent.HandleAsync(req.Message, ct));
app.MapPost("/api/light", (SetLightRequest req, FarmSimulator farm) =>
    Results.Ok(new { light = farm.SetLight(req.On).ToString().ToLowerInvariant() }));
app.MapPost("/api/photo", (FarmSimulator farm) =>
    Results.Ok(new { imageBase64 = farm.CapturePhoto() }));
app.MapPost("/api/voice-token", async (IHttpClientFactory http, IConfiguration config, CancellationToken ct) =>
{
    var apiKey = config["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (string.IsNullOrEmpty(apiKey))
        return Results.Problem("OPENAI_API_KEY is not set", statusCode: 500);

    var model = config["OPENAI_REALTIME_MODEL"] ?? "gpt-realtime-2.1";
    var voice = config["OPENAI_REALTIME_VOICE"] ?? "marin";
    var instructions = config["VOICE_INSTRUCTIONS"] ?? VoiceInstructions.Default;

    var payload = new
    {
        session = new
        {
            type = "realtime",
            model,
            instructions,
            audio = new { output = new { voice } },
        },
    };

    using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/realtime/client_secrets");
    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    using var res = await http.CreateClient("openai").SendAsync(req, ct);
    var body = await res.Content.ReadAsStringAsync(ct);
    if (!res.IsSuccessStatusCode)
        return Results.Problem($"Realtime token failed: {res.StatusCode} {body}", statusCode: 502);

    using var doc = JsonDocument.Parse(body);
    var value = doc.RootElement.GetProperty("value").GetString();
    return Results.Ok(new { clientSecret = value });
});

app.Run();

static void LoadDotEnv()
{
    foreach (var path in new[]
    {
        Path.Combine(AppContext.BaseDirectory, ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env"),
    })
    {
        if (!File.Exists(path)) continue;
        foreach (var line in File.ReadLines(path))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith('#')) continue;
            var eq = t.IndexOf('=');
            if (eq <= 0) continue;
            var key = t[..eq].Trim();
            var val = t[(eq + 1)..].Trim().Trim('"');
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, val);
        }
    }
}

public sealed record SetLightRequest(bool On);
