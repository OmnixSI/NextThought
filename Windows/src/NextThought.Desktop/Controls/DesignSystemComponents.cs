using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using NextThought.Core;

namespace NextThought.Desktop.Controls;

/// <summary>ControlComponents.swift ControlEmphasis.</summary>
public enum ControlEmphasis
{
    Primary,
    Secondary,
    Quiet,
    Destructive
}

/// <summary>DesignSystem.swift StatusDot.Tone.</summary>
public enum StatusTone
{
    Live,
    Ready,
    Caution,
    Idle
}

/// <summary>OperationFeedback.swift OperationTone.</summary>
public enum FeedbackTone
{
    Info,
    Success,
    Warning,
    Failure
}

/// <summary>ControlComponents.swift ControlButtonBody: shared action visuals.</summary>
public class ControlButtonBody : ContentControl
{
    public static readonly StyledProperty<ControlEmphasis> EmphasisProperty =
        AvaloniaProperty.Register<ControlButtonBody, ControlEmphasis>(nameof(Emphasis));
    public static readonly StyledProperty<double> ButtonHeightProperty =
        AvaloniaProperty.Register<ControlButtonBody, double>(nameof(ButtonHeight), 32);
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<ControlButtonBody, bool>(nameof(IsBusy));
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<ControlButtonBody, ICommand?>(nameof(Command));
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<ControlButtonBody, object?>(nameof(CommandParameter));

    public ControlButtonBody()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        PointerReleased += (_, _) =>
        {
            if (Command is { } command && command.CanExecute(CommandParameter)) command.Execute(CommandParameter);
        };
        PropertyChanged += (_, change) =>
        {
            if (change.Property == IsBusyProperty) Classes.Set("busy", IsBusy);
            if (change.Property == ButtonHeightProperty) Height = ButtonHeight;
            if (change.Property == EmphasisProperty)
            {
                foreach (var name in new[] { "primary", "secondary", "quiet", "destructive" }) Classes.Remove(name);
                Classes.Add(Emphasis switch
                {
                    ControlEmphasis.Primary => "primary",
                    ControlEmphasis.Secondary => "secondary",
                    ControlEmphasis.Quiet => "quiet",
                    _ => "destructive"
                });
            }
        };
        Classes.Add("primary");
        Height = 32;
    }

    /// <summary>§6.7: pages bind commands directly, like a Button.</summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public ControlEmphasis Emphasis
    {
        get => GetValue(EmphasisProperty);
        set => SetValue(EmphasisProperty, value);
    }

    public double ButtonHeight
    {
        get => GetValue(ButtonHeightProperty);
        set => SetValue(ButtonHeightProperty, value);
    }

    public bool IsBusy
    {
        get => GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }
}

/// <summary>ControlComponents.swift BusyActionLabel: both labels and the spinner lane reserved.</summary>
public class BusyActionLabel : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<BusyActionLabel, string>(nameof(Title), "");
    public static readonly StyledProperty<string> BusyTitleProperty =
        AvaloniaProperty.Register<BusyActionLabel, string>(nameof(BusyTitle), "");
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<BusyActionLabel, bool>(nameof(IsBusy));

    public BusyActionLabel()
    {
        VerticalAlignment = VerticalAlignment.Center;
        PropertyChanged += (_, change) =>
        {
            if (change.Property == IsBusyProperty || change.Property == TitleProperty || change.Property == BusyTitleProperty) Rebuild();
        };
        Rebuild();
    }

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string BusyTitle { get => GetValue(BusyTitleProperty); set => SetValue(BusyTitleProperty, value); }
    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    private void Rebuild()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(new ProgressBar
        {
            Width = 14, MaxWidth = 14, Height = 14, MinWidth = 14, IsIndeterminate = true, IsVisible = IsBusy,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = IsBusy ? BusyTitle : Title, FontSize = 13, FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center
        });
        Content = panel;
    }
}

