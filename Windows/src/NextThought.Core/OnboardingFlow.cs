namespace NextThought.Core;

/// <summary>The first-run guide, in order (port of OnboardingFlow.swift).</summary>
public enum OnboardingStep
{
    /// <summary>Pick the occasion; it becomes the last scene and the first meeting's scene.</summary>
    Scene = 0,

    /// <summary>Online or in the room, system audio, microphone and the speech model.</summary>
    Sound = 1,

    /// <summary>An answer service; optional for recording and transcription.</summary>
    Answers = 2,

    /// <summary>What the cue island does, then start or look around.</summary>
    Start = 3
}

public static class OnboardingSteps
{
    public const int Count = 4;

    /// <summary>A stored step, clamped into range (old or damaged values open a valid page).</summary>
    public static OnboardingStep Clamped(int raw) => (OnboardingStep)Math.Clamp(raw, 0, Count - 1);

    public static OnboardingStep? Next(this OnboardingStep step) =>
        Enum.IsDefined(typeof(OnboardingStep), (int)step + 1) ? (OnboardingStep)((int)step + 1) : null;

    public static OnboardingStep? Previous(this OnboardingStep step) =>
        (int)step - 1 >= 0 ? (OnboardingStep)((int)step - 1) : null;

    public static bool IsLast(this OnboardingStep step) => (int)step == Count - 1;

    /// <summary>1-based position for "2 / 4".</summary>
    public static int Position(this OnboardingStep step) => (int)step + 1;

    /// <summary>Steps the person may pass without finishing them.</summary>
    public static bool IsSkippable(this OnboardingStep step) => step == OnboardingStep.Answers;

    public static int EstimatedSeconds(this OnboardingStep step) => step switch
    {
        OnboardingStep.Scene => 15,
        OnboardingStep.Sound => 60,
        OnboardingStep.Answers => 90,
        _ => 15
    };
}

/// <summary>What is still open when the guide was put aside (port of OnboardingPending).</summary>
public enum OnboardingPending
{
    Sound,
    Answers
}

public static class OnboardingPendings
{
    /// <summary>Which areas still need setup; "ready" means a meeting could start / an answer service works.</summary>
    public static IReadOnlyList<OnboardingPending> Open(bool soundReady, bool answersReady)
    {
        var result = new List<OnboardingPending>();
        if (!soundReady) result.Add(OnboardingPending.Sound);
        if (!answersReady) result.Add(OnboardingPending.Answers);
        return result;
    }
}
