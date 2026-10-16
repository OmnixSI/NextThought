import Foundation

/// Judgments beyond question detection: the screen, the slide, how much of an answer
/// was said, what kind of question it is, which skills fit, notes worth keeping, the
/// end of the conversation, and whether a web answer changed anything.
/// States stay short (Laya reads at most 1,024 tokens).
extension DecisionTasks {
    public enum InsightKey {
        public static let screenKind = "screen_kind"
        public static let screenRelevant = "screen_relevant"
        public static let slide = "slide"
        public static let questionKind = "question_kind"
        public static let noteKind = "note_kind"
        public static let wrapUp = "wrap_up"
        public static let differs = "answers_differ"
        public static func point(_ index: Int) -> String { "point_\(index)" }
        public static func skill(_ index: Int) -> String { "skill_\(index)" }
    }

    // MARK: Screen

    static let screenOptions: [DecisionQuestion.Option] = [
        .init(ScreenKind.question.rawValue, "an interview, exam or coding question / problem statement"),
        .init(ScreenKind.code.rawValue, "source code or a code editor"),
        .init(ScreenKind.slides.rawValue, "presentation slides"),
        .init(ScreenKind.document.rawValue, "a document, paper, web page or spreadsheet"),
        .init(ScreenKind.chat.rawValue, "chat or meeting messages"),
        .init(ScreenKind.videoCall.rawValue, "a video call window (participants, controls) with little text"),
        .init(ScreenKind.other.rawValue, "anything else: desktop, menus, unrelated apps"),
    ]

    public static func screen(_ text: String, recent: [String], mode: SessionMode) -> (state: String, questions: [String: DecisionQuestion]) {
        var lines = ["Setting: \(roleDescription(mode))"]
        if !recent.isEmpty {
            lines.append("What was said just now:")
            lines += recent.suffix(3).map { String($0.suffix(160)) }
        }
        lines.append("Text recognised on the listener's screen (OCR, may be partial):")
        lines.append("「\(String(text.prefix(700)))」")
        return (lines.joined(separator: "\n"), [
            InsightKey.screenKind: .choice(instructions: "What is on the screen?", options: screenOptions),
            InsightKey.screenRelevant: .yesNo(instructions: "Is this screen content part of the current conversation (being discussed, shared, or the question being asked), so an answer should take it into account?"),
        ])
    }

    public struct ScreenVerdict: Sendable, Equatable {
        public var kind: ScreenKind
        public var relevance: Double
        public var isRelevant: Bool { relevance >= 0.5 }
    }

    public static func screenVerdict(_ answers: [String: DecisionAnswer]) -> ScreenVerdict? {
        guard let relevance = answers[InsightKey.screenRelevant]?.yes else { return nil }
        let kind = answers[InsightKey.screenKind]?.choice.flatMap(ScreenKind.init(rawValue:)) ?? .other
        return ScreenVerdict(kind: kind, relevance: relevance)
    }

    // MARK: Which slide

    /// Candidates are keyed "p<page>" within one deck; "none" when the screen shows none.
    public static func slide(_ screen: String, candidates: [SlidePage]) -> (state: String, questions: [String: DecisionQuestion]) {
        var options = candidates.prefix(5).map { DecisionQuestion.Option(slideKey($0), "page \($0.page): " + String($0.text.replacingOccurrences(of: "\n", with: " ").prefix(110))) }
        options.append(.init("none", "none of these slides is on the screen"))
        return ("Text recognised on the screen: 「\(String(screen.prefix(500)))」",
                [InsightKey.slide: .choice(instructions: "Which slide is currently shown on the screen (the large current slide, not a small preview of the next one)?", options: options)])
    }

    public static func slideKey(_ page: SlidePage) -> String { "p\(page.page)_\(page.documentID.uuidString.prefix(4))" }

    public static func slideChoice(_ answers: [String: DecisionAnswer], candidates: [SlidePage]) -> SlidePage? {
        guard let answer = answers[InsightKey.slide], let choice = answer.choice, choice != "none",
              (answer.probabilities[choice] ?? answer.confidence ?? 1) >= 0.4 else { return nil }
        return candidates.first { slideKey($0) == choice }
    }

    // MARK: How much has been said

    /// One yes/no per point; the state is what the speaker said since the points appeared.
    public static func coverage(speech: String, points: [(index: Int, text: String)]) -> (state: String, questions: [String: DecisionQuestion]) {
        var questions: [String: DecisionQuestion] = [:]
        for point in points.prefix(8) {
            questions[InsightKey.point(point.index)] = .yesNo(instructions: "Has the speaker already said this point, in any wording: 「\(String(point.text.prefix(120)))」?")
        }
        return ("What the speaker has said (speech recognition, may contain errors): 「\(String(speech.suffix(500)))」", questions)
    }

