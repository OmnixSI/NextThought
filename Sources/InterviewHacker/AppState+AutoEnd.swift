import Foundation
import InterviewHackerCore
import Observation

/// 会议自动收尾: a meeting nobody ended stops by itself after a stretch without recognised words.
/// Deliberately independent of which meeting app is running (online, in a room, any software); the
/// rules are `MeetingEndWatch` in the core, this holds its state for the running meeting.
@MainActor
@Observable
final class MeetingAutoEnd {
    static let modeKey = "meetingAutoEndMode"
    static let minutesKey = "meetingAutoEndMinutes"

    /// While the reminder shows: when the meeting ends unless someone speaks or keeps it going.
    private(set) var deadline: Date?

    @ObservationIgnored private var watch = MeetingEndWatch(start: Date())
    @ObservationIgnored private var sawSpeech = false
    /// Only a running meeting is watched.
    @ObservationIgnored private var active = false
    @ObservationIgnored private var lastSpeechEnd: Date?
    /// The once-a-second tick of the running meeting; nil when no meeting is being watched.
    @ObservationIgnored var timer: Task<Void, Never>?

    func begin(at now: Date = Date()) {
        watch = MeetingEndWatch(config: watch.config, start: now)
        sawSpeech = false
        lastSpeechEnd = nil
        active = true
        setDeadline(nil)
    }

    func stop() {
        active = false
        timer?.cancel()
        timer = nil
        setDeadline(nil)
    }

    /// A final line was recognised (mine or theirs, not suspected echo).
    func heard(text: String, end: Date) {
        guard active, !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }
        sawSpeech = true
        lastSpeechEnd = max(lastSpeechEnd ?? end, end)
        watch.heard(at: Date())
        setDeadline(nil)
    }

    func keepGoing(at now: Date = Date()) {
        watch.keepGoing(at: now)
        setDeadline(nil)
    }

    func tick(config: MeetingEndWatch.Config, paused: Bool, now: Date = Date()) -> MeetingEndWatch.Reason? {
        watch.config = config
        let reason = watch.tick(at: now, said: sawSpeech, paused: paused)
        setDeadline(watch.pending?.deadline)
        return reason
    }

    /// The last words, not "now": the silence at the tail stays out of the record.
    func endTime(startedAt: Date, now: Date = Date()) -> Date {
        MeetingEndWatch.endTime(lastSpeechEnd: lastSpeechEnd, startedAt: startedAt, now: now)
    }

    private func setDeadline(_ value: Date?) {
        if deadline != value { deadline = value }
    }
}

@MainActor
extension AppState {
    /// Read at every tick, so a change in settings applies to the meeting that is running.
    var autoEndConfig: MeetingEndWatch.Config {
        let defaults = UserDefaults.standard
        return MeetingEndWatch.Config(modeRawValue: defaults.string(forKey: MeetingAutoEnd.modeKey),
                                      minutes: defaults.object(forKey: MeetingAutoEnd.minutesKey) as? Int)
    }

    /// At the start of a meeting: reset, then tick every second until `stopAutoEnd`.
    func startAutoEnd() {
        autoEnd.stop()
        autoEnd.begin()
        autoEnd.timer = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(for: .seconds(1))
                guard !Task.isCancelled else { return }
                self?.autoEndTick()
            }
        }
    }

    func stopAutoEnd() { autoEnd.stop() }

    /// Paused time isn't counted (the watch is told, and keeps ticking); a change of state in the
    /// middle of starting or stopping is skipped.
    private func autoEndTick() {
        guard isRunning, !isStopping, !isChangingSession else { return }
        guard let reason = autoEnd.tick(config: autoEndConfig, paused: isPaused) else { return }
        Diagnostics.record("session.auto_end", ["reason": reason == .silence ? "silence" : "nobody_spoke"])
        endMeetingAfterSilence(trimTail: reason == .silence)
    }

    /// Ends the meeting where the talk really ended. Also behind "现在结束" on the reminder.
    func endMeetingAfterSilence(trimTail: Bool = true) {
        let endedAt = trimTail ? autoEnd.endTime(startedAt: sessionStartedAt ?? Date()) : nil
        Task { await self.stopSession(endedAt: endedAt) }
    }

    /// "继续记录" on the reminder.
    func keepRecording() { autoEnd.keepGoing() }
}
