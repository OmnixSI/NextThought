using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>One entry of the slides-display picker (empty id = pick automatically).</summary>
public sealed record ScreenChoice(string Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// The displays seen by the main window, refreshed on startup and whenever the topology
/// changes. Kept here so the settings page and the startup report share one snapshot.
/// </summary>
public static class DisplayCatalog
{
    public static IReadOnlyList<DisplayInfo> Displays { get; private set; } = [];

    public static void Refresh(IReadOnlyList<DisplayInfo> displays) => Displays = displays;

    /// <summary>Picker entries: automatic first, then every display by id.</summary>
    public static IReadOnlyList<ScreenChoice> Choices =>
    [
        new("", "自动（第一块外接屏）"),
        .. Displays.Select(display => new ScreenChoice(display.Id, display.Name))
    ];
}
