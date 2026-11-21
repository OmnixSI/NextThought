using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

public partial class AppState
{
    [ObservableProperty] private string notesEditorText = "";
    [ObservableProperty] private string personalNotesText = "";
    [ObservableProperty] private bool notesEditorShowOriginal;
    [ObservableProperty] private bool notesEditorSaving;
    [ObservableProperty] private string notesEditorError = "";
    [ObservableProperty] private string notesEditorOriginalText = "";

    private string notesEditorBaseline = "";
    private Guid? notesEditorSessionId;
    private bool notesEditorWasEdited;

    /// <summary>E16: the draft differs from what was loaded (Mac MeetingNotesEditor.dirty).</summary>
    public bool NotesEditorDirty =>
        NotesEditorText != notesEditorBaseline || PersonalNotesText != personalNotesBaseline
        || notesEditorStructuredDirty;

    private string personalNotesBaseline = "";

    public string NotesEditorDirtyText => NotesEditorDirty
        ? UiText.T("更改尚未保存")
        : UiText.T("没有未保存的更改");
    public bool HasNotesEditorError => NotesEditorError.Length > 0;

    /// <summary>Mac BusyActionLabel: the save button says 正在保存… while the write runs.</summary>
    public string NotesEditorSaveLabel => NotesEditorSaving
        ? UiText.T("正在保存…")
        : UiText.T("保存更改");

    private DateTimeOffset? notesEditorGeneratedAt;
    public bool CanSaveNotesEditor => !NotesEditorSaving && !NotesEditorShowOriginal;

