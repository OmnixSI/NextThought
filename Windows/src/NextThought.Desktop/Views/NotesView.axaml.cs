using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// E15: the meeting page (NotesPageView.swift MeetingPage). Four tabs — 纪要 / 转写 / 回答 /
/// 个人笔记 — with the evidence pane beside the notes and jumps kept inside this page.
/// </summary>
public partial class NotesView : UserControl
{
    public NotesView()
    {
        InitializeComponent();
        Fishbone.Jump += seconds => (DataContext as AppState)?.ShowNotesTranscriptAt(seconds);
        FishboneTree.Jump += seconds => (DataContext as AppState)?.ShowNotesTranscriptAt(seconds);
        Viewpoints.Jump += seconds => (DataContext as AppState)?.ShowNotesTranscriptAt(seconds);
        Loaded += (_, _) =>
        {
            if (DataContext is AppState state)
            {
                state.NotesJumpRequested += OnNotesJumpRequested;
                state.Transcript.CollectionChanged += OnNotesTranscriptChanged;
                TranscriptRow.RefreshSpeakerLabels(state.Transcript);
            }
            // D1: demo mode maps the notes tabs to the Mac demo's capture stages.
            if (this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault() is { } tabs)
                tabs.SelectionChanged += (_, _) =>
                    (DataContext as AppState)?.ApplyDemoNotesTab(tabs.SelectedIndex);
        };
        Unloaded += (_, _) =>
        {
            if (DataContext is AppState state)
            {
                state.NotesJumpRequested -= OnNotesJumpRequested;
                state.Transcript.CollectionChanged -= OnNotesTranscriptChanged;
            }
        };
    }

