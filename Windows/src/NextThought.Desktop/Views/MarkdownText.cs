using System.Text;
using System.Text.RegularExpressions;
using NextThought.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace NextThought.Desktop.Views;

/// <summary>
/// A small Markdown renderer for answers and notes, ported from the Mac app's
/// <c>MarkdownText</c> (Theme.swift): line-oriented parsing, inline bold / italic /
/// code, headings, bullets, numbered items, checkboxes and quotes. It is deliberately
/// not a full Markdown engine — tables and images stay as text.
/// </summary>
public class MarkdownText : ContentControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MarkdownText, string>(nameof(Text), "");

    public static readonly StyledProperty<double> BaseFontSizeProperty =
        AvaloniaProperty.Register<MarkdownText, double>(nameof(BaseFontSize), 15d);

    private static readonly Regex InlinePattern = new(@"(\*\*(?<bold>.+?)\*\*)|(`(?<code>[^`]+)`)|(\*(?<italic>[^*]+)\*)|(\[(?<label>[^\]]+)\]\([^)]*\))",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>How many blocks the last render produced (self-check: bullets/code are rendered).</summary>
    public int RenderedBlockCount { get; private set; }

    /// <summary>How many inline runs the last render produced (self-check: bold/italic/inline code).</summary>
    public int InlineRunCount { get; private set; }

    public MarkdownText()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Rebuild();
        // §6.1/D6: theme dictionaries are resolved per control; a variant switch must re-resolve them.
        ActualThemeVariantChanged += (_, _) => Rebuild();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double BaseFontSize
    {
        get => GetValue(BaseFontSizeProperty);
        set => SetValue(BaseFontSizeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == BaseFontSizeProperty) Rebuild();
    }

    private void Rebuild()
    {
        var panel = new StackPanel { Spacing = 10 };
        var blocks = ParseBlocks(LaTeXMath.Markdown(Text ?? ""));
        RenderedBlockCount = blocks.Count;
        InlineRunCount = blocks.Sum(block => InlinePattern.Matches(block.Text).Count);
        foreach (var block in blocks)
        {
            panel.Children.Add(BuildBlock(block));
        }
        Content = panel;
    }

    private enum BlockKind { Paragraph, Heading, Bullet, Numbered, Check, Quote, Rule, Code, Table }

    private sealed record Block(BlockKind Kind, string Text, int Level = 0, IReadOnlyList<IReadOnlyList<string>>? Rows = null);

    private static List<Block> ParseBlocks(string text)
    {
        var blocks = new List<Block>();
        var paragraph = new StringBuilder();
        void Flush()
        {
            if (paragraph.Length == 0) return;
            blocks.Add(new Block(BlockKind.Paragraph, paragraph.ToString().Trim()));
            paragraph.Clear();
        }

        var inFence = false;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var raw = lines[index];
            var line = raw.TrimEnd();
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                Flush();
                inFence = !inFence;
                continue;
            }
            if (inFence)
            {
                blocks.Add(new Block(BlockKind.Code, line));
                continue;
            }
            if (line.Length == 0) { Flush(); continue; }
            // decisions §3.1 components 4: tables render as a table (Mac MarkdownText uses a Grid).
            if (line.TrimStart().StartsWith('|')
                && index + 1 < lines.Length && IsTableSeparator(lines[index + 1]))
            {
                Flush();
                var rows = new List<IReadOnlyList<string>> { SplitTableRow(line) };
                index++;
                while (index + 1 < lines.Length && lines[index + 1].TrimStart().StartsWith('|'))
                {
                    index++;
                    rows.Add(SplitTableRow(lines[index]));
                }
                blocks.Add(new Block(BlockKind.Table, "", Rows: rows));
                continue;
            }
            if (line.StartsWith("---", StringComparison.Ordinal) || line.StartsWith("***", StringComparison.Ordinal))
            {
                Flush();
                blocks.Add(new Block(BlockKind.Rule, ""));
                continue;
            }
            if (line.StartsWith('#'))
            {
                Flush();
                var level = line.TakeWhile(c => c == '#').Count();
                blocks.Add(new Block(BlockKind.Heading, line[level..].Trim(), Math.Clamp(level, 1, 3)));
                continue;
            }
            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                Flush();
                blocks.Add(new Block(BlockKind.Quote, line[2..].Trim()));
                continue;
            }
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- [ ] ", StringComparison.Ordinal) || trimmed.StartsWith("- [x] ", StringComparison.Ordinal)
                || trimmed.StartsWith("- [X] ", StringComparison.Ordinal))
            {
                Flush();
                var done = trimmed[3] is 'x' or 'X';
                blocks.Add(new Block(BlockKind.Check, trimmed[6..].Trim() + (done ? "\u0001" : "\u0002")));
                continue;
            }
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal) || trimmed.StartsWith("• ", StringComparison.Ordinal))
            {
                Flush();
                blocks.Add(new Block(BlockKind.Bullet, trimmed[2..].Trim()));
                continue;
            }
            var numbered = Regex.Match(trimmed, @"^(\d+)[.)]\s+(.*)$");
            if (numbered.Success)
            {
                Flush();
                blocks.Add(new Block(BlockKind.Numbered, trimmed));
                continue;
            }
            if (paragraph.Length > 0) paragraph.Append(' ');
            paragraph.Append(line.Trim());
        }
        Flush();
        return blocks;
    }

    private static bool IsTableSeparator(string line)
    {
        var cells = SplitTableRow(line);
        return cells.Count > 0 && cells.All(cell => System.Text.RegularExpressions.Regex.IsMatch(cell, "^:?-{2,}:?$"));
    }

    private static IReadOnlyList<string> SplitTableRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|')) trimmed = trimmed[1..];
        if (trimmed.EndsWith('|')) trimmed = trimmed[..^1];
        return trimmed.Split('|').Select(cell => cell.Trim()).ToList();
    }

    private Control Table(Block block, double size)
    {
        var rows = block.Rows ?? [];
        var columns = rows.Count == 0 ? 1 : rows.Max(row => row.Count);
        var grid = new Grid { RowDefinitions = new RowDefinitions() };
        grid.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", columns)));
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var row = rows[rowIndex];
            for (var column = 0; column < columns; column++)
            {
                var cell = column < row.Count ? row[column] : "";
                var content = Inline(cell, size - 1, rowIndex == 0 ? FontWeight.SemiBold : FontWeight.Normal,
                    Fonts.Sans, "InkBrush", 1.5);
                var border = new Border
                {
                    BorderBrush = Resource("Hairline"),
                    BorderThickness = new Thickness(0, 0, 0, 0.5),
                    Padding = new Thickness(8, 5),
                    Child = content
                };
                Grid.SetRow(border, rowIndex);
                Grid.SetColumn(border, column);
                grid.Children.Add(border);
            }
        }
        return new Border
        {
            BorderBrush = Resource("Hairline"),
            BorderThickness = new Thickness(0.5, 0.5, 0.5, 0),
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Child = grid
        };
    }

    private Control BuildBlock(Block block)
    {
        var size = BaseFontSize;
        switch (block.Kind)
        {
            case BlockKind.Heading:
                return Inline(block.Text, block.Level == 1 ? size + 8 : block.Level == 2 ? size + 3 : size + 1,
                    FontWeight.SemiBold, Fonts.Serif, "InkBrush", 1.25);
            case BlockKind.Bullet:
                return Bullet("·", block.Text, size);
            case BlockKind.Numbered:
                return Bullet("", block.Text, size);
            case BlockKind.Check:
                var checkedDone = block.Text.EndsWith('\u0001');
                return Bullet(checkedDone ? "\u2611" : "\u2610", block.Text[..^1], size);
            case BlockKind.Quote:
                return new Border
                {
                    BorderBrush = Resource("Hairline"),
                    BorderThickness = new Thickness(2, 0, 0, 0),
                    Padding = new Thickness(12, 2, 0, 2),
                    Child = Inline(block.Text, size, FontWeight.Normal, Fonts.Sans, "Secondary", 1.6)
                };
            case BlockKind.Table:
                return Table(block, size);
            case BlockKind.Rule:
                return new Border { Height = 1, Background = Resource("Hairline"), Margin = new Thickness(0, 4) };
            case BlockKind.Code:
                return new Border
                {
                    Background = Resource("AiSoft"),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 8),
                    Child = new SelectableTextBlock
                    {
                        Text = block.Text,
                        FontFamily = Fonts.Mono,
                        FontSize = size - 2,
                        TextWrapping = TextWrapping.Wrap
                    }
                };
            default:
                return Inline(block.Text, size, FontWeight.Normal, Fonts.Sans, "InkBrush", 1.62);
        }
    }

    private Control Bullet(string marker, string text, double size)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("18,*") };
        if (marker.Length > 0)
        {
            var bullet = new TextBlock
            {
                Text = marker,
                FontSize = size,
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = Resource("Secondary"),
                Margin = new Thickness(0, 1, 0, 0)
            };
            grid.Children.Add(bullet);
        }
        var content = Inline(text, size, FontWeight.Normal, Fonts.Sans, "InkBrush", 1.62);
        Grid.SetColumn(content, 1);
        grid.Children.Add(content);
        return grid;
    }

    private Control Inline(string text, double size, FontWeight weight, FontFamily family, string brushKey, double lineHeight)
    {
        var selectable = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontFamily = family,
            FontSize = size,
            FontWeight = weight,
            LineHeight = size * lineHeight,
            Foreground = Resource(brushKey)
        };
        Inlines(selectable.Inlines!, text);
        return selectable;
    }

    private static void Inlines(InlineCollection target, string text)
    {
        var index = 0;
        foreach (Match match in InlinePattern.Matches(text))
        {
            if (match.Index > index) target.Add(new Run(text[index..match.Index]) { });
            if (match.Groups["bold"].Success)
                target.Add(new Run(match.Groups["bold"].Value) { FontWeight = FontWeight.SemiBold });
            else if (match.Groups["code"].Success)
                target.Add(new Run(match.Groups["code"].Value) { FontFamily = Fonts.Mono, FontSize = 13 });
            else if (match.Groups["italic"].Success)
                target.Add(new Run(match.Groups["italic"].Value) { FontStyle = FontStyle.Italic });
            else if (match.Groups["label"].Success)
                target.Add(new Run(match.Groups["label"].Value) { TextDecorations = TextDecorations.Underline });
            index = match.Index + match.Length;
        }
        if (index < text.Length) target.Add(new Run(text[index..]));
    }

    private IBrush Resource(string key) => ResourceLookup.ByKey(key, this);

    private static class Fonts
    {
        public static readonly FontFamily Sans = new("Inter, Noto Sans SC, Microsoft YaHei UI, Segoe UI");
        public static readonly FontFamily Serif = new("Source Serif 4, Noto Serif SC, SimSun, Georgia");
        public static readonly FontFamily Mono = new("Cascadia Mono, Consolas");
    }

    private static class ResourceLookup
    {
        /// <summary>
        /// D6: resolve through the control's own theme scope first. Application.TryFindResource only
        /// sees the application theme, which returned light Ink for a control inside a dark window.
        /// </summary>
        public static IBrush ByKey(string key, StyledElement origin)
        {
            if (origin.TryFindResource(key, origin.ActualThemeVariant, out var themedValue) && themedValue is IBrush themed)
                return themed;
            if (origin.TryFindResource(key, out var scopedValue) && scopedValue is IBrush scoped)
                return scoped;
            if (Application.Current is { } app && app.TryFindResource(key, origin.ActualThemeVariant, out var appValue)
                && appValue is IBrush appBrush) return appBrush;
            if (Application.Current is { } current && current.TryFindResource(key, out var anyValue) && anyValue is IBrush any)
                return any;
            return Brushes.Black;
        }
    }
}
