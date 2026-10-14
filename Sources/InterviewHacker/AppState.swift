import AppKit
import SwiftUI
import AVFoundation
import CoreGraphics
import Foundation
import InterviewHackerCore
import UniformTypeIdentifiers

@MainActor
final class AppState: ObservableObject {
    static let shared = AppState()

    @Published var recognitionVocabulary = UserDefaults.standard.string(forKey: "recognitionVocabulary") ?? ""
    @Published var transcriptionBackend = UserDefaults.standard.string(forKey: "transcriptionBackend") ?? "whisper"
    @Published var sessionMode = SessionMode(rawValue: UserDefaults.standard.string(forKey: "sessionMode") ?? "meeting") ?? .meeting
    @Published var answerStyle = AnswerStyle(rawValue: UserDefaults.standard.string(forKey: "answerStyle") ?? "concise") ?? .concise
    @Published var answerPreferences = UserDefaults.standard.string(forKey: "answerPreferences") ?? ""
    @Published var documents: [ContextDocument] = []
    @Published var presets: [SessionPreset] = []
    @Published var isImportingDocuments = false
    @Published var isIndexingDocuments = false
    @Published var selectedExcerpts: [ContextExcerpt] = []
    @Published var lastASRSeconds: Double?
    @Published var lastRTF: Double?
    @Published var lastAnswerSeconds: Double?
    @Published var droppedAudioSegments = 0
    @Published var teleprompterFontSize = UserDefaults.standard.object(forKey: "teleprompterFontSize") as? Double ?? 22
    @Published var capturePrivacy = UserDefaults.standard.object(forKey: "capturePrivacy") as? Bool ?? true
    @Published var customShortcuts: [String: ShortcutBinding] = {
        guard let data = UserDefaults.standard.data(forKey: "customShortcuts"),
              let values = try? JSONDecoder().decode([String: ShortcutBinding].self, from: data) else { return [:] }
        return values
    }()
    var contextIndex = SessionContextIndex()
    var documentRevision = UUID()
    var importTask: Task<Void, Never>?
    var manualTriggerTask: Task<Void, Never>?
    private var microphoneTranscriber: (any LiveTranscribing)?
    private var liveTranscriber: (any LiveTranscribing)?
    var audioEpoch = UUID()
    private var forceTriggerID = UUID()

    // Answer engines
    @Published var answerBackend = AnswerBackend(rawValue: UserDefaults.standard.string(forKey: "answerBackend") ?? "") ?? .codexAppServer
    @Published var apiBaseURL = UserDefaults.standard.string(forKey: "apiBaseURL") ?? "https://api.openai.com/v1"
    @Published var apiModel = UserDefaults.standard.string(forKey: "apiModel") ?? ""
    @Published var apiKey = KeychainStore.read("answer.api")
    @Published var appServerNote = ""
    @Published var lastFirstTokenSeconds: Double?
    @Published var answerHistory: [AnswerRecord] = []
    /// nil shows the latest answer; otherwise an index into `answerHistory`.
    @Published var historyCursor: Int?
    // Question candidates
    @Published var liveCandidate: QuestionCandidate?
    @Published var suggestedQuestions: [QuestionCandidate] = []
    @Published var verifyUncertainWithModel = UserDefaults.standard.bool(forKey: "verifyUncertainWithModel")
    // Cloud speech recognition (explicit opt-in)
    @Published var cloudProvider = CloudSTTProvider(rawValue: UserDefaults.standard.string(forKey: "cloudProvider") ?? "") ?? .soniox
    @Published var cloudModel = UserDefaults.standard.string(forKey: "cloudModel") ?? ""
    @Published var cloudKey = ""
    @Published var cloudConsent = UserDefaults.standard.bool(forKey: "cloudConsent")
    @Published var liveWhisperPreview = UserDefaults.standard.object(forKey: "liveWhisperPreview") as? Bool ?? true
    // Overlay
    @Published var teleprompterOpacity = UserDefaults.standard.object(forKey: "teleprompterOpacity") as? Double ?? 0.96
    @Published var clickThrough = false
    @Published var shareTestImage: NSImage?
    @Published var lastStartupSeconds: Double?
    @Published var keepRecognizerWarm = UserDefaults.standard.object(forKey: "keepRecognizerWarm") as? Bool ?? true
    @Published var companionPort = UserDefaults.standard.object(forKey: "companionPort") as? Int ?? 8_765
    var whisperIdleTask: Task<Void, Never>?
    var systemRecoveryTask: Task<Void, Never>?
    @Published var companionEnabled = false
    @Published var companionAddress: String?
    @Published var companionStatus = "未开启"

    @Published var section: AppSection = .assistant
    @Published var isRunning = false
    @Published var isPaused = false
    @Published var statusText = "就绪"
    @Published var errorMessage: String?
    @Published var entries: [TranscriptEntry] = []
    @Published var partialTranscript = ""
    @Published var microphonePartialTranscript = ""
    @Published var microphoneEnabled = UserDefaults.standard.object(forKey: "microphoneEnabled") as? Bool ?? true
    @Published var discoveredModels: [DiscoveredWhisperModel] = []
    @Published var isScanningModels = false
    @Published var modelScanStatus = ""

    func scanLocalModels() async {
        guard !isScanningModels else { return }
        isScanningModels = true
        defer { isScanningModels = false }
        discoveredModels = await Task.detached(priority: .utility) { LocalModelScanner.scan() }.value
        modelScanStatus = "发现 \(discoveredModels.count) 个本地 Whisper 模型；导入时检查完整性。"
    }
    func useDiscoveredModel(_ model: DiscoveredWhisperModel) {
        guard !modelControlsDisabled else { return }
        if model.url.deletingLastPathComponent().standardizedFileURL == LocalWhisperService.modelsDirectory.standardizedFileURL {
            selectedModelID = WhisperModel.matching(model.url)?.id ?? "local:" + model.url.lastPathComponent
            useSelectedModel()
        } else { importModel(from: model.url) }
    }
    func openMicrophoneSettings() {
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Microphone") { NSWorkspace.shared.open(url) }
    }

