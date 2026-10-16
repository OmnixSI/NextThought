import Foundation
import InterviewHackerCore
import Security

enum AnswerBackend: String, CaseIterable, Identifiable {
    case codexAppServer, codexExec, openAICompatible
    var id: String { rawValue }
    var title: String {
        switch self {
        case .codexAppServer: "Codex 常驻服务 · 流式（推荐）"
        case .codexExec: "Codex exec · 兼容模式"
        case .openAICompatible: "OpenAI 兼容 API · 流式（自带 Key）"
        }
    }
}

struct AnswerRequest {
    var system: String
    var user: String
    var model: String
    var effort: String
    var imageURL: URL?
    /// Codex service tier: "fast" (Fast mode), "default" (force standard on models that
    /// default to Fast) or nil (standard).
    var serviceTier: String? = nil
    /// Let Codex search the web (background “联网” answers).
    var webSearch = false
    var fullPrompt: String { system + "\n\n" + user }
}

enum AnswerEngineError: LocalizedError {
    case appServerUnavailable(String)
    case invalidEndpoint
    case missingKey
    case http(Int, String)
    case empty
    case timeout

    var errorDescription: String? {
        switch self {
        case let .appServerUnavailable(detail): "Codex 常驻服务不可用：\(detail)"
        case .invalidEndpoint: "API 地址无效。请填写 https 地址（本机 Ollama / LM Studio 可用 http://localhost）。"
        case .missingKey: "请先在设置中填写 API Key（保存在系统钥匙串）。"
        case let .http(code, body): "模型服务返回 HTTP \(code)：\(body.prefix(300))"
        case .empty: "模型没有返回文本。"
        case .timeout: "回答超过 90 秒，已自动取消。请检查网络或换用更快的模型。"
        }
    }
}

// MARK: - Codex app-server (persistent, streaming)

/// Long-lived `codex app-server` over stdio. One process is reused for the whole app
/// run, so each answer only costs two RPCs instead of a CLI launch + login check.
final class CodexAppServerClient: @unchecked Sendable {
    private struct Box: @unchecked Sendable { let value: [String: Any] }

    private let lock = NSLock()
    private var process: Process?
    private var input: FileHandle?
    private var nextID = 1
    private var pending: [Int: CheckedContinuation<Box, Error>] = [:]
    private var notificationHandler: ((String, [String: Any]) -> Void)?
    private var startTask: Task<Void, Error>?
    private var activeTurn: TurnState?

    var isRunning: Bool { lock.withLock { process?.isRunning == true && startTask != nil } }

    func prewarm() async {
        do { try await ensureStarted() } catch { Diagnostics.record("appserver.prewarm.failed") }
    }

    func shutdown() {
        let old: Process? = lock.withLock {
            let value = process
            process = nil; input = nil; startTask = nil
            return value
        }
        failAll(CancellationError())
        if old?.isRunning == true { old?.terminate() }
    }

    func cancelActiveTurn() {
        guard let turn = lock.withLock({ activeTurn }) else { return }
        interrupt(turn)
    }

