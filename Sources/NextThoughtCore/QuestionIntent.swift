import Foundation

/// What an utterance is trying to do. Only `question`, `explanationRequest` and
/// `followUp` are actionable; `incomplete` waits for the next words.
public enum UtteranceIntent: String, Codable, Sendable, CaseIterable {
    case statement, incomplete, question, explanationRequest, followUp

    public var isActionable: Bool { self == .question || self == .explanationRequest || self == .followUp }
    public var title: String {
        switch self {
        case .statement: "陈述"
        case .incomplete: "未说完"
        case .question: "明确提问"
        case .explanationRequest: "请求解释"
        case .followUp: "追问"
        }
    }
}

public struct IntentAssessment: Sendable, Equatable {
    public var intent: UtteranceIntent
    /// 0...1. Heuristic evidence strength, not a calibrated probability.
    public var score: Double
    public var reasons: [String]
    public init(intent: UtteranceIntent, score: Double, reasons: [String] = []) {
        self.intent = intent; self.score = score; self.reasons = reasons
    }
}

/// First-layer, deterministic intent classifier for Chinese, English and mixed speech.
/// It scores evidence (question marks, interrogatives, sentence-final particles,
/// addressed requests, follow-up forms) and subtracts evidence for rhetorical
/// self-answers, fillers and first-person narration ("我来介绍一下").
public enum QuestionIntentClassifier {
    public struct Thresholds: Sendable, Equatable {
        public var actionable: Double
        public var uncertain: Double
        public static func forMode(_ mode: SessionMode) -> Thresholds {
            switch mode {
            // Lecturers ask many rhetorical questions; require stronger evidence.
            case .lesson: Thresholds(actionable: 0.6, uncertain: 0.35)
            case .interview: Thresholds(actionable: 0.45, uncertain: 0.25)
            // Audience / chair questions after a talk are usually explicit.
            case .academic: Thresholds(actionable: 0.5, uncertain: 0.3)
            case .meeting, .general, .client, .discussion, .groupProject, .custom: Thresholds(actionable: 0.5, uncertain: 0.3)
            // Supervisors ask real questions in group meetings, but people also
            // think aloud; slightly stricter than a work meeting.
            case .labMeeting: Thresholds(actionable: 0.55, uncertain: 0.3)
            }
        }
    }

