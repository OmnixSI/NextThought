using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using NextThought.Desktop;

namespace NextThought.Desktop.Views.Island;

/// <summary>
/// C5: the row of sound dots beside the meeting pulse (Stage.swift <c>VoiceWave</c> with
/// bars: 5, height: 13, tint: white 85%). Each of the five bars breathes on its own phase and
/// shrinks to a 3 pt dot when nothing is coming in — which is what the Mac reference screenshots
/// show. When Windows asks for fewer animations the bars are drawn at rest.
/// </summary>
public sealed class IslandVoiceWave : Control
{
    public static readonly StyledProperty<IBrush?> TintProperty =
        AvaloniaProperty.Register<IslandVoiceWave, IBrush?>(nameof(Tint));

    public static readonly StyledProperty<bool> ActiveProperty =
        AvaloniaProperty.Register<IslandVoiceWave, bool>(nameof(Active));

    public static readonly StyledProperty<double> LevelProperty =
        AvaloniaProperty.Register<IslandVoiceWave, double>(nameof(Level));

    private readonly DispatcherTimer timer;
    private double phaseTime;
    private bool attached;

    public IslandVoiceWave()
    {
        Width = 25;
        Height = 13;
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            phaseTime += 0.05;
            InvalidateVisual();
        };
    }

    public IBrush? Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    public bool Active
    {
        get => GetValue(ActiveProperty);
        set => SetValue(ActiveProperty, value);
    }

    public double Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        attached = true;
        SyncTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        attached = false;
        timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void SyncTimer()
    {
        var animate = attached && Active && Level > 0 && !Motion.Reduce;
        if (animate && !timer.IsEnabled) timer.Start();
        else if (!animate && timer.IsEnabled) timer.Stop();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActiveProperty || change.Property == LevelProperty || change.Property == TintProperty)
            SyncTimer();
    }

    public override void Render(DrawingContext context)
    {
        const int bars = 5;
        const double spacing = 2.5;
        var width = (Bounds.Width - spacing * (bars - 1)) / bars;
        if (width <= 0 || Bounds.Height <= 0) return;
        var level = Math.Clamp(Level, 0, 1) * 1.6;
        var brush = Active ? Tint ?? Brushes.White : new SolidColorBrush(Colors.White, 0.45);
        for (var index = 0; index < bars; index++)
        {
            // Each bar breathes on its own phase so the meter never looks like a stamp.
            var wobble = Motion.Reduce ? 1 : 0.55 + 0.45 * Math.Sin(phaseTime * 9 + index * 1.9);
            var height = Math.Max(3, Bounds.Height * Math.Min(1, level) * wobble);
            var x = index * (width + spacing);
            var y = (Bounds.Height - height) / 2;
            context.DrawRectangle(brush, null, new RoundedRect(new Rect(x, y, width, height), width / 2));
        }
    }
}

/// <summary>
/// C5/C7: three dots that take turns while the answer is on its way
/// (CueIslandView.swift <c>ThinkingDots</c>); static under reduced motion.
/// </summary>
public sealed class ThinkingDots : Control
{
    public static readonly StyledProperty<IBrush?> TintProperty =
        AvaloniaProperty.Register<ThinkingDots, IBrush?>(nameof(Tint));

    private readonly DispatcherTimer timer;
    private double phaseTime;

    public ThinkingDots()
    {
        Width = 18;
        Height = 4;
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(66) };
        timer.Tick += (_, _) =>
        {
            phaseTime += 0.066;
            InvalidateVisual();
        };
    }

    public IBrush? Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!Motion.Reduce) timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        var brush = Tint ?? Brushes.White;
        for (var index = 0; index < 3; index++)
        {
            var opacity = 0.35 + 0.65 * Math.Max(0, Math.Sin(phaseTime * 5 - index * 0.9));
            using var _ = context.PushOpacity(opacity);
            context.DrawEllipse(brush, null, new Point(2 + index * 7, 2), 2, 2);
        }
    }
}
