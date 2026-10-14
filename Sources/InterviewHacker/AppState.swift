import AppKit
import CoreGraphics
import Foundation
import InterviewHackerCore

@MainActor
final class AppState: ObservableObject {
    static let shared = AppState()

    @Published var section: AppSection = .assistant
    @Published var isRunning = false
    @Published var isPaused = false
    @Published var statusText = "就绪"
    @Published var errorMessage: String?
    @Published var entries: [TranscriptEntry] = []
    @Published var partialTranscript = ""
    @Published var transcriptionStatus = "等待系统音频"
    @Published var isChangingSession = false
    @Published var currentQuestion = ""
    @Published var pendingQuestion = ""
    @Published var answer = ""
    @Published var isAnswering = false
    @Published var questionInput = ""
    @Published var profile = UserDefaults.standard.string(forKey: "profile") ?? ""
    @Published var automaticAnswers = UserDefaults.standard.object(forKey: "automaticAnswers") as? Bool ?? true
    @Published var saveHistory = UserDefaults.standard.bool(forKey: "saveHistory")
    @Published var keepOnTop = UserDefaults.standard.object(forKey: "keepOnTop") as? Bool ?? true
    @Published var darkMode = UserDefaults.standard.bool(forKey: "darkMode")
    @Published var captureTargets: [CaptureTarget] = []
    @Published var selectedTargetID = UserDefaults.standard.string(forKey: "selectedTargetID") ?? ""
    @Published var codexLoginStatus = "正在检查 Codex…"
    @Published var codexVersion = "正在检测 Codex…"
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

    private let audio = AudioCaptureService()
    private let whisper = LocalWhisperService()
    private let codex = CodexBridge()
    private let hotkeys = HotkeyManager()
    private var detector = QuestionDetector()
    private var activeVoiceID: UUID?
    private var finalQueue: [(SpeechSegmentEvent, Bool)] = []
    private var finalTranscriptionTask: Task<Void, Never>?
    private var finalVoiceIDs: Set<UUID> = []
    private var isStopping = false
    private var modelDownloadTask: Task<Void, Never>?
    private var answerTask: Task<Void, Never>?
    private var requestID = UUID()
    private var sessionStartedAt: Date?
    private var sessionAnswers: [String] = []
    private var answerTurns: [AnswerTurn] = []
    private var lastQuestionUsesScreenshot = false
    private var pendingAutomaticQuestion: (question: String, context: String)?

    private init() {
        Diagnostics.record("app.launch", ["bundle_id": Bundle.main.bundleIdentifier ?? "unknown"])
        loadSessions()
        audio.onSegment = { [weak self] event in
            Task { @MainActor in self?.receiveSpeechEvent(event) }
        }
        audio.onCaptureError = { [weak self] message in
            Task { @MainActor in
                self?.errorMessage = "系统音频采集已停止：\(message)"
                self?.statusText = "采集错误"
                Task { await self?.refreshTargets() }
                Task { await self?.stopSession() }
            }
        }
        registerHotkeys()
        Task { await refreshTargets(); await refreshCodexStatus(); await refreshModelStatus() }
    }

    var isModelInstalled: Bool { modelIsReady }

    var readinessText: String {
        if !capturePermissionGranted { return "需要允许录屏与系统音频" }
        if !CodexBridge.isChatGPTLogin(codexLoginStatus) { return "请先用 ChatGPT 账号登录 Codex" }
        if LocalWhisperService.findExecutable() == nil { return "应用内语音运行时不可用" }
        if !isModelInstalled { return "需要下载或修复本地语音模型" }
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
        let version = await Task.detached(priority: .utility) { CodexBridge.version() ?? "未检测到" }.value
        codexLoginStatus = await CodexBridge.loginStatus()
        codexVersion = version
    }

    func refreshModelStatus() async {
        let path = modelPath
        modelIsReady = await Task.detached(priority: .utility) {
            LocalWhisperService.isValidModel(at: path)
        }.value
        if !modelIsReady, FileManager.default.fileExists(atPath: path) {
            modelDownloadStatus = "模型文件不完整或校验失败。删除后重新下载即可修复。"
        }
    }

