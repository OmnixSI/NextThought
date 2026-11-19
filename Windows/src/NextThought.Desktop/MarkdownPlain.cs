using System.Text.RegularExpressions;

namespace NextThought.Desktop;

/// <summary>Strips the Markdown markers the cue island and other one-line surfaces cannot show.</summary>
public static partial class MarkdownPlain
{
    public static string Strip(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var value = Link().Replace(text, "$1");
        value = Bold().Replace(value, "$1");
        value = Code().Replace(value, "$1");
        value = Italic().Replace(value, "$1");
        value = Heading().Replace(value, "$1");
        return value.Replace("**", "").Trim();
    }

    [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Singleline)] private static partial Regex Bold();
    [GeneratedRegex(@"`([^`]+)`")] private static partial Regex Code();
    [GeneratedRegex(@"(?<!\*)\*([^*]+)\*(?!\*)")] private static partial Regex Italic();
    [GeneratedRegex(@"^#+\s*(.+)$", RegexOptions.Multiline)] private static partial Regex Heading();
    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)")] private static partial Regex Link();
}
