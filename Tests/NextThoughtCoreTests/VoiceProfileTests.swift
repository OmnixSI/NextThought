import Foundation
import Testing
@testable import NextThoughtCore

/// 我的声音: building the saved voiceprint from several recordings, deciding whether a voice of a
/// meeting is the user, and how "我" shows in the labels. Small hand-made vectors, so every
/// similarity can be worked out by hand.
struct VoiceProfileTests {
    private func clip(_ values: [Float]) -> [Float] { values }

    // MARK: Enrollment

    @Test func averagesConsistentRecordings() throws {
        let result = VoiceEnrollment.make(from: [[1, 0, 0, 0], [0.8, 0.6, 0, 0], [0.8, -0.6, 0, 0]])
        let profile = try result.get()
        #expect(profile.clips == 3)
        #expect(profile.model == VoiceProfile.modelID)
        // The y parts cancel: the average points along the first axis.
        #expect(abs(profile.embedding[0] - 1) < 0.0001)
        #expect(abs(profile.embedding[1]) < 0.0001)
        #expect(profile.isUsable)
    }

    @Test func needsAtLeastThreeRecordings() {
        let result = VoiceEnrollment.make(from: [[1, 0, 0, 0], [1, 0, 0, 0]])
        #expect(result == .failure(.tooFewClips(have: 2, need: 3)))
    }

    @Test func zeroAndMismatchedRecordingsDoNotCount() {
        let result = VoiceEnrollment.make(from: [[1, 0, 0, 0], [0, 0, 0, 0], [1, 0], [1, 0, 0, 0]])
        #expect(result == .failure(.tooFewClips(have: 2, need: 3)))
    }

    @Test func aRecordingOfAnotherVoiceIsDropped() throws {
        // Against the sum (2.6, 0, 1, 0) the stray one scores 1 / 2.786 = 0.36 < 0.5 and goes;
        // the three that stay still make a profile.
        let profile = try VoiceEnrollment.make(from: [[1, 0, 0, 0], [0.8, 0.6, 0, 0], [0.8, -0.6, 0, 0], [0, 0, 1, 0]]).get()
        #expect(profile.clips == 3)
        #expect(abs(profile.embedding[2]) < 0.0001)
    }

    @Test func recordingsThatDoNotSoundAlikeFail() {
        // Five different directions: each is 1 / sqrt(5) = 0.447 < 0.5 to the sum, so none is kept.
        let clips: [[Float]] = (0..<5).map { index in (0..<5).map { $0 == index ? 1 : 0 } }
        #expect(VoiceEnrollment.make(from: clips) == .failure(.inconsistent))
    }

    // MARK: Recognising

    @Test func recognisesAVoiceCloseToTheProfile() {
        let profile = VoiceProfile(embedding: [1, 0, 0, 0], clips: 3, createdAt: Date(timeIntervalSince1970: 0))
        // Only the direction of the cluster sum matters, not its length.
        #expect(profile.recognises(clusterSum: [3, 0, 0, 0]))
        #expect(profile.recognises(clusterSum: [0.6, 0.8, 0, 0]))     // cosine 0.6
        #expect(!profile.recognises(clusterSum: [0.5, 0.866, 0, 0]))  // cosine 0.5 < 0.55
        #expect(!profile.recognises(clusterSum: [0, 0, 0, 0]))        // a merged-away voice
        #expect(!profile.recognises(clusterSum: [1, 0, 0]))           // another model's size
    }

    @Test func aProfileOfAnotherModelIsNotUsed() {
        var profile = VoiceProfile(embedding: [1, 0, 0, 0], clips: 3, createdAt: Date(timeIntervalSince1970: 0))
        profile.model = "older-model"
        #expect(!profile.isUsable)
        #expect(!profile.recognises(clusterSum: [1, 0, 0, 0]))
        #expect(!VoiceProfile(embedding: [0, 0, 0, 0], clips: 3, createdAt: Date(timeIntervalSince1970: 0)).isUsable)
    }

