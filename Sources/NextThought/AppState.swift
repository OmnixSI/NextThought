import AppKit
import SwiftUI
import AVFoundation
import CoreGraphics
import Foundation
import NextThoughtCore
import Observation
import UniformTypeIdentifiers

/// App-wide state. Uses the Observation framework: a view re-renders only when a
/// property it actually read changes, so a partial transcript arriving 4×/s or an
/// answer streaming 15×/s redraws only the few views that show them.
@MainActor
@Observable
final class AppState {
    static let shared = {
        _ = AppPreferences.standard
        return AppState()
    }()

    // MARK: Session context (the active workspace)
    var recognitionVocabulary = UserDefaults.standard.string(forKey: "recognitionVocabulary") ?? ""
    var transcriptionBackend = AppState.loadTranscriptionBackend()
    let documentPreparation = DocumentPreparation()
    let screenContextService = ScreenContextService()
    let answerCoordinator = AnswerCoordinator()
    let liveTranslation = LiveTranslationCoordinator()
    /// 会议自动收尾 (AppState+AutoEnd.swift).
    let autoEnd = MeetingAutoEnd()
    let liveWindow = LiveWindowController()
    /// 区分对方的每个人 (SpeakerID.swift).
    let speakers = SpeakerVoices()
    var apiVisionEnabled = UserDefaults.standard.bool(forKey: "apiVisionEnabled")
    var preparedWorkspace: Workspace?
    var isImportingDocuments = false
    var isIndexingDocuments = false
    /// Result line of the last material import ("已导入 3 份资料并归档为 Markdown").
    var importStatus = ""
    var selectedExcerpts: [ContextExcerpt] = []
    /// Workspaces whose stage review is being written.
    var stageReviewing: Set<UUID> = []
    /// Result line of the last export / import / backup.
    var dataStatus = ""
    var isDataBusy = false
    /// Session selected in the history page (also set when jumping from a workspace review).
    var historySelection: UUID?
    /// nil shows sessions of all workspaces.
    var historyWorkspaceFilter: UUID?

    /// The scenario in use. Mode, materials, requirements and background all belong to it.
    var activeWorkspace: Workspace {
        get { workspaces.active }
        set { workspaces.update(newValue) }
    }
    var sessionMode: SessionMode {
        get { activeWorkspace.mode }
        set { workspaces.setMode(newValue, for: activeWorkspace.id) }
    }
    var answerStyle: AnswerStyle {
        get { activeWorkspace.style }
        set { activeWorkspace.style = newValue }
    }
    var answerPreferences: String {
        get { activeWorkspace.preferences }
        set { activeWorkspace.preferences = newValue }
    }
    var documents: [ContextDocument] {
        get { activeWorkspace.documents }
        set { activeWorkspace.documents = newValue }
    }
    /// Free-form background notes of the workspace.
    var profile: String {
        get { activeWorkspace.notes }
        set { activeWorkspace.notes = newValue }
    }

    // MARK: Metrics
    var lastASRSeconds: Double?
    var lastRTF: Double?
    var lastRefineSeconds: Double?
    var lastAnswerSeconds: Double?
    var lastFirstTokenSeconds: Double?
    var lastStartupSeconds: Double?
    var droppedAudioSegments = 0
    var echoDroppedCount = 0

    // MARK: Appearance and overlay
    var teleprompterFontSize = UserDefaults.standard.object(forKey: "teleprompterFontSize") as? Double ?? 22
    // Fully opaque by default: a translucent window costs an extra blend every frame.
    var teleprompterOpacity = UserDefaults.standard.object(forKey: "teleprompterOpacity") as? Double ?? 1.0
    var capturePrivacy = UserDefaults.standard.object(forKey: "capturePrivacy") as? Bool ?? true
    var clickThrough = false
    var shareTestImage: NSImage?
    var reducedEffects = UserDefaults.standard.bool(forKey: "reducedEffects")
    var keepOnTop = UserDefaults.standard.object(forKey: "keepOnTop") as? Bool ?? true
    /// Hide the teleprompter's liquid-glass background. The text, the border and the
    /// buttons stay fully opaque so the answer stays readable on any wallpaper.
    var teleprompterHideBackground = UserDefaults.standard.object(forKey: "teleprompterHideBackground") as? Bool ?? false
    /// Simplified interface: secondary controls collapse into overflow menus and
    /// long option lists are folded away, so the app has fewer visible knobs.
    var simplifiedInterface = !(UserDefaults.standard.object(forKey: "advancedSettingsEnabled") as? Bool ?? false)
    /// "system", "light" or "dark". Migrates the old darkMode switch.
    var appearance = UserDefaults.standard.string(forKey: "appearance")
        ?? (UserDefaults.standard.object(forKey: "darkMode") as? Bool == true ? "dark" : "system")
    var customShortcuts: [String: ShortcutBinding] = {
        guard let data = UserDefaults.standard.data(forKey: "customShortcuts"),
              let values = try? JSONDecoder().decode([String: ShortcutBinding].self, from: data) else { return [:] }
        return values
    }()
    var shortcutErrors: [String] = []

    // MARK: Answer engines
    var answerBackend = AnswerBackend.initialChoice()
    var apiBaseURL = UserDefaults.standard.string(forKey: "apiBaseURL") ?? "https://api.openai.com/v1"
    var apiModel = UserDefaults.standard.string(forKey: "apiModel") ?? ""
    var apiKey = KeychainStore.read("answer.api")
    var apiReasoningEffort = "default"
    var apiPresets: [APIPreset] = []
    var activeAPIPresetID: UUID?
    var apiModelChoices: [String] = []
    var apiModelsStatus = ""
    var appServerNote = ""
    var codexModels: [CodexModelOption] = []
    var codexModelID = UserDefaults.standard.string(forKey: "codexModelID") ?? ""
    var codexReasoning = UserDefaults.standard.string(forKey: "codexReasoning") ?? "low"
    /// nil follows the model's own default (some models run Fast unless told otherwise).
    var codexFastSetting = UserDefaults.standard.object(forKey: "codexFastMode") as? Bool
    var codexLoginStatus = "正在检查 Codex…"
    var isRefreshingCodexStatus = false
    var codexVersion = "正在检测 Codex…"
    var lastAnswerModel: String?

    // MARK: Assistant layout
    var assistantLayout = AssistantLayout.load()

    // MARK: Environment setup
    /// Progress of the one-click environment configuration / rebuild.
    var environmentSetupSteps: [EnvironmentSetup.Step] = []
    var isRunningEnvironmentSetup = false
    /// Result line of the last environment setup run.
    var environmentSetupSummary = ""

    // MARK: Web search
    var webSearchMode = WebSearchMode(rawValue: UserDefaults.standard.string(forKey: "webSearchMode") ?? "") ?? .auto
    var webSearchProvider = WebSearchProvider(rawValue: UserDefaults.standard.string(forKey: "webSearchProvider") ?? "") ?? .codex
    var tavilyKey = KeychainStore.read(TavilyClient.keyAccount)
    /// Background web answers by answer id.
    var webLookups: [UUID: WebLookup] = [:]
    /// Answers where the user switched back from the web version.
    var showOriginalAnswer: Set<UUID> = []
    /// Id of the latest answer (also while it streams).
    var currentAnswerID: UUID?
    @ObservationIgnored var webLookupTask: Task<Void, Never>?
    let webAppServer = CodexAppServerClient()
    let decisionAppServer = CodexAppServerClient()
    let decisionCodex = CodexBridge()
    let decisionAPIClient = OpenAICompatibleClient()
    @ObservationIgnored var searchDecisionTask: Task<Void, Never>?
    var acceptedWebAnswers: Set<UUID> = []
    let webCodex = CodexBridge()
    let webAPIClient = OpenAICompatibleClient(resourceTimeout: 120)
    // Slide notes are sequential, but can overlap a lookup. A transport owns one turn.
    let slideAppServer = CodexAppServerClient()
    let slideCodex = CodexBridge()
    let slideAPIClient = OpenAICompatibleClient(resourceTimeout: 120)
    /// Screen, slides and answer progress.
    let insights = LiveInsights()
    @ObservationIgnored var screenWatchTask: Task<Void, Never>?

    // MARK: Questions and answers
    var answerHistory: [AnswerRecord] = []
    /// nil shows the latest answer; otherwise an index into `answerHistory`.
    var historyCursor: Int?
    var liveCandidate: QuestionCandidate?
    /// Recent detected questions, for marking them in the conversation view.
    var questionTexts: [String] = []
    var suggestedQuestions: [QuestionCandidate] = []
    var verifyUncertainWithModel = UserDefaults.standard.bool(forKey: "verifyUncertainWithModel")
    /// 提词 (true) or 只记录 (false) in the running meeting. Set from the meeting
    /// type at every start and switched in the live page's top bar; no longer a setting.
    var automaticAnswers = true
    var currentQuestion = ""
    var currentLeadIn: String?
    var pendingQuestion = ""
    var answer = ""
    var answerDraft = ""
    var isAnswering = false
    var questionInput = ""

