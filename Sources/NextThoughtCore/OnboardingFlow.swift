import Foundation

/// The first-run guide, in order. Raw values are what `quickStartStep` stores. A feature release
/// restarts the guide (`OnboardingVersion`), and a stored value is always clamped into range, so
/// inserting a step only moves an interrupted guide of the same release by one page.
public enum OnboardingStep: Int, CaseIterable, Sendable {
    /// Three sentences about the product.
    case welcome = 0
    /// A scripted sample meeting: no audio, no permission, no model.
    case sample
    /// Screen and system audio permission, and the speech recognizer.
    case sound
    /// Reading four sentences once, so the meeting can tell "me" from the others. Optional.
    case voice
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

    /// 1-based position for "2 / 7".
    public var position: Int { rawValue + 1 }

    /// Steps the person may pass without finishing them.
    public var isSkippable: Bool {
        switch self {
        case .sample, .voice, .answers, .materials: true
        case .welcome, .sound, .start: false
        }
    }

    /// Rough time the step takes, shown next to its title.
    public var estimatedSeconds: Int {
        switch self {
        case .welcome: 10
        case .sample: 40
        case .sound: 60
        case .voice: 45
        case .answers: 120
        case .materials: 30
        case .start: 10
        }
    }
}

/// What the guide's materials page can offer from document components already on this Mac
/// (found by the local scan, or installed earlier). Only components that are really ready
/// count: the runtime and every model a feature needs, never a half-present set.
public struct OnboardingMaterialsOffer: Equatable, Sendable {
    public enum Item: String, CaseIterable, Sendable {
        /// Text recognition and page layout for scans, tables and formulas.
        case ocr
        /// Search by meaning, besides keywords.
        case semantic
    }

    public var ocrReady: Bool
    public var semanticReady: Bool

    public init(ocrReady: Bool, semanticReady: Bool) {
        self.ocrReady = ocrReady
        self.semanticReady = semanticReady
    }

    /// Components that are ready to use.
    public var found: [Item] {
        Item.allCases.filter { isReady($0) }
    }

    /// Components that are not on this Mac yet (these can be downloaded).
    public var missing: [Item] {
        Item.allCases.filter { !isReady($0) }
    }

    /// Ready components that are not switched on yet: what "用上这些" would change.
    public func pending(usingOCR: Bool, usingSemantic: Bool) -> [Item] {
        found.filter { $0 == .ocr ? !usingOCR : !usingSemantic }
    }

    public func isReady(_ item: Item) -> Bool {
        switch item {
        case .ocr: ocrReady
        case .semantic: semanticReady
        }
    }
}
