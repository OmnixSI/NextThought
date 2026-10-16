import CoreGraphics
import Foundation
import InterviewHackerCore
import Observation

/// What the decision model watches besides “is this a question”: the screen, the slide
/// being presented, how much of the answer has been said, notes worth keeping and the
/// end of the conversation. Settings and live state; the logic is in AppState+Insights.
@MainActor
@Observable
final class LiveInsights {
    // MARK: Settings

    /// Reads the screen every few seconds during a session (on-device OCR).
    var screenWatch = UserDefaults.standard.bool(forKey: "insightScreenWatch")
    var screenInterval = UserDefaults.standard.object(forKey: "insightScreenInterval") as? Double ?? 4
    /// Matches the screen against imported decks and shows notes for the current slide.
    var followSlides = UserDefaults.standard.object(forKey: "insightFollowSlides") as? Bool ?? true
    /// Tracks which sentences of the answer (and slide notes) have been said.
    var answerProgress = UserDefaults.standard.object(forKey: "insightAnswerProgress") as? Bool ?? true
    /// Question kind → answer shape, and only the skills that fit.
    var shapeAnswers = UserDefaults.standard.object(forKey: "insightShapeAnswers") as? Bool ?? true
    /// Action items, decisions, deadlines and key points.
    var liveNotesOn = UserDefaults.standard.object(forKey: "insightLiveNotes") as? Bool ?? true
    /// Offers to stop and review when the other side wraps up.
    var wrapUpHint = UserDefaults.standard.object(forKey: "insightWrapUp") as? Bool ?? true
    /// Keeps the quick answer when the web answer changes nothing.
    var webCheck = UserDefaults.standard.object(forKey: "insightWebCheck") as? Bool ?? true

    func save() {
        let defaults = UserDefaults.standard
        defaults.set(screenWatch, forKey: "insightScreenWatch")
        defaults.set(screenInterval, forKey: "insightScreenInterval")
        defaults.set(followSlides, forKey: "insightFollowSlides")
        defaults.set(answerProgress, forKey: "insightAnswerProgress")
        defaults.set(shapeAnswers, forKey: "insightShapeAnswers")
        defaults.set(liveNotesOn, forKey: "insightLiveNotes")
        defaults.set(wrapUpHint, forKey: "insightWrapUp")
        defaults.set(webCheck, forKey: "insightWebCheck")
    }

    // MARK: Screen

    struct ScreenState: Equatable {
        var text: String
        var kind: ScreenKind?
        /// P(relevant to the conversation); from the model, else the overlap rule.
        var relevance: Double
        var capturedAt: Date
        var judgedByModel: Bool
        var isRelevant: Bool { relevance >= 0.5 }
    }

    var screen: ScreenState?
    var screenError: String?
    var isReadingScreen = false
    /// A question or coding problem appeared on screen and has not been answered yet.
    var screenQuestionOffered = false

    // MARK: Slides

    var slide: SlidePage?
    var slideSeenAt: Date?
    var slideNotes: [String: SlideNotes] = [:]
    var generatingNotes: Set<String> = []
    var slideNotesError: String?
    /// Covered talking points per slide key.
    var slideCovered: [String: Set<Int>] = [:]
    @ObservationIgnored var slideSince: Date = .distantPast

    // MARK: Answer progress

    private(set) var answerID: UUID?
    private(set) var answerPoints: [String] = []
    var answerCovered: Set<Int> = []
    @ObservationIgnored private(set) var answerSince: Date = .distantPast
    @ObservationIgnored var coverageRunning = false

    func beginAnswer(id: UUID, text: String, since: Date) {
        answerID = id
        answerPoints = AnswerPoints.split(text)
        answerCovered = []
        answerSince = since
    }

    var nextAnswerPoint: Int? { AnswerPoints.next(after: answerCovered, count: answerPoints.count) }

    // MARK: Notes and wrap-up

    var notes: [LiveNote] = []
    var wrapUpSuggested = false
    @ObservationIgnored var wrapUpDismissedAt: Date?

    func addNote(_ note: LiveNote) {
        // The same commitment repeated (“好，那我下周三前发你”) is kept once.
        guard !notes.suffix(8).contains(where: { $0.kind == note.kind && TextSimilarity.jaccard($0.text, note.text) >= 0.6 }) else { return }
        notes.append(note)
        if notes.count > 80 { notes.removeFirst(notes.count - 80) }
    }

    var notesMarkdown: String {
        LiveNote.Kind.allCases.compactMap { kind in
            let items = notes.filter { $0.kind == kind }
            guard !items.isEmpty else { return nil }
            return "## \(kind.title)\n" + items.map { "- \($0.text)（\($0.isMine ? "我" : "对方")）" }.joined(separator: "\n")
        }.joined(separator: "\n\n")
    }

    // MARK: Answers

    /// Web answers that matched the quick answer (the quick one stays on screen).
    var webConfirmed: Set<UUID> = []
    /// Question kind per answer, shown next to it.
    var questionKinds: [UUID: QuestionKind] = [:]

    // MARK: Side work

    /// Slide notes run one at a time on the background answer clients.
    @ObservationIgnored var sideChain: Task<Void, Never>?

    func resetSession() {
        screen = nil; screenQuestionOffered = false
        slide = nil; slideSeenAt = nil; slideCovered = [:]; slideSince = Date()
        answerID = nil; answerPoints = []; answerCovered = []
        notes = []; wrapUpSuggested = false; wrapUpDismissedAt = nil
        webConfirmed = []; questionKinds = [:]
    }
}