    // MARK: Cloud speech recognition (explicit opt-in)
    var cloudProvider = CloudSTTProvider(rawValue: UserDefaults.standard.string(forKey: "cloudProvider") ?? "") ?? .soniox
    var cloudModel = AppState.loadCloudModel()
    var cloudKey = ""
    var cloudConsent = UserDefaults.standard.bool(forKey: "cloudConsent")

    // MARK: Local recognizer
    var localStreamingModel = LocalStreamingModel(rawValue: UserDefaults.standard.string(forKey: "localStreamingModel") ?? "") ?? .xasr480
    /// Re-decode each finished utterance with the offline model (more accurate final text).
    var secondPassEnabled = UserDefaults.standard.object(forKey: "secondPassEnabled") as? Bool ?? true
    var keepRecognizerWarm = UserDefaults.standard.object(forKey: "keepRecognizerWarm") as? Bool ?? true
    var modelDownloadStatus = ""
    var isDownloadingModel = false
    var modelDownloadProgress = 0.0
    /// Bumped when model files change on disk so readiness is re-evaluated.
    var modelInstallRevision = 0
    var legacyModelInstalled = false
    var isPreparingModel = false
    var modelLoadSeconds: Double?
    var recognitionLanguage = UserDefaults.standard.string(forKey: "recognitionLanguage") ?? "auto"

    // MARK: Managed document models
    var documentOCRProvider = DocumentOCRProvider(rawValue: UserDefaults.standard.string(forKey: "documentOCRProvider") ?? "") ?? .system
    var ocrFallbackToVision = UserDefaults.standard.object(forKey: "ocrFallbackToVision") as? Bool ?? true
    var localOCRModelID = UserDefaults.standard.string(forKey: "localOCRModelID") ?? ModelCatalog.paddleOCR.id
    var codexOCRModel = UserDefaults.standard.string(forKey: "codexOCRModel") ?? "gpt-6-luna"
    var codexOCREffort = UserDefaults.standard.string(forKey: "codexOCREffort") ?? "medium"
    var codexOCRTimeout = UserDefaults.standard.object(forKey: "codexOCRTimeout") as? Double ?? 600
    var documentProviderWasSelected = UserDefaults.standard.bool(forKey: "documentProviderWasSelected")
    var semanticRetrievalEnabled = UserDefaults.standard.object(forKey: "semanticRetrievalEnabled") as? Bool ?? false
    @ObservationIgnored var semanticIndex = SemanticIndex()
    @ObservationIgnored var semanticIndexTask: Task<Void, Never>?
    @ObservationIgnored var semanticGeneration = UUID()
    @ObservationIgnored var runtimeDownloadTask: Task<Void, Never>?
    @ObservationIgnored var managedDownloadTask: Task<Void, Never>?
    @ObservationIgnored var answerRetrievalTask: Task<[ContextExcerpt], Never>?
    var installedDocumentModels: Set<String> = []
    var documentModelsChecked = false
    var documentModelInstallRevision = 0
    var isScanningLocalAssets = false
    var localAssetScanStatus = ""
    @ObservationIgnored var localAssetScanTask: Task<Void, Never>?
    var documentSetupFeature: DocumentFeatureAvailability.Feature?
    var documentSetupStatus = ""
    var documentSetupProgress = 0.0
    @ObservationIgnored var documentSetupTask: Task<Void, Never>?
    @ObservationIgnored var documentSetupGeneration = UUID()
    @ObservationIgnored var documentProviderSelectionRevision = 0
    @ObservationIgnored var semanticSelectionRevision = 0
    var managedModelDownloadID = ""
    var managedModelDownloadProgress = 0.0
    var managedModelDownloadStatus = ""
    var isDownloadingManagedModel = false
    var isBuildingSemanticIndex = false
    var semanticIndexStatus = ""
    var runtimeInstalled = DocumentRuntime.installed
    var isInstallingDocumentRuntime = false
    var documentRuntimeStatus = ""

    // MARK: Echo handling
    /// Drop microphone text that repeats the other side (speaker echo without headphones).
    var echoSuppression = UserDefaults.standard.object(forKey: "echoSuppression") as? Bool ?? true
    /// Apple voice processing (acoustic echo cancellation) on the microphone input.
    var microphoneVoiceProcessing = UserDefaults.standard.bool(forKey: "microphoneVoiceProcessing")

    // MARK: Recording
    /// Record every session (transcript, questions, answers) to the local archive.
    var recordSessions = UserDefaults.standard.object(forKey: "recordSessions") as? Bool ?? true
    /// Ask the answer model for a summary and question list when a session ends.
    var autoSummarize = UserDefaults.standard.object(forKey: "autoSummarize") as? Bool ?? true
    /// Add the review's lessons and improved answers to the workspace automatically.
    var autoApplyLessons = UserDefaults.standard.object(forKey: "autoApplyLessons") as? Bool ?? true
    // MARK: Notes (会后纪要), separate from live answers
    /// "" picks automatically (see `notesEngine`).
    var notesModelID = UserDefaults.standard.string(forKey: "notesModelID") ?? ""
    var notesReasoning = UserDefaults.standard.string(forKey: "notesReasoning") ?? "high"
    /// "auto" (interface language), "zh" or "en".
    var notesLanguage = UserDefaults.standard.string(forKey: "notesLanguage") ?? "auto"
    /// Template per meeting type (SessionMode raw value → template id).
    var notesTemplateByType = UserDefaults.standard.dictionary(forKey: "notesTemplateByType") as? [String: String] ?? [:]
    var customNotesTemplates = AppState.loadCustomNotesTemplates()
    /// Daily automatic backups (also taken before updates and imports).
    var autoBackup = UserDefaults.standard.object(forKey: "autoBackup") as? Bool ?? true
    var activeArchiveID: UUID?

    // MARK: Companion
    var companionPort = UserDefaults.standard.object(forKey: "companionPort") as? Int ?? 8_765
    var companionEnabled = false
    var companionAddress: String?
    var companionStatus = String(localized: "未开启")
    var companionRelayURL = UserDefaults.standard.string(forKey: "companionRelayURL") ?? ""
    var companionBluetoothEnabled = false
    var companionBluetoothStatus = String(localized: "未开启")
    var companionRelayEnabled = false
    var companionRelayStatus = String(localized: "未开启")
    var companionBackupAddress: String?
    var companionRelayTask: Task<Void, Never>?

    // MARK: Live session
    var section: AppSection = .home
    /// The meeting being prepared on the home page (nil shows the home overview).
    var meetingDraft: MeetingDraft?
    /// Title, carry-over and series of the meeting that starts next / is running.
    var plannedMeeting: MeetingPlan?
    /// Online (computer audio + microphone) or in the same room (microphone only).
    /// Kept for the next meeting and for "立即记录".
    var meetingVenue = MeetingVenue(rawValue: UserDefaults.standard.string(forKey: "meetingVenue") ?? "") ?? .online
    /// The answer on the preparation page ("AI 读到的重点 · 可能会问").
    var predictionAnswerID: UUID?
    /// Phone pages streaming from the LAN server right now (CompanionServer).
    var companionClientCount = 0
    /// A phone was connected since the companion was turned on (to say "reconnecting").
    var companionHadClient = false
    /// A start waiting for the one-time recording notice.
    var recordingNoticePending: PendingStart?
    var isRunning = false
    var isPaused = false
    var isChangingSession = false
    var statusText = "就绪"
    var errorMessage: String?
    var entries: [TranscriptEntry] = []
    var partialTranscript = ""
    var microphonePartialTranscript = ""
    var microphoneEnabled = UserDefaults.standard.object(forKey: "microphoneEnabled") as? Bool ?? true
    var transcriptionStatus = "等待系统音频"
    var sessionStartedAt: Date?
    var captureTargets: [CaptureTarget] = []
    var selectedTargetID = UserDefaults.standard.string(forKey: "selectedTargetID") ?? ""
    var capturePermissionGranted = false
    var capturePermissionStatus = String(localized: "正在检查录屏与系统音频权限…")
    var microphoneAccess = MicrophoneAccess.current()

    // MARK: Onboarding
    var isOnboardingPresented = false
    var onboardingGuideEnabled = UserDefaults.standard.bool(forKey: "onboardingGuideEnabled")
    var engineTest = EngineTestState.idle

