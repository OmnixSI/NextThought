using Avalonia.Threading;
using NextThought.Core;
using NextThought.Desktop.Localization;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// G8: proves that XAML text is fetched through the <c>{loc:Tr …}</c> markup extension and that a
    /// language switch updates already-open controls in place (no page reload).
    /// </summary>
    private async Task RunUiLanguageCheckAsync(string path)
    {
        await Task.Delay(400);
        var source = UiTextSource.Instance;
        var previous = source.Preference;
        var failures = new List<string>();
        string[] Normalized() => LocalizationAudit.Collect(this).Select(row => row.Replace('\u0001', '\t')).ToArray();
        try
        {
            source.SetPreference(UiTextSource.ChinesePreference);
            await PumpAsync();
            var dialog = new ConfirmDialog();
            if (dialog.Title != "确认") failures.Add("confirm-dialog Chinese title: " + dialog.Title);
            var chinese = Normalized();
            if (!chinese.Any(row => row.EndsWith("\t首页", StringComparison.Ordinal)))
                failures.Add("Chinese navigation label 首页 not found");
            if (!chinese.Any(row => row.EndsWith("\t设置", StringComparison.Ordinal)))
                failures.Add("Chinese navigation label 设置 not found");

            source.SetPreference(UiTextSource.EnglishPreference);
            await PumpAsync();
            if (dialog.Title != "Confirm") failures.Add("confirm-dialog English title did not update in place: " + dialog.Title);
            var english = Normalized();
            if (!english.Any(row => row.EndsWith("\tHome", StringComparison.Ordinal)))
                failures.Add("English navigation label Home not found");
            if (!english.Any(row => row.EndsWith("\tSettings", StringComparison.Ordinal)))
                failures.Add("English navigation label Settings not found");
            if (english.Any(row => row.EndsWith("\t首页", StringComparison.Ordinal)))
                failures.Add("English page still shows the 首页 navigation label after switching language");

            var islandTitle = island?.Title ?? "";
            if (islandTitle.Length > 0 && islandTitle != "NextThought Cue island")
                failures.Add("cue island title not localized: " + islandTitle);
            var modelChoice = state.Settings.ModelChoicesList.FirstOrDefault()?.ToString() ?? "";
            if (modelChoice.Length > 0 && !modelChoice.Contains("bilingual", StringComparison.Ordinal))
                failures.Add("model choice not localized: " + modelChoice);

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
                .Where(pair => UiText.Translate(pair.Item1, true) != pair.Item2)
                .Select(pair => pair.Item1).ToArray();
            if (missingSamples.Length > 0) failures.Add("missing translations: " + string.Join(", ", missingSamples));
            if (UiText.EnglishStrings.Count < 2600) failures.Add("string table too small: " + UiText.EnglishStrings.Count);

            var report = new Dictionary<string, object?>
            {
                ["chineseCount"] = chinese.Length,
                ["englishCount"] = english.Length,
                ["englishRows"] = english,
                ["dialogTitleZh"] = "确认",
                ["dialogTitleEn"] = dialog.Title,
                ["uiStrings"] = UiText.EnglishStrings.Count,
                ["windowsStrings"] = UiText.WindowsStringCount,
                ["passed"] = failures.Count == 0,
                ["failures"] = failures
            };
            await WriteReportAsync(path, report);
            Console.WriteLine(failures.Count == 0
                ? "ui-language-check: passed"
                : "ui-language-check: failed " + string.Join("; ", failures));
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("ui-language-check: " + error);
            throw;
        }
        finally
        {
            source.SetPreference(previous);
            await PumpAsync();
        }
        Close();
    }

    private static async Task WriteReportAsync(string path, object report)
    {
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
    }

    private static async Task PumpAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await Task.Delay(60);
    }
}