    func startSession() async {
        guard !isRunning, !isChangingSession else { return }
        guard LocalWhisperService.findExecutable() != nil else { errorMessage = AppFailure.noTranscriber.localizedDescription; section = .settings; return }
        guard isModelInstalled else { errorMessage = AppFailure.modelMissing.localizedDescription; section = .settings; return }
        guard CodexBridge.isChatGPTLogin(codexLoginStatus) else { errorMessage = AppFailure.codexNotSignedIn.localizedDescription; section = .settings; return }
        guard !selectedTargetID.isEmpty else { errorMessage = "请先选择屏幕或窗口作为音频捕获目标。"; section = .settings; return }
        isChangingSession = true
        defer { isChangingSession = false }
        do {
            isPreparingModel = true
            statusText = "正在加载本地语音模型…"
            modelLoadSeconds = try await whisper.prepare(modelPath: modelPath)
            isPreparingModel = false
            let startedAt = Date()
            do {
                try await audio.startCapture(targetID: selectedTargetID)
            } catch {
                throw AppFailure.capture("无法捕获所选音源。请在“系统设置 → 隐私与安全性 → 屏幕与系统音频录制”允许 InterviewHacker，并确认目标窗口仍打开。\n\(error.localizedDescription)")
            }
            isRunning = true
            isPaused = false
            statusText = "正在聆听 · 仅本地转录"
            sessionStartedAt = startedAt
            entries = []
            sessionAnswers = []
            detector = QuestionDetector()
            answerTurns.removeAll()
            finalVoiceIDs.removeAll()
            isStopping = false
            transcriptionStatus = "等待系统音频"
            pendingAutomaticQuestion = nil
            errorMessage = nil
        } catch {
            isPreparingModel = false
            errorMessage = "启动辅助失败：\(error.localizedDescription)"
            statusText = "启动失败"
        }
    }

    func pauseSession() async {
        guard isRunning && !isPaused && !isChangingSession else { return }
        isChangingSession = true
        defer { isChangingSession = false }
        isPaused = true
        statusText = "正在暂停…"
        partialTranscript = ""
        let pending = await audio.stopCapture()
        for event in pending where event.kind == .final {
            enqueueFinal(event)
        }
        await finalTranscriptionTask?.value
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
            _ = try await whisper.prepare(modelPath: modelPath)
            try await audio.startCapture(targetID: selectedTargetID)
            isPaused = false
            statusText = "正在聆听 · 仅本地转录"
            transcriptionStatus = "等待系统音频"
            errorMessage = nil
        } catch {
            errorMessage = "恢复辅助失败。请确认系统录制权限仍有效，并且所选窗口仍存在。\n\(error.localizedDescription)"
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
        let pending = await audio.stopCapture()
        for event in pending where event.kind == .final {
            enqueueFinal(event, shouldDetectQuestion: false)
        }
        await finalTranscriptionTask?.value
        cancelAnswer()
        await whisper.shutdown()
        isRunning = false
        isPaused = false
        statusText = "已停止"
        partialTranscript = ""
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
        requestID = UUID()
        answerTask?.cancel()
        codex.cancel()
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
                regionCaptureImage = try await ScreenshotService.captureDisplay(displayID)
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
        guard !isDownloadingModel else { return }
        isDownloadingModel = true
        modelDownloadProgress = 0
        modelDownloadStatus = "正在从 Hugging Face 下载约 547 MiB；下载后会校验文件。"
        modelDownloadTask = Task {
            do {
                let destination = URL(fileURLWithPath: modelPath)
                try await LocalWhisperService.downloadModel(to: destination) { [weak self] progress in
                    Task { @MainActor in
                        self?.modelDownloadProgress = progress
                        let amount = Int(Double(LocalWhisperService.modelBytes) * progress)
                        self?.modelDownloadStatus = "正在下载：\(ByteCountFormatter.string(fromByteCount: Int64(amount), countStyle: .file)) / 547 MiB"
                    }
                }
                modelIsReady = true
                modelDownloadStatus = "模型已下载并通过 SHA-1 完整性校验。"
            } catch {
                modelDownloadStatus = error is CancellationError ? "下载已取消；未完成的文件不会作为可用模型保留。" : "下载失败：\(error.localizedDescription) 可点击重试。"
            }
            isDownloadingModel = false
            modelDownloadTask = nil
        }
    }

