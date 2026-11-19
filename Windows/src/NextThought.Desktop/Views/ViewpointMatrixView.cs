using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// 观点对照: rows are the issues, columns are the sides, each cell holds what that side said on
/// that issue (with its time). Below the matrix sit consensus, disagreements and who does what.
/// </summary>
public class ViewpointMatrixView : UserControl
{
    public static readonly StyledProperty<ViewpointMatrix?> MatrixProperty =
        AvaloniaProperty.Register<ViewpointMatrixView, ViewpointMatrix?>(nameof(Matrix));

    private readonly StackPanel root = new() { Spacing = 14 };
    private readonly List<(int At, Control Cell)> jumpTargets = [];

    public ViewpointMatrixView()
    {
        Content = root;
        Rebuild();
    }

    public ViewpointMatrix? Matrix
    {
        get => GetValue(MatrixProperty);
        set => SetValue(MatrixProperty, value);
    }

    /// <summary>Raised when an item with a time is clicked (jump back to the transcript).</summary>
    public event Action<int>? Jump;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MatrixProperty) Rebuild();
    }

    private static IBrush Brush(string key) =>
        Application.Current?.TryFindResource(key, out var value) == true && value is IBrush brush ? brush : Brushes.Black;

    private static FontFamily Serif => new("Source Serif 4, Noto Serif SC, SimSun, Georgia");
    private static FontFamily Sans => new("Inter, Noto Sans SC, Microsoft YaHei UI, Segoe UI");

    private void Rebuild()
    {
        root.Children.Clear();
        jumpTargets.Clear();
        if (Matrix is not { } matrix) return;
        var ink = Brush("InkBrush");
        var secondary = Brush("Secondary");
        var hairline = Brush("Hairline");
        var seal = Brush("Seal");

        // Topic.
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock
        {
            Text = matrix.Topic,
            FontFamily = Serif,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = ink,
            TextWrapping = TextWrapping.Wrap
        });
        if (matrix.TopicAt is { } topicAt)
            header.Children.Add(TimeLabel("", topicAt, secondary));
        root.Children.Add(header);

        // The matrix: a header row plus one row per issue.
        var columns = matrix.Parties.Count + 1;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions() };
        grid.ColumnDefinitions.Add(new ColumnDefinition(1.1, GridUnitType.Star));
        for (var index = 0; index < matrix.Parties.Count; index++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        for (var row = 0; row <= matrix.Issues.Count; row++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        AddCell(grid, 0, 0, "议题", header: true);
        for (var column = 0; column < matrix.Parties.Count; column++)
            AddCell(grid, column + 1, 0, matrix.Parties[column], header: true);

        for (var row = 0; row < matrix.Issues.Count; row++)
        {
            var issue = matrix.Issues[row];
            AddCell(grid, 0, row + 1, issue.Title, emphasis: true);
            for (var column = 0; column < matrix.Parties.Count; column++)
            {
                var positions = matrix.PositionsOf(issue, matrix.Parties[column]).ToList();
                var cell = new StackPanel { Spacing = 3 };
                if (positions.Count == 0)
                {
                    cell.Children.Add(new TextBlock
                    {
                        Text = "—",
                        FontFamily = Sans,
                        FontSize = 12,
                        Foreground = secondary,
                        Opacity = 0.6
                    });
                }
                foreach (var position in positions)
                {
                    var text = new TextBlock
                    {
                        Text = position.Text,
                        FontFamily = Sans,
                        FontSize = 12.5,
                        Foreground = ink,
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 19
                    };
                    if (position.At is { } at)
                    {
                        text.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
                        var captured = at;
                        text.PointerPressed += (_, _) => Jump?.Invoke(captured);
                        jumpTargets.Add((at, text));
                    }
                    cell.Children.Add(text);
                    if (position.At is { } positionAt)
                        cell.Children.Add(TimeLabel("", positionAt, secondary));
                }
                AddCell(grid, column + 1, row + 1, cell);
            }
        }
        root.Children.Add(new Border
        {
            BorderBrush = hairline,
            BorderThickness = new Thickness(0.5),
            Child = grid
        });

        AddList("共识", matrix.Consensus, ink, secondary, seal);
        AddList("分歧", matrix.Disagreements, ink, secondary, seal);
        if (matrix.Owners.Count > 0) AddOwners(matrix.Owners, ink, secondary, seal);
    }

    private void AddList(string title, IReadOnlyList<TimedText> items, IBrush ink, IBrush secondary, IBrush seal)
    {
        if (items.Count == 0) return;
        var panel = new StackPanel { Spacing = 5 };
        panel.Children.Add(SectionTitle(title, seal));
        foreach (var item in items)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            line.Children.Add(new TextBlock
            {
                Text = item.Text,
                FontFamily = Sans,
                FontSize = 13,
                Foreground = ink,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 760
            });
            if (item.At is { } at)
            {
                line.Children.Add(TimeLabel("", at, secondary));
                line.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
                var captured = at;
                line.PointerPressed += (_, _) => Jump?.Invoke(captured);
                jumpTargets.Add((at, line));
            }
            panel.Children.Add(line);
        }
        root.Children.Add(panel);
    }

    private void AddOwners(IReadOnlyList<ViewpointOwner> owners, IBrush ink, IBrush secondary, IBrush seal)
    {
        var panel = new StackPanel { Spacing = 5 };
        panel.Children.Add(SectionTitle("分工", seal));
        foreach (var owner in owners)
        {
            var parts = new List<string>();
            if (owner.Who.Length > 0) parts.Add(owner.Who);
            parts.Add(owner.Task);
            if (owner.Due is { Length: > 0 } due) parts.Add("截止 " + due);
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            line.Children.Add(new TextBlock
            {
                Text = string.Join(" · ", parts),
                FontFamily = Sans,
                FontSize = 13,
                Foreground = ink,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 760
            });
            if (owner.At is { } at) line.Children.Add(TimeLabel("", at, secondary));
            panel.Children.Add(line);
        }
        root.Children.Add(panel);
    }

    private static TextBlock SectionTitle(string text, IBrush seal) => new()
    {
        Text = text,
        FontFamily = Sans,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        Foreground = seal
    };

    private static TextBlock TimeLabel(string prefix, int at, IBrush secondary) => new()
    {
        Text = (prefix.Length > 0 ? prefix + " " : "") + NotesTime.Text(at),
        FontFamily = Sans,
        FontSize = 11,
        Foreground = secondary,
        Opacity = 0.75,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static void AddCell(Grid grid, int column, int row, string text, bool header = false, bool emphasis = false)
        => AddCell(grid, column, row, new TextBlock
        {
            Text = text,
            FontFamily = header || emphasis ? Sans : Sans,
            FontSize = header ? 12 : 13,
            FontWeight = header || emphasis ? FontWeight.SemiBold : FontWeight.Normal,
            Foreground = header ? Brush("Secondary") : Brush("InkBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0)
        });

    private static void AddCell(Grid grid, int column, int row, Control content)
    {
        var border = new Border
        {
            BorderBrush = Brush("Hairline"),
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(10, 8),
            Child = content
        };
        Grid.SetColumn(border, column);
        Grid.SetRow(border, row);
        grid.Children.Add(border);
    }
}
