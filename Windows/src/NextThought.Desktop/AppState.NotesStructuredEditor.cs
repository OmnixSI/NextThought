using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// task-62/①: the Mac MeetingNotesEditor body. When the meeting has a template document (generated
/// summary.notes, or the user's editedNotes), the editor shows one TextBox per item with its time
/// caption and a 「我已核实」 button on items the model flagged as uncertain; saving writes the
/// document back to user-content.json and the page re-renders its Markdown.
/// </summary>
public partial class AppState
{
    public ObservableCollection<NotesEditorSectionRow> NotesEditorSections { get; } = [];

    [ObservableProperty] private bool notesEditorStructured;

    /// <summary>The plain Markdown text editor is only shown when no template document exists.</summary>
    public bool NotesEditorPlainText => !NotesEditorStructured && !NotesEditorShowOriginal;

    private StructuredNotesDoc? notesEditorStructuredDoc;
    private bool notesEditorStructuredDirty;

    partial void OnNotesEditorStructuredChanged(bool value)
    {
        OnPropertyChanged(nameof(NotesEditorPlainText));
        OnPropertyChanged(nameof(NotesEditorDirty));
        OnPropertyChanged(nameof(NotesEditorDirtyText));
    }

    /// <summary>Mac: detail.effectiveNotes = the user's editedNotes, else the generated summary.notes.</summary>
    public StructuredNotesDoc? EffectiveTemplateNotes()
    {
        if (CurrentSession is not { } meta) return null;
        try
        {
            var content = SessionUserContentStore.Load(Sessions.Folder(meta.Id));
            return content.EditedNotes ?? LoadSummaryNotes(meta);
        }
        catch { return null; }
    }

