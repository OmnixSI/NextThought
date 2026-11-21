using System.Text;

namespace NextThought.Core;

public enum WebSearchMode
{
    Off,
    Auto,
    Always
}

public static class WebSearchModes
{
    public static string Title(this WebSearchMode mode) => mode switch
    {
        WebSearchMode.Off => "关闭",
        WebSearchMode.Always => "每次都查",
        _ => "自动判断"
    };

    /// <summary>E4/Mac UserDefaults "webSearchMode": off / auto / always (unknown falls back to auto).</summary>
    public static WebSearchMode Parse(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "off" => WebSearchMode.Off,
        "always" => WebSearchMode.Always,
        _ => WebSearchMode.Auto
    };

    public static string Value(this WebSearchMode mode) => mode switch
    {
        WebSearchMode.Off => "off",
        WebSearchMode.Always => "always",
        _ => "auto"
    };

    /// <summary>Mode first, then the local rules; a configured provider (allowed) is required.</summary>
    public static bool ShouldSearch(this WebSearchMode mode, bool allowed, bool rulesNeedSearch) =>
        allowed && mode switch
        {
            WebSearchMode.Off => false,
            WebSearchMode.Always => true,
            _ => rulesNeedSearch
        };
}

/// <summary>0…1; <c>NeedsSearch</c> at ≥ 0.6, <c>Uncertain</c> at 0.35–0.6.</summary>
public sealed record WebSearchAssessment(double Score, string Reason)
{
    public bool NeedsSearch => Score >= 0.6;

    public bool Uncertain => Score >= 0.35 && Score < 0.6;
}

/// <summary>
/// Does answering this question need information newer than the model
/// (port of WebSearchNeed.swift)? Time words, fast-changing topics and recent years raise the
/// score; questions about the listener and concept questions keep it offline.
/// </summary>
public static class WebSearchNeed
{
    private static readonly string[] TimeWords =
    [
        "今天", "今日", "昨天", "明天", "刚刚", "刚才发布", "最近", "最新", "近期", "近来", "本周", "这周", "上周", "这个月", "本月", "上个月",
        "今年", "实时", "目前", "当前", "现在", "如今",
        "latest", "recent", "recently", "today", "yesterday", "tomorrow", "this week", "last week", "this month", "this year",
        "right now", "currently", "current", "nowadays", "as of"
    ];

    private static readonly HashSet<string> WeakTimeWords = new(StringComparer.Ordinal)
    {
        "目前", "当前", "现在", "如今", "current", "currently", "nowadays"
    };

    private static readonly string[] FreshTopics =
    [
        "股价", "股票", "市值", "汇率", "价格", "多少钱", "报价", "油价", "金价", "房价", "利率", "币价", "比特币",
        "天气", "气温", "新闻", "热搜", "比分", "赛果", "战绩", "排名", "排行", "票房",
        "发布会", "发布了", "新版本", "最新版", "版本号", "上市", "财报", "营收", "融资", "估值", "收购", "裁员",
        "选举", "大选", "总统", "首相", "现任", "ceo", "董事长", "政策", "法规", "疫情", "颁给", "得主", "冠军", "获奖",
        "stock price", "share price", "exchange rate", "price of", "weather", "news", "score", "release", "released", "launch",
        "earnings", "revenue", "funding", "valuation", "acquisition", "layoff", "election", "president", "ceo of", "version", "winner", "won"
    ];

    private static readonly HashSet<string> StrongTopics = new(StringComparer.Ordinal)
    {
        "股价", "汇率", "油价", "金价", "币价", "天气", "气温", "新闻", "热搜", "比分", "赛果", "票房", "现任", "最新版",
        "stock price", "share price", "exchange rate", "weather", "news", "ceo", "president", "version", "winner", "won"
    };

    private static readonly string[] ConceptWords =
    [
        "是什么", "什么是", "原理", "区别", "怎么实现", "如何实现", "为什么", "优缺点", "介绍一下", "解释一下",
        "what is", "difference between", "how does", "how do you implement", "explain", "why"
    ];

    private static readonly string[] SelfWords = ["你", "您", "你们", "you", "your"];

    private static readonly string[] Products =
        ["claude", "chatgpt", "gpt", "gemini", "deepseek", "qwen", "llama", "pytorch", "cuda", "ios", "android", "swift"];

    private static readonly string[] Capabilities =
    [
        "上下文", "支持多长", "支持哪些", "支持什么", "多少钱", "费用", "额度", "版本",
        "context window", "context length", "support", "pricing", "cost", "limit", "version"
    ];

    private static readonly string[] VolatileSubjects =
    [
        "签证", "航班", "截止日期", "最低工资", "奖学金金额", "商用许可", "免费 api", "在售",
        "visa", "deadline", "vulnerability", "chairs the", "free storage", "coding benchmark", "tax-free", "兑人民币"
    ];

