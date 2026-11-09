import AppKit
import Darwin
import Foundation
import PDFKit
import NextThoughtCore

private actor SelfTestCompletionFlag {
    private var finished = false
    func markFinished() { finished = true }
    func isFinished() -> Bool { finished }
}

private actor SelfTestReleaseGate {
    private var continuation: CheckedContinuation<Void, Never>?
    func wait() async { await withCheckedContinuation { continuation = $0 } }
    func release() { continuation?.resume(); continuation = nil }
}

/// Entry point: the normal app, or `NextThought --self-test <suites…>` which
/// exercises the real code paths headlessly (used by CI on macOS runners):
///
///   data     workspaces, session archive, export → wipe → import round trip
///   signing  local signing identity, re-signing keeps the designated requirement
///   update   download + verify the latest release; rehearse the bundle swap
///   asr      install X-ASR models, two-pass recognition of the fixtures, question detection
///   live-session-ui native live-page screenshot and interaction acceptance matrix
///
/// Set NEXTTHOUGHT_DATA_DIR to a scratch folder; `--fixtures <dir>` for asr.
/// Set NEXTTHOUGHT_SELFTEST_EXPORT_DIRECTORY to retain generated export fixtures.
@main
enum Launcher {
    static func main() {
        let arguments = CommandLine.arguments
        if let index = arguments.firstIndex(of: "--document-tool") {
            do { try CodexDocumentReader.tool(Array(arguments.dropFirst(index + 1))); exit(0) }
            catch { fputs("Document tool: \(error)\n", stderr); exit(1) }
        }
        if let index = arguments.firstIndex(of: "--self-test") {
            let code = SelfTest.run(Array(arguments.dropFirst(index + 1)))
            // Suites await their persistence receipts before returning. Do not run
            // process-global AppKit teardown while background framework tasks live:
            // headless runners have no NSApplication termination lifecycle.
            fflush(nil)
            _exit(code)
        }
        NextThoughtApp.main()
    }
}

enum SelfTest {
    struct Failure: Error, CustomStringConvertible {
        var description: String
        init(_ description: String) { self.description = description }
    }

    static func run(_ arguments: [String]) -> Int32 {
        setvbuf(stdout, nil, _IONBF, 0)
        guard AppEnvironment.values["NEXTTHOUGHT_DATA_DIR"] != nil else {
            print("SELFTEST refuse: set NEXTTHOUGHT_DATA_DIR to a scratch folder first.")
            return 2
        }
        var options: [String: String] = [:]
        var suites: [String] = []
        var index = 0
        while index < arguments.count {
            let item = arguments[index]
            if item.hasPrefix("--"), index + 1 < arguments.count { options[String(item.dropFirst(2))] = arguments[index + 1]; index += 2; continue }
            suites.append(item); index += 1
        }
        if suites.isEmpty || suites == ["all"] { suites = ["data", "signing", "update", "asr"] }
        var failed = 0
        for suite in suites {
            let started = Date()
            print("SELFTEST ▶︎ \(suite)")
            let result: Result<String, Error> = runAsync {
                switch suite {
                case "documents": return try await DocumentSelfTest.run(options)
                case "model-readiness": return try await DocumentSelfTest.modelReadiness()
                case "local-models": return try await DocumentSelfTest.localModels(options)
                case "models": return try await DocumentSelfTest.model()
                case "runtime": return try await DocumentSelfTest.runtime(options)
                case "document-edges": return try await DocumentSelfTest.edges(options)
                case "document-lifecycle": return try await DocumentSelfTest.lifecycle()
                case "model-files": return try await DocumentSelfTest.modelFiles()
                case "model-delete": return try await DocumentSelfTest.modelDeletion()
                case "document-backup": return try await DocumentSelfTest.archiveRoundTrip()
                case "settings-navigation": return try await SettingsNavigationSelfTest.run(options)
                case "meeting-content": return try await MeetingContentSelfTest.run()
                case "archive-ux": return try await ArchiveUXSelfTest.run()
                case "large-pdf": return try await LargePDFSelfTest.run(options)
                case "fishbone-rendering": return try await FishboneRenderingSelfTest.run()
                case "structured-notes": return try await StructuredNotesSelfTest.run()
                case "document-setup": return try await DocumentSelfTest.setupChoices()
                case "retrieval-eval": return try await RetrievalSelfTest.run(options)
                case "screen-context": return try await ScreenSelfTest.run(options)
                case "relay": return try await CompanionRelaySelfTest.run(options)
                case "search-eval": return try await ScreenSelfTest.search(options)
                case "live-session-ui": return try await LiveSessionAcceptanceSelfTest.run(options)
                case "session-export": return try verifySessionExports()
                case "session-backup-drain": return try await testSessionBackupWaitsForFinalTranslationDrain()
                case "session-edit-receipts": return try await testSessionEditsWaitForSaveReceipts()
                case "session-carry-context": return try await testSessionCarryContextSnapshot()
                case "source-integrity": return try await testDeletedMeetingBlocksLateSummaryCandidates()
                case "large-file": return try await PreparationSelfTest.largeFile(options)
                case "preparation-file": return try await PreparationSelfTest.realFile(options)
                case "capacity": return try await PreparationSelfTest.capacity()
                case "preparation": return try await PreparationSelfTest.run()
                case "data": return try await testData()
                case "signing": return try await testSigning()
                case "update": return try await testUpdate(options)
                case "asr": return try await testASR(options)
                default: throw Failure("unknown suite \(suite)")
                }
            }
            let seconds = String(format: "%.1f s", Date().timeIntervalSince(started))
            switch result {
            case let .success(detail): print("SELFTEST ✔ \(suite) (\(seconds)) \(detail)")
            case let .failure(error): failed += 1; print("SELFTEST ✘ \(suite) (\(seconds)) \(error)")
            }
        }
        print(failed == 0 ? "SELFTEST all passed" : "SELFTEST \(failed) failed")
        return failed == 0 ? 0 : 1
    }

    /// Runs async work on the main actor while pumping the main run loop.
    private static func runAsync(_ work: @escaping @MainActor () async throws -> String) -> Result<String, Error> {
        var result: Result<String, Error>?
        Task { @MainActor in
            // Archive/restore paths may create AppKit-backed model state even in CLI tests.
            _ = NSApplication.shared
            do { result = .success(try await work()) } catch { result = .failure(error) }
        }
        while result == nil { RunLoop.main.run(mode: .default, before: Date().addingTimeInterval(0.05)) }
        return result!
    }

