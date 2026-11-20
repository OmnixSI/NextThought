import Foundation
import Testing
@testable import NextThoughtCore

/// 区分对方的每个人: the voice grouping rules, the labels the transcript shows, and the
/// backward-compatible fields they add to the session records. Vectors are small hand-made
/// ones, so every similarity below can be worked out by hand.
struct SpeakerClusteringTests {
    // Unit-length axes standing in for voiceprints of three different people.
    let a: [Float] = [1, 0, 0, 0]
    let b: [Float] = [0, 1, 0, 0]
    let c: [Float] = [0, 0, 1, 0]

    // MARK: VoiceClusterer

    @Test func thresholdsAreCicadasTunedValues() {
        #expect(VoiceClusterer.join == 0.5)
        #expect(VoiceClusterer.startNew == 0.5)
        #expect(VoiceClusterer.newSeconds == 2.0)
    }

    @Test func sameVoiceJoinsEvenWhenShort() {
        var clusterer = VoiceClusterer()
        #expect(clusterer.label(a, seconds: 3) == 0)
        // cosine to [1,0,0,0] is 0.99 / 1 = 0.99 >= 0.5; a short line may join, it just may not start a voice.
        #expect(clusterer.label([0.99, 0.1, 0, 0], seconds: 0.6) == 0)
        #expect(clusterer.voiceCount == 1)
        #expect(abs(clusterer.sums[0][0] - 1.99) < 0.0001)
    }

    @Test func differentVoiceStartsANewOne() {
        var clusterer = VoiceClusterer()
        #expect(clusterer.label(a, seconds: 3) == 0)
        // b is orthogonal to a: similarity 0 < 0.5 and the line lasts 3 s >= 2 s.
        #expect(clusterer.label(b, seconds: 3) == 1)
        // Later lines go to the closest of the two.
        #expect(clusterer.label([0.1, 0.99, 0, 0], seconds: 1) == 1)
        #expect(clusterer.label([0.99, 0.1, 0, 0], seconds: 1) == 0)
        #expect(clusterer.voiceCount == 2)
    }

    @Test func aShortLineNeverStartsAVoice() {
        var clusterer = VoiceClusterer()
        // Nothing to join and under 2 s: stays unlabelled, no voice is created.
        #expect(clusterer.label(a, seconds: 1.0) == nil)
        #expect(clusterer.voiceCount == 0)
        #expect(clusterer.label(a, seconds: 2.0) == 0)
        // 1.9 s of an unlike voice: unlabelled again.
        #expect(clusterer.label(b, seconds: 1.9) == nil)
        #expect(clusterer.voiceCount == 1)
    }

    @Test func similarityAroundTheThreshold() {
        var clusterer = VoiceClusterer()
        _ = clusterer.label(a, seconds: 3)
        // [0.6, 0.8, 0, 0] has cosine 0.6 to a: joins. [0.4, 0.9165, 0, 0] has 0.4: starts a new voice.
        #expect(clusterer.label([0.6, 0.8, 0, 0], seconds: 3) == 0)
        var fresh = VoiceClusterer()
        _ = fresh.label(a, seconds: 3)
        #expect(fresh.label([0.4, 0.9165, 0, 0], seconds: 3) == 1)
    }

    @Test func mergeSendsLaterLinesToTheKeptVoice() {
        var clusterer = VoiceClusterer()
        #expect(clusterer.label(a, seconds: 3) == 0)
        #expect(clusterer.label(b, seconds: 3) == 1)
        clusterer.merge(1, into: 0)
        #expect(clusterer.sums[1] == [0, 0, 0, 0])
        #expect(clusterer.sums[0] == [1, 1, 0, 0])
        // b against [1,1,0,0]: dot 1 / sqrt(2) = 0.707 >= 0.5 -> voice 0, never the emptied voice 1.
        #expect(clusterer.label(b, seconds: 1) == 0)
        #expect(clusterer.sums[0] == [1, 2, 0, 0])
        // a against [1,2,0,0] is only 0.447 (< 0.5): too weak to join, too short to start a voice.
        #expect(clusterer.label(a, seconds: 1) == nil)
        // [0.8, 0.6, 0, 0] against [1,2,0,0]: (0.8 + 1.2) / sqrt(5) = 0.894 -> joins voice 0.
        #expect(clusterer.label([0.8, 0.6, 0, 0], seconds: 1) == 0)
        // A third person gets the next id: ids are never reused.
        #expect(clusterer.label(c, seconds: 3) == 2)
    }

