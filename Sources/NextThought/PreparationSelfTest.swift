import AppKit
import Foundation
import NextThoughtCore
import PDFKit

@MainActor enum PreparationSelfTest {
    static func run() async throws -> String {
        guard DemoMode.isEnabled, Bundle.main.bundleIdentifier?.hasPrefix("com.nextthought.app.ci") == true,
              AppEnvironment.values["NEXTTHOUGHT_DATA_DIR"] != nil else { throw SelfTest.Failure("preparation requires isolated .ci app, DATA_DIR and DEMO=1") }
        let a = Workspace(name: "target", mode: .general), b = Workspace(name: "viewed", mode: .general)
        let state = AppState.shared
        state.workspaces.installDemo([a, b]); state.workspaces.activate(a.id)
        state.semanticRetrievalEnabled = false
        state.documentPreparation.start(testing: true)
        defer { state.documentPreparation.shutdown() }
        let input = AppPaths.root.appendingPathComponent("fixtures", isDirectory: true)
        try FileManager.default.createDirectory(at: input, withIntermediateDirectories: true)
        var urls: [URL] = []
        for i in 0..<20 {
            let url = input.appendingPathComponent("material-\(i).md")
            let text = i == 19 ? String(repeating: "long-document-unique \(i)\n", count: 12000) : "# Material \(i)\n\ncontent-unique-\(i)"
            try text.write(to: url, atomically: true, encoding: .utf8); urls.append(url)
        }
        let bad = input.appendingPathComponent("encrypted-or-broken.pdf")
        try Data("not a PDF".utf8).write(to: bad)
        state.importDocuments(Array(urls[0..<7])); state.importDocuments(Array(urls[7..<14])); state.importDocuments(Array(urls[14..<20]) + [bad])
        // Viewing a different workspace never retargets the uploads.
        state.workspaces.activate(b.id)
        let deadline = Date().addingTimeInterval(60)
        while state.documentPreparation.jobs.count < 21 || state.documentPreparation.jobs.contains(where: { !$0.stage.terminal }) {
            guard Date() < deadline else { throw SelfTest.Failure("queue timeout: \(state.documentPreparation.jobs.map { $0.progressText + $0.message })") }
            try await Task.sleep(for: .milliseconds(100))
        }
        let jobs = state.documentPreparation.jobs
        guard jobs.filter({ $0.stage == .ready }).count == 20, jobs.filter({ $0.stage == .failed }).count == 1,
              state.workspaces.workspace(a.id)?.documents.count == 20, state.workspaces.workspace(b.id)?.documents.isEmpty == true,
              jobs.first(where: { $0.fileName == bad.lastPathComponent })?.message.contains("60 万") == false else { throw SelfTest.Failure("per-file outcome/workspace isolation failed") }
        let store = try DocumentJobStore(url: DocumentPreparation.root.appendingPathComponent("jobs.sqlite"))
        let disk = try await store.all()
        guard disk.count == 21 else { throw SelfTest.Failure("queue not durable") }
        let options = DocumentConversionOptions(checkpointDirectory: AppPaths.root.appendingPathComponent("checkpoints"))
        let checkpoint = DocumentCheckpoint(options: options, sourceHash: "same-file")
        try checkpoint.save("page-1", blocks: [.init(markdown: "page done", locator: .init(page: 1))], warnings: [], rawText: "original text")
        guard checkpoint.load("page-1")?.rawText == "original text",
              DocumentCheckpoint(options: options, sourceHash: "different").load("page-1") == nil else { throw SelfTest.Failure("checkpoint identity mismatch") }
        return "20 files in 3 consecutive uploads; >200k text; one real PDF error isolated; workspace switch preserved; SQLite persisted; page checkpoints validated"
    }
}

extension PreparationSelfTest {
    static func capacity() async throws -> String {
        guard Bundle.main.bundleIdentifier?.hasPrefix("com.nextthought.app.ci") == true,
              let fixture = AppEnvironment.values["IH_CAPACITY_FIXTURE"] else { throw SelfTest.Failure("capacity requires .ci and IH_CAPACITY_FIXTURE") }
        guard let original = PDFDocument(url: URL(fileURLWithPath: fixture)), let page = original.page(at: 0) else { throw SelfTest.Failure("native PDF fixture missing") }
        let root = AppPaths.root.appendingPathComponent("capacity", isDirectory: true)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        for count in [401, 1000, 2000, 2001] {
            let pdf = PDFDocument()
            for i in 0..<count { pdf.insert(page.copy() as! PDFPage, at: i) }
            let file = root.appendingPathComponent("pages-\(count).pdf")
            guard pdf.write(to: file) else { throw SelfTest.Failure("PDF generation") }
            if count <= DocumentLimits.maximumPages {
                let result = try await MarkdownConverter.convert(file, options: .init())
                guard Set(result.content!.blocks.compactMap { $0.locator.page }).count == count else { throw SelfTest.Failure("lost pages at \(count)") }
            } else {
                do { _ = try await MarkdownConverter.convert(file, options: .init()); throw SelfTest.Failure("2001-page limit not enforced") }
                catch is SelfTest.Failure { throw SelfTest.Failure("2001-page limit not enforced") } catch { }
            }
            try FileManager.default.removeItem(at: file)
        }
        return "401/1000/2000 physical native PDF pages fully retained; 2001 pages rejected explicitly"
    }
}