    // MARK: Screenshots
    var screenshotPreview: NSImage?
    var screenshotURL: URL?
    var screenshotWindows: [ScreenshotWindow] = []
    var isWindowPickerPresented = false
    var screenshotDisplays: [ScreenshotDisplay] = []
    var selectedScreenshotDisplayID: UInt32?
    var regionCaptureImage: NSImage?
    var isRegionCapturePresented = false

    // MARK: Not observed (services, tasks, bookkeeping)
    @ObservationIgnored var contextIndex = HybridContextIndex()
    @ObservationIgnored var meetingContextTask: Task<Void, Never>?
    @ObservationIgnored var documentRevision = UUID()
    @ObservationIgnored var importTask: Task<Void, Never>?
    @ObservationIgnored var microphoneTranscriber: (any LiveTranscribing)?
    @ObservationIgnored var liveTranscriber: (any LiveTranscribing)?
    @ObservationIgnored var audioEpoch = UUID()
    let partialThrottle = UIThrottle(interval: 0.2)
    let micPartialThrottle = UIThrottle(interval: 0.25)
    let draftThrottle = UIThrottle(interval: 0.066)
    @ObservationIgnored var recognizerIdleTask: Task<Void, Never>?
    @ObservationIgnored var systemRecoveryTask: Task<Void, Never>?
    @ObservationIgnored private var modelDownloadTask: Task<Void, Never>?
    let audio = AudioCaptureService()
    let microphone = MicrophoneCaptureService()
    let echoMonitor = EchoMonitor()
    private let codex = CodexBridge()
    let summaryCodex = CodexBridge()
    private let hotkeys = HotkeyManager()
    @ObservationIgnored var tracker = QuestionCandidateTracker()
    let appServer = CodexAppServerClient()
    let apiClient = OpenAICompatibleClient()
    let summaryAPIClient = OpenAICompatibleClient(resourceTimeout: 600)
    @ObservationIgnored var candidateTicker: Task<Void, Never>?
    @ObservationIgnored var appleUtteranceCounter: [String: Int] = [:]
    @ObservationIgnored private var firstTokenAt: Date?
    @ObservationIgnored var isStopping = false
    @ObservationIgnored private var answerTask: Task<Void, Never>?
    @ObservationIgnored private var requestID = UUID()
    @ObservationIgnored private var answerTurns: [AnswerTurn] = []
    @ObservationIgnored private var lastQuestionUsesScreenshot = false
    @ObservationIgnored private var pendingAutomaticQuestion: (question: String, context: String, candidateID: UUID?)?
    /// Candidate behind the answer being generated / last generated automatically.
    @ObservationIgnored var answeringCandidateID: UUID?
    @ObservationIgnored var lastAutomaticAnswer: (candidateID: UUID, date: Date)?
    /// A question that arrived while the user was talking; answered if they stop soon.
    @ObservationIgnored var deferredCandidate: (candidate: QuestionCandidate, date: Date)?
    let archive = SessionArchive.shared
    let workspaces = WorkspaceStore.shared
    @ObservationIgnored var recoveredSummaryIDs: [UUID] = []
    @ObservationIgnored var summaryRecoveryTask: Task<Void, Never>?
    @ObservationIgnored var summaryRecoveryReady = false

    var modelControlsDisabled: Bool { isRunning || isChangingSession || isPreparingModel || isDownloadingModel }
    var colorScheme: ColorScheme? { appearance == "dark" ? .dark : appearance == "light" ? .light : nil }
    var codexEfforts: [String] {
        codexModels.first(where: { $0.id == codexModelID })?.efforts ?? ["low", "medium", "high"]
    }
    var currentCodexModel: CodexModelOption? { codexModels.first { $0.id == codexModelID } }
    /// Unknown catalog (no models cache yet): allow it; the server drops tiers a model lacks.
    var codexFastAvailable: Bool {
        answerBackend != .openAICompatible && (codexModels.isEmpty || currentCodexModel?.fastTier != nil)
    }
    var codexFastMode: Bool {
        get { codexFastSetting ?? currentCodexModel?.defaultsToFast ?? false }
        set { codexFastSetting = newValue }
    }
    var codexFastLabel: String {
        let label = currentCodexModel?.fastLabel ?? ""
        return label.isEmpty ? "约 1.5 倍速度 · 用量更多" : label
    }
    /// Service tier for Codex requests: Fast when switched on, explicit standard for a
    /// model that would otherwise default to Fast.
    var activeServiceTier: String? {
        guard answerBackend != .openAICompatible else { return nil }
        if codexFastMode && codexFastAvailable { return "fast" }
        return currentCodexModel?.defaultsToFast == true ? "default" : nil
    }
    var currentAnswerModel: String {
        (answerBackend == .openAICompatible ? apiModel : codexModelID).trimmingCharacters(in: .whitespacesAndNewlines)
    }

    var displayedAnswer: String {
        if showsWebAnswer, let lookup = displayedWebLookup { return lookup.answer }
        if historyCursor == nil, let id = currentAnswerID, answerCoordinator.state?.id == id { return answerCoordinator.state?.text ?? "" }
        if isAnswering && historyCursor == nil { return answerDraft }
        if let historyCursor, answerHistory.indices.contains(historyCursor) { return answerHistory[historyCursor].answer }
        return answer
    }
    var displayedQuestion: String {
        if isAnswering && historyCursor == nil && !pendingQuestion.isEmpty { return pendingQuestion }
        if let historyCursor, answerHistory.indices.contains(historyCursor) { return answerHistory[historyCursor].question }
        return currentQuestion
    }
    var historyPositionText: String? {
        guard answerHistory.count > 1 else { return nil }
        return "\((historyCursor ?? answerHistory.count - 1) + 1)/\(answerHistory.count)"
    }

    private init() {
        Diagnostics.record("app.launch", ["bundle_id": Bundle.main.bundleIdentifier ?? "unknown"])
        if !DemoMode.isEnabled { workspaces.load() }
        documentPreparation.start()
        loadAPIPresets()
        archive.load { [weak self] ids in
            guard let self else { return }
            self.recoveredSummaryIDs = ids
            self.resumeRecoveredSummariesIfNeeded()
        }
        rebuildContextIndex()
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
        // Demo / screenshot mode shows sample data and never touches devices or models.
        guard !DemoMode.isEnabled else { return }
        // The guide preference is test/user scoped. First launch can open it once;
        // dismissing it or switching it off preserves progress without repeat popups.
        prepareOnboardingForCurrentVersion()
        Task { await refreshManagedModelStatus() }
        Updater.shared.start()
        startScreenWatch()
        Task {
            await refreshTargets(); await refreshCodexStatus(); await refreshModelStatus()
            summaryRecoveryReady = true
            resumeRecoveredSummariesIfNeeded()
            // Warm independent services concurrently; no model/search calls are made.
            if webSearchMode != .off, webSearchProvider == .codex { Task { await webAppServer.prewarm() } }
            if webSearchMode == .auto, answerBackend != .openAICompatible { Task { await decisionAppServer.prewarm() } }
            // Warm the persistent Codex service so the first answer does not pay start-up cost.
            if answerBackend == .codexAppServer, CodexBridge.executableURL() != nil { await appServer.prewarm() }
            await prewarmRecognizer()
        }
    }

    private static func loadTranscriptionBackend() -> String {
        let defaults = UserDefaults.standard
        let saved = defaults.string(forKey: "transcriptionBackend")
        if saved?.lowercased() == "whisper" {
            defaults.set("zipformer", forKey: "transcriptionBackend")
            return "zipformer"
        }
        if let saved, ["zipformer", "apple", "cloud"].contains(saved) { return saved }
        defaults.set("zipformer", forKey: "transcriptionBackend")
        return "zipformer"
    }

    private static func loadCloudModel() -> String {
        let defaults = UserDefaults.standard
        let saved = defaults.string(forKey: "cloudModel") ?? ""
        let provider = CloudSTTProvider(rawValue: defaults.string(forKey: "cloudProvider") ?? "") ?? .soniox
        if provider.isRemovedWhisperModelIdentifier(saved) {
            defaults.set("", forKey: "cloudModel")
            return ""
        }
        return saved
    }

    func applyAppearance() {
        // NSApp is nil until NSApplication is created; the app delegate re-applies it.
        guard NSApp != nil else { return }
        NSApp.appearance = appearance == "dark" ? NSAppearance(named: .darkAqua) : appearance == "light" ? NSAppearance(named: .aqua) : nil
    }

