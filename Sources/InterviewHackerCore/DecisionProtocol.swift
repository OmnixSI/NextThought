import Foundation

/// Typed decisions in the StartLux-Decision `/v1/systemone` format: a `state` (the
/// situation as text) and named questions — pick one option, yes/no, or a score. The
/// model returns a probability for every option, read from its letter log-probs, so
/// one request takes tens of milliseconds and never “writes” an answer.
public enum DecisionQuestion: Sendable, Equatable {
    public struct Option: Sendable, Equatable {
        public var id: String
        public var description: String?
        public init(_ id: String, _ description: String?) { self.id = id; self.description = description }
    }
    case choice(instructions: String, options: [Option])
    case yesNo(instructions: String, yes: String? = nil, no: String? = nil)
    case score(instructions: String, levels: [String])

    var json: [String: Any] {
        switch self {
        case let .choice(instructions, options):
            var criteria: [String: Any] = [:]
            for option in options { criteria[option.id] = option.description ?? NSNull() }
            return ["type": "choice", "instructions": instructions, "criteria": criteria]
        case let .yesNo(instructions, yes, no):
            var object: [String: Any] = ["type": "noul", "instructions": instructions]
            if let yes, let no { object["criteria"] = ["true": yes, "false": no] }
            return object
        case let .score(instructions, levels):
            return ["type": "score", "instructions": instructions, "criteria": levels]
        }
    }
}

public struct DecisionAnswer: Sendable, Equatable {
    /// choice: the most likely option id.
    public var choice: String?
    public var confidence: Double?
    /// choice / score: probability per option id (score levels keyed "0"…"n-1").
    public var probabilities: [String: Double] = [:]
    /// yes/no: P(yes).
    public var yes: Double?
    /// score: probability-weighted mean level.
    public var score: Double?
    public var noneProbability: Double?

    public init(choice: String? = nil, confidence: Double? = nil, probabilities: [String: Double] = [:], yes: Double? = nil, score: Double? = nil, noneProbability: Double? = nil) {
        self.choice = choice; self.confidence = confidence; self.probabilities = probabilities
        self.yes = yes; self.score = score; self.noneProbability = noneProbability
    }
}

public enum DecisionProtocol {
    public static let path = "/v1/systemone"

    /// `thinkMode` "never": one read-out, no reasoning — what live decisions need
    /// (Instant models accept only that; AutoThink models would otherwise think for seconds).
    public static func body(state: String, questions: [String: DecisionQuestion], thinkMode: String = "never") -> Data {
        let object: [String: Any] = [
            "state": state,
            "questions": questions.mapValues(\.json),
            "think_mode": thinkMode,
            "none_check": false,
        ]
        return (try? JSONSerialization.data(withJSONObject: object, options: [.sortedKeys])) ?? Data("{}".utf8)
    }

    public static func parse(_ data: Data) -> [String: DecisionAnswer]? {
        guard let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let answers = root["answers"] as? [String: Any] else { return nil }
        var result: [String: DecisionAnswer] = [:]
        for (key, value) in answers {
            guard let item = value as? [String: Any] else { continue }
            var answer = DecisionAnswer()
            answer.choice = item["choice"] as? String
            answer.confidence = (item["confidence"] as? NSNumber)?.doubleValue
            answer.yes = (item["noul"] as? NSNumber)?.doubleValue
            answer.score = (item["score"] as? NSNumber)?.doubleValue
            answer.noneProbability = (item["none_probability"] as? NSNumber)?.doubleValue
            if let probabilities = item["probabilities"] as? [String: Any] {
                answer.probabilities = probabilities.compactMapValues { ($0 as? NSNumber)?.doubleValue }
            }
            result[key] = answer
        }
        return result
    }

    /// Server error text (`{"error": …}`), for the settings page.
    public static func errorMessage(_ data: Data) -> String? {
        (try? JSONSerialization.jsonObject(with: data) as? [String: Any])?["error"] as? String
    }
}

/// The judgments InterviewHacker asks the decision model, each as one request.
/// Instructions are in English (the model's benchmarks are English); the state keeps
/// the conversation as recognised, mostly Chinese.
public enum DecisionTasks {
    public enum Key {
        public static let intent = "intent"
        public static let answerNow = "answer_now"
        public static let continues = "continues_previous"
        public static let needsWeb = "needs_web"
        public static let isUser = "is_user"
        public static let same = "same_question"
    }

    // MARK: Is the other side asking me something?

    public struct UtteranceVerdict: Sendable, Equatable {
        public var intent: UtteranceIntent
        /// P(the other side expects the user to answer now).
        public var answerProbability: Double
        /// P(the utterance completes / rephrases the previous question); nil when not asked.
        public var continuesPrevious: Double?
        /// Line checks (“can you hear me”) and rhetorical questions.
        public var isLogistics: Bool
        public var shouldAnswer: Bool { answerProbability >= 0.5 && intent.isActionable && !isLogistics }
        /// Strong enough to drop a question the rules already triggered on. The answer-now
        /// score runs low on real questions (Laya scored 29 of the corpus's 63 unsure rule
        /// triggers below 0.35, e.g. 「你期望的薪资是多少」 0.11) while its intent label is
        /// mostly right, so a veto needs both a non-question label and a very low score.
        public var declinesTrigger: Bool { answerProbability < 0.15 && (intent == .statement || isLogistics) }
    }

