namespace NextThought.Core;

/// <summary>
/// J7: the port of <c>TextZoom</c> (Sources/NextThought/DesignSystem.swift, Mac v3.4.0). The size
/// of all text in the app: ⌘+ and ⌘− walk six fixed steps, ⌘0 returns to the size it came with.
/// The cue island and the answer window keep their own size (their own 小 / 大 buttons).
///
/// The Mac class reads and writes <c>UserDefaults</c> key "textZoom"; in Core the persisted value
/// is injected (<see cref="PreferenceKey"/> names the key, the platform store saves it) so the
/// logic stays pure. Read wherever a font is made; changed only from the menu, on the main thread.
/// </summary>
public sealed class TextZoom
{
    /// <summary>The six fixed sizes, smallest to largest (Mac TextZoom.steps).</summary>
    public static readonly IReadOnlyList<double> Steps = [0.9, 1, 1.1, 1.2, 1.3, 1.45];

    /// <summary>The Mac preference key the factor is saved under.</summary>
    public const string PreferenceKey = "textZoom";

    /// <summary>The shared instance the pages read (Mac TextZoom.shared).</summary>
    public static TextZoom Shared { get; } = new();

    private readonly Action<double>? save;

    /// <param name="saved">The persisted factor; anything outside <see cref="Steps"/> means 1.</param>
    /// <param name="save">Called with the new factor whenever it changes.</param>
    public TextZoom(double? saved = null, Action<double>? save = null)
    {
        this.save = save;
        Factor = saved is { } value && Steps.Contains(value) ? value : 1;
    }

    /// <summary>The current size multiplier; always one of <see cref="Steps"/>.</summary>
    public double Factor { get; private set; }

    public bool CanGrow => Factor < Steps[^1];

    public bool CanShrink => Factor > Steps[0];

    public void Grow() => Step(1);

    public void Shrink() => Step(-1);

    public void Reset() => Set(1);

    private void Step(int by)
    {
        var index = -1;
        for (var position = 0; position < Steps.Count; position++)
            if (Steps[position] == Factor) { index = position; break; }
        if (index < 0) index = 1;
        Set(Steps[Math.Min(Math.Max(index + by, 0), Steps.Count - 1)]);
    }

    private void Set(double value)
    {
        if (value == Factor) return;
        Factor = value;
        save?.Invoke(value);
    }

    /// <summary>The system text size the live page follows (Mac TextZoom.dynamicType).</summary>
    public DynamicTypeSize DynamicType => Factor switch
    {
        < 0.95 => DynamicTypeSize.Medium,
        < 1.05 => DynamicTypeSize.Large,
        < 1.15 => DynamicTypeSize.XLarge,
        < 1.25 => DynamicTypeSize.XXLarge,
        < 1.35 => DynamicTypeSize.XXXLarge,
        _ => DynamicTypeSize.Accessibility1
    };

    /// <summary>A point size at this zoom, rounded like Mac Font.zoomed (.toNearestOrAwayFromZero).</summary>
    public double Scaled(double size) => Math.Round(size * Factor, MidpointRounding.AwayFromZero);

    public enum DynamicTypeSize
    {
        Medium,
        Large,
        XLarge,
        XXLarge,
        XXXLarge,
        Accessibility1
    }
}