    /// Switches the appearance with a short cross-fade so the switch does not flash.
    /// AppKit cannot animate `NSApp.appearance` itself, so the change is made inside
    /// `withAnimation` and the root view animates on the `appearance` value; this
    /// makes the semantic colors cross-fade instead of snapping.
    func setAppearance(_ value: String) {
        guard value != appearance else { return }
        withAnimation(.easeInOut(duration: 0.32)) {
            appearance = value
            applyAppearance()
        }
        updateSettings()
    }

    // MARK: Readiness

    var readinessText: String {
        if !isOfflineMeeting, !capturePermissionGranted { return "需要允许录屏与系统音频" }
        if transcriptionBackend == "apple" {
            if !LiveTranscriberFactory.supported { return "Apple 流式识别需要 macOS 26" }
            if recognitionLanguage == "auto" { return "请为 Apple 识别选择中文或 English" }
        }
        if transcriptionBackend == "cloud" {
            if !cloudConsent { return "请在设置中确认云端识别的数据说明" }
            if cloudKey.isEmpty { return "请填写云端识别 API Key" }
        }
        if transcriptionBackend == "zipformer", !isModelInstalled { return "需要先下载本地识别模型" }
        if transcriptionBackend == "zipformer", !missingPackages.isEmpty { return "需要完成语音模型下载" }
        if usesMicrophone && microphoneAccess != .granted { return "请先允许麦克风权限" }
        if !isOfflineMeeting, !liveSetupReadiness.audioSource { return "请选择会议声音来源" }
        return "可以开始"
    }

    var isReadyToStart: Bool { readinessText == "可以开始" }

