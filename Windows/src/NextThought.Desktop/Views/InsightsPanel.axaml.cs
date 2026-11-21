using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace NextThought.Desktop.Views;

/// <summary>
/// E7: 屏幕与讲稿 (InsightsPanel.swift) — the module header, the screen-watch hint, the slide section
/// with its talking points, the screen section with its source and recognised text, and the
/// answer-progress strip. The slide follow state itself is maintained by AppState.SlideFollow; this
/// view refreshes the derived rows when the page, the screen or the answer changes, and keeps the
/// 「N 秒前」 clock moving (Mac ScreenSection's TimelineView).
/// </summary>
public partial class InsightsPanel : UserControl
{
    private AppState? state;
    private DispatcherTimer? ageTimer;

    public InsightsPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
        DetachedFromVisualTree += (_, _) => StopTimer();
    }

    private void Attach()
    {
        if (state is not null) state.PropertyChanged -= OnStatePropertyChanged;
        state = DataContext as AppState;
        if (state is not null)
        {
            state.PropertyChanged += OnStatePropertyChanged;
            state.RefreshInsights();
            StartTimer();
        }
    }

    private void StartTimer()
    {
        if (ageTimer is not null) return;
        ageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        ageTimer.Tick += (_, _) => state?.RefreshScreenAge();
        ageTimer.Start();
    }

    private void StopTimer()
    {
        ageTimer?.Stop();
        ageTimer = null;
    }

    private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.SlideStatus) or nameof(AppState.CurrentSlidePointsText)
            or nameof(AppState.ScreenWatchStatus) or nameof(AppState.ScreenWatchCapturing)
            or nameof(AppState.AnswerText) or nameof(AppState.Answering) or nameof(AppState.Recording)
            or nameof(AppState.CurrentSession) or nameof(AppState.HasCurrentSlidePoints)
            or nameof(AppState.ScreenContextText) or nameof(AppState.ScreenWatchKind))
        {
            state?.RefreshInsights();
        }
    }

    private void OpenInsightSettings_Click(object? sender, RoutedEventArgs e) =>
        state?.NavigateCommand.Execute("Settings");

    private void EnableScreenWatch_Click(object? sender, RoutedEventArgs e) => state?.EnableScreenWatch();

    private void ToggleScreenText_Click(object? sender, RoutedEventArgs e) => state?.ToggleScreenText();

    private void AskSlideQuestion_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string question } || state is null) return;
        if (state.AskSlideQuestionCommand.CanExecute(question)) state.AskSlideQuestionCommand.Execute(question);
    }
}
