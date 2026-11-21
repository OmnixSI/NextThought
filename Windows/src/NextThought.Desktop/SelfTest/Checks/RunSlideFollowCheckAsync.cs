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
        // The slide page text the matcher should match against the OCR result.
        const string probeText = "缓存一致性 读写时序方案 重试与降级";
        try
        {
            // Render the "slide" offscreen instead of screenshotting a probe window: the previous
            // capture depended on window Position/render scaling and, under 150% scaling + Topmost,
            // cropped whatever window was on screen instead of the probe.
            var probe = new Avalonia.Controls.TextBlock
            {
                Text = "缓存一致性\n读写时序方案\n重试与降级",
                FontSize = 54,
                Foreground = Avalonia.Media.Brushes.Black,
                Background = Avalonia.Media.Brushes.White,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            var probeSize = new Avalonia.Size(900, 260);
            probe.Measure(probeSize);
            probe.Arrange(new Avalonia.Rect(probeSize));
            var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
                new Avalonia.PixelSize((int)probeSize.Width, (int)probeSize.Height),
                new Avalonia.Vector(96, 96));
            bitmap.Render(probe);
            var imagePath = path + ".png";
            bitmap.Save(imagePath, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

            var reader = new DocumentReader();
            var (blocks, warnings) = await reader.ReadAsync(imagePath);
            screenText = string.Join("\n", blocks.Select(block => block.Text));
            var pages = new[] { new SlidePage { Page = 1, PageCount = 2, DocumentName = "check.pptx", Text = probeText } };
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
            try { } catch { }
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
            var resumed = controller.ObserveScreen(pages[2].Text + " " + pages[2].Notes, now.AddSeconds(3));
            var speechController = new SlideFollowController(pages);
            var speech = speechController.ObserveSpeech(pages[0].Notes, now);
            var graceKept = controller.ObserveScreen("unrelated demo text", now.AddSeconds(4))?.Page == 3;
            var graceExpired = controller.ObserveScreen("unrelated demo text", now.AddSeconds(40)) is null;
            rulesPassed = seen?.Page == 1 && manual?.Page == 2 && heldManual?.Page == 2
                          && resumed?.Page == 3 && speech?.Page == 1 && graceKept && graceExpired;
            if (!rulesPassed) ruleError = $"seen={seen?.Page} manual={manual?.Page} held={heldManual?.Page} resumed={resumed?.Page} speech={speech?.Page} grace={graceKept}/{graceExpired}";
            var directory = Path.Combine(Path.GetTempPath(), "nt-slide-notes-" + Guid.NewGuid().ToString("N"));
            var fingerprint = NextThought.Core.SlideNotesCacheStore.Fingerprint(pages[1], "meeting");
            NextThought.Core.SlideNotesCacheStore.Save(directory, pages[1],
                new NextThought.Core.SlideNotes { Points = ["write through", "write back"] }, fingerprint);
            var loaded = NextThought.Core.SlideNotesCacheStore.Notes(directory, pages[1], fingerprint);
            var mismatch = NextThought.Core.SlideNotesCacheStore.Notes(directory, pages[1], "different");
            cachePassed = loaded is { Points.Count: 2 } && mismatch is null;
            try { Directory.Delete(directory, true); } catch (IOException) { }
            classifyPassed = NextThought.Core.ScreenClassification.Classify(
                                 "public static int Add(int a, int b)\n{\n    var sum = a + b;\n    Console.WriteLine(sum);\n    return sum;\n}") == NextThought.Core.ScreenKind.Code
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