    func refreshTargets() async {
        capturePermissionGranted = CGPreflightScreenCaptureAccess()
        Diagnostics.record("permission.checked", ["granted": capturePermissionGranted])
        guard capturePermissionGranted else {
            captureTargets = []
            selectedTargetID = ""
            capturePermissionStatus = String(localized: "请允许当前 NextThought 访问录屏与系统音频。绑定本机签名后，以后更新不必重新授权。")
            return
        }
        do {
            captureTargets = try await AudioCaptureService.availableTargets()
            capturePermissionStatus = String(localized: "已允许 · 已读取 \(captureTargets.count) 个捕获目标")
            Diagnostics.record("capture.targets", ["count": captureTargets.count])
            if captureTargets.contains(where: { $0.id == selectedTargetID }) == false {
                // "电脑声音": all audio this Mac plays (this app excluded), whatever the meeting app.
                selectedTargetID = mainDisplayTargetID ?? captureTargets.first?.id ?? ""
            }
        } catch {
            captureTargets = []
            selectedTargetID = ""
            capturePermissionStatus = String(localized: "系统仍拒绝当前进程。请从固定安装路径重启应用后刷新。")
            errorMessage = "读取屏幕与音频目标失败。请在系统设置的“隐私与安全性 → 屏幕与系统音频录制”中允许 NextThought。\n\(error.localizedDescription)"
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

    func openMicrophoneSettings() {
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Microphone") { NSWorkspace.shared.open(url) }
    }

    func refreshCodexStatus() async {
        guard !isRefreshingCodexStatus else { return }
        isRefreshingCodexStatus = true
        defer { isRefreshingCodexStatus = false }
        codex.invalidateLoginCache()
        let version = await Task.detached(priority: .utility) { CodexBridge.version() ?? "未检测到" }.value
        codexLoginStatus = await CodexBridge.loginStatus()
        codexVersion = version
        await refreshCodexModels()
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

    // MARK: Session lifecycle

    func startSession() async {
        if DemoMode.isEnabled {
            liveTranslation.beginMeeting()
            isRunning = true; isPaused = false; statusText = "演示：正在聆听"
            return
        }
        guard !isRunning, !isChangingSession else { return }
        guard !isDownloadingModel else { return }
        guard transcriptionBackend != "zipformer" || isModelInstalled else { errorMessage = AppFailure.modelMissing.localizedDescription; section = .settings; return }
        guard isOfflineMeeting || !selectedTargetID.isEmpty else { errorMessage = String(localized: "请先选择声音来源（默认是电脑声音）。"); return }
        liveTranslation.beginMeeting()
        speakers.beginMeeting(inPerson: isOfflineMeeting)
        // 提词 / 只记录 starts from the meeting type's default; the live page can switch it.
        // In the room people talk face to face: answers stay quiet unless asked for.
        automaticAnswers = typeDefaults.liveMode == .prompting && !isOfflineMeeting
        documentPreparation.pauseForMeeting()
        Task { await SemanticWorkspacePreparation.shared.cancelAll() }
        cancelAnswer()
        currentQuestion = ""; currentLeadIn = nil; answer = ""; pendingQuestion = ""; partialTranscript = ""
        microphonePartialTranscript = ""
        lastAnswerModel = nil
        clearScreenshot()
        selectedExcerpts = []; lastASRSeconds = nil; lastRTF = nil; lastRefineSeconds = nil; lastAnswerSeconds = nil
        droppedAudioSegments = 0; echoDroppedCount = 0
        audioEpoch = UUID()
        meetingContextTask?.cancel(); meetingContextTask = nil
        isChangingSession = true
        defer { isChangingSession = false }
        do {
            let startupBegan = Date()
            if semanticRetrievalReady {
                Task.detached(priority: .utility) { await SemanticRetrievalService.shared.prepare() }
            }
            recognizerIdleTask?.cancel(); recognizerIdleTask = nil
            if usesMicrophone { try await MicrophoneCaptureService.authorize() }
            isPreparingModel = true
            if transcriptionBackend == "zipformer" {
                statusText = "正在加载本地识别模型…"
            }
            modelLoadSeconds = try await prepareTranscriber()
            Diagnostics.record("session.transcriber_ready", ["backend": transcriptionBackend, "seconds": Date().timeIntervalSince(startupBegan)])
            isPreparingModel = false
            let startedAt = Date()
            sessionStartedAt = startedAt
            entries = []
            tracker = QuestionCandidateTracker(mode: activeWorkspace.effectiveMode)
            liveCandidate = nil; suggestedQuestions = []; questionTexts = []; appleUtteranceCounter = [:]
            deferredCandidate = nil; answeringCandidateID = nil; lastAutomaticAnswer = nil
            answerHistory = []; historyCursor = nil; webLookups = [:]; showOriginalAnswer = []; currentAnswerID = nil; webLookupTask?.cancel()
            answerTurns.removeAll()
            insights.resetSession()
            echoMonitor.reset()
            if answerBackend == .codexAppServer { Task { await appServer.prewarm() } }
            isStopping = false
            let startPlan = plannedMeeting
            let startWorkspace = meetingWorkspaceForStart()
            preparedWorkspace = startWorkspace
            isRunning = true
            documentPreparation.pauseForMeeting()
            beginArchive(startedAt: startedAt)
            prepareMeetingContextForStart(workspace: startWorkspace, plan: startPlan, epoch: audioEpoch)
            if !isOfflineMeeting {
                do {
                    try await audio.startCapture(targetID: selectedTargetID)
                } catch {
                    throw AppFailure.capture("无法捕获所选音源。请在“系统设置 → 隐私与安全性 → 屏幕与系统音频录制”允许 NextThought，并确认目标窗口仍打开。\n\(error.localizedDescription)")
                }
            }
            if usesMicrophone {
                microphone.voiceProcessing = microphoneVoiceProcessing
                do { try await microphone.startCapture() }
                catch { microphoneUnavailable("麦克风启动失败：\(error.localizedDescription)") }
            }
            isRunning = true
            isPaused = false
            lastStartupSeconds = Date().timeIntervalSince(startupBegan)
            Diagnostics.record("session.started", ["backend": transcriptionBackend, "seconds": lastStartupSeconds ?? -1])
            statusText = String(format: "正在聆听 · 启动 %.1f s", lastStartupSeconds ?? 0)
            isStopping = false
            transcriptionStatus = isOfflineMeeting ? "等待麦克风" : "等待系统音频"
            pendingAutomaticQuestion = nil
            errorMessage = nil
            startCandidateTicker()
            startAutoEnd()
        } catch {
            isRunning = false
            meetingContextTask?.cancel(); meetingContextTask = nil
            speakers.endMeeting()
            // A carry-filtered index (meetingWorkspaceForStart) must not outlive a failed start.
            if let prepared = preparedWorkspace, prepared.retrievalDocuments != activeWorkspace.retrievalDocuments {
                rebuildContextIndex()
            }
            _ = await audio.stopCapture()
            _ = microphone.stopCapture()
            audio.setPCMHandler(nil)
            microphone.setPCMHandler(nil)
            await microphoneTranscriber?.cancel()
            microphoneTranscriber = nil
            await liveTranscriber?.cancel()
            liveTranscriber = nil
            isPreparingModel = false
            audioEpoch = UUID()
            sessionStartedAt = nil
            finishArchive()
            errorMessage = "开始记录失败：\(error.localizedDescription)"
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
        liveTranslation.clearPartial(.system)
        liveTranslation.clearPartial(.microphone)
        _ = await audio.stopCapture()
        _ = microphone.stopCapture()
        await liveTranscriber?.finish(); await microphoneTranscriber?.finish()
        audio.setPCMHandler(nil); microphone.setPCMHandler(nil)
        statusText = "已暂停"
        transcriptionStatus = "已暂停采集"
    }

    func resumeSession() async {
        guard isRunning && isPaused && !isChangingSession else { return }
        guard isOfflineMeeting || !selectedTargetID.isEmpty else {
            errorMessage = String(localized: "声音来源已不可用，请刷新后重新选择。")
            return
        }
        isChangingSession = true
        defer { isChangingSession = false }
        do {
            audioEpoch = UUID()
            _ = try await prepareTranscriber()
            if !isOfflineMeeting { try await audio.startCapture(targetID: selectedTargetID) }
            if usesMicrophone {
                microphone.voiceProcessing = microphoneVoiceProcessing
                do { try await microphone.startCapture() }
                catch { microphoneUnavailable("麦克风启动失败：\(error.localizedDescription)") }
            }
            isPaused = false
            statusText = "正在聆听"
            transcriptionStatus = isOfflineMeeting ? "等待麦克风" : "等待系统音频"
            errorMessage = nil
        } catch {
            _ = await audio.stopCapture()
            _ = microphone.stopCapture()
            audio.setPCMHandler(nil); microphone.setPCMHandler(nil)
            await liveTranscriber?.cancel(); await microphoneTranscriber?.cancel()
            liveTranscriber = nil; microphoneTranscriber = nil
            errorMessage = "恢复聆听失败：\(error.localizedDescription)"
        }
    }

    func togglePause() async {
        if isPaused { await resumeSession() } else { await pauseSession() }
    }

    func changeCaptureTarget() async {
        updateSettings()
        guard !DemoMode.isEnabled else { return }
        guard isRunning && !isPaused else { return }
        await pauseSession()
        await resumeSession()
    }

    /// `endedAt`: where the meeting really ended, when that was before now (auto-end after silence).
    func stopSession(endedAt: Date? = nil) async {
        guard (isRunning || isPaused) && !isChangingSession else { return }
        isChangingSession = true
        defer { isChangingSession = false }
        isStopping = true
        meetingContextTask?.cancel(); meetingContextTask = nil
        stopAutoEnd()
        statusText = "正在处理最后片段…"
        pendingAutomaticQuestion = nil
        deferredCandidate = nil
        _ = await audio.stopCapture()
        _ = microphone.stopCapture()
        await liveTranscriber?.finish(); await microphoneTranscriber?.finish()
        liveTranslation.finishMeeting()
        audio.setPCMHandler(nil); microphone.setPCMHandler(nil)
        audioEpoch = UUID()
        liveTranscriber = nil
        microphoneTranscriber = nil
        candidateTicker?.cancel(); candidateTicker = nil
        liveCandidate = nil
        cancelAnswer()
        // Keep the model loaded so the next start is instant; unload after idling.
        scheduleRecognizerIdleShutdown()
        isRunning = false
        isPaused = false
        statusText = "已停止"
        partialTranscript = ""
        microphonePartialTranscript = ""
        transcriptionStatus = "已停止"
        finishArchive(endedAt: endedAt)
        // Voices are forgotten with the meeting: nothing about them is kept for the next one.
        speakers.endMeeting()
        preparedWorkspace = nil
        plannedMeeting = nil
        rebuildContextIndex()
        sessionStartedAt = nil
        isStopping = false
    }

    // MARK: Asking

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
        answerCoordinator.cancel()
        searchDecisionTask?.cancel(); searchDecisionTask = nil
        decisionAppServer.cancelActiveTurn(); decisionCodex.cancel()
        answerRetrievalTask?.cancel(); answerRetrievalTask = nil
        webLookupTask?.cancel(); webCodex.cancel(); webAppServer.cancelActiveTurn()
        answerDraft = ""
        requestID = UUID()
        answerTask?.cancel()
        codex.cancel()
        appServer.cancelActiveTurn()
        isAnswering = false
        pendingQuestion = ""
        pendingAutomaticQuestion = nil
        answeringCandidateID = nil
        Diagnostics.record("answer.cancelled")
        statusText = isRunning ? (isPaused ? "已暂停" : "正在聆听") : "就绪"
    }

    /// - Parameters:
    ///   - candidateID: the detector candidate this answers (for superseding).
    ///   - supersede: replace an in-flight automatic answer instead of queueing.
    func ask(_ question: String, screenshot: URL?, recentContext: String? = nil, isAutomatic: Bool = false,
             candidateID: UUID? = nil, supersede: Bool = false) {
        if DemoMode.isEnabled {
            historyCursor = nil; currentQuestion = question; answerDraft = ""
            answer = "这是界面演示，不会调用真实回答服务。正式使用时，助手会结合实时对话与当前场景生成建议回答。"
            lastAnswerModel = "UI Demo"; statusText = "演示：未调用模型"
            return
        }
        if isAutomatic && isAnswering && !supersede {
            pendingAutomaticQuestion = (question, recentContext ?? "", candidateID)
            Diagnostics.record("answer.queued")
            return
        }
        guard answerEngineReady else {
            if !isAutomatic {
                errorMessage = "连接回答服务后才能生成回答；当前会议记录会继续。"
                openSettings(.answers)
            }
            return
        }
        let backend = answerBackend
        if backend == .codexExec, !CodexBridge.isChatGPTLogin(codexLoginStatus) {
            errorMessage = "转写可继续；生成回答需要先登录 Codex。请在设置中刷新登录状态，或切换到 API 回答引擎。"
            return
        }
        if isIndexingDocuments { errorMessage = "资料索引正在更新，请完成后再生成回答。"; return }
        let selectedModel = currentAnswerModel
        guard !selectedModel.isEmpty else { errorMessage = "请在设置中选择回答模型。"; section = .settings; return }
        let selectedReasoning = codexReasoning
        cancelAnswer()
        historyCursor = nil
        pendingQuestion = question
        currentQuestion = question
        answer = ""
        isAnswering = true
        answeringCandidateID = candidateID
        firstTokenAt = nil
        statusText = backend == .openAICompatible ? "正在流式生成回答…" : "Codex 正在整理回答…"
        let id = UUID()
        requestID = id
        currentAnswerID = id
        answerCoordinator.begin(id: id, question: question)
        Diagnostics.record("answer.started", ["request_id": id.uuidString, "automatic": isAutomatic, "screenshot": screenshot != nil, "question_characters": question.count, "backend": backend.rawValue, "supersede": supersede])

        let context = recentContext ?? TranscriptContextBuilder.recent(contextEntries.map { (date: $0.date, text: $0.contextualText) })
        // The passages are looked up while the answer starts: with the embedding
        // model the question is turned into a vector first, which takes a moment.
        let retrievalQuestion = question + "\n" + (answerTurns.last?.question ?? "")
        let retrievalSnapshot = contextIndex
        let retrieval = Task { [weak self] in
            guard let self else { return [ContextExcerpt]() }
            return await self.retrieveExcerpts(question: retrievalQuestion, snapshot: retrievalSnapshot)
        }
        answerRetrievalTask = retrieval
        let workspace = preparedWorkspace ?? activeWorkspace
        let allSnippets = activeSkills.map { SkillSnippet(name: $0.name, content: $0.content) }
        let glossarySection = glossaryPromptSection(for: workspace)
        let turns = answerTurns
        let tier = activeServiceTier
        let screenTask = Task { [weak self] () -> QuestionScreenContext in
            guard let self else { return .init() }
            if let screenshot, let image = NSImage(contentsOf: screenshot)?.cgImage(forProposedRect: nil, context: nil, hints: nil),
               let regions = try? await ScreenContextService.recognize(image) {
                return .init(context: ScreenContext(frameID: UUID().uuidString, targetID: "screenshot", source: "本题截图", regions: regions))
            }
            return await self.screenForQuestion(question)
        }
        let makeParts = { (webSearch: Bool, results: [WebResult]?, excerpts: [ContextExcerpt], screen: ScreenContext?, slide: ContextExcerpt?, image: Bool) in
            AnswerPromptBuilder.makeParts(question: question, context: context, profile: workspace.materialText, turns: turns, hasScreenshot: image,
                preferences: workspace.preferences, mode: workspace.effectiveMode, style: workspace.style, excerpts: excerpts,
                experience: workspace.experienceNotes(), skills: allSnippets, webResults: results, webSearch: webSearch,
                screen: screen.map { "来源：\($0.source)；采集时间：\(AnswerPromptBuilder.todayText($0.capturedAt))；识别：\($0.recognizer)\n" + ScreenText.trimmed($0.text, limit: 5000) }, currentSlide: slide,
                glossary: glossarySection)
        }
        searchDecisionTask = Task { [weak self] in
            guard let self else { return }
            let screen = await screenTask.value
            let decision = await self.assessWebSearch(question, context: context, screen: screen.context?.text)
            guard !Task.isCancelled, self.requestID == id else { return }
            self.answerCoordinator.update(id: id) { $0.evidence.search = decision }
            Diagnostics.record("web.decision", ["request_id": id.uuidString, "search": decision.search, "reason": decision.reason, "mechanism": decision.mechanism])
            guard decision.search else { return }
            guard self.webSearchAvailable else {
                self.answerCoordinator.update(id: id) { $0.evidence.notes.append("需要联网，但搜索服务未就绪。") }; return
            }
            let excerpts = await retrieval.value
            guard !Task.isCancelled, self.requestID == id else { return }
            let slide = screen.slide
            self.startWebLookup(id: id, question: question, query: decision.query) { webSearch, results in
                makeParts(webSearch, results, excerpts, screen.context, slide, false)
            }
        }
        let answerStartedAt = Date()

        answerTask = Task {
            var automaticScreenImage: URL?
            defer {
                screenTask.cancel()
                if let automaticScreenImage { try? FileManager.default.removeItem(at: automaticScreenImage) }
                if let screenshot, screenshot.pathExtension.lowercased() == "jpg" {
                    try? FileManager.default.removeItem(at: screenshot)
                }
            }
            do {
                let excerpts = await retrieval.value
                try Task.checkCancellation()
                guard self.requestID == id, self.activeWorkspace.id == workspace.id else { return }
                let frozen = await screenTask.value
                try Task.checkCancellation()
                guard self.requestID == id else { return }
                automaticScreenImage = frozen.image
                let image = screenshot ?? frozen.image
                selectedExcerpts = excerpts
                answerCoordinator.update(id: id) {
                    $0.evidence.materials = excerpts; $0.evidence.screen = frozen.context
                    $0.evidence.screenImageAttached = image != nil
                    $0.evidence.retrieval = semanticRetrievalReady ? "混合检索" : "关键词查找"
                    if let note = frozen.note { $0.evidence.notes.append(note) }
                }
                var parts = makeParts(false, nil, excerpts, frozen.context, frozen.slide, image != nil)
                if let note = frozen.note { parts.user += "\n屏幕状态：" + note }
                var request = AnswerRequest(system: parts.system, user: parts.user, model: selectedModel, effort: selectedReasoning, imageURL: image, serviceTier: tier)
                let onDelta: @Sendable (String) -> Void = { [weak self] text in
                    Task { @MainActor in
                        guard let self, self.requestID == id, self.isAnswering else { return }
                        if self.firstTokenAt == nil {
                            self.firstTokenAt = Date()
                            self.lastFirstTokenSeconds = Date().timeIntervalSince(answerStartedAt)
                        }
                        // Deltas carry the full text so far; never step backwards. Coalesced to
                        // ~15 UI updates/s to keep window-server compositing light.
                        self.draftThrottle.run { [weak self] in
                            guard let self, self.requestID == id, self.isAnswering else { return }
                            if text.count >= self.answerDraft.count { self.answerDraft = text; self.answerCoordinator.draft(id: id, text: text) }
                        }
                    }
                }
                let result: String
                do { result = try await runAnswer(request, backend: backend, onDelta: onDelta) }
                catch let AnswerEngineError.http(code, detail) where screenshot == nil && automaticScreenImage != nil && [400, 415, 422].contains(code) {
                    guard requestID == id else { throw CancellationError() }
                    answerCoordinator.update(id: id) { $0.evidence.screenImageAttached = false; $0.evidence.notes.append("模型未接受屏幕图片，改用 OCR 文字：" + String(detail.prefix(120))) }
                    request.imageURL = nil
                    result = try await runAnswer(request, backend: backend, onDelta: onDelta)
                }
                guard requestID == id else { return }
                draftThrottle.cancel()
                answer = result
                answerCoordinator.finish(id: id, text: result)
                answerDraft = ""
                lastAnswerSeconds = Date().timeIntervalSince(answerStartedAt)
                if firstTokenAt == nil { lastFirstTokenSeconds = lastAnswerSeconds }
                lastAnswerModel = selectedModel
                currentQuestion = question
                pendingQuestion = ""
                lastQuestionUsesScreenshot = request.imageURL != nil
                isAnswering = false
                if let candidateID, isAutomatic { lastAutomaticAnswer = (candidateID, Date()) }
                answeringCandidateID = nil
                Diagnostics.record("answer.completed", ["request_id": id.uuidString, "answer_characters": result.count, "model": selectedModel, "reasoning": selectedReasoning, "service_tier": request.serviceTier ?? "standard", "backend": backend.rawValue, "first_token_seconds": lastFirstTokenSeconds ?? -1, "seconds": lastAnswerSeconds ?? -1])
                statusText = isRunning ? (isPaused ? "已暂停" : "正在聆听") : "就绪"
                let record = AnswerRecord(id: id, question: question, answer: result, model: selectedModel, seconds: lastAnswerSeconds, firstTokenSeconds: lastFirstTokenSeconds, automatic: isAutomatic, screenshotPath: archive.archiveScreenshot(image), webAnswer: webLookups[id]?.status == .done ? webLookups[id]?.answer : nil, webSources: webLookups[id]?.sources, evidence: answerCoordinator.evidenceHistory[id])
                answerHistory.append(record)
                insights.beginAnswer(id: id, text: result, since: answerStartedAt)
                if answerHistory.count > 100 { answerHistory.removeFirst(answerHistory.count - 100) }
                tracker.noteAnswered()
                answerTurns.append(AnswerTurn(question: question, answer: result))
                if answerTurns.count > 6 { answerTurns.removeFirst(answerTurns.count - 6) }
                errorMessage = nil
                recordAnswer(record)
                if webSearchMode == .auto, webLookups[id] == nil, AnswerGrounding.needsVerification(result), webSearchAvailable {
                    answerCoordinator.update(id: id) { $0.evidence.notes.append("快速回答指出时效性不足，已追加一次联网核查。") }
                    startWebLookup(id: id, question: question) { webSearch, results in
                        makeParts(webSearch, results, excerpts, frozen.context, nil, false)
                    }
                }
                if let next = pendingAutomaticQuestion, isRunning, !isPaused, !isStopping {
                    pendingAutomaticQuestion = nil
                    ask(next.question, screenshot: nil, recentContext: next.context, isAutomatic: true, candidateID: next.candidateID)
                }
            } catch is CancellationError {
                guard requestID == id else { return }
                isAnswering = false
                pendingQuestion = ""
            } catch {
                guard requestID == id else { return }
                isAnswering = false
                pendingQuestion = ""
                answerCoordinator.update(id: id) { $0.phase = .failed; $0.error = error.localizedDescription }
                // Keep whatever streamed so far visible instead of losing it.
                if !answerDraft.isEmpty { answer = answerDraft; currentQuestion = question; answerDraft = "" }
                Diagnostics.record("answer.failed", ["request_id": id.uuidString, "backend": backend.rawValue])
                statusText = "回答出错"
                errorMessage = error.localizedDescription
            }
        }
    }

    func runAnswer(_ request: AnswerRequest, backend: AnswerBackend, onDelta: @escaping @Sendable (String) -> Void) async throws -> String {
        switch backend {
        case .openAICompatible:
            return try await apiClient.answer(request, baseURL: apiBaseURL, apiKey: apiKey, reasoningEffort: apiEffortParameter, onDelta: onDelta)
        case .codexExec:
            return try await codex.run(prompt: request.fullPrompt, model: request.model, reasoning: request.effort, imageURL: request.imageURL, serviceTier: request.serviceTier) { text in onDelta(text) }
        case .codexAppServer:
            do {
                let text = try await appServer.answer(request, onDelta: onDelta)
                appServerNote = ""
                return text
            } catch let AnswerEngineError.appServerUnavailable(detail) {
                // Older CLIs or a crashed service: fall back to the one-shot exec path.
                appServerNote = "常驻服务不可用，已临时改用 codex exec：\(detail)"
                Diagnostics.record("appserver.fallback")
                return try await codex.run(prompt: request.fullPrompt, model: request.model, reasoning: request.effort, imageURL: request.imageURL, serviceTier: request.serviceTier) { text in onDelta(text) }
            }
        }
    }

    /// A finished web lookup updates the remembered answer so follow-ups build on it.
    func replaceTurnAnswer(question: String, with answer: String) {
        guard let index = answerTurns.lastIndex(where: { $0.question == question }) else { return }
        answerTurns[index].answer = answer
    }

    func forceAnswerCurrent() {
        // Priority: typed text → latest unanswered question candidate → recent transcript.
        if questionInput.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
           let candidate = [liveCandidate].compactMap({ $0 }).first(where: { $0.state != .triggered && $0.assessment.intent != .statement }) ?? suggestedQuestions.last,
           Date().timeIntervalSince(candidate.lastChanged) < 30 {
            answerCandidate(candidate)
            return
        }
        let cutoff = Date().addingTimeInterval(-45)
        let hasRecent = entries.contains { $0.isOtherSide && ($0.endedAt ?? $0.date) > cutoff }
        if !questionInput.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || !partialTranscript.isEmpty
            || (isOfflineMeeting && !microphonePartialTranscript.isEmpty) || hasRecent {
            submitCurrentText()
            return
        }
        errorMessage = "还没有识别出的文字，请先说话或手动输入问题。"
    }

    /// Answer one transcript line on demand (click in the conversation view).
    func answerEntry(_ entry: TranscriptEntry) {
        recordQuestion(text: entry.text, leadIn: nil, intent: "manual", score: 1, disposition: "manual")
        ask(entry.text, screenshot: nil)
    }

    private func submitCurrentText() {
        let cutoff = Date().addingTimeInterval(-45)
        let recent = entries.filter { $0.isOtherSide && ($0.endedAt ?? $0.date) > cutoff }.map(\.text)
        let partial = isOfflineMeeting ? microphonePartialTranscript : partialTranscript
        guard let question = ManualQuestionBuilder.make(typed: questionInput, partial: partial, recent: recent) else {
            errorMessage = "还没有最近 45 秒的转写。请先说话或手动输入问题。"; return
        }
        ask(question, screenshot: nil)
    }

    // MARK: Screenshots

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
            if !isRunning { section = .home }
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
                errorMessage = "没有找到可截图的窗口。请确认系统已允许 NextThought 录屏，并先打开要分析的页面。"
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
                if !isRunning { section = .home }
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

    // MARK: Settings

    func setProfile(_ text: String) {
        profile = text
    }

    /// Runs the whole environment checklist and reports every step, so a
    /// half-configured setup shows up as a named failed step.
    func runQuickEnvironmentSetup() async {
        guard !isRunningEnvironmentSetup else { return }
        isRunningEnvironmentSetup = true
        environmentSetupSteps = []
        defer { isRunningEnvironmentSetup = false }
        await EnvironmentSetup.quickSetup(state: self) { [weak self] step in
            self?.updateEnvironmentStep(step)
        }
        environmentSetupSummary = environmentSetupSummaryText()
        Diagnostics.record("environment.quick_setup", ["steps": String(environmentSetupSteps.count)])
    }

    /// Rebuilds the runtime environment (index, models, sources, folders) without
    /// changing the user's choices.
    func rebuildEnvironment() async {
        guard !isRunningEnvironmentSetup else { return }
        isRunningEnvironmentSetup = true
        environmentSetupSteps = []
        defer { isRunningEnvironmentSetup = false }
        await EnvironmentSetup.rebuild(state: self) { [weak self] step in
            self?.updateEnvironmentStep(step)
        }
        environmentSetupSummary = environmentSetupSummaryText()
        Diagnostics.record("environment.rebuilt", ["steps": String(environmentSetupSteps.count)])
    }

    private func updateEnvironmentStep(_ step: EnvironmentSetup.Step) {
        if let index = environmentSetupSteps.firstIndex(where: { $0.id == step.id }) {
            environmentSetupSteps[index] = step
        } else {
            environmentSetupSteps.append(step)
        }
    }

    private func environmentSetupSummaryText() -> String {
        let failed = environmentSetupSteps.filter { $0.status == .failed }
        if failed.isEmpty {
            return "环境已经就绪：\(environmentSetupSteps.count) 项检查全部通过。"
        }
        return "有 \(failed.count) 项需要处理：\(failed.map(\.title).joined(separator: "、"))。"
    }

    func updateSettings() {
        UserDefaults.standard.set(documentOCRProvider.rawValue, forKey: "documentOCRProvider")
        UserDefaults.standard.set(documentProviderWasSelected, forKey: "documentProviderWasSelected")
        UserDefaults.standard.set(ocrFallbackToVision, forKey: "ocrFallbackToVision")
        UserDefaults.standard.set(apiVisionEnabled, forKey: "apiVisionEnabled")
        UserDefaults.standard.set(localOCRModelID, forKey: "localOCRModelID")
        UserDefaults.standard.set(codexOCRModel, forKey: "codexOCRModel")
        UserDefaults.standard.set(codexOCREffort, forKey: "codexOCREffort")
        UserDefaults.standard.set(codexOCRTimeout, forKey: "codexOCRTimeout")
        UserDefaults.standard.set(semanticRetrievalEnabled, forKey: "semanticRetrievalEnabled")
        let defaults = UserDefaults.standard
        defaults.set(codexModelID, forKey: "codexModelID")
        defaults.set(codexReasoning, forKey: "codexReasoning")
        if let codexFastSetting { defaults.set(codexFastSetting, forKey: "codexFastMode") }
        defaults.set(recordSessions, forKey: "recordSessions")
        defaults.set(autoSummarize, forKey: "autoSummarize")
        resumeRecoveredSummariesIfNeeded()
        defaults.set(autoApplyLessons, forKey: "autoApplyLessons")
        defaults.set(autoBackup, forKey: "autoBackup")
        defaults.set(keepOnTop, forKey: "keepOnTop")
        defaults.set(appearance, forKey: "appearance")
        defaults.set(microphoneEnabled, forKey: "microphoneEnabled")
        defaults.set(echoSuppression, forKey: "echoSuppression")
        defaults.set(microphoneVoiceProcessing, forKey: "microphoneVoiceProcessing")
        applyAppearance()
        defaults.set(selectedTargetID, forKey: "selectedTargetID")
        defaults.set(recognitionLanguage, forKey: "recognitionLanguage")
        defaults.set(recognitionVocabulary, forKey: "recognitionVocabulary")
        defaults.set(transcriptionBackend, forKey: "transcriptionBackend")
        defaults.set(localStreamingModel.rawValue, forKey: "localStreamingModel")
        defaults.set(secondPassEnabled, forKey: "secondPassEnabled")
        defaults.set(ocrFallbackToVision, forKey: "ocrFallbackToVision")
        defaults.set(semanticRetrievalEnabled, forKey: "semanticRetrievalEnabled")
        MainWindowLayout.refreshLevels()
        TeleprompterController.shared.applyPrivacy(capturePrivacy)
    }

    func saveTranscriptExport() {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.plainText]
        panel.nameFieldStringValue = "NextThought-Transcript-\(Date.now.formatted(.iso8601.year().month().day())).txt"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let text = contextEntries.map { "[\($0.date.formatted(date: .omitted, time: .shortened))] \($0.roleLabel): \($0.text)" }.joined(separator: "\n")
        do {
            try text.write(to: url, atomically: true, encoding: .utf8)
        } catch {
            errorMessage = "导出失败：\(error.localizedDescription)"
        }
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
        localAssetScanTask?.cancel()
        documentSetupTask?.cancel(); managedDownloadTask?.cancel(); runtimeDownloadTask?.cancel()
        importTask?.cancel()
        workspaces.flush()
        audio.setPCMHandler(nil)
        microphone.setPCMHandler(nil)
        _ = microphone.stopCapture()
        if isRunning { _ = archive.finish() }
        Task {
            await liveTranscriber?.cancel()
            await microphoneTranscriber?.cancel()
            await ZipformerRuntime.shared.unload()
            await OfflineSegmentRecognizer.shared.unload()
        }
        answerTask?.cancel()
        codex.cancel()
        summaryCodex.cancel()
        appServer.shutdown()
        webLookupTask?.cancel()
        webAppServer.shutdown()
        decisionAppServer.shutdown(); decisionCodex.cancel(); searchDecisionTask?.cancel()
        screenContextService.invalidate()
        documentPreparation.shutdown()
        screenWatchTask?.cancel()
        webCodex.cancel()
        insights.sideChain?.cancel()
        slideAppServer.shutdown()
        slideCodex.cancel()
        CompanionServer.shared.stop()
        if let screenshotURL { try? FileManager.default.removeItem(at: screenshotURL) }
    }

    // MARK: Hotkeys

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
                Task { await self.togglePause() }
            },
            .toggleTeleprompter: { TeleprompterController.shared.toggle() },
            .cancelAnswer: { [weak self] in self?.cancelAnswer() },
            .copyAnswer: { [weak self] in self?.copyAnswer() },
            .toggleClickThrough: { [weak self] in self?.toggleClickThrough() },
            .panicHide: { TeleprompterController.shared.panicHide() },
            .previousAnswer: { [weak self] in self?.showPreviousAnswer() },
            .nextAnswer: { [weak self] in self?.showNextAnswer() },
            .refineShorter: { [weak self] in self?.refine(.shorter) },
            .toggleLiveWindow: { [weak self] in self?.liveWindow.cycle() }
        ], overrides: customShortcuts)
    }

    // MARK: Transcriber setup

    private func receiveSpeechEvent(_ event: SpeechSegmentEvent) {
        guard isRunning, !isPaused, !isStopping, event.kind == .final else { return }
        guard transcriptionBackend == "zipformer"
            || (transcriptionBackend == "cloud" && cloudProvider == .openai) else { return }
        let transcriber = event.source == .microphone ? microphoneTranscriber : liveTranscriber
        Task { await transcriber?.commitUtterance(event) }
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
                await self.microphoneTranscriber?.reset()
                self.transcriptionStatus = "麦克风已切换到 \(name)"
                Diagnostics.record("microphone.device_changed")
            }
        }

        let factory: () -> (any LiveTranscribing)?
        switch transcriptionBackend {
        case "zipformer":
            guard isModelInstalled else { throw AppFailure.modelMissing }
            let vocabulary = recognitionHotwords
            let model = localStreamingModel.onlineModel
            let refiner = refinerModel
            factory = { ZipformerStreamingTranscriber(model: model, refiner: refiner, vocabulary: vocabulary) }
            statusText = "正在准备本地流式识别…"
        case "cloud":
            guard cloudConsent, !cloudKey.isEmpty else {
                throw AppFailure.transcription("请先在设置中确认云端识别说明并填写 API Key。")
            }
            let provider = cloudProvider
            let key = cloudKey
            let model = cloudProvider.isRemovedWhisperModelIdentifier(cloudModel) ? "" : cloudModel
            let vocabulary = recognitionVocabulary
            factory = { CloudStreamingTranscriber(provider: provider, apiKey: key, model: model, vocabulary: vocabulary) }
            statusText = "正在连接云端识别…"
        default:
            factory = { LiveTranscriberFactory.make() }
            statusText = "正在准备 Apple Speech；首次使用可能下载语言资源…"
        }

        guard let service = factory() else {
            throw AppFailure.transcription("Apple 流式识别需要 macOS 26 与支持的设备；请切换本地或云端识别。")
        }
        let started = Date()
        // In the room there is no computer audio: only the microphone is recognized.
        let offline = isOfflineMeeting
        liveTranscriber = offline ? nil : service
        let micService = usesMicrophone ? factory() : nil
        microphoneTranscriber = micService
        let micStart: Task<String?, Never>? = micService.map { mic in
            Task { @MainActor in
                do { try await self.startLive(mic, source: .microphone, epoch: captureEpoch); return nil }
                catch { return error.localizedDescription }
            }
        }
        if !offline { try await startLive(service, source: .system, epoch: captureEpoch) }
        if let micStart, let failure = await micStart.value {
            microphoneTranscriber = nil
            microphoneUnavailable("麦克风识别器启动失败：\(failure)")
        }
        // Both channels also feed the echo monitor (loudness envelopes only).
        let monitor = echoMonitor
        // With 区分对方的每个人 on, the last minute of the channel that carries other people's voices
        // is also kept in memory, so a finished sentence can be cut out for its voiceprint.
        let systemWindow = speakers.meeting?.system
        let roomWindow = speakers.meeting?.microphone
        if offline {
            audio.setPCMHandler(nil)
        } else {
            let systemInput = service.inputHandler()
            audio.setPCMHandler { samples, origin in
                systemWindow?.append(samples, origin: origin)
                systemInput(samples, origin)
                monitor.system(samples)
            }
        }
        if let micService = microphoneTranscriber {
            let micInput = micService.inputHandler()
            microphone.setPCMHandler { samples, origin in
                roomWindow?.append(samples, origin: origin)
                micInput(samples, origin)
                monitor.microphone(samples)
            }
        }
        return Date().timeIntervalSince(started)
    }

    private func startLive(_ service: any LiveTranscribing, source: CaptureSource, epoch: UUID) async throws {
        try await service.start(language: recognitionLanguage) { [weak self] text, final, start, end, inferenceSeconds, audioSeconds in
            guard let self, self.audioEpoch == epoch, self.isRunning else { return }
            self.receiveStreamingResult(
                text, final: final, start: start, end: end, source: source,
                inferenceSeconds: inferenceSeconds, audioSeconds: audioSeconds)
        } onError: { [weak self] message in
            guard let self, self.audioEpoch == epoch else { return }
            self.errorMessage = message
            Task { await self.pauseSession() }
        }
    }

    // MARK: Local model management

    var refinerModel: OfflineTransducerModel? {
        guard secondPassEnabled, localStreamingModel != .legacy, LocalASRPackage.xasrOffline.isInstalled else { return nil }
        return .xasrOffline(at: LocalASRPackage.xasrOffline.directory)
    }

    var isModelInstalled: Bool {
        if DemoMode.speechReady { return true }
        _ = modelInstallRevision
        if let package = localStreamingModel.package { return package.isInstalled }
        return legacyModelInstalled
    }

    /// Packages still missing for the current choice (streaming + optional second pass).
    var missingPackages: [LocalASRPackage] {
        if DemoMode.speechReady { return [] }
        _ = modelInstallRevision
        var result: [LocalASRPackage] = []
        if let package = localStreamingModel.package, !package.isInstalled { result.append(package) }
        if secondPassEnabled, localStreamingModel != .legacy, !LocalASRPackage.xasrOffline.isInstalled { result.append(.xasrOffline) }
        return result
    }

    func refreshModelStatus() async {
        legacyModelInstalled = await Task.detached(priority: .utility) { ZipformerModelStore.isInstalled() }.value
        modelInstallRevision += 1
        if !isModelInstalled, modelDownloadStatus.isEmpty {
            modelDownloadStatus = "本地识别模型尚未下载。"
        }
    }

    func downloadModel() {
        guard !modelControlsDisabled else { return }
        isDownloadingModel = true
        modelDownloadProgress = 0
        modelDownloadTask = Task {
            do {
                if localStreamingModel == .legacy {
                    modelDownloadStatus = "正在下载旧版双语模型（约 200 MB）…"
                    try await ZipformerModelStore.download { [weak self] progress, message in
                        Task { @MainActor in
                            self?.modelDownloadProgress = progress
                            self?.modelDownloadStatus = "\(message) · \(Int(progress * 100))%"
                        }
                    }
                    legacyModelInstalled = true
                } else {
                    let packages = missingPackages
                    for (index, package) in packages.enumerated() {
                        try Task.checkCancellation()
                        try await package.install { [weak self] progress, message in
                            Task { @MainActor in
                                let overall = (Double(index) + progress) / Double(max(1, packages.count))
                                self?.modelDownloadProgress = overall
                                self?.modelDownloadStatus = "\(message) · \(Int(overall * 100))%"
                            }
                        }
                        modelInstallRevision += 1
                    }
                }
                modelInstallRevision += 1
                modelDownloadStatus = "模型已下载并通过 SHA-256 校验，可离线使用。"
                updateSettings()
                await prewarmRecognizer()
            } catch is CancellationError {
                modelDownloadStatus = "下载已取消。"
            } catch {
                modelDownloadStatus = "下载失败：\(error.localizedDescription)"
            }
            isDownloadingModel = false
            modelDownloadTask = nil
        }
    }

    func cancelModelDownload() { modelDownloadTask?.cancel() }

    func deleteModels() {
        guard !modelControlsDisabled else { return }
        Task {
            isPreparingModel = true
            defer { isPreparingModel = false }
            await ZipformerRuntime.shared.unload()
            await OfflineSegmentRecognizer.shared.unload()
            do {
                for package in LocalASRPackage.allCases { try package.remove() }
                if FileManager.default.fileExists(atPath: ZipformerModelStore.modelDirectory.path) {
                    try FileManager.default.removeItem(at: ZipformerModelStore.modelDirectory)
                }
                legacyModelInstalled = false
                modelInstallRevision += 1
                modelDownloadProgress = 0
                modelDownloadStatus = "已删除全部本地识别模型。"
            } catch {
                modelDownloadStatus = "删除模型失败：\(error.localizedDescription)"
            }
        }
    }
}
