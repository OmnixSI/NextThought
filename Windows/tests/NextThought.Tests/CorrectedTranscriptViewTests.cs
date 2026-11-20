using System.Text.RegularExpressions;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E17: the 校正原话 (CorrectedTranscript) view exists, shows the corrected/original transcript
/// versions with one undo per applied correction, and is wired to the proofreading decisions.
/// Desktop views cannot be instantiated here (no Avalonia), so this locks the source contract the
/// way the other UI checks in this project do.
/// </summary>
public sealed class CorrectedTranscriptViewTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative));

    [Fact]
    public void TheViewExistsWithTheMacViewName()
    {
        var path = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "CorrectedTranscriptView.axaml");
        Assert.True(File.Exists(path), path);
        var xaml = File.ReadAllText(path);
        Assert.Contains("x:Class=\"NextThought.Desktop.Views.CorrectedTranscript\"", xaml);
        Assert.Contains("校正原话", xaml);
        // Mac NotesPageView CorrectedTranscript: 校正转写 / 原始转写 version segments.
        Assert.Contains("校正转写", xaml);
        Assert.Contains("原始转写", xaml);
        Assert.Contains("{Binding CorrectedTranscriptRows}", xaml);
        Assert.Contains("在转写里查找（听错的写法也会找出来）", xaml);
        Assert.Contains("这场会议没有转写。", xaml);
        Assert.Contains("清空搜索", xaml);
    }

    [Fact]
    public void TheViewUsesTheLocalizationMarkupForEverySentence()
    {
        var xaml = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "CorrectedTranscriptView.axaml"));
        // G9: user-visible copy is a Mac sentence through {loc:Tr ...}; no bare Chinese literals in the view.
        var withoutLocalization = Regex.Replace(xaml, "\\{loc:Tr [^}]*\\}", "");
        Assert.DoesNotMatch(new Regex("[\\u4e00-\\u9fff]"), withoutLocalization);
    }

    [Fact]
    public void TheWindowBuildsRowsFromTheProofreadingDecisions()
    {
        var state = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.Proofread.cs"));
        // Rows are the raw transcript with the user's correction decisions applied (never rewritten).
        Assert.Contains("RefreshCorrectedTranscript", state);
        Assert.Contains("SessionUserContentStore.Load", state);
        Assert.Contains("NotesCorrections.Apply", state);
        Assert.Contains("CorrectedTranscriptRows", state);
        // Mac action labels for the per-line undo / redo.
        Assert.Contains("撤销校对：%@ → %@", state);
        Assert.Contains("恢复校对：%@ → %@", state);
        // Undo persists a decision, it does not edit transcript.jsonl.
        Assert.Contains("CorrectionDecisions", state);
        Assert.Contains("ToggleCorrectedTranscriptFix", state);
        Assert.Contains("已校对 %lld 处识别错误", state);
        Assert.Contains("DemoCorrectedTranscriptCorrections", state);
    }

    [Fact]
    public void TheWindowIsReachableFromTheAppStateCommand()
    {
        var state = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.Proofread.cs"));
        Assert.Contains("OpenCorrectedTranscript", state);
        Assert.Contains("CorrectedTranscript.ShowFor", state);
        var window = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "CorrectedTranscriptView.axaml.cs"));
        Assert.Contains("class CorrectedTranscript : Window", window);
        Assert.Contains("ShowFor", window);
        Assert.Contains("ToggleCorrectedTranscriptFixCommand", window);
        Assert.Contains("ClearFind", window);
    }

    [Fact]
    public void TheNativeEnglishStringsExistInTheMacCatalogue()
    {
        var catalogue = Read(Path.Combine("Windows", "src", "NextThought.Core", "UiTextMac.g.cs"));
        foreach (var key in new[]
                 {
                     "校正原话", "校正转写", "原始转写", "转写版本", "撤销校对：%@ → %@", "恢复校对：%@ → %@",
                     "已校对 %lld 处识别错误", "这场会议没有转写。", "在转写里查找（听错的写法也会找出来）",
                     "查看校正转写", "查看原始转写", "清空搜索", "关闭"
                 })
            Assert.Contains($"[\"{key}\"]", catalogue);
    }
}
