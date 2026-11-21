using Xunit;

namespace NextThought.Tests;

/// <summary>
/// task-62/①: the Mac MeetingNotesEditor structured body. When a meeting has a template document
/// (summary.notes or the user's editedNotes) the editor lists sections and items with a TextBox, the
/// item's time caption and a 「我已核实」 button for uncertain items; saving writes the document back
/// to user-content.json and re-renders the Markdown.
/// </summary>
public sealed class StructuredNotesEditorTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "NotesEditorWindow.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void TheEditorWindowHasTheStructuredBodyAndConfirmButton()
    {
        var xaml = Read("Views", "NotesEditorWindow.axaml");
        Assert.Contains("NotesEditorStructured", xaml);
        Assert.Contains("NotesEditorSections", xaml);
        Assert.Contains("{Binding Title}", xaml);
        Assert.Contains("{Binding Text, Mode=TwoWay}", xaml);
        Assert.Contains("{Binding Time}", xaml);
        Assert.Contains("{Binding HasTime}", xaml);
        Assert.Contains("{loc:Tr 我已核实}", xaml);
        Assert.Contains("{Binding Uncertain}", xaml);
        Assert.Contains("ConfirmNoteItem_Click", xaml);
        Assert.Contains("NotesEditorPlainText", xaml);
        var code = Read("Views", "NotesEditorWindow.axaml.cs");
        Assert.Contains("ConfirmNotesEditorItem", code);
    }

    [Fact]
    public void TheEditorLoadsTheTemplateDocumentAndPersistsTheUserVersion()
    {
        var state = Read("AppState.NotesStructuredEditor.cs");
        Assert.Contains("EffectiveTemplateNotes", state);
        Assert.Contains("LoadSummaryNotes", state);
        Assert.Contains("LoadStructuredNotesEditor", state);
        Assert.Contains("BuildEditedNotesDocument", state);
        Assert.Contains("SaveStructuredNotesEditorAsync", state);
        Assert.Contains("StructuredNotesEditing.WithEditedNotes", state);
        Assert.Contains("NotesMarkdown.Render", state);
        var editor = Read("AppState.NotesEditor.cs");
        Assert.Contains("LoadStructuredNotesEditor(meta, content);", editor);
        Assert.Contains("SaveStructuredNotesEditorAsync(meta, content);", editor);
    }
}
