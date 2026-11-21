using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NextThought.Desktop.Views.Controls;

/// <summary>
/// §3.3 live 16 / Mac SpeakerStrip: the 「我是哪个？」 capsule is drawn with a dashed hairline
/// (SwiftUI <c>Capsule().strokeBorder(Ink.hairline, style: StrokeStyle(lineWidth: 1, dash: [3, 2]))</c>).
/// Avalonia's Border has no dashed border, so this control paints the capsule outline itself while
/// the content sits on top of it.
/// </summary>
public sealed class DashedCapsule : Control
{
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<DashedCapsule, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<DashedCapsule, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<DashedCapsule, double>(nameof(StrokeThickness), 1);

    static DashedCapsule()
    {
        AffectsRender<DashedCapsule>(StrokeProperty, FillProperty, StrokeThicknessProperty);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var thickness = Math.Max(0, StrokeThickness);
        var half = thickness / 2;
        var rect = new Rect(Bounds.Size).Deflate(half);
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var radius = rect.Height / 2;
        var pen = Stroke is null || thickness <= 0
            ? null
            : new Pen(Stroke, thickness, new DashStyle([3, 2], 0));
        context.DrawRectangle(Fill, pen, rect, radius, radius);
    }
}
