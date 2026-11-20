using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using NextThought.Core;

namespace NextThought.Desktop.Views.Controls;

/// <summary>D6: pigment dot plus scene name.</summary>
public sealed class SceneTag : StackPanel
{
    public static readonly StyledProperty<MeetingMode> ModeProperty =
        AvaloniaProperty.Register<SceneTag, MeetingMode>(nameof(Mode));
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<SceneTag, string?>(nameof(Name));

    public MeetingMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public string? Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    public SceneTag()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        VerticalAlignment = VerticalAlignment.Center;
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty || change.Property == NameProperty) Rebuild();
    }

    private void Rebuild()
    {
        var theme = SceneTheme.For(Mode);
        var tint = new SolidColorBrush(Color.Parse(theme.TintLight));
        Children.Clear();
        Children.Add(new Ellipse { Width = 6, Height = 6, Fill = tint, VerticalAlignment = VerticalAlignment.Center });
        Children.Add(new TextBlock { Text = Name ?? theme.Mode.ToString(), FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#6B6963")) });
    }
}