    static let intentOptions: [DecisionQuestion.Option] = [
        .init("question", "a direct question to the listener"),
        .init("request", "asks the listener to explain, introduce, walk through or do something"),
        .init("follow_up", "a follow-up on the listener's previous answer (why? any downsides? and then?)"),
        .init("incomplete", "the speaker has not finished the sentence yet"),
        .init("statement", "a statement, comment or acknowledgement that needs no answer"),
        .init("logistics", "a call check: can you hear me, screen sharing, waiting, scheduling"),
        .init("rhetorical", "a rhetorical question the speaker answers themselves (e.g. a lecturer)"),
    ]

    public static func utterance(_ text: String, context: [String], previousQuestion: String?, mode: SessionMode) -> (state: String, questions: [String: DecisionQuestion]) {
        var lines = ["Setting: \(roleDescription(mode))"]
        if !context.isEmpty {
            lines.append("Recent conversation (speech recognition, may contain recognition errors):")
            lines += context.suffix(6).map { String($0.prefix(300)) }
        }
        if let previousQuestion, !previousQuestion.isEmpty {
            lines.append("Previous question from the other side: 「\(String(previousQuestion.prefix(300)))」")
        }
        lines.append("The other side just said: 「\(String(text.prefix(600)))」")
        var questions: [String: DecisionQuestion] = [
            Key.intent: .choice(instructions: "What is this last utterance from the other side?", options: intentOptions),
            Key.answerNow: .yesNo(instructions: "Does the other side now expect the listener (me) to answer or respond to this last utterance?"),
        ]
        if let previousQuestion, !previousQuestion.isEmpty {
            questions[Key.continues] = .yesNo(instructions: "Is the last utterance a continuation, completion or rewording of the previous question (rather than a new topic)?")
        }
        return (lines.joined(separator: "\n"), questions)
    }

    public static func utteranceVerdict(_ answers: [String: DecisionAnswer]) -> UtteranceVerdict? {
        guard let yes = answers[Key.answerNow]?.yes else { return nil }
        let choice = answers[Key.intent]?.choice ?? "statement"
        let intent: UtteranceIntent
        switch choice {
        case "question": intent = .question
        case "request": intent = .explanationRequest
        case "follow_up": intent = .followUp
        case "incomplete": intent = .incomplete
        default: intent = .statement
        }
        return UtteranceVerdict(intent: intent, answerProbability: yes, continuesPrevious: answers[Key.continues]?.yes,
                                isLogistics: choice == "logistics" || choice == "rhetorical")
    }

    static func roleDescription(_ mode: SessionMode) -> String {
        switch mode {
        case .interview: "a job interview; the listener (me) is the candidate, the other side is the interviewer"
        case .academic: "an academic talk; the listener (me) is the presenter, the other side is the audience or chair"
        case .meeting: "a business meeting; the listener (me) is one participant"
        case .lesson: "a class; the other side is the lecturer, who often asks rhetorical questions"
        case .general: "a live conversation; the listener (me) is one participant"
        }
    }

    // MARK: Does this need the web?

    public static func webSearch(_ question: String, today: String) -> (state: String, questions: [String: DecisionQuestion]) {
        ("Today is \(today).\nQuestion asked in a live conversation: 「\(String(question.prefix(600)))」",
         [Key.needsWeb: .yesNo(instructions: "Does a good answer need facts that may be newer than a language model's training data (current prices, news, recent releases or versions, current office holders, events near today)? Concept, how-to and personal-experience questions do not.")])
    }

    // MARK: Is this microphone line the user, or the speakers?

    public static func echo(microphone text: String, otherSide: [String]) -> (state: String, questions: [String: DecisionQuestion]) {
        var lines = ["Without headphones the microphone also picks up the other side's voice from the speakers."]
        lines.append("The other side said at about the same time:")
        lines += otherSide.suffix(3).map { "「\(String($0.prefix(300)))」" }
        lines.append("The microphone recognised: 「\(String(text.prefix(300)))」")
        return (lines.joined(separator: "\n"),
                [Key.isUser: .yesNo(instructions: "Is the microphone text the listener's own words (a reply, comment or their own sentence), rather than the speakers replaying what the other side said?")])
    }

    // MARK: Are these the same question?

    public static func sameQuestion(_ first: String, _ second: String) -> (state: String, questions: [String: DecisionQuestion]) {
        ("Question A: 「\(String(first.prefix(400)))」\nQuestion B: 「\(String(second.prefix(400)))」",
         [Key.same: .yesNo(instructions: "Do A and B ask the same thing, so that one prepared answer fits both?")])
    }
}
