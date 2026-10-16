import Foundation
import InterviewHackerCore
import Observation

/// The local decision models. Laya (multilingual, 322M, a bidirectional encoder) is
/// the default: about 10 ms a decision on Apple silicon and well under 1 GB. The
/// StartLux-Decision v1.1 Instant sizes are larger LLM-based models: more general
/// knowledge, much more memory and time. (AutoThink builds think for seconds, too slow
/// for live judgments.)
enum DecisionModelVariant: String, CaseIterable, Identifiable, Codable {
    case laya = "Laya", b08 = "0.8B", b2 = "2B", b4 = "4B", b9 = "9B", b27 = "27B"
    var id: String { rawValue }

    var isLaya: Bool { self == .laya }
    var title: String { isLaya ? "Laya 多语言 322M" : "StartLux \(rawValue)" }
    func displayName(bits: Int) -> String { isLaya ? title : "\(title) \(bits)-bit" }

    /// Estimated download in GB for 8-bit / 4-bit (Laya ships FP16 only; StartLux from
    /// the published GGUF sizes, the MLX builds keep a small vision tower in BF16). The
    /// real size is read from Hugging Face before downloading.
    var estimatedGB: (eight: Double, four: Double) {
        switch self {
        case .laya: (0.7, 0.7)
        case .b08: (0.9, 0.6)
        case .b2: (2.2, 1.4)
        case .b4: (4.7, 2.9)
        case .b9: (10, 6)
        case .b27: (29, 17)
        }
    }

    /// Memory while running, GB, for 8-bit / 4-bit (Laya: 0.69 GB measured peak plus Python).
    var estimatedRAM: (eight: Double, four: Double) {
        if isLaya { return (1, 1) }
        let size = estimatedGB
        return (size.eight + 1.5, size.four + 1.5)
    }

    /// StartLux Decision Index 0.3 (vendor-reported, English benchmarks); Laya publishes
    /// different benchmarks, so it has none.
    var decisionIndex: Double? {
        switch self {
        case .laya: nil
        case .b08: 53.75
        case .b2: 59.14
        case .b4: 67.88
        case .b9: 69.96
        case .b27: 72.83
        }
    }

    var note: String {
        switch self {
        case .laya: "推荐：最小最快（M3 Max 实测约 7 ms 一次），支持中文等 100 多种语言"
        case .b08: "StartLux 最小版，准确率明显低"
        case .b2: "8 GB 内存也能跑；每次判断要几百毫秒"
        case .b4: "比 Laya 大得多、慢得多，常识更多；16 GB 以上内存"
        case .b9: "更准一些，需要 32 GB 以上内存"
        case .b27: "最准但太大太慢，不适合实时判断"
        }
    }

    var license: String { isLaya ? "Apache-2.0（可商用）" : "CC BY-NC 4.0（仅限非商业用途）" }

    func repository(bits: Int) -> String {
        isLaya ? "aac6fef/laya-multilingual-mlx" : "startlux-models/StartLux-Decision-v1.1-\(rawValue)-Instant-MLX-\(bits)bit"
    }

    /// Laya on every Mac: live judgments run on every sentence, so speed and memory
    /// matter most. The in-app evaluation shows whether a StartLux size does better.
    static var recommended: (variant: DecisionModelVariant, bits: Int) { (.laya, 8) }
}

/// Runs the decision model for the app: locally (Laya through `laya-mlx`, or
/// `startlux-decide serve --backend mlx`, installed into Application Support on first
/// use) or any `/v1/systemone` server the user points at. Every call has a short deadline; on a miss the caller keeps its
/// rule-based result, so a slow or missing model never delays an answer.
@MainActor
@Observable
final class DecisionService {
    enum Source: String, CaseIterable, Identifiable {
        case local, server
        var id: String { rawValue }
        var title: String { self == .local ? "在这台 Mac 上运行" : "连接已有的服务" }
    }

    enum Phase: Equatable {
        case notInstalled
        case installing(String, Double?)
        case stopped
        case starting
        case running
        case failed(String)
    }