    @Published var transcriptionStatus = "等待系统音频"
    @Published var isChangingSession = false
    @Published var currentQuestion = ""
    @Published var pendingQuestion = ""
    @Published var answer = ""
    @Published var answerDraft = ""
    var displayedAnswer: String {
        if isAnswering && !answerDraft.isEmpty && historyCursor == nil { return answerDraft }
        if let historyCursor, answerHistory.indices.contains(historyCursor) { return answerHistory[historyCursor].answer }
        return answer
    }
    var displayedQuestion: String {
        if isAnswering && !answerDraft.isEmpty && historyCursor == nil { return pendingQuestion }
        if let historyCursor, answerHistory.indices.contains(historyCursor) { return answerHistory[historyCursor].question }
        return currentQuestion
    }
    var historyPositionText: String? {
        guard answerHistory.count > 1 else { return nil }
        return "\((historyCursor ?? answerHistory.count - 1) + 1)/\(answerHistory.count)"
    }
    @Published var isAnswering = false
    @Published var questionInput = ""
    @Published var profile = UserDefaults.standard.string(forKey: "profile") ?? ""
    @Published var automaticAnswers = UserDefaults.standard.object(forKey: "automaticAnswers") as? Bool ?? true
    @Published var saveHistory = UserDefaults.standard.bool(forKey: "saveHistory")
    @Published var keepOnTop = UserDefaults.standard.object(forKey: "keepOnTop") as? Bool ?? true
    /// "system", "light" or "dark". Migrates the old darkMode switch.
    @Published var appearance = UserDefaults.standard.string(forKey: "appearance")
        ?? (UserDefaults.standard.object(forKey: "darkMode") as? Bool == true ? "dark" : "system")
    var colorScheme: ColorScheme? { appearance == "dark" ? .dark : appearance == "light" ? .light : nil }
    func applyAppearance() {
        NSApp.appearance = appearance == "dark" ? NSAppearance(named: .darkAqua) : appearance == "light" ? NSAppearance(named: .aqua) : nil
    }
    @Published var captureTargets: [CaptureTarget] = []
    @Published var selectedTargetID = UserDefaults.standard.string(forKey: "selectedTargetID") ?? ""
    @Published var codexModels: [CodexModelOption] = []
    @Published var codexModelID = UserDefaults.standard.string(forKey: "codexModelID") ?? ""
    @Published var codexReasoning = UserDefaults.standard.string(forKey: "codexReasoning") ?? "low"
    @Published var lastAnswerModel: String?
    var codexEfforts: [String] {
        codexModels.first(where: { $0.id == codexModelID })?.efforts ?? ["low", "medium", "high"]
    }
    func refreshCodexModels() async {
        let models = await Task.detached(priority: .utility) { CodexModelCatalog.load() }.value
        codexModels = models
        if codexModelID.isEmpty, let first = models.first {
            codexModelID = first.id
        }
        normalizeCodexSelection()
    }
    func normalizeCodexSelection() {
        if let model = codexModels.first(where: { $0.id == codexModelID }), !model.efforts.contains(codexReasoning) {
            codexReasoning = model.defaultEffort
        }
        updateSettings()
    }
    @Published var codexLoginStatus = "正在检查 Codex…"
    @Published var codexVersion = "正在检测 Codex…"
    @Published var selectedModelID = WhisperModel.matching(URL(fileURLWithPath: UserDefaults.standard.string(forKey: "modelPath") ?? LocalWhisperService.defaultModelURL.path))?.id ?? WhisperModel.defaultID
    @Published var localModelPaths: [String] = []
    @Published var isImportingModel = false
    var modelControlsDisabled: Bool { isRunning || isChangingSession || isPreparingModel || isDownloadingModel || isImportingModel }
    var selectedModel: WhisperModel? { WhisperModel.named(selectedModelID) }
    var selectedModelURL: URL {
        if let model = selectedModel { return LocalWhisperService.modelsDirectory.appendingPathComponent(model.filename) }
        return LocalWhisperService.modelsDirectory.appendingPathComponent(String(selectedModelID.dropFirst("local:".count)))
    }
    var selectedModelExists: Bool { FileManager.default.fileExists(atPath: selectedModelURL.path) }
    func reloadLocalModels() {
        localModelPaths = ((try? FileManager.default.contentsOfDirectory(at: LocalWhisperService.modelsDirectory, includingPropertiesForKeys: nil)) ?? [])
            .filter { $0.lastPathComponent.hasPrefix("imported-") && $0.pathExtension == "bin" }.map(\.lastPathComponent).sorted()
    }
    func useSelectedModel() {
        guard !modelControlsDisabled else { return }
        let url = selectedModelURL
        isImportingModel = true
        Task {
            defer { isImportingModel = false }
            do {
                try await Task.detached(priority: .utility) { try WhisperModelFile.verify(at: url, expected: WhisperModel.matching(url)) }.value
                modelPath = url.path
                if url.lastPathComponent.contains(".en") { recognitionLanguage = "en" }
                modelIsReady = true
                modelDownloadStatus = "已切换到 \(url.lastPathComponent)"
                updateSettings()
                await prewarmRecognizer()
            } catch { modelDownloadStatus = error.localizedDescription }
        }
    }
    func importLocalModel() {
        guard !modelControlsDisabled else { return }
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [UTType(filenameExtension: "bin")!, UTType(filenameExtension: "pt")!]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        panel.directoryURL = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".cache/whisper")
        panel.message = "选择 Whisper .bin 或 .pt 模型；原文件会保留，.pt 将在本机转换。"
        guard panel.runModal() == .OK, let source = panel.url else { return }
        importModel(from: source)
    }
    func importModel(from source: URL) {
        guard !modelControlsDisabled else { return }
        isImportingModel = true
        modelDownloadStatus = "正在导入 \(source.lastPathComponent)；.pt 转换可能需要一些时间…"
        Task {
            defer { isImportingModel = false }
            do {
                let url = try await Task.detached(priority: .utility) { try LocalModelImporter.importModel(from: source) }.value
                do {
                    _ = try await whisper.prepare(modelPath: url.path)
                    await whisper.shutdown()
                } catch {
                    try? FileManager.default.removeItem(at: url)
                    try? FileManager.default.removeItem(at: url.appendingPathExtension("sha256"))
                    throw error
                }
                reloadLocalModels()
                selectedModelID = "local:" + url.lastPathComponent
                modelPath = url.path
                if url.lastPathComponent.contains(".en") { recognitionLanguage = "en" }
                modelIsReady = true
                modelDownloadStatus = "已导入，通过完整性校验和本地加载验证。"
                await scanLocalModels()
                updateSettings()
            } catch { modelDownloadStatus = error.localizedDescription }
        }
    }
    @Published var modelPath = UserDefaults.standard.string(forKey: "modelPath") ?? LocalWhisperService.defaultModelURL.path
    @Published var modelDownloadStatus = ""
    @Published var isDownloadingModel = false
    @Published var modelDownloadProgress = 0.0
    @Published var modelIsReady = false
    @Published var isPreparingModel = false
    @Published var modelLoadSeconds: Double?
    @Published var recognitionLanguage = UserDefaults.standard.string(forKey: "recognitionLanguage") ?? "auto"
    @Published var screenshotPreview: NSImage?
    @Published var screenshotURL: URL?
    @Published var screenshotWindows: [ScreenshotWindow] = []
    @Published var isWindowPickerPresented = false
    @Published var screenshotDisplays: [ScreenshotDisplay] = []
    @Published var selectedScreenshotDisplayID: UInt32?
    @Published var regionCaptureImage: NSImage?
    @Published var isRegionCapturePresented = false
    @Published var savedSessions: [SavedSession] = []
    @Published var shortcutErrors: [String] = []
    @Published var capturePermissionGranted = false
    @Published var capturePermissionStatus = "正在检查录屏与系统音频权限…"

    let audio = AudioCaptureService()
    let microphone = MicrophoneCaptureService()
    let whisper = LocalWhisperService()
    private let codex = CodexBridge()
    private let hotkeys = HotkeyManager()
    var tracker = QuestionCandidateTracker()
    let appServer = CodexAppServerClient()
    let apiClient = OpenAICompatibleClient()
    var candidateTicker: Task<Void, Never>?
    var appleUtteranceCounter: [String: Int] = [:]
    var isPreviewTranscribing = false
    private var firstTokenAt: Date?
    var activeVoiceID: UUID?
    var finalQueue: [(SpeechSegmentEvent, Bool)] = []
    var finalTranscriptionTask: Task<Void, Never>?
    private var finalVoiceIDs: Set<UUID> = []
    var isStopping = false
    private var modelDownloadTask: Task<Void, Never>?
    private var answerTask: Task<Void, Never>?
    private var requestID = UUID()
    var sessionStartedAt: Date?
    private var sessionAnswers: [String] = []
    private var answerTurns: [AnswerTurn] = []
    private var lastQuestionUsesScreenshot = false
    private var pendingAutomaticQuestion: (question: String, context: String)?

    private init() {
        Diagnostics.record("app.launch", ["bundle_id": Bundle.main.bundleIdentifier ?? "unknown"])
        loadSessions()
        loadPresets()
        audio.onSegment = { [weak self] event in
            Task { @MainActor in self?.receiveSpeechEvent(event) }
        }
        audio.onCaptureError = { [weak self] message in
            Task { @MainActor in self?.recoverSystemCapture(message) }
        }
        applyAppearance()
        registerHotkeys()
        loadCloudKey()
        Diagnostics.pruneOldLogs()
        Task { await scanLocalModels() }
        Task {
            await refreshTargets(); await refreshCodexStatus(); await refreshModelStatus()
            // Warm the persistent Codex service so the first answer does not pay start-up cost.
            if answerBackend == .codexAppServer, CodexBridge.executableURL() != nil { await appServer.prewarm() }
            await prewarmRecognizer()
        }
    }

    var isModelInstalled: Bool { modelIsReady }

    var readinessText: String {
        if !capturePermissionGranted { return "需要允许录屏与系统音频" }
        if transcriptionBackend == "apple" {
            if !LiveTranscriberFactory.supported { return "Apple 流式识别需要 macOS 26" }
            if recognitionLanguage == "auto" { return "请为 Apple 识别选择中文或 English" }
        }
        if transcriptionBackend == "cloud" {
            if !cloudConsent { return "请在设置中确认云端识别的数据说明" }
            if cloudKey.isEmpty { return "请填写云端识别 API Key" }
        }
        if transcriptionBackend == "whisper", LocalWhisperService.findExecutable() == nil { return "应用内语音运行时不可用" }
        if transcriptionBackend == "whisper", !isModelInstalled { return "需要下载或修复本地语音模型" }
        if captureTargets.isEmpty || selectedTargetID.isEmpty { return "请检查屏幕与系统音频权限" }
        return "可以开始"
    }

    func refreshTargets() async {
        capturePermissionGranted = CGPreflightScreenCaptureAccess()
        Diagnostics.record("permission.checked", ["granted": capturePermissionGranted])
        guard capturePermissionGranted else {
            captureTargets = []
            selectedTargetID = ""
            capturePermissionStatus = "请允许当前 InterviewHacker 访问录屏与系统音频；构建脚本会复用同一签名身份。"
            return
        }
        do {
            captureTargets = try await AudioCaptureService.availableTargets()
            capturePermissionStatus = "已允许 · 已读取 \(captureTargets.count) 个捕获目标"
            Diagnostics.record("capture.targets", ["count": captureTargets.count])
            if captureTargets.contains(where: { $0.id == selectedTargetID }) == false {
                selectedTargetID = captureTargets.first?.id ?? ""
            }
        } catch {
            captureTargets = []
            selectedTargetID = ""
            capturePermissionStatus = "系统仍拒绝当前进程。请从固定安装路径重启应用后刷新。"
            errorMessage = "读取屏幕与音频目标失败。请在系统设置的“隐私与安全性 → 屏幕与系统音频录制”中允许 InterviewHacker。\n\(error.localizedDescription)"
        }
    }

    func requestCapturePermission() async {
        let granted = CGRequestScreenCaptureAccess()
        Diagnostics.record("permission.requested", ["granted": granted])
        await refreshTargets()
        if !capturePermissionGranted {
            openCapturePermissionSettings()
        }
    }

    func openCapturePermissionSettings() {
        guard let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture") else { return }
        NSWorkspace.shared.open(url)
    }

    func refreshCodexStatus() async {
        codex.invalidateLoginCache()
        let version = await Task.detached(priority: .utility) { CodexBridge.version() ?? "未检测到" }.value
        codexLoginStatus = await CodexBridge.loginStatus()
        codexVersion = version
        await refreshCodexModels()
    }

    func refreshModelStatus() async {
        reloadLocalModels()
        if URL(fileURLWithPath: modelPath).lastPathComponent.hasPrefix("imported-") {
            selectedModelID = "local:" + URL(fileURLWithPath: modelPath).lastPathComponent
        }
        let path = modelPath
        let ready = await Task.detached(priority: .utility) {
            LocalWhisperService.isValidModel(at: path)
        }.value
        guard modelPath == path else { return }
        modelIsReady = ready
        if !modelIsReady, FileManager.default.fileExists(atPath: path) {
            modelDownloadStatus = "模型文件不完整或校验失败。可重新下载或导入完整的本地模型。"
        }
    }

    func startSession() async {
        guard !isRunning, !isChangingSession else { return }
        guard transcriptionBackend != "whisper" || LocalWhisperService.findExecutable() != nil else { errorMessage = AppFailure.noTranscriber.localizedDescription; section = .settings; return }
        guard !isImportingModel, !isDownloadingModel else { return }
        guard transcriptionBackend != "whisper" || isModelInstalled else { errorMessage = AppFailure.modelMissing.localizedDescription; section = .settings; return }
        guard !selectedTargetID.isEmpty else { errorMessage = "请先选择屏幕或窗口作为音频捕获目标。"; section = .settings; return }
        cancelAnswer()
        currentQuestion = ""; answer = ""; pendingQuestion = ""; partialTranscript = ""
        microphonePartialTranscript = ""
        lastAnswerModel = nil
        clearScreenshot()
        selectedExcerpts = []; lastASRSeconds = nil; lastRTF = nil; lastAnswerSeconds = nil; droppedAudioSegments = 0
        finalQueue.removeAll(); finalVoiceIDs.removeAll()
        audioEpoch = UUID()
        isChangingSession = true
        defer { isChangingSession = false }
        do {
            let startupBegan = Date()
            whisperIdleTask?.cancel(); whisperIdleTask = nil
            if microphoneEnabled { try await MicrophoneCaptureService.authorize() }
            isPreparingModel = true
            if transcriptionBackend == "whisper" {
                statusText = whisper.isWarm(modelPath: modelPath) ? "正在启动…" : "正在加载本地语音模型（首次较慢，之后会常驻）…"
            }
            modelLoadSeconds = try await prepareTranscriber()
            Diagnostics.record("session.transcriber_ready", ["backend": transcriptionBackend, "seconds": Date().timeIntervalSince(startupBegan)])
            isPreparingModel = false
            let startedAt = Date()
            sessionStartedAt = startedAt
            entries = []
            sessionAnswers = []
            tracker = QuestionCandidateTracker(mode: sessionMode)
            liveCandidate = nil; suggestedQuestions = []; appleUtteranceCounter = [:]
            answerHistory = []; historyCursor = nil
            answerTurns.removeAll()
            if answerBackend == .codexAppServer { Task { await appServer.prewarm() } }
            isStopping = false
            isRunning = true
            do {
                try await audio.startCapture(targetID: selectedTargetID)
            } catch {
                throw AppFailure.capture("无法捕获所选音源。请在“系统设置 → 隐私与安全性 → 屏幕与系统音频录制”允许 InterviewHacker，并确认目标窗口仍打开。\n\(error.localizedDescription)")
            }
            if microphoneEnabled {
                do { try await microphone.startCapture() }
                catch { microphoneUnavailable("麦克风启动失败：\(error.localizedDescription)") }
            }
            isRunning = true
            isPaused = false
            lastStartupSeconds = Date().timeIntervalSince(startupBegan)
            Diagnostics.record("session.started", ["backend": transcriptionBackend, "seconds": lastStartupSeconds ?? -1])
            statusText = String(format: "正在聆听 · 启动 %.1f s", lastStartupSeconds ?? 0)
            isStopping = false
            transcriptionStatus = "等待系统音频"
            pendingAutomaticQuestion = nil
            errorMessage = nil
            startCandidateTicker()
        } catch {
            isRunning = false
            _ = await audio.stopCapture()
            _ = microphone.stopCapture()
            audio.setPCMHandler(nil)
            microphone.setPCMHandler(nil)
            await microphoneTranscriber?.cancel()
            microphoneTranscriber = nil
            await whisper.shutdown()
            await liveTranscriber?.cancel()
            liveTranscriber = nil
            isPreparingModel = false
            audioEpoch = UUID()
            finalTranscriptionTask?.cancel()
            finalTranscriptionTask = nil
            finalQueue.removeAll(); finalVoiceIDs.removeAll()
            sessionStartedAt = nil
            errorMessage = "启动辅助失败：\(error.localizedDescription)"
            statusText = "启动失败"
        }
    }

    func pauseSession() async {
        guard isRunning && !isPaused && !isChangingSession else { return }
        isChangingSession = true
        defer { isChangingSession = false }
        isPaused = true
        cancelAnswer()
        statusText = "正在暂停…"
        partialTranscript = ""
        microphonePartialTranscript = ""
        let systemPending = await audio.stopCapture()
        let pending = systemPending + microphone.stopCapture()
        if transcriptionBackend != "whisper" {
            await liveTranscriber?.finish(); await microphoneTranscriber?.finish()
            audio.setPCMHandler(nil); microphone.setPCMHandler(nil)
        }
        else {
            for event in pending where event.kind == .final { enqueueFinal(event, shouldDetectQuestion: false) }
            await drainTranscriptions()
        }
        statusText = "已暂停"
        transcriptionStatus = "已暂停采集"
    }

    func resumeSession() async {
        guard isRunning && isPaused && !isChangingSession else { return }
        guard !selectedTargetID.isEmpty else {
            errorMessage = "请先刷新并选择仍可用的屏幕或窗口。"
            section = .settings
            return
        }
        isChangingSession = true
        defer { isChangingSession = false }
        do {
            audioEpoch = UUID()
            _ = try await prepareTranscriber()
            try await audio.startCapture(targetID: selectedTargetID)
            if microphoneEnabled {
                do { try await microphone.startCapture() }
                catch { microphoneUnavailable("麦克风启动失败：\(error.localizedDescription)") }
            }
            isPaused = false
            statusText = "正在聆听 · 仅本地转录"
            transcriptionStatus = "等待系统音频"
            errorMessage = nil
        } catch {
            _ = await audio.stopCapture()
            _ = microphone.stopCapture()
            audio.setPCMHandler(nil); microphone.setPCMHandler(nil)
            await liveTranscriber?.cancel(); await microphoneTranscriber?.cancel()
            liveTranscriber = nil; microphoneTranscriber = nil
            errorMessage = "恢复辅助失败：\(error.localizedDescription)"
        }
    }

    func changeCaptureTarget() async {
        updateSettings()
        guard isRunning && !isPaused else { return }
        await pauseSession()
        await resumeSession()
    }

    func stopSession() async {
        guard (isRunning || isPaused) && !isChangingSession else { return }
        isChangingSession = true
        defer { isChangingSession = false }
        isStopping = true
        statusText = "正在处理最后片段…"
        pendingAutomaticQuestion = nil
        let systemPending = await audio.stopCapture()
        let pending = systemPending + microphone.stopCapture()
        if transcriptionBackend != "whisper" {
            await liveTranscriber?.finish(); await microphoneTranscriber?.finish()
            audio.setPCMHandler(nil); microphone.setPCMHandler(nil)
        }
        else {
            for event in pending where event.kind == .final { enqueueFinal(event, shouldDetectQuestion: false) }
            await drainTranscriptions()
        }
        audioEpoch = UUID()
        liveTranscriber = nil
        microphoneTranscriber = nil
        candidateTicker?.cancel(); candidateTicker = nil
        liveCandidate = nil
        cancelAnswer()
        // Keep the model loaded so the next start is instant; unload after idling.
        scheduleWhisperIdleShutdown()
        isRunning = false
        isPaused = false
        statusText = "已停止"
        partialTranscript = ""
        microphonePartialTranscript = ""
        activeVoiceID = nil
        transcriptionStatus = "已停止"
        if saveHistory { persistActiveSession() }
        sessionStartedAt = nil
        isStopping = false
    }

    func answerInput() {
        let question = questionInput.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !question.isEmpty else { return }
        questionInput = ""
        ask(question, screenshot: nil)
    }

    func answerScreenshot() {
        guard let screenshotURL else { return }
        let typed = questionInput.trimmingCharacters(in: .whitespacesAndNewlines)
        let question = typed.isEmpty ? "请阅读截图中的题目或知识点并给出简洁回答。若文字、代码或公式看不清，请明确指出，不要猜测。" : typed
        questionInput = ""
        do {
            ask(question, screenshot: try ScreenshotService.optimizedImageURL(from: screenshotURL))
        } catch {
            errorMessage = "截图处理失败：\(error.localizedDescription)"
        }
    }

    func regenerate() {
        guard !currentQuestion.isEmpty else { return }
        do {
            let image = lastQuestionUsesScreenshot ? try screenshotURL.map { try ScreenshotService.optimizedImageURL(from: $0) } : nil
            ask(currentQuestion, screenshot: image)
        } catch {
            errorMessage = "截图处理失败：\(error.localizedDescription)"
        }
    }

    func cancelAnswer() {
        answerDraft = ""
        forceTriggerID = UUID()
        manualTriggerTask?.cancel()
        manualTriggerTask = nil
        requestID = UUID()
        answerTask?.cancel()
        codex.cancel()
        appServer.cancelActiveTurn()
        isAnswering = false
        pendingQuestion = ""
        pendingAutomaticQuestion = nil
        Diagnostics.record("answer.cancelled")
        statusText = isRunning ? (isPaused ? "已暂停" : "正在聆听 · 仅本地转录") : "就绪"
    }

    func captureScreenshot(_ kind: ScreenshotKind) {
        if kind == .window {
            presentWindowPicker()
            return
        }
        Task { await presentRegionPicker() }
    }

    private func presentRegionPicker() async {
        clearScreenshot()
        errorMessage = nil
        Diagnostics.record("screenshot.capture.started", ["mode": "region"])
        do {
            let displays = try await ScreenshotService.availableDisplays()
            guard let first = displays.first else {
                throw AppFailure.screenshot("没有找到可截图的显示器。请检查系统录屏权限后重试。")
            }
            screenshotDisplays = displays
            selectedScreenshotDisplayID = first.id
            regionCaptureImage = try await ScreenshotService.captureDisplay(first.id)
            isRegionCapturePresented = true
        } catch {
            errorMessage = "截图失败：\(error.localizedDescription)"
            Diagnostics.record("screenshot.capture.failed", ["mode": "region"])
        }
    }

    func selectScreenshotDisplay(_ displayID: UInt32) {
        selectedScreenshotDisplayID = displayID
        Task {
            do {
                let image = try await ScreenshotService.captureDisplay(displayID)
                guard selectedScreenshotDisplayID == displayID, isRegionCapturePresented else { return }
                regionCaptureImage = image
            } catch {
                errorMessage = "切换显示器失败：\(error.localizedDescription)"
            }
        }
    }

    func commitRegionScreenshot(_ normalizedRect: CGRect) {
        guard let regionCaptureImage else { return }
        do {
            let result = try ScreenshotService.crop(image: regionCaptureImage, normalizedRect: normalizedRect)
            self.regionCaptureImage = nil
            isRegionCapturePresented = false
            screenshotURL = result.url
            screenshotPreview = result.preview
            section = .assistant
            statusText = isRunning ? (isPaused ? "已暂停" : "正在聆听 · 仅本地转录") : "就绪"
            Diagnostics.record("screenshot.capture.completed", ["mode": "region", "width": Int(result.preview.size.width), "height": Int(result.preview.size.height)])
        } catch {
            errorMessage = "区域截图失败：\(error.localizedDescription)"
        }
    }

    func cancelRegionPicker() {
        regionCaptureImage = nil
        isRegionCapturePresented = false
        Diagnostics.record("screenshot.capture.cancelled", ["mode": "region"])
    }

    private func presentWindowPicker() {
        do {
            let windows = try ScreenshotService.availableWindows()
            screenshotWindows = windows
            guard !windows.isEmpty else {
                errorMessage = "没有找到可截图的窗口。请确认系统已允许 InterviewHacker 录屏，并先打开要分析的页面。"
                return
            }
            isWindowPickerPresented = true
        } catch {
            errorMessage = "读取窗口列表失败：\(error.localizedDescription)"
        }
    }

    func captureScreenshot(_ window: ScreenshotWindow) {
        isWindowPickerPresented = false
        clearScreenshot()
        errorMessage = nil
        Diagnostics.record("screenshot.capture.started", ["mode": "window", "owner": window.owner])
        Task {
            do {
                guard let result = try await ScreenshotService.captureWindow(window) else {
                    Diagnostics.record("screenshot.capture.cancelled", ["mode": "window"])
                    return
                }
                screenshotURL = result.url
                screenshotPreview = result.preview
                section = .assistant
                statusText = isRunning ? (isPaused ? "已暂停" : "正在聆听 · 仅本地转录") : "就绪"
                Diagnostics.record("screenshot.capture.completed", ["mode": "window", "owner": window.owner, "width": Int(result.preview.size.width), "height": Int(result.preview.size.height)])
            } catch {
                errorMessage = "截图失败：\(error.localizedDescription)"
                Diagnostics.record("screenshot.capture.failed", ["mode": "window", "owner": window.owner])
            }
        }
    }

    func cancelWindowPicker() {
        isWindowPickerPresented = false
        Diagnostics.record("screenshot.capture.cancelled", ["mode": "window_picker"])
    }

    func clearScreenshot() {
        if let screenshotURL { try? FileManager.default.removeItem(at: screenshotURL) }
        screenshotURL = nil
        screenshotPreview = nil
    }

    func downloadModel() {
        guard !modelControlsDisabled, let model = selectedModel else { return }
        let destination = selectedModelURL
        isDownloadingModel = true
        modelDownloadProgress = 0
        modelDownloadStatus = "正在下载 \(model.id) · \(model.sizeLabel)，完成后校验。"
        modelDownloadTask = Task {
            do {
                try await LocalWhisperService.downloadModel(model, to: destination) { [weak self] progress in
                    Task { @MainActor in
                        self?.modelDownloadProgress = progress
                        self?.modelDownloadStatus = "正在下载 \(model.id)：\(Int(progress * 100))% / \(model.sizeLabel)"
                    }
                }
                modelPath = destination.path
                if model.englishOnly { recognitionLanguage = "en" }
                modelIsReady = true
                updateSettings()
                modelDownloadStatus = "已下载并通过 SHA-256 校验，当前使用 \(model.id)。"
                await prewarmRecognizer()
            } catch {
                modelDownloadStatus = error is CancellationError ? "下载已取消。" : "下载失败：\(error.localizedDescription) 可点击重试。"
            }
            isDownloadingModel = false
            modelDownloadTask = nil
            await scanLocalModels()
        }
    }

    func cancelModelDownload() {
        modelDownloadTask?.cancel()
    }

    func deleteModel() {
        guard !modelControlsDisabled else { return }
        let url = selectedModelURL
        // Only managed copies are deleted; imported source files are never removed.
        guard url.deletingLastPathComponent().standardizedFileURL == LocalWhisperService.modelsDirectory.standardizedFileURL else { return }
        isImportingModel = true
        Task {
            defer { isImportingModel = false }
            await whisper.shutdown()
            do {
                try FileManager.default.removeItem(at: url)
                try? FileManager.default.removeItem(at: url.appendingPathExtension("sha256"))
                if modelPath == url.path { modelIsReady = false }
                if selectedModel == nil { selectedModelID = WhisperModel.defaultID }
                reloadLocalModels()
                modelDownloadStatus = "已删除应用内模型副本，原始导入文件保留。"
                await scanLocalModels()
            } catch { modelDownloadStatus = error.localizedDescription }
        }
    }

    func setProfile(_ text: String) {
        profile = text
        UserDefaults.standard.set(text, forKey: "profile")
    }

    func updateSettings() {
        UserDefaults.standard.set(codexModelID, forKey: "codexModelID")
        UserDefaults.standard.set(codexReasoning, forKey: "codexReasoning")
        UserDefaults.standard.set(automaticAnswers, forKey: "automaticAnswers")
        UserDefaults.standard.set(saveHistory, forKey: "saveHistory")
        UserDefaults.standard.set(keepOnTop, forKey: "keepOnTop")
        UserDefaults.standard.set(appearance, forKey: "appearance")
        UserDefaults.standard.set(microphoneEnabled, forKey: "microphoneEnabled")
        applyAppearance()
        UserDefaults.standard.set(selectedTargetID, forKey: "selectedTargetID")
        UserDefaults.standard.set(modelPath, forKey: "modelPath")
        UserDefaults.standard.set(recognitionLanguage, forKey: "recognitionLanguage")
        UserDefaults.standard.set(recognitionVocabulary, forKey: "recognitionVocabulary")
        UserDefaults.standard.set(transcriptionBackend, forKey: "transcriptionBackend")
        UserDefaults.standard.set(sessionMode.rawValue, forKey: "sessionMode")
        UserDefaults.standard.set(answerStyle.rawValue, forKey: "answerStyle")
        UserDefaults.standard.set(answerPreferences, forKey: "answerPreferences")
        for window in NSApp.windows where !(window is NSPanel) {
            window.level = keepOnTop ? .floating : .normal
        }
        TeleprompterController.shared.applyPrivacy(capturePrivacy)
    }

    func saveTranscriptExport() {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.plainText]
        panel.nameFieldStringValue = "InterviewHacker-Transcript-\(Date.now.formatted(.iso8601.year().month().day())).txt"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let text = entries.map { "[\($0.date.formatted(date: .omitted, time: .shortened))] \($0.roleLabel): \($0.text)" }.joined(separator: "\n")
        do {
            try text.write(to: url, atomically: true, encoding: .utf8)
        } catch {
            errorMessage = "导出失败：\(error.localizedDescription)"
        }
    }

    func deleteHistory() {
        savedSessions.removeAll()
        persistSessions()
    }

    func copyAnswer() {
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(displayedAnswer, forType: .string)
    }

    func closeError() { errorMessage = nil }

    func showPreviousAnswer() {
        guard !answerHistory.isEmpty else { return }
        historyCursor = max(0, (historyCursor ?? answerHistory.count - 1) - 1)
    }

    func showNextAnswer() {
        guard let current = historyCursor else { return }
        historyCursor = current + 1 >= answerHistory.count ? nil : current + 1
    }

    func prepareForTermination() {
        importTask?.cancel()
        manualTriggerTask?.cancel()
        audio.setPCMHandler(nil)
        microphone.setPCMHandler(nil)
        _ = microphone.stopCapture()
        finalTranscriptionTask?.cancel()
        answerTask?.cancel()
        codex.cancel()
        whisper.shutdownImmediately()
        appServer.shutdown()
        CompanionServer.shared.stop()
        if let screenshotURL { try? FileManager.default.removeItem(at: screenshotURL) }
    }

    private func receiveSpeechEvent(_ event: SpeechSegmentEvent) {
        guard isRunning, transcriptionBackend == "whisper" else { return }
        Diagnostics.record(event.kind == .final ? "voice.final" : "voice.partial", ["voice_id": event.id.uuidString, "source": event.source.rawValue, "samples": event.samples.count, "start_sample": event.startSample, "end_sample": event.endSample])
        if event.kind == .partial {
            guard !isPaused, !isStopping else { return }
            activeVoiceID = event.id
            if finalTranscriptionTask == nil { transcriptionStatus = "正在采集语音…" }
            transcribePreview(event)
        } else {
            activeVoiceID = event.id
            if event.source == .system { partialTranscript = "" } else { microphonePartialTranscript = "" }
            enqueueFinal(event, shouldDetectQuestion: !isStopping)
        }
    }

    func enqueueFinal(_ event: SpeechSegmentEvent, shouldDetectQuestion: Bool = true) {
        guard finalVoiceIDs.insert(event.id).inserted else { return }
        if finalQueue.filter({ $0.0.source == event.source }).count >= 3,
           let index = finalQueue.firstIndex(where: { $0.0.source == event.source }) {
            let removed = finalQueue.remove(at: index)
            finalVoiceIDs.remove(removed.0.id)
            Diagnostics.record("asr.dropped", ["voice_id": removed.0.id.uuidString])
            droppedAudioSegments += 1
            transcriptionStatus = "识别积压 · 已遗漏 \(droppedAudioSegments) 段"
        }
        finalQueue.append((event, shouldDetectQuestion))
        transcriptionStatus = "本地识别中 · \(finalQueue.count) 段待处理"
        Diagnostics.record("asr.queued", ["voice_id": event.id.uuidString, "queue_count": finalQueue.count])
        guard finalTranscriptionTask == nil else { return }
        finalTranscriptionTask = Task {
            while !finalQueue.isEmpty, !Task.isCancelled {
                let next = finalQueue.removeFirst()
                await transcribeFinal(next.0, shouldDetectQuestion: next.1)
            }
            finalTranscriptionTask = nil
        }
    }

    private func transcribeFinal(_ event: SpeechSegmentEvent, shouldDetectQuestion: Bool) async {
        let epoch = audioEpoch
        let began = Date()
        defer {
            finalVoiceIDs.remove(event.id)
            if finalQueue.isEmpty { transcriptionStatus = isPaused ? "已暂停采集" : "等待系统音频" }
        }
        do {
            var text = try await whisper.transcribe(event.samples, modelPath: modelPath, language: recognitionLanguage, vocabulary: recognitionVocabulary)
            guard isRunning, audioEpoch == epoch, !Task.isCancelled else { return }
            lastASRSeconds = Date().timeIntervalSince(began)
            lastRTF = (lastASRSeconds ?? 0) / max(0.001, Double(event.samples.count) / 16_000)
            let incomingStart = (event.streamStartedAt ?? sessionStartedAt ?? Date()).addingTimeInterval(Double(event.startSample) / 16_000)
            if let previous = entries.last(where: { $0.source == (event.source == .microphone ? CaptureSource.microphone.rawValue : CaptureSource.system.rawValue) }) {
                text = TranscriptDeduplicator.removeOverlappingPrefix(previous: previous.text, incoming: text, previousEnd: previous.endedAt, incomingStart: incomingStart)
            }
            if activeVoiceID == event.id {
                if event.source == .system { partialTranscript = "" } else { microphonePartialTranscript = "" }
                activeVoiceID = nil
            }
            guard !text.isEmpty else { return }
            let origin = event.streamStartedAt ?? sessionStartedAt ?? Date()
            let startedAt = origin.addingTimeInterval(Double(event.startSample) / 16_000)
            let endedAt = origin.addingTimeInterval(Double(event.endSample) / 16_000)
            let entry = TranscriptEntry(date: startedAt, source: event.source == .microphone ? CaptureSource.microphone.rawValue : CaptureSource.system.rawValue, text: text, isFinal: true, startedAt: startedAt, endedAt: endedAt)
            entries.append(entry)
            entries.sort { $0.date < $1.date }
            Diagnostics.record("transcript.committed", ["voice_id": event.id.uuidString, "source": event.source.rawValue, "characters": text.count])
            if entries.count > 400 { entries.removeFirst(entries.count - 400) }
            if event.source == .system {
                processRecognized(text, isFinal: true, key: event.id.uuidString, allowAutomatic: shouldDetectQuestion)
            }
        } catch is CancellationError {
        } catch {
            guard isRunning, !Task.isCancelled, !isStopping else { return }
            errorMessage = "本地识别失败：\(error.localizedDescription)"
        }
    }

    func ask(_ question: String, screenshot: URL?, recentContext: String? = nil, isAutomatic: Bool = false) {
        if isAutomatic && isAnswering {
            pendingAutomaticQuestion = (question, recentContext ?? "")
            Diagnostics.record("answer.queued")
            return
        }
        let backend = answerBackend
        if backend == .codexExec, !CodexBridge.isChatGPTLogin(codexLoginStatus) {
            errorMessage = "转写可继续；生成回答需要先登录 Codex。请在设置中刷新登录状态，或切换到 API 回答引擎。"
            return
        }
        if isIndexingDocuments { errorMessage = "资料索引正在更新，请完成后再生成回答。"; return }
        let selectedModel = (backend == .openAICompatible ? apiModel : codexModelID).trimmingCharacters(in: .whitespacesAndNewlines)
        guard !selectedModel.isEmpty else { errorMessage = "请在设置中选择回答模型。"; section = .settings; return }
        let selectedReasoning = codexReasoning
        cancelAnswer()
        historyCursor = nil
        pendingQuestion = question
        isAnswering = true
        firstTokenAt = nil
        statusText = backend == .openAICompatible ? "正在流式生成回答…" : "Codex 正在整理回答…"
        let id = UUID()
        requestID = id
        Diagnostics.record("answer.started", ["request_id": id.uuidString, "automatic": isAutomatic, "screenshot": screenshot != nil, "question_characters": question.count, "backend": backend.rawValue])

        let context = recentContext ?? TranscriptContextBuilder.recent(entries.map { (date: $0.date, text: $0.contextualText) })
        let excerpts = contextIndex.retrieve(question: question + "\n" + (answerTurns.last?.question ?? ""))
        selectedExcerpts = excerpts
        let parts = AnswerPromptBuilder.makeParts(question: question, context: context, profile: profile, turns: answerTurns, hasScreenshot: screenshot != nil, preferences: answerPreferences, mode: sessionMode, style: answerStyle, excerpts: excerpts)
        let request = AnswerRequest(system: parts.system, user: parts.user, model: selectedModel, effort: selectedReasoning, imageURL: screenshot)
        let answerStartedAt = Date()

        answerTask = Task {
            defer {
                if let screenshot, screenshot.pathExtension.lowercased() == "jpg" {
                    try? FileManager.default.removeItem(at: screenshot)
                }
            }
            do {
                let onDelta: @Sendable (String) -> Void = { [weak self] text in
                    Task { @MainActor in
                        guard let self, self.requestID == id, self.isAnswering else { return }
                        if self.firstTokenAt == nil {
                            self.firstTokenAt = Date()
                            self.lastFirstTokenSeconds = Date().timeIntervalSince(answerStartedAt)
                        }
                        // Deltas carry the full text so far; never step backwards.
                        if text.count >= self.answerDraft.count { self.answerDraft = text }
                    }
                }
                let result = try await runAnswer(request, backend: backend, onDelta: onDelta)
                guard requestID == id else { return }
                answer = result
                answerDraft = ""
                lastAnswerSeconds = Date().timeIntervalSince(answerStartedAt)
                if firstTokenAt == nil { lastFirstTokenSeconds = lastAnswerSeconds }
                lastAnswerModel = selectedModel
                currentQuestion = question
                pendingQuestion = ""
                lastQuestionUsesScreenshot = screenshot != nil
                isAnswering = false
                Diagnostics.record("answer.completed", ["request_id": id.uuidString, "answer_characters": result.count, "model": selectedModel, "reasoning": selectedReasoning, "backend": backend.rawValue, "first_token_seconds": lastFirstTokenSeconds ?? -1, "seconds": lastAnswerSeconds ?? -1])
                statusText = isRunning ? (isPaused ? "已暂停" : "正在聆听") : "就绪"
                sessionAnswers.append(result)
                answerHistory.append(AnswerRecord(question: question, answer: result, model: selectedModel, seconds: lastAnswerSeconds, firstTokenSeconds: lastFirstTokenSeconds, automatic: isAutomatic))
                if answerHistory.count > 100 { answerHistory.removeFirst(answerHistory.count - 100) }
                tracker.noteAnswered()
                answerTurns.append(AnswerTurn(question: question, answer: result))
                if answerTurns.count > 6 { answerTurns.removeFirst(answerTurns.count - 6) }
                errorMessage = nil
                if saveHistory { persistActiveSession() }
                if let next = pendingAutomaticQuestion, isRunning, !isPaused, !isStopping {
                    pendingAutomaticQuestion = nil
                    ask(next.question, screenshot: nil, recentContext: next.context, isAutomatic: true)
                }
            } catch is CancellationError {
                guard requestID == id else { return }
                isAnswering = false
                pendingQuestion = ""
            } catch {
                guard requestID == id else { return }
                isAnswering = false
                pendingQuestion = ""
                // Keep whatever streamed so far visible instead of losing it.
                if !answerDraft.isEmpty { answer = answerDraft; currentQuestion = question; answerDraft = "" }
                Diagnostics.record("answer.failed", ["request_id": id.uuidString, "backend": backend.rawValue])
                statusText = "回答出错"
                errorMessage = error.localizedDescription
            }
        }
    }

    private func runAnswer(_ request: AnswerRequest, backend: AnswerBackend, onDelta: @escaping @Sendable (String) -> Void) async throws -> String {
        switch backend {
        case .openAICompatible:
            return try await apiClient.answer(request, baseURL: apiBaseURL, apiKey: apiKey, onDelta: onDelta)
        case .codexExec:
            return try await codex.run(prompt: request.fullPrompt, model: request.model, reasoning: request.effort, imageURL: request.imageURL) { text in onDelta(text) }
        case .codexAppServer:
            do {
                let text = try await appServer.answer(request, onDelta: onDelta)
                appServerNote = ""
                return text
            } catch let AnswerEngineError.appServerUnavailable(detail) {
                // Older CLIs or a crashed service: fall back to the one-shot exec path.
                appServerNote = "常驻服务不可用，已临时改用 codex exec：\(detail)"
                Diagnostics.record("appserver.fallback")
                return try await codex.run(prompt: request.fullPrompt, model: request.model, reasoning: request.effort, imageURL: request.imageURL) { text in onDelta(text) }
            }
        }
    }

    func suspendShortcuts() { hotkeys.unregister() }

    func registerHotkeys() {
        shortcutErrors = hotkeys.register(actions: [
            .captureWindow: { [weak self] in self?.captureScreenshot(.window) },
            .captureRegion: { [weak self] in self?.captureScreenshot(.region) },
            .toggleSession: { [weak self] in
                guard let self else { return }
                Task { @MainActor in
                    if self.isRunning { await self.stopSession() }
                    else { await self.startSession() }
                }
            },
            .regenerate: { [weak self] in self?.regenerate() },
            .forceAnswer: { [weak self] in self?.forceAnswerCurrent() },
            .togglePause: { [weak self] in
                guard let self else { return }
                Task { if self.isPaused { await self.resumeSession() } else { await self.pauseSession() } }
            },
            .toggleTeleprompter: { TeleprompterController.shared.toggle() },
            .cancelAnswer: { [weak self] in self?.cancelAnswer() },
            .copyAnswer: { [weak self] in self?.copyAnswer() },
            .toggleClickThrough: { [weak self] in self?.toggleClickThrough() },
            .panicHide: { TeleprompterController.shared.panicHide() },
            .previousAnswer: { [weak self] in self?.showPreviousAnswer() },
            .nextAnswer: { [weak self] in self?.showNextAnswer() },
            .refineShorter: { [weak self] in self?.refine(.shorter) }
        ], overrides: customShortcuts)
    }

    private func persistActiveSession() {
        guard let startedAt = sessionStartedAt, !entries.isEmpty else { return }
        let saved = SavedSession(startedAt: startedAt, endedAt: Date(), entries: entries, answers: sessionAnswers, records: answerHistory, mode: sessionMode.rawValue)
        if let idx = savedSessions.firstIndex(where: { $0.startedAt == startedAt }) {
            savedSessions[idx] = saved
        } else {
            savedSessions.insert(saved, at: 0)
        }
        persistSessions()
    }

    func persistSessions() {
        guard let url = sessionsURL else { return }
        do {
            try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            let data = try JSONEncoder().encode(savedSessions)
            try data.write(to: url, options: .atomic)
        } catch {
            errorMessage = "保存本地记录失败：\(error.localizedDescription)"
        }
    }

    private func loadSessions() {
        guard let url = sessionsURL, let data = try? Data(contentsOf: url),
              let sessions = try? JSONDecoder().decode([SavedSession].self, from: data) else { return }
        savedSessions = sessions
    }

    private var sessionsURL: URL? {
        FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first?
            .appendingPathComponent("InterviewHacker/sessions.json")
    }

    private func prepareTranscriber() async throws -> TimeInterval {
        let captureEpoch = audioEpoch
        let receive: (SpeechSegmentEvent) -> Void = { [weak self] event in
            Task { @MainActor in
                guard let self, self.audioEpoch == captureEpoch else { return }
                self.receiveSpeechEvent(event)
            }
        }
        audio.onSegment = receive
        microphone.onSegment = receive
        // Device changes (headphones, AirPods, display sleep) must not end the session:
        // system audio reconnects automatically; the microphone follows the default input.
        audio.onCaptureError = { [weak self] message in
            Task { @MainActor in
                guard let self, self.audioEpoch == captureEpoch else { return }
                self.recoverSystemCapture(message)
            }
        }
        microphone.onCaptureError = { [weak self] message in
            Task { @MainActor in
                guard let self, self.audioEpoch == captureEpoch else { return }
                self.microphoneUnavailable(message)
            }
        }
        microphone.onDeviceChanged = { [weak self] name in
            Task { @MainActor in
                guard let self, self.audioEpoch == captureEpoch, self.isRunning else { return }
                self.transcriptionStatus = "麦克风已切换到 \(name)"
                Diagnostics.record("microphone.device_changed")
            }
        }
        if transcriptionBackend == "whisper" {
            audio.setPCMHandler(nil); microphone.setPCMHandler(nil)
            return try await whisper.prepare(modelPath: modelPath)
        }
        let factory: () -> (any LiveTranscribing)?
        if transcriptionBackend == "cloud" {
            guard cloudConsent, !cloudKey.isEmpty else { throw AppFailure.transcription("请先在设置中确认云端识别说明并填写 API Key。") }
            let provider = cloudProvider, key = cloudKey, model = cloudModel, vocabulary = recognitionVocabulary
            factory = { CloudStreamingTranscriber(provider: provider, apiKey: key, model: model, vocabulary: vocabulary) }
            statusText = "正在连接云端识别…"
        } else {
            factory = { LiveTranscriberFactory.make() }
            statusText = "正在准备 Apple Speech；首次使用可能下载语言资源…"
        }
        guard let service = factory() else {
            throw AppFailure.transcription("Apple 流式识别需要 macOS 26 与支持的设备；请切换 Whisper 或云端识别。")
        }
        let started = Date()
        liveTranscriber = service
        let micService = microphoneEnabled ? factory() : nil
        microphoneTranscriber = micService
        // System and microphone recognizers start in parallel (both on the main actor,
        // interleaving at their awaits).
        let micStart: Task<String?, Never>? = micService.map { mic in
            Task { @MainActor in
                do { try await self.startApple(mic, source: .microphone, epoch: captureEpoch); return nil }
                catch { return error.localizedDescription }
            }
        }
        try await startApple(service, source: .system, epoch: captureEpoch)
        if let micStart, let failure = await micStart.value {
            microphoneTranscriber = nil
            microphoneUnavailable("麦克风识别器启动失败：\(failure)")
        }
        audio.setPCMHandler(service.inputHandler())
        if let micService = microphoneTranscriber { microphone.setPCMHandler(micService.inputHandler()) }
        return Date().timeIntervalSince(started)
    }

    private func startApple(_ service: any LiveTranscribing, source: CaptureSource, epoch: UUID) async throws {
        try await service.start(language: recognitionLanguage) { [weak self] text, final, start, end in
            guard let self, self.audioEpoch == epoch, self.isRunning else { return }
            self.receiveStreamingResult(text, final: final, start: start, end: end, source: source)
        } onError: { [weak self] message in
            guard let self, self.audioEpoch == epoch else { return }
            self.errorMessage = message
            Task { await self.pauseSession() }
        }
    }

    private func drainTranscriptions() async {
        guard let task = finalTranscriptionTask else { return }
        let epoch = audioEpoch
        let deadline = Task { [weak self] in
            try? await Task.sleep(for: .seconds(3))
            guard !Task.isCancelled, let self, self.audioEpoch == epoch else { return }
            self.droppedAudioSegments += self.finalQueue.count + 1
            self.finalQueue.removeAll()
            task.cancel()
            self.whisper.cancelAll()
        }
        await task.value
        deadline.cancel()
        finalTranscriptionTask = nil
    }

    /// Uses the freshest text. A Whisper flush runs without stopping capture; it is
    /// explicitly not presented as instantaneous speech recognition.
    func forceAnswerCurrent() {
        // Priority: typed text → latest unanswered question candidate → recent transcript.
        if questionInput.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
           let candidate = [liveCandidate].compactMap({ $0 }).first(where: { $0.state != .triggered && $0.assessment.intent != .statement }) ?? suggestedQuestions.last,
           Date().timeIntervalSince(candidate.lastChanged) < 30 {
            answerCandidate(candidate)
            return
        }
        let cutoff = Date().addingTimeInterval(-45)
        let hasRecent = entries.contains { $0.source == CaptureSource.system.rawValue && ($0.endedAt ?? $0.date) > cutoff }
        if !questionInput.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || !partialTranscript.isEmpty || hasRecent {
            submitCurrentText()
            return
        }
        guard isRunning, !isPaused, transcriptionBackend == "whisper", manualTriggerTask == nil else {
            errorMessage = "还没有识别出的文字，请先说话或手动输入问题。"; return
        }
        let id = UUID(); forceTriggerID = id
        manualTriggerTask = Task {
            defer { if forceTriggerID == id { manualTriggerTask = nil } }
            if let event = await audio.flushUtterance() {
                enqueueFinal(event, shouldDetectQuestion: false)
                transcriptionStatus = "已提交当前语音；文字出现后可点击回答"
            }
        }
    }

    private func submitCurrentText() {
        let cutoff = Date().addingTimeInterval(-45)
        let recent = entries.filter { $0.source == CaptureSource.system.rawValue && ($0.endedAt ?? $0.date) > cutoff }.map(\.text)
        guard let question = ManualQuestionBuilder.make(typed: questionInput, partial: partialTranscript, recent: recent) else {
            errorMessage = "还没有最近 45 秒的转写。请先说话或手动输入问题。"; return
        }
        ask(question, screenshot: nil)
    }

}
