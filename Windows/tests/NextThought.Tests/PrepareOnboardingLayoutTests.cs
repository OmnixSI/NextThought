using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E2/E3 structure tests: the prepare page keeps the Mac layout pieces and the first-run guide is a
/// dedicated view (the old inline overlay in MainWindow.axaml is gone).
/// </summary>
public sealed class PrepareOnboardingLayoutTests
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

    private static string Desktop => Path.Combine(Root(), "Windows", "src", "NextThought.Desktop");

    [Fact]
    public void PreparePageKeepsTheMacLayoutPieces()
    {
        var xaml = File.ReadAllText(Path.Combine(Desktop, "Views", "PrepareView.axaml"));
        // D6 pieces that must survive an E2 rewrite.
        Assert.Contains("InkWash", xaml, StringComparison.Ordinal);
        Assert.Contains("Animated=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SceneGlyph", xaml, StringComparison.Ordinal);
        Assert.Contains("Size=\"44\"", xaml, StringComparison.Ordinal);
        // Mac PrepareComposer / TypePicker / SeriesCarryCard / SeriesBriefingPanel / Materials / CheckCard / StartBar.
        foreach (var element in new[]
                 {
                     "给这场会起个名字", "系列与资料", "系列简报", "带入资料", "带入题库", "带入经验", "带入上一场转写",
                     "PrepareCarrySummary", "PrepareMaterialsLine", "PrepareAudioReady", "ImportPreparedDocumentsCommand",
                     "CancelDraftCommand", "开始记录"
                 })
            Assert.Contains(element, xaml, StringComparison.Ordinal);
        var state = File.ReadAllText(Path.Combine(Desktop, "AppState.Prepare.cs"));
        Assert.Contains("PreparedMaterialCount", state, StringComparison.Ordinal);
        Assert.Contains("ImportPreparedDocumentsAsync", state, StringComparison.Ordinal);
        // The start path builds the ported MeetingPlan, like Mac startPreparedMeeting.
        var live = File.ReadAllText(Path.Combine(Desktop, "AppState.Live.cs"));
        Assert.Contains("BeginPreparedMeeting(planSeries)", live, StringComparison.Ordinal);
    }

    [Fact]
    public void FirstRunGuideIsItsOwnView()
    {
        var view = Path.Combine(Desktop, "Views", "OnboardingView.axaml");
        Assert.True(File.Exists(view), view);
        var xaml = File.ReadAllText(view);
        foreach (var element in new[]
                 {
                     "OnboardingIsScene", "OnboardingIsSound", "OnboardingIsAnswers", "OnboardingIsStart",
                     "OnboardingPosition", "OnboardingStepTitle", "OnboardingStepTime", "稍后继续",
                     "看一场示例会议", "OnboardingSampleToggle", "提词岛示意", "提词岛示意", "设置清单",
                     "先看看", "开始第一场", "回到声音"
                 })
            Assert.Contains(element, xaml, StringComparison.Ordinal);
        var window = File.ReadAllText(Path.Combine(Desktop, "Views", "MainWindow.axaml"));
        Assert.Contains("<views:OnboardingView", window, StringComparison.Ordinal);
        Assert.DoesNotContain("OnboardingStepTitle", window, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Desktop, "Views", "OnboardingView.axaml.cs")));
        Assert.True(File.Exists(Path.Combine(Desktop, "SelfTest", "Checks", "RunPrepareCheckAsync.cs")));
        Assert.True(File.Exists(Path.Combine(Desktop, "SelfTest", "Checks", "RunOnboardingCheckAsync.cs")));
    }
}
