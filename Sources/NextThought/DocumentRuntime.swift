import Foundation
import NextThoughtCore

enum DocumentOCRProvider: String, CaseIterable, Identifiable {
    case system, paddle, codex
    var id: String { rawValue }
    var title: String {
        switch self {
        case .system: "系统 OCR（离线、无需下载）"
        case .paddle: "本机增强识别"
        case .codex: "Codex 文件代读（在线）"
        }
    }
}

struct DocumentConversionOptions: Sendable {
    var provider: DocumentOCRProvider = .system
    var fallback = true
    var model = "gpt-6-luna"
    var effort = "medium"
    var timeout: TimeInterval = 600
    var isPaused: @Sendable () async -> Bool = { false }
    var localModelID: String = ModelCatalog.paddleOCR.id
    var sourceHash: String? = nil
    var checkpointDirectory: URL? = nil
    var onProgress: @Sendable (DocumentJobEvent) -> Void = { _ in }
}

enum DocumentRuntime {
    static let version = "1.7.0"
    static var directory: URL {
        if let path = AppEnvironment.values["NEXTTHOUGHT_RUNTIME_DIR"] {
            return URL(fileURLWithPath: path, isDirectory: true)
        }
        return AppPaths.root.appendingPathComponent("Runtimes/document-\(version)", isDirectory: true)
    }
    static var python: URL { directory.appendingPathComponent("python/bin/python3") }
    static var installed: Bool {
        FileManager.default.isExecutableFile(atPath: python.path)
            && ((try? Data(contentsOf: directory.appendingPathComponent("installed.json")))
                .flatMap { try? JSONSerialization.jsonObject(with: $0) as? [String: Any] })?["version"] as? String == version
    }
    static var resources: URL {
        let bundled = Bundle.main.resourceURL!.appendingPathComponent("DocumentRuntime", isDirectory: true)
        if FileManager.default.fileExists(atPath: bundled.path) { return bundled }
        // SwiftPM developer builds; packaged apps always use their own resources.
        return URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            .deletingLastPathComponent().appendingPathComponent("Resources/DocumentRuntime", isDirectory: true)
    }
    static var worker: URL { resources.appendingPathComponent("worker.py") }
    static var temporary: URL { AppPaths.root.appendingPathComponent("Temporary/Documents", isDirectory: true) }

    static func environment() -> [String: String] {
        var env = AppEnvironment.values
        for key in ["OPENAI_API_KEY", "CODEX_API_KEY", "OPENAI_BASE_URL", "PYTHONPATH", "PYTHONHOME"] { env.removeValue(forKey: key) }
        let cache = directory.appendingPathComponent("cache").path
        env["PYTHONNOUSERSITE"] = "1"; env["PYTHONUNBUFFERED"] = "1"
        env["HF_HOME"] = cache + "/huggingface"; env["HF_HUB_OFFLINE"] = "1"
        env["TRANSFORMERS_OFFLINE"] = "1"; env["HF_HUB_DISABLE_TELEMETRY"] = "1"
        env["XDG_CACHE_HOME"] = cache; env["TMPDIR"] = temporary.path
        env["TOKENIZERS_PARALLELISM"] = "false"
        return env
    }
}

