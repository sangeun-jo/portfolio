namespace SmartFarmDemo.Contracts;

public enum LightState
{
    Off,
    On
}

public sealed record EnvironmentReading(
    double TemperatureC,
    double HumidityPercent,
    double IlluminanceLux,
    DateTimeOffset Timestamp);

public sealed record FarmStatus(
    EnvironmentReading Latest,
    LightState Light,
    bool CameraAvailable,
    DateTimeOffset Timestamp);

public sealed record ChatMessage(
    Guid Id,
    string Role,          // "user" | "assistant" | "system"
    string Content,
    DateTimeOffset Timestamp,
    string? ImageBase64 = null);

public sealed record ChatRequest(string Message);

public sealed record ChatResponse(
    string Reply,
    IReadOnlyList<string> ToolTrace,
    string? ImageBase64 = null);