/// <summary>DesignSystem.swift AIHeading: sparkle glyph + overline in the AI colour.</summary>
public class AIHeading : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AIHeading, string>(nameof(Title), "");
    public AIHeading()
    {
        PropertyChanged += (_, change) => { if (change.Property == TitleProperty) Rebuild(); };
        Rebuild();
    }
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    private void Rebuild()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = "✦", FontSize = 11, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = Title, Classes = { "overline" }, VerticalAlignment = VerticalAlignment.Center });
        Content = panel;
    }
}

/// <summary>DesignSystem.swift InkSegmented: sunken track, raised white selected thumb.</summary>
public class InkSegmented : ListBox
{
    public InkSegmented()
    {
        SelectionMode = SelectionMode.Single;
        Background = Brushes.Transparent;
        ItemsPanel = new FuncTemplate<Panel>(() => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 });
        ItemTemplate = new FuncDataTemplate<string>((value, _) => new TextBlock
        {
            Text = value ?? "", FontSize = 13, FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
        }, true);
    }
}

/// <summary>DesignSystem.swift InteractiveRowBody: hover/pressed wash on a whole row.</summary>
public class InteractiveRowBody : ContentControl
{
    public InteractiveRowBody()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        MinHeight = 32;
    }
}

/// <summary>DesignSystem.swift ShortcutHint: 11px medium monospaced digits.</summary>
public class ShortcutHint : ContentControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ShortcutHint, string>(nameof(Text), "");
    public static readonly StyledProperty<bool> OnInkProperty =
        AvaloniaProperty.Register<ShortcutHint, bool>(nameof(OnInk));

    public ShortcutHint()
    {
        PropertyChanged += (_, change) =>
        {
            if (change.Property == TextProperty || change.Property == OnInkProperty) Rebuild();
        };
        Rebuild();
    }

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool OnInk { get => GetValue(OnInkProperty); set => SetValue(OnInkProperty, value); }

    private void Rebuild() => Content = new TextBlock
    {
        Text = Text, FontSize = 11, FontWeight = FontWeight.Medium,
        Foreground = OnInk ? Brushes.White : null, Classes = { OnInk ? "" : "caption" }
    };
}

/// <summary>DesignSystem.swift StatusDot: live/ready/caution/idle 7px dot, always with words.</summary>
public class StatusDot : ContentControl
{
    public static readonly StyledProperty<StatusTone> ToneProperty =
        AvaloniaProperty.Register<StatusDot, StatusTone>(nameof(Tone), StatusTone.Idle);
    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<StatusDot, double>(nameof(DotSize), 7);

    public StatusDot()
    {
        Width = 7; Height = 7; VerticalAlignment = VerticalAlignment.Center;
        PropertyChanged += (_, change) => { if (change.Property == ToneProperty) UpdateTone(Tone); };
        UpdateTone(Tone);
    }

    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public double DotSize { get => GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }

    private void UpdateTone(StatusTone tone)
    {
        foreach (var name in new[] { "tone-live", "tone-ready", "tone-caution", "tone-idle" }) Classes.Remove(name);
        Classes.Add(tone switch
        {
            StatusTone.Live => "tone-live",
            StatusTone.Ready => "tone-ready",
            StatusTone.Caution => "tone-caution",
            _ => "tone-idle"
        });
    }
}

/// <summary>HitTargets.swift InkSwitch: 34x20 capsule with a 16px knob.</summary>
public class InkSwitch : Avalonia.Controls.Primitives.ToggleButton
{
    public InkSwitch() { Focusable = true; Cursor = new Cursor(StandardCursorType.Hand); }
}

/// <summary>HitTargets.swift WideToggleRow: label plus InkSwitch, whole row clickable.</summary>
public class WideToggleRow : Avalonia.Controls.Primitives.ToggleButton
{
    public WideToggleRow()
    {
        Focusable = true;
        MinHeight = 36;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Cursor = new Cursor(StandardCursorType.Hand);
    }
}

/// <summary>LiquidGlass.swift AppBackdrop: plain paper behind everything.</summary>
public class AppBackdrop : ContentControl
{
}

