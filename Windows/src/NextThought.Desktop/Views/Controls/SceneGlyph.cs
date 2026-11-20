using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NextThought.Core;

namespace NextThought.Desktop.Views.Controls;

/// <summary>D6/D7: a rounded scene mark filled with its pigment and an icon from Icons.axaml.</summary>
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
        if (Child is not TextBlock text) return;
        // D7: the glyph comes from the shared Styles/Icons.axaml table (same as every other icon).
        var key = IconKey(theme.Symbol);
        text.Text = Lookup(key) as string ?? "";
        // D7: font and glyph come from the shared Styles/Icons.axaml table, like every other icon.
        text.FontFamily = Lookup("IconFont") as FontFamily ?? FontFamily.Default;
        text.FontSize = Size * 0.44;
        text.Foreground = new SolidColorBrush(tint);
    }

    private static string IconKey(string sf) => sf switch
    {
        "person.crop.rectangle" => "IconPersonCropRectangle",
        "flask" => "IconFlask",
        "graduationcap" => "IconGraduationcap",
        "person.3" => "IconPerson3",
        "person.2" => "IconPerson2",
        "briefcase" => "IconBriefcase",
        "book" => "IconBook",
        "bubble.left.and.bubble.right" => "IconBubbleLeftAndBubbleRight",
        "slider.horizontal.3" => "IconSliderHorizontal3",
        _ => "IconSparkles"
    };

    private static object? Lookup(object key)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out var value) == true) return value;
        return null;
    }
}
