using System.Text.RegularExpressions;

namespace NextThought.Core;

/// <summary>Picks a scenario from words in a calendar or typed title; general when nothing fits.</summary>
public static class MeetingTypeGuesser
{
    private static readonly (SessionMode Mode, string[] Words)[] Keywords =
    [
        (SessionMode.Interview, ["面试", "一面", "二面", "三面", "四面", "终面", "hr面", "interview", "onsite", "phone screen"]),
        (SessionMode.LabMeeting, ["组会", "group meeting", "lab meeting"]),
        (SessionMode.GroupProject, ["小组作业", "大作业", "课程设计", "课设", "团队作业", "group project", "group assignment", "team project", "course project", "capstone"]),
        (SessionMode.Client, ["客户", "client", "customer"]),
        (SessionMode.Academic, ["答辩", "defense", "defence", "汇报", "报告会", "presentation", "talk"]),
        (SessionMode.Lesson, ["讲座", "课程", "上课", "lecture", "class", "course"]),
        (SessionMode.Discussion, ["小组讨论", "讨论", "discussion", "brainstorm", "头脑风暴"]),
        (SessionMode.Meeting, ["例会", "周会", "站会", "小组会", "项目组会", "项目会议", "sync", "standup", "stand-up", "1:1", "one on one", "review"])
    ];

    public static SessionMode Guess(string title)
    {
        var text = title.ToLowerInvariant();
        if (text.Trim().Length == 0) return SessionMode.General;
        foreach (var (mode, words) in Keywords)
            if (words.Any(word => Matches(word, text))) return mode;
        return SessionMode.General;
    }

    private static bool Matches(string word, string text)
    {
        if (word == "组会")
        {
            var index = 0;
            while ((index = text.IndexOf(word, index, StringComparison.Ordinal)) >= 0)
            {
                var before = text[..index];
                if (!before.EndsWith('小') && !before.EndsWith("项目", StringComparison.Ordinal)) return true;
                index += word.Length;
            }
            return false;
        }
        if (word.All(character => character < 128 && (char.IsLetter(character) || character is ' ' or '-')))
        {
            // English words match whole words only ("class" is not in "classic").
            var pattern = "(?<![a-z0-9])" + Regex.Escape(word) + "(?:s|es)?(?![a-z0-9])";
            return Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant);
        }
        return text.Contains(word, StringComparison.Ordinal);
    }
}

/// <summary>
/// Finds the series a new meeting continues: "字节跳动 · 后端二面" belongs to "字节跳动 · 后端".
/// </summary>
public static class SeriesMatcher
{
    /// <summary>Round markers that differ between meetings of the same series.</summary>
    private static readonly Regex[] RoundPatterns =
    [
        new(@"第\s*[0-9一二三四五六七八九十]+\s*(轮|场|次|期|周|面)", RegexOptions.Compiled),
        new(@"[0-9一二三四五六七八九十]+\s*面", RegexOptions.Compiled),
        new(@"(终面|hr\s*面|复试|初试)", RegexOptions.Compiled),
        new(@"\bround\s*[0-9]+\b", RegexOptions.Compiled),
        new(@"\b(r|#)[0-9]+\b", RegexOptions.Compiled),
        new(@"\bweek\s*[0-9]+\b", RegexOptions.Compiled),
        new(@"第\s*[0-9一二三四五六七八九十]+\s*(讲|课|节|章|季度|季)", RegexOptions.Compiled),
        new(@"\bq[1-4]\b", RegexOptions.Compiled),
        new(@"\b(first|second|third|final)\s+round\b", RegexOptions.Compiled),
        new(@"(面试|interview)", RegexOptions.Compiled)
    ];

    /// <summary>Numbered units whose noun is part of the series name: "Lecture 4" → "lecture".</summary>
    private static readonly Regex[] NumberedPatterns =
    [
        new(@"\b(lecture|lec|session|sprint|seminar|workshop|class|part)\s*#?[0-9]+\b", RegexOptions.Compiled)
    ];

    /// <summary>The title without round markers ("一面", "round 2", "第 3 周", "Lecture 4", "Q3").</summary>
    public static string BaseTitle(string title)
    {
        var text = title.ToLowerInvariant();
        foreach (var pattern in NumberedPatterns) text = pattern.Replace(text, "$1");
        foreach (var pattern in RoundPatterns) text = pattern.Replace(text, " ");
        return text.Trim();
    }

    /// <summary>Similarity of two titles once round markers are gone (0…1).</summary>
    public static double Similarity(string title, string series)
    {
        var left = BaseTitle(title);
        var right = BaseTitle(series);
        static int Letters(string text) => text.Count(character => char.IsLetterOrDigit(character));
        if (Letters(left) < 2 || Letters(right) < 2) return 0;
        return KnowledgeMerger.Similarity(left, right);
    }

    public sealed record Candidate(Guid Id, string Name, SessionMode Mode, DateTimeOffset LastUsed);

    /// <summary>The best matching series, or null; same type wins ties, then the most recently used.</summary>
    public static Guid? BestMatch(string title, SessionMode? mode, IReadOnlyList<Candidate> candidates, double minimum = 0.5)
    {
        var scored = candidates.Select(candidate =>
        {
            var score = Similarity(title, candidate.Name);
            if (mode is { } current && current != SessionMode.General && candidate.Mode == current) score += 0.05;
            return (Candidate: candidate, Score: score);
        }).Where(pair => pair.Score >= minimum).ToList();
        if (scored.Count == 0) return null;
        return scored.OrderByDescending(pair => pair.Score)
            .ThenByDescending(pair => pair.Candidate.LastUsed)
            .First().Candidate.Id;
    }
}
