import Foundation
import InterviewHackerCore

final class CodexBridge: @unchecked Sendable {
    private let lock = NSLock()
    private var cachedLogin: (status: String, date: Date)?
    private var activeProcess: Process?
    private var activeID: UUID?
    private var timedOutIDs: Set<UUID> = []

    static func executableURL() -> URL? {
        var candidates = [
            ProcessInfo.processInfo.environment["CODEX_CLI_PATH"],
            "/Applications/ChatGPT.app/Contents/Resources/codex-cli/CodexCLI.app/Contents/MacOS/codex",
            "/Applications/Codex.app/Contents/Resources/codex",
            NSHomeDirectory() + "/.local/bin/codex",
            NSHomeDirectory() + "/.cargo/bin/codex",
            "/opt/homebrew/bin/codex",
            "/usr/local/bin/codex"
        ].compactMap { $0 }
        if let path = ProcessInfo.processInfo.environment["PATH"] {
            candidates.append(contentsOf: path.split(separator: ":").map { String($0) + "/codex" })
        }
        return candidates.first(where: { FileManager.default.isExecutableFile(atPath: $0) }).map { URL(fileURLWithPath: $0) }
    }

    static func isChatGPTLogin(_ status: String) -> Bool {
        let lower = status.lowercased()
        return lower.contains("logged in") && lower.contains("chatgpt")
    }

    static func version() -> String? {
        guard let executable = executableURL() else { return nil }
        let result = runStatus(executable: executable, arguments: ["--version"], timeout: 8)
        guard result.status == 0 else { return nil }
        return result.output
    }

    static func loginStatus() async -> String {
        await Task.detached(priority: .utility) {
            guard let executable = executableURL() else { return "未检测到 Codex CLI" }
            let result = runStatus(executable: executable, arguments: ["login", "status"], timeout: 8)
            if result.status == 0 { return result.output.isEmpty ? "Codex 已登录" : result.output }
            return result.output.isEmpty ? "未登录 Codex；请在 ChatGPT / Codex 客户端登录" : result.output
        }.value
    }

    func cancel() {
        lock.lock()
        let process = activeProcess
        lock.unlock()
        if let process { Self.stop(process) }
    }

    func run(prompt: String, model: String, reasoning: String, imageURL: URL? = nil, documentJob: URL? = nil, serviceTier: String? = nil, webSearch: Bool = false, timeout: TimeInterval = 90, onUpdate: @escaping (String) async -> Void) async throws -> String {
        let requestID = UUID()
        return try await withTaskCancellationHandler {
            try await runRequest(id: requestID, prompt: prompt, model: model, reasoning: reasoning, imageURL: imageURL, documentJob: documentJob, serviceTier: serviceTier, webSearch: webSearch, timeout: timeout, onUpdate: onUpdate)
        } onCancel: {
            self.terminate(requestID)
        }
    }

