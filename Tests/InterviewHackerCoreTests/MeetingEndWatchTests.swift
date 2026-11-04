import Foundation
import Testing
@testable import InterviewHackerCore

/// 会议自动收尾: silence (recognised words only) ends a meeting nobody ended. Times are seconds
/// after `t0`; the defaults are 600 s of silence and a 60 s reminder.
struct MeetingEndWatchTests {
    let t0 = Date(timeIntervalSinceReferenceDate: 1_000_000)

    private func at(_ seconds: TimeInterval) -> Date { t0.addingTimeInterval(seconds) }

    private func watch(_ mode: MeetingEndWatch.Mode = .prompt) -> MeetingEndWatch {
        MeetingEndWatch(config: .init(mode: mode), start: t0)
    }

    @Test func defaultsAreTenMinutesAndOneMinute() {
        let config = MeetingEndWatch.Config()
        #expect(config.mode == .prompt)
        #expect(config.silenceAfter == 600)
        #expect(config.countdown == 60)
    }

    @Test func doesNothingBeforeTheSilenceTime() {
        var w = watch()
        #expect(w.tick(at: at(1), said: true) == nil)
        #expect(w.tick(at: at(599), said: true) == nil)
        #expect(w.pending == nil)
    }

    @Test func silenceStartsTheReminder() {
        var w = watch()
        // 600 s without words: the reminder starts and ends at 600 + 60.
        #expect(w.tick(at: at(600), said: true) == nil)
        #expect(w.pending == MeetingEndWatch.Pending(deadline: at(660)))
        #expect(w.remaining(at: at(630)) == 30)
        #expect(w.tick(at: at(659), said: true) == nil)
        #expect(w.pending != nil)
    }

    @Test func speakingDuringTheCountdownCancelsIt() {
        var w = watch()
        _ = w.tick(at: at(600), said: true)
        w.heard(at: at(630))
        #expect(w.pending == nil)
        // Counting again from 630: nothing at 660 or 1229, the reminder at 1230 (630 + 600).
        #expect(w.tick(at: at(660), said: true) == nil)
        #expect(w.tick(at: at(1229), said: true) == nil)
        #expect(w.pending == nil)
        #expect(w.tick(at: at(1230), said: true) == nil)
        #expect(w.pending == MeetingEndWatch.Pending(deadline: at(1290)))
    }

    @Test func wordsBeforeTheReminderPushItBack() {
        var w = watch()
        w.heard(at: at(300))
        #expect(w.tick(at: at(899), said: true) == nil)
        #expect(w.pending == nil)
        #expect(w.tick(at: at(900), said: true) == nil)
        #expect(w.pending == MeetingEndWatch.Pending(deadline: at(960)))
    }

    @Test func endsWhenTheCountdownRunsOut() {
        var w = watch()
        _ = w.tick(at: at(600), said: true)
        #expect(w.tick(at: at(660), said: true) == .silence)
        #expect(w.pending == nil)
        // A stop that didn't happen doesn't fire again at once: the quiet restarted at 660.
        #expect(w.tick(at: at(661), said: true) == nil)
        #expect(w.pending == nil)
        #expect(w.tick(at: at(1259), said: true) == nil)
    }

    @Test func keepGoingStartsTheCountAgain() {
        var w = watch()
        _ = w.tick(at: at(600), said: true)
        w.keepGoing(at: at(620))
        #expect(w.pending == nil)
        #expect(w.tick(at: at(1219), said: true) == nil)
        #expect(w.pending == nil)
        #expect(w.tick(at: at(1220), said: true) == nil)
        #expect(w.pending == MeetingEndWatch.Pending(deadline: at(1280)))
    }

    @Test func endModeEndsWithoutAReminder() {
        var w = watch(.end)
        #expect(w.tick(at: at(599), said: true) == nil)
        #expect(w.tick(at: at(600), said: true) == .silence)
        #expect(w.pending == nil)
    }

    @Test func offModeNeverEnds() {
        var w = watch(.off)
        #expect(w.tick(at: at(600), said: true) == nil)
        #expect(w.tick(at: at(100_000), said: true) == nil)
        #expect(w.tick(at: at(200_000), said: false) == nil)
        #expect(w.pending == nil)
    }

    @Test func switchingToOffWithdrawsTheReminder() {
        var w = watch()
        _ = w.tick(at: at(600), said: true)
        w.config.mode = .off
        #expect(w.tick(at: at(610), said: true) == nil)
        #expect(w.pending == nil)
    }

