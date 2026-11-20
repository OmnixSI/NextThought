using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NextThought.Core;

namespace NextThought.Desktop.Views.Controls;

/// <summary>D6: a rounded scene mark filled with its pigment and a glyph.</summary>
public sealed class SceneGlyph : Border
{
    public static readonly StyledProperty<MeetingMode> ModeProperty =
        AvaloniaProperty.Register<SceneGlyph, MeetingMode>(nameof(Mode));
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<SceneGlyph, double>(nameof(Size), 40d);

    public MeetingMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public SceneGlyph()
    {
        Child = new TextBlock
        {
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty || change.Property == SizeProperty) Rebuild();
    }

    private void Rebuild()
    {
        var theme = SceneTheme.For(Mode);
        var tint = Color.Parse(theme.TintLight);
        Width = Size;
        Height = Size;
        CornerRadius = new CornerRadius(Size * 0.3);
        Background = new SolidColorBrush(tint, 0.13);
        if (Child is TextBlock text)
        {
            text.Text = Symbol(theme.Symbol);
            text.FontSize = Size * 0.44;
            text.Foreground = new SolidColorBrush(tint);
        }
    }

    private static string Symbol(string sf) => sf switch
    {
        "person.crop.rectangle" => "\uE77B",
        "flask" => "\uE9D9",
        "graduationcap" => "\uE7BE",
        "person.3" or "person.2" => "\uE716",
        "briefcase" => "\uE821",
        "book" => "\uE8F1",
        "bubble.left.and.bubble.right" => "\uE8BD",
        "sparkles" => "\uE945",
        "slider.horizontal.3" => "\uE9E9",
        _ => "\uE8A5"
    };
}