    private static func expect(_ condition: Bool, _ message: @autoclosure () -> String) throws {
        if !condition { throw Failure(message()) }
    }

    // MARK: data

    @MainActor
    private static func testData() async throws -> String {
        guard !DemoMode.isEnabled else { throw Failure("run data selftest without NEXTTHOUGHT_DEMO") }
        let defaults = UserDefaults.standard
        let keys = ["workspaceMigrationCompleted", "sessionMode", "answerPreferences", "activeWorkspaceID"]
        let previous = keys.map { defaults.object(forKey: $0) }
        defer {
            for (key, value) in zip(keys, previous) {
                if let value { defaults.set(value, forKey: key) } else { defaults.removeObject(forKey: key) }
            }
        }
        defaults.removeObject(forKey: "workspaceMigrationCompleted")
        let store = WorkspaceStore.shared
        let archive = SessionArchive.shared
        try? FileManager.default.removeItem(at: AppPaths.workspaces)
        try? FileManager.default.removeItem(at: AppPaths.sessions)
        // Legacy migration: the old single context becomes the first workspace.
        UserDefaults.standard.set("interview", forKey: "sessionMode")
        UserDefaults.standard.set("旧的回答要求", forKey: "answerPreferences")
        store.load()
        try expect(store.workspaces.count >= 1 && store.workspaces[0].mode == .interview && store.workspaces[0].preferences == "旧的回答要求",
                   "legacy context not migrated: \(store.workspaces.map(\.name))")

        var interview = store.create(mode: .interview, name: "自检面试")
        interview.fields["company"] = "测试公司"
        interview.documents = [ContextDocument(name: "简历.pdf", pages: ["Redis 缓存层"], category: "简历")]
        interview.lessons = [WorkspaceLesson(text: "先说结论")]
        interview.questionBank = [QuestionBankItem(question: "介绍一下项目", answer: "订单缓存层")]
        store.update(interview)
        let academic = store.create(mode: .academic, name: "自检报告")
        store.flush()

        let meta = SessionMeta(startedAt: Date(), mode: .interview, workspaceID: interview.id, workspaceName: interview.name)
        archive.begin(meta, context: SessionContextSnapshot(mode: .interview, style: .concise, preferences: "", profile: "", documents: []))
        archive.append(SessionTranscriptLine(date: Date(), speaker: .remote, text: "介绍一下你的项目"))
        archive.append(SessionTranscriptLine(date: Date(), speaker: .me, text: "我负责订单缓存层"))
        archive.append(SessionQuestionEvent(date: Date(), text: "介绍一下你的项目", intent: "explanationRequest", score: 0.9, disposition: "auto"))
        archive.finish()
        guard await archive.flush(sessionID: meta.id) else { throw Failure("session writes did not receive a save receipt") }
        guard let saved = await archive.detail(meta.id) else { throw Failure("session not readable after writing") }
        try expect(saved.lines.count == 2 && saved.questions.count == 1, "session content \(saved.lines.count)/\(saved.questions.count)")

        // A file this version cannot read is set aside, never deleted.
        let junk = AppPaths.workspaces.appendingPathComponent("\(UUID().uuidString).json")
        try Data("{not json".utf8).write(to: junk)
        store.load()
        try expect(FileManager.default.fileExists(atPath: junk.appendingPathExtension("unreadable").path), "unreadable workspace file was not kept aside")

        let exportFile = FileManager.default.temporaryDirectory.appendingPathComponent("selftest-\(UUID().uuidString).ntbackup")
        try DataBackup.exportAll(to: exportFile, includeSecrets: false, workspaces: store.workspaces)
        let before = store.workspaces.count

        // Simulate a fresh machine, then import.
        try FileManager.default.removeItem(at: AppPaths.workspaces)
        try FileManager.default.removeItem(at: AppPaths.sessions)
        store.replaceAll([Workspace(name: "新机器", mode: .general)])
        let summary = try await DataBackup.importArchive(exportFile, applySettings: false)
        archive.load()
        try await Task.sleep(for: .milliseconds(500))
        try expect(store.workspaces.count == before + 1, "workspaces after import: \(store.workspaces.count), expected \(before + 1)")
        guard let restored = store.workspace(interview.id) else { throw Failure("interview workspace missing after import") }
        try expect(restored.fields["company"] == "测试公司" && restored.documents.first?.category == "简历" && restored.lessons.count == 1 && restored.questionBank.count == 1,
                   "workspace content changed in round trip")
        try expect(store.workspace(academic.id)?.mode == .academic, "academic workspace lost")
        try expect(archive.sessions.contains { $0.id == meta.id && $0.workspaceID == interview.id }, "session lost in round trip")

        // Importing the same file again changes nothing.
        let again = try await DataBackup.importArchive(exportFile, applySettings: false)
        try expect(again.workspacesAdded == 0 && again.sessionsAdded == 0, "re-import duplicated data: \(again.description)")
        try expect(!DataBackup.listBackups().isEmpty, "no automatic backup taken before import")
        return summary.description
    }

