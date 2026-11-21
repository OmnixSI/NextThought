using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using NextThought.Core;

namespace NextThought.Desktop.Views.Island;

/// <summary>One cue line as the island draws it (CueIslandView.swift <c>CueRow</c>).</summary>
public sealed class CueRowItem
{
    public CueLine.Kind Kind { get; init; }
    public string Head { get; init; } = "";
    public string Rest { get; init; } = "";
    public string Gloss { get; init; } = "";
    public bool Said { get; init; }
    public bool Current { get; init; }
    public bool Expanded { get; init; }
    public double TextSize { get; init; } = 22;
    public IBrush MarkerBrush { get; init; } = Brushes.White;
}

/// <summary>One launcher chip: a scene, its pigment dot and the scene name (CueIslandView launcher).</summary>
public sealed class LauncherSceneItem
{
    public required string Name { get; init; }
    public required string ModeKey { get; init; }
    public required IBrush Tint { get; init; }
}

/// <summary>
/// C5/C7: one cue row — the words to catch first in white, the rest behind them, a capsule that
/// fills in on the line to say next, and the meaning underneath for a bilingual answer. The row
/// is rebuilt from its <see cref="CueRowItem"/> whenever the island refreshes.
/// </summary>
public sealed class IslandCueRow : UserControl
{
    private static readonly IBrush White = new SolidColorBrush(Colors.White);
    private static readonly IBrush RestBrush = new SolidColorBrush(Colors.White, 0.66);
    private static readonly IBrush RestNoHeadBrush = new SolidColorBrush(Colors.White, 0.90);
    private static readonly IBrush GlossBrush = new SolidColorBrush(Colors.White, 0.52);
    private static readonly IBrush SaidGlossBrush = new SolidColorBrush(Colors.White, 0.28);

    private readonly Border marker;
    private readonly TextBlock line;
    private readonly TextBlock gloss;
    private readonly Grid grid;

    public IslandCueRow()
    {
        marker = new Border
        {
            Width = 3,
            CornerRadius = new CornerRadius(1.5),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            IsVisible = false
        };
        line = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = White
        };
        gloss = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = GlossBrush,
            IsVisible = false
        };
        var texts = new StackPanel { Spacing = 1 };
        texts.Children.Add(line);
        texts.Children.Add(gloss);
        grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(marker);
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);
        Content = grid;
        DataContextChanged += (_, _) => Rebuild();
    }

    private CueRowItem? Row => DataContext as CueRowItem;

    private void Rebuild()
    {
        if (Row is not { } row)
        {
            line.Text = "";
            return;
        }
        var size = row.TextSize;
        var lead = row.Kind == CueLine.Kind.Lead;
        // The rows breathe by a third of the type size (CueIslandView.cue VStack spacing).
        Margin = new Thickness(0, 0, 0, size * 0.34);
        marker.IsVisible = !lead;
        marker.Height = Math.Max(6, size * 0.56);
        marker.Margin = new Thickness(0, size * 0.26, 9, 0);
        marker.Background = row.Current ? row.MarkerBrush : new SolidColorBrush(Colors.White, 0.28);
        grid.Margin = new Thickness(0);

        if (lead)
        {
            line.Text = row.Rest.Length > 0 && row.Head.Length == 0 ? row.Rest : row.Head + row.Rest;
            line.FontSize = size * 0.9;
            line.FontWeight = FontWeight.SemiBold;
            line.LineHeight = size * 0.9 * 1.2 + size * 0.14;
            line.MaxLines = row.Expanded ? 5 : 2;
            line.Inlines = null;
        }
        else
        {
            line.Inlines = new InlineCollection();
            if (row.Head.Length > 0)
                line.Inlines.Add(new Run { Text = row.Head, FontWeight = FontWeight.SemiBold, Foreground = White });
            if (row.Head.Length > 0 && row.Rest.Length > 0)
                line.Inlines.Add(new Run { Text = "  " });
            if (row.Rest.Length > 0)
                line.Inlines.Add(new Run
                {
                    Text = row.Rest,
                    Foreground = row.Head.Length == 0 ? RestNoHeadBrush : RestBrush
                });
            line.FontSize = size * 0.76;
            line.FontWeight = FontWeight.Normal;
            line.LineHeight = size * 0.76 * 1.2 + size * 0.12;
            line.MaxLines = row.Expanded ? 4 : 1;
        }

        gloss.IsVisible = row.Gloss.Length > 0;
        gloss.Text = row.Gloss;
        gloss.FontSize = Math.Max(11, size * 0.56);
        gloss.LineHeight = Math.Max(11, size * 0.56) * 1.25;
        gloss.Margin = new Thickness(lead ? 0 : 12, 1, 0, 0);
        gloss.MaxLines = row.Expanded ? 3 : 1;
        gloss.Foreground = row.Said ? SaidGlossBrush : GlossBrush;

        Opacity = row.Said ? 0.36 : 1;
    }
}
