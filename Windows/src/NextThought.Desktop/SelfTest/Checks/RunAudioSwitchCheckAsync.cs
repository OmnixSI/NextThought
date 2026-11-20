namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunAudioSwitchCheckAsync(string path)
    {
        await Task.Delay(300);
        var passed = false; var error = ""; var before = ""; var after = ""; var frames = 0;
        try
        {
            var devices = NextThought.Platform.AudioCaptureService.Devices(true);
            if (devices.Count < 2) throw new InvalidOperationException("需要至少两个播放设备才能验证切换。");
            await using var capture = new NextThought.Platform.AudioCaptureService();
            capture.FrameReady += _ => Interlocked.Increment(ref frames);
            capture.Start(new AppSettings { CaptureSystem = true, CaptureMicrophone = false });
            await Task.Delay(800);
            before = capture.ActiveDeviceId(AudioSource.System) ?? "";
            var target = devices.First(device => device.Id != before);
            capture.SwitchForCheck(AudioSource.System, target.Id);
            await Task.Delay(1500);
            after = capture.ActiveDeviceId(AudioSource.System) ?? "";
            passed = after == target.Id && frames > 0 && capture.LastError is null;
        }
        catch (Exception exception) { error = exception.ToString(); }
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(
                new { passed, before, after, frames, error }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
        Console.WriteLine(passed ? "audio-switch-check: passed" : "audio-switch-check: failed");
        Close();
    }

    /// <summary>QA: a saved key override is used and all 19 bindings register.</summary>
    /// <summary>QA: --demo seeds the sample meeting without writing a session.</summary>
    /// <summary>QA: Alt+F4-style close hides the floating windows instead of disposing them.</summary>
}
