import Foundation

/// The first-run guide, in order. Raw values are what `quickStartStep` stores. A feature release
/// restarts the guide (`OnboardingVersion`), and a stored value is always clamped into range.
public enum OnboardingStep: Int, CaseIterable, Sendable {
    /// Pick the occasion the app is for first. The pick becomes the last scene and the first meeting's scene.
    case scene = 0
    /// Online or in the room, system audio, microphone, and the speech model: asked once, here.
    case sound
    /// An answer service. Optional: recording and transcription work without it.
    case answers
    /// What the cue island does in a meeting, then start or look around.
    case start

    public static var count: Int { allCases.count }

    /// A stored step, clamped into range (an old or damaged value opens the guide at a valid page).
    public static func clamped(_ raw: Int) -> OnboardingStep {
        OnboardingStep(rawValue: min(max(raw, 0), count - 1)) ?? .scene
    }

    public var next: OnboardingStep? { OnboardingStep(rawValue: rawValue + 1) }
    public var previous: OnboardingStep? { OnboardingStep(rawValue: rawValue - 1) }
    public var isLast: Bool { next == nil }

    /// 1-based position for "2 / 4".
    public var position: Int { rawValue + 1 }

    /// Steps the person may pass without finishing them.
    public var isSkippable: Bool { self == .answers }

    /// Rough time the step takes, shown next to its title.
    public var estimatedSeconds: Int {
        switch self {
        case .scene: 15
        case .sound: 60
        case .answers: 90
        case .start: 15
        }
    }
}

/// What is still open when the guide was put aside, in the order the home page names it.
public enum OnboardingPending: String, CaseIterable, Sendable {
    case sound, answers

    /// Which areas still need setup. `soundReady` is "a meeting could start now"; `answersReady`
    /// is "an answer service works".
    public static func open(soundReady: Bool, answersReady: Bool) -> [OnboardingPending] {
        var result: [OnboardingPending] = []
        if !soundReady { result.append(.sound) }
        if !answersReady { result.append(.answers) }
        return result
    }
}
