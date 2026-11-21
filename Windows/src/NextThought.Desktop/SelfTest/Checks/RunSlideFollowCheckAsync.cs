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
        // E9: the pure following rules, note cache and classification (no screen needed).
        bool rulesPassed = false, cachePassed = false, classifyPassed = false;
        var ruleError = "";
        try
        {
            SlidePage Page(int number, string text, string notes) => new()
            {
                DocumentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Page = number, PageCount = 3, DocumentName = "check.pptx", Text = text, Notes = notes
            };
            var pages = new[]
            {
                Page(1, "Cache consistency read write ordering", "First slide explains cache consistency and read write ordering in detail."),
                Page(2, "Write through and write back", "Second slide compares write through and write back strategies."),
                Page(3, "Retry and fallback", "Third slide covers retry and fallback choices.")
            };
            var now = DateTimeOffset.UtcNow;
            var controller = new SlideFollowController(pages);
            var seen = controller.ObserveScreen(pages[0].Text, now);
            var manual = controller.Step(1, now.AddSeconds(1));
            var heldManual = controller.ObserveScreen(pages[0].Text, now.AddSeconds(2));
            var resumed = controller.ObserveScreen(pages[2].Text, now.AddSeconds(3));
            var speechController = new SlideFollowController(pages);
            var speech = speechController.ObserveSpeech(
                "First slide explains cache consistency and read write ordering in detail", now);
            var graceKept = controller.ObserveScreen("unrelated demo text", now.AddSeconds(4))?.Page == 3;
            var graceExpired = controller.ObserveScreen("unrelated demo text", now.AddSeconds(40)) is null;
            rulesPassed = seen?.Page == 1 && manual?.Page == 2 && heldManual?.Page == 2
                          && resumed?.Page == 3 && speech?.Page == 2 && graceKept && graceExpired;
            if (!rulesPassed) ruleError = $"seen={seen?.Page} manual={manual?.Page} held={heldManual?.Page} resumed={resumed?.Page} speech={speech?.Page} grace={graceKept}/{graceExpired}";
            var directory = Path.Combine(Path.GetTempPath(), "nt-slide-notes-" + Guid.NewGuid().ToString("N"));
            var fingerprint = NextThought.Core.SlideNotesCacheStore.Fingerprint(pages[1], "meeting");
            NextThought.Core.SlideNotesCacheStore.Save(directory, pages[1],
                new NextThought.Core.SlideNotes { Points = ["write through", "write back"] }, fingerprint);
            var loaded = NextThought.Core.SlideNotesCacheStore.Notes(directory, pages[1], fingerprint);
            var mismatch = NextThought.Core.SlideNotesCacheStore.Notes(directory, pages[1], "different");
            cachePassed = loaded is { Points.Count: 2 } && mismatch is null;
            try { Directory.Delete(directory, true); } catch (IOException) { }
            classifyPassed = NextThought.Core.ScreenClassification.Classify("{\n  var x = 1;\n  return x;\n}") == NextThought.Core.ScreenKind.Code
                             && NextThought.Core.ScreenClassification.Classify("Title\nShort line\nAnother short line") == NextThought.Core.ScreenKind.Slides;
        }
        catch (Exception exception) { ruleError = exception.Message; }
        var report = new
        {
            passed = passed && rulesPassed && cachePassed && classifyPassed,
            ocrChars = screenText.Length, matchedPage = matched?.Page,
            screenExcerpt = screenText.Length <= 240 ? screenText : screenText[..240], ranked, error,
            rulesPassed, cachePassed, classifyPassed, ruleError
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
