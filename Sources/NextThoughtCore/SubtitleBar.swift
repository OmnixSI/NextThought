import Foundation

// MARK: - Window mode

/// What the meeting shows on screen while it is being recorded. The recording never
/// stops: only what is drawn changes. One shortcut cycles through the three.
public enum LiveWindowMode: String, CaseIterable, Codable, Sendable, Equatable, Identifiable {
    /// The live meeting page (transcript, answers, composer): the existing behaviour.
    case notes
    /// A slim floating bar with the latest sentence, only while somebody speaks.
    case subtitles
    /// Neither the page nor the bar. Recording, answers and the answer overlay go on.
    case hidden

    public var id: String { rawValue }

    /// 纪要窗口 → 字幕条 → 隐藏 → 纪要窗口 …
    public func next() -> LiveWindowMode {
        switch self {
        case .notes: .subtitles
        case .subtitles: .hidden
        case .hidden: .notes
        }
    }

    public var title: String {
        switch self {
        case .notes: "纪要窗口"
        case .subtitles: "字幕条"
        case .hidden: "隐藏"
        }
    }
}

// MARK: - When the bar is visible

/// How long the subtitle bar stays up after the last piece of text, and how long it
/// takes to fade out. Pure arithmetic on dates so the rule can be tested.
public struct SubtitleBarTiming: Sendable, Equatable {
    public enum Phase: Sendable, Equatable {
        /// Recent text: fully shown.
        case visible
        /// Nobody has spoken for a while: fading out, the window is still on screen.
        case fading
        /// Gone: the window can be taken off screen.
        case hidden
    }

    public var holdSeconds: TimeInterval
    public var fadeSeconds: TimeInterval

    public init(holdSeconds: TimeInterval = 4, fadeSeconds: TimeInterval = 0.5) {
        self.holdSeconds = max(0, holdSeconds)
        self.fadeSeconds = max(0, fadeSeconds)
    }

    public static let standard = SubtitleBarTiming()

    /// `lastActivity` is the time of the latest text change (a partial included).
    /// nil means nothing was said yet. A date in the future counts as "just now".
    public func phase(now: Date, lastActivity: Date?) -> Phase {
        guard let lastActivity else { return .hidden }
        let elapsed = max(0, now.timeIntervalSince(lastActivity))
        if elapsed <= holdSeconds { return .visible }
        if elapsed < holdSeconds + fadeSeconds { return .fading }
        return .hidden
    }

    /// 1 while visible, then a straight line down to 0 over `fadeSeconds`.
    public func opacity(now: Date, lastActivity: Date?) -> Double {
        guard let lastActivity else { return 0 }
        let elapsed = max(0, now.timeIntervalSince(lastActivity))
        if elapsed <= holdSeconds { return 1 }
        guard fadeSeconds > 0, elapsed < holdSeconds + fadeSeconds else { return 0 }
        return 1 - (elapsed - holdSeconds) / fadeSeconds
    }
}

// MARK: - What the bar says

/// One sentence for the bar: the original, and its translation when there is one.
public struct SubtitleLine: Sendable, Equatable {
    public var speaker: SessionSpeaker
    public var text: String
    public var translation: String
    /// Still being recognized (the recognizer may revise it).
    public var isPartial: Bool

    public init(speaker: SessionSpeaker, text: String, translation: String = "", isPartial: Bool = false) {
        self.speaker = speaker
        self.text = text
        self.translation = translation
        self.isPartial = isPartial
    }
}

/// The half-sentence one audio channel is recognizing right now.
public struct SubtitleChannel: Sendable, Equatable {
    public var speaker: SessionSpeaker
    public var partial: String
    public var partialTranslation: String

    public init(speaker: SessionSpeaker, partial: String, partialTranslation: String = "") {
        self.speaker = speaker
        self.partial = partial
        self.partialTranslation = partialTranslation
    }
}

/// Follows the transcript and decides what the bar shows and when it last had news.
///
/// Rules:
/// - the newest sentence wins: a half-sentence being spoken, otherwise the last
///   finished line;
/// - any change in a half-sentence (or its translation), and any new finished line
///   or its late translation, counts as activity and keeps the bar up;
/// - whatever was already on screen when the feed started is not news.
public struct SubtitleFeed: Sendable {
    public private(set) var line: SubtitleLine?
    public private(set) var lastActivity: Date?

    private var channelSignatures: [String] = []
    private var channelChangedAt: [Date] = []
    private var finalSignature: String?
    private var hasBaseline = false

    public init() {}

    public mutating func update(channels: [SubtitleChannel], lastFinal: SubtitleLine?, now: Date) {
        if channelSignatures.count != channels.count {
            channelSignatures = Array(repeating: "", count: channels.count)
            channelChangedAt = Array(repeating: .distantPast, count: channels.count)
        }

        for (index, channel) in channels.enumerated() {
            let partial = SubtitleText.normalized(channel.partial)
            let translation = SubtitleText.normalized(channel.partialTranslation)
            let signature = partial.isEmpty ? "" : "\(partial)\u{1}\(translation)"
            guard signature != channelSignatures[index] else { continue }
            channelSignatures[index] = signature
            if !partial.isEmpty {
                channelChangedAt[index] = now
                lastActivity = now
            }
        }

        let signature = lastFinal.map { "\($0.speaker.rawValue)\u{1}\($0.text)\u{1}\($0.translation)" }
        // The first look is only a baseline: a line from before the bar existed is not news.
        if hasBaseline, signature != nil, signature != finalSignature { lastActivity = now }
        finalSignature = signature
        hasBaseline = true

        // Newest partial; on a tie the earlier channel (the other side) wins.
        var best: Int?
        for (index, channel) in channels.enumerated() where !SubtitleText.normalized(channel.partial).isEmpty {
            if let current = best, channelChangedAt[index] <= channelChangedAt[current] { continue }
            best = index
        }
        if let best {
            let channel = channels[best]
            line = SubtitleLine(speaker: channel.speaker,
                                text: SubtitleText.normalized(channel.partial),
                                translation: SubtitleText.normalized(channel.partialTranslation),
                                isPartial: true)
        } else if let lastFinal, !SubtitleText.normalized(lastFinal.text).isEmpty {
            var finished = lastFinal
            finished.isPartial = false
            line = finished
        } else {
            line = nil
        }
    }
}

