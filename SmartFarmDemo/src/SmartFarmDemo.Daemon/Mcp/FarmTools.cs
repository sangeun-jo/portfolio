using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SmartFarmDemo.Daemon.Mcp;

[McpServerToolType]
public sealed class FarmTools
{
    [McpServerTool(Name = "get_environment", ReadOnly = true),
     Description("Read the current simulated farm temperature in Celsius, relative humidity in percent, illuminance in lux, and light state. Call this when asked for a current reading.")]
    public static object GetEnvironment(FarmSimulator farm)
    {
        var status = farm.GetStatus();
        return new
        {
            temperatureC = status.Latest.TemperatureC,
            humidityPercent = status.Latest.HumidityPercent,
            illuminanceLux = status.Latest.IlluminanceLux,
            timestamp = status.Latest.Timestamp,
            light = status.Light.ToString().ToLowerInvariant(),
            simulated = true,
        };
    }

    [McpServerTool(Name = "set_light", ReadOnly = false, Destructive = false, Idempotent = true),
     Description("Set the single farm-wide simulated light. Call only when the user asks to turn the light on or off.")]
    public static object SetLight(
        FarmSimulator farm,
        [Description("True to turn the light on; false to turn it off.")] bool on)
    {
        return new { light = farm.SetLight(on).ToString().ToLowerInvariant(), simulated = true };
    }
}