/// <summary>LiquidGlass.swift GlassGroup: a fallback glass panel grouping controls.</summary>
public class GlassGroup : ContentControl
{
    public GlassGroup() => Padding = new Thickness(12);
}

/// <summary>LiquidGlass.swift MenuPicker: paper dropdown whose labels stay ink-coloured.</summary>
public class MenuPicker : ComboBox
{
}

/// <summary>LiquidGlass.swift PillTabs (Swift: UnderlineTabs with pill chrome).</summary>
public class PillTabs : ListBox
{
    public PillTabs()
    {
        SelectionMode = SelectionMode.Single;
        Background = Brushes.Transparent;
        ItemsPanel = new FuncTemplate<Panel>(() => new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = double.NaN });
        ItemTemplate = new FuncDataTemplate<string>((value, _) => new TextBlock
        {
            Text = value ?? "", FontSize = 13, FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center
        }, true);
    }
}

/// <summary>LiquidGlass.swift ProminentFill: solid ink rounded fill for key actions.</summary>
public class ProminentFill : ContentControl
{
}

/// <summary>LiquidGlass.swift ScrollEdgeFade: paper-to-clear gradient under a bar.</summary>
public class ScrollEdgeFade : ContentControl
{
    public static readonly StyledProperty<bool> BottomProperty =
        AvaloniaProperty.Register<ScrollEdgeFade, bool>(nameof(Bottom));
    public static readonly StyledProperty<double> FadeHeightProperty =
        AvaloniaProperty.Register<ScrollEdgeFade, double>(nameof(FadeHeight), 36);

    public ScrollEdgeFade()
    {
        IsHitTestVisible = false;
        PropertyChanged += (_, change) =>
        {
            if (change.Property == BottomProperty) Classes.Set("bottom", Bottom);
            if (change.Property == FadeHeightProperty) Height = FadeHeight;
        };
    }

    public bool Bottom { get => GetValue(BottomProperty); set => SetValue(BottomProperty, value); }
    public double FadeHeight { get => GetValue(FadeHeightProperty); set => SetValue(FadeHeightProperty, value); }
}

/// <summary>OperationFeedback.swift OperationFeedback: icon + caption in the tone colour.</summary>
public class OperationFeedback : ContentControl
{
    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<OperationFeedback, string>(nameof(Message), "");
    public static readonly StyledProperty<FeedbackTone> ToneProperty =
        AvaloniaProperty.Register<OperationFeedback, FeedbackTone>(nameof(Tone));

    public OperationFeedback()
    {
        Padding = new Thickness(0, 6);
        PropertyChanged += (_, change) => { if (change.Property == MessageProperty) Rebuild(); };
        PropertyChanged += (_, change) => { if (change.Property == ToneProperty) UpdateTone(Tone); };
        UpdateTone(Tone);
    }

    public string Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public FeedbackTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    private void Rebuild()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = Tone switch
            {
                FeedbackTone.Success => "✓",
                FeedbackTone.Warning => "!",
                FeedbackTone.Failure => "✕",
                _ => "ⓘ"
            },
            Width = 16, FontSize = 12
        });
        panel.Children.Add(new TextBlock { Text = Message, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        Content = panel;
    }

    private void UpdateTone(FeedbackTone tone) =>
        Classes.Set("tone-failure", tone == FeedbackTone.Failure);
}

/// <summary>PaperTextFieldStyle.swift PaperFieldBody: 13px paper field, 9px corners, 2px focus ring.</summary>
public class PaperFieldBody : TextBox
{
    public PaperFieldBody()
    {
        FontSize = 13;
        MinHeight = 32;
        Padding = new Thickness(10, 7);
    }
}

/// <summary>Stage.swift Lift: paper card button that rises on hover.</summary>
public class Lift : Button
{
    public Lift()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }
}