    @MainActor
    private static func testSessionBackupWaitsForFinalTranslationDrain() async throws -> String {
        guard AppEnvironment.values["NEXTTHOUGHT_DATA_DIR"] != nil else {
            throw Failure("session-backup-drain requires NEXTTHOUGHT_DATA_DIR scratch storage")
        }
        let archive = SessionArchive.shared
        guard archive.activeID == nil else { throw Failure("an archive is already active") }
        let defaults = UserDefaults.standard
        let previousAutoBackup = defaults.object(forKey: "autoBackup")
        defaults.set(true, forKey: "autoBackup")
        defer {
            if let previousAutoBackup { defaults.set(previousAutoBackup, forKey: "autoBackup") }
            else { defaults.removeObject(forKey: "autoBackup") }
        }

        let meta = SessionMeta(startedAt: Date(), mode: .meeting, title: "final translation backup self-test")
        let lineID = UUID()
        archive.begin(meta, context: SessionContextSnapshot(mode: .meeting, style: .concise,
                                                            preferences: "", profile: "", documents: []))
        archive.append(SessionTranscriptLine(id: lineID, date: Date(), speaker: .remote, text: "The final translation must be backed up."))
        archive.beginFinalTranslationDrain(for: meta.id)
        guard archive.finish() != nil else { throw Failure("session did not finalize") }

        let backupFinished = SelfTestCompletionFlag()
        guard let backupTask = DataBackup.afterSession(sessionID: meta.id) else {
            throw Failure("automatic session backup was disabled")
        }
        let observer = Task.detached {
            _ = await backupTask.value
            await backupFinished.markFinished()
        }
        try await Task.sleep(for: .seconds(1))
        guard !(await backupFinished.isFinished()) else {
            throw Failure("after-session backup completed before the final translation drain")
        }

        archive.appendTranslation(sessionID: meta.id, lineID: lineID, text: "最终翻译已保存")
        await archive.finishFinalTranslationDrain(for: meta.id)
        guard let backupURL = await backupTask.value else {
            throw Failure("after-session backup failed: \(AppState.shared.backupIssue ?? AppState.shared.dataStatus)")
        }
        await observer.value
        guard await backupFinished.isFinished() else { throw Failure("backup completion was not observed") }

        let point = try JSONDecoder().decode(RestorePoint.self, from: Data(contentsOf: backupURL))
        let translationObjects = point.files.filter { $0.key.contains(meta.id.uuidString) && $0.key.hasSuffix("/translations.jsonl") }
        guard translationObjects.count == 1, let translationHash = translationObjects.first?.value else {
            throw Failure("restore point is missing the session translation file")
        }
        let translationFile = AppPaths.backups.appendingPathComponent("Objects/\(translationHash)")
        let translations = JSONLines.decode(SessionTranscriptTranslation.self, from: try Data(contentsOf: translationFile))
        guard translations == [SessionTranscriptTranslation(id: lineID, text: "最终翻译已保存")] else {
            throw Failure("restore point omitted or duplicated the final translation")
        }
        return "final translation is present in the session restore point"
    }

    @MainActor
    private static func testSessionEditsWaitForSaveReceipts() async throws -> String {
        guard AppEnvironment.values["NEXTTHOUGHT_DATA_DIR"] != nil else {
            throw Failure("session-edit-receipts requires NEXTTHOUGHT_DATA_DIR scratch storage")
        }
        let archive = SessionArchive.shared
        guard archive.activeID == nil else { throw Failure("an archive is already active") }

        let meta = SessionMeta(startedAt: Date(), mode: .meeting, title: "edit receipt self-test")
        archive.begin(meta, context: SessionContextSnapshot(mode: .meeting, style: .concise,
                                                            preferences: "", profile: "", documents: []))
        let transcriptLine = SessionTranscriptLine(date: Date(), speaker: .remote, text: "A saved note must be durable.")
        archive.append(transcriptLine)
        guard archive.finish() != nil, await archive.flush(sessionID: meta.id) else {
            throw Failure("session fixture did not receive a save receipt")
        }

        let itemID = UUID()
        let notes = StructuredNotes(templateID: "general", templateName: "General", language: "en",
                                    title: "Receipt test", sections: [
                                        NotesSectionResult(id: "main", title: "Main", kind: .items,
                                                           items: [NotesItem(id: itemID, text: "before")])
                                    ])
        guard archive.saveNotes(notes, for: meta.id), await archive.flush(sessionID: meta.id) else {
            throw Failure("notes fixture did not receive a save receipt")
        }
        var legacySummary = SessionSummary(
            title: "Receipt test", markdown: "## Main\n\nbefore", questions: [], generatedAt: Date(), model: "selftest",
            transcriptSourceRevision: SessionProjectionBuilder.transcriptSourceRevision(meta: meta, lines: [transcriptLine]))
        legacySummary.store(StructuredNotesPayload(format: .outline,
            outline: OutlineNotes(sections: [OutlineSection(title: "Main", points: [OutlinePoint(text: "original structured node")])]),
            transcriptSourceRevision: legacySummary.transcriptSourceRevision))
        guard legacySummary.structuredNotesSourceRevision == nil,
              archive.saveSummary(legacySummary, for: meta.id), await archive.flush(sessionID: meta.id) else {
            throw Failure("legacy summary fixture did not receive a save receipt")
        }
        let notesURL = archive.folder(meta.id).appendingPathComponent("notes.json")
        let summaryURL = archive.folder(meta.id).appendingPathComponent("summary.json")
        let originalNotesBytes = try Data(contentsOf: notesURL)
        let originalSummaryBytes = try Data(contentsOf: summaryURL)
        archive.summarizing.insert(meta.id)
        let busyNoteEdit: SessionArchiveEditResult = await withCheckedContinuation { continuation in
            archive.updateNotes(meta.id, { current in
                current.sections[0].items[0].text = "blocked edit"
                return true
            }, completion: { continuation.resume(returning: $0) })
        }
        let busySummaryEdit: SessionArchiveEditResult = await withCheckedContinuation { continuation in
            archive.updateSummary(meta.id, { current in
                current.markdown = "blocked edit"
                return true
            }, completion: { continuation.resume(returning: $0) })
        }
        archive.summarizing.remove(meta.id)
        try expect(busyNoteEdit == .busy && busySummaryEdit == .busy,
                   "archive accepted edits while summary regeneration was active")

        let noteEdit: SessionArchiveEditResult = await withCheckedContinuation { continuation in
            archive.updateNotes(meta.id, { current in
                guard let sectionIndex = current.sections.firstIndex(where: { $0.items.contains { $0.id == itemID } }),
                      let itemIndex = current.sections[sectionIndex].items.firstIndex(where: { $0.id == itemID }) else {
                    return false
                }
                current.sections[sectionIndex].items[itemIndex].text = "after"
                return true
            }, completion: { continuation.resume(returning: $0) })
        }
        try expect(noteEdit == .saved, "text-note edit returned \(noteEdit), expected saved")
        guard let afterNoteEdit = await archive.detail(meta.id),
              afterNoteEdit.effectiveNotes?.sections.first?.items.first?.text == "after" else {
            throw Failure("text-note save receipt arrived before the edited value was readable")
        }
        guard afterNoteEdit.notes?.sections.first?.items.first?.text == "before",
              afterNoteEdit.summary?.markdown == legacySummary.markdown,
              try Data(contentsOf: notesURL) == originalNotesBytes,
              try Data(contentsOf: summaryURL) == originalSummaryBytes else {
            throw Failure("editing text notes replaced the saved AI originals")
        }
        let legacySummaryHit = SessionContentSearch.firstHit(
            in: SessionContentSearch.fields(meta: afterNoteEdit.meta, lines: afterNoteEdit.lines,
                                            questions: afterNoteEdit.questions, answers: afterNoteEdit.answers,
                                            summary: afterNoteEdit.effectiveSummary, notes: afterNoteEdit.effectiveNotes),
            terms: Glossary().search(for: "before"))
        try expect(legacySummaryHit == nil || legacySummaryHit?.isStale == true,
                   "canonical search returned superseded text as current content")

        let staleNoteEdit: SessionArchiveEditResult = await withCheckedContinuation { continuation in
            archive.updateNotes(meta.id, { _ in false }, completion: { continuation.resume(returning: $0) })
        }
        try expect(staleNoteEdit == .staleTarget, "missing text-note target returned \(staleNoteEdit)")

        let summaryEdit: SessionArchiveEditResult = await withCheckedContinuation { continuation in
            archive.updateSummary(meta.id, { current in
                guard var payload = current.payload(for: .outline),
                      payload.outline?.sections.first?.points.first != nil else { return false }
                payload.outline?.sections[0].points[0].text = "structured after"
                current.store(payload)
                current.markdown = "summary after"
                return true
            }, completion: { continuation.resume(returning: $0) })
        }
        try expect(summaryEdit == .saved, "structured-note edit returned \(summaryEdit), expected saved")
        guard let afterSummaryEdit = await archive.detail(meta.id),
              afterSummaryEdit.effectiveSummary?.markdown == "summary after",
              afterSummaryEdit.effectiveSummary?.payload(for: .outline)?.outline?.sections.first?.points.first?.text == "structured after",
              afterSummaryEdit.userContent.editedSummary != nil else {
            throw Failure("structured-note save receipt arrived before the full edited summary was readable")
        }
        guard afterSummaryEdit.notes?.sections.first?.items.first?.text == "before",
              afterSummaryEdit.summary?.payload(for: .outline)?.outline?.sections.first?.points.first?.text == "original structured node",
              try Data(contentsOf: notesURL) == originalNotesBytes,
              try Data(contentsOf: summaryURL) == originalSummaryBytes else {
            throw Failure("structured-note editing replaced an AI original instead of the user version")
        }
        let staleSummaryEdit: SessionArchiveEditResult = await withCheckedContinuation { continuation in
            archive.updateSummary(meta.id, { _ in false }, completion: { continuation.resume(returning: $0) })
        }
        try expect(staleSummaryEdit == .staleTarget, "missing structured-note target returned \(staleSummaryEdit)")
        var regeneratedNotes = notes
        regeneratedNotes.sections[0].items[0].text = "new AI notes"
        var regeneratedSummary = legacySummary
        regeneratedSummary.markdown = "new AI summary"
        regeneratedSummary.generatedAt = Date()
        try await archive.saveGeneratedContent(regeneratedSummary, notes: regeneratedNotes, for: meta.id)
        guard await archive.flush(sessionID: meta.id), let reopened = await archive.detail(meta.id),
              reopened.notes?.sections.first?.items.first?.text == "new AI notes",
              reopened.summary?.markdown == "new AI summary",
              reopened.effectiveNotes?.sections.first?.items.first?.text == "after",
              reopened.effectiveSummary?.markdown == "summary after",
              reopened.effectiveSummary?.payload(for: .outline)?.outline?.sections.first?.points.first?.text == "structured after" else {
            throw Failure("regeneration replaced a durable user-authored notes or summary version")
        }
        let oldStructuredHit = SessionContentSearch.firstHit(
            in: SessionContentSearch.fields(meta: reopened.meta, lines: reopened.lines,
                                            questions: reopened.questions, answers: reopened.answers,
                                            summary: reopened.effectiveSummary, notes: reopened.effectiveNotes),
            terms: Glossary().search(for: "original structured node"))
        try expect(oldStructuredHit == nil || oldStructuredHit?.isStale == true,
                   "canonical search returned the old structured AI text as current")
        return "text and full structured-summary edits wait for durable user-version receipts; AI originals, stale/busy guards, canonical search and regeneration protection passed"
    }