    // Settings
    var enabled = UserDefaults.standard.bool(forKey: "decisionEnabled")
    var source = Source(rawValue: UserDefaults.standard.string(forKey: "decisionSource") ?? "") ?? .local
    var variant = DecisionModelVariant(rawValue: UserDefaults.standard.string(forKey: "decisionVariant") ?? "") ?? DecisionModelVariant.recommended.variant
    var bits = UserDefaults.standard.object(forKey: "decisionBits") as? Int ?? 8
    var serverURL = UserDefaults.standard.string(forKey: "decisionServerURL") ?? ""
    var serverKey = KeychainStore.read(DecisionService.keyAccount)
    var useForQuestions = UserDefaults.standard.object(forKey: "decisionForQuestions") as? Bool ?? true
    var useForWebSearch = UserDefaults.standard.object(forKey: "decisionForWebSearch") as? Bool ?? true
    var useForEcho = UserDefaults.standard.object(forKey: "decisionForEcho") as? Bool ?? true
    var useForKnowledge = UserDefaults.standard.object(forKey: "decisionForKnowledge") as? Bool ?? true

    // State
    private(set) var phase: Phase = .notInstalled
    private(set) var latencyMs: Double?
    private(set) var downloadBytes: Int64?
    private(set) var evaluation = ""
    private(set) var isEvaluating = false

    static let keyAccount = "decision.api"
    @ObservationIgnored private var server: Process?
    @ObservationIgnored private var installTask: Task<Void, Never>?
    private let port = 8790
    private let session: URLSession = {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 10
        return URLSession(configuration: configuration)
    }()

    init() { refreshPhase() }

    // MARK: Availability

    var isReady: Bool { enabled && phase == .running }
    var handlesQuestions: Bool { isReady && useForQuestions }
    var handlesWebSearch: Bool { isReady && useForWebSearch }
    var handlesEcho: Bool { isReady && useForEcho }
    var handlesKnowledge: Bool { isReady && useForKnowledge }

    private var baseURL: URL? {
        switch source {
        case .local: URL(string: "http://127.0.0.1:\(port)")
        case .server:
            URL(string: serverURL.trimmingCharacters(in: .whitespacesAndNewlines).trimmingCharacters(in: CharacterSet(charactersIn: "/")))
        }
    }

    // MARK: Paths

    private var root: URL { AppPaths.root.appendingPathComponent("decision", isDirectory: true) }
    /// One Python environment per family: their dependencies do not overlap.
    private func venv(_ variant: DecisionModelVariant) -> URL { root.appendingPathComponent(variant.isLaya ? "laya-venv" : "venv", isDirectory: true) }
    /// The file whose presence means the runtime is installed.
    private func runtime(_ variant: DecisionModelVariant) -> URL {
        venv(variant).appendingPathComponent(variant.isLaya ? "lib/.laya-mlx-installed" : "bin/startlux-decide")
    }
    private var layaServer: URL { root.appendingPathComponent("laya_server.py") }
    private func modelFolder(_ variant: DecisionModelVariant, bits: Int) -> URL {
        root.appendingPathComponent("models", isDirectory: true).appendingPathComponent(variant.repository(bits: bits).components(separatedBy: "/").last ?? "model", isDirectory: true)
    }
    var logFile: URL { root.appendingPathComponent("decision.log") }

    func isInstalled(_ variant: DecisionModelVariant, bits: Int) -> Bool {
        let folder = modelFolder(variant, bits: bits)
        guard FileManager.default.fileExists(atPath: runtime(variant).path) else { return false }
        if variant.isLaya {
            let files = (try? FileManager.default.contentsOfDirectory(atPath: folder.path)) ?? []
            return files.contains { $0.hasSuffix(".safetensors") }
        }
        return FileManager.default.fileExists(atPath: folder.appendingPathComponent("decision_config.json").path)
    }

    var installedModels: [(variant: DecisionModelVariant, bits: Int)] {
        DecisionModelVariant.allCases.flatMap { variant in
            (variant.isLaya ? [8] : [8, 4]).filter { isInstalled(variant, bits: $0) }.map { (variant: variant, bits: $0) }
        }
    }