/// <summary>Stage.swift VoiceWave: five rounded bars breathing with the audio level.</summary>
public class VoiceWave : Control
{
    public static readonly StyledProperty<int> BarsProperty =
        AvaloniaProperty.Register<VoiceWave, int>(nameof(Bars), 5);
    public static readonly StyledProperty<double> LevelProperty =
        AvaloniaProperty.Register<VoiceWave, double>(nameof(Level), 0.6);
    public static readonly StyledProperty<bool> ActiveProperty =
        AvaloniaProperty.Register<VoiceWave, bool>(nameof(Active), true);

    public int Bars { get => GetValue(BarsProperty); set => SetValue(BarsProperty, value); }
    public double Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public bool Active { get => GetValue(ActiveProperty); set => SetValue(ActiveProperty, value); }

    static VoiceWave()
    {
        AffectsRender<VoiceWave>(BarsProperty, LevelProperty, ActiveProperty);
        AffectsMeasure<VoiceWave>(BarsProperty);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Bars * 3 + Math.Max(0, Bars - 1) * 2.5, 16);

    public override void Render(DrawingContext context)
    {
        var spacing = 2.5;
        var width = (Bounds.Width - spacing * (Bars - 1)) / Math.Max(1, Bars);
        var color = Active
            ? new SolidColorBrush(Color.Parse("#6B6963"))
            : new SolidColorBrush(Color.Parse("#706E68"));
        for (var index = 0; index < Bars; index++)
        {
            var wobble = 0.55 + 0.45 * Math.Sin(index * 1.9);
            var height = Math.Max(3, Bounds.Height * Math.Min(1, Level * 1.6) * wobble);
            var rect = new Rect(index * (width + spacing), (Bounds.Height - height) / 2, width, height);
            var rounded = new RoundedRect(rect, width / 2);
            context.DrawRectangle(color, null, rounded);
        }
    }
}

/// <summary>Theme.swift MetricPill: symbol + 11px medium text in a glass capsule.</summary>
public class MetricPill : ContentControl
{
    public static readonly StyledProperty<string> SymbolProperty =
        AvaloniaProperty.Register<MetricPill, string>(nameof(Symbol), "");
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MetricPill, string>(nameof(Text), "");

    public MetricPill()
    {
        PropertyChanged += (_, change) =>
        {
            if (change.Property == SymbolProperty || change.Property == TextProperty) Rebuild();
        };
        Rebuild();
    }

    public string Symbol { get => GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    private void Rebuild()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        if (Symbol.Length > 0) panel.Children.Add(new TextBlock { Text = Symbol, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = Text, FontSize = 11, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center });
        Content = panel;
    }
}

/// <summary>Theme.swift RefinementBar: quick follow-up chips on the answer card.</summary>
public class RefinementBar : ContentControl
{
    private static readonly string[] DefaultActions = ["更简短", "举例", "英文版", "⋯"];
    public static readonly StyledProperty<IReadOnlyList<string>?> ActionsProperty =
        AvaloniaProperty.Register<RefinementBar, IReadOnlyList<string>?>(nameof(Actions));

    public RefinementBar()
    {
        PropertyChanged += (_, change) => { if (change.Property == ActionsProperty) Rebuild(); };
        Rebuild();
    }

    public IReadOnlyList<string>? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    private void Rebuild()
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = double.NaN };
        foreach (var action in Actions ?? DefaultActions)
            panel.Children.Add(new Button { Content = action, Classes = { "quiet", "chip" }, FontSize = 12 });
        Content = panel;
    }
}

/// <summary>UnderlineTabs.swift UnderlineTabs: labels with selected underline and hover colour.</summary>
public class UnderlineTabs : ListBox
{
    public UnderlineTabs()
    {
        SelectionMode = SelectionMode.Single;
        Background = Brushes.Transparent;
        ItemsPanel = new FuncTemplate<Panel>(() => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 });
        ItemTemplate = new FuncDataTemplate<string>((value, _) => new TextBlock
        {
            Text = value ?? "", FontSize = 13, FontWeight = FontWeight.Medium, Margin = new Thickness(0, 0, 0, 6),
            VerticalAlignment = VerticalAlignment.Center
        }, true);
    }
}
