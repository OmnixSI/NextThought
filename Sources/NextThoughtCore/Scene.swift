import Foundation

// MARK: - Scene presets

/// How a suggested answer is laid out on the cue surface next to the camera.
public enum CueStyle: String, Codable, CaseIterable, Sendable, Identifiable {
    /// The conclusion, then the lead-in of every point: enough to glance at.
    case keyPoints
    /// Sentence by sentence, the way it would be said out loud.
    case script
    public var id: String { rawValue }
}

/// What the live coach watches for in a scene. Rates are syllables per minute
/// (`SpeechUnits`): one Chinese character or about two thirds of an English word.
public struct CoachingProfile: Sendable, Equatable {
    /// Speaking without a break for longer than this earns a nudge; nil never nudges.
    public var monologueLimit: TimeInterval?
    /// The share of the talking that suits the scene; nil when it does not matter.
    public var talkShare: ClosedRange<Double>?
    public var pace: ClosedRange<Double>
    /// False in a scene where the user mostly listens: no nudge of any kind.
    public var nudgesPace: Bool

    public init(monologueLimit: TimeInterval?, talkShare: ClosedRange<Double>? = nil,
                pace: ClosedRange<Double> = 150...310, nudgesPace: Bool = true) {
        self.monologueLimit = monologueLimit; self.talkShare = talkShare; self.pace = pace; self.nudgesPace = nudgesPace
    }

    /// A scene where the user mostly listens (a lecture): nothing to coach.
    public static let listening = CoachingProfile(monologueLimit: nil, nudgesPace: false)
}

/// Everything a scene decides beyond `MeetingTypeDefaults`: how cues look, whether
/// they sit beside the camera, and what the coach pays attention to. One table, so a
/// scene is one decision for the user instead of a page of switches.
public struct ScenePreset: Sendable, Equatable {
    public var mode: SessionMode
    public var cueStyle: CueStyle
    /// Cues appear on the eye line (next to the camera) rather than in a free window.
    public var eyeLine: Bool
    public var coaching: CoachingProfile

    public init(mode: SessionMode, cueStyle: CueStyle, eyeLine: Bool, coaching: CoachingProfile) {
        self.mode = mode; self.cueStyle = cueStyle; self.eyeLine = eyeLine; self.coaching = coaching
    }

    public static func `for`(_ mode: SessionMode) -> ScenePreset {
        switch mode {
        case .interview:
            ScenePreset(mode: mode, cueStyle: .keyPoints, eyeLine: true,
                        coaching: CoachingProfile(monologueLimit: 120, talkShare: 0.4...0.78))
        case .academic:
            ScenePreset(mode: mode, cueStyle: .script, eyeLine: true,
                        coaching: CoachingProfile(monologueLimit: nil, pace: 150...290))
        case .labMeeting:
            ScenePreset(mode: mode, cueStyle: .keyPoints, eyeLine: false,
                        coaching: CoachingProfile(monologueLimit: 180))
        case .groupProject:
            ScenePreset(mode: mode, cueStyle: .keyPoints, eyeLine: false,
                        coaching: CoachingProfile(monologueLimit: 120, talkShare: 0.12...0.5))
        case .client:
            ScenePreset(mode: mode, cueStyle: .keyPoints, eyeLine: true,
                        coaching: CoachingProfile(monologueLimit: 90, talkShare: 0.25...0.6))
        case .meeting:
            ScenePreset(mode: mode, cueStyle: .keyPoints, eyeLine: false,
                        coaching: CoachingProfile(monologueLimit: 120))
        case .lesson:
            ScenePreset(mode: mode, cueStyle: .keyPoints, eyeLine: false, coaching: .listening)
        case .discussion:
            ScenePreset(mode: mode, cueStyle: .keyPoints, eyeLine: false,
                        coaching: CoachingProfile(monologueLimit: 90, talkShare: 0.1...0.45))
        case .general, .custom:
            ScenePreset(mode: mode, cueStyle: .keyPoints, eyeLine: false,
                        coaching: CoachingProfile(monologueLimit: 150))
        }
    }
}

// MARK: - Cue script

/// One line on the cue surface.
public struct CueLine: Sendable, Equatable, Identifiable {
    public enum Kind: String, Sendable { case lead, point, detail }
    public var id: Int
    public var kind: Kind
    /// The words to catch first ("Cache Aside"); empty when the line has none.
    public var head: String
    public var text: String

    public init(id: Int, kind: Kind, head: String = "", text: String) {
        self.id = id; self.kind = kind; self.head = head; self.text = text
    }

    /// The whole line as it would be spoken, for matching against speech.
    public var spoken: String { head.isEmpty ? text : (text.isEmpty ? head : head + "，" + text) }
}

