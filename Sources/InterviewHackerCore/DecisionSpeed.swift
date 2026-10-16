import Foundation

/// How fast the decision model answers on this Mac, from its recent calls, and how long
/// each use may wait for it. Community numbers come from M3 / M4 Max; a base M1 / M2 can
/// be several times slower, and the answer engine (Codex / API) already takes about a
/// second to its first token, so anything that delays an answer gets a budget scaled to
/// the measured speed and is skipped outright on a slow Mac. Background uses (screen,
/// slides, notes) never block an answer and only get a longer budget.
public struct DecisionSpeed: Sendable, Equatable {
    public enum Tier: String, Sendable {
        /// P50 under 30 ms (M-series Max / Pro class).
        case instant
        /// Under 80 ms: fine on the answer path with a tight budget.
        case fast
        /// Under 200 ms: kept off the answer path, background only.
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

    /// A successful call, in milliseconds.
    public mutating func record(ms: Double) {
        samples.append(max(0, ms))
        if samples.count > Self.window { samples.removeFirst(samples.count - Self.window) }
    }

    /// A call that ran past its deadline: counted as the deadline, so a slow Mac is not
    /// mistaken for a fast one just because its slow calls never return in time.
    public mutating func recordMiss(deadlineMs: Double) { record(ms: deadlineMs) }

    public mutating func reset() { samples = [] }

    public var p50: Double? { percentile(0.5) }
    public var p90: Double? { percentile(0.9) }

    /// nil until there are a few calls to go on.
    public var tier: Tier? {
        guard samples.count >= 3, let p50 else { return nil }
        switch p50 {
        case ..<30: return .instant
        case ..<80: return .fast
        case ..<200: return .slow
        default: return .tooSlow
        }
    }

    /// Seconds this use may wait, or nil to skip the model and keep the rule.
    public func budget(for use: Use) -> Double? {
        let p90 = p90 ?? 0
        switch (use, tier) {
        case (.beforeAnswer, nil):
            // Unmeasured: a short wait at most.
            return 0.12
        case (.beforeAnswer, .instant?), (.beforeAnswer, .fast?):
            return Self.clamp(p90 * 1.5 + 25, 60, 150) / 1000
        case (.beforeAnswer, _):
            return nil
        case (.ruleTrigger, nil):
            return 0.3
        case (.ruleTrigger, .instant?), (.ruleTrigger, .fast?):
            return Self.clamp(p90 * 2 + 40, 100, 300) / 1000
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

    private static func clamp(_ value: Double, _ low: Double, _ high: Double) -> Double { min(max(value, low), high) }
}