    public static func covered(_ answers: [String: DecisionAnswer], threshold: Double = 0.6) -> Set<Int> {
        Set(answers.compactMap { key, answer in
            guard key.hasPrefix("point_"), let index = Int(key.dropFirst(6)), (answer.yes ?? 0) >= threshold else { return nil }
            return index
        })
    }

    // MARK: What kind of question, which skills

    public static func questionKind(_ question: String, mode: SessionMode) -> (state: String, questions: [String: DecisionQuestion]) {
        ("Setting: \(roleDescription(mode))\nQuestion to answer: 「\(String(question.prefix(500)))」",
         [InsightKey.questionKind: .choice(instructions: "What kind of question is this?", options: QuestionKind.options)])
    }

    public static func questionKindChoice(_ answers: [String: DecisionAnswer]) -> QuestionKind? {
        guard let answer = answers[InsightKey.questionKind], let choice = answer.choice,
              (answer.probabilities[choice] ?? 1) >= 0.35 else { return nil }
        return QuestionKind(rawValue: choice)
    }

    /// One yes/no per skill (name + summary).
    public static func skills(_ question: String, skills: [(name: String, summary: String)]) -> (state: String, questions: [String: DecisionQuestion]) {
        var questions: [String: DecisionQuestion] = [:]
        for (index, skill) in skills.prefix(12).enumerated() {
            let about = skill.summary.isEmpty ? skill.name : "\(skill.name) — \(String(skill.summary.prefix(160)))"
            questions[InsightKey.skill(index)] = .yesNo(instructions: "Would this answering guide help answer the question: 「\(about)」?")
        }
        return ("Question to answer: 「\(String(question.prefix(500)))」", questions)
    }

    /// Indices of skills to drop: clearly unrelated (P < 0.3). Unanswered ones stay.
    public static func unrelatedSkills(_ answers: [String: DecisionAnswer], count: Int) -> Set<Int> {
        Set((0..<count).filter { (answers[InsightKey.skill($0)]?.yes ?? 1) < 0.3 })
    }

    // MARK: Notes and the end of the conversation

    static let noteOptions: [DecisionQuestion.Option] = [
        .init(LiveNote.Kind.actionItem.rawValue, "someone commits to do something or assigns a task (I will…, please send…, can you follow up…)"),
        .init(LiveNote.Kind.decision.rawValue, "a decision or agreement is stated (we will go with…, agreed, let's do…)"),
        .init(LiveNote.Kind.deadline.rawValue, "a date, deadline or next meeting time is set"),
        .init(LiveNote.Kind.keyPoint.rawValue, "the speaker stresses something as important, required, or on the exam / homework"),
        .init("none", "none of the above: ordinary talk, a question or an explanation"),
    ]

    public static func liveLine(_ text: String, isMine: Bool, mode: SessionMode) -> (state: String, questions: [String: DecisionQuestion]) {
        let speaker = isMine ? "The listener (me)" : "The other side"
        return ("Setting: \(roleDescription(mode))\n\(speaker) said: 「\(String(text.prefix(400)))」", [
            InsightKey.noteKind: .choice(instructions: "Is this sentence worth keeping as a note?", options: noteOptions),
            InsightKey.wrapUp: .yesNo(instructions: "Is the speaker wrapping up and ending the conversation (thanks for your time, that's all, goodbye, we will get back to you)?"),
        ])
    }

    public struct LineVerdict: Sendable, Equatable {
        public var note: LiveNote.Kind?
        public var noteProbability: Double
        public var wrapUp: Double
    }

    public static func lineVerdict(_ answers: [String: DecisionAnswer]) -> LineVerdict? {
        guard let wrapUp = answers[InsightKey.wrapUp]?.yes else { return nil }
        let choice = answers[InsightKey.noteKind]?.choice
        let probability = choice.flatMap { answers[InsightKey.noteKind]?.probabilities[$0] } ?? answers[InsightKey.noteKind]?.confidence ?? 0
        return LineVerdict(note: choice.flatMap(LiveNote.Kind.init(rawValue:)), noteProbability: probability, wrapUp: wrapUp)
    }

    // MARK: Did the web answer change anything

    public static func answersDiffer(quick: String, web: String) -> (state: String, questions: [String: DecisionQuestion]) {
        ("Answer A (without web search): 「\(String(quick.prefix(450)))」\nAnswer B (with web search): 「\(String(web.prefix(450)))」",
         [InsightKey.differs: .yesNo(instructions: "Does B correct A or add facts that matter (different numbers, names, dates, versions, prices or conclusions)?")])
    }
}
