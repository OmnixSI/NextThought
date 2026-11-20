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

    /// <summary>E31: the Mac demo stages (NEXTTHOUGHT_DEMO_SECTION) in script order.</summary>
    public static IReadOnlyList<string> StageNames => DemoStages.Names;

    /// <summary>E31: the plan of one Mac demo stage (running state, answer clearing, draft, page).</summary>
    public static DemoStagePlan Stage(string name) => DemoStages.For(name);

    /// <summary>E31: one of the two DemoFilm.swift frame scripts (island / home).</summary>
    public static DemoFilm Film(string name) => DemoFilms.For(name);

    /// <summary>E31: one DemoMode.snapshotIsland state with the Mac copy it shows.</summary>
    public static DemoIslandState Island(string name) => DemoIslands.For(name);

    private static DemoOnboardingSample ScriptOnboarding() =>
        Platform.DemoScript.Onboarding(NextThought.Core.UiText.EnglishUi);
}