    private func runRequest(id requestID: UUID, prompt: String, model: String, reasoning: String, imageURL: URL?, documentJob: URL?, serviceTier: String?, webSearch: Bool, timeout: TimeInterval, onUpdate: @escaping (String) async -> Void) async throws -> String {
        try Task.checkCancellation()
        let identifierCharacters = CharacterSet.alphanumerics.union(CharacterSet(charactersIn: "-._/:"))
        guard !model.isEmpty, model.unicodeScalars.allSatisfy({ identifierCharacters.contains($0) }),
              ["none", "minimal", "low", "medium", "high", "xhigh", "max", "ultra"].contains(reasoning) else {
            throw AppFailure.transcription("请选择有效的 Codex 模型与推理强度。")
        }
        guard let executable = Self.executableURL() else { throw AppFailure.codexUnavailable }
        let status = await validatedLoginStatus()
        try Task.checkCancellation()
        guard Self.isChatGPTLogin(status) else { throw AppFailure.codexNotSignedIn }

        let workDir = FileManager.default.temporaryDirectory.appendingPathComponent("interviewhacker-codex-\(requestID.uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: workDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: workDir) }

        let errorURL = workDir.appendingPathComponent("codex-stderr.log")
        FileManager.default.createFile(atPath: errorURL.path, contents: nil)
        let errorHandle = try FileHandle(forWritingTo: errorURL)
        defer { try? errorHandle.close() }

        let process = Process()
        let completion = ProcessExitWaiter(process)
        let input = Pipe()
        let output = Pipe()
        process.executableURL = executable
        process.arguments = [
            "exec", "--json", "--ephemeral", "--ignore-user-config", "--ignore-rules", "--skip-git-repo-check",
            "--sandbox", "read-only", "--model", model,
            "--config", "model_reasoning_effort=\"\(reasoning)\"",
            "--config", "features.shell_tool=false",
            "--config", "features.apps=false",
            "--config", "features.multi_agent=false",
            "--config", "web_search=\"\(webSearch ? "live" : "disabled")\"",
            "--cd", workDir.path
        ]
        if let documentJob {
            // Explicit deny-root profile supersedes the broad built-in read-only sandbox.
            process.arguments = process.arguments?.filter { $0 != "--sandbox" && $0 != "read-only" }
            func literal(_ value: String) -> String { { let encoder = JSONEncoder(); encoder.outputFormatting = .withoutEscapingSlashes; return String(data: try! encoder.encode(value), encoding: .utf8)! }() }
            let helper = DocumentRuntime.resources.appendingPathComponent("document_mcp.py").path
            let args = ["-I", helper, documentJob.appendingPathComponent("manifest.json").path]
            let argsJSON = String(data: try JSONSerialization.data(withJSONObject: args, options: .withoutEscapingSlashes), encoding: .utf8)!
            let fs = "{\":root\"=\"deny\",\":minimal\"=\"read\",\(literal(documentJob.path))=\"read\",\(literal(DocumentRuntime.directory.path))=\"read\",\(literal(DocumentRuntime.resources.path))=\"read\"}"
            process.arguments?.append(contentsOf: [
                "-c", "approval_policy=\"never\"", "-c", "default_permissions=\"document-read\"",
                "-c", "permissions.document-read.filesystem=\(fs)",
                "-c", "permissions.document-read.network.enabled=false",
                "-c", "mcp_servers.document.command=\(literal(DocumentRuntime.python.path))",
                "-c", "mcp_servers.document.args=\(argsJSON)",
                "-c", "mcp_servers.document.required=true",
                "-c", "mcp_servers.document.tool_timeout_sec=120"
            ])
        }
        // `--ignore-user-config` drops the user's own service_tier, so set it explicitly.
        if let serviceTier, ["fast", "default"].contains(serviceTier) {
            process.arguments?.append(contentsOf: ["--config", "service_tier=\"\(serviceTier)\""])
        }
        if let imageURL {
            process.arguments?.append(contentsOf: ["--image", imageURL.path])
        }
        process.arguments?.append("-")
        process.standardInput = input
        process.standardOutput = output
        process.standardError = errorHandle
        process.environment = Self.childEnvironment()

        do {
            try process.run()
        } catch {
            throw AppFailure.capture("无法启动 Codex CLI：\(error.localizedDescription)")
        }
        setActiveProcess(process, id: requestID)
        defer { clearActiveProcess(id: requestID) }
        let startedAt = Date()
        Diagnostics.record("codex.started", ["request_id": requestID.uuidString, "model": model, "reasoning": reasoning, "pid": process.processIdentifier])

        let timeoutTask = Task { [weak self] in
            try? await Task.sleep(for: .seconds(timeout))
            guard !Task.isCancelled else { return }
            self?.markTimedOut(requestID)
            self?.terminate(requestID)
        }
        defer { timeoutTask.cancel() }

        if Task.isCancelled {
            terminate(requestID)
            throw CancellationError()
        }
        input.fileHandleForWriting.write(Data(prompt.utf8))
        try? input.fileHandleForWriting.close()

        var fullText = ""
        var readError: Error?
        var eventTypes: Set<String> = []
        do {
            for try await line in ProcessLineReader.lines(from: output.fileHandleForReading) {
                if documentJob != nil, let trace = ProcessInfo.processInfo.environment["INTERVIEWHACKER_DOCUMENT_TRACE_DIR"] {
                    let dir = URL(fileURLWithPath: trace, isDirectory: true)
                    try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
                    let url = dir.appendingPathComponent(requestID.uuidString + ".ndjson")
                    if !FileManager.default.fileExists(atPath: url.path) { FileManager.default.createFile(atPath: url.path, contents: nil) }
                    if let h = try? FileHandle(forWritingTo: url) { _ = try? h.seekToEnd(); try? h.write(contentsOf: Data((line + "\n").utf8)); try? h.close() }
                }
                if let data = line.data(using: .utf8), let item = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
                    let type = item["type"] as? String ?? "unknown"
                    let subtype = (item["item"] as? [String: Any])?["type"] as? String ?? ""
                    eventTypes.insert(subtype.isEmpty ? type : type + ":" + subtype)
                }
                guard let event = CodexOutputParser.parse(line) else { continue }
                switch event {
                case let .failure(detail):
                    readError = AppFailure.capture("Codex 请求失败：\(detail)")
                case let .message(text):
                    fullText = text
                    await onUpdate(text)
                }
            }
        } catch {
            readError = error
        }

        let exitStatus = await completion.wait()
        Diagnostics.record("codex.exited", ["request_id": requestID.uuidString, "seconds": Date().timeIntervalSince(startedAt), "exit_code": exitStatus, "events": eventTypes.sorted()])
        let detail = (try? String(contentsOf: errorURL, encoding: .utf8))?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if takeTimedOut(requestID) {
            throw AppFailure.capture("Codex 请求超过 \(Int(timeout)) 秒，已自动取消。请检查网络或稍后重试。")
        }
        try Task.checkCancellation()
        if let readError { throw readError }
        guard exitStatus == 0 else {
            invalidateLoginCache()
            throw AppFailure.capture(detail.isEmpty ? "Codex CLI 退出，状态码 \(exitStatus)。请检查登录状态与账号使用限制。" : detail)
        }
        guard !fullText.isEmpty else {
            throw AppFailure.capture("Codex 没有返回文本。请确认本机 Codex CLI 与账号可用。")
        }
        return fullText
    }

