using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// 因果鱼骨图: the head is the problem on the right, the spine runs left, and each category is a
/// rib with its causes written along it. A plain control rather than a drawing library so the text
/// stays selectable and wraps in the same fonts as the rest of the app.
/// </summary>
public class FishboneView : UserControl
{
    public static readonly StyledProperty<FishboneNotes?> NotesProperty =
        AvaloniaProperty.Register<FishboneView, FishboneNotes?>(nameof(Notes));

    private readonly Canvas canvas = new();
    private readonly List<(int At, TextBlock Label)> jumpTargets = [];

    public FishboneView()
    {
        Content = canvas;
        MinHeight = 320;
        SizeChanged += (_, _) => Rebuild();
    }

    public FishboneNotes? Notes
    {
        get => GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    /// <summary>Raised when a cause with a time is clicked (jump back to the transcript).</summary>
    public event Action<int>? Jump;

    public void RaiseJumpForCheck(int seconds) => Jump?.Invoke(seconds);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NotesProperty) Rebuild();
    }

    private static IBrush Brush(string key) =>
        Application.Current?.TryFindResource(key, out var value) == true && value is IBrush brush ? brush : Brushes.Black;

    private static FontFamily Serif => new("Source Serif 4, Noto Serif SC, SimSun, Georgia");
    private static FontFamily Sans => new("Inter, Noto Sans SC, Microsoft YaHei UI, Segoe UI");

    private void Rebuild()
    {
        canvas.Children.Clear();
        jumpTargets.Clear();
        if (Notes is not { } notes || Bounds.Width < 200) return;

        var width = Bounds.Width;
        var height = Math.Max(320, Bounds.Height);
        var midY = height / 2;
        var headWidth = 200.0;
        var spineLeft = 24.0;
        var spineRight = width - headWidth - 24;
        var ink = Brush("InkBrush");
        var hairline = Brush("Hairline");
        var secondary = Brush("Secondary");
        var seal = Brush("Seal");

        // Spine and head.
        canvas.Children.Add(new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Point(spineLeft, midY),
            EndPoint = new Point(spineRight, midY),
            Stroke = ink,
            StrokeThickness = 2
        });
        var head = new TextBlock
        {
            Text = notes.Head,
            Width = headWidth,
            FontFamily = Serif,
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = ink,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Left
        };
        Canvas.SetLeft(head, width - headWidth - 12);
        Canvas.SetTop(head, midY - 30);
        canvas.Children.Add(head);
        canvas.Children.Add(new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Point(width - headWidth - 16, midY - 34),
            EndPoint = new Point(width - 12, midY - 34),
            Stroke = seal,
            StrokeThickness = 1.5
        });

        var categories = notes.Categories;
        if (categories.Count == 0) return;
        var slot = (spineRight - spineLeft - 40) / categories.Count;
        for (var index = 0; index < categories.Count; index++)
        {
            var category = categories[index];
            var above = index % 2 == 0;
            var x = spineLeft + 40 + slot * index + slot * 0.55;
            var inner = x - 46;
            var outerY = above ? 34.0 : height - 34;
            var labelY = above ? 10.0 : height - 30;

            canvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Point(x, midY),
                EndPoint = new Point(inner, outerY),
                Stroke = hairline,
                StrokeThickness = 1.5
            });
            var title = new TextBlock
            {
                Text = category.Name,
                FontFamily = Sans,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = ink,
                TextWrapping = TextWrapping.Wrap,
                Width = Math.Max(70, slot - 18)
            };
            Canvas.SetLeft(title, inner - 8);
            Canvas.SetTop(title, above ? labelY : labelY + 12);
            canvas.Children.Add(title);

            // Causes stacked next to the rib, away from the spine.
            var causeTop = above ? labelY + 26 : labelY - 8;
            var step = above ? 34 : -34;
            for (var causeIndex = 0; causeIndex < category.Causes.Count; causeIndex++)
            {
                var cause = category.Causes[causeIndex];
                var label = new TextBlock
                {
                    Text = cause.Text + (cause.At is { } at ? $"  {NotesTime.Text(at)}" : ""),
                    FontFamily = Sans,
                    FontSize = 12,
                    Foreground = secondary,
                    TextWrapping = TextWrapping.Wrap,
                    Width = Math.Max(90, slot - 14),
                    Cursor = cause.At is null ? null : new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                };
                Canvas.SetLeft(label, x + 6);
                Canvas.SetTop(label, causeTop + step * causeIndex);
                if (cause.At is { } seconds)
                {
                    var captured = seconds;
                    label.PointerPressed += (_, _) => Jump?.Invoke(captured);
                    jumpTargets.Add((captured, label));
                }
                canvas.Children.Add(label);
            }
        }
    }
}