    @MainActor
    private static func testSessionCarryContextSnapshot() async throws -> String {
        guard AppEnvironment.values["NEXTTHOUGHT_DATA_DIR"] != nil else {
            throw Failure("session-carry-context requires NEXTTHOUGHT_DATA_DIR scratch storage")
        }
        let archive = SessionArchive.shared
        guard archive.activeID == nil else { throw Failure("an archive is already active") }

        let baseline = ContextDocument(name: "excluded baseline", pages: ["baseline material"])
        let disabled = ContextDocument(name: "disabled material", pages: ["disabled material"], enabled: false)
        let series = Workspace(name: "carry context", mode: .meeting, documents: [baseline, disabled])
        let plan = MeetingPlan(carry: MeetingCarry(materials: false, transcript: true),
                               baselineDocumentIDs: [baseline.id])
        let initialWorkspace = plan.carry.workspaceSnapshot(from: series, baselineDocumentIDs: plan.baselineDocumentIDs)
        let meta = SessionMeta(startedAt: Date(), mode: .meeting, workspaceID: series.id, workspaceName: series.name)
        let app = AppState.shared
        archive.begin(meta, context: app.sessionContextSnapshot(for: initialWorkspace))
        archive.append(SessionTranscriptLine(date: Date(), speaker: .remote, text: "carry context fixture"))

        var finalWorkspace = initialWorkspace
        finalWorkspace.documents.append(ContextDocument(name: "previous transcript", pages: ["carried transcript text"],
                                                         category: "上一场转写"))
        guard app.updateArchiveContext(finalWorkspace, sessionID: meta.id) else {
            throw Failure("final carry context was not accepted by the archive queue")
        }
        guard archive.finish() != nil, await archive.flush(sessionID: meta.id),
              let detail = await archive.detail(meta.id), let documents = detail.context?.documents else {
            throw Failure("final carry context did not receive a readable archive receipt")
        }
        let names = Set(documents.map(\.name))
        try expect(names.contains("previous transcript"), "archived context omitted the carried transcript")
        try expect(!names.contains("excluded baseline"), "archived context leaked excluded baseline material")
        try expect(!names.contains("disabled material"), "archived context included a disabled material")
        return "asynchronously carried transcript is persisted while excluded and disabled documents stay out"
    }

