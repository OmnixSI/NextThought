using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// E3: playback state of the scripted sample meeting (Mac OnboardingSampleMeeting). The transcript,
/// the growing notes and the reference answer are revealed by elapsed time; nothing is recorded.
/// </summary>
public partial class AppState
{
    private DispatcherTimer? sampleTimer;
    private DateTimeOffset sampleStartedAt;
    private IReadOnlyList<double> sampleLineTimes = [];
    private IReadOnlyList<double> sampleTakeawayTimes = [];
    private IReadOnlyList<OnboardingSampleLine> sampleLines = [];
    private IReadOnlyList<OnboardingSampleTakeaway> sampleTakeaways = [];

    [ObservableProperty] private double onboardingSampleElapsed;
    [ObservableProperty] private bool onboardingSampleFinished;

    public IReadOnlyList<OnboardingSampleLine> OnboardingSampleLines
    {
        get
        {
            if (sampleLines.Count == 0) sampleLines = OnboardingSample.Lines;
            return sampleLines;
        }
    }

    public IReadOnlyList<OnboardingSampleTakeaway> OnboardingSampleTakeaways
    {
        get
        {
            if (sampleTakeaways.Count == 0) sampleTakeaways = OnboardingSample.Takeaways;
            return sampleTakeaways;
        }
    }

    public int OnboardingSampleVisibleLineCount =>
        ScriptTimeline.VisibleCount(sampleLineTimes.Count == 0 ? OnboardingSample.Lines.Select(line => line.At).ToList() : sampleLineTimes,
            OnboardingSampleElapsed);

    public IReadOnlyList<OnboardingSampleLine> OnboardingSampleVisibleLines =>
        OnboardingSampleLines.Take(OnboardingSampleVisibleLineCount).Reverse().ToList();

    public IReadOnlyList<OnboardingSampleTakeaway> OnboardingSampleVisibleTakeaways =>
        OnboardingSampleTakeaways.Take(VisibleTakeawayCount).ToList();

    private int VisibleTakeawayCount => ScriptTimeline.VisibleCount(
        sampleTakeawayTimes.Count == 0 ? OnboardingSample.Takeaways.Select(item => item.At).ToList() : sampleTakeawayTimes,
        OnboardingSampleElapsed);

    public bool OnboardingSampleShowSuggestion => OnboardingSampleElapsed >= OnboardingSample.Suggestion.At;

    public string OnboardingSampleSuggestionQuestion => OnboardingSample.Suggestion.Question;
    public string OnboardingSampleSuggestionAnswer => OnboardingSample.Suggestion.Answer;
    public bool OnboardingSampleHasTakeaways => OnboardingSampleVisibleTakeaways.Count > 0;

    public double OnboardingSampleProgress => ScriptTimeline.Progress(OnboardingSampleElapsed, OnboardingSample.Duration);

    partial void OnOnboardingSampleElapsedChanged(double value) => RaiseSample();

    partial void OnOnboardingSampleFinishedChanged(bool value) => RaiseSample();

    [RelayCommand]
    private void ReplayOnboardingSample() => PlaySample();

    private void PlaySample()
    {
        StopSample();
        sampleStartedAt = DateTimeOffset.Now;
        OnboardingSampleElapsed = 0;
        OnboardingSampleFinished = false;
        sampleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        sampleTimer.Tick += (_, _) =>
        {
            var seconds = (DateTimeOffset.Now - sampleStartedAt).TotalSeconds;
            OnboardingSampleElapsed = Math.Min(seconds, OnboardingSample.Duration);
            if (seconds >= OnboardingSample.Duration)
            {
                OnboardingSampleFinished = true;
                StopSample();
            }
        };
        sampleTimer.Start();
    }

    private void StopSample()
    {
        sampleTimer?.Stop();
        sampleTimer = null;
    }

    private void RaiseSample()
    {
        OnPropertyChanged(nameof(OnboardingSampleVisibleLineCount));
        OnPropertyChanged(nameof(OnboardingSampleVisibleLines));
        OnPropertyChanged(nameof(OnboardingSampleVisibleTakeaways));
        OnPropertyChanged(nameof(OnboardingSampleShowSuggestion));
        OnPropertyChanged(nameof(OnboardingSampleSuggestionQuestion));
        OnPropertyChanged(nameof(OnboardingSampleSuggestionAnswer));
        OnPropertyChanged(nameof(OnboardingSampleHasTakeaways));
        OnPropertyChanged(nameof(OnboardingSampleProgress));
    }
}


public partial class AppState
{
    // E3: closing the guide stops the sample replay (Mac cancels the task with the view).
    partial void OnOnboardingVisibleChanged(bool value)
    {
        if (!value) StopSample();
    }
}