/// Turns a Markdown answer into lines short enough to read in a glance.
public enum CueScript {
    public static func lines(from answer: String, style: CueStyle, limit: Int = 14) -> [CueLine] {
        switch style {
        case .script:
            return AnswerPoints.split(answer, limit: limit).enumerated().map { index, sentence in
                CueLine(id: index, kind: index == 0 ? .lead : .detail, text: sentence)
            }
        case .keyPoints:
            return keyPoints(answer, limit: limit)
        }
    }

    private static func keyPoints(_ answer: String, limit: Int) -> [CueLine] {
        var result: [CueLine] = []
        var inCode = false
        var sawLead = false
        for raw in answer.components(separatedBy: .newlines) {
            let trimmed = raw.trimmingCharacters(in: .whitespaces)
            if trimmed.hasPrefix("```") { inCode.toggle(); continue }
            if inCode || trimmed.isEmpty || isTableRule(trimmed) { continue }
            if let body = bulletBody(trimmed) {
                let (head, rest) = splitHead(body)
                result.append(CueLine(id: result.count, kind: .point, head: head, text: rest))
            } else {
                let heading = trimmed.hasPrefix("#")
                let text = AnswerPoints.stripMarkup(String(trimmed.drop { $0 == "#" || $0 == ">" || $0 == " " }))
                guard !text.isEmpty else { continue }
                if heading {
                    result.append(CueLine(id: result.count, kind: .point, head: text, text: ""))
                } else {
                    result.append(CueLine(id: result.count, kind: sawLead ? .detail : .lead, text: text))
                    sawLead = true
                }
            }
            if result.count >= limit { break }
        }
        return result
    }

    private static func isTableRule(_ line: String) -> Bool {
        line.contains("|") && line.allSatisfy { "|-: ".contains($0) }
    }

    /// The text of a list item, or nil when the line is not one.
    static func bulletBody(_ line: String) -> String? {
        for marker in ["- ", "* ", "• ", "· "] where line.hasPrefix(marker) {
            return String(line.dropFirst(marker.count)).trimmingCharacters(in: .whitespaces)
        }
        if let range = line.range(of: #"^\d{1,2}[.)、]\s*"#, options: .regularExpression) {
            return String(line[range.upperBound...]).trimmingCharacters(in: .whitespaces)
        }
        return nil
    }

    /// "**Cache Aside**：写库成功后删除缓存" → ("Cache Aside", "写库成功后删除缓存").
    /// Without bold text, a short lead-in before a colon counts as the head.
    static func splitHead(_ body: String) -> (head: String, rest: String) {
        let separators: Set<Character> = ["：", ":", "—", "–", "，", ","]
        func clean(_ text: Substring) -> String {
            AnswerPoints.stripMarkup(String(text.drop { separators.contains($0) || $0 == " " }))
        }
        if body.hasPrefix("**"), let close = body.dropFirst(2).range(of: "**") {
            let head = body[body.index(body.startIndex, offsetBy: 2)..<close.lowerBound]
            // "**Cache Aside：**写库…" keeps its colon inside the bold run.
            let trimmedHead = AnswerPoints.stripMarkup(String(head)).trimmingCharacters(in: CharacterSet(charactersIn: "：: "))
            return (trimmedHead, clean(body[close.upperBound...]))
        }
        if let colon = body.firstIndex(where: { $0 == "：" || $0 == ":" }) {
            let head = body[..<colon]
            // "https://…" and times ("10:30") are not lead-ins.
            let next = body.index(after: colon)
            let looksLikeValue = next < body.endIndex && (body[next] == "/" || body[next].isNumber)
            if head.count <= 14, !head.isEmpty, !looksLikeValue {
                return (AnswerPoints.stripMarkup(String(head)), clean(body[next...]))
            }
        }
        return ("", AnswerPoints.stripMarkup(body))
    }
}

// MARK: - Speaking coach

/// Speech length that compares across Chinese and English: one unit per Chinese
/// character (a syllable), one and a half per Latin word.
public enum SpeechUnits {
    public static func count(_ text: String) -> Double {
        var units = 0.0
        var inWord = false
        for scalar in text.unicodeScalars {
            let value = scalar.value
            let cjk = (0x4E00...0x9FFF).contains(value) || (0x3400...0x4DBF).contains(value)
                || (0x3040...0x30FF).contains(value) || (0xAC00...0xD7AF).contains(value)
            if cjk {
                units += 1; inWord = false
            } else if CharacterSet.alphanumerics.contains(scalar) {
                if !inWord { units += 1.5; inWord = true }
            } else {
                inWord = false
            }
        }
        return units
    }
}

