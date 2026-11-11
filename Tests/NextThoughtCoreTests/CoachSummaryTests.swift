import Foundation
import Testing
@testable import NextThoughtCore

@Suite struct CoachSummaryTests {
    private func segment(me: Bool, _ start: Double, _ end: Double, chars: Int = 40) -> SpokenSegment {
        SpokenSegment(isMe: me, start: start, end: end, text: String(repeating: "话", count: chars))
    }

    @Test func tooLittleSpeechGivesNothing() {
        var accumulator = CoachSummaryAccumulator()
        accumulator.ingest(segment(me: true, 0, 5))
        #expect(accumulator.summary().isEmpty)
        #expect(accumulator.summary().pace == nil)
        #expect(accumulator.summary().talkShare == nil)
    }

    @Test func paceAndShareCoverTheWholeMeeting() {
        var accumulator = CoachSummaryAccumulator()
        // 30 s of me at 4 syllables a second, 30 s of them: 240 per minute, half the talking.
        accumulator.ingest(segment(me: true, 0, 30, chars: 120))
        accumulator.ingest(segment(me: false, 30, 60, chars: 120))
        let summary = accumulator.summary()
        #expect(abs((summary.pace ?? 0) - 240) < 0.001)
        #expect(abs((summary.talkShare ?? 0) - 0.5) < 0.001)
        #expect(!summary.isEmpty)
    }

    @Test func longestMonologueKeepsTheMaximumAndNudgesAreCounted() {
        var accumulator = CoachSummaryAccumulator()
        accumulator.observe(CoachSnapshot(monologue: 130))
        accumulator.observe(CoachSnapshot(monologue: 40))
        accumulator.record(.wrapUp); accumulator.record(.wrapUp); accumulator.record(.slowDown)
        accumulator.recordLookUp()
        accumulator.observeEyeContact(0.8); accumulator.observeEyeContact(nil)
        let summary = accumulator.summary()
        #expect(summary.longestMonologue == 130)
        #expect(summary.nudges["wrapUp"] == 2 && summary.nudges["slowDown"] == 1)
        #expect(summary.lookUpCues == 1)
        #expect(summary.eyeContact == 0.8)
    }

    @Test func roundTripsThroughJSON() throws {
        let summary = CoachSummary(pace: 236, talkShare: 0.41, longestMonologue: 130, eyeContact: 0.78, nudges: ["wrapUp": 1], lookUpCues: 2)
        let data = try JSONEncoder().encode(summary)
        #expect(try JSONDecoder().decode(CoachSummary.self, from: data) == summary)
    }

    @Test func cancelledCalibrationLeavesNoFramesBehind() {
        var estimator = GazeEstimator()
        for index in 0..<10 {
            estimator.addCalibration(GazeEstimatorTests.frame(pupil: GazePoint(0.4, -0.3), time: Double(index) * 0.1))
        }
        estimator.cancelCalibration()
        let finished = estimator.finishCalibration()
        #expect(!finished)
    }
}
