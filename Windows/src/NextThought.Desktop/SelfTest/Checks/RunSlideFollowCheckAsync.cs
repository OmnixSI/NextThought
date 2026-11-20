namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunSlideFollowCheckAsync(string path)
    {
        await Task.Delay(300);
        var passed = false;
        var error = "";
        var screenText = "";
        SlidePage? matched = null;
        List<object> ranked = [];
        var window = new Window
        {
            Width = 900,
            Height = 260,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            Content = new TextBlock
            {
                Text = "缓存一致性 读写时序 方案",
                FontSize = 56,
                Margin = new Thickness(40),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            }
        };
        try
        {
            window.Show();
            await Task.Delay(800);
            var capture = ScreenCapture.VirtualScreen();
            // Crop to the probe so the main window and desktop text do not drown the slide OCR.
            var scale = window.RenderScaling is > 0 and var value ? value : 1;
            var crop = ScreenCapture.Crop(capture, new Avalonia.PixelRect(
                Math.Clamp(window.Position.X - capture.X, 0, capture.Width - 1),
                Math.Clamp(window.Position.Y - capture.Y, 0, capture.Height - 1),
                Math.Min((int)(window.Width * scale), capture.Width),
                Math.Min((int)(window.Height * scale), capture.Height)));
            var imagePath = path + ".png";
            crop.Save(imagePath, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            window.Close();
            var reader = new DocumentReader();
            var (blocks, warnings) = await reader.ReadAsync(imagePath);
            screenText = string.Join("\n", blocks.Select(block => block.Text));
            var pages = new[] { new SlidePage { Page = 1, PageCount = 2, DocumentName = "check.pptx", Text = "缓存一致性 读写时序 方案" } };
            var follower = new SlideFollower(pages);
            matched = follower.Observe(screenText);
            ranked = NextThought.Core.SlideMatcher.Rank(screenText, pages)
                .Select(match => (object)new { page = match.Page.Page, score = Math.Round(match.Score, 3) }).ToList();
            passed = matched?.Page == 1;
            if (!passed) error = warnings.Count > 0 ? string.Join("；", warnings) : "OCR 文本未匹配到幻灯片。";
        }
        catch (Exception exception)
        {
            error = exception.Message;
            try { window.Close(); } catch { }
        }
        var report = new
        {
            passed, ocrChars = screenText.Length, matchedPage = matched?.Page,
            screenExcerpt = screenText.Length <= 240 ? screenText : screenText[..240], ranked, error
        };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "slide-follow-check: passed" : "slide-follow-check: failed");
        Close();
    }


    /// <summary>Installs the streaming recognizer and (when enabled) the offline refinement model.</summary>
}
