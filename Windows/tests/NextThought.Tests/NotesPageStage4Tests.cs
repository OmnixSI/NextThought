using Xunit;

namespace NextThought.Tests;

/// <summary>
/// task-62/E15: the Mac 3.4 NotesPageView header, format selector and tab order. The page skeleton
/// was checked element by element against mac-screens/notes/review-light.png and the Mac source;
/// these assertions keep the new structure from regressing.
/// </summary>
public sealed class NotesPageStage4Tests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "NotesView.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void TheHeaderHasTheMacBreadcrumbTitleFactsAndCopyAction()
    {
        var view = Read("Views", "NotesView.axaml");
        Assert.Contains("{Binding NotesMeetingTitle}", view);
        Assert.Contains("{loc:Tr 所有会议}", view);
        Assert.Contains("{loc:Tr 复盘}", view);
        Assert.Contains("{Binding NotesFactsLine}", view);
        Assert.Contains("{loc:Tr 复制纪要}", view);
        Assert.Contains("CopyNotesCommand", view);
        Assert.Contains("FontSize=\"30\"", view);
        var state = Read("AppState.NotesPage.cs");
        Assert.Contains("NotesBreadcrumb", state);
        Assert.Contains("NotesMeetingTitle", state);
        Assert.Contains("CopyNotesAsync", state);
        Assert.Contains("RefreshNotesHeader", state);
    }

    [Fact]
    public void TheTabsAreTheMacThreePointFourOrder()
    {
        var view = Read("Views", "NotesView.axaml");
        var order = new[] { "{loc:Tr 复盘}", "{loc:Tr 转写}", "{loc:Tr 我的笔记}", "{loc:Tr 会中回答}" };
        var indexes = order.Select(label => view.IndexOf(label, StringComparison.Ordinal)).ToArray();
        Assert.All(indexes, index => Assert.True(index >= 0));
        for (var i = 1; i < indexes.Length; i++) Assert.True(indexes[i - 1] < indexes[i], order[i] + " must follow " + order[i - 1]);
        Assert.DoesNotContain("<TabItem Header=\"{loc:Tr 回答}\"", view);
        Assert.DoesNotContain("<TabItem Header=\"{loc:Tr 个人笔记}\"", view);
    }

    [Fact]
    public void TheFormatSelectorUsesTheMacLabelsInTheMacOrder()
    {
        var view = Read("Views", "NotesView.axaml");
        // components-6: the format selector is the design-system PillTabs, items in Mac order.
        Assert.Contains("<c:PillTabs SelectedIndex=\"{Binding NotesFormatIndex}\"", view);
        foreach (var label in new[] { "复盘", "提纲", "鱼骨图", "观点对照" })
            Assert.Contains("Content=\"{loc:Tr " + label + "}\"", view);
        var order = new[] { "复盘", "提纲", "鱼骨图", "观点对照" }
            .Select(label => view.IndexOf("{loc:Tr " + label + "}", StringComparison.Ordinal)).ToArray();
        for (var i = 1; i < order.Length; i++) Assert.True(order[i - 1] < order[i]);
        Assert.DoesNotContain("GroupName=\"notesformat\"", view);
        Assert.DoesNotContain("NotesFormatIsFishbone", view.Split("notesformat").Length > 1 ? view : "");
    }

    [Fact]
    public void TheImpressionCardUsesTheMacThreePointFourLabel()
    {
        var view = Read("Views", "NotesView.axaml");
        Assert.Contains("{loc:Tr 一句话印象}", view);
        Assert.Contains("IconSparkles", view);
        Assert.DoesNotContain("{loc:Tr 教练小结}", view);
    }

    [Fact]
    public void EveryNewHeaderStringIsLocalized()
    {
        var table = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Core", "UiTextWindows.cs"));
        Assert.Contains("[\"一句话印象\"]", table);
        var wording = File.ReadAllText(Path.Combine(Root(), "Windows", "WORDING.md"));
        Assert.Contains("一句话印象", wording);
    }

    [Fact]
    public void TheAiNewVersionStripOffersBothActions()
    {
        var state = Read("AppState.NotesNewVersion.cs");
        Assert.Contains("RefreshAiNewVersion", state);
        Assert.Contains("ViewNewAiVersion", state);
        Assert.Contains("KeepMyNotesRevision", state);
        Assert.Contains("BasedOnGeneratedAt", state);
        var view = Read("Views", "NotesView.axaml");
        Assert.Contains("{Binding HasAiNewVersion}", view);
        Assert.Contains("{Binding ViewNewAiVersionCommand}", view);
        Assert.Contains("{Binding KeepMyNotesRevisionCommand}", view);
        Assert.Contains("AI 整理出了新版本。现在显示的仍是你改过的版本。", view);
    }

    [Fact]
    public void TheStaleSourceHintUsesTheMacSentence()
    {
        var state = Read("AppState.NotesStatus.cs");
        Assert.Contains("RefreshNotesStatus", state);
        Assert.Contains("transcriptSourceRevision", state);
        Assert.Contains("部分旧内容未记录来源版本；无法确认是否与当前原话和纪要一致。", state);
        var view = Read("Views", "NotesView.axaml");
        Assert.Contains("{Binding HasNotesStaleHint}", view);
        Assert.Contains("{Binding NotesStaleHintText}", view);
    }

    [Fact]
    public void TheReadIssuesRowReportsSkippedLines()
    {
        var state = Read("AppState.NotesStatus.cs");
        Assert.Contains("RefreshNotesReadIssues", state);
        Assert.Contains("已跳过 %lld 行损坏记录，其余内容仍可查看。", state);
        Assert.Contains("ShowNotesReadIssues", state);
        var history = Read("AppState.StructuredNotes.cs");
        Assert.Contains("RefreshNotesReadIssues(data.SkippedLines)", history);
        var view = Read("Views", "NotesView.axaml");
        Assert.Contains("{Binding HasNotesReadIssues}", view);
        Assert.Contains("{Binding NotesReadIssueText}", view);
        Assert.Contains("ShowNotesReadIssuesCommand", view);
    }

    [Fact]
    public void SelectingAHighlightLinksTheEvidence()
    {
        var state = Read("AppState.NotesEvidenceLink.cs");
        Assert.Contains("SelectNotesEvidenceForHighlight", state);
        Assert.Contains("SelectedNotesEvidenceText", state);
        Assert.Contains("HasSelectedNotesEvidence", state);
        var notes = Read("Views", "MeetingUserNotes.axaml.cs");
        Assert.Contains("SelectNotesEvidenceForHighlight", notes);
        var view = Read("Views", "NotesView.axaml");
        Assert.Contains("{Binding HasSelectedNotesEvidence}", view);
        Assert.Contains("{Binding SelectedNotesEvidenceText}", view);
    }

    [Fact]
    public void TheArchiveStatusBarExplainsThePendingJob()
    {
        var state = Read("AppState.NotesStatus.cs");
        Assert.Contains("RefreshNotesArchiveStatus", state);
        Assert.Contains("HasNotesArchiveStatus", state);
        Assert.Contains("正在整理纪要 · 通常 1–3 分钟", state);
        var view = Read("Views", "NotesView.axaml");
        Assert.Contains("{Binding HasNotesArchiveStatus}", view);
        Assert.Contains("{Binding NotesArchiveStatusText}", view);
    }

    [Fact]
    public void TheAnswersTabShowsTheArchiveMetadata()
    {
        var view = Read("Views", "NotesView.axaml");
        Assert.Contains("{Binding HistoryAnswers}", view);
        Assert.Contains("{Binding MetaLine}", view);
        Assert.Contains("{Binding !IsComplete}", view);
        Assert.Contains("{Binding HasScreenshot}", view);
        Assert.Contains("{Binding AutomaticLabel}", view);
    }
}
