using NextThought.Platform;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;

namespace NextThought.Desktop;

/// <summary>
/// D5 motion tokens. Durations are the Mac spring responses; the exact spring is
/// approximated with the closest Avalonia easing until a spring renderer exists.
/// Nothing runs when Windows animations are disabled.
/// </summary>
public static class Motion
{
    public enum Kind { Hover, Expand, Phase, Snappy, Morph }

    private const uint SpiGetClientAreaAnimation = 0x1042;
public static bool Reduce
    {
        get
        {
            try { return !Win32Native.SystemParametersInfo(SpiGetClientAreaAnimation, 0, out var enabled, 0) || enabled == 0; }
            catch { return false; }
        }
    }

    public static TimeSpan Duration(Kind kind) => Reduce ? TimeSpan.Zero : kind switch
    {
        Kind.Hover => TimeSpan.FromMilliseconds(140),
        Kind.Expand => TimeSpan.FromMilliseconds(320),
        Kind.Phase => TimeSpan.FromMilliseconds(460),
        Kind.Snappy => TimeSpan.FromMilliseconds(280),
        _ => TimeSpan.FromMilliseconds(500)
    };

    public static Easing Easing(Kind kind) => kind switch
    {
        Kind.Hover => new CubicEaseInOut(),
        Kind.Expand => new CubicEaseOut(),
        Kind.Phase => new CubicEaseOut(),
        Kind.Snappy => new CubicEaseInOut(),
        _ => new CubicEaseOut()
    };

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