    func answer(_ request: AnswerRequest, onDelta: @escaping @Sendable (String) -> Void) async throws -> String {
        try await ensureStarted()
        let workDir = FileManager.default.temporaryDirectory.appendingPathComponent("interviewhacker-appserver-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: workDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: workDir) }

        let started: [String: Any]
        do {
            started = try await send("thread/start", CodexAppServerProtocol.threadStartParams(model: request.model, effort: request.effort, cwd: workDir.path, fast: request.serviceTier == "fast", webSearch: request.webSearch), timeout: 20)
        } catch let error as AnswerEngineError {
            throw error
        } catch {
            // Usually an old CLI that does not know the v2 API.
            throw AnswerEngineError.appServerUnavailable(error.localizedDescription)
        }
        guard let threadID = CodexAppServerProtocol.threadID(fromThreadStart: started) else {
            throw AnswerEngineError.appServerUnavailable("thread/start 没有返回 thread id")
        }
        let turn = TurnState(threadID: threadID, onDelta: onDelta)
        lock.withLock {
            activeTurn = turn
            notificationHandler = { method, params in turn.handle(method: method, params: params) }
        }
        defer {
            lock.withLock {
                if activeTurn === turn { activeTurn = nil; notificationHandler = nil }
            }
        }
        let timeout = Task { [weak self] in
            try? await Task.sleep(for: .seconds(90))
            guard !Task.isCancelled else { return }
            turn.finish(.failure(AnswerEngineError.timeout))
            self?.interrupt(turn)
        }
        defer { timeout.cancel() }

        return try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<String, Error>) in
                turn.setContinuation(continuation)
                Task {
                    do {
                        let result = try await self.send("turn/start", CodexAppServerProtocol.turnStartParams(threadID: threadID, text: request.fullPrompt, imagePath: request.imageURL?.path, effort: request.effort, fast: request.serviceTier == "fast"), timeout: 20)
                        turn.setTurnID(CodexAppServerProtocol.turnID(fromTurnStart: result))
                    } catch {
                        turn.finish(.failure(error))
                    }
                }
            }
        } onCancel: {
            turn.finish(.failure(CancellationError()))
            self.interrupt(turn)
        }
    }

    private func interrupt(_ turn: TurnState) {
        guard let turnID = turn.turnID else { return }
        write(CodexAppServerProtocol.request(id: allocateID(), method: "turn/interrupt", params: ["threadId": turn.threadID, "turnId": turnID]))
    }

    private func ensureStarted() async throws {
        let task: Task<Void, Error> = lock.withLock {
            if let startTask, process?.isRunning == true { return startTask }
            let next = Task { try await self.launch() }
            startTask = next
            return next
        }
        do { try await task.value }
        catch {
            lock.withLock { if startTask == task { startTask = nil } }
            throw error
        }
    }

    private func launch() async throws {
        guard let executable = CodexBridge.executableURL() else { throw AppFailure.codexUnavailable }
        let process = Process()
        let stdin = Pipe(), stdout = Pipe()
        process.executableURL = executable
        process.arguments = ["app-server"]
        process.standardInput = stdin
        process.standardOutput = stdout
        process.standardError = FileHandle.nullDevice
        process.environment = CodexBridge.childEnvironment()
        process.currentDirectoryURL = FileManager.default.temporaryDirectory
        process.terminationHandler = { [weak self] ended in
            guard let self else { return }
            let current = self.lock.withLock { () -> Bool in
                guard self.process === ended else { return false }
                self.process = nil; self.input = nil; self.startTask = nil
                return true
            }
            if current { self.failAll(AnswerEngineError.appServerUnavailable("进程已退出（\(ended.terminationStatus)）")) }
            Diagnostics.record("appserver.exited", ["status": ended.terminationStatus])
        }
        do { try process.run() } catch {
            throw AnswerEngineError.appServerUnavailable(error.localizedDescription)
        }
        lock.withLock { self.process = process; self.input = stdin.fileHandleForWriting }
        let reader = stdout.fileHandleForReading
        Task.detached(priority: .userInitiated) { [weak self] in
            do {
                for try await line in reader.bytes.lines { self?.handle(line) }
            } catch {}
        }
        let startedAt = Date()
        do {
            _ = try await send("initialize", CodexAppServerProtocol.initializeParams(version: Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "dev"), timeout: 20)
            write(CodexAppServerProtocol.notification(method: "initialized"))
            let account = try await send("account/read", ["refreshToken": false], timeout: 15)
            guard CodexAppServerProtocol.isLoggedIn(accountRead: account) else {
                throw AppFailure.codexNotSignedIn
            }
        } catch {
            if process.isRunning { process.terminate() }
            if error is AppFailure { throw error }
            throw AnswerEngineError.appServerUnavailable(error.localizedDescription)
        }
        Diagnostics.record("appserver.ready", ["seconds": Date().timeIntervalSince(startedAt), "pid": process.processIdentifier])
    }

    private func allocateID() -> Int { lock.withLock { defer { nextID += 1 }; return nextID } }

    private func send(_ method: String, _ params: [String: Any], timeout: TimeInterval) async throws -> [String: Any] {
        let id = allocateID()
        let watchdog = Task { [weak self] in
            try? await Task.sleep(for: .seconds(timeout))
            guard !Task.isCancelled else { return }
            self?.resolve(id, .failure(AnswerEngineError.appServerUnavailable("\(method) 超时")))
        }
        defer { watchdog.cancel() }
        let box: Box = try await withCheckedThrowingContinuation { continuation in
            let accepted = lock.withLock { () -> Bool in
                guard input != nil else { return false }
                pending[id] = continuation
                return true
            }
            guard accepted else {
                continuation.resume(throwing: AnswerEngineError.appServerUnavailable("服务未运行"))
                return
            }
            write(CodexAppServerProtocol.request(id: id, method: method, params: params))
        }
        return box.value
    }

    private func resolve(_ id: Int, _ result: Result<Box, Error>) {
        let continuation = lock.withLock { pending.removeValue(forKey: id) }
        continuation?.resume(with: result)
    }

    private func failAll(_ error: Error) {
        let all = lock.withLock { () -> [CheckedContinuation<Box, Error>] in
            let values = Array(pending.values); pending.removeAll(); return values
        }
        all.forEach { $0.resume(throwing: error) }
        lock.withLock { activeTurn }?.finish(.failure(error))
    }

    private func write(_ data: Data) {
        let handle = lock.withLock { input }
        try? handle?.write(contentsOf: data)
    }

    private func handle(_ line: String) {
        guard let message = CodexAppServerProtocol.parse(line) else { return }
        switch message {
        case let .response(id, result, error):
            if let error { resolve(id, .failure(AnswerEngineError.appServerUnavailable(error))) }
            else { resolve(id, .success(Box(value: result ?? [:]))) }
        case let .notification(method, params):
            lock.withLock { notificationHandler }?(method, params)
        case let .serverRequest(id, method, _):
            Diagnostics.record("appserver.server_request", ["method": method])
            write(CodexAppServerProtocol.reply(toServerRequest: id, method: method))
        }
    }

    /// State of the one in-flight turn. Notifications for other threads/turns are ignored.
    final class TurnState: @unchecked Sendable {
        let threadID: String
        private let lock = NSLock()
        private var accumulator = AgentMessageAccumulator()
        private var continuation: CheckedContinuation<String, Error>?
        private var result: Result<String, Error>?
        private var storedTurnID: String?
        private let onDelta: @Sendable (String) -> Void

        init(threadID: String, onDelta: @escaping @Sendable (String) -> Void) {
            self.threadID = threadID; self.onDelta = onDelta
        }
        var turnID: String? { lock.withLock { storedTurnID } }
        func setTurnID(_ id: String?) { lock.withLock { storedTurnID = id } }

        func setContinuation(_ next: CheckedContinuation<String, Error>) {
            let ready: Result<String, Error>? = lock.withLock {
                if let result { return result }
                continuation = next
                return nil
            }
            if let ready { next.resume(with: ready) }
        }

        func handle(method: String, params: [String: Any]) {
            if let thread = params["threadId"] as? String, thread != threadID { return }
            guard let event = CodexAppServerProtocol.turnEvent(method: method, params: params, turnID: turnID) else { return }
            switch event {
            case .delta, .agentMessageCompleted:
                let text = lock.withLock { () -> String in accumulator.apply(event); return accumulator.text }
                onDelta(text)
            case let .retrying(message):
                Diagnostics.record("appserver.retrying", ["message_characters": message.count])
            case let .completed(status, error):
                let text = lock.withLock { accumulator.text }
                switch status {
                case "completed" where !text.isEmpty: finish(.success(text))
                case "completed": finish(.failure(AnswerEngineError.empty))
                case "interrupted": finish(.failure(CancellationError()))
                default: finish(.failure(AppFailure.capture("Codex 请求失败：\(error ?? status)")))
                }
            }
        }

        func finish(_ value: Result<String, Error>) {
            let pendingContinuation: CheckedContinuation<String, Error>? = lock.withLock {
                guard result == nil else { return nil }
                result = value
                let current = continuation; continuation = nil
                return current
            }
            pendingContinuation?.resume(with: value)
        }
    }
}

