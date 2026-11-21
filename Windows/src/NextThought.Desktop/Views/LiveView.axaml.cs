using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// §6.5 / E4: the live meeting page (LiveSessionView.swift) — the one-row top bar with the listening
/// timer/status, the scene tag, the title, the 自动回答/只记录 segmented switch, the answer window and
/// tools icons and the bordered pause/end; the flat transcript column with its question marker, echo
/// row and status line; the three-tab right column (实时纪要 / 问答 / 我的笔记) with the raised answer
/// sheet; and the bottom LiveComposer.
/// </summary>
public partial class LiveView : UserControl
{
    private enum RightTab { LiveNotes, Answers, MyNotes }

    private AppState? state;
    private ScrollViewer? transcriptScroll;
    private bool scrollingToLatest;

    public LiveView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
        // E4/Mac ⌘L 聚焦输入框: Ctrl+L focuses the composer.
        KeyDown += OnLiveKeyDown;
    }

    private void OnLiveKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.L || e.KeyModifiers != KeyModifiers.Control) return;
        var composer = this.FindControl<TextBox>("LiveComposer");
        if (composer is null) return;
        composer.Focus();
        composer.SelectAll();
        e.Handled = true;
    }

    private void OnTranscriptScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (state is null || transcriptScroll is null || scrollingToLatest) return;
        var remaining = transcriptScroll.Extent.Height - transcriptScroll.Viewport.Height - transcriptScroll.Offset.Y;
        if (remaining <= 24) state.LiveReachedLatest();
        else state.LiveScrolledAway();
    }

    private void OnLiveFollowScrollRequested() => ScrollTranscriptToLatest();

    private void ScrollTranscriptToLatest()
    {
        if (transcriptScroll is null) return;
        scrollingToLatest = true;
        try { transcriptScroll.ScrollToEnd(); }
        catch { }
        Dispatcher.UIThread.Post(() => scrollingToLatest = false, DispatcherPriority.Background);
    }

    private void Attach()
    {
        if (state is not null)
        {
            state.Transcript.CollectionChanged -= OnTranscriptChanged;
            state.TranscriptJumpRequested -= OnTranscriptJumpRequested;
            state.PropertyChanged -= OnStatePropertyChanged;
            state.CurrentAnswerHistory.CollectionChanged -= OnAnswerHistoryChanged;
            state.LiveFollowScrollRequested -= OnLiveFollowScrollRequested;
            state.Settings.PropertyChanged -= OnSettingsPropertyChanged;
            if (transcriptScroll is not null) transcriptScroll.ScrollChanged -= OnTranscriptScrollChanged;
        }
        state = DataContext as AppState;
        if (state is not null)
        {
            state.Transcript.CollectionChanged += OnTranscriptChanged;
            state.TranscriptJumpRequested += OnTranscriptJumpRequested;
            state.PropertyChanged += OnStatePropertyChanged;
            state.CurrentAnswerHistory.CollectionChanged += OnAnswerHistoryChanged;
            state.LiveFollowScrollRequested += OnLiveFollowScrollRequested;
            state.Settings.PropertyChanged += OnSettingsPropertyChanged;
            transcriptScroll = TranscriptList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (transcriptScroll is not null) transcriptScroll.ScrollChanged += OnTranscriptScrollChanged;
            // Mac RightColumn.onAppear: what the meeting was started for is what shows first.
            ShowRightTab(state.AnswerModeIsAuto
                ? RightTab.Answers
                : state.LiveNotesEnabled ? RightTab.LiveNotes : RightTab.MyNotes);
            state.RefreshSessionAnswerHistory();
            state.RaiseLiveAnswerUi();
            state.RaiseLiveSceneUi();
            state.RaiseWebSearchModeUi();
            state.RaiseLiveNotesTabUi();
            state.ReloadLiveSpeakerStrip();
        }
    }

    private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppState.AnswerText):
            case nameof(AppState.Answering):
            case nameof(AppState.DetectedQuestion):
            case nameof(AppState.HasBilingualQuestion):
                state?.RaiseLiveAnswerUi();
                break;
            case nameof(AppState.SelectedMode):
                state?.RaiseLiveSceneUi();
                state?.RaiseLiveInsightsEmbed();
                break;
            case nameof(AppState.AnswerModeIsAuto):
                if (state is not null)
                {
                    ShowRightTab(state.AnswerModeIsAuto
                        ? RightTab.Answers
                        : state.LiveNotesEnabled ? RightTab.LiveNotes : RightTab.MyNotes);
                }
                break;
            case nameof(AppState.LiveNotesEnabled):
                state?.RaiseLiveNotesTabUi();
                break;
            case nameof(AppState.CurrentSession):
                state?.RefreshSessionAnswerHistory();
                state?.ReloadLiveSpeakerStrip();
                break;
            case nameof(AppState.Recording):
                state?.RaiseLiveStatusCountersUi();
                break;
            case nameof(AppState.SpeakerPeople):
                state?.RefreshLiveSpeakerNames();
                break;
        }
    }

    private void OnAnswerHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        state?.RefreshSessionAnswerHistory();
        state?.RaiseLiveAnswerUi();
    }

    // ---- J6/Mac SpeakerStrip + LineSpeakerMenu ---------------------------------------

    /// <summary>Mac SpeakerStrip: tapping a person opens their name / merge / 这是我 editor.</summary>
    private void SpeakerChip_Click(object? sender, RoutedEventArgs e)
    {
        if (state is null) return;
        if (sender is Button { Tag: SpeakerPersonRow person }) state.SelectSpeakerPersonCommand.Execute(person);
        new SpeakerEditorWindow { DataContext = state }.Show();
    }

    /// <summary>Mac SpeakerStrip's 「我是哪个？」: pick which voice is the user.</summary>
    private void WhoAmI_Click(object? sender, RoutedEventArgs e)
    {
        if (state is null) return;
        new SpeakerEditorWindow { DataContext = state }.Show();
    }

    /// <summary>
    /// Mac TranscriptLine's context menu: 回答这句 / 标记重点 / 这句是谁说的 (我 · 新说话人 · each
    /// recognised voice). assignLine only appends a voices.jsonl tag and writes meta.json, so the
    /// recognised transcript.jsonl stays byte for byte unchanged.
    /// </summary>
    private void TranscriptRow_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (state is null || sender is not Control control || control.DataContext is not TranscriptRow row) return;
        if (!e.GetCurrentPoint(control).Properties.IsRightButtonPressed) return;
        var flyout = new Flyout { Placement = Avalonia.Controls.PlacementMode.Pointer };
        var panel = new StackPanel { Width = 220, Spacing = 2 };
        void Add(string text, Action action)
        {
            var button = new Button
            {
                Content = text,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left
            };
            button.Classes.Add("quiet");
            button.Click += (_, _) => { flyout.Hide(); action(); };
            panel.Children.Add(button);
        }
        Add(UiText.T("回答这句"), () =>
        {
            state.DetectedQuestion = row.Text;
            if (state.ForceAnswerCommand.CanExecute(null)) state.ForceAnswerCommand.Execute(null);
        });
        Add(UiText.T("标记或取消重点"), () => state.HighlightLineCommand.Execute(row));
        panel.Children.Add(new Border { Height = 0.5, Background = Avalonia.Media.Brushes.Gray, Margin = new Thickness(0, 4, 0, 4) });
        panel.Children.Add(new TextBlock { Text = UiText.T("这句是谁说的"), FontSize = 11, Opacity = 0.7, Margin = new Thickness(8, 2, 0, 2) });
        Add(UiText.T("我"), () => _ = state.AssignLiveLineSpeakerAsync(row.Id, LineSpeaker.Me));
        Add(UiText.T("新说话人"), () => _ = state.AssignLiveLineSpeakerAsync(row.Id, LineSpeaker.SomeoneNew));
        foreach (var person in state.SpeakerPeople.Where(person => !person.IsMe))
            Add(person.DisplayLabel, () => _ = state.AssignLiveLineSpeakerAsync(row.Id, LineSpeaker.FromVoice(person.Voice)));
        flyout.Content = panel;
        flyout.ShowAt(control);
        e.Handled = true;
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsViewModel.InsightScreenWatch)
            or nameof(SettingsViewModel.AutomaticAnswers))
            state?.RaiseLiveInsightsEmbed();
    }

    private void LatestAnswer_Click(object? sender, RoutedEventArgs e) => state?.ShowLatestAnswer();

    // ---- right column: 实时纪要 / 问答 / 我的笔记 (Mac RightColumn) ---------------------

    private void ShowRightTab(RightTab tab)
    {
        LiveNotesPanel.IsVisible = tab == RightTab.LiveNotes;
        AnswerPanel.IsVisible = tab == RightTab.Answers;
        NotesPanel.IsVisible = tab == RightTab.MyNotes;
        LiveNotesTab.IsChecked = tab == RightTab.LiveNotes;
        AnswerTab.IsChecked = tab == RightTab.Answers;
        NotesTab.IsChecked = tab == RightTab.MyNotes;
    }

    private void LiveNotesTab_Click(object? sender, RoutedEventArgs e) => ShowRightTab(RightTab.LiveNotes);

    private void AnswerTab_Click(object? sender, RoutedEventArgs e) => ShowRightTab(RightTab.Answers);

    private void NotesTab_Click(object? sender, RoutedEventArgs e) => ShowRightTab(RightTab.MyNotes);

    /// <summary>
    /// Self-check hook (like the cue island's ApplyStateForCheck): show one of the three right-column
    /// tabs so the screenshot batch can capture 实时纪要 / 问答 / 我的笔记.
    /// </summary>
    public bool SelectRightTabForCheck(string tab)
    {
        switch (tab)
        {
            case "live-notes": ShowRightTab(RightTab.LiveNotes); return true;
            case "answers": ShowRightTab(RightTab.Answers); return true;
            case "my-notes": ShowRightTab(RightTab.MyNotes); return true;
            default: return false;
        }
    }

    // ---- top bar: answer mode, answer settings, tools ---------------------------------

    private void AnswerModeAuto_Click(object? sender, RoutedEventArgs e) => state?.SetAnswerMode(true);

    private void AnswerModeRecord_Click(object? sender, RoutedEventArgs e) => state?.SetAnswerMode(false);

    /// <summary>The more menu's 回答设置 section: show the current model (Mac AnswerSettingsMenu).</summary>
    private void MoreMenu_Opened(object? sender, EventArgs e)
    {
        if (state is null) return;
        LiveModelBox.SelectedItem = state.LiveModelChoices.FirstOrDefault(choice => choice.Id == state.Settings.Model);
    }

    private void LiveModel_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (state is null || sender is not ComboBox { SelectedItem: ModelChoice choice }) return;
        if (state.Settings.Model == choice.Id) return;
        state.Settings.Model = choice.Id;
        _ = state.Settings.SaveAsync();
        state.RaiseLiveAnswerUi();
    }

    private void AnswerLengthBrief_Click(object? sender, RoutedEventArgs e)
    {
        if (state is null) return;
        state.CurrentAnswerIsDetailed = false;
    }

    private void AnswerLengthDetailed_Click(object? sender, RoutedEventArgs e)
    {
        if (state is null) return;
        state.CurrentAnswerIsDetailed = true;
    }

    private void CueAlwaysOpen_Click(object? sender, RoutedEventArgs e)
    {
        if (state is null) return;
        state.ToggleLiveCueAlwaysOpen();
        state.RaiseLiveAnswerUi();
    }

    private void PresenterMode_Click(object? sender, RoutedEventArgs e)
    {
        if (state is null) return;
        state.Settings.PresenterMode = !state.Settings.PresenterMode;
        _ = state.Settings.SaveAsync();
        state.RaiseLiveAnswerUi();
    }

    private void AnswerLanguage_Click(object? sender, RoutedEventArgs e)
    {
        if (state is null || sender is not Button { Tag: string language }) return;
        state.Settings.AnswerLanguage = language;
        _ = state.Settings.SaveAsync();
        state.SetStatus("回答语言已更新。");
    }

    private void AnswerGloss_Click(object? sender, RoutedEventArgs e)
    {
        if (state is null) return;
        _ = state.Settings.SaveAsync();
    }

    private void Companion_Click(object? sender, RoutedEventArgs e)
    {
        if (state is null) return;
        if (state.ToggleCompanionCommand.CanExecute(null)) state.ToggleCompanionCommand.Execute(null);
        state.RaiseLiveAnswerUi();
    }

    private void LiveTranslation_Click(object? sender, RoutedEventArgs e)
    {
        if (state is null) return;
        if (state.ToggleLiveTranslationCommand.CanExecute(null)) state.ToggleLiveTranslationCommand.Execute(null);
        state.RaiseLiveAnswerUi();
    }

    // ---- 实时纪要 tab (Mac LiveNotesColumn) -------------------------------------------

    private void EnableLiveNotes_Click(object? sender, RoutedEventArgs e)
    {
        if (state is null) return;
        if (!state.LiveNotesEnabled) state.EnableLiveNotes();
        else state.UpdateLiveNotesNow();
        ShowRightTab(RightTab.LiveNotes);
    }

    // ---- transcript / answer actions ------------------------------------------------

    private void MarkNotEcho_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TranscriptRow row } && state is not null) state.MarkNotEcho(row.Id);
    }

    private void AnswerLine_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TranscriptRow row } || state is null) return;
        state.DetectedQuestion = row.Text;
        if (state.ForceAnswerCommand.CanExecute(null)) state.ForceAnswerCommand.Execute(null);
    }

    private void AnswerCandidate_Click(object? sender, RoutedEventArgs e) => state?.AnswerLiveCandidate();

    private void HighlightLine_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TranscriptRow row } && DataContext is AppState state)
            state.HighlightLineCommand.Execute(row);
    }

    private async void Refine_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string name } || state is null) return;
        if (!Enum.TryParse<AnswerRefinement>(name, ignoreCase: true, out var refinement)) return;
        await state.RefineAsync(refinement);
    }

    private void WebSource_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WebSourceRow row } && state is not null) state.OpenWebSource(row);
    }

    // ---- bottom composer (Mac LiveComposer) -----------------------------------------

    private void Composer_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift)) return;
        if (state is null) return;
        e.Handled = true;
        state.SubmitLiveComposer();
    }

    private void Composer_Send_Click(object? sender, RoutedEventArgs e) => state?.SubmitLiveComposer();

    private void CaptureMenu_Click(object? sender, RoutedEventArgs e)
    {
        // The flyout opens from the button itself; nothing else to do.
    }

    private async void CaptureRegion_Click(object? sender, RoutedEventArgs e)
    {
        try { if (TopLevel.GetTopLevel(this) is MainWindow window) await window.CaptureRegionAsync(); }
        catch (Exception ex) { state?.SetStatus("操作失败：" + ex.Message, true); }
    }

    private async void CaptureWindow_Click(object? sender, RoutedEventArgs e)
    {
        try { if (TopLevel.GetTopLevel(this) is MainWindow window) await window.CaptureWindowAsync(); }
        catch (Exception ex) { state?.SetStatus("操作失败：" + ex.Message, true); }
    }

    // ---- windows opened from the tools menu -----------------------------------------

    private void Teleprompter_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window) window.ToggleTeleprompter();
    }

    private void Insights_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        new InsightsPanelWindow { DataContext = state }.Show();
    }

    private void Speakers_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        state.OpenSpeakerEditorCommand.Execute(null);
        new SpeakerEditorWindow { DataContext = state }.Show();
    }

    private void DetachAnswer_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as AppState)?.OpenDetachedModule(AssistantModule.Answer);

    private void OnTranscriptJumpRequested(int seconds)
    {
        if (state?.CurrentSession is not { } session || transcriptScroll is null) return;
        var target = session.StartedAt.AddSeconds(seconds);
        var index = state.Transcript
            .Select((row, i) => (row, i, distance: Math.Abs((row.Date - target).TotalSeconds)))
            .OrderBy(item => item.distance)
            .Select(item => item.i)
            .FirstOrDefault();
        // The rows are not measured up front; approximate their height and clamp to the extent.
        var max = Math.Max(0, transcriptScroll.Extent.Height - transcriptScroll.Viewport.Height);
        transcriptScroll.Offset = new Vector(0, Math.Min(max, index * 56));
    }

    private void OnTranscriptChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (TranscriptList.ItemCount == 0) return;
        state?.RefreshLiveSpeakerNames();
        var added = e.Action == NotifyCollectionChangedAction.Add ? e.NewItems?.Count ?? 1 : 1;
        if (state is not null && !state.LiveIsFollowingTranscript)
        {
            // E4/Mac ScrollFollowState: reading history keeps the viewport; the banner counts new lines.
            state.LiveTranscriptGrew(added);
            return;
        }
        ScrollTranscriptToLatest();
    }
}