    func refreshPhase() {
        if case .installing = phase { return }
        // A remote server is never “installed”; it is stopped until it answers /health.
        guard source == .local else { if phase == .notInstalled { phase = .stopped }; return }
        if server?.isRunning == true { return }
        phase = isInstalled(variant, bits: bits) ? .stopped : .notInstalled
    }

    func save() {
        let defaults = UserDefaults.standard
        defaults.set(enabled, forKey: "decisionEnabled")
        defaults.set(source.rawValue, forKey: "decisionSource")
        defaults.set(variant.rawValue, forKey: "decisionVariant")
        defaults.set(bits, forKey: "decisionBits")
        defaults.set(serverURL, forKey: "decisionServerURL")
        defaults.set(useForQuestions, forKey: "decisionForQuestions")
        defaults.set(useForWebSearch, forKey: "decisionForWebSearch")
        defaults.set(useForEcho, forKey: "decisionForEcho")
        defaults.set(useForKnowledge, forKey: "decisionForKnowledge")
        KeychainStore.write(serverKey.trimmingCharacters(in: .whitespacesAndNewlines), account: Self.keyAccount)
    }

    // MARK: Start / stop

    /// At launch: bring the model up in the background when it is switched on.
    func startIfEnabled() {
        guard enabled, !DemoMode.isEnabled else { return }
        Task {
            try? await Task.sleep(for: .seconds(6))
            await start()
        }
    }

    func start() async {
        save()
        switch source {
        case .server:
            phase = .starting
            phase = await healthy() ? .running : .failed("连接不上 \(serverURL)（GET /health 失败）")
            if phase == .running { await measureLatency() }
        case .local:
            if server?.isRunning == true, phase == .running { return }
            guard isInstalled(variant, bits: bits) else { phase = .notInstalled; return }
            stop()
            phase = .starting
            do {
                try launchServer()
            } catch {
                phase = .failed("启动失败：\(error.localizedDescription)")
                return
            }
            // Loading the weights takes a while the first time.
            for _ in 0..<240 {
                try? await Task.sleep(for: .milliseconds(500))
                guard server?.isRunning == true else {
                    phase = .failed("服务意外退出，详情见日志 \(logFile.path)")
                    return
                }
                if await healthy() { phase = .running; await measureLatency(); return }
            }
            phase = .failed("服务启动超时（2 分钟），详情见日志")
        }
    }

    func stop() {
        if let server, server.isRunning { server.terminate() }
        server = nil
        refreshPhase()
    }

    private func launchServer() throws {
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        if !FileManager.default.fileExists(atPath: logFile.path) { FileManager.default.createFile(atPath: logFile.path, contents: nil) }
        let log = try FileHandle(forWritingTo: logFile)
        log.seekToEndOfFile()
        let process = Process()
        let folder = modelFolder(variant, bits: bits).path
        if variant.isLaya {
            try Self.layaServerScript.write(to: layaServer, atomically: true, encoding: .utf8)
            process.executableURL = venv(variant).appendingPathComponent("bin/python")
            process.arguments = ["-I", layaServer.path, folder, String(port)]
        } else {
            process.executableURL = runtime(variant)
            process.arguments = ["serve", "--model", folder, "--backend", "mlx",
                                 "--host", "127.0.0.1", "--port", String(port), "--think-mode", "never", "--no-images"]
        }
        process.environment = ProcessInfo.processInfo.environment.merging(["PYTHONUNBUFFERED": "1", "HF_HUB_OFFLINE": "1"]) { $1 }
        process.standardOutput = log
        process.standardError = log
        process.terminationHandler = { _ in try? log.close() }
        try process.run()
        server = process
        Diagnostics.record("decision.server_started", ["variant": variant.rawValue, "bits": bits])
    }

    // MARK: Install

