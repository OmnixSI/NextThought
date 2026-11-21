using System.Text;

namespace NextThought.Core;

/// <summary>
/// Turns recognised speech into a question, merging short fragments and suppressing exact
/// repeats for 15 seconds (port of QuestionDetector.swift).
/// </summary>
public sealed class QuestionDetector
{
    private static readonly char[] PunctuationChars = Enumerable.Range(0, 0x10000).Select(value => (char)value).Where(char.IsPunctuation).ToArray();
    private static readonly string[] EnglishStarts =
    [
        "what ", "what's", "why ", "how ", "when ", "where ", "who ", "which ",
        "can you ", "could you ", "would you ", "tell me ", "explain ",
        "describe ", "compare ", "please explain ", "walk me through ",
        "help me understand ", "difference between ", "do you ", "did you ", "have you ",
        "is there ", "are there ", "could we ", "can we "
    ];

    private static readonly string[] ChineseMarkers =
    [
        "你觉得", "为什么", "怎么", "如何", "能不能", "可不可以", "请解释", "解释一下",
        "请说明", "说明一下", "分析一下", "比较一下", "讲讲", "谈谈", "什么是", "有什么",
        "哪些", "是否", "设计思路是什么", "怎么样", "介绍一下", "举个例子", "举一个例子", "能否", "对吗", "是不是", "有什么区别"
    ];

    private static readonly string[] FollowUps =
    [
        "为什么", "怎么", "然后呢", "那为什么", "那怎么", "why", "how", "and why", "what about that",
        "然后呢", "所以呢", "举例呢", "why not", "and then"
    ];

    private readonly List<(string Key, DateTimeOffset Date)> recentKeys = [];
    private string pendingFragment = "";
    private DateTimeOffset? fragmentDate;
    private const int DuplicateWindowSeconds = 15;
    private const int FragmentWindowSeconds = 8;

    /// <summary>The question to answer, or null while the sentence is incomplete or a repeat.</summary>
    public string? Detect(string text, DateTimeOffset? now = null)
    {
        var current = now ?? DateTimeOffset.UtcNow;
        var duplicateCutoff = current.AddSeconds(-DuplicateWindowSeconds);
        recentKeys.RemoveAll(entry => entry.Date <= duplicateCutoff);
        if (fragmentDate is { } stamped && (current - stamped).TotalSeconds > FragmentWindowSeconds)
        {
            pendingFragment = "";
            fragmentDate = null;
        }
        var incoming = (text.Length > 2000 ? text[^2000..] : text).Trim();
        if (incoming.Length == 0) return null;

        var question = incoming;
        if (pendingFragment.Length > 0)
        {
            question = pendingFragment + " " + incoming;
            pendingFragment = "";
            fragmentDate = null;
        }

        if (!IsShortFollowUp(question) && IsIncomplete(question))
        {
            pendingFragment = question.Length > 2000 ? question[^2000..] : question;
            fragmentDate = current;
            return null;
        }
        if (!IsShortFollowUp(question) && !LooksLikeQuestion(question)) return null;
        if (question.Length < 4 && !IsShortFollowUp(question)) return null;

        var key = Normalize(question);
        if (key.Length == 0) return null;
        if (recentKeys.Any(entry => entry.Key == key)) return null;
        recentKeys.Add((key, current));
        if (recentKeys.Count > 30) recentKeys.RemoveRange(0, recentKeys.Count - 30);
        return question;
    }

    public static bool LooksLikeQuestion(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        if (lower.Contains('?') || lower.Contains('？')) return true;
        if (EnglishStarts.Any(start => lower.StartsWith(start, StringComparison.Ordinal))) return true;
        return ChineseMarkers.Any(marker => lower.Contains(marker, StringComparison.Ordinal));
    }

    public static bool IsShortFollowUp(string text)
    {
        var lower = text.Trim().Trim(PunctuationChars).ToLowerInvariant();
        return FollowUps.Contains(lower);
    }

    public static bool IsIncomplete(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        if (lower.Contains('?') || lower.Contains('？')) return false;
        var count = CountWords(lower);
        if (lower.StartsWith("can you", StringComparison.Ordinal) || lower.StartsWith("could you", StringComparison.Ordinal)
            || lower.StartsWith("would you", StringComparison.Ordinal))
            return count <= 4;
        if (new[] { "what ", "why ", "how ", "when ", "where ", "who ", "which " }.Any(prefix => lower.StartsWith(prefix, StringComparison.Ordinal)))
            return count <= 3;
        if (lower.StartsWith("please explain", StringComparison.Ordinal) || lower.StartsWith("explain", StringComparison.Ordinal)
            || lower.StartsWith("请解释", StringComparison.Ordinal) || lower.StartsWith("解释一下", StringComparison.Ordinal))
        {
            var cjk = lower.Count(IsCjk);
            return cjk > 0 ? cjk <= 7 : count <= 2;
        }
        return false;
    }

    public static string Normalize(string text)
    {
        var tokens = new List<string>();
        foreach (var part in SplitWords(text.ToLowerInvariant()))
        {
            if (part.Any(IsCjk))
            {
                if (part.Length == 1) tokens.Add(part);
                else for (var index = 0; index < part.Length - 1; index++) tokens.Add(part.Substring(index, 2));
            }
            else
            {
                tokens.Add(part);
            }
        }
        return string.Join(" ", tokens);
    }

    private static int CountWords(string text) => SplitWords(text).Count;

    private static List<string> SplitWords(string text)
    {
        var words = new List<string>();
        var builder = new StringBuilder();
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character)) builder.Append(character);
            else if (builder.Length > 0) { words.Add(builder.ToString()); builder.Clear(); }
        }
        if (builder.Length > 0) words.Add(builder.ToString());
        return words;
    }

    private static bool IsCjk(char character) => character is >= '\u4E00' and <= '\u9FFF';
}