    static let englishWh = ["what", "what's", "whats", "why", "how", "when", "where", "who", "whom", "whose", "which", "how's", "where's", "who's"]
    static let englishAux = ["is", "are", "was", "were", "do", "does", "did", "can", "could", "would", "will", "should", "shall", "have", "has", "had", "may", "might", "isn't", "aren't", "don't", "doesn't", "didn't", "can't", "won't", "wouldn't", "shouldn't", "haven't", "hasn't"]
    static let englishRequests = ["tell me", "tell us", "walk me through", "walk us through", "explain", "describe", "compare", "talk about", "talk a little bit about", "talk a bit about", "give me an example", "give us an example", "give an example", "elaborate", "help me understand", "help us understand", "share", "please explain", "can you explain", "could you explain", "say more about", "go over", "talk me through", "talk us through", "introduce yourself", "define", "i'd like to hear", "i'd like to know", "i'd like to understand", "i'd like to learn", "i'd love to hear", "i want to hear", "i want to know", "i'm curious", "i am curious", "i was wondering", "curious about", "any thoughts", "your thoughts", "thoughts on", "let's start with your", "let's talk about your", "let's hear", "would you mind", "what about your", "how about your"]
    static let englishSelfNarration = ["let me", "i'll", "i will", "let's", "we'll", "we will", "i'm going to", "i am going to", "today we", "now we", "next we", "so we", "i want to explain", "i'd like to start", "i'd like to introduce", "i'd like to share", "i'd like to show"]
    /// Audio and screen-sharing checks: questions in form, never something to answer.
    static let logistics = ["hear me", "see my screen", "see the screen", "my screen", "on mute", "you're muted", "can you see", "听到我", "听得到", "听得见", "听见我", "看到我的屏幕", "看得到我", "看得见", "声音", "网络", "卡了", "有点卡", "麦克风", "静音", "共享屏幕", "屏幕共享", "信号"]
    static let englishFillers: Set<String> = ["um", "uh", "erm", "hmm", "so", "okay", "ok", "alright", "well", "and", "right", "now", "yeah", "yes", "great", "good", "cool", "then", "also", "but", "anyway"]
    static let chineseInterjections: Set<String> = ["嗯嗯", "嗯", "啊", "呃", "额", "哦", "噢", "唔"]
    static let chineseFillers = ["嗯嗯", "嗯", "啊", "呃", "额", "哦", "噢", "唔", "那个", "就是说", "就是", "然后", "好的", "好", "行", "对对", "对", "ok", "okay", "嗯好"]
    static let chineseInterrogatives = ["什么", "为什么", "为啥", "怎么", "怎样", "如何", "哪个", "哪些", "哪里", "哪儿", "谁", "多少", "几个", "几种", "是否", "是不是", "有没有", "能不能", "会不会", "可不可以", "对不对", "行不行", "要不要", "好不好", "能否", "可否", "难道", "咋", "哪种", "哪一", "哪家", "哪位", "为何", "何时", "何种", "几点", "多久", "多长", "多大", "用没用过", "了没有", "过没有", "有何"]
    static let chineseFalseInterrogatives = ["没什么", "什么的", "不管怎么", "怎么说呢", "不知道为什么", "不知道怎么", "什么都", "怎么都", "为什么呢因为", "无论如何", "不论什么", "说什么也", "这么说", "那么说", "可以随时问", "有什么不懂", "有什么问题可以", "有什么问题随时", "也不知道", "不清楚为什么", "不确定"]
    static let chineseRequests = ["介绍一下", "介绍下", "自我介绍", "讲一下", "讲讲", "讲一讲", "讲下", "谈谈", "谈一下", "谈一谈", "说说", "说一下", "说一说", "说下", "聊聊", "聊一下", "聊一聊", "解释", "说明一下", "分析一下", "分析下", "比较一下", "对比一下", "描述一下", "描述一次", "描述一个", "举个例子", "举一个例子", "举例", "展开说", "展开讲", "详细说", "详细讲", "具体说", "具体讲", "讲解一下", "阐述", "总结一下", "回答一下", "给我讲", "跟我说", "怎么理解", "分享一下", "分享一个", "分享下", "请你", "麻烦你", "想了解", "想听听", "听听你", "好奇", "你来说", "你先说", "你来讲", "你先讲", "看一下这个", "帮忙看"]
    /// Request markers that only count when the listener is addressed (你 / 您 / 大家).
    static let chineseAddressedRequests = ["看法", "怎么看", "意见", "的理解", "想法", "进度", "经历", "经验"]
    static let chineseSelfNarration = ["我来", "我先", "让我", "我们来看", "下面我", "接下来我", "我给大家", "我想先", "我简单", "我们先", "我们接下来", "今天我们", "我跟大家", "我这边先", "我稍微"]
    static let rhetoricalAnswerStarts = ["因为", "答案是", "其实", "原因是", "这是因为", "很简单", "我们来看", "首先", "because", "the answer", "well,", "it's because", "that's because", "let's see"]
    static let shortFollowUps: Set<String> = ["为什么", "怎么", "然后呢", "那为什么", "那怎么", "所以呢", "举例呢", "比如呢", "还有呢", "具体呢", "怎么做", "为啥", "然后", "那然后呢", "还有吗", "还有别的吗", "比如说呢", "具体说说", "展开说说", "why", "how", "and why", "why not", "how so", "and then", "such as", "for example", "like what", "what about that", "what else", "anything else", "go on", "then what", "how come", "can you elaborate", "elaborate", "for instance", "like how"]
    static let danglingEnglish: Set<String> = ["the", "a", "an", "of", "and", "or", "to", "about", "between", "with", "for", "in", "on", "your", "my", "is", "are", "you", "if", "that", "which", "because", "so", "but", "than", "from", "like", "this", "these", "those", "into"]
    static let danglingChinese = ["的", "和", "或者", "还有", "就是", "那个", "这个", "然后", "如果", "因为", "以及", "跟", "与", "关于", "比如", "一个", "在", "对于", "把", "被", "但是", "而且", "所以"]