    /// Creates a Python environment with `startlux-decide[mlx]` and downloads the model.
    func install() {
        guard installTask == nil else { return }
        save()
        let variant = variant, bits = bits
        let repository = variant.repository(bits: bits)
        let folder = modelFolder(variant, bits: bits)
        installTask = Task { [weak self] in
            guard let self else { return }
            defer { self.installTask = nil }
            do {
                try FileManager.default.createDirectory(at: self.root, withIntermediateDirectories: true)
                if !FileManager.default.fileExists(atPath: self.runtime(variant).path) {
                    self.phase = .installing("正在准备 Python 环境…", nil)
                    try await self.createEnvironment(variant)
                }
                self.phase = .installing("正在查询模型大小…", nil)
                let expected = await self.repositorySize(repository)
                self.downloadBytes = expected
                self.phase = .installing("正在下载 \(repository)" + (expected.map { "（\(ByteCountFormatter.string(fromByteCount: $0, countStyle: .file))）" } ?? ""), 0)
                try await self.download(repository, to: folder, expected: expected, python: self.venv(variant).appendingPathComponent("bin/python").path)
                guard self.isInstalled(variant, bits: bits) else { throw DecisionError.command("下载完成但模型目录不完整，详情见日志") }
                Diagnostics.record("decision.installed", ["variant": variant.rawValue, "bits": bits])
                self.phase = .stopped
                await self.start()
            } catch is CancellationError {
                self.phase = self.isInstalled(variant, bits: bits) ? .stopped : .notInstalled
            } catch {
                self.phase = .failed(error.localizedDescription)
                Diagnostics.record("decision.install_failed")
            }
        }
    }

    func cancelInstall() {
        installTask?.cancel()
    }

    /// Removes one downloaded model (the Python environment stays).
    func delete(_ variant: DecisionModelVariant, bits: Int) {
        if variant == self.variant, bits == self.bits { stop() }
        try? FileManager.default.removeItem(at: modelFolder(variant, bits: bits))
        refreshPhase()
    }

    private func createEnvironment(_ variant: DecisionModelVariant) async throws {
        let venv = self.venv(variant)
        let python = venv.appendingPathComponent("bin/python").path
        let packages = variant.isLaya ? Self.layaPackages : Self.startLuxPackages
        switch try await Self.findPython(minimumMinor: variant.isLaya ? 11 : 10) {
        case let .uv(uv):
            try await run(uv, ["venv", "--python", "3.12", venv.path])
            try await run(uv, ["pip", "install", "--python", python] + packages)
        case let .python(path):
            try await run(path, ["-m", "venv", venv.path])
            try await run(python, ["-m", "pip", "install", "--upgrade", "pip"])
            try await run(python, ["-m", "pip", "install"] + packages)
        }
        if variant.isLaya {
            try await run(python, ["-I", "-c", "import laya_mlx, huggingface_hub"])
            FileManager.default.createFile(atPath: runtime(variant).path, contents: nil)
        }
        guard FileManager.default.fileExists(atPath: runtime(variant).path) else { throw DecisionError.command("安装后没有找到运行环境，详情见日志") }
    }

    static let startLuxPackages = ["startlux-decide[mlx] @ git+https://github.com/StartLuxLabs/Startlux-Decision", "huggingface_hub>=0.30"]
    /// Native MLX port of Laya: no PyTorch or Transformers.
    static let layaPackages = ["laya-mlx>=0.4,<0.5", "huggingface_hub>=0.34"]

    enum PythonTool { case uv(String), python(String) }

