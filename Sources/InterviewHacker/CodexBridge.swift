import Foundation
import InterviewHackerCore

final class CodexBridge: @unchecked Sendable {
    private let lock = NSLock()
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
        if process?.isRunning == true { process?.terminate() }
    }

    func run(prompt: String, imageURL: URL? = nil, onUpdate: @escaping (String) async -> Void) async throws -> String {
        let requestID = UUID()
        return try await withTaskCancellationHandler {
            try await runRequest(id: requestID, prompt: prompt, imageURL: imageURL, onUpdate: onUpdate)
        } onCancel: {
            self.terminate(requestID)
        }
    }

    private func runRequest(id requestID: UUID, prompt: String, imageURL: URL?, onUpdate: @escaping (String) async -> Void) async throws -> String {
        try Task.checkCancellation()
        guard let executable = Self.executableURL() else { throw AppFailure.codexUnavailable }
        let status = await Self.loginStatus()
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
        let input = Pipe()
        let output = Pipe()
        process.executableURL = executable
        process.arguments = [
            "exec", "--json", "--ephemeral", "--ignore-user-config", "--ignore-rules", "--skip-git-repo-check",
            "--sandbox", "read-only", "--model", "gpt-5.6-luna",
            "--config", "model_reasoning_effort=none",
            "--config", "features.shell_tool=false",
            "--config", "features.apps=false",
            "--config", "features.multi_agent=false",
            "--config", "web_search=\"disabled\"",
            "--cd", workDir.path
        ]
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
        Diagnostics.record("codex.started", ["request_id": requestID.uuidString, "model": "gpt-5.6-luna", "reasoning": "none", "pid": process.processIdentifier])

        let timeoutTask = Task { [weak self] in
            try? await Task.sleep(for: .seconds(90))
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
            for try await line in output.fileHandleForReading.bytes.lines {
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

        process.waitUntilExit()
        Diagnostics.record("codex.exited", ["request_id": requestID.uuidString, "seconds": Date().timeIntervalSince(startedAt), "exit_code": process.terminationStatus, "events": eventTypes.sorted()])
        let detail = (try? String(contentsOf: errorURL, encoding: .utf8))?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if takeTimedOut(requestID) {
            throw AppFailure.capture("Codex 请求超过 90 秒，已自动取消。请检查网络或稍后重试。")
        }
        try Task.checkCancellation()
        if let readError { throw readError }
        guard process.terminationStatus == 0 else {
            throw AppFailure.capture(detail.isEmpty ? "Codex CLI 退出，状态码 \(process.terminationStatus)。请检查登录状态与账号使用限制。" : detail)
        }
        guard !fullText.isEmpty else {
            throw AppFailure.capture("Codex 没有返回文本。请确认本机 Codex CLI 与账号可用。")
        }
        return fullText
    }

    private static func runStatus(executable: URL, arguments: [String], timeout: TimeInterval) -> (status: Int32, output: String) {
        let process = Process()
        let pipe = Pipe()
        process.executableURL = executable
        process.arguments = arguments
        process.standardOutput = pipe
        process.standardError = pipe
        process.environment = childEnvironment()
        do {
            try process.run()
            DispatchQueue.global(qos: .utility).asyncAfter(deadline: .now() + timeout) {
                if process.isRunning { process.terminate() }
            }
            let data = pipe.fileHandleForReading.readDataToEndOfFile()
            process.waitUntilExit()
            let text = String(data: data, encoding: .utf8)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            return (process.terminationStatus, text)
        } catch {
            return (-1, error.localizedDescription)
        }
    }

    private static func childEnvironment() -> [String: String] {
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
        if process?.isRunning == true { process?.terminate() }
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