    /// Drops leading discourse fillers (“嗯 那个 就是…”, “um so okay…”) that the
    /// recognizer keeps verbatim, so structural cues at the start are seen.
    static func strippingFillers(_ lower: String) -> String {
        var text = trimmingLeading(lower)
        var changed = true
        while changed, !text.isEmpty {
            changed = false
            for filler in chineseFillers where text.hasPrefix(filler) {
                let rest = text.dropFirst(filler.count)
                guard let next = rest.first else { continue }
                // Interjections (嗯 啊 呃…) go regardless; word fillers (好 / 对 / 就是 / 那个)
                // only when the recognizer separated them, so “对于…”, “好不好”, “就是说明…”
                // and “那个方案…” stay intact.
                let separated = next.isWhitespace || next.isPunctuation
                if !chineseInterjections.contains(filler), !separated { continue }
                if next == "不" || next == "吗" || next == "嘛" || next == "吧" { continue }
                text = trimmingLeading(String(rest))
                changed = true
                break
            }
            let words = text.split(separator: " ", maxSplits: 1, omittingEmptySubsequences: true)
            if words.count == 2, englishFillers.contains(String(words[0]).trimmingCharacters(in: .punctuationCharacters)) {
                text = trimmingLeading(String(words[1]))
                changed = true
            }
        }
        return text
    }

    static func trimmingLeading(_ text: String) -> String {
        let separators = CharacterSet.whitespacesAndNewlines.union(.punctuationCharacters)
        return String(text.drop(while: { $0.unicodeScalars.allSatisfy(separators.contains) }))
    }

