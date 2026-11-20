using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using NextThought.Core;

namespace NextThought.Desktop.Views.Controls;

/// <summary>
/// D6: pigment bleeding into wet paper at the top of a page — the port of Stage.swift's
/// <c>InkWash</c>. Two soft blooms in the scene's pigment drift slowly (a 20 fps sin wave of
/// ±18 px), are masked by a vertical fade so there is no edge where the wash meets the title row
/// or the page below it, and stop moving when the system asks for reduced motion. The blooms are
/// sized relative to the control's width, exactly like the Mac view.
/// </summary>
public sealed class InkWash : Canvas
{
    /// <summary>Mac: <c>TimelineView(.animation(minimumInterval: 1/20))</c>.</summary>
    public static readonly StyledProperty<bool> AnimatedProperty =
        AvaloniaProperty.Register<InkWash, bool>(nameof(Animated), true);

    public static readonly StyledProperty<MeetingMode> ModeProperty =
        AvaloniaProperty.Register<InkWash, MeetingMode>(nameof(Mode), MeetingMode.General);

    private const double FramesPerSecond = 20;
    private const double DriftAmplitude = 18;
    private const double DriftRate = 0.18;

    private readonly Ellipse first = new();
    private readonly Ellipse second = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(1000 / FramesPerSecond) };
    private DateTimeOffset started = DateTimeOffset.UtcNow;
    private double drift;

    public bool Animated { get => GetValue(AnimatedProperty); set => SetValue(AnimatedProperty, value); }

    public MeetingMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    public InkWash()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        OpacityMask = BuildFadeMask();
        Children.Add(first);
        Children.Add(second);
        timer.Tick += (_, _) => Tick();
        ActualThemeVariantChanged += (_, _) => Rebuild();
        Rebuild();
    }

    /// <summary>Mac: the wash has no edge — a vertical gradient that fades in at 16% and out at the bottom.</summary>
    private static LinearGradientBrush BuildFadeMask() => new()
    {
        StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Colors.Transparent, 0),
            new GradientStop(Colors.Black, 0.16),
            new GradientStop(Colors.Black, 0.55),
            new GradientStop(Colors.Transparent, 1)
        }
    };

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        started = DateTimeOffset.UtcNow;
        if (Animated && !Motion.Reduce) timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        timer.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            Rebuild();
        }
        else if (change.Property == AnimatedProperty)
        {
            if (Animated && !Motion.Reduce) timer.Start();
            else timer.Stop();
        }
        else if (change.Property == BoundsProperty)
        {
            LayoutBlooms(drift);
        }
    }

    private void Tick()
    {
        if (!Animated || Motion.Reduce)
        {
            drift = 0;
            LayoutBlooms(0);
            timer.Stop();
            return;
        }
        drift = Math.Sin((DateTimeOffset.UtcNow - started).TotalSeconds * DriftRate) * DriftAmplitude;
        LayoutBlooms(drift);
    }

    private void Rebuild()
    {
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        var tint = Color.Parse(SceneTheme.For(Mode).Tint(dark));
        first.Fill = new SolidColorBrush(tint, 0.20);
        first.Effect = new BlurEffect { Radius = 80 };
        second.Fill = new SolidColorBrush(tint, 0.09);
        second.Effect = new BlurEffect { Radius = 80 };
        LayoutBlooms(drift);
    }

    /// <summary>Mac sizes/positions relative to the canvas width (Stage.swift InkWash).</summary>
    private void LayoutBlooms(double offset)
    {
        var width = Bounds.Width;
        if (width <= 0) return;
        first.Width = width * 0.44;
        first.Height = 250;
        SetLeft(first, width * 0.58 + offset);
        SetTop(first, -120);
        second.Width = width * 0.32;
        second.Height = 230;
        SetLeft(second, width * 0.08 - offset * 0.6);
        SetTop(second, -170);
    }
}
