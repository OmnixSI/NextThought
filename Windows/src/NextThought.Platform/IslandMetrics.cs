namespace NextThought.Platform;

/// <summary>
/// C5: the cue island's presentations, in the grammar of a Dynamic Island
/// (<c>CueIslandView.Phase</c>): <c>compact</c> sits beside the notch, <c>hearing</c>,
/// <c>listening</c>, <c>cue</c> and <c>slide</c> are live activities that open by themselves,
/// <c>deck</c>, <c>launcher</c> and <c>autoEnd</c> open under the pointer.
/// </summary>
public enum CueIslandPhase
{
    Hidden,
    Compact,
    Hearing,
    Listening,
    Cue,
    Deck,
    Launcher,
    AutoEnd,
    Slide
}

/// <summary>The three cue widths of the settings (CueIsland.swift <c>CueWidth</c>).</summary>
public enum CueIslandWidth
{
    Narrow = 440,
    Regular = 540,
    Wide = 660
}

/// <summary>
/// C5: where the island hangs (CueIsland.swift <c>IslandMetrics</c>). Windows has no hardware
/// notch, so the shell draws the Mac demo-notch metrics (186 pt notch, 32 pt bar, flush against
/// the top edge) — that is what the Mac reference screenshots show and what §6.6 asks for
/// ("贴着屏幕顶边向下长出，上方两肩内凹").
/// </summary>
public readonly record struct IslandMetrics(bool HasNotch, double NotchWidth, double BarHeight, double TopOffset)
{
    /// <summary>The Mac demo notch (CueIslandController.reposition with NEXTTHOUGHT_DEMO_NOTCH=1).</summary>
    public static IslandMetrics Notch { get; } = new(true, 186, 32, 0);

    /// <summary>No notch: a floating rounded tablet under the menu bar.</summary>
    public static IslandMetrics Floating { get; } = new(false, 0, 30, 6);
}

/// <summary>
/// C5–C9: the island's geometry, widths and interaction numbers, one place so the window, the
/// self-check and the contract tests all read the same values from CueIsland.swift.
/// </summary>
public static class IslandLayout
{
    // -- C2: hover dwell before the island opens, and delay before it closes -----------------
    /// <summary>Pointer on the island for this long before it opens (track() 0.22 s).</summary>
    public const double HoverOpenSeconds = 0.22;

    /// <summary>Pointer away for this long before it closes (track() 0.2 s).</summary>
    public const double HoverCloseSeconds = 0.2;

    // -- C9: outline ------------------------------------------------------------------------
    /// <summary>Closed, the outline is the hardware notch's: 6 pt flare, 14 pt corners.</summary>
    public const double IdleFlare = 6;
    /// <summary>Open, the shoulders relax to a 14 pt flare and the bottom corners to 24 pt.</summary>
    public const double OpenFlare = 14;
    public const double IdleRadius = 14;
    public const double OpenRadius = 24;
    /// <summary>Scene-pigment edge: 1.2 pt, 2 pt while the coach asks for attention.</summary>
    public const double EdgeWidth = 1.2;
    public const double CautionEdgeWidth = 2;
    /// <summary>Black shadow under the island: radius 18, y 10, 18% (35% lifted on a floating surface).</summary>
    public const double ShadowRadius = 18;
    public const double ShadowOffsetY = 10;
    public const double ShadowOpacity = 0.18;
    public const double LiftedShadowOpacity = 0.35;

    // -- C5: widths -------------------------------------------------------------------------
    /// <summary>The row level with the notch: the notch plus an ear on each side (bar + clock).</summary>
    public const double EarPadding = 76;
    /// <summary>While the coach has something to say the ears grow (CueIslandView.width).</summary>
    public const double CoachEarPadding = 132;
    public const double FloatingWidth = 164;
    public const double HearingMaxWidth = 440;
    public const double DeckWidth = 470;
    public const double LauncherWidth = 500;
    public const double AutoEndWidth = 470;

    /// <summary>The compact width: notch + two ears on a notched screen, 164 without a notch.</summary>
    public static double EarsWidth(IslandMetrics metrics, bool coachEar = false) => metrics.HasNotch
        ? metrics.NotchWidth + 2 * (coachEar ? CoachEarPadding : EarPadding)
        : FloatingWidth;

    /// <summary>
    /// C5: the content width of one phase (CueIslandView.width). "ears" is the compact width,
    /// matching what the notch row needs; every other phase is at least that wide.
    /// </summary>
    public static double ContentWidth(CueIslandPhase phase, IslandMetrics metrics, CueIslandWidth width,
        bool coachEar = false)
    {
        var ears = EarsWidth(metrics, coachEar);
        return phase switch
        {
            CueIslandPhase.Hidden or CueIslandPhase.Compact => ears,
            CueIslandPhase.Hearing => Math.Max(ears, Math.Min((double)width, HearingMaxWidth)),
            CueIslandPhase.Cue or CueIslandPhase.Slide or CueIslandPhase.Listening =>
                Math.Max(ears, (double)width),
            CueIslandPhase.Deck => Math.Max(ears, DeckWidth),
            CueIslandPhase.Launcher => Math.Max(ears, LauncherWidth),
            CueIslandPhase.AutoEnd => Math.Max(ears, AutoEndWidth),
            _ => ears
        };
    }

    /// <summary>Closed outlines carry the notch's flare; open ones relax to 14 pt.</summary>
    public static double Flare(bool idle, IslandMetrics metrics) =>
        metrics.HasNotch ? (idle ? IdleFlare : OpenFlare) : 0;

    /// <summary>Closed corners are 14 pt (15 without a notch), open corners 24 pt.</summary>
    public static double Radius(bool idle, IslandMetrics metrics) =>
        idle ? (metrics.HasNotch ? IdleRadius : 15) : OpenRadius;

    /// <summary>The chrome width padding the content box: one flare on each side.</summary>
    public static double FrameWidth(double contentWidth, bool idle, IslandMetrics metrics) =>
        contentWidth + 2 * Flare(idle, metrics);
}
