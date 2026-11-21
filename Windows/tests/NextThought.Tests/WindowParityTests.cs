using Xunit;

namespace NextThought.Tests;

/// <summary>
/// §6.7 second batch: the four windows with no Mac screenshots — subtitle bar (E14), answer window
/// (E29), export sheet (E21) and the backup restore review (E22). Source-scan contract tests of the
/// elements, copy and behaviours each Swift source defines.
/// </summary>
public sealed class WindowParityTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void E14TheSubtitleBarHasTheMacCardAndCopy()
    {
        var xaml = Read("Views", "SubtitleBarWindow.axaml");
        Assert.Contains("NextThought 字幕条", xaml);
        // Space.radiusLarge card with the shadow insets (16/20/28) and the shared badge.
        Assert.Contains("CornerRadius=\"{DynamicResource D4CornerRadius_22}\"", xaml);
        Assert.Contains("Margin=\"{DynamicResource D4Margin_20_16_20_28}\"", xaml);
        Assert.Contains("对方可见", xaml);
        Assert.Contains("继续记录", xaml);
        Assert.Contains("MaxLines=\"2\"", xaml);
        var code = Read("Views", "SubtitleBarWindow.axaml.cs");
        Assert.Contains("SubtitleBarTiming", code);
        Assert.Contains("HoverDimSeconds = 0.6", code);
        Assert.Contains("MaxCardWidth = 900", code);
        Assert.Contains("TextZoomService.Scaled", code);
        Assert.Contains("CueIslandKeepRecording", code);
    }

    [Fact]
    public void E22TheGlossarySheetPrefillsAndShowsTheSeriesScope()
    {
        var sheet = Read("Views", "AddToGlossarySheet.axaml");
        // glossary 3: 只要有任意系列就显示「只用于当前系列」.
        Assert.Contains("IsVisible=\"{Binding HasSeries}\"", sheet);
        var code = Read("Views", "AddToGlossarySheet.axaml.cs");
        // glossary 2: 打开时读剪贴板预填（Avalonia 剪贴板接口）.
        Assert.Contains("GetTextAsync", code);
        Assert.Contains("GlossaryDraftHeard", code);
        // glossary 1: Mac 的 24/16 间距.
        Assert.Contains("D4Spacing_16", sheet);
        Assert.Contains("D4Margin_24", sheet);
    }

    [Fact]
    public void E29TheAnswerWindowOffersClarifyAndFollowUps()
    {
        var xaml = Read("Views", "TeleprompterWindow.axaml");
        Assert.Contains("MoreClarifyItem", xaml);
        Assert.Contains("澄清问题", xaml);
        Assert.Contains("MoreFollowUpsItem", xaml);
        Assert.Contains("我可以反问", xaml);
    }

    [Fact]
    public void E4TheErrorBannerRetriesBlockedArchiveSaves()
    {
        var shell = Read("Views", "MainWindow.axaml");
        Assert.Contains("RetryPersistenceCommand", shell);
        Assert.Contains("AcknowledgeSaveLossCommand", shell);
        Assert.Contains("ShowRetrySave", shell);
        Assert.Contains("ErrorBannerText", shell);
        var state = Read("AppState.Shell.cs");
        Assert.Contains("ShowErrorBanner", state);
        Assert.Contains("SaveIsBlocked", state);
    }

    [Fact]
    public void E4TheShellHasNoBottomStatusBar()
    {
        var shell = Read("Views", "MainWindow.axaml");
        // decisions §3.1: 去掉窗口底部常驻文字条（含演示模式/快捷键冲突）.
        Assert.DoesNotContain("取消当前任务", shell);
        var demo = Read("AppState.Demo.cs");
        Assert.DoesNotContain("演示模式", demo);
    }

    [Fact]
    public void E29TheAnswerWindowHasTheMacHeaderBodyAndFooter()
    {
        var xaml = Read("Views", "TeleprompterWindow.axaml");
        Assert.Contains("NextThought 回答浮窗", xaml);
        Assert.Contains("回到最新", xaml);
        Assert.Contains("更简短", xaml);
        Assert.Contains("举例", xaml);
        Assert.Contains("英文版", xaml);
        Assert.Contains("停止生成", xaml);
        Assert.Contains("立即回答", xaml);
        Assert.Contains("MarkdownText", xaml);
        Assert.Contains("CornerRadius=\"{DynamicResource D4CornerRadius_22}\"", xaml);
        var code = Read("Views", "TeleprompterWindow.axaml.cs");
        Assert.Contains("正在听", code);
        // The three Mac placements: camera top centre, bottom, right.
        Assert.Contains("\"bottom\"", code);
        Assert.Contains("\"right\"", code);
        Assert.Contains("24 * scale", code);
        Assert.Contains("16 * scale", code);
        Assert.Contains("8 * scale", code);
        Assert.Contains("TeleprompterFontSize", code);
    }

    [Fact]
    public void E21TheExportSheetHasPickersInfoWarningsPreviewAndDefaultAction()
    {
        var xaml = Read("Views", "SessionExportSheet.axaml");
        Assert.Contains("controls:MenuPicker", xaml);
        Assert.Contains("格式", xaml);
        Assert.Contains("转写", xaml);
        Assert.Contains("语言", xaml);
        Assert.Contains("归档有缺失或读取错误，导出已暂停。", xaml);
        Assert.Contains("没有可导出的内容。", Read("Views", "SessionExportSheet.axaml.cs"));
        Assert.Contains("导出…", xaml);
        var code = Read("Views", "SessionExportSheet.axaml.cs");
        Assert.Contains("%lld 段结束时间为估算值", code);
        Assert.Contains("%lld 条译文可能已过期", code);
        Assert.Contains("Key.Enter", code);
        Assert.Contains("ExportButton.IsEnabled", code);
    }

    [Fact]
    public void E22TheBackupReviewHasStatsRowsAndApply()
    {
        var xaml = Read("Views", "BackupRestoreReviewView.axaml");
        Assert.Contains("导入与恢复预览", xaml);
        Assert.Contains("新增记录", xaml);
        Assert.Contains("相同记录", xaml);
        Assert.Contains("冲突记录", xaml);
        Assert.Contains("备份模板", xaml);
        Assert.Contains("系列按修改时间合并", xaml);
        Assert.Contains("自定义纪要模板", xaml);
        Assert.Contains("使用备份", xaml);
        Assert.Contains("同时导入偏好设置", xaml);
        Assert.Contains("应用所选内容", xaml);
        Assert.Contains("正在恢复，请保留此窗口…", xaml);
        // The row status texts are the Mac originals.
        Assert.Contains("新增 · 将导入", Read("Views", "BackupRestoreReviewView.axaml.cs"));
        Assert.Contains("内容不同 · 默认保留本机", Read("Views", "BackupRestoreReviewView.axaml.cs"));
    }

    [Fact]
    public void TheWindowsSelfTestCoversTheFourWindows()
    {
        var check = Read("SelfTest", "Checks", "RunWindowsCheckAsync.cs");
        foreach (var name in new[] { "subtitle-bar", "teleprompter", "export", "backup-review" })
            Assert.Contains("\"" + name + "\"", check);
        Assert.Contains("windows-check", check);
    }
}