    private void OnNotesTranscriptChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is AppState state) TranscriptRow.RefreshSpeakerLabels(state.Transcript);
    }
    private void NotesStructured_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is AppState state) state.SetNotesStructuredWidth(e.NewSize.Width);
    }

    /// <summary>MeetingPage keeps the jump inside the page: switch to 转写 and scroll to the line.</summary>
    private async void OnNotesJumpRequested(int seconds)
    {
        await Task.Delay(120);
        if (DataContext is not AppState state) return;
        var list = this.FindControl<ListBox>("TranscriptListBox")
            ?? this.GetVisualDescendants().OfType<ListBox>().FirstOrDefault(box => box.Name == "TranscriptListBox");
        if (list is null) return;
        var index = -1;
        for (var position = 0; position < state.Transcript.Count; position++)
            if (state.Transcript[position].IsHighlighted) { index = position; break; }
        if (index < 0 && seconds >= 0)
            index = state.Transcript.Count > 0 ? Math.Clamp(seconds / 30, 0, state.Transcript.Count - 1) : -1;
        if (index >= 0) list.ScrollIntoView(index);
    }

    private async void EditNotes_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not AppState state || TopLevel.GetTopLevel(this) is not Window owner) return;
            state.LoadNotesEditor();
            var dialog = new NotesEditorWindow { DataContext = state };
            await dialog.ShowDialog(owner);
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void EditEvidence_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: SessionEvidenceRow row } || DataContext is not AppState state) return;
            if (row.Target is null || TopLevel.GetTopLevel(this) is not Window owner) return;
            var dialog = new EditTextDialog(row.Claim);
            var text = await dialog.ShowDialog<string?>(owner);
            if (text is not null) state.ApplyNotesEdit(row.Target, text);
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void JumpEvidence_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button { Tag: SessionEvidenceRow row } && DataContext is AppState state)
                state.JumpToEvidenceLine(row.Reference.LineId, row.Reference.Location);
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void OutlineTime_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: int seconds } || DataContext is not AppState state) return;
            state.ShowNotesTranscriptAt(seconds);
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    /// <summary>E12/Mac SpeakerStrip: tap a person above the transcript to open the speaker editor.</summary>
    private void OpenSpeakerStrip_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not AppState state) return;
            if (sender is Button { Tag: SpeakerPersonRow person }) state.SelectSpeakerPersonCommand.Execute(person);
            else state.OpenSpeakerEditorCommand.Execute(null);
            new SpeakerEditorWindow { DataContext = state }.Show(TopLevel.GetTopLevel(this) as Window);
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    /// <summary>E22: AddToGlossarySheet from a transcript line (or a find hit).</summary>
    private async void AddTranscriptToGlossary_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not AppState state || TopLevel.GetTopLevel(this) is not Window owner) return;
            var text = sender switch
            {
                MenuItem { Tag: TranscriptRow row } => row.Text,
                Button { Tag: GlossaryTranscriptHitRow hit } => hit.Text,
                _ => null
            };
            if (string.IsNullOrWhiteSpace(text)) return;
            state.BeginAddToGlossary(text);
            await new AddToGlossarySheet { DataContext = state }.ShowDialog(owner);
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void HighlightJump_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button { Tag: SessionHighlight highlight } && DataContext is AppState state)
                state.JumpToMeetingHighlight(highlight);
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void Info_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not AppState state || TopLevel.GetTopLevel(this) is not Window owner) return;
            await new SessionInfoWindow { DataContext = state }.ShowDialog(owner);
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    /// <summary>
    /// 重新整理 asks first when the user edited this meeting's notes (NotesRegeneration.swift);
    /// a regenerated AI draft must not silently hide the edits.
    /// </summary>
    private async void RegenerateNotes_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not AppState state) return;
            if (state.HasEditedNotesForCurrentSession)
            {
                var confirmed = await new ConfirmDialog(
                    UiText.T("重新整理这份纪要？这场会议的纪要你改过。重新整理会生成新的 AI 版本，你改过的版本会保留。"))
                    .ShowDialog<bool>(TopLevel.GetTopLevel(this) as Window ?? throw new InvalidOperationException());
                if (!confirmed) return;
            }
            if (state.SummarizeCommand.CanExecute(null)) state.SummarizeCommand.Execute(null);
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    // ---- task-62/E16: Mac LineSpeakerMenu + editLine on the 转写 rows ---------------------

    private TranscriptRow? transcriptMenuRow;

    private void TranscriptRowMenu_Opening(object? sender, CancelEventArgs e)
    {
        if (sender is not ContextMenu menu || DataContext is not AppState state) return;
        transcriptMenuRow = menu.PlacementTarget?.DataContext as TranscriptRow;
        if (transcriptMenuRow is null) { e.Cancel = true; return; }
        if (menu.Items.OfType<MenuItem>().FirstOrDefault(item => item.Name == "LineSpeakerMenu") is not { } speakers) return;
        speakers.Items.Clear();
        AddSpeakerItem(speakers, UiText.T("我"), LineSpeaker.Me);
        AddSpeakerItem(speakers, UiText.T("新说话人"), LineSpeaker.SomeoneNew);
        foreach (var person in state.SpeakerPeople.Where(person => !person.IsMe))
            AddSpeakerItem(speakers, person.DisplayLabel, LineSpeaker.FromVoice(person.Voice));
    }

    private void AddSpeakerItem(MenuItem parent, string label, LineSpeaker target)
    {
        var item = new MenuItem { Header = label, Tag = target };
        item.Click += AssignTranscriptSpeaker_Click;
        parent.Items.Add(item);
    }

    private async void AssignTranscriptSpeaker_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: LineSpeaker target } || transcriptMenuRow is not { } row) return;
        if (DataContext is AppState state) await state.AssignTranscriptLineSpeakerAsync(row, target);
    }

    private async void EditTranscriptLine_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: TranscriptRow row } || DataContext is not AppState state) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var dialog = new EditTextDialog(row.Text);
        var text = await dialog.ShowDialog<string?>(owner);
        if (!string.IsNullOrWhiteSpace(text)) await state.SaveTranscriptLineEditAsync(row, text);
    }

}