    /// <summary>Labeled questions used by the unit tests and the in-app rules-vs-model comparison.</summary>
    public static readonly IReadOnlyList<(string Question, bool NeedsSearch)> EvalSamples =
    [
        ("现在比特币多少钱", true),
        ("最近 OpenAI 发布了什么新模型", true),
        ("今天北京天气怎么样", true),
        ("英伟达最新一季的财报营收是多少", true),
        ("苹果现任 CEO 是谁", true),
        ("2026 年诺贝尔物理学奖颁给了谁", true),
        ("What's the latest version of Swift?", true),
        ("What is the current exchange rate of USD to CNY", true),
        ("这周 A 股的行情怎么样，股价跌了吗", true),
        ("你最近在做什么项目", false),
        ("介绍一下你目前负责的工作", false),
        ("Redis 和 Memcached 有什么区别", false),
        ("数据库和缓存怎么保证一致性", false),
        ("为什么选择 Kafka 而不是 RabbitMQ", false),
        ("讲一下 TCP 三次握手的原理", false),
        ("Can you walk me through your current project?", false),
        ("目前我们团队用的是微服务架构，你怎么看", false)
    ];

    /// <summary>
    /// A Latin word counts only when it stands on its own: "launches"/"released" are the word,
    /// "wondering" is another. Chinese has no word breaks, so those are found anywhere.
    /// </summary>
    public static bool Mentions(string word, string text)
    {
        if (word.Any(character => character > 127)) return text.Contains(word, StringComparison.Ordinal);
        var from = 0;
        while (from <= text.Length - word.Length)
        {
            var index = text.IndexOf(word, from, StringComparison.Ordinal);
            if (index < 0) return false;
            var before = index == 0 ? (char?)null : text[index - 1];
            var end = index + word.Length;
            var builder = new StringBuilder();
            while (end < text.Length && IsAsciiLetterOrDigit(text[end])) builder.Append(text[end++]);
            var ending = builder.ToString();
            if (!IsAsciiLetterOrDigit(before) && ending is "" or "s" or "es" or "d" or "ed") return true;
            from = index + word.Length;
        }
        return false;
    }

    public static WebSearchAssessment Assess(string question, DateTimeOffset? now = null)
    {
        var text = question.ToLowerInvariant();
        var times = TimeWords.Where(word => Mentions(word, text)).ToList();
        var topics = FreshTopics.Where(word => Mentions(word, text)).ToList();
        var strongTime = times.Any(word => !WeakTimeWords.Contains(word));
        var year = (now ?? DateTimeOffset.Now).Year;
        var thisYear = text.Contains(year.ToString(), StringComparison.Ordinal);
        var lastYear = text.Contains((year - 1).ToString(), StringComparison.Ordinal);

        if (Products.Any(word => text.Contains(word, StringComparison.Ordinal)) && Capabilities.Any(word => text.Contains(word, StringComparison.Ordinal))
            && !ConceptWords.Any(word => text.Contains(word, StringComparison.Ordinal) && !text.Contains("context", StringComparison.Ordinal)))
            return new WebSearchAssessment(0.8, "产品当前能力或规格需要核查");
        if (new[] { "sota", "state of the art", "研究进展", "主流大模型" }.Any(word => text.Contains(word, StringComparison.Ordinal)))
            return new WebSearchAssessment(0.8, "研究或产品现状需要核查");
        if (VolatileSubjects.Any(word => text.Contains(word, StringComparison.Ordinal))) return new WebSearchAssessment(0.8, "外部条件可能变化，需要核查");
        if (Products.Any(word => text.Contains(word, StringComparison.Ordinal)) && new[] { "处理图像", "process images", "开源模型" }.Any(word => text.Contains(word, StringComparison.Ordinal)))
            return new WebSearchAssessment(0.8, "产品能力需要核查");

        var score = 0.0;
        var reasons = new List<string>();
        if (strongTime)
        {
            score += 0.7;
            reasons.Add($"时间词“{times.First(word => !WeakTimeWords.Contains(word))}”");
        }
        else if (times.Count > 0)
        {
            score += 0.2;
            reasons.Add($"时间词“{times[0]}”");
        }
        if (topics.FirstOrDefault(StrongTopics.Contains) is { } strong)
        {
            score += 0.65;
            reasons.Add($"时效话题“{strong}”");
        }
        else if (topics.Count > 0)
        {
            score += 0.45;
            reasons.Add($"时效话题“{topics[0]}”");
        }
        if (thisYear)
        {
            score += 0.45;
            reasons.Add("今年");
        }
        else if (lastYear)
        {
            score += 0.3;
            reasons.Add("去年");
        }

        // “你最近在做什么项目” is about the speaker, not the news.
        if (topics.Count == 0 && IsAboutListener(text, times))
        {
            score *= 0.3;
            reasons.Add("问的是你本人");
        }
        // Concept questions without fresh topics need no search.
        if (topics.Count == 0 && !strongTime && ConceptWords.Any(word => text.Contains(word, StringComparison.Ordinal))) score *= 0.5;
        score = Math.Min(1, score);
        return new WebSearchAssessment(score, reasons.Count == 0 ? "无时效线索" : string.Join("、", reasons));
    }

    /// <summary>A second-person word right before the time word: “你最近…”, “your current…”.</summary>
    public static bool IsAboutListener(string text, IReadOnlyList<string> times)
    {
        foreach (var time in times)
        {
            var index = text.IndexOf(time, StringComparison.Ordinal);
            if (index < 0) continue;
            var before = text[Math.Max(0, index - 6)..index];
            if (SelfWords.Any(word => before.Contains(word, StringComparison.Ordinal))) return true;
        }
        return false;
    }

    private static bool IsAsciiLetterOrDigit(char? character) =>
        character is { } value && value < 128 && (char.IsLetter(value) || char.IsNumber(value));
}
