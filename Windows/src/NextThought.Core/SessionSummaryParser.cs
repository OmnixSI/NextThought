using System.Text.RegularExpressions;

namespace NextThought.Core;

public sealed record SummaryQuestion
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");
    public string Text { get; init; } = "";
    public string? AnswerGist { get; init; }
}

public sealed record BankEntry
{
    public string Question { get; init; } = "";
    public string Answer { get; init; } = "";
}

/// <summary>
/// Extracts the title, question list, lessons and improved answers from the model's summary
/// Markdown (port of SessionSummaryParser).
/// </summary>
public static class SessionSummaryParser
{
    public sealed record Parsed
    {
        public string? Title { get; set; }
        public List<SummaryQuestion> Questions { get; } = [];
        public List<string> Lessons { get; } = [];
        public List<BankEntry> Bank { get; } = [];
    }

    public static (string? Title, IReadOnlyList<SummaryQuestion> Questions) Parse(string markdown)
    {
        var parsed = ParseAll(markdown);
        return (parsed.Title, parsed.Questions);
    }

    public static Parsed ParseAll(string markdown)
    {
        var parsed = new Parsed();
        var section = "";
        string? pendingQuestion = null;
        var pendingAnswer = new List<string>();

        void FlushBank()
        {
            if (pendingQuestion is { Length: > 0 } && pendingAnswer.Count > 0)
                parsed.Bank.Add(new BankEntry { Question = pendingQuestion, Answer = string.Join("\n", pendingAnswer) });
            pendingQuestion = null;
            pendingAnswer.Clear();
        }

        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('#'))
            {
                FlushBank();
                section = line.TrimStart('#').Trim();
                continue;
            }
            if (line.Length == 0) continue;

            if (section.StartsWith("标题", StringComparison.Ordinal) && parsed.Title is null)
            {
                var title = line.Trim('*', '_', '`', ' ');
                parsed.Title = title.Length > 60 ? title[..60] : title;
            }
            else if (section.StartsWith("问题", StringComparison.Ordinal))
            {
                if (StripListMarker(line) is not { } body) continue;
                var parts = body.Split("我的回答要点");
                var text = parts[0].Trim();
                foreach (var suffix in new[] { "——", "—", "--", "-", "｜", "|" })
                {
                    while (text.EndsWith(suffix, StringComparison.Ordinal))
                        text = text[..^suffix.Length].Trim();
                }
                var gist = parts.Length > 1 ? parts[1].Trim('：', ':', ' ') : null;
                if (text.Length > 0)
                    parsed.Questions.Add(new SummaryQuestion { Text = text, AnswerGist = gist is { Length: > 0 } ? gist : null });
            }
            else if (section.StartsWith("经验", StringComparison.Ordinal))
            {
                var body = StripListMarker(line) ?? (line.StartsWith('·') ? line[1..] : null);
                if (body is not null)
                {
                    var text = body.Trim();
                    if (text.Length > 0) parsed.Lessons.Add(text);
                }
            }
            else if (section.StartsWith("题库", StringComparison.Ordinal))
            {
                var body = (StripListMarker(line) ?? line).Trim();
                if (StripPrefix(body, ["问：", "问:", "Q：", "Q:", "**问**：", "**问：**"]) is { } question)
                {
                    FlushBank();
                    pendingQuestion = question;
                }
                else if (StripPrefix(body, ["答：", "答:", "A：", "A:", "**答**：", "**答：**"]) is { } answer)
                {
                    if (pendingQuestion is not null) pendingAnswer.Add(answer);
                }
                else if (pendingQuestion is not null && pendingAnswer.Count > 0)
                {
                    pendingAnswer.Add(body);
                }
            }
        }
        FlushBank();
        return parsed;
    }

    private static string? StripPrefix(string text, IReadOnlyList<string> prefixes)
    {
        foreach (var prefix in prefixes)
            if (text.StartsWith(prefix, StringComparison.Ordinal))
                return text[prefix.Length..].Trim();
        return null;
    }

    private static readonly Regex NumberedMarker = new(@"^\d{1,3}[.)、]\s*", RegexOptions.Compiled);

    private static string? StripListMarker(string line)
    {
        var match = NumberedMarker.Match(line);
        if (match.Success) return line[match.Length..];
        foreach (var marker in new[] { "- ", "* ", "• " })
            if (line.StartsWith(marker, StringComparison.Ordinal)) return line[marker.Length..];
        return null;
    }
}