    @MainActor
    private static func testDeletedMeetingBlocksLateSummaryCandidates() async throws -> String {
        _ = NSApplication.shared
        guard AppEnvironment.values["NEXTTHOUGHT_DATA_DIR"] != nil else {
            throw Failure("source-integrity requires NEXTTHOUGHT_DATA_DIR scratch storage")
        }
        guard !AppState.shared.isRunning else { throw Failure("source-integrity requires no active meeting") }
        let app = AppState.shared
        let archive = SessionArchive.shared
        let store = WorkspaceStore.shared
        let workspace = store.create(mode: .meeting, name: "source-integrity self-test")
        let meta = SessionMeta(startedAt: Date(), mode: .meeting, title: "delete race", workspaceID: workspace.id,
                               workspaceName: workspace.name)
        let line = SessionTranscriptLine(date: Date(), speaker: .remote, text: "This transcript belongs to the deleted meeting.")
        archive.begin(meta, context: SessionContextSnapshot(mode: .meeting, style: .concise,
                                                            preferences: "", profile: "", documents: []))
        archive.append(line)
        guard archive.finish() != nil, await archive.flush(sessionID: meta.id) else {
            throw Failure("meeting fixture did not receive a save receipt")
        }

        let sourceRevision = SessionProjectionBuilder.transcriptSourceRevision(meta: meta, lines: [line])
        let lateSummary = SessionSummary(title: "late summary", markdown: "", questions: [], generatedAt: Date(),
                                         model: "self-test", lessons: ["deleted meeting lesson"],
                                         bank: [BankEntry(question: "deleted meeting question", answer: "deleted meeting answer")],
                                         transcriptSourceRevision: sourceRevision)
        let releaseGate = SelfTestReleaseGate()
        let finished = SelfTestCompletionFlag()
        let lateSummaryTask = Task { @MainActor in
            await releaseGate.wait()
            _ = app.collectReviewCandidates(lateSummary, sessionID: meta.id, workspaceID: workspace.id)
            await finished.markFinished()
        }
        archive.summarizing.insert(meta.id)
        archive.summaryTasks[meta.id] = lateSummaryTask

        app.deleteArchivedMeeting(meta.id)
        for _ in 0..<200 where archive.sessions.contains(where: { $0.id == meta.id }) {
            try await Task.sleep(for: .milliseconds(10))
        }
        guard !archive.sessions.contains(where: { $0.id == meta.id }), archive.isTombstoned(meta.id) else {
            await releaseGate.release()
            await lateSummaryTask.value
            throw Failure("delete did not remove and tombstone the meeting")
        }

        // Release a cancelled but non-cooperative callback after deletion. The
        // tombstone/session-presence check must prevent candidate resurrection.
        await releaseGate.release()
        await lateSummaryTask.value
        guard await finished.isFinished() else { throw Failure("late summary callback did not finish") }
        guard let cleanedWorkspace = store.workspace(workspace.id) else { throw Failure("workspace was removed unexpectedly") }
        try expect(cleanedWorkspace.knowledgeCandidates?.isEmpty != false,
                   "late summary callback recreated a candidate for the deleted meeting")
        try expect(!cleanedWorkspace.lessons.contains { $0.text == "deleted meeting lesson" },
                   "late summary callback recreated a lesson for the deleted meeting")
        return "deleting a meeting tombstones and cancels its summary; a late summary cannot recreate knowledge"
    }

    /// Exercises the exact file writers used by the preview sheet with the synthetic F10
    /// fixture: a 20+ page bilingual transcript, Chinese names, a long URL, nested lists,
    /// and a Markdown table. Kept isolated from the user's archive and signing identity.
    private static func verifySessionExports() throws -> String {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let longURL = "https://example.invalid/releases/nextthought/2026/11/09/" + String(repeating: "review-and-approval-", count: 12)
            + "?owner=wang-xiaoming&reviewer=chen-xiaoyu&build=20261109"
        let longTableCell = "WRAP-START 王小明负责在上线前逐项检查双语会议记录、数据备份、审批状态、责任人和时间；陈晓雨确认所有问题都已关闭后再提交最终的发布复核报告。 WRAP-END"
        let wideTableCell = "WIDE-START " + String(repeating: "责任人已核对", count: 12) + " WIDE-END"
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(5_300), mode: .meeting,
                               title: "导出格式自检")
        let lines = (0..<650).map { index in
            let date = start.addingTimeInterval(Double(index * 8 + 2))
            let person = index.isMultiple(of: 2) ? "王小明" : "陈晓雨"
            let source = "\(person)确认第\(index + 1)段：先核对上线名单、责任人和时间，再按已记录的依据安排下一步。"
            let id = UUID()
            return SessionTranscriptLine(id: id, date: date, speaker: index.isMultiple(of: 2) ? .remote : .me,
                                         text: source, translatedText: "Part \(index + 1): confirm the release list, owner, and timing.",
                                         translationInputRevision: SessionProjectionBuilder.transcriptRevision(id: id, text: source),
                                         startedAt: date, endedAt: date.addingTimeInterval(4))
        }
        let summaryMarkdown = """
        ## 决定与行动
        - 王小明负责预发布验证。
          1. 陈晓雨复核长会记录。
            - 如果失败，先保留旧版并联系值班负责人。

        | 负责人 | 交付物 | 日期 |
        | --- | --- | --- |
        | 王小明 | 预发布验证清单 | 周五 |
        | 陈晓雨 | 双语会议摘要 | 下周一 |
        | 王小明 | \(longTableCell) | 周五 |

        | 负责人 | 交付物 | 日期 | 状态 |
        | --- | --- | --- | --- |
        | \(String(repeating: "王小明", count: 8)) | \(wideTableCell) | \(String(repeating: "周五", count: 16)) | \(String(repeating: "待确认", count: 10)) |

        ## 参考链接
        \(longURL)
        """
        let summary = SessionSummary(title: "王小明与陈晓雨的导出格式自检", markdown: summaryMarkdown,
                                    questions: [], generatedAt: start, model: "self-test")
        let projection = SessionProjectionBuilder.build(meta: meta, lines: lines, summary: summary)
        let markdown = SessionMarkdownExporter.export(meta: meta, projection: projection)
        for format in [SessionExportFormat.markdown, .docx, .pdf] {
            let preview = SessionExportPreviewContent.make(format: format, meta: meta, projection: projection)
            try expect(preview.usesPageLayout && preview.sourceText == markdown,
                       "\(format.rawValue) preview did not use the same Markdown source as export")
        }
        for (format, subtitleFormat) in [(SessionExportFormat.srt, SessionSubtitleFormat.srt),
                                         (.vtt, .vtt)] {
            let expected = SessionSubtitleExporter.export(meta: meta, projection: projection, format: subtitleFormat).text
            let preview = SessionExportPreviewContent.make(format: format, meta: meta, projection: projection)
            try expect(!preview.usesPageLayout && preview.sourceText == expected,
                       "\(format.rawValue) preview did not keep its subtitle source text")
        }
        let retainedPath = ProcessInfo.processInfo.environment["NEXTTHOUGHT_SELFTEST_EXPORT_DIRECTORY"]?
            .trimmingCharacters(in: .whitespacesAndNewlines)
        let retainedDirectory = retainedPath.flatMap { $0.isEmpty ? nil : URL(fileURLWithPath: $0, isDirectory: true) }
        let retainArtifacts = retainedDirectory != nil
        let root = retainedDirectory
            ?? FileManager.default.temporaryDirectory.appendingPathComponent("nextthought-export-\(UUID().uuidString)",
                                                                              isDirectory: true)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { if !retainArtifacts { try? FileManager.default.removeItem(at: root) } }