/// One finished stretch of speech, in seconds since the meeting started.
public struct SpokenSegment: Sendable, Equatable {
    public var isMe: Bool
    public var start: TimeInterval
    public var end: TimeInterval
    public var units: Double

    public init(isMe: Bool, start: TimeInterval, end: TimeInterval, text: String) {
        self.isMe = isMe; self.start = start; self.end = max(end, start); self.units = SpeechUnits.count(text)
    }
}

public enum CoachNudge: String, Sendable, Equatable {
    /// "说了两分钟了，收个尾，把话交回去。"
    case wrapUp
    case slowDown
    /// The user is doing most of the talking in a scene that is about listening.
    case letThemTalk
    /// The user has barely spoken in a scene where they are expected to.
    case speakUp
}

public struct CoachSnapshot: Sendable, Equatable {
    /// Syllables per minute over the recent speech; nil until there is enough of it.
    public var pace: Double?
    /// The user's share of the talking over the recent window; nil until both sides spoke.
    public var talkShare: Double?
    /// How long the user has been talking without the other side cutting in.
    public var monologue: TimeInterval
    public var nudge: CoachNudge?

    public init(pace: Double? = nil, talkShare: Double? = nil, monologue: TimeInterval = 0, nudge: CoachNudge? = nil) {
        self.pace = pace; self.talkShare = talkShare; self.monologue = monologue; self.nudge = nudge
    }
}

/// Pace, share of the talking and monologue length, from finished transcript lines.
/// Pure arithmetic over timestamps: no model, no audio.
public struct SpeakingCoach: Sendable {
    public var profile: CoachingProfile
    public private(set) var segments: [SpokenSegment] = []

    /// Recent speech the pace is measured over, and the window for the talk share.
    static let paceWindow: TimeInterval = 40
    static let shareWindow: TimeInterval = 300
    /// A pause this long (or the other side speaking) ends a monologue.
    static let monologueGap: TimeInterval = 3.5
    /// Nobody has settled into a rhythm yet: "该你说两句了" waits until this much of the meeting has passed.
    static let speakUpGrace: TimeInterval = 180
    /// The user has to have said enough for a share to mean anything.
    static let minimumOwnSpeech: TimeInterval = 10

    public init(profile: CoachingProfile) { self.profile = profile }

    public mutating func ingest(_ segment: SpokenSegment) {
        guard segment.end > segment.start, segment.units > 0 else { return }
        segments.append(segment)
        // Ten minutes of history is more than either window needs.
        if let last = segments.last, segments.count > 400 {
            segments.removeAll { $0.end < last.end - 600 }
        }
    }

    public mutating func reset() { segments.removeAll() }

    public func snapshot(at now: TimeInterval) -> CoachSnapshot {
        let pace = pace(at: now)
        let share = talkShare(at: now)
        let monologue = monologue(at: now)
        var nudge: CoachNudge?
        if let limit = profile.monologueLimit, monologue >= limit {
            nudge = .wrapUp
        } else if profile.nudgesPace, let pace, pace > profile.pace.upperBound {
            nudge = .slowDown
        } else if let share, let range = profile.talkShare {
            if share > range.upperBound {
                nudge = .letThemTalk
            } else if share < range.lowerBound, now >= Self.speakUpGrace {
                nudge = .speakUp
            }
        }
        return CoachSnapshot(pace: pace, talkShare: share, monologue: monologue, nudge: nudge)
    }

    func pace(at now: TimeInterval) -> Double? {
        let recent = segments.filter { $0.isMe && $0.end >= now - Self.paceWindow }
        let duration = recent.reduce(0) { $0 + ($1.end - $1.start) }
        guard duration >= 6 else { return nil }
        return recent.reduce(0) { $0 + $1.units } / duration * 60
    }

    func talkShare(at now: TimeInterval) -> Double? {
        let recent = segments.filter { $0.end >= now - Self.shareWindow }
        let mine = recent.filter(\.isMe).reduce(0) { $0 + ($1.end - $1.start) }
        let theirs = recent.filter { !$0.isMe }.reduce(0) { $0 + ($1.end - $1.start) }
        guard mine >= Self.minimumOwnSpeech, theirs > 0, mine + theirs >= 45 else { return nil }
        return mine / (mine + theirs)
    }

    func monologue(at now: TimeInterval) -> TimeInterval {
        guard let last = segments.last, last.isMe, now - last.end <= Self.monologueGap else { return 0 }
        var start = last.start
        var previousStart = last.start
        for segment in segments.dropLast().reversed() {
            guard segment.isMe, previousStart - segment.end <= Self.monologueGap else { break }
            start = segment.start
            previousStart = segment.start
        }
        return max(0, last.end - start)
    }
}
