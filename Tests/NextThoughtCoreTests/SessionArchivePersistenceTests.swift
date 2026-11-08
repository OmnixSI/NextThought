import Foundation
import Testing
@testable import NextThoughtCore

struct SessionArchivePersistenceTests {
    @Test func jsonLinesReportsMalformedTailAndKeepsValidPrefix() throws {
        let first = SessionTranscriptLine(date: Date(timeIntervalSince1970: 10), speaker: .remote, text: "第一句")
        let valid = try JSONLines.line(first)
        let torn = Data("{\"id\":\"broken".utf8)
        let report = JSONLines.decodeReport(SessionTranscriptLine.self, from: valid + torn)
        #expect(report.records == [first])
        #expect(report.malformedLineNumbers == [2])
        #expect(report.truncatedFinalLine)

        let empty = JSONLines.decodeReport(SessionTranscriptLine.self, from: Data())
        #expect(empty.records.isEmpty)
        #expect(empty.malformedLineNumbers.isEmpty)
        #expect(!empty.truncatedFinalLine)
    }

    @Test func jsonLinesRecoversRecordsAfterMalformedMiddleLineAndLegacyFinalLine() throws {
        let first = SessionTranscriptLine(date: Date(timeIntervalSince1970: 10), speaker: .remote, text: "第一句")
        let last = SessionTranscriptLine(date: Date(timeIntervalSince1970: 20), speaker: .me, text: "最后一句")
        let firstLine = try JSONLines.line(first)
        var lastLine = try JSONLines.line(last)
        lastLine.removeLast() // Older archives can end with a valid line without a newline.
        let bytes = firstLine + Data("{\"torn\n".utf8) + lastLine

        let report = JSONLines.decodeReport(SessionTranscriptLine.self, from: bytes)
        #expect(report.records == [first, last])
        #expect(report.malformedLineNumbers == [2])
        #expect(!report.truncatedFinalLine)
    }