    public static func assess(_ raw: String, mode: SessionMode = .meeting, hasPriorAnswer: Bool = false) -> IntentAssessment {
        let text = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { return .init(intent: .statement, score: 0, reasons: ["empty"]) }
        let original = text.lowercased()
        let originalStripped = original.trimmingCharacters(in: .whitespacesAndNewlines.union(.punctuationCharacters).union(.symbols))
        let originalCompact = originalStripped.filter { !$0.isWhitespace && !$0.isPunctuation }
        let lower = strippingFillers(original)
        let stripped = lower.trimmingCharacters(in: .whitespacesAndNewlines.union(.punctuationCharacters).union(.symbols))
        let compact = stripped.filter { !$0.isWhitespace && !$0.isPunctuation }
        let cjk = cjkCount(stripped)
        let words = lower.split(whereSeparator: { !$0.isLetter && !$0.isNumber && $0 != "'" }).map(String.init)
        var reasons: [String] = []

        // Short follow-ups are only meaningful in a conversation.
        if shortFollowUps.contains(stripped) || shortFollowUps.contains(compact) || shortFollowUps.contains(originalCompact)
            || isPatternFollowUp(stripped, cjk: cjk, words: words) || isPatternFollowUp(originalStripped, cjk: cjk, words: words) {
            let base = hasPriorAnswer ? 0.75 : 0.55
            return .init(intent: .followUp, score: base, reasons: ["follow-up form"])
        }

        var score = 0.0
        var request = false
        let lastQuestionMark = text.lastIndex(where: { $0 == "?" || $0 == "？" })
        let endsWithQuestionMark = text.last == "?" || text.last == "？"
        if endsWithQuestionMark { score += 0.6; reasons.append("ends with ?") }
        else if let mark = lastQuestionMark {
            let tail = String(text[text.index(after: mark)...]).trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
            if rhetoricalAnswerStarts.contains(where: { tail.hasPrefix($0) }) && tail.count >= 4 {
                score -= 0.35; reasons.append("question answered by speaker")
            } else { score += 0.4; reasons.append("contains ?") }
        }
        let addressed = compact.contains("你") || compact.contains("您") || compact.contains("大家") || compact.contains("各位")
            || words.contains("you") || words.contains("your") || words.contains("you're") || words.contains("you've") || words.contains("you'd") || words.contains("anyone")

        // English structure.
        if let first = words.first {
            let subjects = ["you", "we", "they", "it", "i", "there", "this", "that", "he", "she", "your", "the", "a", "an", "any", "anyone", "someone", "everyone", "anybody"]
            if englishWh.contains(first) {
                // "What we need is…" / "How I see it…" are pseudo-clefts, not questions.
                let cleft = words.count >= 3 && ["i", "we", "they", "he", "she"].contains(words[1])
                if cleft { score += 0.2; reasons.append("wh cleft") } else { score += 0.55; reasons.append("wh-start") }
            }
            else if englishAux.contains(first), words.count >= 3, subjects.contains(words[1]), !(first == "do" && words[1] == "it") { score += 0.55; reasons.append("aux inversion") }
            else if englishAux.contains(first), words.count >= 3 { score += 0.35; reasons.append("aux inversion weak") }
            else if words.count >= 4, ["so", "and", "but", "okay", "ok", "now", "then", "also"].contains(first),
                    englishWh.contains(words[1]) || (englishAux.contains(words[1]) && ["you", "we", "they", "it", "i", "there", "this", "that"].contains(words[2])) {
                score += 0.55; reasons.append("wh after discourse marker")
            }
        }
        // Tag questions: "you've used postgres before right", "…, isn't it".
        if cjk == 0, words.count >= 4, addressed,
           let last = words.last, ["right", "correct", "yeah", "no"].contains(last) || lower.hasSuffix("isn't it") || lower.hasSuffix("aren't you") || lower.hasSuffix("don't you") || lower.hasSuffix("didn't you") || lower.hasSuffix("haven't you") {
            score += 0.45; reasons.append("tag question")
        }
        let englishRequest = englishRequests.contains(where: { lower.hasPrefix($0) || lower.contains(" " + $0) })
        if englishRequest { score += 0.5; request = true; reasons.append("english request") }
        if !englishRequest, englishSelfNarration.contains(where: { lower.hasPrefix($0) }) {
            score -= 0.55; reasons.append("speaker narration")
        }
        if lower.hasPrefix("i wonder") || lower.hasPrefix("i don't know why") || lower.hasPrefix("i'm not sure why") { score -= 0.2 }
        if cjk == 0, addressed { score += 0.05 }

        // Chinese structure.
        if cjk > 0 {
            let falsePositive = chineseFalseInterrogatives.contains { compact.contains($0) }
            if !falsePositive, chineseInterrogatives.contains(where: { compact.contains($0) }) {
                score += 0.55; reasons.append("chinese interrogative")
            } else if falsePositive { reasons.append("interrogative filler") }
            // Alternative question: “你们是单体还是微服务”.
            if addressed, let alt = compact.range(of: "还是"), compact[..<alt.lowerBound].contains("是"), compact[alt.upperBound...].count >= 2 {
                score += 0.5; reasons.append("alternative interrogative")
            }
            if let tail = compact.last {
                switch tail {
                case "吗", "么": score += 0.5; reasons.append("particle 吗")
                // Recognizers often write 吗 as 嘛 / 嘛 is also a rhetorical particle.
                case "嘛": score += addressed ? 0.45 : 0.25; reasons.append("particle 嘛")
                case "呢":
                    score += (compact.hasPrefix("那") || addressed) ? 0.5 : 0.3; reasons.append("particle 呢")
                case "吧":
                    if compact.hasSuffix("对吧") || compact.hasSuffix("是吧") { score += 0.35; reasons.append("particle 吧") }
                    // “你应该用过 Redis 吧” is a confirmation question addressed to me.
                    else if addressed, compact.count >= 6, !compact.hasSuffix("好吧"), !compact.hasSuffix("行吧"), !compact.hasSuffix("算了吧") { score += 0.45; reasons.append("particle 吧") }
                default: break
                }
            }
            if chineseRequests.contains(where: { compact.contains($0) }) {
                score += 0.5; request = true; reasons.append("chinese request")
            } else if addressed, chineseAddressedRequests.contains(where: { compact.contains($0) }) {
                score += 0.5; request = true; reasons.append("addressed request")
            }
            if chineseSelfNarration.contains(where: { compact.hasPrefix($0) || compact.contains("，" + $0) }) {
                score -= 0.55; reasons.append("speaker narration")
            }
            if addressed { score += 0.05 }
        }
        if logistics.contains(where: { lower.contains($0) || compact.contains($0) }) {
            score -= 0.6; reasons.append("audio / screen check")
        }

        if compact.count < 2 { score = min(score, 0.2) }
        score = max(0, min(1, score))

        if isIncomplete(lower: lower, compact: compact, words: words, cjk: cjk, endsWithQuestionMark: endsWithQuestionMark) {
            return .init(intent: .incomplete, score: score, reasons: reasons + ["incomplete"])
        }
        let thresholds = Thresholds.forMode(mode)
        if score >= thresholds.uncertain {
            let isQuestionShape = reasons.contains { $0.contains("?") || $0.contains("wh") || $0.contains("interrogative") || $0.contains("particle") || $0.contains("inversion") || $0.contains("tag") }
            return .init(intent: request && !isQuestionShape ? .explanationRequest : .question, score: score, reasons: reasons)
        }
        return .init(intent: .statement, score: score, reasons: reasons)
    }

