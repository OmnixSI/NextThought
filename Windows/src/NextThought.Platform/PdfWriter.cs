using SkiaSharp;

namespace NextThought.Platform;

/// <summary>
/// PDF export through the SkiaSharp already shipped with Avalonia: A4 pages, embedded
/// Microsoft YaHei, headings and wrapped body text.
/// </summary>
public static class PdfWriter
{
    private const float PageWidth = 595f;
    private const float PageHeight = 842f;
    private const float Margin = 56f;

    public static void Write(string markdown, string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        using var stream = File.Create(path);
        using var document = SKDocument.CreatePdf(stream);
        using var regular = LoadCjkFont();
        using var bold = regular;
        using var paint = new SKPaint { IsAntialias = true, Color = SKColors.Black };
        using var font = new SKFont();

        var canvas = document.BeginPage(PageWidth, PageHeight);
        var y = Margin;
        void NewPage()
        {
            document.EndPage();
            canvas = document.BeginPage(PageWidth, PageHeight);
            y = Margin;
        }

        var normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (var raw in normalized.Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0)
            {
                y += 8;
                if (y > PageHeight - Margin) NewPage();
                continue;
            }

            float size;
            SKTypeface typeface;
            var prefix = "";
            if (line.StartsWith("# ", StringComparison.Ordinal)) { size = 20; typeface = bold; line = line[2..]; }
            else if (line.StartsWith("## ", StringComparison.Ordinal)) { size = 15; typeface = bold; line = line[3..]; }
            else if (line.StartsWith("### ", StringComparison.Ordinal)) { size = 12.5f; typeface = bold; line = line[4..]; }
            else if (line.StartsWith("> ", StringComparison.Ordinal)) { size = 10.5f; typeface = regular; prefix = "“"; line = line[2..] + "”"; }
            else if (line.StartsWith("  - ", StringComparison.Ordinal)) { size = 10.5f; typeface = regular; prefix = "    ◦ "; line = line[4..]; }
            else if (line.StartsWith("- ", StringComparison.Ordinal)) { size = 10.5f; typeface = regular; prefix = "• "; line = line[2..]; }
            else { size = 10.5f; typeface = regular; }

            font.Typeface = typeface;
            font.Size = size;
            var text = prefix + Plain(line);
            foreach (var part in Wrap(text, font, PageWidth - Margin * 2))
            {
                if (y + size > PageHeight - Margin) NewPage();
                canvas.DrawText(part, Margin, y, SKTextAlign.Left, font, paint);
                y += size * 1.55f;
            }
            if (typeface == bold) y += 3;
        }

        document.EndPage();
        document.Close();
    }

    private static SKTypeface LoadCjkFont()
    {
        // SimHei is smaller than YaHei; embed it when present so exported PDFs stay moderate.
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var name in new[] { "simhei.ttf", "msyh.ttc", "simsun.ttc" })
        {
            var path = Path.Combine(fonts, name);
            if (File.Exists(path))
            {
                try { return SKTypeface.FromFile(path); } catch { }
            }
        }
        return SKTypeface.FromFamilyName("Microsoft YaHei") ?? SKTypeface.Default;
    }

    private static IEnumerable<string> Wrap(string text, SKFont font, float width)
    {
        var current = "";
        foreach (var rune in text.EnumerateRunes())
        {
            var candidate = current + rune;
            if (font.MeasureText(candidate) > width && current.Length > 0)
            {
                yield return current;
                current = rune.ToString();
            }
            else current = candidate;
        }
        if (current.Length > 0) yield return current;
    }

    private static string Plain(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '`') continue;
            if (text[index] == '*' && index + 1 < text.Length && text[index + 1] == '*') { index++; continue; }
            if (text[index] == '[')
            {
                var close = text.IndexOf(']', index + 1);
                if (close > 0 && close + 1 < text.Length && text[close + 1] == '(')
                {
                    var end = text.IndexOf(')', close + 2);
                    if (end > 0) { builder.Append(text[(index + 1)..close]); index = end; continue; }
                }
            }
            builder.Append(text[index]);
        }
        return builder.ToString();
    }
}
