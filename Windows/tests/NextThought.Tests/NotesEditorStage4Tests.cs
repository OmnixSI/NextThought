using Xunit;

namespace NextThought.Tests;

/// <summary>
/// task-78/§3.4 MeetingNotesEditor items 3 and 4: Mac alert wording, the busy save label, and
/// basedOnGeneratedAt taken from summary.json's AI generatedAt. Item 1 (structured item editing) is
/// covered by StructuredNotesEditorTests; the evidence-sidebar context work is recorded as pending.
/// </summary>
public sealed class NotesEditorStage4Tests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "AppState.NotesEditor.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void TheDiscardAlertUsesTheMacLabels()
    {
        var dialog = Read("Views", "ConfirmDialog.axaml.cs");
        Assert.Contains("string confirmLabel, string cancelLabel", dialog);
        var window = Read("Views", "NotesEditorWindow.axaml.cs");
        Assert.Contains("放弃更改", window);
        Assert.Contains("继续编辑", window);
        Assert.Contains("放弃尚未保存的更改？", window);
    }

    [Fact]
    public void TheSaveButtonShowsTheBusyLabelAndUsesTheAiDraftTime()
    {
        var state = Read("AppState.NotesEditor.cs");
        Assert.Contains("NotesEditorSaveLabel", state);
        Assert.Contains("正在保存…", state);
        Assert.Contains("notesEditorGeneratedAt = LoadSummaryGeneratedAt(meta);", state);
        Assert.Contains("LoadSummaryGeneratedAt", state);
        var structured = Read("AppState.NotesStructuredEditor.cs");
        Assert.Contains("LoadSummaryGeneratedAt(meta)", structured);
        var view = Read("Views", "NotesEditorWindow.axaml");
        Assert.Contains("{Binding NotesEditorSaveLabel}", view);
    }

    [Fact]
    public void TheEvidenceSidebarShowsContextAndCanMarkTheLine()
    {
        var row = Read("SessionEvidenceRow.cs");
        Assert.Contains("ContextText", row);
        Assert.Contains("HasMissing", row);
        Assert.Contains("此原话无法定位。已有笔记仍保留，请查看完整转写核实。", row);
        var state = Read("AppState.cs");
        Assert.Contains("lines[position].Text", state);
        Assert.Contains("SessionEvidenceRow(reference, context, index >= 0)", state);
        var view = Read("Views", "NotesEditorWindow.axaml");
        Assert.Contains("{Binding ContextText}", view);
        Assert.Contains("{Binding MissingText}", view);
        Assert.Contains("MarkEvidenceHighlight_Click", view);
        var code = Read("Views", "NotesEditorWindow.axaml.cs");
        Assert.Contains("HighlightLineCommand.Execute", code);
    }
}
