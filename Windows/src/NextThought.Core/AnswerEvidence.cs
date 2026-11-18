using System.Text.Json;

namespace NextThought.Core;

/// <summary>
/// The model's "should this be searched" answer (port of SearchDecision in AnswerEvidence.swift).
/// The reply is not guaranteed to be clean JSON: take the outermost object.
/// </summary>
public sealed record SearchDecision(bool Search, string Reason, string Query, string Mechanism)
{
    public static SearchDecision? Parse(string output, string question)
    {
        var cleaned = output.Trim();
        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        try
        {
            using var document = JsonDocument.Parse(cleaned[start..(end + 1)]);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("needsSearch", out var search) || search.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return null;
            if (!root.TryGetProperty("reason", out var reason) || reason.ValueKind != JsonValueKind.String) return null;
            var query = root.TryGetProperty("query", out var candidate) && candidate.ValueKind == JsonValueKind.String
                ? (candidate.GetString() ?? "").Trim()
                : question;
            if (query.Length == 0) query = question;
            var text = reason.GetString() ?? "";
            if (text.Length > 200) text = text[..200];
            if (query.Length > 500) query = query[..500];
            return new SearchDecision(search.GetBoolean(), text, query, "model");
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Rewrite/search detection before answering (port of AnswerGrounding in AnswerEvidence.swift).</summary>
public static class AnswerGrounding
{
    private static readonly string[] RewritePrefixes = ["把上一条回答压缩", "Rewrite the previous answer", "把上一条回答改写为"];

    private static readonly string[] VerificationHints =
    [
        "可能不是最新", "可能已过时", "需要联网核实", "需要实时核查", "无法确认最新",
        "may be outdated", "need to verify the current"
    ];

    private static readonly string[] SearchPhrases =
    [
        "联网查", "上网查", "搜索一下", "查一下最新",
        "search the web", "look it up", "browse the web", "verify online"
    ];

    public static bool PureRewrite(string question) =>
        RewritePrefixes.Any(prefix => question.StartsWith(prefix, StringComparison.Ordinal));

    public static bool NeedsVerification(string answer)
    {
        var lower = answer.ToLowerInvariant();
        return VerificationHints.Any(hint => lower.Contains(hint.ToLowerInvariant(), StringComparison.Ordinal));
    }

    public static bool ExplicitSearch(string question)
    {
        var lower = question.ToLowerInvariant();
        return SearchPhrases.Any(phrase => lower.Contains(phrase.ToLowerInvariant(), StringComparison.Ordinal));
    }
}
