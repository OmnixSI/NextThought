using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// task-62/①: the Mac MeetingNotesEditor edits a template document item by item. These tests pin the
/// data model half: one item edit leaves the rest untouched and re-renders, 「我已核实」 clears only
/// that item's uncertain flag, the edited document round-trips through user-content.json's Mac
/// editedNotes field, and an old Windows structured-view payload under editedNotes migrates to its
/// own field instead of being mistaken for the template document.
/// </summary>
public sealed class StructuredNotesEditingTests
{
    private static StructuredNotesDoc Doc() => new(
        "interview-review", "面试复盘", "zh", "后端二面",
        [
            new NotesSectionResult("impression", "一句话印象", NotesSectionKind.OneLine, false,
                [new NotesItem { Text = "项目讲得扎实。", Time = "00:10" }]),
            new NotesSectionResult("questions", "逐题回放", NotesSectionKind.Questions, false,
            [
                new NotesItem { Text = "如果缓存失败？", Time = "18:22", Uncertain = true,
                    Answer = "重试队列。", Verdict = "good", Better = "先给结论。" },
                new NotesItem { Text = "热点 key 怎么办？", Time = "27:05" }
            ])
        ]);

    [Fact]
    public void EditingOneItemKeepsTheRestAndRendersTheNewText()
    {
        var edited = StructuredNotesEditing.EditItem(Doc(), 1, 0, "缓存失败了怎么处理？");
        Assert.Equal("缓存失败了怎么处理？", edited.Sections[1].Items[0].Text);
        Assert.Equal("重试队列。", edited.Sections[1].Items[0].Answer);
        Assert.Equal("项目讲得扎实。", edited.Sections[0].Items[0].Text);
        var markdown = NotesMarkdown.Render(edited, "后端二面", "");
        Assert.Contains("缓存失败了怎么处理？", markdown);
        Assert.DoesNotContain("如果缓存失败？", markdown);
    }

    [Fact]
    public void ConfirmingAnUncertainItemClearsOnlyThatFlag()
    {
        var confirmed = StructuredNotesEditing.Confirm(Doc(), 1, 0);
        Assert.False(confirmed.Sections[1].Items[0].Uncertain);
        Assert.Equal("缓存失败了怎么处理？", StructuredNotesEditing.EditItem(confirmed, 1, 0, "缓存失败了怎么处理？")
            .Sections[1].Items[0].Text is { Length: > 0 } ? "缓存失败了怎么处理？" : "");
        // The other item keeps its state and the sections are not reordered.
        Assert.Equal("热点 key 怎么办？", confirmed.Sections[1].Items[1].Text);
        Assert.Equal("一句话印象", confirmed.Sections[0].Title);
    }

    [Fact]
    public void TheEditedDocumentRoundTripsThroughUserContent()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-edited-notes-" + Guid.NewGuid().ToString("N"));
        try
        {
            var edited = StructuredNotesEditing.EditItem(Doc(), 0, 0, "项目讲得扎实，数字也清楚。");
            var saved = SessionUserContentStore.Save(new SessionUserContent { EditedNotes = edited }, root);
            var reopened = SessionUserContentStore.Load(root);
            Assert.NotNull(reopened.EditedNotes);
            Assert.Equal("项目讲得扎实，数字也清楚。", reopened.EditedNotes!.Sections[0].Items[0].Text);
            Assert.Equal("逐题回放", reopened.EditedNotes.Sections[1].Title);
            Assert.Equal(saved.Revision, reopened.Revision);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void AnOldStructuredViewPayloadUnderEditedNotesMovesToItsOwnField()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-edited-notes-legacy-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, SessionUserContent.FileName),
                "{\"schemaVersion\":1,\"revision\":0,\"editedNotes\":{\"format\":\"outline\",\"outline\":{\"sections\":[]}}}");
            var content = SessionUserContentStore.Load(root);
            Assert.Null(content.EditedNotes);
            Assert.NotNull(content.EditedStructuredViews);
            Assert.Equal(NotesFormat.Outline, content.EditedStructuredViews!.Format);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