/// Multiplexed NDJSON transport. Cancelling a caller releases it immediately;
/// the worker independently stops the corresponding job at its next checkpoint.
actor DocumentWorker {
    static let shared = DocumentWorker(terminateOnCancel: true)
    static let embedding = DocumentWorker()
    static let realtime = DocumentWorker(terminateOnCancel: true)
    static let queryEmbedding = DocumentWorker(terminateOnCancel: true)
    private let terminateOnCancel: Bool
    init(terminateOnCancel: Bool = false) { self.terminateOnCancel = terminateOnCancel }
    private var process: Process?
    private var input: FileHandle?
    private var reader: Task<Void, Never>?
    private var pending: [String: CheckedContinuation<[String: Any], Error>] = [:]
    private var generation = UUID()
    private var progress: [String: @Sendable (Int, Int) -> Void] = [:]

    func request(_ operation: String, _ payload: [String: Any] = [:], timeout: TimeInterval = 120, onProgress: @escaping @Sendable (Int, Int) -> Void = { _, _ in }) async throws -> [String: Any] {
        try Task.checkCancellation()
        try start()
        let id = UUID().uuidString
        progress[id] = onProgress
        defer { progress[id] = nil }
        let deadline = Task { [weak self] in
            try? await Task.sleep(for: .seconds(timeout))
            guard !Task.isCancelled else { return }
            await self?.cancel(id, error: AppFailure.capture("文档任务超时（\(Int(timeout)) 秒）。"))
        }
        defer { deadline.cancel() }
        return try await withTaskCancellationHandler {
            try Task.checkCancellation()
            return try await withCheckedThrowingContinuation { continuation in
                pending[id] = continuation
                do { try send(["version": 1, "id": id, "operation": operation, "payload": payload]) }
                catch { pending.removeValue(forKey: id)?.resume(throwing: error) }
            }
        } onCancel: { Task { await self.cancel(id, error: CancellationError()) } }
    }

    private func start() throws {
        if process?.isRunning == true { return }
        guard DocumentRuntime.installed else { throw AppFailure.capture(String(localized: "请先在 设置 → 高级 → 资料组件 下载文档运行包。")) }
        try FileManager.default.createDirectory(at: DocumentRuntime.temporary, withIntermediateDirectories: true)
        let child = Process(); let stdin = Pipe(); let stdout = Pipe()
        child.executableURL = DocumentRuntime.python
        child.arguments = ["-I", DocumentRuntime.worker.path]
        child.environment = DocumentRuntime.environment()
        child.currentDirectoryURL = DocumentRuntime.temporary
        child.standardInput = stdin; child.standardOutput = stdout; child.standardError = FileHandle.nullDevice
        let token = UUID(); generation = token
        try child.run()
        process = child; input = stdin.fileHandleForWriting
        reader = Task { [weak self] in
            do {
                for try await line in ProcessLineReader.lines(from: stdout.fileHandleForReading) {
                    await self?.receive(line, generation: token)
                }
            } catch { }
            await self?.ended(generation: token)
        }
    }

    private func send(_ message: [String: Any]) throws {
        guard let input else { throw AppFailure.capture("文档运行包尚未启动。") }
        var data = try JSONSerialization.data(withJSONObject: message); data.append(10)
        try input.write(contentsOf: data)
    }
    private func receive(_ line: String, generation token: UUID) {
        guard token == generation, let data = line.data(using: .utf8),
              let message = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let id = message["id"] as? String else { return }
        if message["event"] as? String == "progress" {
            if let completed = message["completed"] as? Int, let total = message["total"] as? Int { progress[id]?(completed, total) }
            return
        }
        guard let continuation = pending.removeValue(forKey: id) else { return }
        if let error = message["error"] as? String { continuation.resume(throwing: AppFailure.capture(error)) }
        else if let result = message["result"] as? [String: Any] { continuation.resume(returning: result) }
        else { continuation.resume(throwing: AppFailure.capture("文档运行包返回了无效响应。")) }
    }
    private func cancel(_ id: String, error: Error) {
        guard let continuation = pending.removeValue(forKey: id) else { return }
        try? send(["version": 1, "id": UUID().uuidString, "operation": "cancel", "payload": ["id": id]])
        continuation.resume(throwing: error)
        if terminateOnCancel { unload() }
    }
    private func ended(generation token: UUID) {
        guard generation == token else { return }
        let requests = pending; pending.removeAll(); process = nil; input = nil
        for continuation in requests.values { continuation.resume(throwing: AppFailure.capture("文档运行包已退出，请重试。")) }
    }
    func unload() {
        let requests = pending; pending.removeAll()
        generation = UUID()
        if let process { CodexBridge.stop(process) }
        process = nil; input = nil; reader?.cancel(); reader = nil
        for continuation in requests.values { continuation.resume(throwing: CancellationError()) }
    }
}
