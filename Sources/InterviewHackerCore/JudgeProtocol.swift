import Foundation

/// Contract for an external judge model (“JEV”): small, fast yes/no decisions the
/// local rules are unsure about. Transport is an OpenAI-compatible chat completion;
/// the user message is a JSON object, the reply must contain one JSON object.
/// Full description: docs/JUDGE-API.md.
public enum JudgeTask: String, Codable, Sendable {
    /// Is this utterance a question / request the user should answer now?
    case questionDetection = "question_detection"
    /// Does answering need information newer than a model's training data?
    case webSearchNeed = "web_search_need"
}

public enum JudgeProtocol {
    public static let version = 1

    public struct Request: Codable, Sendable, Equatable {
        public var task: JudgeTask
        public var version: Int
        /// The utterance or question being judged.
        public var text: String
        /// A few preceding transcript lines, tagged [对方] / [我].
        public var context: String
        /// interview / academic / meeting / lesson / general
        public var mode: String
        /// Local rule score 0…1 for the same decision, as a hint.
        public var rule_score: Double
        public var today: String
    }

    public struct Verdict: Sendable, Equatable {
        public var yes: Bool
        public var confidence: Double
        /// web_search_need: a better search query, when the judge gives one.
        public var query: String?
    }

    public static let system = """
    You are a fast judge inside a live meeting / interview assistant. Read the JSON task and reply with ONE JSON object only, no prose.
    task "question_detection": is `text` a question or request that the listener (the user) should answer now? Consider `context` and `mode`. Reply {"answer": true|false, "confidence": 0..1}.
    task "web_search_need": does a good answer to `text` need facts newer than a language model's training data (prices, news, versions, current office holders, recent events, anything dated near `today`)? Concept and how-to questions do not. Reply {"answer": true|false, "confidence": 0..1, "query": "search query if true"}.
    """

    public static func request(task: JudgeTask, text: String, context: String = "", mode: String = "general", ruleScore: Double, today: String) -> (system: String, user: String) {
        let body = Request(task: task, version: version, text: String(text.prefix(2_000)), context: String(context.suffix(1_200)),
                           mode: mode, rule_score: (ruleScore * 100).rounded() / 100, today: today)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys]
        let data = (try? encoder.encode(body)) ?? Data("{}".utf8)
        return (system, String(decoding: data, as: UTF8.self))
    }

    /// Reads the first JSON object in the reply; also accepts a bare yes / no.
    public static func parse(_ reply: String) -> Verdict? {
        let trimmed = reply.trimmingCharacters(in: .whitespacesAndNewlines)
        if let start = trimmed.firstIndex(of: "{"), let end = trimmed.lastIndex(of: "}"), start < end,
           let object = try? JSONSerialization.jsonObject(with: Data(trimmed[start...end].utf8)) as? [String: Any] {
            let value = object["answer"] ?? object["is_question"] ?? object["needs_web_search"]
            let yes: Bool?
            switch value {
            case let bool as Bool: yes = bool
            case let text as String: yes = ["true", "yes", "是"].contains(text.lowercased())
            case let number as NSNumber: yes = number.boolValue
            default: yes = nil
            }
            guard let yes else { return nil }
            let confidence = (object["confidence"] as? NSNumber)?.doubleValue ?? 0.7
            let query = (object["query"] as? String).flatMap { $0.isEmpty ? nil : $0 }
            return Verdict(yes: yes, confidence: min(1, max(0, confidence)), query: query)
        }
        let word = trimmed.lowercased()
        if word.hasPrefix("yes") || word.hasPrefix("true") || word == "是" { return Verdict(yes: true, confidence: 0.6, query: nil) }
        if word.hasPrefix("no") || word.hasPrefix("false") || word == "否" { return Verdict(yes: false, confidence: 0.6, query: nil) }
        return nil
    }
}
