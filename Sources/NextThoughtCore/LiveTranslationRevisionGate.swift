import Foundation

public struct LiveTranslationRevision: Sendable, Equatable {
    public let generation: Int
    public let value: Int

    public init(generation: Int, value: Int) {
        self.generation = generation
        self.value = value
    }
}

/// Tracks changing recognizer snapshots so in-flight translations from a previous
/// utterance cannot overwrite the current one.
public struct LiveTranslationRevisionGate: Sendable {
    public private(set) var generation = 0
    public private(set) var value = 0
    private var displayedValue = 0

    public init() {}

    public var current: LiveTranslationRevision {
        LiveTranslationRevision(generation: generation, value: value)
    }

    @discardableResult
    public mutating func revise() -> LiveTranslationRevision {
        value += 1
        return current
    }

    public mutating func invalidate() {
        generation += 1
        value = 0
        displayedValue = 0
    }

    public func isLatest(_ revision: LiveTranslationRevision) -> Bool {
        revision.generation == generation && revision.value == value
    }

    /// Accepts an intermediate response only if it is newer than what is visible.
    /// This lets a useful snapshot appear while a later one is still translating.
    public mutating func accept(_ revision: LiveTranslationRevision) -> Bool {
        guard revision.generation == generation,
              revision.value > displayedValue,
              revision.value <= value else { return false }
        displayedValue = revision.value
        return true
    }
}

/// A fixed-rate throttle for partial translation requests. Unlike a debounce, new
/// speech updates do not restart the timer, so a long utterance keeps producing work.
public struct LiveTranslationPartialThrottle: Sendable {
    private let interval: TimeInterval
    private var lastDispatch: TimeInterval?

    public init(interval: TimeInterval) {
        self.interval = max(0, interval)
    }

    public mutating func shouldDispatch(at time: TimeInterval) -> Bool {
        guard let lastDispatch else {
            self.lastDispatch = time
            return true
        }
        guard time - lastDispatch >= interval else { return false }
        self.lastDispatch = time
        return true
    }

    public mutating func reset() {
        lastDispatch = nil
    }
}