    static func isPatternFollowUp(_ stripped: String, cjk: Int, words: [String]) -> Bool {
        // 那缺点呢 / 那 Redis 呢 / what about Redis / how about the cost
        let compact = stripped.filter { !$0.isWhitespace }
        if cjk > 0, compact.hasPrefix("那"), !compact.hasPrefix("那么"), !compact.hasPrefix("那个"), compact.hasSuffix("呢"), compact.count <= 40 { return true }
        if (stripped.hasPrefix("what about ") || stripped.hasPrefix("how about ")) && words.count <= 6 { return true }
        return false
    }
    static func isIncomplete(lower: String, compact: String, words: [String], cjk: Int, endsWithQuestionMark: Bool) -> Bool {
        if endsWithQuestionMark { return false }
        let trimmed = lower.trimmingCharacters(in: .whitespaces)
        if trimmed.hasSuffix(",") || trimmed.hasSuffix("，") || trimmed.hasSuffix("、") || trimmed.hasSuffix("...") || trimmed.hasSuffix("…") { return true }
        if cjk == 0 {
            guard let last = words.last else { return false }
            // Long sentences may end in a preposition (“…you're proud of”); only articles,
            // conjunctions and short fragments count as dangling there.
            let alwaysDangling: Set<String> = ["the", "a", "an", "and", "or", "but", "because", "if", "than", "your", "my"]
            if words.count >= 2, danglingEnglish.contains(last),
               words.count < 6 || alwaysDangling.contains(last),
               !(["you", "this", "that", "these", "those", "is", "are", "so"].contains(last) && words.count >= 5) { return true }
            if let first = words.first {
                if ["can", "could", "would", "will"].contains(first), words.count >= 2, words[1] == "you" { return words.count <= 3 }
                if englishWh.contains(first) { return words.count <= 2 }
            }
            if lower.hasPrefix("please explain") || lower.hasPrefix("explain") || lower.hasPrefix("tell me") { return words.count <= 2 }
            return false
        }
        let hasInterrogative = chineseInterrogatives.contains { compact.contains($0) }
        if danglingChinese.contains(where: { compact.hasSuffix($0) }), compact.count >= 3, !compact.hasSuffix("的吗"), !compact.hasSuffix("是的"),
           !(hasInterrogative && compact.hasSuffix("的")) { return true }
        if compact.hasPrefix("请解释") || compact.hasPrefix("解释一下") || compact == "介绍一下" || compact == "讲一下" || compact == "请问" || compact == "我想问" || compact == "我想问一下" { return compact.count <= 4 }
        if compact.hasPrefix("请问") || compact.hasPrefix("我想问") { return compact.count <= 5 }
        return false
    }

    static func cjkCount(_ text: String) -> Int {
        text.unicodeScalars.filter { (0x3400...0x9FFF).contains($0.value) }.count
    }
}

public struct QuestionCandidate: Identifiable, Sendable, Equatable {
    public enum State: String, Sendable { case forming, ready, triggered, dismissed }
    public let id: UUID
    public var text: String
    public var assessment: IntentAssessment
    public var utteranceKey: String?
    public var firstSeen: Date
    public var lastChanged: Date
    public var revision: Int
    public var isFinal: Bool
    public var state: State
    /// Statements the same speaker said just before the question (“我看你简历上写了
    /// Kafka。” → “讲讲你是怎么用的”), so the answer sees the whole question.
    public var leadIn: String? = nil
    /// Set when this candidate replaces an earlier, premature trigger of the same
    /// utterance (the speaker kept talking after a stable partial was answered).
    public var supersedes: UUID? = nil

