using System.Collections.ObjectModel;
using System.Net.Http;
using System.IO;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFarmDemo.Contracts;

namespace SmartFarmDemo.Client;

public partial class MainViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5080") };
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(2) };

    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<double> TempSeries { get; } = [];
    public ObservableCollection<double> LuxSeries { get; } = [];

    [ObservableProperty] private string _tempText = "--";
    [ObservableProperty] private string _humText = "--";
    [ObservableProperty] private string _luxText = "--";
    [ObservableProperty] private string _lightText = "조명 꺼짐";
    [ObservableProperty] private bool _lightOn;
    [ObservableProperty] private string _lastUpdate = "-";
    [ObservableProperty] private BitmapImage? _photo;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "데몬 연결 대기 중…";

    public MainViewModel()
    {
        _poll.Tick += async (_, _) => await PollAsync();
        _poll.Start();
        _ = PollAsync();
    }

    private async Task PollAsync()
    {
        try
        {
            var status = await _http.GetFromJsonAsync<FarmStatus>("/api/status", JsonOpts);
            if (status is null) return;

            TempText = $"{status.Latest.TemperatureC:0.0} °C";
            HumText = $"{status.Latest.HumidityPercent:0} %";
            LuxText = $"{status.Latest.IlluminanceLux:0} lx";
            LightOn = status.Light == LightState.On;
            LightText = LightOn ? "조명 켜짐" : "조명 꺼짐";
            LastUpdate = status.Latest.Timestamp.LocalDateTime.ToString("HH:mm:ss");
            StatusMessage = "연결됨";

            Push(TempSeries, status.Latest.TemperatureC);
            Push(LuxSeries, status.Latest.IlluminanceLux);
        }
        catch
        {
            StatusMessage = "데몬에 연결할 수 없습니다 (localhost:5080)";
        }
    }

    private static void Push(ObservableCollection<double> series, double value)
    {
        series.Add(value);
        while (series.Count > 60) series.RemoveAt(0);
    }

    [RelayCommand]
    private async Task ToggleLightAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var res = await _http.PostAsJsonAsync("/api/light", new { on = !LightOn });
            res.EnsureSuccessStatusCode();
            await PollAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CapturePhotoAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var res = await _http.PostAsync("/api/photo", null);
            res.EnsureSuccessStatusCode();
            var doc = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
            var b64 = doc.GetProperty("imageBase64").GetString();
            if (b64 is null) return;
            var bytes = Convert.FromBase64String(b64);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            Photo = bmp;
        }
        finally { IsBusy = false; }
    }
}
