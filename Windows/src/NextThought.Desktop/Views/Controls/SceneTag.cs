using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using NextThought.Core;

namespace NextThought.Desktop.Views.Controls;

/// <summary>
/// D6: a small scene tag — pigment dot and name, "● 面试" (Stage.swift <c>SceneTag</c>). The name
/// follows the interface language; the dot uses the scene's light/dark pigment.
/// </summary>
public sealed class SceneTag : StackPanel
{
    public static readonly StyledProperty<MeetingMode> ModeProperty =
        AvaloniaProperty.Register<SceneTag, MeetingMode>(nameof(Mode));

    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<SceneTag, string?>(nameof(Name));

    private readonly Ellipse dot = new() { Width = 6, Height = 6, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock label = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    public MeetingMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public string? Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    public SceneTag()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        VerticalAlignment = VerticalAlignment.Center;
        Children.Add(dot);
        Children.Add(label);
        ActualThemeVariantChanged += (_, _) => Rebuild();
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty || change.Property == NameProperty) Rebuild();
    }

    private static string SceneTitle(MeetingMode mode) => mode switch
    {
        MeetingMode.Interview => "面试",
        MeetingMode.LabMeeting => "组会",
        MeetingMode.Academic => "汇报 / 答辩",
        MeetingMode.GroupProject => "小组作业",
        MeetingMode.Client => "客户沟通",
        MeetingMode.Meeting => "工作会议",
        MeetingMode.Lesson => "讲座 / 课程",
        MeetingMode.Discussion => "小组讨论",
        _ => "通用"
    };

    private void Rebuild()
    {
        var theme = SceneTheme.For(Mode);
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        dot.Fill = new SolidColorBrush(Color.Parse(theme.Tint(dark)));
        // Mac text is a machine key here, so it goes through the string table like the rest.
        label.Text = UiText.T(Name is { Length: > 0 } name ? name : SceneTitle(Mode));
        label.Foreground = new SolidColorBrush(Color.Parse(dark ? "#A3A19B" : "#6B6963"));
    }
}