    private StructuredNotesDoc? LoadSummaryNotes(SessionMeta meta)
    {
        try
        {
            var path = Path.Combine(Sessions.Folder(meta.Id), "summary.json");
            if (!File.Exists(path)) return null;
            if (System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path)) is not System.Text.Json.Nodes.JsonObject summary) return null;
            return summary["notes"] is { } notes
                ? notes.Deserialize<StructuredNotesDoc>(JsonDefaults.ArchiveOptions)
                : null;
        }
        catch { return null; }
    }

    /// <summary>Self-check/capture hook: show the structured editor body for a synthetic document.</summary>
    public void ShowStructuredNotesEditorForCheck(StructuredNotesDoc doc)
    {
        notesEditorStructuredDoc = doc;
        NotesEditorStructured = true;
        NotesEditorSections.Clear();
        for (var section = 0; section < doc.Sections.Count; section++)
        {
            var row = new NotesEditorSectionRow(doc.Sections[section].Title);
            for (var item = 0; item < doc.Sections[section].Items.Count; item++)
            {
                var value = doc.Sections[section].Items[item];
                row.Items.Add(new NotesEditorItemRow(section, item, value.Text, value.Time, value.Uncertain,
                    () => MarkNotesEditorStructuredDirty()));
            }
            NotesEditorSections.Add(row);
        }
        notesEditorStructuredDirty = false;
        OnPropertyChanged(nameof(NotesEditorPlainText));
    }

    private void LoadStructuredNotesEditor(SessionMeta meta, SessionUserContent content)
    {
        notesEditorStructuredDoc = content.EditedNotes ?? LoadSummaryNotes(meta);
        NotesEditorStructured = notesEditorStructuredDoc is not null;
        NotesEditorSections.Clear();
        if (notesEditorStructuredDoc is not { } doc) { notesEditorStructuredDirty = false; return; }
        for (var section = 0; section < doc.Sections.Count; section++)
        {
            var row = new NotesEditorSectionRow(doc.Sections[section].Title);
            for (var item = 0; item < doc.Sections[section].Items.Count; item++)
            {
                var value = doc.Sections[section].Items[item];
                row.Items.Add(new NotesEditorItemRow(section, item, value.Text, value.Time, value.Uncertain,
                    () => MarkNotesEditorStructuredDirty()));
            }
            NotesEditorSections.Add(row);
        }
        notesEditorStructuredDirty = false;
        OnPropertyChanged(nameof(NotesEditorPlainText));
    }

    private void MarkNotesEditorStructuredDirty()
    {
        notesEditorStructuredDirty = true;
        RaiseNotesEditorDirty();
    }

    /// <summary>Mac 「我已核实」: one item is confirmed, the rest of the document stays as it is.</summary>
    public void ConfirmNotesEditorItem(NotesEditorItemRow? row)
    {
        if (row is null || !row.Uncertain) return;
        row.Uncertain = false;
        MarkNotesEditorStructuredDirty();
        OnPropertyChanged(nameof(NotesEditorStructured));
        OnPropertyChanged(nameof(NotesEditorPlainText));
    }

    private StructuredNotesDoc? BuildEditedNotesDocument()
    {
        if (notesEditorStructuredDoc is not { } doc) return null;
        var sections = new List<NotesSectionResult>();
        foreach (var sectionRow in NotesEditorSections)
        {
            var items = sectionRow.Items.Select(item => doc.Sections[item.SectionIndex].Items[item.ItemIndex]
                with { Text = item.Text, Uncertain = item.Uncertain }).ToList();
            var title = doc.Sections.Count > sections.Count ? doc.Sections[sections.Count].Title : sectionRow.Title;
            var id = doc.Sections.Count > sections.Count ? doc.Sections[sections.Count].Id : "";
            var kind = doc.Sections.Count > sections.Count ? doc.Sections[sections.Count].Kind : NotesSectionKind.Items;
            var isGap = doc.Sections.Count > sections.Count && doc.Sections[sections.Count].IsGap;
            sections.Add(new NotesSectionResult(id, title, kind, isGap, items));
        }
        return doc with { Sections = sections };
    }

    /// <summary>Mac MeetingNotesEditor.save with notes: editedNotes in user-content, page re-rendered.</summary>
    private async Task SaveStructuredNotesEditorAsync(SessionMeta meta, SessionUserContent content)
    {
        if (BuildEditedNotesDocument() is not { } edited) return;
        var folder = Sessions.Folder(meta.Id);
        var saved = SessionUserContentStore.Save(StructuredNotesEditing.WithEditedNotes(content, edited) with
        {
            PersonalNotes = PersonalNotesText
        }, folder);
        notesEditorStructuredDoc = saved.EditedNotes;
        notesEditorStructuredDirty = false;
        notesEditorBaseline = NotesEditorText;
        personalNotesBaseline = PersonalNotesText;
        notesEditorWasEdited = true;
        RaiseNotesEditorDirty();
        var title = string.IsNullOrWhiteSpace(meta.Title) ? edited.Title : meta.Title.Trim();
        var duration = meta.EndedAt is { } ended ? (ended - meta.StartedAt).TotalSeconds : 0;
        SummaryText = NotesMarkdown.Render(edited, title,
            NotesMarkdown.Facts(edited, meta.StartedAt, duration, meta.InPerson, UiText.EnglishUi));
        OnPropertyChanged(nameof(HasStructuredNotes));
        SetStatus(UiText.T("纪要编辑已保存；生成结果未被覆盖。"));
        await Task.CompletedTask;
    }
}

public partial class NotesEditorSectionRow : ObservableObject
{
    public NotesEditorSectionRow(string title) => Title = title;
    public string Title { get; }
    public ObservableCollection<NotesEditorItemRow> Items { get; } = [];
}

public partial class NotesEditorItemRow : ObservableObject
{
    private readonly Action changed;

    public NotesEditorItemRow(int sectionIndex, int itemIndex, string text, string? time, bool uncertain, Action changed)
    {
        SectionIndex = sectionIndex;
        ItemIndex = itemIndex;
        this.changed = changed;
        this.text = text;
        timeText = time ?? "";
        this.uncertain = uncertain;
    }

    public int SectionIndex { get; }
    public int ItemIndex { get; }
    [ObservableProperty] private string text;
    private string timeText;
    public string Time => timeText;
    public bool HasTime => timeText.Length > 0;
    [ObservableProperty] private bool uncertain;

    partial void OnTextChanged(string value) => changed();
}