// MARK: - OpenAI-compatible chat completions (streaming SSE)

final class OpenAICompatibleClient: @unchecked Sendable {
    // One shared session keeps TLS/HTTP2 connections warm between answers.
    private let session: URLSession

    /// `resourceTimeout` caps a whole answer; summaries of long sessions get more time.
    init(resourceTimeout: TimeInterval = 90) {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 30
        configuration.timeoutIntervalForResource = resourceTimeout
        session = URLSession(configuration: configuration)
    }

    func answer(_ request: AnswerRequest, baseURL: String, apiKey: String, reasoningEffort: String? = nil, onDelta: @escaping @Sendable (String) -> Void) async throws -> String {
        guard let endpoint = ChatCompletionStreamParser.endpoint(from: baseURL) else { throw AnswerEngineError.invalidEndpoint }
        let isLocal = ["localhost", "127.0.0.1", "::1"].contains(endpoint.host?.lowercased() ?? "")
        guard isLocal || !apiKey.isEmpty else { throw AnswerEngineError.missingKey }
        var urlRequest = URLRequest(url: endpoint)
        urlRequest.httpMethod = "POST"
        urlRequest.setValue("application/json", forHTTPHeaderField: "Content-Type")
        urlRequest.setValue("text/event-stream", forHTTPHeaderField: "Accept")
        if !apiKey.isEmpty { urlRequest.setValue("Bearer \(apiKey)", forHTTPHeaderField: "Authorization") }
        let image = try request.imageURL.map { try Data(contentsOf: $0).base64EncodedString() }
        urlRequest.httpBody = ChatCompletionStreamParser.body(model: request.model, system: request.system, user: request.user, imageJPEGBase64: image, reasoningEffort: reasoningEffort)

        let (bytes, response) = try await session.bytes(for: urlRequest)
        guard let http = response as? HTTPURLResponse else { throw AnswerEngineError.empty }
        if !(200..<300).contains(http.statusCode) {
            var body = ""
            for try await line in bytes.lines { body += line; if body.count > 600 { break } }
            throw AnswerEngineError.http(http.statusCode, body)
        }
        let parser = ChatCompletionStreamParser()
        var text = ""
        for try await line in bytes.lines {
            try Task.checkCancellation()
            switch parser.parse(line: line) {
            case let .delta(piece)?:
                text += piece
                onDelta(text)
            case .done?: break
            case let .failure(message)?: throw AnswerEngineError.http(http.statusCode, message)
            case nil: continue
            }
        }
        guard !text.isEmpty else { throw AnswerEngineError.empty }
        return text
    }
}

// MARK: - Keychain

enum KeychainStore {
    /// The app's bundle identifier ("com.interviewhacker.app"); CI builds use their own
    /// identifier, so tests never read or prompt for the user's real keychain items.
    private static let service = Bundle.main.bundleIdentifier ?? "com.interviewhacker.app"

    static func read(_ account: String) -> String {
        let query: [String: Any] = [kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service,
                                    kSecAttrAccount as String: account, kSecReturnData as String: true, kSecMatchLimit as String: kSecMatchLimitOne]
        var item: CFTypeRef?
        guard SecItemCopyMatching(query as CFDictionary, &item) == errSecSuccess, let data = item as? Data else { return "" }
        return String(decoding: data, as: UTF8.self)
    }

    @discardableResult
    static func write(_ value: String, account: String) -> Bool {
        let base: [String: Any] = [kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service, kSecAttrAccount as String: account]
        SecItemDelete(base as CFDictionary)
        guard !value.isEmpty else { return true }
        var attributes = base
        attributes[kSecValueData as String] = Data(value.utf8)
        attributes[kSecAttrAccessible as String] = kSecAttrAccessibleWhenUnlockedThisDeviceOnly
        return SecItemAdd(attributes as CFDictionary, nil) == errSecSuccess
    }
}
