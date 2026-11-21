using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>
/// E7: 屏幕与讲稿 (InsightsPanel.swift) — the slide section with its talking points, the screen
/// section with its source and recognised text, and the answer-progress strip. The slide follow
/// state itself is maintained by AppState.SlideFollow; this view refreshes the derived rows when
/// the page, the screen or the answer changes.
/// </summary>
public partial class InsightsPanel : UserControl
{
    private AppState? state;

    public InsightsPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    private void Attach()
    {
        if (state is not null) state.PropertyChanged -= OnStatePropertyChanged;
        state = DataContext as AppState;
        if (state is not null)
        {
            state.PropertyChanged += OnStatePropertyChanged;
            state.RefreshInsights();
        }
    }

    private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.SlideStatus) or nameof(AppState.CurrentSlidePointsText)
            or nameof(AppState.ScreenWatchStatus) or nameof(AppState.ScreenWatchCapturing)
            or nameof(AppState.AnswerText) or nameof(AppState.Answering) or nameof(AppState.Recording)
            or nameof(AppState.CurrentSession) or nameof(AppState.HasCurrentSlidePoints))
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
