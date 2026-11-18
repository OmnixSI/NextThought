using System.Text;

namespace NextThought.Core;

/// <summary>One question detected during a meeting (port of the SessionQuestionEvent fields used here).</summary>
public sealed record SessionQuestionEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");
    public DateTimeOffset Date { get; init; } = DateTimeOffset.UtcNow;
    public string Text { get; init; } = "";
    public string? LeadIn { get; init; }
    public string Intent { get; init; } = "";
    public double Score { get; init; }
    public string? Disposition { get; init; }
}

/// <summary>
/// Renders a meeting as Markdown (port of SessionMarkdownExporter adapted to the Windows
/// projection and transcript models). Search and export share the same freshness decisions.
/// </summary>
public static class SessionMarkdownExporter
{
    public static string Export(
        string title,
        string modeTitle,
        DateTimeOffset startedAt,
        double durationSeconds,
        string audioSource,
        SessionTextProjection projection,
        IReadOnlyList<TranscriptLine> lines,
        IReadOnlyList<SessionQuestionEvent>? detectedQuestions = null)
    {
        var output = new List<string>
        {
            "# " + (projection.Summary is { Title.Length: > 0 } summary ? summary.Title : title),
            ""
        };
        var facts = new List<string> { modeTitle, startedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm") };
        if (durationSeconds > 0) facts.Add((int)(durationSeconds / 60) + " 分钟");
        if (audioSource.Length > 0) facts.Add(audioSource);
        output.Add("> " + string.Join(" · ", facts));
        output.Add("");

        switch (projection.DerivedContentStatus)
        {
            case SessionDerivedContentStatus.Stale:
                output.Add("> 提示：部分纪要内容来自较旧的转写版本；请核对当前原话后再转发。");
                output.Add("");
                break;
            case SessionDerivedContentStatus.Unknown:
                output.Add("> 提示：旧纪要未记录转写版本，无法确认它是否与当前原话一致。");
                output.Add("");
                break;
        }

        if (projection.Summary is { } projectedSummary && projectedSummary.Markdown.Length > 0)
        {
            // The model's own document, minus its title section (already the heading).
            var body = projectedSummary.Markdown;
            var range = body.IndexOf("## ", StringComparison.Ordinal);
            if (range >= 0) body = body[range..];
            output.Add(body.Trim());
            output.Add("");
        }
        else if (detectedQuestions is { Count: > 0 })
        {
            output.Add("## 检测到的问题");
            output.AddRange(detectedQuestions.Select((question, index) => (index + 1) + ". " + question.Text));
            output.Add("");
        }

        if (projection.Answers.Count > 0)
        {
            output.Add("## 会中回答");
            foreach (var answer in projection.Answers)
            {
                output.Add("");
                var question = answer.Question.Replace("\n", " ");
                output.Add("### " + (question.Length > 200 ? question[..200] : question));
                output.Add("");
                if (!answer.IsComplete) output.Add("> 未完成的回答片段，请核实后使用。\n");
                output.Add(answer.Answer);
            }
            output.Add("");
        }

        output.Add("## 完整转录");
        output.Add("");
        foreach (var line in lines)
        {
            var offset = line.StartedAt is { } started
                ? Math.Max(0, (int)(started - startedAt).TotalSeconds)
                : Math.Max(0, (int)(line.Date - startedAt).TotalSeconds);
            var timestamp = $"- `{offset / 60:00}:{offset % 60:00}` ";
            var speaker = "**" + SpeakerLabel(line.Speaker) + "**：";
            var original = timestamp + speaker + line.Text;
            var translation = line.Translation?.Text;
            if (translation is null || translation.Trim().Length == 0)
            {
                output.Add(original);
                continue;
            }
            output.Add(original);
            output.Add("  *译文：* " + translation);
        }
        return string.Join("\n", output) + "\n";
    }

    private static string SpeakerLabel(string speaker) => speaker switch
    {
        "me" => "我",
        "remote" => "对方",
        _ => speaker
    };
}