    @Test func profileSurvivesEncoding() throws {
        let profile = VoiceProfile(embedding: [0.5, 0.5, 0.5, 0.5], clips: 4, createdAt: Date(timeIntervalSince1970: 1_700_000_000))
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        #expect(try decoder.decode(VoiceProfile.self, from: encoder.encode(profile)) == profile)
    }

    // MARK: Labels with "我"

    private func line(_ speaker: SessionSpeaker, voice: Int?, _ text: String = "这是一句比较长的话") -> SessionTranscriptLine {
        SessionTranscriptLine(date: Date(timeIntervalSince1970: 0), speaker: speaker, text: text, voice: voice)
    }

    @Test func myVoiceIsLabelledEvenWithFewLines() {
        let lines = [line(.room, voice: 0), line(.room, voice: 1)]
        let labels = SpeakerLabels.promptLabels(lines, voiceNames: nil, meVoices: [0], english: false)
        #expect(labels[lines[0].id] == "我")
        #expect(labels[lines[1].id] == nil)
        #expect(SpeakerLabels.promptLabels(lines, voiceNames: nil, meVoices: [0], english: true)[lines[0].id] == "Me")
    }

    @Test func myVoiceDoesNotCountAsAnotherPerson() {
        // Voice 0 is the user, voice 1 the only other one: a one-to-one stays 对方 / 现场, no "说话人 1".
        let lines = (0..<6).map { _ in line(.room, voice: 0) } + (0..<6).map { _ in line(.room, voice: 1) }
        #expect(SpeakerLabels.voiceNumbers(lines, meVoices: [0]).isEmpty)
        // Without the mark, both are numbered as before.
        #expect(SpeakerLabels.voiceNumbers(lines).count == 2)
    }

    @Test func othersAreStillNumberedAroundMe() {
        let lines = (0..<6).map { _ in line(.room, voice: 0) }
            + (0..<5).map { _ in line(.room, voice: 1) } + (0..<5).map { _ in line(.room, voice: 2) }
        let numbers = SpeakerLabels.voiceNumbers(lines, meVoices: [0])
        #expect(numbers == [1: 1, 2: 2])
        let labels = SpeakerLabels.promptLabels(lines, voiceNames: ["2": "小王"], meVoices: [0], english: false)
        #expect(labels[lines[0].id] == "我")
        #expect(labels[lines[7].id] == "说话人 1")
        #expect(labels[lines[12].id] == "小王")
    }

    @Test func meLinesLeaveTheUsersOwnLinesAlone() {
        let mine = line(.me, voice: nil)
        let recognised = line(.room, voice: 3)
        #expect(SpeakerLabels.meLines([mine, recognised], meVoices: [3]) == [recognised.id])
        #expect(SpeakerLabels.meLines([mine, recognised], meVoices: []).isEmpty)
    }

    @Test func runStartsTreatRecognisedLinesAsMe() {
        let first = line(.me, voice: nil), second = line(.room, voice: 3), third = line(.room, voice: 4)
        let starts = SpeakerLabels.runStarts([first, second, third], [:], mine: [second.id])
        // me, me (same speaker: no new tag), then somebody else.
        #expect(starts == [first.id, third.id])
    }

    @Test func mergingKeepsTheMark() {
        #expect(SpeakerLabels.mergedMe([2], from: 2, into: 5) == [5])
        #expect(SpeakerLabels.mergedMe([5], from: 2, into: 5) == [5])
        #expect(SpeakerLabels.mergedMe([1], from: 2, into: 5) == [1])
        #expect(SpeakerLabels.mergedMe(nil, from: 2, into: 5) == nil)
    }

    @Test func notesPromptMentionsMe() {
        let lines = (0..<6).map { _ in line(.room, voice: 0) }
        let alone = SpeakerLabels.promptNote(lines, voiceNames: nil, meVoices: [0], english: false, inPerson: true)
        #expect(alone?.contains("“我”") == true)
        #expect(SpeakerLabels.promptNote(lines, voiceNames: nil, meVoices: [], english: false, inPerson: true) == nil)
    }

    @Test func oldMetaWithoutTheFieldStillDecodes() throws {
        let meta = SessionMeta(startedAt: Date(timeIntervalSince1970: 1_700_000_000), mode: .interview)
        #expect(meta.meVoices == nil)
    }
}
