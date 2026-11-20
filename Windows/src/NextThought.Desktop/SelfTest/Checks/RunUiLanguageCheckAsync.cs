namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunUiLanguageCheckAsync(string path)
    {
        await Task.Delay(400);
        UiTranslation.Apply(this, true);
        var texts = this.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>()
            .Select(block => block.Text ?? "").ToHashSet(StringComparer.Ordinal);
        var labels = new[] { "Home", "Meetings", "Notes", "History", "Series", "Library", "Settings" };
        var found = labels.Where(texts.Contains).ToArray();
        UiTranslation.Apply(this, false);
        var back = this.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>()
            .Select(block => block.Text ?? "").ToHashSet(StringComparer.Ordinal);
        var islandTitle = island?.Title ?? "";
        var modelChoice = state.Settings.ModelChoicesList.FirstOrDefault()?.ToString() ?? "";
        var translatedSamples = new[]
        {
            ("云端识别（实验）", "Cloud transcription (experimental)"),
            ("全局快捷键", "Global shortcuts"),
            ("接口预设", "API preset"),
            ("外观", "Appearance"),
            ("声音来源", "Audio sources"),
            ("本机语音识别", "On-device speech")
        };
        var missingSamples = translatedSamples
            .Where(pair => NextThought.Core.UiText.Translate(pair.Item1, true) != pair.Item2)
            .Select(pair => pair.Item1).ToArray();
        var uiStrings = NextThought.Core.UiText.EnglishStrings.Count;
        var passed = found.Length == labels.Length && back.Contains("首页") && back.Contains("设置")
                     && (islandTitle.Length == 0 || islandTitle.Contains("Cue island", StringComparison.Ordinal))
                     && (modelChoice.Length == 0 || modelChoice.Contains("bilingual", StringComparison.Ordinal))
                     && uiStrings >= 2000 && missingSamples.Length == 0;
        var report = new { passed, found, missing = labels.Except(found).ToArray(), reversed = back.Contains("首页"), islandTitle, modelChoice, uiStrings, missingSamples };
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
        Console.WriteLine(passed ? "ui-language-check: passed" : "ui-language-check: failed");
        Close();
    }

    /// <summary>QA: the window-capture crop really contains the foreground window's pixels.</summary>
}
