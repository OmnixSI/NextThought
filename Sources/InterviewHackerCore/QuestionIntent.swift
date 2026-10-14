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
            case .meeting: Thresholds(actionable: 0.5, uncertain: 0.3)
            }
        }
    }

    static let englishWh = ["what", "what's", "whats", "why", "how", "when", "where", "who", "whom", "whose", "which"]
    static let englishAux = ["is", "are", "was", "were", "do", "does", "did", "can", "could", "would", "will", "should", "shall", "have", "has", "had", "may", "might", "isn't", "aren't", "don't", "doesn't", "didn't", "can't", "won't", "wouldn't", "shouldn't"]
    static let englishRequests = ["tell me", "walk me through", "explain", "describe", "compare", "talk about", "give me an example", "give an example", "elaborate", "help me understand", "share", "please explain", "can you explain", "could you explain", "say more about", "go over", "talk me through", "introduce yourself", "define"]
    static let englishSelfNarration = ["let me", "i'll", "i will", "let's", "we'll", "we will", "i'm going to", "i am going to", "today we", "now we", "next we", "so we", "i want to explain", "i'd like to"]
    static let chineseInterrogatives = ["什么", "为什么", "为啥", "怎么", "怎样", "如何", "哪个", "哪些", "哪里", "哪儿", "谁", "多少", "几个", "几种", "是否", "是不是", "有没有", "能不能", "会不会", "可不可以", "对不对", "行不行", "要不要", "好不好", "能否", "可否", "难道", "咋", "哪种", "哪一", "哪家", "哪位", "为何", "何时", "何种", "几点", "多久", "多长", "多大"]
    static let chineseFalseInterrogatives = ["没什么", "什么的", "不管怎么", "怎么说呢", "不知道为什么", "不知道怎么", "什么都", "怎么都", "为什么呢因为", "无论如何", "不论什么", "说什么也", "这么说", "那么说", "可以随时问", "有什么不懂", "有什么问题可以", "有什么问题随时"]
    static let chineseRequests = ["介绍一下", "介绍下", "讲一下", "讲讲", "谈谈", "谈一下", "说说", "说一下", "聊聊", "聊一下", "解释", "说明一下", "分析一下", "分析下", "比较一下", "对比一下", "描述一下", "举个例子", "举一个例子", "举例", "展开说", "展开讲", "详细说", "详细讲", "具体说", "具体讲", "讲解一下", "阐述", "总结一下", "回答一下", "给我讲", "跟我说", "怎么理解"]
    static let chineseSelfNarration = ["我来", "我先", "让我", "我们来", "下面我", "接下来我", "我给大家", "我想先", "我简单", "我们先", "我们接下来", "今天我们", "我跟大家"]
    static let rhetoricalAnswerStarts = ["因为", "答案是", "其实", "原因是", "这是因为", "很简单", "我们来看", "首先", "because", "the answer", "well,", "it's because", "that's because", "let's see"]
    static let shortFollowUps: Set<String> = ["为什么", "怎么", "然后呢", "那为什么", "那怎么", "所以呢", "举例呢", "比如呢", "还有呢", "具体呢", "怎么做", "为啥", "然后", "why", "how", "and why", "why not", "how so", "and then", "such as", "for example", "like what", "what about that", "what else", "anything else", "go on", "then what", "how come"]
    static let danglingEnglish: Set<String> = ["the", "a", "an", "of", "and", "or", "to", "about", "between", "with", "for", "in", "on", "your", "my", "is", "are", "you", "if", "that", "which", "because", "so", "but", "than", "from", "like", "this", "these", "those", "into"]
    static let danglingChinese = ["的", "和", "或者", "还有", "就是", "那个", "这个", "然后", "如果", "因为", "以及", "跟", "与", "关于", "比如", "一个", "在", "对于", "把", "被", "但是", "而且", "所以"]

    public static func assess(_ raw: String, mode: SessionMode = .meeting, hasPriorAnswer: Bool = false) -> IntentAssessment {
        let text = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { return .init(intent: .statement, score: 0, reasons: ["empty"]) }
        let lower = text.lowercased()
        let stripped = lower.trimmingCharacters(in: .whitespacesAndNewlines.union(.punctuationCharacters).union(.symbols))
        let compact = stripped.filter { !$0.isWhitespace && !$0.isPunctuation }
        let cjk = cjkCount(stripped)
        let words = lower.split(whereSeparator: { !$0.isLetter && !$0.isNumber && $0 != "'" }).map(String.init)
        var reasons: [String] = []

        // Short follow-ups are only meaningful in a conversation.
        if shortFollowUps.contains(stripped) || shortFollowUps.contains(compact) || isPatternFollowUp(stripped, cjk: cjk, words: words) {
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

        // English structure.
        if let first = words.first {
            let subjects = ["you", "we", "they", "it", "i", "there", "this", "that", "he", "she", "your", "the", "a", "an", "any", "anyone", "someone", "everyone"]
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
        if englishRequests.contains(where: { lower.hasPrefix($0) || lower.contains(" " + $0) || lower.contains("you " + $0) }) {
            score += 0.5; request = true; reasons.append("english request")
        }
        if englishSelfNarration.contains(where: { lower.hasPrefix($0) }) {
            score -= 0.55; reasons.append("speaker narration")
        }
        if lower.hasPrefix("i wonder") || lower.hasPrefix("i don't know why") || lower.hasPrefix("i'm not sure why") { score -= 0.2 }
        if lower.contains(" you ") || lower.hasPrefix("you ") || lower.contains(" your ") { score += 0.05 }

        // Chinese structure.
        if cjk > 0 {
            let falsePositive = chineseFalseInterrogatives.contains { compact.contains($0) }
            if !falsePositive, chineseInterrogatives.contains(where: { compact.contains($0) }) {
                score += 0.55; reasons.append("chinese interrogative")
            } else if falsePositive { reasons.append("interrogative filler") }
            if let tail = compact.last {
                switch tail {
                case "吗", "么": score += 0.5; reasons.append("particle 吗")
                case "呢": score += 0.3; reasons.append("particle 呢")
                case "吧": if compact.hasSuffix("对吧") || compact.hasSuffix("是吧") { score += 0.35 }
                default: break
                }
            }
            if chineseRequests.contains(where: { compact.contains($0) }) {
                score += 0.5; request = true; reasons.append("chinese request")
            }
            if chineseSelfNarration.contains(where: { compact.hasPrefix($0) || compact.contains("，" + $0) }) {
                score -= 0.55; reasons.append("speaker narration")
            }
            if compact.contains("你") || compact.contains("您") || compact.contains("大家觉得") || compact.contains("同学们") { score += 0.05 }
        }

        if compact.count < 2 { score = min(score, 0.2) }
        score = max(0, min(1, score))

        if isIncomplete(lower: lower, compact: compact, words: words, cjk: cjk, endsWithQuestionMark: endsWithQuestionMark) {
            return .init(intent: .incomplete, score: score, reasons: reasons + ["incomplete"])
        }
        let thresholds = Thresholds.forMode(mode)
        if score >= thresholds.uncertain {
            let isQuestionShape = reasons.contains { $0.contains("?") || $0.contains("wh") || $0.contains("interrogative") || $0.contains("particle") || $0.contains("inversion") }
            return .init(intent: request && !isQuestionShape ? .explanationRequest : .question, score: score, reasons: reasons)
        }
        return .init(intent: .statement, score: score, reasons: reasons)
    }

    static func isPatternFollowUp(_ stripped: String, cjk: Int, words: [String]) -> Bool {
        // 那缺点呢 / 那 Redis 呢 / what about Redis / how about the cost
        if cjk > 0, stripped.hasPrefix("那"), !stripped.hasPrefix("那么"), stripped.hasSuffix("呢"), stripped.count <= 14 { return true }
        if (stripped.hasPrefix("what about ") || stripped.hasPrefix("how about ")) && words.count <= 6 { return true }
        return false
    }

    static func isIncomplete(lower: String, compact: String, words: [String], cjk: Int, endsWithQuestionMark: Bool) -> Bool {
        if endsWithQuestionMark { return false }
        let trimmed = lower.trimmingCharacters(in: .whitespaces)
        if trimmed.hasSuffix(",") || trimmed.hasSuffix("，") || trimmed.hasSuffix("、") || trimmed.hasSuffix("...") || trimmed.hasSuffix("…") { return true }
        if cjk == 0 {
            guard let last = words.last else { return false }
            if words.count >= 2, danglingEnglish.contains(last), !(["you", "this", "that", "these", "those", "is", "are", "so"].contains(last) && words.count >= 5) { return true }
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
        public var earlyTriggerScore: Double = 0.8
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
    private var triggeredKeyForUtterance: [String: String] = [:]

    public init(mode: SessionMode = .meeting, config: Config = Config()) {
        self.mode = mode; self.config = config
    }

    public mutating func reset() {
        current = nil; fragment = nil; recentKeys.removeAll(); lastTriggerAt = nil; triggeredKeyForUtterance.removeAll(); hasPriorAnswer = false
    }

    /// Feed recognized text. `utteranceKey` identifies one utterance whose partial
    /// text may be revised (Apple/cloud result id, Whisper voice id).
    public mutating func ingest(_ raw: String, isFinal: Bool, utteranceKey: String? = nil, now: Date = Date()) -> CandidateDecision {
        expire(now)
        let incoming = String(raw.suffix(2_000)).trimmingCharacters(in: .whitespacesAndNewlines)
        guard !incoming.isEmpty else { return .none }
        let combined = fragment.map { $0.text + (needsSpace($0.text, incoming) ? " " : "") + incoming } ?? incoming
        let assessment = QuestionIntentClassifier.assess(combined, mode: mode, hasPriorAnswer: hasPriorAnswer)

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
            // different final text counts as a new question.
            if similarity(earlier, normalize(combined)) >= 0.6 { candidate.state = .triggered; return .none }
        }
        return decide(candidate, now: now)
    }

    /// Call periodically (e.g. every 250 ms) to allow early triggering of a stable partial.
    public mutating func tick(now: Date = Date()) -> CandidateDecision {
        expire(now)
        guard var candidate = current, !candidate.isFinal, candidate.state == .forming,
              candidate.assessment.intent.isActionable,
              candidate.assessment.score >= config.earlyTriggerScore,
              now.timeIntervalSince(candidate.lastChanged) >= config.stabilityInterval else { return .none }
        let decision = decide(candidate, now: now)
        if case let .trigger(triggered) = decision {
            candidate = triggered
            current = candidate
            if let key = candidate.utteranceKey { triggeredKeyForUtterance[key] = normalize(candidate.text) }
        }
        return decision
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
