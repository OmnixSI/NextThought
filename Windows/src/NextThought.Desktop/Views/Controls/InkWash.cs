using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using NextThought.Core;

namespace NextThought.Desktop.Views.Controls;

/// <summary>D6: two soft pigment blooms at the top of a page (Stage.swift InkWash).</summary>
public sealed class InkWash : Canvas
{
    public static readonly StyledProperty<MeetingMode> ModeProperty =
        AvaloniaProperty.Register<InkWash, MeetingMode>(nameof(Mode), MeetingMode.General);

    public MeetingMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    public InkWash()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty) Rebuild();
    }

    private void Rebuild()
    {
        var tint = Color.Parse(SceneTheme.For(Mode).TintLight);
        Children.Clear();
        Children.Add(Bloom(tint, 0.20, 0.44, 250));
        Children.Add(Bloom(tint, 0.09, 0.32, 230));
    }

    private static Ellipse Bloom(Color tint, double opacity, double widthRatio, double height)
    {
        var ellipse = new Ellipse
        {
            Fill = new SolidColorBrush(tint, opacity),
            Width = 420, Height = height,
            Effect = new BlurEffect { Radius = 80 }
        };
        Canvas.SetLeft(ellipse, 520);
        Canvas.SetTop(ellipse, -120);
        return ellipse;
    }
}