    /// `uv` (which fetches a Python itself), else Python 3.`minimumMinor`+ from Homebrew or python.org.
    static func findPython(minimumMinor: Int) async throws -> PythonTool {
        try await Task.detached(priority: .utility) {
            let home = FileManager.default.homeDirectoryForCurrentUser.path
            for uv in ["/opt/homebrew/bin/uv", "/usr/local/bin/uv", "\(home)/.local/bin/uv", "\(home)/.cargo/bin/uv"] where FileManager.default.isExecutableFile(atPath: uv) {
                return .uv(uv)
            }
            let candidates = ["3.13", "3.12", "3.11", "3.10"].flatMap { ["/opt/homebrew/bin/python\($0)", "/usr/local/bin/python\($0)", "/Library/Frameworks/Python.framework/Versions/\($0)/bin/python3"] }
                + ["/opt/homebrew/bin/python3", "/usr/local/bin/python3"]
            for path in candidates where FileManager.default.isExecutableFile(atPath: path) {
                if let result = try? Shell.run(path, ["-c", "import sys; print(sys.version_info >= (3, \(minimumMinor)))"], timeout: 10), result.output.contains("True") {
                    return .python(path)
                }
            }
            throw DecisionError.command("需要 Python 3.\(minimumMinor) 或更新版本。在终端运行 brew install uv（或 brew install python@3.12），然后再点“下载并启动”。")
        }.value
    }

