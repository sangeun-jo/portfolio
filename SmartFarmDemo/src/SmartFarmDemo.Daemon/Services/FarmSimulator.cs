using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SmartFarmDemo.Contracts;

namespace SmartFarmDemo.Daemon;

public sealed class FarmSimulator : IDisposable
{
    private const int HistoryCapacity = 1000;
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly Random _rng = new();
    private readonly Timer _timer;
    private readonly List<EnvironmentReading> _history = [];

    private double _temperature = 24.0;
    private double _humidity = 55.0;
    private double _illuminance = 350.0;
    private LightState _light = LightState.Off;
    private string? _latestPhotoBase64;

    public FarmSimulator()
    {
        Record(new EnvironmentReading(_temperature, _humidity, _illuminance, DateTimeOffset.Now));
        _timer = new Timer(_ => TickOnce(), null, Tick, Tick);
    }

    public FarmStatus GetStatus()
    {
        lock (_gate)
        {
            return new FarmStatus(_history[^1], _light, CameraAvailable: true, DateTimeOffset.Now);
        }
    }

    public IReadOnlyList<EnvironmentReading> GetHistory(int limit = 120)
    {
        lock (_gate)
        {
            return _history.TakeLast(Math.Clamp(limit, 1, HistoryCapacity)).ToList();
        }
    }

    public LightState SetLight(bool on)
    {
        lock (_gate)
        {
            _light = on ? LightState.On : LightState.Off;
            return _light;
        }
    }

    public string? LatestPhotoBase64
    {
        get { lock (_gate) return _latestPhotoBase64; }
    }

    public string CapturePhoto()
    {
        var png = RenderScene();
        var base64 = Convert.ToBase64String(png);
        lock (_gate) _latestPhotoBase64 = base64;
        return base64;
    }

    private void TickOnce()
    {
        lock (_gate)
        {
            // Random-walk sensor simulation drifting toward plausible greenhouse values.
            _temperature += (_rng.NextDouble() - 0.5) * 0.3;
            _temperature += (24.0 - _temperature) * 0.02;
            _humidity += (_rng.NextDouble() - 0.5) * 0.8;
            _humidity += (55.0 - _humidity) * 0.02;

            var ambient = _light == LightState.On ? 8500.0 : 320.0;
            _illuminance += (ambient - _illuminance) * 0.15
                + (_rng.NextDouble() - 0.5) * 200.0;
            _illuminance = Math.Max(0, _illuminance);

            Record(new EnvironmentReading(
                Math.Round(_temperature, 1),
                Math.Round(_humidity, 1),
                Math.Round(_illuminance, 0),
                DateTimeOffset.Now));
        }
    }

    private void Record(EnvironmentReading reading)
    {
        _history.Add(reading);
        if (_history.Count > HistoryCapacity)
            _history.RemoveRange(0, _history.Count - HistoryCapacity);
    }

    private byte[] RenderScene()
    {
        const int w = 640, h = 480;
        using var bmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Sky
            using (var sky = new LinearGradientBrush(
                new Rectangle(0, 0, w, h),
                Color.FromArgb(135, 206, 235), Color.FromArgb(240, 248, 255), 90f))
                g.FillRectangle(sky, 0, 0, w, h);

            // Grow light glow
            if (_light == LightState.On)
            {
                using var glow = new SolidBrush(Color.FromArgb(90, 255, 236, 140));
                g.FillEllipse(glow, w / 2 - 160, 10, 320, 180);
            }

            // Ground
            using (var ground = new SolidBrush(Color.FromArgb(110, 160, 90)))
                g.FillRectangle(ground, 0, h - 120, w, 120);

            // Sun icon when light is on
            if (_light == LightState.On)
            {
                using var sun = new SolidBrush(Color.FromArgb(255, 210, 80));
                g.FillEllipse(sun, w / 2 - 28, 26, 56, 56);
            }

            // Plants in pots
            var rnd = new Random(42);
            for (int i = 0; i < 5; i++)
            {
                int px = 60 + i * 125 + rnd.Next(-15, 15);
                int py = h - 105;

                using var pot = new SolidBrush(Color.FromArgb(160, 90, 60));
                g.FillRectangle(pot, px - 22, py + 30, 44, 34);

                using var leaf = new SolidBrush(Color.FromArgb(60 + rnd.Next(40), 140 + rnd.Next(60), 90));
                for (int l = 0; l < 4; l++)
                {
                    int lx = px - 18 + l * 12;
                    int ly = py + 10 - rnd.Next(0, 18);
                    g.FillEllipse(leaf, lx, ly, 20, 34);
                }
            }

            // HUD text
            using var font = new Font("Segoe UI", 14, FontStyle.Bold);
            using var fmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
            using var hudBg = new SolidBrush(Color.FromArgb(160, 20, 24, 30));
            g.FillRectangle(hudBg, 10, h - 52, 330, 42);
            using var hudFg = new SolidBrush(Color.White);
            var latest = _history.Count > 0 ? _history[^1] : null;
            var text = latest is null ? "no data"
                : $"{latest.TemperatureC:0.0}C  {latest.HumidityPercent:0}%  {latest.IlluminanceLux:0}lx";
            g.DrawString(text, font, hudFg, new RectangleF(18, h - 50, 320, 38), fmt);
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public void Dispose() => _timer.Dispose();
}
