import AppKit
import Foundation
import InterviewHackerCore

/// Entry point: the normal app, or `InterviewHacker --self-test <suites…>` which
/// exercises the real code paths headlessly (used by CI on macOS runners):
///
///   data     workspaces, session archive, export → wipe → import round trip
///   signing  local signing identity, re-signing keeps the designated requirement
///   update   download + verify the latest release; rehearse the bundle swap
///   asr      install X-ASR models, two-pass recognition of the fixtures, question detection
///
/// Set INTERVIEWHACKER_DATA_DIR to a scratch folder; `--fixtures <dir>` for asr.
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
            exit(code)
        }
        InterviewHackerApp.main()
    }
}

enum SelfTest {
    struct Failure: Error, CustomStringConvertible {
        var description: String
        init(_ description: String) { self.description = description }
    }

    static func run(_ arguments: [String]) -> Int32 {
        setvbuf(stdout, nil, _IONBF, 0)
        guard ProcessInfo.processInfo.environment["INTERVIEWHACKER_DATA_DIR"] != nil else {
            print("SELFTEST refuse: set INTERVIEWHACKER_DATA_DIR to a scratch folder first.")
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
                case "document-setup": return try await DocumentSelfTest.setupChoices()
                case "retrieval-eval": return try await RetrievalSelfTest.run(options)
                case "screen-context": return try await ScreenSelfTest.run(options)
                case "relay": return try await CompanionRelaySelfTest.run(options)
                case "search-eval": return try await ScreenSelfTest.search(options)
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
        await archive.flush()
        guard let saved = await archive.detail(meta.id) else { throw Failure("session not readable after writing") }
        try expect(saved.lines.count == 2 && saved.questions.count == 1, "session content \(saved.lines.count)/\(saved.questions.count)")

        // A file this version cannot read is set aside, never deleted.
        let junk = AppPaths.workspaces.appendingPathComponent("\(UUID().uuidString).json")
        try Data("{not json".utf8).write(to: junk)
        store.load()
        try expect(FileManager.default.fileExists(atPath: junk.appendingPathExtension("unreadable").path), "unreadable workspace file was not kept aside")

        let exportFile = FileManager.default.temporaryDirectory.appendingPathComponent("selftest-\(UUID().uuidString).ihbackup")
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

    // MARK: signing

    @MainActor
    private static func testSigning() async throws -> String {
        let app = Bundle.main.bundleURL
        guard app.pathExtension == "app" else { throw Failure("run the self-test from inside InterviewHacker.app") }
        let identity = try await Task.detached { try LocalSigning.ensureIdentity() }.value
        try expect(LocalSigning.isBound, "identity created but not bound: " + LocalSigning.bindingDiagnostics)
        let work = FileManager.default.temporaryDirectory.appendingPathComponent("selftest-sign-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: work, withIntermediateDirectories: true)
        let first = work.appendingPathComponent("A/InterviewHacker.app"), second = work.appendingPathComponent("B/InterviewHacker.app")
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
        let token = ProcessInfo.processInfo.environment["GITHUB_TOKEN"].flatMap { $0.isEmpty ? nil : $0 }
        let channel = UpdateChannel(rawValue: options["channel"] ?? "stable") ?? .stable
        let staged = try await updater.stageForSelfTest(channel: channel, token: token)
        try expect(FileManager.default.fileExists(atPath: staged.app.appendingPathComponent("Contents/MacOS/InterviewHacker").path), "staged app has no executable")

        // Rehearse the swap on a scratch copy (a dead pid, no relaunch).
        let scratch = FileManager.default.temporaryDirectory.appendingPathComponent("selftest-swap-\(UUID().uuidString)", isDirectory: true)
        let destination = scratch.appendingPathComponent("InterviewHacker.app")
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
