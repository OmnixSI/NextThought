import Foundation

public enum WebSearchMode: String, CaseIterable, Identifiable, Sendable {
    case off, auto, always
    public var id: String { rawValue }
    public var title: String {
        switch self {
        case .off: "关闭"
        case .auto: "自动判断"
        case .always: "每次都查"
        }
    }
}

public struct WebSearchAssessment: Sendable, Equatable {
    /// 0…1; `needsSearch` at ≥ 0.6. The decision model, when on, decides instead.
    public var score: Double
    public var reason: String
    public var needsSearch: Bool { score >= 0.6 }
    public var uncertain: Bool { score >= 0.35 && score < 0.6 }
}

/// Does answering this question need information newer than the model's training?
/// Cheap word rules: time words (今天 / 最新 / latest…), fast-changing topics (价格 /
/// 新闻 / 版本 / 现任…) and recent years. Questions about the speaker themselves
/// (“你最近在做什么”) and concept questions stay offline.
public enum WebSearchNeed {
    static let timeWords = ["今天", "今日", "昨天", "明天", "刚刚", "刚才发布", "最近", "最新", "近期", "近来", "本周", "这周", "上周", "这个月", "本月", "上个月", "今年", "实时", "目前", "当前", "现在", "如今",
                            "latest", "recent", "recently", "today", "yesterday", "tomorrow", "this week", "last week", "this month", "this year", "right now", "currently", "current", "nowadays", "as of"]
    /// Time words that alone say little (“目前我们用 Redis”).
    static let weakTimeWords: Set<String> = ["目前", "当前", "现在", "如今", "current", "currently", "nowadays"]
    static let freshTopics = ["股价", "股票", "市值", "汇率", "价格", "多少钱", "报价", "油价", "金价", "房价", "利率", "币价", "比特币",
                              "天气", "气温", "新闻", "热搜", "比分", "赛果", "战绩", "排名", "排行", "票房",
                              "发布会", "发布了", "新版本", "最新版", "版本号", "上市", "财报", "营收", "融资", "估值", "收购", "裁员",
                              "选举", "大选", "总统", "首相", "现任", "ceo", "董事长", "政策", "法规", "疫情", "颁给", "得主", "冠军", "获奖",
                              "stock price", "share price", "exchange rate", "price of", "weather", "news", "score", "release", "released", "launch",
                              "earnings", "revenue", "funding", "valuation", "acquisition", "layoff", "election", "president", "ceo of", "version", "winner", "won"]
    /// Topics that are time-bound on their own (“现任 CEO”, “汇率”).
    static let strongTopics: Set<String> = ["股价", "汇率", "油价", "金价", "币价", "天气", "气温", "新闻", "热搜", "比分", "赛果", "票房", "现任", "最新版",
                                            "stock price", "share price", "exchange rate", "weather", "news"]
    /// Concept / how-to questions: knowledge, not news.
    static let conceptWords = ["是什么", "什么是", "原理", "区别", "怎么实现", "如何实现", "为什么", "优缺点", "介绍一下", "解释一下",
                               "what is", "difference between", "how does", "how do you implement", "explain", "why"]
    static let selfWords = ["你", "您", "你们", "you", "your"]

    /// Labeled questions for the unit tests and the in-app rules-vs-model comparison.
    public static let evalSamples: [(question: String, needsSearch: Bool)] = [
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
        ("目前我们团队用的是微服务架构，你怎么看", false),
    ]

    public static func assess(_ question: String, now: Date = Date()) -> WebSearchAssessment {
        let text = question.lowercased()
        let times = timeWords.filter { text.contains($0) }
        let topics = freshTopics.filter { text.contains($0) }
        let strongTime = times.contains { !weakTimeWords.contains($0) }
        let year = Calendar(identifier: .gregorian).component(.year, from: now)
        let thisYear = text.contains(String(year))
        let lastYear = text.contains(String(year - 1))

        var score = 0.0
        var reasons: [String] = []
        if strongTime { score += 0.45; reasons.append("时间词“\(times.first { !weakTimeWords.contains($0) } ?? "")”") }
        else if !times.isEmpty { score += 0.2; reasons.append("时间词“\(times[0])”") }
        if let strong = topics.first(where: { strongTopics.contains($0) }) { score += 0.65; reasons.append("时效话题“\(strong)”") }
        else if !topics.isEmpty { score += 0.45; reasons.append("时效话题“\(topics[0])”") }
        if thisYear { score += 0.45; reasons.append("今年") } else if lastYear { score += 0.3; reasons.append("去年") }

        // “你最近在做什么项目” is about the speaker, not the news.
        if topics.isEmpty, isAboutListener(text, times: times) { score *= 0.3; reasons.append("问的是你本人") }
        // Concept questions without fresh topics need no search.
        if topics.isEmpty, conceptWords.contains(where: { text.contains($0) }) { score *= 0.5 }
        score = min(1, score)
        return WebSearchAssessment(score: score, reason: reasons.isEmpty ? "无时效线索" : reasons.joined(separator: "、"))
    }

    /// A second-person word right before the time word: “你最近…”, “你们目前…”, “your current…”.
    static func isAboutListener(_ text: String, times: [String]) -> Bool {
        for time in times {
            guard let range = text.range(of: time) else { continue }
            let before = text[text.startIndex..<range.lowerBound].suffix(6)
            if selfWords.contains(where: { before.contains($0) }) { return true }
        }
        return false
    }
}