    @Test func mergeIgnoresNonsense() {
        var clusterer = VoiceClusterer()
        _ = clusterer.label(a, seconds: 3)
        _ = clusterer.label(b, seconds: 3)
        let before = clusterer.sums
        clusterer.merge(0, into: 0)
        clusterer.merge(5, into: 0)
        clusterer.merge(0, into: -1)
        #expect(clusterer.sums == before)
    }

    // MARK: SpeakerLabels

    private func line(_ speaker: SessionSpeaker, voice: Int?, _ text: String = "这是一句比较长的话") -> SessionTranscriptLine {
        SessionTranscriptLine(date: Date(timeIntervalSince1970: 0), speaker: speaker, text: text, voice: voice)
    }

    @Test func substantiveMeansWordsOrCharacters() {
        #expect(SpeakerLabels.substantive("I think so"))
        #expect(SpeakerLabels.substantive("我觉得可以"))
        #expect(!SpeakerLabels.substantive("Yeah."))
        #expect(!SpeakerLabels.substantive("ok sure"))
        #expect(!SpeakerLabels.substantive("好的"))
        #expect(!SpeakerLabels.substantive("..."))
        #expect(!SpeakerLabels.substantive("1 2 3"))   // no letters: not words
    }

    @Test func numbersFollowTheOrderVoicesReachFiveLines() {
        // Voice 3 speaks first but voice 7 reaches five lines first (index 6 vs index 9).
        var lines: [SessionTranscriptLine] = []
        for _ in 0..<2 { lines.append(line(.remote, voice: 3)) }
        for _ in 0..<5 { lines.append(line(.remote, voice: 7)) }
        for _ in 0..<3 { lines.append(line(.remote, voice: 3)) }
        #expect(SpeakerLabels.voiceNumbers(lines) == [7: 1, 3: 2])
        // A later line never renumbers anyone.
        lines.append(line(.remote, voice: 9))
        for _ in 0..<5 { lines.append(line(.remote, voice: 9)) }
        #expect(SpeakerLabels.voiceNumbers(lines) == [7: 1, 3: 2, 9: 3])
        #expect(SpeakerLabels.shownCount(lines) == 3)
    }

    @Test func aSingleVoiceShowsNoNumbers() {
        let lines = (0..<8).map { _ in line(.remote, voice: 0) }
        #expect(SpeakerLabels.voiceNumbers(lines).isEmpty)
        #expect(SpeakerLabels.numbers(lines).isEmpty)
        // Two voices, but the second has only four lines: still one on show, so nothing shows.
        let other = lines + (0..<4).map { _ in line(.remote, voice: 1) }
        #expect(SpeakerLabels.voiceNumbers(other).isEmpty)
    }

    @Test func shortLinesAndMyLinesDoNotCount() {
        var lines: [SessionTranscriptLine] = (0..<5).map { _ in line(.remote, voice: 0) }
        lines += (0..<9).map { _ in line(.remote, voice: 1, "嗯") }          // not substantive
        lines += (0..<9).map { _ in line(.me, voice: 2) }                      // the user is never told apart
        #expect(SpeakerLabels.voiceNumbers(lines).isEmpty)
        lines += (0..<5).map { _ in line(.remote, voice: 1) }
        #expect(SpeakerLabels.voiceNumbers(lines) == [0: 1, 1: 2])
    }

    @Test func theRoomIsToldApartToo() {
        var lines = (0..<5).map { _ in line(.room, voice: 4) }
        lines += (0..<5).map { _ in line(.room, voice: 1) }
        #expect(SpeakerLabels.voiceNumbers(lines) == [4: 1, 1: 2])
    }