        var pdfPages = 0
        for format in SessionExportFormat.allCases {
            let content: String
            switch format {
            case .markdown, .docx, .pdf: content = markdown
            case .srt: content = SessionSubtitleExporter.export(meta: meta, projection: projection, format: .srt).text
            case .vtt: content = SessionSubtitleExporter.export(meta: meta, projection: projection, format: .vtt).text
            }
            let url = root.appendingPathComponent("meeting.\(format.fileExtension)")
            try SessionFileExporter.write(content: content, format: format, to: url)
            let data = try Data(contentsOf: url)
            try expect(!data.isEmpty, "\(format.rawValue) export is empty")
            switch format {
            case .markdown:
                let text = String(decoding: data, as: UTF8.self)
                try expect(text.contains("第650段"), "Markdown omitted the final transcript line")
                try expect(text.contains("王小明") && text.contains("陈晓雨"), "Markdown omitted Chinese names")
                try expect(text.contains("如果失败，先保留旧版") && text.contains("| 王小明 | 预发布验证清单 | 周五 |"),
                           "Markdown omitted nested list or table fixture")
                try expect(text.contains(longURL), "Markdown omitted the long URL")
            case .srt:
                let text = String(decoding: data, as: UTF8.self)
                try expect(text.contains("第650段"), "SRT omitted the final transcript line")
                if let source = text.range(of: "第1段"), let translation = text.range(of: "Part 1:") {
                    try expect(source.lowerBound < translation.lowerBound, "SRT placed the translation before its source")
                } else {
                    throw Failure("SRT omitted the first bilingual cue")
                }
            case .vtt:
                let text = String(decoding: data, as: UTF8.self)
                try expect(text.hasPrefix("WEBVTT") && text.contains("第650段"), "VTT header or final cue missing")
                if let source = text.range(of: "第1段"), let translation = text.range(of: "Part 1:") {
                    try expect(source.lowerBound < translation.lowerBound, "VTT placed the translation before its source")
                } else {
                    throw Failure("VTT omitted the first bilingual cue")
                }
            case .docx:
                try expect(data.starts(with: [0x50, 0x4B]), "DOCX is not a ZIP package")
                let decoded = try NSAttributedString(url: url,
                                                    options: [.documentType: NSAttributedString.DocumentType.officeOpenXML],
                                                    documentAttributes: nil)
                try expect(decoded.string.contains("第650段"), "DOCX round trip omitted the final transcript line")
                try expect(decoded.string.contains("王小明") && decoded.string.contains("陈晓雨"), "DOCX round trip omitted Chinese names")
                try expect(decoded.string.contains("如果失败，先保留旧版") && decoded.string.contains("预发布验证清单"),
                           "DOCX round trip omitted nested-list or table content")
                try expect(decoded.string.contains(longURL), "DOCX round trip omitted the long URL")
                let documentXML = try Shell.check("/usr/bin/unzip", ["-p", url.path, "word/document.xml"],
                                                  "读取 DOCX 主文档").output
                try expect(documentXML.contains("<w:p") && documentXML.contains("<w:r"),
                           "DOCX does not contain editable Word paragraphs and runs")
                try expect(documentXML.contains("<w:tbl"), "DOCX omitted the native editable table")
                try expect(documentXML.components(separatedBy: "<w:tr>").count - 1 >= 3
                           && documentXML.components(separatedBy: "<w:tc>").count - 1 >= 9,
                           "DOCX table omitted header or data cells")
                try expect(documentXML.contains("<w:numPr") && documentXML.contains("<w:ilvl"),
                           "DOCX omitted editable nested-list numbering")
                try expect(documentXML.contains("<w:ilvl w:val=\"1\""),
                           "DOCX omitted the nested list level")
                try expect(documentXML.contains("<w:ilvl w:val=\"2\""),
                           "DOCX omitted the mixed-style nested list level")
            case .pdf:
                guard let document = PDFDocument(url: url) else { throw Failure("PDF could not be opened") }
                pdfPages = document.pageCount
                try expect(pdfPages > 20, "F10 PDF fixture should paginate to more than 20 pages; got \(pdfPages)")
                guard let page = document.page(at: 0), let text = document.string else {
                    throw Failure("PDF has no readable first page or text")
                }
                let mediaBox = page.bounds(for: .mediaBox)
                try expect(mediaBox.width >= 500 && mediaBox.height >= 700, "PDF page media box is unexpectedly small")
                try expect(text.contains("第650段"), "PDF omitted the final transcript line")
                try expect(text.contains("王小明") && text.contains("陈晓雨"), "PDF omitted Chinese names")
                try expect(text.contains("如果失败，先保留旧版") && text.contains("预发布验证清单"),
                           "PDF omitted nested-list or table content")
                try expect(text.contains("1. 陈晓雨复核长会记录。"),
                           "PDF omitted the ordered nested-list marker")
                let pdfWithoutWrapMarkers = String(text.filter { !$0.isWhitespace && $0 != "\u{200B}" })
                let compactURL = String(longURL.filter { !$0.isWhitespace })
                try expect(pdfWithoutWrapMarkers.contains(compactURL), "PDF omitted the long URL")
                try expect(text.contains("│ 负责人") && text.contains("│ 王小明"),
                           "PDF omitted the visible table grid")
                try expect(text.contains("\n"), "PDF omitted visible paragraph separation")
                try expect(text.contains("WRAP-START"), "PDF omitted the long table-cell fixture")
                for marker in ["WRAP-START", "WRAP-END", "WIDE-START"] {
                    guard let markerPage = (0..<document.pageCount).compactMap({ document.page(at: $0) })
                        .first(where: { $0.string?.contains(marker) == true }) else {
                        throw Failure("PDF omitted a wrapped table-cell marker: \(marker)")
                    }
                    let markerBounds = markerPage.bounds(for: .mediaBox)
                    let markerLines = markerPage.selection(for: markerBounds)?.selectionsByLine() ?? []
                    guard let line = markerLines.first(where: { $0.string?.contains(marker) == true }),
                          let lineText = line.string else {
                        throw Failure("PDF omitted a wrapped table-cell marker: \(marker)")
                    }
                    if marker.hasPrefix("WRAP-") {
                        let visibleLine = lineText.trimmingCharacters(in: .whitespacesAndNewlines)
                        try expect(visibleLine.hasPrefix("│") && visibleLine.hasSuffix("│"),
                                   "PDF table-cell wrap broke the grid around \(marker)")
                    }
                    try expect(line.bounds(for: markerPage).maxX <= markerBounds.maxX - 40 + 2,
                               "PDF table-cell line exceeded the page text bounds around \(marker)")
                }
            }
        }

