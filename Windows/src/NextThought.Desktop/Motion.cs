using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// D5 motion tokens. The Mac design system (DesignSystem.swift, <c>enum Motion</c>) uses one
/// timing curve and four damped springs:
/// <list type="bullet">
/// <item><c>hover</c>: 0.14 s cubic-bezier(0.2, 0.8, 0.2, 1) — a timing curve, not a spring.</item>
/// <item><c>expand</c>: spring(response: 0.32, dampingFraction: 0.9).</item>
/// <item><c>phase</c>: spring(response: 0.46, dampingFraction: 0.9).</item>
/// <item><c>snappy</c>: spring(response: 0.28, dampingFraction: 0.78).</item>
/// <item><c>morph</c>: spring(response: 0.5, dampingFraction: 0.82).</item>
/// </list>
/// The springs are driven by the analytic under-damped solution in
/// <see cref="MotionTokens"/>/<see cref="MotionPlan"/> (<c>NextThought.Platform</c>), sampled
/// every frame; durations are the spring settling times, never the response values.
/// When Windows asks for fewer animations (<see cref="Reduce"/>) nothing animates: floating
/// windows appear at their final opacity and the cue island snaps to its final width.
/// </summary>
public static class Motion
{
    public enum Kind { Hover, Expand, Phase, Snappy, Morph }

    private const uint SpiGetClientAreaAnimation = 0x1042;

    /// <summary>G5: the app's own 减少视觉特效 switch (Mac key <c>reducedEffects</c>), synced by SettingsViewModel.</summary>
    public static bool AppReducedEffects { get; set; }

    /// <summary>True when the system animation setting is off ("show animations in Windows").</summary>
    private static bool SystemReduce
    {
        get
        {
            try { return !Win32Native.SystemParametersInfo(SpiGetClientAreaAnimation, 0, out var enabled, 0) || enabled == 0; }
            catch { return false; }
        }
    }

    /// <summary>True when the system animation setting is off or the app asks for fewer effects.</summary>
    public static bool Reduce => MotionPlan.ShouldReduce(SystemReduce, AppReducedEffects);

    internal static MotionKind PlatformKind(Kind kind) => kind switch
    {
        Kind.Hover => MotionKind.Hover,
        Kind.Expand => MotionKind.Expand,
        Kind.Phase => MotionKind.Phase,
        Kind.Snappy => MotionKind.Snappy,
        _ => MotionKind.Morph
    };

    /// <summary>The Mac token's duration: a spring's settling time, or 0.14 s for hover.</summary>
    public static TimeSpan Duration(Kind kind) => MotionPlan.Duration(PlatformKind(kind), Reduce);

    /// <summary>Avalonia easing that samples the analytic spring (or the hover timing curve).</summary>
    public static Easing Easing(Kind kind)
    {
        var mapped = PlatformKind(kind);
        return mapped == MotionKind.Hover ? new MotionHoverEasing() : new MotionSpringEasing(mapped);
    }

    /// <summary>
    /// Eased progress for an animation driven by hand every frame (the cue island width
    /// morph). 0 → 1 over the spring's settling time; under-damped tokens overshoot past 1.
    /// </summary>
    public static double Progress(Kind kind, TimeSpan elapsed) => MotionPlan.Progress(PlatformKind(kind), elapsed, Reduce);

    public static Animation? FadeIn() => Reduce ? null : Fade(0d, 1d, Kind.Expand);
    public static Animation? FadeOut() => Reduce ? null : Fade(1d, 0d, Kind.Snappy);

    private static Animation Fade(double from, double to, Kind kind) => new()
    {
        Duration = Duration(kind),
        Easing = Easing(kind),
        FillMode = FillMode.Forward,
        Children =
        {
            new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, from) } },
            new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, to) } }
        }
    };
}

/// <summary>D5: Avalonia easing wrapper around one of the analytic spring tokens.</summary>
public sealed class MotionSpringEasing : Easing
{
    private readonly MotionKind kind;

    public MotionSpringEasing() : this(MotionKind.Morph) { }

    public MotionSpringEasing(MotionKind kind) => this.kind = kind;

    public override double Ease(double progress) => MotionPlan.EasedProgress(kind, progress);
}

/// <summary>D5: the Mac <c>Motion.hover</c> cubic-bezier(0.2, 0.8, 0.2, 1) timing curve.</summary>
public sealed class MotionHoverEasing : Easing
{
    public override double Ease(double progress) => MotionPlan.EasedProgress(MotionKind.Hover, progress);
}

/// <summary>D5: the Mac <c>Motion.snappy</c> spring (response 0.28, dampingFraction 0.78).</summary>
public sealed class MotionSnappyEasing : Easing
{
    public override double Ease(double progress) => MotionPlan.EasedProgress(MotionKind.Snappy, progress);
}
