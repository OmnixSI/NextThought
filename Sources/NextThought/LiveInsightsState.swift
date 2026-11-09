import CoreGraphics
import Foundation
import NextThoughtCore
import Observation

/// The screen, the slide being presented and how much of the answer has been said.
/// Settings and live state; the logic (local rules) is in AppState+Insights.
@MainActor
@Observable
final class LiveInsights {
    // MARK: Settings

    /// Reads the screen every few seconds during a session (on-device OCR).
    var screenTarget = UserDefaults.standard.string(forKey: "insightScreenTarget") ?? ""
    var screenRegion = UserDefaults.standard.array(forKey: "insightScreenRegion") as? [Double]
    var screenWatch = UserDefaults.standard.bool(forKey: "insightScreenWatch")
    var screenInterval = UserDefaults.standard.object(forKey: "insightScreenInterval") as? Double ?? 1
    /// Matches the screen against imported decks and shows notes for the current slide.
    var followSlides = UserDefaults.standard.object(forKey: "insightFollowSlides") as? Bool ?? true
    /// Tracks which sentences of the answer (and slide notes) have been said.
    var answerProgress = UserDefaults.standard.object(forKey: "insightAnswerProgress") as? Bool ?? true

    func save() {
        let defaults = UserDefaults.standard
        defaults.set(screenTarget, forKey: "insightScreenTarget")
        if let screenRegion { defaults.set(screenRegion, forKey: "insightScreenRegion") } else { defaults.removeObject(forKey: "insightScreenRegion") }
        defaults.set(screenWatch, forKey: "insightScreenWatch")
        defaults.set(screenInterval, forKey: "insightScreenInterval")
        defaults.set(followSlides, forKey: "insightFollowSlides")
        defaults.set(answerProgress, forKey: "insightAnswerProgress")
    }

    // MARK: Screen

    struct ScreenState: Equatable {
        var text: String
        var kind: ScreenKind?
        /// Relevant to the conversation: the screen text overlaps recent speech.
        var relevance: Double
        var capturedAt: Date
        var isRelevant: Bool { relevance >= 0.5 }
    }

    var screen: ScreenState?
    var screenError: String?
    var isReadingScreen = false

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

    // MARK: Side work

    /// Slide notes run one at a time on the background answer clients.
    @ObservationIgnored var sideChain: Task<Void, Never>?

    func resetSession() {
        screen = nil
        slide = nil; slideSeenAt = nil; slideCovered = [:]; slideSince = Date()
        answerID = nil; answerPoints = []; answerCovered = []
    }
}