    private func download(_ repository: String, to folder: URL, expected: Int64?, python: String) async throws {
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let script = "from huggingface_hub import snapshot_download; snapshot_download(repo_id='\(repository)', local_dir=r'''\(folder.path)''')"
        let progress = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(for: .seconds(1))
                guard let self, let expected, expected > 0 else { continue }
                let done = Self.folderSize(folder)
                if case let .installing(message, _) = self.phase {
                    self.phase = .installing(message, min(0.99, Double(done) / Double(expected)))
                }
            }
        }
        defer { progress.cancel() }
        try await run(python, ["-I", "-c", script])
    }

    /// Sum of the repository's files from the Hugging Face API, when reachable.
    private func repositorySize(_ repository: String) async -> Int64? {
        guard let url = URL(string: "https://huggingface.co/api/models/\(repository)?blobs=true") else { return nil }
        guard let (data, response) = try? await session.data(from: url), (response as? HTTPURLResponse)?.statusCode == 200,
              let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let siblings = root["siblings"] as? [[String: Any]] else { return nil }
        let total = siblings.compactMap { ($0["size"] as? NSNumber)?.int64Value }.reduce(0, +)
        return total > 0 ? total : nil
    }

    nonisolated private static func folderSize(_ folder: URL) -> Int64 {
        guard let enumerator = FileManager.default.enumerator(at: folder, includingPropertiesForKeys: [.fileSizeKey]) else { return 0 }
        var total: Int64 = 0
        for case let url as URL in enumerator { total += Int64((try? url.resourceValues(forKeys: [.fileSizeKey]).fileSize) ?? 0) }
        return total
    }

    /// Runs an install step with its output in the log; cancellable.
    private func run(_ executable: String, _ arguments: [String]) async throws {
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        if !FileManager.default.fileExists(atPath: logFile.path) { FileManager.default.createFile(atPath: logFile.path, contents: nil) }
        let log = try FileHandle(forWritingTo: logFile)
        log.seekToEndOfFile()
        log.write(Data("\n$ \(executable) \(arguments.joined(separator: " "))\n".utf8))
        let process = Process()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = arguments
        process.standardOutput = log
        process.standardError = log
        process.standardInput = FileHandle.nullDevice
        let status: Int32 = try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { continuation in
                process.terminationHandler = { finished in
                    try? log.close()
                    continuation.resume(returning: finished.terminationStatus)
                }
                do { try process.run() } catch { try? log.close(); continuation.resume(throwing: error) }
            }
        } onCancel: {
            if process.isRunning { process.terminate() }
        }
        try Task.checkCancellation()
        guard status == 0 else {
            let tail = (try? String(contentsOf: logFile, encoding: .utf8)).map { String($0.suffix(400)) } ?? ""
            throw DecisionError.command("“\((executable as NSString).lastPathComponent) \(arguments.first ?? "")”失败（\(status)）：\(tail)")
        }
    }

    // MARK: Calls

    private func healthy() async -> Bool {
        guard let url = baseURL?.appendingPathComponent("health") else { return false }
        var request = URLRequest(url: url, timeoutInterval: 3)
        if source == .server, !serverKey.isEmpty { request.setValue("Bearer \(serverKey)", forHTTPHeaderField: "Authorization") }
        guard let (_, response) = try? await session.data(for: request) else { return false }
        return (response as? HTTPURLResponse)?.statusCode == 200
    }

    /// One request; nil on error or past the deadline.
    func decide(state: String, questions: [String: DecisionQuestion], deadline: Double) async -> [String: DecisionAnswer]? {
        guard enabled, let base = baseURL else { return nil }
        var request = URLRequest(url: base.appendingPathComponent("v1/systemone"), timeoutInterval: max(1, deadline + 0.5))
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        if source == .server, !serverKey.isEmpty { request.setValue("Bearer \(serverKey)", forHTTPHeaderField: "Authorization") }
        request.httpBody = DecisionProtocol.body(state: state, questions: questions)
        let session = session
        let started = Date()
        let data: Data? = try? await withThrowingTaskGroup(of: Data?.self) { group in
            group.addTask {
                let (data, response) = try await session.data(for: request)
                return (response as? HTTPURLResponse)?.statusCode == 200 ? data : nil
            }
            group.addTask { try await Task.sleep(for: .seconds(deadline)); return nil }
            defer { group.cancelAll() }
            return try await group.next() ?? nil
        }
        guard let data, let answers = DecisionProtocol.parse(data) else {
            Diagnostics.record("decision.missed", ["questions": questions.count])
            return nil
        }
        let ms = Date().timeIntervalSince(started) * 1000
        latencyMs = latencyMs.map { $0 * 0.8 + ms * 0.2 } ?? ms
        return answers
    }

    private func measureLatency() async {
        let task = DecisionTasks.webSearch("今天美元兑人民币汇率是多少？", today: AnswerPromptBuilder.todayText())
        latencyMs = nil
        for _ in 0..<3 { _ = await decide(state: task.state, questions: task.questions, deadline: 15) }
    }

    // MARK: The judgments

    func judgeUtterance(_ text: String, context: [String], previousQuestion: String?, mode: SessionMode, deadline: Double = 0.9) async -> DecisionTasks.UtteranceVerdict? {
        // Laya reads at most 1,024 tokens; a shorter context keeps the utterance itself in view.
        let recent = variant.isLaya && source == .local ? context.suffix(3).map { String($0.suffix(160)) } : context
        let task = DecisionTasks.utterance(text, context: recent, previousQuestion: previousQuestion, mode: mode)
        guard let answers = await decide(state: task.state, questions: task.questions, deadline: deadline) else { return nil }
        return DecisionTasks.utteranceVerdict(answers)
    }

    func needsWebSearch(_ question: String, deadline: Double = 1.2) async -> Double? {
        let task = DecisionTasks.webSearch(question, today: AnswerPromptBuilder.todayText())
        return await decide(state: task.state, questions: task.questions, deadline: deadline)?[DecisionTasks.Key.needsWeb]?.yes
    }

    func isUserSpeech(_ microphone: String, otherSide: [String], deadline: Double = 1.5) async -> Double? {
        let task = DecisionTasks.echo(microphone: microphone, otherSide: otherSide)
        return await decide(state: task.state, questions: task.questions, deadline: deadline)?[DecisionTasks.Key.isUser]?.yes
    }

    func sameQuestion(_ first: String, _ second: String) async -> Double? {
        let task = DecisionTasks.sameQuestion(first, second)
        return await decide(state: task.state, questions: task.questions, deadline: 4)?[DecisionTasks.Key.same]?.yes
    }

    // MARK: Rules vs model

    /// Runs the built-in labeled Chinese / English samples through the local rules and
    /// the model and reports accuracy and speed, so the choice is made on real numbers.
    func evaluate(mode: SessionMode) async {
        guard isReady, !isEvaluating else { return }
        isEvaluating = true
        defer { isEvaluating = false }
        evaluation = "正在评测…"
        let samples = QuestionEvalCorpus.all
        let threshold = QuestionIntentClassifier.Thresholds.forMode(mode).actionable
        var ruleRight = 0, modelRight = 0, modelAnswered = 0
        var latencies: [Double] = []
        for (index, sample) in samples.enumerated() {
            let rule = QuestionIntentClassifier.assess(sample.text, mode: mode)
            if (rule.intent.isActionable && rule.score >= threshold) == sample.isQuestion { ruleRight += 1 }
            let started = Date()
            if let verdict = await judgeUtterance(sample.text, context: [], previousQuestion: nil, mode: mode, deadline: 10) {
                latencies.append(Date().timeIntervalSince(started) * 1000)
                modelAnswered += 1
                if verdict.shouldAnswer == sample.isQuestion { modelRight += 1 }
            }
            if index % 10 == 0 { evaluation = "正在评测 \(index + 1)/\(samples.count)…" }
        }
        var webRule = 0, webModel = 0
        for sample in WebSearchNeed.evalSamples {
            if WebSearchNeed.assess(sample.question).needsSearch == sample.needsSearch { webRule += 1 }
            if let yes = await needsWebSearch(sample.question, deadline: 10), (yes >= 0.5) == sample.needsSearch { webModel += 1 }
        }
        latencies.sort()
        let median = latencies.isEmpty ? 0 : latencies[latencies.count / 2]
        let p90 = latencies.isEmpty ? 0 : latencies[min(latencies.count - 1, latencies.count * 9 / 10)]
        func percent(_ right: Int, _ total: Int) -> String { total == 0 ? "–" : String(format: "%.0f%%", Double(right) * 100 / Double(total)) }
        evaluation = """
        是不是在问我（\(samples.count) 句）：本地规则 \(percent(ruleRight, samples.count))，模型 \(percent(modelRight, samples.count))\(modelAnswered < samples.count ? "（\(samples.count - modelAnswered) 句没有回复）" : "")
        要不要联网（\(WebSearchNeed.evalSamples.count) 题）：本地规则 \(percent(webRule, WebSearchNeed.evalSamples.count))，模型 \(percent(webModel, WebSearchNeed.evalSamples.count))
        模型耗时：中位数 \(Int(median)) ms，90% 在 \(Int(p90)) ms 内
        """
        Diagnostics.record("decision.evaluated", ["rule": ruleRight, "model": modelRight, "count": samples.count, "median_ms": median])
    }
}

