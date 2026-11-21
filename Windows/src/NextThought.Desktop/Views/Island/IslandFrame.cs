using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NextThought.Desktop.Views.Island;

/// <summary>
/// C5/C9: the island's outline (CueIsland.swift <c>IslandShape</c> + <c>IslandChrome</c>).
/// With a notch its top corners flare outwards the way the notch itself meets the screen edge;
/// without one it is a rounded tablet. Both radii animate, so the outline morphs instead of
/// cross-fading (the window drives <see cref="Flare"/> and <see cref="CornerRadiusValue"/> from
/// the same <c>Motion.morph</c> spring as the width).
///
/// The content is clipped to the outline; the fill is black like the notch, the scene's pigment
/// runs along the lower edge as a transparent→pigment gradient stroke, and the soft drop shadow
/// behind the frame is the <see cref="BoxShadow"/> of the layer this control sits in.
/// </summary>
public sealed class IslandFrame : Decorator
{
    public static readonly StyledProperty<double> FlareProperty =
        AvaloniaProperty.Register<IslandFrame, double>(nameof(Flare), 6);

    public static readonly StyledProperty<double> CornerRadiusValueProperty =
        AvaloniaProperty.Register<IslandFrame, double>(nameof(CornerRadiusValue), 14);

    /// <summary>The scene-pigment stroke; already gradient-packed by the caller.</summary>
    public static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<IslandFrame, IBrush?>(nameof(EdgeBrush));

    public static readonly StyledProperty<double> EdgeWidthProperty =
        AvaloniaProperty.Register<IslandFrame, double>(nameof(EdgeWidth), 1.2);

    static IslandFrame()
    {
        AffectsRender<IslandFrame>(FlareProperty, CornerRadiusValueProperty, EdgeBrushProperty, EdgeWidthProperty);
    }

    public double Flare
    {
        get => GetValue(FlareProperty);
        set => SetValue(FlareProperty, value);
    }

    public double CornerRadiusValue
    {
        get => GetValue(CornerRadiusValueProperty);
        set => SetValue(CornerRadiusValueProperty, value);
    }

    public IBrush? EdgeBrush
    {
        get => GetValue(EdgeBrushProperty);
        set => SetValue(EdgeBrushProperty, value);
    }

    public double EdgeWidth
    {
        get => GetValue(EdgeWidthProperty);
        set => SetValue(EdgeWidthProperty, value);
    }

    /// <summary>The geometry of the outline inside <paramref name="rect"/> (CueIsland.swift <c>path(in:)</c>).</summary>
    public static Geometry Outline(Rect rect, double flare, double radius)
    {
        var figure = new PathFigure
        {
            StartPoint = new Point(rect.X, rect.Y),
            IsClosed = true,
            IsFilled = true
        };
        void Line(Point point) => figure.Segments!.Add(new LineSegment { Point = point });
        void Quad(Point control, Point end) => figure.Segments!.Add(new QuadraticBezierSegment { Point1 = control, Point2 = end });

        if (flare <= 0)
        {
            // No notch: a rounded tablet (Path(roundedRect:) in the Swift fallback).
            var corner = Math.Min(Math.Min(radius, rect.Height / 2), rect.Width / 2);
            figure.StartPoint = new Point(rect.X + corner, rect.Y);
            Line(new Point(rect.Right - corner, rect.Y));
            Quad(new Point(rect.Right, rect.Y), new Point(rect.Right, rect.Y + corner));
            Line(new Point(rect.Right, rect.Bottom - corner));
            Quad(new Point(rect.Right, rect.Bottom), new Point(rect.Right - corner, rect.Bottom));
            Line(new Point(rect.X + corner, rect.Bottom));
            Quad(new Point(rect.X, rect.Bottom), new Point(rect.X, rect.Bottom - corner));
            Line(new Point(rect.X, rect.Y + corner));
            Quad(new Point(rect.X, rect.Y), new Point(rect.X + corner, rect.Y));
        }
        else
        {
            var r = Math.Min(Math.Min(radius, rect.Height / 2), Math.Max(0, (rect.Width - 2 * flare) / 2));
            var left = rect.X + flare;
            var right = rect.Right - flare;
            // The two shoulders flare out to the screen edge, then a quad curve in to the body.
            Quad(new Point(left, rect.Y), new Point(left, rect.Y + flare));
            Line(new Point(left, rect.Bottom - r));
            Quad(new Point(left, rect.Bottom), new Point(left + r, rect.Bottom));
            Line(new Point(right - r, rect.Bottom));
            Quad(new Point(right, rect.Bottom), new Point(right, rect.Bottom - r));
            Line(new Point(right, rect.Y + flare));
            Quad(new Point(right, rect.Y), new Point(rect.Right, rect.Y));
        }

        var geometry = new PathGeometry();
        geometry.Figures!.Add(figure);
        return geometry;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (Child is { } child) child.Clip = Outline(new Rect(size), Flare, CornerRadiusValue);
        return size;
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        var geometry = Outline(rect, Flare, CornerRadiusValue);
        // Black like the notch (CueIslandView island(_:)).
        context.DrawGeometry(Brushes.Black, null, geometry);
        if (EdgeBrush is { } edge && EdgeWidth > 0)
            context.DrawGeometry(null, new Pen(edge, EdgeWidth), geometry);
    }
}
