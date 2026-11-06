import Foundation
import NextThoughtCore

/// Exercises the real job state without recording audio or calling a model.
@MainActor
enum StructuredNotesSelfTest {
    static func run() async throws -> String {
        let jobs = StructuredNotesJobs.shared
        let key = StructuredJobKey(id: UUID(), format: .outline)
        let payload = StructuredNotesPayload(format: .outline,
            outline: OutlineNotes(sections: [OutlineSection(title: "Topic", at: 0,
                points: [OutlinePoint(text: "Evidence", at: 1)])]))
        var summary = SessionSummary(title: "Test", markdown: "Notes", questions: [], generatedAt: Date(), model: "test")
        summary.store(payload)
        let old = jobs.start(key)
        jobs.cancel(key)
        let current = jobs.start(key)
        jobs.complete(payload, for: key, token: old)
        jobs.finish(key, token: old)
        guard jobs.running[key] == current, jobs.completed[key] == nil else {
            throw SelfTest.Failure("cancelled job changed a newer job")
        }
        jobs.complete(payload, for: key, token: current)
        jobs.finish(key, token: current)
        guard !jobs.isRunning(key), jobs.completed[key] == payload else {
            throw SelfTest.Failure("completed view vanished before the page reloaded")
        }
        jobs.acknowledge(nil, for: key.id)
        guard jobs.completed[key] == payload else { throw SelfTest.Failure("stale page cleared the result") }
        jobs.acknowledge(summary, for: key.id)
        guard jobs.completed[key] == nil else { throw SelfTest.Failure("persisted result was not acknowledged") }
        jobs.markFailed(key)
        _ = jobs.start(key)
        guard !jobs.hasFailed(key) else { throw SelfTest.Failure("retry retained the previous failure") }
        jobs.cancel(key)

        var events: [Int] = []
        let first = Task { @MainActor in
            await jobs.exclusive {
                events.append(1)
                try? await Task.sleep(for: .milliseconds(80))
                events.append(2)
            }
        }
        while events.isEmpty { await Task.yield() }
        await jobs.exclusive { events.append(3) }
        await first.value
        guard events == [1, 2, 3] else { throw SelfTest.Failure("summary commits overlapped") }
        return "cancel/restart tokens, result display until reload, retry state and exclusive summary commits passed; no model calls"
    }
}