extension PreparationSelfTest {
    static func realFile(_ options: [String: String]) async throws -> String {
        guard DemoMode.isEnabled, Bundle.main.bundleIdentifier?.hasPrefix("com.nextthought.app.ci") == true,
              let input = options["input"] else { throw SelfTest.Failure("preparation-file requires isolated .ci, DEMO=1 and --input") }
        let url = URL(fileURLWithPath: input)
        guard let pdf = PDFDocument(url: url) else { throw SelfTest.Failure("PDF cannot be opened") }
        let expectedPages = pdf.pageCount, initialHash = ManagedModelStore.sha256(of: url)
        let state = AppState.shared, workspace = Workspace(name: "真实 PDF 验证", mode: .academic)
        state.workspaces.installDemo([workspace]); state.workspaces.activate(workspace.id)
        state.semanticRetrievalEnabled = false; state.documentOCRProvider = .system
        state.documentPreparation.start(testing: true)
        defer { state.documentPreparation.shutdown() }
        let started = Date(); state.importDocuments([url])
        guard let id = state.documentPreparation.jobs.last?.id else { throw SelfTest.Failure("no intake job") }
        var phases: [String] = [], cancelled = false
        let deadline = Date().addingTimeInterval(300)
        while Date() < deadline {
            guard let job = state.documentPreparation.jobs.first(where: { $0.id == id }) else { throw SelfTest.Failure("job disappeared") }
            if phases.last != job.progressText { phases.append(job.progressText) }
            if !cancelled && job.completed >= min(2, expectedPages), job.documentID == nil, !job.stage.terminal {
                state.documentPreparation.cancel(id); cancelled = true
                try await Task.sleep(for: .milliseconds(500)); state.documentPreparation.retry(id)
            }
            if job.stage == .cancelled && cancelled { state.documentPreparation.retry(id) }
            if [.ready, .warning].contains(job.stage) {
                guard let doc = state.documents.first(where: { $0.id == job.documentID }), let content = doc.content else { throw SelfTest.Failure("committed content missing") }
                let pages = Set(content.blocks.compactMap { $0.locator.page })
                guard pages == Set(1...expectedPages), initialHash == ManagedModelStore.sha256(of: url),
                      job.sourceHash == initialHash else { throw SelfTest.Failure("page coverage/source integrity mismatch") }
                let output = AppPaths.root.appendingPathComponent("scan-best-result.json")
                let result: [String: Any] = ["bytes": (try url.resourceValues(forKeys: [.fileSizeKey])).fileSize ?? 0,
                    "pages": expectedPages, "coveredPages": pages.count, "characters": content.markdown.count,
                    "seconds": Date().timeIntervalSince(started), "cancelRetryExercised": cancelled,
                    "sourceUnchanged": true, "phases": phases, "warnings": content.warnings]
                try JSONSerialization.data(withJSONObject: result, options: [.prettyPrinted, .sortedKeys]).write(to: output)
                try content.markdown.write(to: AppPaths.root.appendingPathComponent("scan-best.md"), atomically: true, encoding: .utf8)
                return "\(expectedPages)/\(expectedPages) pages, \(content.markdown.count) characters, cancellation/retry=\(cancelled), original SHA-256 unchanged; \(output.path)"
            }
            if job.stage == .failed { throw SelfTest.Failure(job.message) }
            try await Task.sleep(for: .milliseconds(100))
        }
        throw SelfTest.Failure("real file preparation timed out")
    }
}

extension PreparationSelfTest {
    static func largeFile(_ options: [String: String]) async throws -> String {
        guard Bundle.main.bundleIdentifier?.hasPrefix("com.nextthought.app.ci") == true, let input = options["input"] else { throw SelfTest.Failure("large-file requires isolated .ci and --input") }
        let source = URL(fileURLWithPath: input)
        let size = try source.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
        let outcome = try await DocumentImporter.read([source], categories: [])
        guard let result = outcome.documents.first, outcome.failures.isEmpty,
              result.markdown.contains("1234.56"), result.markdown.contains("-7.5"),
              result.content?.blocks.first?.locator.page == 1 else { throw SelfTest.Failure("large PDF rejected or text lost: \(outcome.failures)") }
        return "\(size) bytes accepted through DocumentImporter; native text, exact numbers and p.1 retained"
    }
}