    func cancelModelDownload() {
        modelDownloadTask?.cancel()
    }

    func deleteModel() {
        Task {
            await whisper.shutdown()
            try? FileManager.default.removeItem(atPath: modelPath)
            try? FileManager.default.removeItem(atPath: modelPath + ".partial")
            modelIsReady = false
            modelDownloadStatus = "模型已删除。"
        }
    }

    func setProfile(_ text: String) {
        profile = text
        UserDefaults.standard.set(text, forKey: "profile")
    }

    func updateSettings() {
        UserDefaults.standard.set(automaticAnswers, forKey: "automaticAnswers")
        UserDefaults.standard.set(saveHistory, forKey: "saveHistory")
        UserDefaults.standard.set(keepOnTop, forKey: "keepOnTop")
        UserDefaults.standard.set(darkMode, forKey: "darkMode")
        UserDefaults.standard.set(selectedTargetID, forKey: "selectedTargetID")
        UserDefaults.standard.set(modelPath, forKey: "modelPath")
        UserDefaults.standard.set(recognitionLanguage, forKey: "recognitionLanguage")
        NSApp.windows.forEach { $0.level = keepOnTop ? .floating : .normal }
    }

    func saveTranscriptExport() {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.plainText]
        panel.nameFieldStringValue = "InterviewHacker-Transcript-\(Date.now.formatted(.iso8601.year().month().day())).txt"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let text = entries.map { "[\($0.date.formatted(date: .omitted, time: .shortened))] \($0.source): \($0.text)" }.joined(separator: "\n")
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
        NSPasteboard.general.setString(answer, forType: .string)
    }

    func closeError() { errorMessage = nil }

    func prepareForTermination() {
        finalTranscriptionTask?.cancel()
        answerTask?.cancel()
        codex.cancel()
        whisper.shutdownImmediately()
        if let screenshotURL { try? FileManager.default.removeItem(at: screenshotURL) }
    }

    private func receiveSpeechEvent(_ event: SpeechSegmentEvent) {
        guard isRunning else { return }
        Diagnostics.record(event.kind == .final ? "voice.final" : "voice.partial", ["voice_id": event.id.uuidString, "samples": event.samples.count, "start_sample": event.startSample, "end_sample": event.endSample])
        if event.kind == .partial {
            guard !isPaused, !isStopping else { return }
            activeVoiceID = event.id
            if finalTranscriptionTask == nil { transcriptionStatus = "正在采集语音…" }
        } else {
            activeVoiceID = event.id
            partialTranscript = ""
            enqueueFinal(event, shouldDetectQuestion: !isStopping)
        }
    }

    private func enqueueFinal(_ event: SpeechSegmentEvent, shouldDetectQuestion: Bool = true) {
        guard finalVoiceIDs.insert(event.id).inserted else { return }
        if finalQueue.count >= 3 {
            let removed = finalQueue.removeFirst()
            finalVoiceIDs.remove(removed.0.id)
            Diagnostics.record("asr.dropped", ["voice_id": removed.0.id.uuidString])
            errorMessage = "本机识别速度低于输入速度，已丢弃一段较早的待处理音频。请缩短会议片段或关闭其他占用较高的程序。"
        }
        finalQueue.append((event, shouldDetectQuestion))
        transcriptionStatus = "本地识别中 · \(finalQueue.count) 段待处理"
        Diagnostics.record("asr.queued", ["voice_id": event.id.uuidString, "queue_count": finalQueue.count])
        guard finalTranscriptionTask == nil else { return }
        finalTranscriptionTask = Task {
            while !finalQueue.isEmpty {
                let next = finalQueue.removeFirst()
                await transcribeFinal(next.0, shouldDetectQuestion: next.1)
            }
            finalTranscriptionTask = nil
        }
    }

    private func transcribeFinal(_ event: SpeechSegmentEvent, shouldDetectQuestion: Bool) async {
        defer {
            if finalQueue.isEmpty { transcriptionStatus = isPaused ? "已暂停采集" : "等待系统音频" }
        }
        do {
            var text = try await whisper.transcribe(event.samples, modelPath: modelPath, language: recognitionLanguage)
            guard isRunning else { return }
            if let previous = entries.last {
                text = TranscriptDeduplicator.removeRepeatedPrefix(previous: previous.text, incoming: text)
            }
            if activeVoiceID == event.id {
                partialTranscript = ""
                activeVoiceID = nil
            }
            guard !text.isEmpty else { return }
            let origin = event.streamStartedAt ?? sessionStartedAt ?? Date()
            let startedAt = origin.addingTimeInterval(Double(event.startSample) / 16_000)
            let endedAt = origin.addingTimeInterval(Double(event.endSample) / 16_000)
            let entry = TranscriptEntry(date: startedAt, source: CaptureSource.system.rawValue, text: text, isFinal: true, startedAt: startedAt, endedAt: endedAt)
            entries.append(entry)
            Diagnostics.record("transcript.committed", ["voice_id": event.id.uuidString, "characters": text.count])
            if entries.count > 400 { entries.removeFirst(entries.count - 400) }
            let recent = TranscriptContextBuilder.recent(entries.map { (date: $0.date, text: $0.text) })
            if automaticAnswers, shouldDetectQuestion, !isStopping, let question = detector.detect(text) {
                ask(question, screenshot: nil, recentContext: recent, isAutomatic: true)
            }
        } catch is CancellationError {
        } catch {
            guard isRunning else { return }
            errorMessage = "本地识别失败：\(error.localizedDescription)"
        }
    }

    private func ask(_ question: String, screenshot: URL?, recentContext: String? = nil, isAutomatic: Bool = false) {
        if isAutomatic && isAnswering {
            pendingAutomaticQuestion = (question, recentContext ?? "")
            Diagnostics.record("answer.queued")
            return
        }
        cancelAnswer()
        pendingQuestion = question
        isAnswering = true
        statusText = "Codex 正在整理回答…"
        let id = UUID()
        requestID = id
        Diagnostics.record("answer.started", ["request_id": id.uuidString, "automatic": isAutomatic, "screenshot": screenshot != nil, "question_characters": question.count])

        let context = recentContext ?? TranscriptContextBuilder.recent(entries.map { (date: $0.date, text: $0.text) })
        let prompt = AnswerPromptBuilder.make(question: question, context: context, profile: profile, turns: answerTurns, hasScreenshot: screenshot != nil)

        answerTask = Task {
            defer {
                if let screenshot, screenshot.pathExtension.lowercased() == "jpg" {
                    try? FileManager.default.removeItem(at: screenshot)
                }
            }
            do {
                let result = try await codex.run(prompt: prompt, imageURL: screenshot) { _ in }
                guard requestID == id else { return }
                answer = result
                currentQuestion = question
                pendingQuestion = ""
                lastQuestionUsesScreenshot = screenshot != nil
                isAnswering = false
                Diagnostics.record("answer.completed", ["request_id": id.uuidString, "answer_characters": result.count])
                statusText = isRunning ? (isPaused ? "已暂停" : "正在聆听 · 仅本地转录") : "就绪"
                sessionAnswers.append(result)
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
                Diagnostics.record("answer.failed", ["request_id": id.uuidString])
                statusText = "Codex 错误"
                errorMessage = error.localizedDescription
            }
        }
    }

    private func registerHotkeys() {
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
            .regenerate: { [weak self] in self?.regenerate() }
        ])
    }

    private func persistActiveSession() {
        guard let startedAt = sessionStartedAt, !entries.isEmpty else { return }
        let saved = SavedSession(startedAt: startedAt, endedAt: Date(), entries: entries, answers: sessionAnswers)
        if let idx = savedSessions.firstIndex(where: { $0.startedAt == startedAt }) {
            savedSessions[idx] = saved
        } else {
            savedSessions.insert(saved, at: 0)
        }
        persistSessions()
    }

    private func persistSessions() {
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
}
