namespace NextThought.Core;

/// <summary>
/// One display as the presenter layout sees it. Ported from the screen half of
/// PresenterMode.swift: the camera is on the laptop, the slides are on the other display.
/// </summary>
public sealed record DisplayInfo(
    string Id,
    string Name,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary,
    bool IsBuiltin)
{
    public bool Contains(int x, int y) => x >= X && x < X + Width && y >= Y && y < Y + Height;

    public string Bounds => $"{X},{Y},{Width},{Height}";
}

/// <summary>Where the cue island and the slides should live for one meeting.</summary>
public sealed record PresenterPlan(DisplayInfo? Camera, DisplayInfo? Slides, bool CanActivate, string Summary)
{
    /// <summary>Nothing of NextThought is drawn on this display.</summary>
    public bool SlidesIsSeparate => Slides is not null && Camera is not null && Slides.Id != Camera.Id;
}

/// <summary>
/// Dual-screen presenting rules (PresenterMode.swift): the built-in display with the camera
/// carries the island, the other display carries the slides; without a second display the
/// mode stays off instead of hiding the only window.
/// </summary>
public static class PresenterLayout
{
    public const string NoDisplay = "没有可用的显示器";
    public const string NeedsSecondDisplay = "接上外接屏后可用";
    public const string Off = "双屏演讲已关闭";

    /// <summary>The display with the camera: the built-in one, else the primary one.</summary>
    public static DisplayInfo? Camera(IReadOnlyList<DisplayInfo> screens) =>
        screens.FirstOrDefault(screen => screen.IsBuiltin)
        ?? screens.FirstOrDefault(screen => screen.IsPrimary)
        ?? screens.FirstOrDefault();

    /// <summary>Displays the slides could be on (everything except the camera's).</summary>
    public static IReadOnlyList<DisplayInfo> Others(IReadOnlyList<DisplayInfo> screens, DisplayInfo? camera) =>
        camera is null ? [] : screens.Where(screen => screen.Id != camera.Id).ToList();

    /// <summary>
    /// Picks the slides display by id, falling back to the first non-camera display.
    /// Returns null when there is only one display.
    /// </summary>
    public static DisplayInfo? Slides(IReadOnlyList<DisplayInfo> screens, DisplayInfo? camera, string? preferredId)
    {
        var others = Others(screens, camera);
        if (others.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(preferredId)
            && others.FirstOrDefault(screen => screen.Id == preferredId) is { } chosen) return chosen;
        return others[0];
    }

    public static PresenterPlan Resolve(IReadOnlyList<DisplayInfo> screens, string? preferredSlidesId)
    {
        if (screens.Count == 0) return new PresenterPlan(null, null, false, NoDisplay);
        var camera = Camera(screens);
        var slides = Slides(screens, camera, preferredSlidesId);
        if (camera is null || slides is null) return new PresenterPlan(camera, null, false, NeedsSecondDisplay);
        return new PresenterPlan(camera, slides, true, $"幻灯片在 {slides.Name}，提示在 {camera.Name}");
    }

    /// <summary>Short line for the settings page, independent of whether a meeting is running.</summary>
    public static string Summary(IReadOnlyList<DisplayInfo> screens, string? preferredSlidesId, bool enabled)
    {
        if (!enabled) return Off;
        return Resolve(screens, preferredSlidesId).Summary;
    }
}
