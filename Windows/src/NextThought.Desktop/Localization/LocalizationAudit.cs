using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace NextThought.Desktop.Localization;

/// <summary>
/// G9 evidence collector: reads every piece of text a page currently shows (including tooltips that
/// are not open) so the English screenshot batch can prove there is no Chinese outside user data.
/// Only controls that are effectively visible are collected, so a hidden page from another tab does
/// not leak into the current page's inventory. This is a read-only audit — unlike the removed
/// load-time tree walk it never writes text back and no production view calls it.
/// </summary>
public static class LocalizationAudit
{
    public static IReadOnlyList<string> Collect(Visual root)
    {
        var rows = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(string kind, string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var trimmed = text.Trim();
            if (trimmed.Length == 0) return;
            if (seen.Add(kind + "\u0001" + trimmed)) rows.Add(kind + "\u0001" + trimmed);
        }

        foreach (var window in root.GetVisualDescendants().OfType<Window>().Prepend(root as Window))
        {
            if (window is null || !window.IsVisible) continue;
            Add("title", window.Title);
        }
        foreach (var text in root.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!text.IsEffectivelyVisible) continue;
            Add("text", text.Text);
        }
        foreach (var box in root.GetVisualDescendants().OfType<TextBox>())
        {
            if (!box.IsEffectivelyVisible) continue;
            Add("edit", box.Text);
            Add("placeholder", box.PlaceholderText);
        }
        foreach (var box in root.GetVisualDescendants().OfType<ComboBox>())
        {
            if (!box.IsEffectivelyVisible) continue;
            Add("placeholder", box.PlaceholderText);
        }
        foreach (var control in root.GetVisualDescendants().OfType<ContentControl>())
        {
            if (!control.IsEffectivelyVisible) continue;
            if (control.Content is string value) Add("content", value);
        }
        foreach (var item in root.GetVisualDescendants().OfType<MenuItem>())
        {
            if (!item.IsEffectivelyVisible) continue;
            if (item.Header is string header) Add("menu", header);
        }
        foreach (var control in root.GetVisualDescendants().OfType<Control>())
        {
            if (!control.IsEffectivelyVisible) continue;
            if (ToolTip.GetTip(control) is string tip) Add("tooltip", tip);
        }
        rows.Sort(StringComparer.Ordinal);
        return rows;
    }

    /// <summary>
    /// G10 §6.8: text that does not fit its laid-out bounds. A visible TextBlock is measured again
    /// without the parent's constraint and compared with the bounds it actually got: a NoWrap line
    /// wider than its box is clipped, and a wrapped line taller than its box loses whole lines.
    /// </summary>
    public static IReadOnlyList<string> CollectTruncations(Visual root)
    {
        var rows = new List<string>();
        foreach (var text in root.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!text.IsEffectivelyVisible || string.IsNullOrWhiteSpace(text.Text)) continue;
            var bounds = text.Bounds;
            if (bounds.Width <= 1 || bounds.Height <= 1) continue;
            var probe = new TextBlock
            {
                Text = text.Text,
                FontFamily = text.FontFamily,
                FontSize = text.FontSize,
                FontWeight = text.FontWeight,
                FontStyle = text.FontStyle,
                LetterSpacing = text.LetterSpacing,
                TextWrapping = text.TextWrapping,
                MaxLines = 0,
                TextTrimming = Avalonia.Media.TextTrimming.None
            };
            var width = text.TextWrapping == TextWrapping.NoWrap ? double.PositiveInfinity : bounds.Width;
            probe.Measure(new Size(width, double.PositiveInfinity));
            var needed = probe.DesiredSize;
            var over = text.TextWrapping == TextWrapping.NoWrap
                ? (needed.Width > bounds.Width + 1.5 ? $"{needed.Width:0.0}>{bounds.Width:0.0}" : null)
                : (needed.Height > bounds.Height + 1.5 ? $"{needed.Height:0.0}>{bounds.Height:0.0}" : null);
            if (over is not null)
            {
                var owner = "text";
                for (var parent = text.Parent; parent is not null; parent = parent.Parent)
                    if (parent is Control { Name.Length: > 0 } named)
                    {
                        owner = named.GetType().Name + "#" + named.Name;
                        break;
                    }
                rows.Add($"text\t{over}\t{owner}\t{text.Text}");
            }
        }
        rows.Sort(StringComparer.Ordinal);
        return rows;
    }

    /// <summary>Writes the truncation rows for one English page (empty file when nothing overflows).</summary>
    public static async Task WriteTruncationsAsync(Visual root, string directory, string name)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllLinesAsync(Path.Combine(directory, name + ".truncation.txt"), CollectTruncations(root));
    }

    /// <summary>Writes a page inventory next to its screenshot (UTF-8, one kind/text pair per line).</summary>
    public static async Task WriteAsync(Visual root, string directory, string name)
    {
        Directory.CreateDirectory(directory);
        var lines = Collect(root).Select(row => row.Replace("\u0001", "\t", StringComparison.Ordinal)).ToArray();
        await File.WriteAllLinesAsync(Path.Combine(directory, name + ".txt"), lines);
    }
}
