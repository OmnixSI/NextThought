import Foundation

/// The first-run guide, in order. Raw values are what `quickStartStep` stores, so
/// they never change; new steps only go at the end of a guide revision.
public enum OnboardingStep: Int, CaseIterable, Sendable {
    /// Three sentences about the product.
    case welcome = 0
    /// A scripted sample meeting: no audio, no permission, no model.
    case sample
    /// Screen and system audio permission, and the speech recognizer.
    case sound
    /// An API key (or Codex) for answers and notes.
    case answers
    /// Optional.
    case materials
    /// Pick the sound source and start.
    case start

    public static var count: Int { allCases.count }

    /// A stored step, clamped into range (an old or damaged value opens the guide at a valid page).
    public static func clamped(_ raw: Int) -> OnboardingStep {
        OnboardingStep(rawValue: min(max(raw, 0), count - 1)) ?? .welcome
    }

    public var next: OnboardingStep? { OnboardingStep(rawValue: rawValue + 1) }
    public var previous: OnboardingStep? { OnboardingStep(rawValue: rawValue - 1) }
    public var isLast: Bool { next == nil }

    /// 1-based position for "2 / 6".
    public var position: Int { rawValue + 1 }

    /// Steps the person may pass without finishing them.
    public var isSkippable: Bool {
        switch self {
        case .sample, .answers, .materials: true
        case .welcome, .sound, .start: false
        }
    }

    /// Rough time the step takes, shown next to its title.
    public var estimatedSeconds: Int {
        switch self {
        case .welcome: 10
        case .sample: 40
        case .sound: 60
        case .answers: 120
        case .materials: 30
        case .start: 10
        }
    }
}