    /// Question text including its lead-in, for the answer prompt and the UI.
    public var fullText: String {
        guard let leadIn, !leadIn.isEmpty else { return text }
        return leadIn + (QuestionIntentClassifier.cjkCount(String(leadIn.suffix(1))) > 0 ? "" : " ") + text
    }
}

public enum CandidateDecision: Equatable, Sendable {
    case none
    /// UI-only update; no model call.
    case updated(QuestionCandidate)
    /// Stable, actionable question: start exactly one answer.
    case trigger(QuestionCandidate)
    /// Between the uncertain and actionable thresholds: optional second-layer check.
    case uncertain(QuestionCandidate)
}

/// Candidate-based question scheduling. Streaming revisions of the same utterance
/// update one candidate; a model call is made only when the candidate is stable
/// (final, or a strong question unchanged for `stabilityInterval`). Each candidate
/// triggers at most once, and exact repeats inside `duplicateWindow` are ignored.
public struct QuestionCandidateTracker: Sendable {
    public struct Config: Sendable, Equatable {
        public var stabilityInterval: TimeInterval = 1.2
        /// Shorter wait for partials that already end like a finished question
        /// (“？”, 吗 / 呢, or very strong evidence).
        public var strongStabilityInterval: TimeInterval = 0.6
        public var earlyTriggerScore: Double = 0.8
        /// Statements within this window before a question become its lead-in.
        public var leadInWindow: TimeInterval = 8
        public var fragmentWindow: TimeInterval = 8
        public var duplicateWindow: TimeInterval = 20
        public var minimumTriggerInterval: TimeInterval = 2.5
        public init() {}
    }

    public var mode: SessionMode
    public var hasPriorAnswer = false
    public var config: Config
    public private(set) var current: QuestionCandidate?
    private var fragment: (text: String, date: Date)?
    private var recentKeys: [(key: String, date: Date)] = []
    private var lastTriggerAt: Date?
    private var triggeredKeyForUtterance: [String: (key: String, id: UUID)] = [:]
    private var recentStatements: [(text: String, date: Date)] = []

    public init(mode: SessionMode = .meeting, config: Config = Config()) {
        self.mode = mode; self.config = config
    }

    public mutating func reset() {
        current = nil; fragment = nil; recentKeys.removeAll(); lastTriggerAt = nil; triggeredKeyForUtterance.removeAll(); recentStatements.removeAll(); hasPriorAnswer = false
    }

    /// Feed recognized text. `utteranceKey` identifies one utterance whose partial
    /// text may be revised (Apple/cloud result id or local stream identity).
    public mutating func ingest(_ raw: String, isFinal: Bool, utteranceKey: String? = nil, now: Date = Date()) -> CandidateDecision {
        expire(now)
        let incoming = String(raw.suffix(2_000)).trimmingCharacters(in: .whitespacesAndNewlines)
        guard !incoming.isEmpty else { return .none }
        var combined = fragment.map { $0.text + (needsSpace($0.text, incoming) ? " " : "") + incoming } ?? incoming
        var assessment = QuestionIntentClassifier.assess(combined, mode: mode, hasPriorAnswer: hasPriorAnswer)
        if fragment != nil {
            // A dangling fragment must not turn the speaker's own narration
            // (“我想先说一下背景”) into a request just by being glued in front of it.
            let own = QuestionIntentClassifier.assess(incoming, mode: mode, hasPriorAnswer: hasPriorAnswer)
            if own.reasons.contains(where: { $0 == "speaker narration" || $0 == "audio / screen check" }) {
                combined = incoming; assessment = own
                if isFinal { fragment = nil }
            }
        }

        var candidate: QuestionCandidate
        if var existing = current, !existing.isFinal, existing.utteranceKey == utteranceKey || utteranceKey == nil {
            if existing.text != combined { existing.lastChanged = now; existing.revision += 1 }
            existing.text = combined; existing.assessment = assessment; existing.isFinal = isFinal
            candidate = existing
        } else {
            candidate = QuestionCandidate(id: UUID(), text: combined, assessment: assessment, utteranceKey: utteranceKey, firstSeen: now, lastChanged: now, revision: 0, isFinal: isFinal, state: .forming)
        }

        if !isFinal {
            current = candidate
            return assessment.intent.isActionable || assessment.intent == .incomplete ? .updated(candidate) : .none
        }

        // Final text for this utterance.
        fragment = nil
        if assessment.intent == .incomplete {
            fragment = (String(combined.suffix(2_000)), now)
            candidate.state = .forming
            current = nil
            return .updated(candidate)
        }
        current = nil
        if let key = utteranceKey, let earlier = triggeredKeyForUtterance[key] {
            // An early trigger already answered this utterance; only a materially
            // different final text counts as a new question. When the final text
            // extends the early one (the speaker kept going), it replaces that answer.
            let final = normalize(combined)
            let overlap = similarity(earlier.key, final)
            if overlap >= 0.6 && final.count <= earlier.key.count + 6 { candidate.state = .triggered; return .none }
            if final.hasPrefix(earlier.key) || overlap >= 0.3 { candidate.supersedes = earlier.id }
        }
        let decision = decide(candidate, now: now)
        switch decision {
        case .trigger, .uncertain: break
        default:
            if !assessment.intent.isActionable {
                recentStatements.append((String(combined.suffix(300)), now))
                if recentStatements.count > 3 { recentStatements.removeFirst(recentStatements.count - 3) }
            }
        }
        return decision
    }

