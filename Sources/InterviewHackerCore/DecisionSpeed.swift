import Foundation

/// How fast the decision model answers on this Mac, from its recent calls, and how long
/// each use may wait for it. Times are kept as one-question equivalents: each extra question
/// adds about half a one-question call (Laya on an M2 Pro: ~43 ms for one, ~66 for two,
/// ~89 for three, ~135 for five), so mixing one-question background checks with
/// three-question utterance checks would otherwise make the tier swing with whatever ran last. Community numbers come from M3 / M4 Max; a base M1 / M2 can
/// be several times slower, and the answer engine (Codex / API) already takes about a
/// second to its first token, so anything that delays an answer gets a budget scaled to
/// the measured speed and is skipped outright on a slow Mac. Background uses (screen,
/// slides, notes) never block an answer and only get a longer budget.
public struct DecisionSpeed: Sendable, Equatable {
    public enum Tier: String, Sendable {
        /// Median under 15 ms for a one-question call (M3 / M4 Max class).
        case instant
        /// Under 50 ms (Laya on an M2 Pro: ~35-45): fine on the answer path with a tight budget.
        case fast
        /// Under 120 ms: kept off the answer path, background only.
        case slow
        /// Slower still: only the uses that never block (and fewer of them).
        case tooSlow

        public var title: String {
            switch self {
            case .instant: "很快"
            case .fast: "够快"
            case .slow: "偏慢：回答前不再等它，只在后台用"
            case .tooSlow: "太慢：只在后台少量使用"
            }
        }
    }

    /// Where a call sits relative to an answer.
    public enum Use: Sendable {
        /// Runs before the answer starts (question kind, skills): every millisecond is added.
        case beforeAnswer
        /// Confirms a question the rules would answer but are not sure of (score < 0.85):
        /// the answer waits for it, so it gets the tightest budget after `beforeAnswer`.
        case ruleTrigger
        /// Decides whether to answer an utterance the rules were unsure about; a confident
        /// question never waits for it.
        case uncertainQuestion
        /// Never blocks an answer (web-search need, echo, screen, slides, notes).
        case background
    }

    public static let window = 30
    private(set) public var samples: [Double] = []

    public init(samples: [Double] = []) { self.samples = Array(samples.suffix(Self.window)) }

    /// A successful call, in milliseconds, that asked `questions` questions.
    public mutating func record(ms: Double, questions: Int = 1) {
        samples.append(max(0, ms) / Self.cost(questions))
        if samples.count > Self.window { samples.removeFirst(samples.count - Self.window) }
    }

    /// A call that ran past its deadline: counted as the deadline, so a slow Mac is not
    /// mistaken for a fast one just because its slow calls never return in time.
    public mutating func recordMiss(deadlineMs: Double, questions: Int = 1) { record(ms: deadlineMs, questions: questions) }

    public mutating func reset() { samples = [] }

    /// For a one-question call.
    public var p50: Double? { percentile(0.5) }
    public var p90: Double? { percentile(0.9) }

    /// nil until there are a few calls to go on.
    public var tier: Tier? {
        guard samples.count >= 3, let p50 else { return nil }
        switch p50 {
        case ..<15: return .instant
        case ..<50: return .fast
        case ..<120: return .slow
        default: return .tooSlow
        }
    }

    /// Seconds a call asking `questions` questions may wait, or nil to skip the model and
    /// keep the rule. On the answer path a call that would not finish inside the cap is
    /// skipped rather than started: it would only miss, and the miss would make this Mac
    /// look slower than it is.
    public func budget(for use: Use, questions: Int = 1) -> Double? {
        let p90 = (p90 ?? 0) * Self.cost(questions)
        switch (use, tier) {
        case (.beforeAnswer, nil):
            // Unmeasured: a short wait at most.
            return 0.12
        case (.beforeAnswer, .instant?), (.beforeAnswer, .fast?):
            return Self.within(p90 * 1.5 + 25, 60, 150)
        case (.beforeAnswer, _):
            return nil
        case (.ruleTrigger, nil):
            return 0.3
        case (.ruleTrigger, .instant?), (.ruleTrigger, .fast?):
            return Self.within(p90 * 2 + 40, 100, 300)
        case (.ruleTrigger, _):
            return nil
        case (.uncertainQuestion, nil):
            return 0.5
        case (.uncertainQuestion, .instant?), (.uncertainQuestion, .fast?):
            return Self.clamp(p90 * 2.5 + 60, 150, 500) / 1000
        case (.uncertainQuestion, .slow?):
            return Self.clamp(p90 * 2 + 100, 300, 700) / 1000
        case (.uncertainQuestion, .tooSlow?):
            return nil
        case (.background, _):
            return Self.clamp(p90 * 4 + 300, 800, 4000) / 1000
        }
    }

    /// How much longer than usual background polling should wait between calls, so a slow
    /// Mac is not kept busy (and its answers slowed) by screen and slide checks.
    public var backgroundSlowdown: Double {
        switch tier {
        case .slow?: 2
        case .tooSlow?: 4
        default: 1
        }
    }

    private func percentile(_ fraction: Double) -> Double? {
        guard !samples.isEmpty else { return nil }
        let sorted = samples.sorted()
        let index = Int((Double(sorted.count - 1) * fraction).rounded())
        return sorted[min(max(index, 0), sorted.count - 1)]
    }

    /// A call asking `questions` questions, in one-question calls.
    static func cost(_ questions: Int) -> Double { (1 + Double(max(1, questions))) / 2 }

    /// The wait in seconds, at least `low` ms, or nil when it would exceed `high` ms.
    private static func within(_ value: Double, _ low: Double, _ high: Double) -> Double? {
        value > high ? nil : max(value, low) / 1000
    }

    private static func clamp(_ value: Double, _ low: Double, _ high: Double) -> Double { min(max(value, low), high) }
}
