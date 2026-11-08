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

    @Test func midWriteFailureRollsBackToAcknowledgedPrefixAndRetrySavesOnce() throws {
        enum Injected: Error { case midWrite }
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        let url = directory.appendingPathComponent("transcript.jsonl")
        defer { try? FileManager.default.removeItem(at: directory) }

        let earlier = SessionTranscriptLine(date: Date(timeIntervalSince1970: 10), speaker: .remote, text: "已保存记录")
        let pending = SessionTranscriptLine(date: Date(timeIntervalSince1970: 20), speaker: .me, text: "待重试记录")
        let earlierLine = try JSONLines.line(earlier)
        let pendingLine = try JSONLines.line(pending)
        var queue = SessionArchiveWriteQueue()
        guard case .queued(let earlierRevision) = queue.enqueue(fileName: "transcript.jsonl", data: earlierLine) else {
            Issue.record("earlier record should be queued"); return
        }
        try SessionArchiveFileAppender.live.append(try #require(queue.entries.first?.data), to: url)
        queue.acknowledge(through: earlierRevision)

        guard case .queued(let pendingRevision) = queue.enqueue(fileName: "transcript.jsonl", data: pendingLine) else {
            Issue.record("pending record should be queued"); return
        }
        let partialWriter = SessionArchiveFileAppender.usingFileHandleAppend { data, handle in
            let partialCount = max(1, data.count / 2)
            try handle.write(contentsOf: Data(data.prefix(partialCount)))
            throw Injected.midWrite
        }
        do {
            try partialWriter.append(try #require(queue.entries.first?.data), to: url)
            Issue.record("expected injected mid-write failure")
        } catch Injected.midWrite {
            queue.fail("mid-write failure")
        }

        #expect(try Data(contentsOf: url) == earlierLine)
        let afterFailure = JSONLines.decodeReport(SessionTranscriptLine.self, from: try Data(contentsOf: url))
        #expect(afterFailure.records == [earlier])
        #expect(afterFailure.malformedLineNumbers.isEmpty)
        #expect(queue.snapshot.persistedRevision == earlierRevision)
        #expect(queue.snapshot.pendingEvents == 1)
        #expect(queue.snapshot.state == .blocked)

        try SessionArchiveFileAppender.live.append(try #require(queue.entries.first?.data), to: url)
        queue.acknowledge(through: pendingRevision)

        let savedBytes = try Data(contentsOf: url)
        let report = JSONLines.decodeReport(SessionTranscriptLine.self, from: savedBytes)
        #expect(savedBytes == earlierLine + pendingLine)
        #expect(report.records == [earlier, pending])
        #expect(report.malformedLineNumbers.isEmpty)
        #expect(queue.snapshot.state == .saved)
        #expect(queue.snapshot.pendingEvents == 0)
        #expect(queue.snapshot.persistedRevision == pendingRevision)
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

    @Test func overflowRecoveryKeepsLossVisibleAndReopensFutureWritesOnlyAfterAcknowledgement() {
        var queue = SessionArchiveWriteQueue(maximumPendingBytes: 4)
        guard case .queued(let acceptedRevision) = queue.enqueue(fileName: "transcript.jsonl", data: Data([1, 2, 3])) else {
            Issue.record("accepted write should fit"); return
        }
        #expect(queue.enqueue(fileName: "transcript.jsonl", data: Data([4, 5])) == .full)
        #expect(queue.enqueue(fileName: "transcript.jsonl", data: Data([6])) == .full)
        #expect(queue.snapshot.pendingEvents == 1)
        #expect(queue.snapshot.unqueuedWriteCount == 2)
        #expect(!queue.snapshot.canAcknowledgeDataLoss)
        let acknowledgementBeforeDrain = queue.acknowledgeDataLossAndResume()
        #expect(!acknowledgementBeforeDrain)

        // The only accepted entry must be acknowledged before the missing writes can
        // be acknowledged. Clearing the queue failure never claims the rejected data saved.
        queue.acknowledge(through: acceptedRevision)
        #expect(queue.snapshot.pendingEvents == 0)
        #expect(queue.snapshot.state == .blocked)
        #expect(queue.snapshot.hasUnacknowledgedDataLoss)
        #expect(queue.snapshot.canAcknowledgeDataLoss)
        #expect(!queue.snapshot.dataLossAcknowledged)

        let acknowledged = queue.acknowledgeDataLossAndResume()
        #expect(acknowledged)
        #expect(queue.snapshot.state == .saved)
        #expect(queue.snapshot.unqueuedWriteCount == 2)
        #expect(queue.snapshot.dataLossAcknowledged)

        guard case .queued(let futureRevision) = queue.enqueue(fileName: "transcript.jsonl", data: Data([7])) else {
            Issue.record("future write should be accepted after acknowledgement"); return
        }
        #expect(futureRevision == acceptedRevision + 1)
        queue.acknowledge(through: futureRevision)
        #expect(queue.snapshot.state == .saved)
        #expect(queue.snapshot.unqueuedWriteCount == 2)
        #expect(queue.snapshot.dataLossAcknowledged)
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

    @Test func translationRefreshWaitsForRetryReceiptAndIsSessionScoped() {
        var queue = SessionArchiveWriteQueue()
        guard case .queued(let firstRevision) = queue.enqueue(fileName: "translations.jsonl", data: Data("one\n".utf8)),
              case .queued(let secondRevision) = queue.enqueue(fileName: "translations.jsonl", data: Data("two\n".utf8)) else {
            Issue.record("translation writes should be queued"); return
        }
        var firstSession = SessionTranslationWriteReceipts()
        firstSession.recordSubmittedRevision(firstRevision)
        firstSession.recordSubmittedRevision(secondRevision)
        var otherSession = SessionTranslationWriteReceipts()
        otherSession.recordSubmittedRevision(secondRevision)

        queue.fail("disk full")
        let firstFailedAcknowledgement = firstSession.acknowledge(persistedRevision: queue.snapshot.persistedRevision)
        let otherFailedAcknowledgement = otherSession.acknowledge(persistedRevision: queue.snapshot.persistedRevision)
        #expect(!firstFailedAcknowledgement)
        #expect(!otherFailedAcknowledgement)

        // The first receipt persists a prefix; one refresh covers that whole successful batch.
        queue.acknowledge(through: firstRevision)
        let firstReceiptAcknowledged = firstSession.acknowledge(persistedRevision: queue.snapshot.persistedRevision)
        let duplicateFirstAcknowledgement = firstSession.acknowledge(persistedRevision: queue.snapshot.persistedRevision)
        #expect(firstReceiptAcknowledged)
        #expect(!duplicateFirstAcknowledgement)
        #expect(otherSession.hasPending)

        // A retry receipt for the remaining write refreshes only its owning session.
        queue.acknowledge(through: secondRevision)
        let otherReceiptAcknowledged = otherSession.acknowledge(persistedRevision: queue.snapshot.persistedRevision)
        let duplicateOtherAcknowledgement = otherSession.acknowledge(persistedRevision: queue.snapshot.persistedRevision)
        #expect(otherReceiptAcknowledged)
        #expect(!duplicateOtherAcknowledgement)
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