    /// Call periodically (e.g. every 250 ms) to allow early triggering of a stable partial.
    public mutating func tick(now: Date = Date()) -> CandidateDecision {
        expire(now)
        guard var candidate = current, !candidate.isFinal, candidate.state == .forming,
              candidate.assessment.intent.isActionable,
              candidate.assessment.score >= config.earlyTriggerScore,
              now.timeIntervalSince(candidate.lastChanged) >= stability(for: candidate) else { return .none }
        let decision = decide(candidate, now: now)
        if case let .trigger(triggered) = decision {
            candidate = triggered
            current = candidate
            if let key = candidate.utteranceKey { triggeredKeyForUtterance[key] = (normalize(candidate.text), candidate.id) }
        }
        return decision
    }

    private func stability(for candidate: QuestionCandidate) -> TimeInterval {
        let text = candidate.text.trimmingCharacters(in: .whitespacesAndNewlines)
        let endsLikeQuestion = text.hasSuffix("?") || text.hasSuffix("？") || text.hasSuffix("吗") || text.hasSuffix("呢")
        return endsLikeQuestion || candidate.assessment.score >= 0.95 ? config.strongStabilityInterval : config.stabilityInterval
    }

    private func leadIn(before date: Date) -> String? {
        let lines = recentStatements.filter { date.timeIntervalSince($0.date) <= config.leadInWindow && $0.date <= date }.map(\.text)
        guard !lines.isEmpty else { return nil }
        return String(lines.joined(separator: " ").suffix(400))
    }

    /// Record that an answer was produced, enabling bare follow-ups like “为什么”.
    public mutating func noteAnswered() { hasPriorAnswer = true }

    private mutating func decide(_ input: QuestionCandidate, now: Date) -> CandidateDecision {
        var candidate = input
        let thresholds = QuestionIntentClassifier.Thresholds.forMode(mode)
        guard candidate.assessment.intent.isActionable else { return .none }
        let key = normalize(candidate.text)
        guard !key.isEmpty, !recentKeys.contains(where: { $0.key == key }) else { return .none }
        if candidate.assessment.score < thresholds.actionable {
            candidate.state = .forming
            if candidate.leadIn == nil { candidate.leadIn = leadIn(before: candidate.firstSeen) }
            return .uncertain(candidate)
        }
        if let lastTriggerAt, now.timeIntervalSince(lastTriggerAt) < config.minimumTriggerInterval,
           candidate.assessment.intent == .followUp {
            // Bare follow-ups right after another trigger are usually echoes.
            return .none
        }
        recentKeys.append((key, now))
        if recentKeys.count > 40 { recentKeys.removeFirst(recentKeys.count - 40) }
        lastTriggerAt = now
        candidate.state = .triggered
        if candidate.leadIn == nil { candidate.leadIn = leadIn(before: candidate.firstSeen) }
        recentStatements.removeAll()
        return .trigger(candidate)
    }