        let selectionLine = SessionTranscriptLine(date: start.addingTimeInterval(20), speaker: .remote,
                                                  text: "王小明确认周五提交。")
        let selectionMeta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(30), mode: .meeting)
        let selectionNotes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "版本选择",
                                             sections: [], corrections: [TranscriptCorrection(
                                                line: 0, lineID: selectionLine.id,
                                                baseRevision: SessionProjectionBuilder.transcriptRevision(id: selectionLine.id,
                                                                                                         text: selectionLine.text),
                                                original: "周五", corrected: "周一")])
        let originalProjection = SessionProjectionBuilder.build(
            meta: selectionMeta, lines: [selectionLine], notes: selectionNotes,
            options: .init(transcriptVariant: .original, languageView: .source))
        let correctedProjection = SessionProjectionBuilder.build(
            meta: selectionMeta, lines: [selectionLine], notes: selectionNotes,
            options: .init(transcriptVariant: .corrected, languageView: .source))
        for (name, selectedProjection, expected, absent) in [
            ("original", originalProjection, "周五", "周一"),
            ("corrected", correctedProjection, "周一", "周五")
        ] {
            let content = SessionMarkdownExporter.export(meta: selectionMeta, projection: selectedProjection)
            let pagePreview = SessionExportPreviewContent.make(format: .docx, meta: selectionMeta,
                                                               projection: selectedProjection)
            try expect(pagePreview.usesPageLayout && pagePreview.sourceText == content,
                       "DOCX page preview did not use the selected \(name) source revision")
            for format in [SessionExportFormat.docx, .pdf] {
                let url = root.appendingPathComponent("\(name).\(format.fileExtension)")
                try SessionFileExporter.write(content: content, format: format, to: url)
                let decoded: String
                switch format {
                case .docx:
                    decoded = try NSAttributedString(url: url,
                                                     options: [.documentType: NSAttributedString.DocumentType.officeOpenXML],
                                                     documentAttributes: nil).string
                case .pdf:
                    guard let document = PDFDocument(url: url), let text = document.string else {
                        throw Failure("\(name) PDF projection could not be read")
                    }
                    decoded = text
                default:
                    throw Failure("unexpected projection writer \(format.rawValue)")
                }
                try expect(decoded.contains(expected) && !decoded.contains(absent),
                           "\(format.rawValue) writer did not preserve the selected \(name) projection")
            }
        }
        let retainedNote = retainArtifacts ? "; files retained at \(root.path)" : ""
        return "F10 fixture: Markdown, editable DOCX, PDF (\(pdfPages) pages), SRT and VTT passed\(retainedNote)"
    }

    // MARK: signing

    @MainActor
    private static func testSigning() async throws -> String {
        let app = Bundle.main.bundleURL
        guard app.pathExtension == "app" else { throw Failure("run the self-test from inside NextThought.app") }
        let identity = try await Task.detached { try LocalSigning.ensureIdentity() }.value
        try expect(LocalSigning.isBound, "identity created but not bound: " + LocalSigning.bindingDiagnostics)
        let work = FileManager.default.temporaryDirectory.appendingPathComponent("selftest-sign-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: work, withIntermediateDirectories: true)
        let first = work.appendingPathComponent("A/NextThought.app"), second = work.appendingPathComponent("B/NextThought.app")
        for copy in [first, second] {
            try FileManager.default.createDirectory(at: copy.deletingLastPathComponent(), withIntermediateDirectories: true)
            try Shell.check("/usr/bin/ditto", [app.path, copy.path], "copy")
        }
        try await Task.detached { try LocalSigning.sign(first); try LocalSigning.sign(second) }.value
        let a = LocalSigning.requirement(of: first), b = LocalSigning.requirement(of: second)
        try expect(a != nil && a == b && a == LocalSigning.designatedRequirement, "requirements differ: \(a ?? "nil") / \(b ?? "nil")")
        try expect(a?.lowercased().contains(identity.lowercased()) == true, "requirement does not pin the certificate")
        let info = LocalSigning.signature(of: first)
        try expect(info?.isAdHoc == false, "re-signed copy still ad-hoc")
        // A second ensureIdentity must reuse the same certificate (permissions depend on it).
        let again = try await Task.detached { try LocalSigning.ensureIdentity() }.value
        try expect(again == identity, "identity changed on second use")
        return "identity \(identity.prefix(8))…, stable requirement"
    }

    // MARK: update

    @MainActor
    private static func testUpdate(_ options: [String: String]) async throws -> String {
        let updater = Updater.shared
        if let repository = options["repo"] { updater.repository = repository }
        let token = AppEnvironment.values["GITHUB_TOKEN"].flatMap { $0.isEmpty ? nil : $0 }
        let channel = UpdateChannel(rawValue: options["channel"] ?? "stable") ?? .stable
        let staged = try await updater.stageForSelfTest(channel: channel, token: token)
        guard let executableName = Bundle(url: staged.app)?.object(forInfoDictionaryKey: "CFBundleExecutable") as? String else {
            throw Failure("staged app has no executable declaration")
        }
        let executableURL = staged.app.appendingPathComponent("Contents/MacOS", isDirectory: true)
            .appendingPathComponent(executableName)
        try expect(FileManager.default.isExecutableFile(atPath: executableURL.path),
                   "staged app executable \(executableName) is missing or not executable")

        // Rehearse the swap on a scratch copy (a dead pid, no relaunch).
        let scratch = FileManager.default.temporaryDirectory.appendingPathComponent("selftest-swap-\(UUID().uuidString)", isDirectory: true)
        let destination = scratch.appendingPathComponent("NextThought.app")
        try FileManager.default.createDirectory(at: scratch, withIntermediateDirectories: true)
        try Shell.check("/usr/bin/ditto", [Bundle.main.bundleURL.path, destination.path], "copy current app")
        let status = try Updater.runSwap(waitingFor: 999_999, source: staged.app, destination: destination, relaunch: false, wait: true)
        try expect(status == 0, "swap script failed with \(status)")
        let installed = LocalSigning.requirement(of: destination)
        try expect(installed != nil, "swapped bundle has no signature")
        let version = (NSDictionary(contentsOf: destination.appendingPathComponent("Contents/Info.plist"))?["CFBundleShortVersionString"] as? String) ?? "?"
        return "staged \(staged.tag) (version \(version))\(staged.note.map { "; \($0)" } ?? "")"
    }

    // MARK: asr

    @MainActor
    private static func testASR(_ options: [String: String]) async throws -> String {
        guard let fixtures = options["fixtures"] else { throw Failure("pass --fixtures <dir with zh.wav, en.wav, mixed.wav>") }
        for package in [LocalASRPackage.xasrStreaming480, .xasrOffline] {
            try await package.install { _, _ in }
            try expect(package.isInstalled, "\(package.title) not installed")
            try expect(FileManager.default.fileExists(atPath: package.directory.appendingPathComponent("bpe.vocab").path), "bpe.vocab missing")
        }
        var report: [String] = []
        for (name, expected) in [("mixed", "system design"), ("zh", "数据库"), ("en", "query")] {
            let samples = try readWAV(URL(fileURLWithPath: fixtures).appendingPathComponent("\(name).wav"))
            let started = Date()
            let finals = try await transcribe(samples, hotwords: name == "en" ? "database index" : "")
            let text = finals.joined(separator: " ")
            print("SELFTEST   \(name): \(text)")
            try expect(text.localizedCaseInsensitiveContains(expected), "\(name) transcript lacks “\(expected)”: \(text)")
            report.append(String(format: "%@ %.1fs", name, Date().timeIntervalSince(started)))
            if name == "mixed" {
                var tracker = QuestionCandidateTracker(mode: .interview)
                var triggered = false
                for (offset, final) in finals.enumerated() {
                    if case .trigger = tracker.ingest(final, isFinal: true, utteranceKey: "s\(offset)") { triggered = true }
                }
                try expect(triggered, "no question detected in “\(text)”")
            }
        }
        return report.joined(separator: ", ")
    }

    /// The app's own pipeline: streaming X-ASR fed in 100 ms chunks, the voice-activity
    /// segmenter closing utterances, and the offline second pass for the final text.
    @MainActor
    private static func transcribe(_ samples: [Int16], hotwords: String) async throws -> [String] {
        let transcriber = ZipformerStreamingTranscriber(model: LocalStreamingModel.xasr480.onlineModel,
                                                       refiner: .xasrOffline(at: LocalASRPackage.xasrOffline.directory), vocabulary: hotwords)
        var finals: [String] = []
        var failure: String?
        try await transcriber.start(language: "auto") { text, isFinal, _, _, _, _ in
            if isFinal { finals.append(text) }
        } onError: { failure = $0 }
        let input = transcriber.inputHandler()
        var segmenter = VoiceActivitySegmenter()
        let origin = Date()
        // Trailing silence lets the segmenter close the last utterance.
        let padded = samples + [Int16](repeating: 0, count: 16_000 * 2)
        for start in stride(from: 0, to: padded.count, by: 1_600) {
            let chunk = Array(padded[start..<min(start + 1_600, padded.count)])
            input(chunk, origin)
            for var event in segmenter.append(chunk) where event.kind == .final {
                event.streamStartedAt = origin
                await transcriber.commitUtterance(event)
            }
        }
        await transcriber.finish()
        try await Task.sleep(for: .milliseconds(300))
        if let failure { throw Failure(failure) }
        return finals
    }

    private static func readWAV(_ url: URL) throws -> [Int16] {
        let data = try Data(contentsOf: url)
        guard data.count > 44, String(decoding: data[0..<4], as: UTF8.self) == "RIFF" else { throw Failure("not a WAV: \(url.lastPathComponent)") }
        // Walk chunks to “data”; fixtures are 16 kHz mono PCM16.
        var offset = 12
        while offset + 8 <= data.count {
            let id = String(decoding: data[offset..<offset + 4], as: UTF8.self)
            let size = Int(data[offset + 4]) | Int(data[offset + 5]) << 8 | Int(data[offset + 6]) << 16 | Int(data[offset + 7]) << 24
            if id == "data" {
                let body = data[(offset + 8)..<min(data.count, offset + 8 + size)]
                return stride(from: body.startIndex, to: body.endIndex - 1, by: 2).map { Int16(bitPattern: UInt16(body[$0]) | UInt16(body[$0 + 1]) << 8) }
            }
            offset += 8 + size + (size % 2)
        }
        throw Failure("no data chunk in \(url.lastPathComponent)")
    }
}