extension DecisionService {
    /// `laya-mlx` has no server of its own: this one serves the same `/v1/systemone`
    /// format as StartLux and Jev on 127.0.0.1, with the Python standard library only.
    /// The model loads (and answers one warm-up question) before /health turns green.
    static let layaServerScript = #"""
    import json
    import sys
    import threading
    from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

    import laya_mlx as laya

    model_dir, port = sys.argv[1], int(sys.argv[2])
    agent = laya.load(model_dir)
    lock = threading.Lock()


    def clean(questions):
        # Laya takes criteria as labels or {label: description}; empty descriptions become the label.
        result = {}
        for key, question in questions.items():
            question = dict(question)
            criteria = question.get("criteria")
            if isinstance(criteria, dict):
                question["criteria"] = {label: (text or label) for label, text in criteria.items()}
            result[key] = question
        return result


    with lock:
        agent.predict("你好", {"warmup": {"type": "noul", "instructions": "Is this a greeting?"}})


    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *args):
            pass

        def send(self, status, body):
            data = json.dumps(body, ensure_ascii=False).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)

        def do_GET(self):
            if self.path == "/health":
                self.send(200, {"status": "ok"})
            else:
                self.send(404, {"error": "not found"})

        def do_POST(self):
            if self.path != "/v1/systemone":
                return self.send(404, {"error": "not found"})
            try:
                length = int(self.headers.get("Content-Length") or 0)
                body = json.loads(self.rfile.read(length) or b"{}")
                with lock:
                    result = agent.predict(body.get("state", ""), clean(body.get("questions", {})))
                self.send(200, result)
            except Exception as error:
                self.send(400, {"error": str(error)})


    print(f"laya server on 127.0.0.1:{port} with {model_dir}", flush=True)
    ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()
    """#
}

enum DecisionError: LocalizedError {
    case command(String)
    var errorDescription: String? {
        switch self {
        case let .command(message): message
        }
    }
}