    @Test func pausedTimeIsNotCounted() {
        var w = watch()
        // Paused from 100 to 1000: the quiet restarts at the last paused tick.
        #expect(w.tick(at: at(100), said: true, paused: true) == nil)
        #expect(w.tick(at: at(1000), said: true, paused: true) == nil)
        #expect(w.tick(at: at(1001), said: true) == nil)
        #expect(w.tick(at: at(1599), said: true) == nil)
        #expect(w.pending == nil)
        // 1000 + 600.
        #expect(w.tick(at: at(1600), said: true) == nil)
        #expect(w.pending == MeetingEndWatch.Pending(deadline: at(1660)))
    }

    @Test func pausingWithdrawsAShowingReminder() {
        var w = watch()
        _ = w.tick(at: at(600), said: true)
        #expect(w.pending != nil)
        #expect(w.tick(at: at(610), said: true, paused: true) == nil)
        #expect(w.pending == nil)
        // Resumed at 611: not ended at the old deadline 660, and no new reminder before 610 + 600.
        #expect(w.tick(at: at(611), said: true) == nil)
        #expect(w.tick(at: at(700), said: true) == nil)
        #expect(w.pending == nil)
    }

    @Test func nobodySpokeStopsQuietlyWithoutAReminder() {
        var w = watch()
        #expect(w.tick(at: at(599), said: false) == nil)
        #expect(w.pending == nil)
        #expect(w.tick(at: at(600), said: false) == .nobodySpoke)
        #expect(w.pending == nil)
        var direct = watch(.end)
        #expect(direct.tick(at: at(600), said: false) == .nobodySpoke)
    }

    @Test func aMeetingThatNeverSpokeRespectsPause() {
        var w = watch()
        #expect(w.tick(at: at(500), said: false, paused: true) == nil)
        #expect(w.tick(at: at(1000), said: false) == nil)
        #expect(w.tick(at: at(1099), said: false) == nil)
        #expect(w.tick(at: at(1100), said: false) == .nobodySpoke)
    }

    @Test func customTimesAreUsed() {
        var w = MeetingEndWatch(config: .init(mode: .prompt, silenceAfter: 300, countdown: 30), start: t0)
        #expect(w.tick(at: at(299), said: true) == nil)
        #expect(w.tick(at: at(300), said: true) == nil)
        #expect(w.pending == MeetingEndWatch.Pending(deadline: at(330)))
        #expect(w.tick(at: at(330), said: true) == .silence)
    }

    @Test func settingsMapToAConfig() {
        #expect(MeetingEndWatch.Config(modeRawValue: nil, minutes: nil) == MeetingEndWatch.Config())
        #expect(MeetingEndWatch.Config(modeRawValue: "end", minutes: 20) == .init(mode: .end, silenceAfter: 1_200, countdown: 60))
        #expect(MeetingEndWatch.Config(modeRawValue: "off", minutes: 5).mode == .off)
        // Unknown or nonsensical values fall back instead of misfiring.
        #expect(MeetingEndWatch.Config(modeRawValue: "banana", minutes: 0) == MeetingEndWatch.Config())
        #expect(MeetingEndWatch.Config(modeRawValue: "prompt", minutes: -3).silenceAfter == 600)
        #expect(MeetingEndWatch.Config(modeRawValue: "prompt", minutes: 100_000).silenceAfter == 14_400)
        #expect(MeetingEndWatch.Config.minuteChoices.contains(MeetingEndWatch.Config.defaultMinutes))
    }

    // MARK: End time

    @Test func endTimeIsTheLastLineNotNow() {
        // Last words ended at 1000; now is 1700 (after 600 s of quiet and a 60 s reminder): 1000 + 5.
        #expect(MeetingEndWatch.endTime(lastSpeechEnd: at(1000), startedAt: t0, now: at(1700)) == at(1005))
        #expect(MeetingEndWatch.endTime(lastSpeechEnd: at(1000), startedAt: t0, now: at(1700), padding: 0) == at(1000))
    }

    @Test func endTimeStaysInsideTheMeeting() {
        // Never later than now.
        #expect(MeetingEndWatch.endTime(lastSpeechEnd: at(1699), startedAt: t0, now: at(1702)) == at(1702))
        #expect(MeetingEndWatch.endTime(lastSpeechEnd: at(1701), startedAt: t0, now: at(1702)) == at(1702))
        // Never before it started.
        #expect(MeetingEndWatch.endTime(lastSpeechEnd: at(-10), startedAt: t0, now: at(900)) == t0)
        // No words at all: now.
        #expect(MeetingEndWatch.endTime(lastSpeechEnd: nil, startedAt: t0, now: at(900)) == at(900))
    }
}
