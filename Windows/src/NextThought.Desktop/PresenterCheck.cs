namespace NextThought.Desktop;

/// <summary>One step of the dual-screen presenter self-check.</summary>
public sealed record PresenterCheckStep(string Name, bool Passed, string? Detail = null);

/// <summary>
/// Result of the presenter self-check. This machine has one display, so the check feeds a
/// synthetic second display through the same layout code the real meeting uses, then
/// exercises the window side effects (main window steps aside, island stays, everything
/// comes back) and confirms the real single-display topology refuses to activate.
/// </summary>
public sealed record PresenterCheckReport
{
    public bool Passed { get; init; }

    public string Scope { get; init; } = "synthetic second display; window visibility and island, not real projector output";

    public IReadOnlyList<PresenterCheckStep> Steps { get; init; } = [];

    public string RealScreens { get; init; } = "";

    public string PlanSummary { get; init; } = "";
}