    /// Accept an uncertain candidate after a second-layer check (or a user click).
    public mutating func confirm(_ candidate: QuestionCandidate, now: Date = Date()) -> Bool {
        let key = normalize(candidate.text)
        guard !recentKeys.contains(where: { $0.key == key }) else { return false }
        recentKeys.append((key, now)); lastTriggerAt = now
        return true
    }

    private mutating func expire(_ now: Date) {
        recentKeys.removeAll { now.timeIntervalSince($0.date) > config.duplicateWindow }
        recentStatements.removeAll { now.timeIntervalSince($0.date) > config.leadInWindow }
        if let fragment, now.timeIntervalSince(fragment.date) > config.fragmentWindow { self.fragment = nil }
        if triggeredKeyForUtterance.count > 50 { triggeredKeyForUtterance.removeAll() }
    }

    private func needsSpace(_ left: String, _ right: String) -> Bool {
        guard let l = left.last, let r = right.first else { return false }
        return !(QuestionIntentClassifier.cjkCount(String(l)) > 0 && QuestionIntentClassifier.cjkCount(String(r)) > 0)
    }

    func normalize(_ text: String) -> String {
        text.lowercased().filter { $0.isLetter || $0.isNumber || $0 == " " }
            .split(separator: " ").joined(separator: " ")
    }

    func similarity(_ a: String, _ b: String) -> Double {
        let left = Set(bigrams(a)), right = Set(bigrams(b))
        guard !left.isEmpty || !right.isEmpty else { return 1 }
        return Double(left.intersection(right).count) / Double(max(1, left.union(right).count))
    }

    private func bigrams(_ text: String) -> [String] {
        let chars = Array(text.filter { !$0.isWhitespace })
        guard chars.count > 1 else { return chars.map(String.init) }
        return (0..<(chars.count - 1)).map { String(chars[$0...$0 + 1]) }
    }
}

/// Offline evaluation: recall and false triggers per hour on a labeled corpus.
public enum QuestionDetectionEvaluator {
    public struct Sample: Sendable {
        public var text: String
        public var isQuestion: Bool
        public init(_ text: String, _ isQuestion: Bool) { self.text = text; self.isQuestion = isQuestion }
    }
    public struct Report: Sendable, CustomStringConvertible {
        public var questions = 0, detected = 0, statements = 0, falseTriggers = 0
        public var missed: [String] = [], falsePositives: [String] = []
        public var simulatedHours = 0.0
        public var recall: Double { questions == 0 ? 1 : Double(detected) / Double(questions) }
        public var falseTriggersPerHour: Double { simulatedHours == 0 ? 0 : Double(falseTriggers) / simulatedHours }
        public var description: String {
            String(format: "recall %.2f (%d/%d), false triggers %d (%.1f/h)", recall, detected, questions, falseTriggers, falseTriggersPerHour)
                + (missed.isEmpty ? "" : "\nmissed: " + missed.joined(separator: " | "))
                + (falsePositives.isEmpty ? "" : "\nfalse: " + falsePositives.joined(separator: " | "))
        }
    }

    /// Each sample is fed as one final utterance, `spacing` seconds apart.
    /// Uncertain decisions count as not triggered (first layer only).
    public static func evaluate(_ samples: [Sample], mode: SessionMode = .meeting, spacing: TimeInterval = 6) -> Report {
        var tracker = QuestionCandidateTracker(mode: mode)
        tracker.hasPriorAnswer = true
        var report = Report()
        var now = Date(timeIntervalSince1970: 0)
        for sample in samples {
            now = now.addingTimeInterval(spacing)
            let triggered: Bool
            if case .trigger = tracker.ingest(sample.text, isFinal: true, now: now) { triggered = true } else { triggered = false }
            if sample.isQuestion {
                report.questions += 1
                if triggered { report.detected += 1 } else { report.missed.append(sample.text) }
            } else {
                report.statements += 1
                if triggered { report.falseTriggers += 1; report.falsePositives.append(sample.text) }
            }
        }
        report.simulatedHours = Double(samples.count) * spacing / 3_600
        return report
    }
}
