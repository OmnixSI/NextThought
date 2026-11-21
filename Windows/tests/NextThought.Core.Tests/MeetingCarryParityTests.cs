using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from MeetingCarryTests.swift: a custom carry filters only the selected baseline
/// context, and the standard carry keeps the whole workspace unchanged.
/// </summary>
public sealed class MeetingCarryParityTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    [Fact]
    public void CustomCarryFiltersOnlyTheSelectedBaselineContext()
    {
        var baseline = new ContextDocument { Name = "旧资料.pdf", Pages = ["baseline material"] };
        var addedDuringPreparation = new ContextDocument { Name = "新资料.pdf", Pages = ["new material"] };
        var workspace = new WorkspaceSeries("s", "项目系列", Start, [], [new WorkspaceLesson { Text = "保留的经验" }])
        {
            Mode = "meeting",
            Documents = [baseline, addedDuringPreparation],
            QuestionBank = [new QuestionBankItem { Question = "旧问题？", Answer = "旧答案" }]
        };
        var carry = new MeetingCarry { Materials = false, Review = false, Lessons = false, Transcript = true };

        var next = carry.WorkspaceSnapshot(workspace, new HashSet<string> { baseline.Id });

        Assert.Equal([addedDuringPreparation.Id], next.Documents.Select(document => document.Id));
        Assert.Empty(next.Lessons);
        Assert.Empty(next.QuestionBank);
        Assert.DoesNotContain(next.RetrievalDocuments, document => document.Name == "题库（历次复盘）");
        Assert.True(carry.Transcript);

        // The Windows series record does not persist Mac's style/preferences; the snapshot only
        // needs the mode, profile text and the enabled documents for this assertion.
        var archivedContext = new SessionContextSnapshot
        {
            Mode = next.Mode ?? "meeting",
            Profile = next.MaterialText(),
            Documents = next.Documents.Where(document => document.Enabled).ToList()
        };
        Assert.Equal([addedDuringPreparation.Id], archivedContext.Documents.Select(document => document.Id));
        Assert.DoesNotContain(archivedContext.Documents, document => document.Name == baseline.Name);
    }

    [Fact]
    public void StandardCarryKeepsTheWholeWorkspace()
    {
        var baseline = new ContextDocument { Name = "系列资料.pdf", Pages = ["material"] };
        var workspace = new WorkspaceSeries("s", "项目系列", Start, [], [new WorkspaceLesson { Text = "经验" }])
        {
            Mode = "meeting",
            Documents = [baseline],
            QuestionBank = [new QuestionBankItem { Question = "问题？", Answer = "答案" }]
        };

        var next = MeetingCarry.Standard.WorkspaceSnapshot(workspace, new HashSet<string> { baseline.Id });

        Assert.Equal(workspace, next);
    }
}