    @Test func namesAndLabels() {
        var lines = (0..<5).map { _ in line(.remote, voice: 7) }
        lines += (0..<5).map { _ in line(.remote, voice: 3) }
        #expect(SpeakerLabels.names(["7": "小王", "3": ""], lines: lines) == [1: "小王"])
        #expect(SpeakerLabels.names(nil, lines: lines).isEmpty)
        #expect(SpeakerLabels.label(1, [1: "小王"]) == "小王")
        #expect(SpeakerLabels.label(2, [1: "小王"]) == "说话人 2")
        #expect(SpeakerLabels.label(2, [:], english: true) == "Speaker 2")
    }

    @Test func runStartsMarkWhereTheSpeakerChanges() {
        let mine = line(.me, voice: nil)
        var lines = (0..<5).map { _ in line(.remote, voice: 7) }
        lines += (0..<5).map { _ in line(.remote, voice: 3) }
        lines.insert(mine, at: 2)
        let numbers = SpeakerLabels.numbers(lines)
        let starts = SpeakerLabels.runStarts(lines, numbers)
        // Indices: 0,1 speaker 1 | 2 me | 3,4,5 speaker 1 | 6...10 speaker 2.
        #expect(starts == [lines[0].id, lines[2].id, lines[3].id, lines[6].id])
    }

    @Test func notesLabelsUseNamesThenNumbers() {
        var lines = (0..<5).map { _ in line(.remote, voice: 7) }
        lines += (0..<5).map { _ in line(.remote, voice: 3) }
        lines.append(line(.remote, voice: nil))
        lines.append(line(.me, voice: nil))
        let labels = SpeakerLabels.promptLabels(lines, voiceNames: ["7": "  小王\n"], english: false)
        #expect(labels[lines[0].id] == "小王")
        #expect(labels[lines[5].id] == "说话人 2")
        #expect(labels[lines[10].id] == nil)    // not on show: keeps 对方
        #expect(labels[lines[11].id] == nil)    // me
        #expect(SpeakerLabels.promptLabels(lines, voiceNames: nil, english: true)[lines[5].id] == "Speaker 2")

        let note = SpeakerLabels.promptNote(lines, voiceNames: ["7": "小王"], english: false, inPerson: false)
        #expect(note?.contains("小王、说话人 2") == true)
        #expect(note?.contains("“对方”") == true)
        let room = SpeakerLabels.promptNote(lines, voiceNames: nil, english: false, inPerson: true)
        #expect(room?.contains("“现场”") == true)
        #expect(SpeakerLabels.promptNote([line(.remote, voice: 0)], voiceNames: nil, english: false, inPerson: false) == nil)
    }

    @Test func transcriptTextCarriesTheLabels() {
        let start = Date(timeIntervalSince1970: 0)
        let first = SessionTranscriptLine(date: start.addingTimeInterval(65), speaker: .remote, text: "周四交", voice: 1)
        let mine = SessionTranscriptLine(date: start.addingTimeInterval(70), speaker: .me, text: "好")
        let text = NotesPrompt.transcript([first, mine], start: start, speakerLabels: [first.id: "小王"])
        #expect(text == "[01:05] 小王：周四交\n[01:10] 我：好")
        // Without labels the old format is unchanged.
        #expect(NotesPrompt.transcript([first, mine], start: start) == "[01:05] 对方：周四交\n[01:10] 我：好")
    }

    @Test func cleanNameAndMerge() {
        #expect(SpeakerLabels.cleanName("  小王 ") == "小王")
        #expect(SpeakerLabels.cleanName("a\nb") == "a b")
        #expect(SpeakerLabels.cleanName("   ") == nil)
        #expect(SpeakerLabels.cleanName(String(repeating: "x", count: 40))?.count == 24)
        // Merging 3 into 7: 3's name moves over only when 7 has none.
        #expect(SpeakerLabels.merged(["3": "小李"], from: 3, into: 7) == ["7": "小李"])
        #expect(SpeakerLabels.merged(["3": "小李", "7": "小王"], from: 3, into: 7) == ["7": "小王"])
        #expect(SpeakerLabels.merged(nil, from: 3, into: 7) == nil)
    }

