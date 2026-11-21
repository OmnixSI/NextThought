using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;

namespace NextThought.Desktop.Views;

/// <summary>
/// E3: the guide's motion, ported from Mac <c>Sources/NextThought/DesignSystem.swift</c> Motion:
/// a critically damped settle for layout and deliberate transitions for page changes.
/// </summary>
public static class GuideMotion
{
    public static readonly TimeSpan Hover = TimeSpan.FromMilliseconds(140);
    public static readonly TimeSpan Snappy = TimeSpan.FromMilliseconds(280);
    public static readonly TimeSpan Expand = TimeSpan.FromMilliseconds(320);
    public static readonly TimeSpan Phase = TimeSpan.FromMilliseconds(460);

    /// <summary>Fade and lift a page into place (Motion.phase).</summary>
    public static Animation PhaseIn(TimeSpan? duration = null) => new()
    {
        Duration = duration ?? Phase,
        Easing = new CubicEaseOut(),
        Children =
        {
            new KeyFrame
            {
                Cue = new Cue(0d),
                Setters = { new Setter(Visual.OpacityProperty, 0d) }
            },
            new KeyFrame
            {
                Cue = new Cue(1d),
                Setters = { new Setter(Visual.OpacityProperty, 1d) }
            }
        }
    };

    /// <summary>Grow a block into place (Motion.expand).</summary>
    public static Animation ExpandIn() => new()
    {
        Duration = Expand,
        Easing = new CubicEaseOut(),
        Children =
        {
            new KeyFrame
            {
                Cue = new Cue(0d),
                Setters = { new Setter(Visual.OpacityProperty, 0d) }
            },
            new KeyFrame
            {
                Cue = new Cue(1d),
                Setters = { new Setter(Visual.OpacityProperty, 1d) }
            }
        }
    };
}