    func invalidateLoginCache() {
        lock.lock(); cachedLogin = nil; lock.unlock()
    }

    private func readLoginCache() -> String? {
        lock.lock(); defer { lock.unlock() }
        guard let cachedLogin, Date().timeIntervalSince(cachedLogin.date) < 30 else { return nil }
        return cachedLogin.status
    }
    private func storeLoginCache(_ status: String) {
        lock.lock(); defer { lock.unlock() }
        cachedLogin = Self.isChatGPTLogin(status) ? (status, Date()) : nil
    }
    private func validatedLoginStatus() async -> String {
        if let status = readLoginCache() { return status }
        let status = await Self.loginStatus()
        storeLoginCache(status)
        return status
    }

    private static func runStatus(executable: URL, arguments: [String], timeout: TimeInterval) -> (status: Int32, output: String) {
        let process = Process()
        let completion = ProcessExitWaiter(process)
        let pipe = Pipe()
        process.executableURL = executable
        process.arguments = arguments
        process.standardOutput = pipe
        process.standardError = pipe
        process.environment = childEnvironment()
        do {
            try process.run()
            DispatchQueue.global(qos: .utility).asyncAfter(deadline: .now() + timeout) {
                Self.stop(process)
            }
            let data = pipe.fileHandleForReading.readDataToEndOfFile()
            let exitStatus = completion.waitSynchronously(timeout: timeout + 2) ?? -1
            let text = String(data: data, encoding: .utf8)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            return (exitStatus, text)
        } catch {
            return (-1, error.localizedDescription)
        }
    }

    static func childEnvironment() -> [String: String] {
        var environment = ProcessInfo.processInfo.environment
        environment.removeValue(forKey: "OPENAI_API_KEY")
        environment.removeValue(forKey: "CODEX_API_KEY")
        environment.removeValue(forKey: "OPENAI_BASE_URL")
        return environment
    }

    private func setActiveProcess(_ process: Process, id: UUID) {
        lock.lock()
        activeProcess = process
        activeID = id
        lock.unlock()
    }

    private func clearActiveProcess(id: UUID) {
        lock.lock()
        if activeID == id {
            activeProcess = nil
            activeID = nil
        }
        lock.unlock()
    }

    private func terminate(_ id: UUID) {
        lock.lock()
        let process = activeID == id ? activeProcess : nil
        lock.unlock()
        if let process { Self.stop(process) }
    }

    static func stop(_ process: Process) {
        guard process.isRunning else { return }
        process.terminate()
        DispatchQueue.global(qos: .utility).asyncAfter(deadline: .now() + 1) {
            if process.isRunning { kill(process.processIdentifier, SIGKILL) }
        }
    }

    private func markTimedOut(_ id: UUID) {
        lock.lock()
        timedOutIDs.insert(id)
        lock.unlock()
    }

    private func takeTimedOut(_ id: UUID) -> Bool {
        lock.lock()
        defer { lock.unlock() }
        return timedOutIDs.remove(id) != nil
    }
}
