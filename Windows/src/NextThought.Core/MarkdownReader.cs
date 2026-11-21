using System.Text;

namespace NextThought.Core;

/// <summary>Port of Theme.swift MarkdownText.block parsing (paragraph/bullet/heading/code).</summary>
public enum MarkdownBlockKind
{
    Paragraph,
    Bullet,
    Heading,
    Code
}

public sealed record MarkdownBlock(MarkdownBlockKind Kind, string Text, int Level = 0);

/// <summary>One inline run of a Markdown line: bold/italic/code are separate runs.</summary>
public enum MarkdownInlineKind
{
    Text,
    Bold,
    Italic,
    Code
}

public sealed record MarkdownInlineRun(string Text, MarkdownInlineKind Kind = MarkdownInlineKind.Text);

/// <summary>
/// A6/§6.1: the Windows port of MarkdownText's renderer input. Blocks keep the Mac shapes
/// (heading level ignored, two-space indent per level) and inline runs carry bold/italic/code so the
/// answer cards can actually draw the emphasis the Mac shows.
/// </summary>
public static class MarkdownReader
{
    public static IReadOnlyList<MarkdownBlock> Blocks(string? text)
    {
        var result = new List<MarkdownBlock>();
        if (string.IsNullOrEmpty(text)) return result;
        var code = new List<string>?();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                if (code is not null)
                {
                    result.Add(new MarkdownBlock(MarkdownBlockKind.Code, string.Join("\n", code)));
                    code = null;
                }
                else code = [];
                continue;
            }
            if (code is not null) { code.Add(raw); continue; }
            if (trimmed.Length == 0) continue;
            var indent = (raw.TakeWhile(character => character == ' ').Count()) / 2;
            if (trimmed.StartsWith('#'))
                result.Add(new MarkdownBlock(MarkdownBlockKind.Heading, trimmed.TrimStart('#').Trim()));
            else if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal)
                     || trimmed.StartsWith("• ", StringComparison.Ordinal))
                result.Add(new MarkdownBlock(MarkdownBlockKind.Bullet, trimmed[2..], indent));
            else if (OrderedBullet(trimmed) is { } ordered)
                result.Add(new MarkdownBlock(MarkdownBlockKind.Bullet, ordered, indent));
            else
                result.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, trimmed));
        }
        // An unterminated fence while streaming still shows what arrived.
        if (code is { Count: > 0 }) result.Add(new MarkdownBlock(MarkdownBlockKind.Code, string.Join("\n", code)));
        return result;
    }

    private static string? OrderedBullet(string line)
    {
        var dot = line.IndexOfAny(['.', ')', '、']);
        if (dot is < 1 or > 2 || dot + 1 >= line.Length || line[dot + 1] != ' ') return null;
        for (var index = 0; index < dot; index++)
            if (!char.IsDigit(line[index])) return null;
        return line[..(dot + 1)] + " " + line[(dot + 2)..];
    }

    public static IReadOnlyList<MarkdownInlineRun> Inline(string? line)
    {
        var runs = new List<MarkdownInlineRun>();
        if (string.IsNullOrEmpty(line)) return runs;
        var text = new StringBuilder();
        void Flush()
        {
            if (text.Length == 0) return;
            runs.Add(new MarkdownInlineRun(text.ToString()));
            text.Clear();
        }
        for (var index = 0; index < line.Length;)
        {
            if (line[index] == '`')
            {
                var end = line.IndexOf('`', index + 1);
                if (end < 0) { text.Append(line[index++]); continue; }
                Flush();
                runs.Add(new MarkdownInlineRun(line[(index + 1)..end], MarkdownInlineKind.Code));
                index = end + 1;
                continue;
            }
            if (line.AsSpan(index).StartsWith("**"))
            {
                var end = line.IndexOf("**", index + 2, StringComparison.Ordinal);
                if (end < 0) { text.Append("**"); index += 2; continue; }
                Flush();
                runs.Add(new MarkdownInlineRun(line[(index + 2)..end], MarkdownInlineKind.Bold));
                index = end + 2;
                continue;
            }
            if (line[index] == '*')
            {
                var end = line.IndexOf('*', index + 1);
                if (end < 0) { text.Append('*'); index++; continue; }
                Flush();
                runs.Add(new MarkdownInlineRun(line[(index + 1)..end], MarkdownInlineKind.Italic));
                index = end + 1;
                continue;
            }
            text.Append(line[index++]);
        }
        Flush();
        return runs;
    }
}