    partial void OnNotesEditorTextChanged(string value) => RaiseNotesEditorDirty();
    partial void OnPersonalNotesTextChanged(string value) => RaiseNotesEditorDirty();
    partial void OnNotesEditorSavingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSaveNotesEditor));
        OnPropertyChanged(nameof(NotesEditorSaveLabel));
    }
    partial void OnNotesEditorShowOriginalChanged(bool value) => OnPropertyChanged(nameof(CanSaveNotesEditor));
    partial void OnNotesEditorErrorChanged(string value) => OnPropertyChanged(nameof(HasNotesEditorError));

    private void RaiseNotesEditorDirty()
    {
        OnPropertyChanged(nameof(NotesEditorDirty));
        OnPropertyChanged(nameof(NotesEditorDirtyText));
    }

    /// <summary>
    /// E16: prefill from user-content.json. The editor shows the user's version when it exists;
    /// 查看 AI 原稿 shows the AI draft without touching the edits (Mac MeetingNotesEditor init).
    /// </summary>
    public void LoadNotesEditor()
    {
        if (CurrentSession is not { } meta) return;
        notesEditorSessionId = meta.Id;
        notesEditorError = "";
        try
        {
            var content = SessionUserContentStore.Load(Sessions.Folder(meta.Id));
            notesEditorGeneratedAt = LoadSummaryGeneratedAt(meta);
            var aiDraft = SummaryText;
            NotesEditorOriginalText = aiDraft;
            LoadStructuredNotesEditor(meta, content);
            var duration = meta.EndedAt is { } ended ? (ended - meta.StartedAt).TotalSeconds : 0;
            NotesEditorText = content.EditedMarkdown
                ?? content.EditedSummary?.Markdown
                ?? (notesEditorStructuredDoc is { } document
                    ? NotesMarkdown.Render(document, meta.Title,
                        NotesMarkdown.Facts(document, meta.StartedAt, duration, meta.InPerson, UiText.EnglishUi))
                    : aiDraft);
            PersonalNotesText = content.PersonalNotes;
            notesEditorWasEdited = content.EditedNotes is not null || content.EditedSummary is not null
                || content.EditedMarkdown is not null;
        }
        catch
        {
            NotesEditorOriginalText = SummaryText;
            NotesEditorText = SummaryText;
            PersonalNotesText = "";
            notesEditorWasEdited = false;
        }
        notesEditorBaseline = NotesEditorText;
        personalNotesBaseline = PersonalNotesText;
        NotesEditorShowOriginal = false;
        RaiseNotesEditorDirty();
        OnPropertyChanged(nameof(NotesEditorDirtyText));
        OnPropertyChanged(nameof(CanSaveNotesEditor));
    }

    /// <summary>
    /// E16 (MeetingNotesEditor.save): the edit goes to the user's own version — editedSummary when
    /// no structured notes exist, editedMarkdown otherwise; the generated notes stay untouched.
    /// </summary>
    [RelayCommand]
    private async Task SaveNotesEditorAsync()
    {
        if (CurrentSession is not { } meta) return;
        NotesEditorSaving = true;
        NotesEditorError = "";
        try
        {
            await Task.WhenAll(writes.ToArray());
            var folder = Sessions.Folder(meta.Id);
            var content = SessionUserContentStore.Load(folder);
            if (notesEditorStructuredDoc is not null)
            {
                await SaveStructuredNotesEditorAsync(meta, content);
                return;
            }
            var text = NotesEditorText;
            SessionSummary? editedSummary = content.EditedSummary;
            string? editedMarkdown = content.EditedMarkdown;
            if (content.EditedNotes is null)
            {
                editedSummary = (editedSummary ?? new SessionSummary { Title = meta.Title }) with
                {
                    Markdown = text,
                    Model = (editedSummary ?? new SessionSummary()).Model
                };
                editedMarkdown = null;
            }
            else
            {
                editedMarkdown = text;
            }
            SessionUserContentStore.Save(content with
            {
                EditedSummary = editedSummary,
                EditedMarkdown = editedMarkdown,
                PersonalNotes = PersonalNotesText,
                BasedOnGeneratedAt = content.BasedOnGeneratedAt ?? notesEditorGeneratedAt ?? DateTimeOffset.UtcNow
            }, folder);
            notesEditorBaseline = NotesEditorText;
            personalNotesBaseline = PersonalNotesText;
            notesEditorWasEdited = true;
            RaiseNotesEditorDirty();
            SetStatus(UiText.T("纪要编辑已保存；生成结果未被覆盖。"));
        }
        catch (Exception ex)
        {
            NotesEditorError = UiText.Format("更改尚未保存：%@", ex.Message);
            SetStatus(NotesEditorError, true);
        }
        finally
        {
            NotesEditorSaving = false;
        }
    }

    /// <summary>Mac parser reads summary.json generatedAt so basedOnGeneratedAt is the AI draft time.</summary>
    private DateTimeOffset? LoadSummaryGeneratedAt(SessionMeta meta)
    {
        try
        {
            var path = Path.Combine(Sessions.Folder(meta.Id), "summary.json");
            if (!File.Exists(path)) return null;
            if (System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path)) is not System.Text.Json.Nodes.JsonObject summary) return null;
            return summary["generatedAt"] is { } node
                && DateTimeOffset.TryParse(node.GetValue<string>(), out var generated) ? generated : null;
        }
        catch { return null; }
    }

    /// <summary>True when the current meeting's notes have a user version (Mac hasEdits).</summary>
    public bool NotesEditorHasUserVersion => notesEditorWasEdited;

    /// <summary>E15: apply an edit to one structured-notes node and keep it in user-content.json.</summary>
    public void ApplyNotesEdit(SessionStructuredNotesTarget target, string text)
    {
        if (CurrentSession is not { } meta || StructuredPayload is not { } payload) return;
        var edited = SessionNotesEditor.Apply(payload, target, text);
        if (edited is null) { SetStatus(UiText.T("这条内容已被重新生成，编辑未应用。"), true); return; }
        StructuredPayload = edited;
        StructuredMarkdown = StructuredNotesMarkdown.Render(edited);
        OnPropertyChanged(nameof(HasStructuredNotes));
        try
        {
            var folder = Sessions.Folder(meta.Id);
            var content = SessionUserContentStore.Load(folder);
            SessionUserContentStore.Save(content with
            {
                EditedStructuredViews = edited,
                BasedOnGeneratedAt = content.BasedOnGeneratedAt ?? DateTimeOffset.UtcNow
            }, folder);
            SetStatus(UiText.T("条目已编辑；生成结果的其他内容未改。"));
        }
        catch (Exception ex) { SetStatus(UiText.T("保存条目失败：") + ex.Message, true); }
    }
}
