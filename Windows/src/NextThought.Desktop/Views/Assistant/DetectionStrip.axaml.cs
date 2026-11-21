using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using NextThought.Core;

namespace NextThought.Desktop.Views.Assistant;

/// <summary>
/// E5: the question the detector is following, with its confidence bar (Mac DetectionStrip /
/// ConfidenceBar). The fill follows the Mac thresholds: green at ≥0.6, amber at ≥0.4, otherwise
/// the secondary grey; the tooltip is the Mac 问题置信度 percentage.
/// </summary>
public partial class DetectionStrip : UserControl
{
    private AppState? state;

    public DetectionStrip()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach();
        DataContextChanged += (_, _) => Attach();
    }

    private void Attach()
    {
        if (state is not null) state.PropertyChanged -= OnStateChanged;
        state = DataContext as AppState;
        if (state is null) return;
        state.PropertyChanged += OnStateChanged;
        UpdateConfidence();
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.CandidateConfidence)) UpdateConfidence();
    }

    private void UpdateConfidence()
    {
        var score = state?.CandidateConfidence ?? 0;
        var brushKey = score >= 0.6 ? "Ready" : score >= 0.4 ? "Caution" : "Tertiary";
        ConfidenceBar.Foreground = Application.Current?.TryFindResource(brushKey, out var value) == true
            ? value as IBrush
            : null;
        ToolTip.SetTip(ConfidenceBar,
            UiText.T("问题置信度") + " " + (int)Math.Round(score * 100) + "%");
    }

    private void Answer_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as AppState)?.AnswerLiveCandidate();
}