    // MARK: Records stay readable

    @Test func oldRecordsDecodeWithoutVoice() throws {
        let oldLine = #"{"id":"6F9619FF-8B86-D011-B42D-00C04FC964FF","date":"2026-10-26T10:00:00Z","speaker":"remote","text":"你好"}"#
        let decoded = try JSONLines.decoder().decode(SessionTranscriptLine.self, from: Data(oldLine.utf8))
        #expect(decoded.voice == nil && decoded.text == "你好")

        let withVoice = SessionTranscriptLine(date: Date(timeIntervalSince1970: 1_700_000_000), speaker: .room, text: "好", voice: 2)
        let data = try JSONLines.line(withVoice)
        #expect(JSONLines.decode(SessionTranscriptLine.self, from: data) == [withVoice])
        // A line without a voice writes no key at all, so old readers see what they always saw.
        let plain = try JSONLines.line(SessionTranscriptLine(date: Date(timeIntervalSince1970: 1_700_000_000), speaker: .me, text: "x"))
        #expect(!String(decoding: plain, as: UTF8.self).contains("voice"))
    }

    @Test func metaVoiceNamesAreOptional() throws {
        var meta = SessionMeta(startedAt: Date(timeIntervalSince1970: 1_700_000_000), mode: .meeting)
        let encoded = try JSONLines.encoder().encode(meta)
        #expect(!String(decoding: encoded, as: UTF8.self).contains("voiceNames"))
        #expect(try JSONLines.decoder().decode(SessionMeta.self, from: encoded).voiceNames == nil)
        meta.voiceNames = ["1": "小王"]
        let roundTrip = try JSONLines.decoder().decode(SessionMeta.self, from: try JSONLines.encoder().encode(meta))
        #expect(roundTrip.voiceNames == ["1": "小王"])
    }

    @Test func voiceTagsRoundTripAsJSONLines() throws {
        let tags = [SessionVoiceTag(id: UUID(), voice: 0), SessionVoiceTag(id: UUID(), voice: 4)]
        var data = Data()
        for tag in tags { data.append(try JSONLines.line(tag)) }
        #expect(JSONLines.decode(SessionVoiceTag.self, from: data) == tags)
    }

    @Test func aPersonNamedByHandIsShownHoweverLittleTheySaid() {
        func line(_ voice: Int, _ text: String) -> SessionTranscriptLine {
            SessionTranscriptLine(id: UUID(), date: Date(), speaker: .room, text: text, voice: voice)
        }
        // One voice does all the talking; two others say a sentence each.
        var lines = (0..<8).map { _ in line(1, "这一页讲的是缓存一致性的方案") }
        lines.insert(line(4, "我有一个问题想问一下"), at: 3)
        lines.append(line(7, "好的谢谢老师的解释"))
        #expect(SpeakerLabels.voiceNumbers(lines).isEmpty)
        // Naming the quiet ones puts them on show, after the one who earned a number.
        #expect(SpeakerLabels.voiceNumbers(lines, named: [7, 4]) == [1: 1, 4: 2, 7: 3])
        #expect(SpeakerLabels.names(["4": "小王"], lines: lines) == [2: "小王"])
        #expect(SpeakerLabels.numbers(lines, named: [4]).count == 9)
        #expect(SpeakerLabels.newVoice(lines, voiceNames: ["9": "x"], meVoices: [12]) == 13)
    }

    @Test func numbersEarnedByTalkingDoNotMoveWhenSomeoneIsNamed() {
        func line(_ voice: Int) -> SessionTranscriptLine {
            SessionTranscriptLine(id: UUID(), date: Date(), speaker: .remote, text: "这是一句足够长的话用来计数", voice: voice)
        }
        let lines = [line(9)] + (0..<5).map { _ in line(3) } + (0..<5).map { _ in line(5) }
        #expect(SpeakerLabels.voiceNumbers(lines) == [3: 1, 5: 2])
        #expect(SpeakerLabels.voiceNumbers(lines, named: [9, 5]) == [3: 1, 5: 2, 9: 3])
    }
}
