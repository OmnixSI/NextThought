using System.Security.Cryptography;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Platform;

namespace NextThought.Desktop;

public partial class AppState
{
    private DispatcherTimer? screenWatchTimer;
    private DateTimeOffset screenWatchNext;
    private DateTimeOffset screenWatchRetryAfter;
    private string? lastScreenText;

    [ObservableProperty] private bool screenWatchCapturing;
    [ObservableProperty] private string screenWatchStatus = "";
    [ObservableProperty] private string screenWatchError = "";

    private void StartScreenWatchTimer()
    {
        if (screenWatchTimer is not null) return;
        screenWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        screenWatchTimer.Tick += async (_, _) => await ScreenWatchTickAsync();
        screenWatchTimer.Start();
    }

    private void RestartScreenWatch()
    {
        screenWatchNext = DateTimeOffset.MinValue;
        screenWatchRetryAfter = DateTimeOffset.MinValue;
        ScreenWatchError = "";
        lastScreenText = null;
        if (!Settings.InsightScreenWatch || !Recording) ScreenWatchStatus = "";
    }

    private async Task ScreenWatchTickAsync()
    {
        if (!Settings.InsightScreenWatch || !Recording || ListeningPaused || Stopping || StartupOptions.Demo) return;
        var now = DateTimeOffset.UtcNow;
        if (now < screenWatchNext || now < screenWatchRetryAfter) return;
        screenWatchNext = now.AddSeconds(Math.Max(1, Settings.InsightScreenInterval));
        ScreenWatchCapturing = true;
        ScreenWatchStatus = "正在截屏读取屏幕…";
        try
        {
            ScreenCapture.ExcludeOwnWindowsFromCapture();
            await Task.Delay(80);
            var png = CaptureScreenPng();
            var text = await ScreenOcrReader.ReadAsync(png);
            if (string.IsNullOrWhiteSpace(text))
            {
                ScreenWatchStatus = "屏幕上没有读到文字。";
                return;
            }
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
            if (hash != lastScreenText)
            {
                lastScreenText = hash;
                if (Settings.InsightFollowSlides) FollowSlideFromScreen(text);
            }
            ScreenWatchStatus = $"已读屏 · {text.Length} 字";
            ScreenWatchError = "";
        }
        catch (Exception ex)
        {
            ScreenWatchError = ex.Message;
            ScreenWatchStatus = "读屏失败，20 秒后重试。";
            screenWatchRetryAfter = DateTimeOffset.UtcNow.AddSeconds(20);
        }
        finally { ScreenWatchCapturing = false; }
    }

    private byte[] CaptureScreenPng()
    {
        var capture = ScreenCapture.VirtualScreen();
        var source = Settings.InsightScreenSource;
        Avalonia.Media.Imaging.Bitmap bitmap;
        if (source == "window")
        {
            var bounds = ScreenCapture.ForegroundWindowBounds();
            bitmap = bounds.Width <= 1 || bounds.Height <= 1
                ? capture.Image
                : ScreenCapture.Crop(capture, new Avalonia.PixelRect(
                    bounds.X - capture.X, bounds.Y - capture.Y, bounds.Width, bounds.Height));
        }
        else if (source == "region" && ParseRegion(Settings.InsightScreenRegion) is { } region)
        {
            bitmap = ScreenCapture.Crop(capture, region);
        }
        else bitmap = capture.Image;
        return ScreenCapture.PngBytes(bitmap);
    }

    private static Avalonia.PixelRect? ParseRegion(string raw)
    {
        var parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !parts.All(p => int.TryParse(p, out _))) return null;
        var numbers = parts.Select(int.Parse).ToArray();
        return numbers[2] > 3 && numbers[3] > 3 ? new Avalonia.PixelRect(numbers[0], numbers[1], numbers[2], numbers[3]) : null;
    }

    [RelayCommand]
    private async Task ReadScreenNowAsync()
    {
        if (!Recording) { SetStatus("开始记录后才能读屏。"); return; }
        screenWatchRetryAfter = DateTimeOffset.MinValue;
        screenWatchNext = DateTimeOffset.MinValue;
        await ScreenWatchTickAsync();
    }
}
