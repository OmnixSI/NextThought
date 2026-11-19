using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// Applies the small English UI dictionary to realized controls. Only known static labels are
/// replaced, so meeting titles, transcripts and status text keep their original language.
/// </summary>
public static class UiTranslation
{
    public static void Apply(Visual root, bool english)
    {
        foreach (var text in root.GetVisualDescendants().OfType<TextBlock>())
        {
            if (string.IsNullOrEmpty(text.Text)) continue;
            var translated = UiText.Translate(text.Text, english);
            if (!string.Equals(translated, text.Text, StringComparison.Ordinal)) text.Text = translated;
        }
        foreach (var box in root.GetVisualDescendants().OfType<TextBox>())
        {
            if (box.PlaceholderText is not { Length: > 0 } watermark) continue;
            var translated = UiText.Translate(watermark, english);
            if (!string.Equals(translated, watermark, StringComparison.Ordinal)) box.PlaceholderText = translated;
        }
        foreach (var box in root.GetVisualDescendants().OfType<ComboBox>())
        {
            if (box.PlaceholderText is not { Length: > 0 } placeholder) continue;
            var translated = UiText.Translate(placeholder, english);
            if (!string.Equals(translated, placeholder, StringComparison.Ordinal)) box.PlaceholderText = translated;
        }
        foreach (var control in root.GetVisualDescendants().OfType<ContentControl>())
        {
            if (control.Content is not string value || value.Length == 0) continue;
            var translated = UiText.Translate(value, english);
            if (!string.Equals(translated, value, StringComparison.Ordinal)) control.Content = translated;
        }
    }
}
