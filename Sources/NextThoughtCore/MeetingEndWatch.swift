import Foundation

/// Decides when a meeting nobody ended is over, so a forgotten one doesn't run on for hours and pull
/// the next meeting into its notes.
///
/// Only one signal, on purpose: nothing was recognised for a while. It counts recognised words, not
/// sound, so background music doesn't keep a meeting alive; the user's own words and the other side's
/// both count. It never looks at which meeting app is running, so it works the same online, in a
/// room, with any software. Pure: the app calls `heard`, `keepGoing` and `tick` once a second.
public struct MeetingEndWatch: Equatable, Sendable {
    /// What to do once the silence has lasted `silenceAfter`.
    public enum Mode: String, CaseIterable, Sendable {
        /// Remind, count down, then end (the default): speaking or "继续记录" cancels it.
        case prompt
        /// End straight away.
        case end
        /// Never end by itself.
        case off
    }

    public struct Config: Equatable, Sendable {
        public static let defaultMinutes = 10
        /// What the settings offer.
        public static let minuteChoices = [5, 10, 20, 30]

        public var mode: Mode
        /// How long without recognised words counts as the end. It is also how long a meeting with no
        /// line at all is listened to before it stops quietly: the same time, so the user has one
        /// number to understand (and an empty meeting costs nothing to restart).
        public var silenceAfter: TimeInterval
        /// How long the reminder waits before ending (prompt mode).
        public var countdown: TimeInterval

        public init(mode: Mode = .prompt, silenceAfter: TimeInterval = 600, countdown: TimeInterval = 60) {
            self.mode = mode
            self.silenceAfter = silenceAfter
            self.countdown = countdown
        }

        /// From the two stored settings; anything unknown or missing falls back to the defaults.
        public init(modeRawValue: String?, minutes: Int?) {
            let minutes = minutes.map { $0 > 0 ? min($0, 240) : Self.defaultMinutes } ?? Self.defaultMinutes
            self.init(mode: modeRawValue.flatMap(Mode.init(rawValue:)) ?? .prompt,
                      silenceAfter: TimeInterval(minutes) * 60)
        }
    }

    public enum Reason: Equatable, Sendable {
        /// Words stopped; the meeting ends now (after the reminder, or at once in `end` mode).
        case silence
        /// Nobody said anything at all: there is nothing to keep, so it just stops, with no reminder.
        case nobodySpoke
    }

    /// The reminder is showing: the meeting ends at `deadline` unless someone speaks or keeps it going.
    public struct Pending: Equatable, Sendable {
        public var deadline: Date
        public init(deadline: Date) { self.deadline = deadline }
    }

    public var config: Config
    public private(set) var pending: Pending?
    /// When the current stretch of quiet began: the start, the last words heard, the last "继续记录",
    /// or the last moment the meeting was paused.
    public private(set) var quietSince: Date

    public init(config: Config = Config(), start: Date) {
        self.config = config
        quietSince = start
    }

    /// Words were recognised (mine or theirs): the quiet starts over and a reminder is withdrawn.
    public mutating func heard(at now: Date) {
        quietSince = now
        pending = nil
    }

    /// "继续记录": withdraw the reminder and count the quiet from now.
    public mutating func keepGoing(at now: Date) {
        quietSince = now
        pending = nil
    }

    /// Call about once a second while the meeting runs. `said`: the meeting has at least one line.
    /// `paused`: the meeting is paused, which isn't counted (a reminder showing is withdrawn, and the
    /// quiet counts from the moment it resumes, so keep ticking while paused). Returns why the meeting
    /// should end now, if it should. After returning a reason the quiet restarts, so a stop that
    /// didn't happen doesn't immediately fire again.
    public mutating func tick(at now: Date, said: Bool, paused: Bool = false) -> Reason? {
        if paused || config.mode == .off {
            pending = nil
            quietSince = now
            return nil
        }
        if let pending {
            guard now >= pending.deadline else { return nil }
            self.pending = nil
            quietSince = now
            return .silence
        }
        guard now.timeIntervalSince(quietSince) >= config.silenceAfter else { return nil }
        if !said {
            quietSince = now
            return .nobodySpoke
        }
        switch config.mode {
        case .end:
            quietSince = now
            return .silence
        case .prompt:
            pending = Pending(deadline: now.addingTimeInterval(config.countdown))
            return nil
        case .off:
            return nil
        }
    }

    /// Seconds the reminder has left, if it is showing.
    public func remaining(at now: Date) -> TimeInterval? {
        pending.map { max(0, $0.deadline.timeIntervalSince(now)) }
    }

    /// Where an auto-ended meeting really ended: a little after the last words (not `now`, which is
    /// minutes of silence later), but never before it started or after `now`. Without any words, `now`.
    public static func endTime(lastSpeechEnd: Date?, startedAt: Date, now: Date, padding: TimeInterval = 5) -> Date {
        guard let lastSpeechEnd else { return max(startedAt, now) }
        let candidate = lastSpeechEnd.addingTimeInterval(padding)
        return max(startedAt, min(candidate, now))
    }
}
