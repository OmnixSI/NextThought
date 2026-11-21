namespace NextThought.Core;

/// <summary>Recent transcript lines for a live prompt (port of TranscriptContextBuilder.swift).</summary>
public static class TranscriptContextBuilder
{
    /// <summary>The last five minutes of recognised text, newline-joined.</summary>
    public static string Recent(IEnumerable<(DateTimeOffset Date, string Text)> entries, DateTimeOffset now)
    {
        var cutoff = now.AddSeconds(-300);
        return string.Join("\n", entries
            .Where(entry => entry.Date > cutoff && entry.Date <= now)
            .Select(entry => entry.Text));
    }
}

/// <summary>
/// When the onboarding guide is shown (port of OnboardingVersion.swift). Feature releases
/// reintroduce it once; patch releases do not.
/// </summary>
public static class OnboardingVersion
{
    public static string GuideVersion(string appVersion) =>
        string.Join(".", appVersion.Split('.').Take(2));

    public static bool NeedsGuide(string appVersion, string? completedVersion) =>
        completedVersion != GuideVersion(appVersion);

    public static bool IsFeatureUpgrade(string appVersion, string? startedVersion, int? startedLayout, int layout) =>
        startedVersion != GuideVersion(appVersion) || startedLayout != layout;
}
