using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// D1: adapter between the Mac demonstration data (<see cref="DemoScript"/>, a verbatim port of
/// DemoMode.swift / DemoFilm.swift) and the desktop shell. The demo mode never writes to the
/// user's data directory: everything is constructed in memory on demand.
/// </summary>
public static class DemoContent
{
    /// <summary>The live question of the demo interview (Mac DemoMode.swift <c>question</c>).</summary>
    public const string Question = DemoScript.LiveQuestion;

    /// <summary>The live answer of the demo interview (Mac DemoMode.swift <c>reply</c>).</summary>
    public const string Answer = DemoScript.LiveAnswer;

    /// <summary>The Chinese line under the bilingual island answer (Mac snapshotIsland).</summary>
    public const string IslandTranslation = DemoScript.IslandBilingualTranslation;

    public static DemoDataset Create() => DemoScript.Create(DateTimeOffset.Now);

    /// <summary>The first-run guide's sample interview, in the interface language (Mac localized).</summary>
    public static DemoOnboardingSample Onboarding() => ScriptOnboarding();

    private static DemoOnboardingSample ScriptOnboarding() =>
        Platform.DemoScript.Onboarding(NextThought.Core.UiText.EnglishUi);
}
