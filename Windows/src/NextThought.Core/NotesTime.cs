using System.Globalization;
using System.Text;

namespace NextThought.Core;

/// <summary>
/// Meeting-relative times as the notes write them. Ported from <c>NotesTime</c> in
/// NotesTemplates.swift; the rest of NotesTemplates is ported separately.
/// </summary>
public static class NotesTime
{
    /// <summary>0 → "00:00", 3725 → "1:02:05".</summary>
    public static string Text(int seconds)
    {
        var value = Math.Max(0, seconds);
        var hours = value / 3600;
        var minutes = value / 60 % 60;
        var rest = value % 60;
        return value >= 3600
            ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, rest)
            : string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minutes, rest);
    }

    /// <summary>"12:41", "[12:41]", "1:02:05" → seconds; nil when it is not a time.</summary>
    public static int? Seconds(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var value = text.Trim().Replace('：', ':').Trim('[', ']', '【', '】', '(', ')', ' ');
        var parts = value.Split(':');
        if (parts.Length is < 2 or > 3) return null;
        var numbers = new int[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[index]))
                return null;
        }
        for (var index = 1; index < numbers.Length; index++)
            if (numbers[index] >= 60) return null;
        return parts.Length == 3
            ? numbers[0] * 3600 + numbers[1] * 60 + numbers[2]
            : numbers[0] * 60 + numbers[1];
    }
}

/// <summary>The structured view as Markdown, for copying and for anything that cannot draw it.</summary>
public static class StructuredNotesMarkdown
{
    public static string Render(StructuredNotesPayload payload)
    {
        if (!payload.HasContent) return "";
        var english = payload.IsEnglish;
        var lines = new List<string>();
        switch (payload.Format)
        {
            case NotesFormat.Standard:
                return "";
            case NotesFormat.Fishbone:
                if (payload.Fishbone is not { } fishbone) return "";
                lines.Add(Heading(english ? "Cause-and-effect fishbone" : "因果鱼骨图", fishbone.Head, english));
                lines.Add("");
                foreach (var category in fishbone.Categories)
                {
                    lines.Add("### " + category.Name);
                    foreach (var cause in category.Causes) lines.Add("- " + Line(cause.Text, cause.At));
                    lines.Add("");
                }
                break;
            case NotesFormat.Outline:
                if (payload.Outline is not { } outline) return "";
                lines.Add("## " + (english ? "Outline" : "要点大纲"));
                lines.Add("");
                for (var index = 0; index < outline.Sections.Count; index++)
                {
                    var section = outline.Sections[index];
                    lines.Add($"### {index + 1}. " + Line(section.Title, section.At));
                    foreach (var point in section.Points)
                    {
                        lines.Add("- " + Line(point.Text, point.At));
                        foreach (var child in point.Children) lines.Add("  - " + Line(child.Text, child.At));
                    }
                    lines.Add("");
                }
                if (outline.Concepts.Count > 0)
                {
                    lines.Add("### " + (english ? "Key concepts" : "关键概念"));
                    foreach (var concept in outline.Concepts)
                        lines.Add("- **" + concept.Term + "**" + (english ? ": " : "：") + Line(concept.Meaning, concept.At));
                    lines.Add("");
                }
                if (outline.Review.Count > 0)
                {
                    lines.Add("### " + (english ? "To review" : "待复习"));
                    foreach (var item in outline.Review) lines.Add("- " + Line(item.Text, item.At));
                    lines.Add("");
                }
                break;
            case NotesFormat.Viewpoints:
                if (payload.Viewpoints is not { } matrix) return "";
                lines.Add(Heading(english ? "Viewpoints" : "观点对照", matrix.Topic, english));
                lines.Add("");
                foreach (var issue in matrix.Issues)
                {
                    lines.Add("### " + issue.Title);
                    foreach (var party in matrix.Parties)
                    {
                        foreach (var position in matrix.PositionsOf(issue, party))
                            lines.Add("- **" + party + "**" + (english ? ": " : "：") + Line(position.Text, position.At));
                    }
                    lines.Add("");
                }
                if (matrix.Consensus.Count > 0)
                {
                    lines.Add("### " + (english ? "Agreed" : "共识"));
                    foreach (var item in matrix.Consensus) lines.Add("- " + Line(item.Text, item.At));
                    lines.Add("");
                }
                if (matrix.Disagreements.Count > 0)
                {
                    lines.Add("### " + (english ? "Still disagree" : "分歧"));
                    foreach (var item in matrix.Disagreements) lines.Add("- " + Line(item.Text, item.At));
                    lines.Add("");
                }
                if (matrix.Owners.Count > 0)
                {
                    lines.Add("### " + (english ? "Who does what" : "分工"));
                    foreach (var owner in matrix.Owners)
                    {
                        var parts = new List<string>();
                        if (owner.Who.Length > 0) parts.Add(owner.Who);
                        parts.Add(owner.Task);
                        if (owner.Due is { Length: > 0 } due) parts.Add(due);
                        lines.Add("- " + Line(string.Join(" · ", parts), owner.At));
                    }
                    lines.Add("");
                }
                break;
        }
        return string.Join('\n', lines);
    }

    private static string Heading(string name, string topic, bool english) =>
        topic.Length == 0 ? "## " + name : "## " + name + (english ? ": " : "：") + topic;

    private static string Line(string text, int? at) => at is { } seconds ? text + " [" + NotesTime.Text(seconds) + "]" : text;
}
