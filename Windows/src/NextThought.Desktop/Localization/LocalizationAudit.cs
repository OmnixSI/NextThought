using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace NextThought.Desktop.Localization;

/// <summary>
/// G9 evidence collector: reads every piece of text a page currently shows (including tooltips that
/// are not open) so the English screenshot batch can prove there is no Chinese outside user data.
/// This is a read-only audit — unlike the removed load-time tree walk it never writes text back
/// and no production view calls it.
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

        foreach (var window in root.GetVisualDescendants().OfType<Window>().Prepend(root as Window).Where(window => window is not null))
            Add("title", window!.Title);
        foreach (var text in root.GetVisualDescendants().OfType<TextBlock>()) Add("text", text.Text);
        foreach (var box in root.GetVisualDescendants().OfType<TextBox>())
        {
            Add("edit", box.Text);
            Add("placeholder", box.PlaceholderText);
        }
        foreach (var box in root.GetVisualDescendants().OfType<ComboBox>()) Add("placeholder", box.PlaceholderText);
        foreach (var control in root.GetVisualDescendants().OfType<ContentControl>())
            if (control.Content is string value) Add("content", value);
        foreach (var item in root.GetVisualDescendants().OfType<MenuItem>())
            if (item.Header is string header) Add("menu", header);
        foreach (var control in root.GetVisualDescendants().OfType<Control>())
        {
            if (ToolTip.GetTip(control) is string tip) Add("tooltip", tip);
        }
        rows.Sort(StringComparer.Ordinal);
        return rows;
    }

    /// <summary>Writes a page inventory next to its screenshot (UTF-8, one kind/text pair per line).</summary>
    public static async Task WriteAsync(Visual root, string directory, string name)
    {
        Directory.CreateDirectory(directory);
        var lines = Collect(root).Select(row => row.Replace("\u0001", "\t", StringComparison.Ordinal)).ToArray();
        await File.WriteAllLinesAsync(Path.Combine(directory, name + ".txt"), lines);
    }
}
