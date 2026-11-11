import Foundation

/// What the live coach measured over a whole meeting, kept next to its notes.
public struct CoachSummary: Codable, Sendable, Equatable {
    /// Syllables per minute over everything the user said; nil when too little was said.
    public var pace: Double?
    /// The user's share of the talking, 0...1; nil until both sides spoke long enough.
    public var talkShare: Double?
    /// The longest stretch the user talked without the other side or a pause.
    public var longestMonologue: TimeInterval
    /// Share of the user's speaking time spent looking at the lens; nil without the camera.
    public var eyeContact: Double?
    /// How often each nudge (`CoachNudge.rawValue`) was shown.
    public var nudges: [String: Int]
    /// How often "看镜头" was shown.
    public var lookUpCues: Int

    public init(pace: Double? = nil, talkShare: Double? = nil, longestMonologue: TimeInterval = 0,
                eyeContact: Double? = nil, nudges: [String: Int] = [:], lookUpCues: Int = 0) {
        self.pace = pace; self.talkShare = talkShare; self.longestMonologue = longestMonologue
        self.eyeContact = eyeContact; self.nudges = nudges; self.lookUpCues = lookUpCues
    }

    /// Nothing worth showing: no pace, share, long monologue or eye contact.
    public var isEmpty: Bool {
        pace == nil && talkShare == nil && eyeContact == nil && longestMonologue < CoachSummaryAccumulator.minimumMonologue
    }
}

/// Folds a meeting's transcript segments and coach snapshots into a `CoachSummary`.
public struct CoachSummaryAccumulator: Sendable, Equatable {
    /// A monologue shorter than this is just talking.
    public static let minimumMonologue: TimeInterval = 30
    static let minimumSpeaking: TimeInterval = 20
    static let minimumTotal: TimeInterval = 45

    private var myDuration: TimeInterval = 0
    private var theirDuration: TimeInterval = 0
    private var myUnits = 0.0
    private var longest: TimeInterval = 0
    private var contact: Double?
    private var nudges: [String: Int] = [:]
    private var lookUps = 0

    public init() {}

    public mutating func ingest(_ segment: SpokenSegment) {
        guard segment.end > segment.start, segment.units > 0 else { return }
        let length = segment.end - segment.start
        if segment.isMe { myDuration += length; myUnits += segment.units } else { theirDuration += length }
    }

    public mutating func observe(_ snapshot: CoachSnapshot) {
        longest = max(longest, snapshot.monologue)
    }

    public mutating func record(_ nudge: CoachNudge) { nudges[nudge.rawValue, default: 0] += 1 }
    public mutating func recordLookUp() { lookUps += 1 }

    /// The camera's running ratio; the last one is the meeting's.
    public mutating func observeEyeContact(_ ratio: Double?) { if let ratio { contact = ratio } }

    public func summary() -> CoachSummary {
        CoachSummary(
            pace: myDuration >= Self.minimumSpeaking ? myUnits / myDuration * 60 : nil,
            talkShare: myDuration + theirDuration >= Self.minimumTotal ? myDuration / (myDuration + theirDuration) : nil,
            longestMonologue: longest, eyeContact: contact, nudges: nudges, lookUpCues: lookUps)
    }
}