    @Test func failedWriteKeepsQueuedBytesForRetryAndReceipt() throws {
        enum Injected: Error { case diskFull }
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        let url = directory.appendingPathComponent("transcript.jsonl")
        defer { try? FileManager.default.removeItem(at: directory) }
        var queue = SessionArchiveWriteQueue(maximumPendingBytes: 100)
        let result = queue.enqueue(fileName: "transcript.jsonl", data: Data("line\n".utf8))
        guard case .queued(let revision) = result else { Issue.record("expected queued write"); return }

        let failing = SessionArchiveFileAppender { _, _ in throw Injected.diskFull }
        do {
            try failing.append(try #require(queue.entries.first?.data), to: url)
            Issue.record("expected injected write failure")
        } catch Injected.diskFull {
            queue.fail("disk full")
        }
        #expect(queue.snapshot.state == .blocked)
        #expect(queue.snapshot.pendingEvents == 1)
        #expect(queue.snapshot.persistedRevision == 0)

        try SessionArchiveFileAppender.live.append(try #require(queue.entries.first?.data), to: url)
        queue.acknowledge(through: revision, at: Date(timeIntervalSince1970: 100))
        #expect(try Data(contentsOf: url) == Data("line\n".utf8))
        #expect(queue.snapshot.state == .saved)
        #expect(queue.snapshot.pendingBytes == 0)
        #expect(queue.snapshot.persistedRevision == revision)
        #expect(queue.snapshot.lastPersistedAt == Date(timeIntervalSince1970: 100))
    }

    @Test func failedReceiptRetainsEditedNotesAndVoiceEventsForRetry() throws {
        enum Injected: Error { case diskFull }
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        let notesURL = directory.appendingPathComponent("notes.json")
        let voicesURL = directory.appendingPathComponent("voices.jsonl")
        defer { try? FileManager.default.removeItem(at: directory) }

        let correction = TranscriptCorrection(line: 0, original: "缓存击穿", corrected: "缓存击穿")
        var notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "会议",
                                    sections: [], corrections: [correction])
        let lineID = UUID()
        var queue = SessionArchiveWriteQueue()
        guard case .queued = queue.enqueue(fileName: "notes.json", data: try JSONLines.encoder().encode(notes), mode: .replace),
              case .queued = queue.enqueue(fileName: "voices.jsonl", data: try JSONLines.line(SessionVoiceTag(id: lineID, voice: 1))) else {
            Issue.record("initial notes and voice edits should be queued"); return
        }

        let failing = SessionArchiveFileAppender { _, _ in throw Injected.diskFull }
        do {
            try failing.append(try #require(queue.entries.last?.data), to: voicesURL)
            Issue.record("expected injected write failure")
        } catch Injected.diskFull {
            queue.fail("disk full")
        }
        #expect(queue.snapshot.state == .blocked)

        // A further edit starts from the pending notes snapshot and queues the
        // merged voice value instead of losing either change when the disk recovers.
        notes = try JSONLines.decoder().decode(StructuredNotes.self, from: try #require(queue.entries.first?.data))
        notes.corrections[0].reverted = true
        guard case .queued(let lastRevision) = queue.enqueue(fileName: "notes.json", data: try JSONLines.encoder().encode(notes), mode: .replace),
              case .queued(let voiceRevision) = queue.enqueue(fileName: "voices.jsonl", data: try JSONLines.line(SessionVoiceTag(id: lineID, voice: 2))) else {
            Issue.record("retry edits should stay queued"); return
        }

        for entry in queue.entries {
            try SessionArchiveFileAppender.live.write(entry.data,
                to: directory.appendingPathComponent(entry.fileName), mode: entry.mode)
        }
        queue.acknowledge(through: max(lastRevision, voiceRevision))

        let savedNotes = try JSONLines.decoder().decode(StructuredNotes.self, from: Data(contentsOf: notesURL))
        let savedVoices = JSONLines.decode(SessionVoiceTag.self, from: try Data(contentsOf: voicesURL))
        #expect(savedNotes.corrections.first?.reverted == true)
        #expect(savedVoices.last == SessionVoiceTag(id: lineID, voice: 2))
        #expect(queue.snapshot.state == .saved)
    }

    @Test func queueCapacityIsBoundedAndFailureIsNotClearedByEarlierReceipts() {
        var queue = SessionArchiveWriteQueue(maximumPendingBytes: 4)
        guard case .queued(let firstRevision) = queue.enqueue(fileName: "a", data: Data([1, 2, 3])) else {
            Issue.record("first write should fit"); return
        }
        #expect(queue.enqueue(fileName: "b", data: Data([4, 5])) == .full)
        queue.acknowledge(through: firstRevision)
        #expect(queue.snapshot.state == .blocked)
        #expect(queue.snapshot.lastFailure?.contains("队列已满") == true)
        #expect(queue.enqueue(fileName: "c", data: Data([6])) == .full)

        var eventLimited = SessionArchiveWriteQueue(maximumPendingBytes: 100, maximumPendingEvents: 2)
        #expect(eventLimited.enqueue(fileName: "a", data: Data([1])) != .full)
        #expect(eventLimited.enqueue(fileName: "b", data: Data([2])) != .full)
        #expect(eventLimited.enqueue(fileName: "c", data: Data([3])) == .full)
        #expect(eventLimited.snapshot.pendingEvents == 2)
    }

    @Test func receiptOnlyAcknowledgesACompletedPrefixAndDoesNotHidePermanentLoss() {
        var queue = SessionArchiveWriteQueue(maximumPendingBytes: 20)
        guard case .queued(let first) = queue.enqueue(fileName: "a", data: Data([1])) else {
            Issue.record("first write should fit"); return
        }
        guard case .queued(let second) = queue.enqueue(fileName: "b", data: Data([2])) else {
            Issue.record("second write should fit"); return
        }
        queue.fail("disk full")
        queue.acknowledge(through: second + 1)
        #expect(queue.snapshot.persistedRevision == 0)
        #expect(queue.snapshot.pendingEvents == 2)
        queue.acknowledge(through: first)
        #expect(queue.snapshot.persistedRevision == first)
        #expect(queue.snapshot.pendingEvents == 1)
        #expect(queue.snapshot.state == .blocked)
        queue.acknowledge(through: second)
        #expect(queue.snapshot.state == .saved)

        queue.fail("encoding failed", permanent: true)
        guard case .queued(let third) = queue.enqueue(fileName: "c", data: Data([3])) else {
            Issue.record("write should remain queued after permanent failure"); return
        }
        queue.acknowledge(through: third)
        queue.fail("another transient error")
        #expect(queue.snapshot.state == .blocked)
        #expect(queue.snapshot.lastFailure == "encoding failed")
    }

    @Test func liveAppenderSeparatesAnOlderFileWithoutTrailingNewline() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        let url = directory.appendingPathComponent("transcript.jsonl")
        defer { try? FileManager.default.removeItem(at: directory) }
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let first = SessionTranscriptLine(date: Date(timeIntervalSince1970: 10), speaker: .remote, text: "旧会议")
        let second = SessionTranscriptLine(date: Date(timeIntervalSince1970: 20), speaker: .me, text: "新发言")
        var firstLine = try JSONLines.line(first)
        firstLine.removeLast()
        try firstLine.write(to: url)

        try SessionArchiveFileAppender.live.append(try JSONLines.line(second), to: url)

        let report = JSONLines.decodeReport(SessionTranscriptLine.self, from: try Data(contentsOf: url))
        #expect(report.records == [first, second])
        #expect(report.malformedLineNumbers.isEmpty)
    }

    @Test func liveAppenderReplacesSnapshotFilesInsteadOfAppendingJson() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        let url = directory.appendingPathComponent("meta.json")
        defer { try? FileManager.default.removeItem(at: directory) }

        try SessionArchiveFileAppender.live.write(Data("{\"version\":1}".utf8), to: url, mode: .replace)
        try SessionArchiveFileAppender.live.write(Data("{\"version\":2}".utf8), to: url, mode: .replace)

        #expect(try Data(contentsOf: url) == Data("{\"version\":2}".utf8))
    }
}