// MARK: - Fitting text

/// What is drawn: the main text (the translation when there is one) and, smaller
/// beneath it, the original.
public struct SubtitleDisplay: Sendable, Equatable {
    public var primary: String
    public var secondary: String
    public var speaker: SessionSpeaker
    public var isPartial: Bool

    public init(primary: String, secondary: String, speaker: SessionSpeaker, isPartial: Bool) {
        self.primary = primary
        self.secondary = secondary
        self.speaker = speaker
        self.isPartial = isPartial
    }
}

public enum SubtitleText {
    /// Newlines and runs of spaces become one space; the ends are trimmed.
    public static func normalized(_ text: String) -> String {
        text.split(whereSeparator: \.isWhitespace).joined(separator: " ")
    }

    /// Width in half-em units: Chinese, Japanese, Korean and emoji are twice as wide
    /// as a Latin letter.
    public static func width(of character: Character) -> Int {
        guard let scalar = character.unicodeScalars.first else { return 1 }
        return isWide(scalar) ? 2 : 1
    }

    public static func units(of text: String) -> Int {
        text.reduce(0) { $0 + width(of: $1) }
    }

    /// The newest part of `text` that fits in `maxUnits` half-em units, with a leading
    /// "…" when the start was dropped. The cut never falls inside a Latin word
    /// (a word longer than the whole budget is cut anyway) and never leaves a comma
    /// or full stop at the start. Chinese can be cut between any two characters.
    public static func tail(_ text: String, maxUnits: Int) -> String {
        let value = normalized(text)
        guard maxUnits > 0, !value.isEmpty else { return "" }
        let characters = Array(value)
        if units(of: value) <= maxUnits { return value }

        // The ellipsis takes one unit.
        let budget = max(1, maxUnits - 1)
        var start = characters.count
        var used = 0
        while start > 0 {
            let columns = width(of: characters[start - 1])
            if used + columns > budget { break }
            used += columns
            start -= 1
        }
        // Even one character did not fit (a wide one into a budget of 1): keep it.
        if start == characters.count { start = characters.count - 1 }

        if start > 0, isWordCharacter(characters[start - 1]), isWordCharacter(characters[start]) {
            var end = start
            while end < characters.count, isWordCharacter(characters[end]) { end += 1 }
            // end == count: the last word alone is longer than the budget; cut it.
            if end < characters.count { start = end }
        }
        while start < characters.count - 1, characters[start].isWhitespace || isClosingPunctuation(characters[start]) {
            start += 1
        }
        return "…" + String(characters[start...])
    }

    /// Picks the text for the bar. With a translation the translation is the main
    /// line and the original goes beneath it; without one only the original shows.
    public static func display(for line: SubtitleLine, primaryUnits: Int, secondaryUnits: Int) -> SubtitleDisplay {
        let original = normalized(line.text)
        let translation = normalized(line.translation)
        if translation.isEmpty {
            return SubtitleDisplay(primary: tail(original, maxUnits: primaryUnits), secondary: "",
                                   speaker: line.speaker, isPartial: line.isPartial)
        }
        return SubtitleDisplay(primary: tail(translation, maxUnits: primaryUnits),
                               secondary: tail(original, maxUnits: secondaryUnits),
                               speaker: line.speaker, isPartial: line.isPartial)
    }

    // MARK: Character classes

    private static func isWide(_ scalar: Unicode.Scalar) -> Bool {
        switch scalar.value {
        case 0x1100...0x115F,       // Hangul Jamo
             0x2E80...0xA4CF,       // CJK radicals, kana, CJK symbols, Han, Yi
             0xAC00...0xD7AF,       // Hangul syllables
             0xF900...0xFAFF,       // CJK compatibility ideographs
             0xFE30...0xFE4F,       // CJK compatibility forms
             0xFF00...0xFF60,       // Fullwidth forms
             0xFFE0...0xFFE6,
             0x1F300...0x1FAFF,     // Emoji and pictographs
             0x20000...0x3FFFF:     // Han extensions
            return true
        default:
            return false
        }
    }

    /// Letters and digits of a Latin-style word, with the apostrophe and hyphen that
    /// join its parts. Wide characters are never word characters: between them,
    /// and next to them, a cut is fine.
    private static func isWordCharacter(_ character: Character) -> Bool {
        guard let scalar = character.unicodeScalars.first, !isWide(scalar) else { return false }
        return character.isLetter || character.isNumber
            || character == "'" || character == "\u{2019}" || character == "-" || character == "_"
    }

    private static let closingPunctuation: Set<Character> = [
        "，", "。", "！", "？", "、", "；", "：", "）", "】", "」", "』", "”", "’", "》", "…",
        ",", ".", "!", "?", ";", ":", ")", "]", "}"
    ]

    private static func isClosingPunctuation(_ character: Character) -> Bool {
        closingPunctuation.contains(character)
    }
}
