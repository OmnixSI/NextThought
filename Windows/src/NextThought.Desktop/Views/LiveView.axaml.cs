using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// E4: the live meeting page (LiveSessionView.swift) — top bar with the listening timer/status,
/// mode switch, phone/screen status, answer settings and tools; the live transcript with the
/// question marker, candidate strip and status line; the notes/answers column with the answer
/// header, web status and sources, refinement row, evidence line/details; and the bottom composer.
/// </summary>
public partial class LiveView : UserControl
{
    private AppState? state;

    public LiveView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    private void Attach()
    {
        if (state is not null)
        {
            state.Transcript.CollectionChanged -= OnTranscriptChanged;
            state.TranscriptJumpRequested -= OnTranscriptJumpRequested;
            state.PropertyChanged -= OnStatePropertyChanged;
            state.CurrentAnswerHistory.CollectionChanged -= OnAnswerHistoryChanged;
        }
        state = DataContext as AppState;
        if (state is not null)
        {
            state.Transcript.CollectionChanged += OnTranscriptChanged;
            state.TranscriptJumpRequested += OnTranscriptJumpRequested;
            state.PropertyChanged += OnStatePropertyChanged;
            state.CurrentAnswerHistory.CollectionChanged += OnAnswerHistoryChanged;
            ShowRightTab(state.Settings.AutomaticAnswers);
            state.RefreshSessionAnswerHistory();
            state.RaiseLiveAnswerUi();
            state.RaiseLiveSceneUi();
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
                break;
            case nameof(AppState.AnswerModeIsAuto):
                if (state is not null) ShowRightTab(state.Settings.AutomaticAnswers);
                break;
            case nameof(AppState.CurrentSession):
                state?.RefreshSessionAnswerHistory();
                break;
        }
    }

    private void OnAnswerHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        state?.RefreshSessionAnswerHistory();
        state?.RaiseLiveAnswerUi();
    }

    private void LatestAnswer_Click(object? sender, RoutedEventArgs e) => state?.ShowLatestAnswer();

    private void LayoutMenu_Click(object? sender, RoutedEventArgs e)
    {
        // The flyout opens from the button itself.
    }

    private void ModuleWindow_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string name } || state is null) return;
        if (!Enum.TryParse<AssistantModule>(name, ignoreCase: true, out var module)) return;
        state.OpenDetachedModule(module);
    }

    private void LayoutPreset_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string name } || state is null) return;
        if (!Enum.TryParse<AssistantLayout.Preset>(name, ignoreCase: true, out var preset)) return;
        state.ApplyAssistantPreset(preset);
    }

    private void OnTranscriptJumpRequested(int seconds)
    {
        if (state?.CurrentSession is not { } session) return;
        var target = session.StartedAt.AddSeconds(seconds);
        var row = state.Transcript.OrderBy(item => Math.Abs((item.Date - target).TotalSeconds)).FirstOrDefault();
        if (row is null) return;
        TranscriptList.SelectedItem = row;
        TranscriptList.ScrollIntoView(row);
    }

    private void OnTranscriptChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (TranscriptList.ItemCount == 0) return;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                TranscriptList.ScrollIntoView(TranscriptList.ItemCount - 1);
            }
            catch
            {
                // Scrolling is cosmetic; a stale item during a reset must not break recording.
            }
        }, DispatcherPriority.Background);
    }

    // ---- right column tabs (Mac RightColumn: 我的笔记 / 问答) -------------------------

    private void ShowRightTab(bool showsAnswer)
    {
        AnswerPanel.IsVisible = showsAnswer;
        NotesPanel.IsVisible = !showsAnswer;
        AnswerTab.IsChecked = showsAnswer;
        NotesTab.IsChecked = !showsAnswer;
    }

    private void AnswerTab_Click(object? sender, RoutedEventArgs e) => ShowRightTab(true);

    private void NotesTab_Click(object? sender, RoutedEventArgs e) => ShowRightTab(false);

    // ---- top bar: answer mode, settings menu, tools ---------------------------------

    private void AnswerModeAuto_Click(object? sender, RoutedEventArgs e) => state?.SetAnswerMode(true);

    private void AnswerModeRecord_Click(object? sender, RoutedEventArgs e) => state?.SetAnswerMode(false);

    private void AnswerSettings_Opened(object? sender, EventArgs e)
    {
        if (state is null) return;
        if (sender is Flyout { Content: Panel panel })
        {
            var box = panel.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault();
            if (box is not null) box.SelectedItem = state.LiveModelChoices.FirstOrDefault(choice => choice.Id == state.Settings.Model);
        }
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

    private void ScreenWatch_Click(object? sender, RoutedEventArgs e)
    {
        if (state is null) return;
        state.Settings.InsightScreenWatch = !state.Settings.InsightScreenWatch;
        _ = state.Settings.SaveAsync();
        state.SetStatus(state.Settings.InsightScreenWatch ? "屏幕感知已开启。" : "屏幕感知已关闭。");
        state.RaiseLiveAnswerUi();
    }

    // ---- transcript / answer actions ------------------------------------------------

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

    private void CancelScreenshot_Click(object? sender, RoutedEventArgs e) => state?.ClearScreenshotPreview();

    private async void AnalyzeScreenshot_Click(object? sender, RoutedEventArgs e)
    {
        if (state is null) return;
        try { await state.AnalyzeScreenshotAsync(); }
        catch (Exception error) { state.SetStatus(error.Message, true); }
    }

    // ---- existing page actions ------------------------------------------------------

    private void Question_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift)) return;
        if (DataContext is not AppState state) return;
        e.Handled = true;
        state.SubmitLiveComposer();
    }

    private void Teleprompter_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window) window.ToggleTeleprompter();
    }

    private async void Capture_Click(object? sender, RoutedEventArgs e)
    {
        try { if (TopLevel.GetTopLevel(this) is MainWindow window) await window.CaptureRegionAsync(); }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
    }

    private void Island_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window) window.ToggleCueIsland();
    }

    private void DetachAnswer_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as AppState)?.OpenDetachedModule(AssistantModule.Answer);

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
}
