namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunWindowShotCheckAsync(string path)
    {
        var probe = new Window
        {
            Width = 360, Height = 240, Background = Avalonia.Media.Brushes.Red,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        var passed = false;
        var bounds = default(Avalonia.PixelRect);
        (byte Red, byte Green, byte Blue) center = (0, 0, 0);
        var error = "";
        try
        {
            probe.Show();
            probe.Activate();
            await Task.Delay(450);
            bounds = ScreenCapture.ForegroundWindowBounds();
            var capture = ScreenCapture.VirtualScreen();
            var cropped = ScreenCapture.Crop(capture, new Avalonia.PixelRect(
                bounds.X - capture.X, bounds.Y - capture.Y, bounds.Width, bounds.Height));
            using (var locked = cropped.Lock())
            {
                var offset = (cropped.PixelSize.Height / 2) * locked.RowBytes + (cropped.PixelSize.Width / 2) * 4;
                center = (System.Runtime.InteropServices.Marshal.ReadByte(locked.Address, offset + 2),
                          System.Runtime.InteropServices.Marshal.ReadByte(locked.Address, offset + 1),
                          System.Runtime.InteropServices.Marshal.ReadByte(locked.Address, offset));
            }
            passed = bounds.Width > 100 && bounds.Height > 100 && center.Item1 > 200 && center.Item2 < 60 && center.Item3 < 60;
        }
        catch (Exception ex) { error = ex.Message; }
        finally
        {
            try { probe.Close(); } catch { }
            Show(); Activate();
        }
        var report = new
        {
            passed, width = bounds.Width, height = bounds.Height,
            centerRed = center.Item1, centerGreen = center.Item2, centerBlue = center.Item3, error
        };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "window-shot-check: passed" : "window-shot-check: failed");
        Close();
    }
}
