import AVFoundation
import AppKit
import SwiftUI
import NextThoughtCore

// MARK: - State

enum MicrophoneAccess: Equatable {
    case granted, denied, undetermined

    static func current() -> MicrophoneAccess {
        switch AVCaptureDevice.authorizationStatus(for: .audio) {
        case .authorized: .granted
        case .notDetermined: .undetermined
        default: .denied
        }
    }
}

enum EngineTestState: Equatable {
    case idle, running
    case passed(seconds: Double, reply: String)
    case failed(String)
}

struct AIServiceReadiness {
    var configured: Bool
    var verification: EngineTestState
    var notesModelSelected: Bool
    var canGenerateNotes: Bool { configured && notesModelSelected }
}

/// One line of the “使用前检查” list.
struct SetupItem: Identifiable {
    enum Status { case done, required, recommended, working }
    var id: String
    var icon: String
    var title: String
    var detail: String
    var status: Status
    var actionTitle: String?
    var action: (() -> Void)?
}

@MainActor
extension AppState {
    var codexInstalled: Bool { DemoMode.isEnabled || CodexBridge.executableURL() != nil }
    var checkingCodexStatus: Bool { isRefreshingCodexStatus || codexLoginStatus.hasPrefix("正在") }
    var codexReady: Bool { codexInstalled && codexLoginStatus.lowercased().contains("logged in") }

    /// Answers and notes need a working service: an API address, model and key, or a
    /// signed-in Codex. Nothing else (such as an installed Codex) is required with an API key.
    var answerEngineReady: Bool {
        switch answerBackend {
        case .openAICompatible:
            guard let url = URL(string: apiBaseURL.trimmingCharacters(in: .whitespacesAndNewlines)),
                  ["http", "https"].contains(url.scheme?.lowercased() ?? ""), let host = url.host, !host.isEmpty else { return false }
            let local = ["localhost", "127.0.0.1", "[::1]", "::1"].contains(host.lowercased())
            return !apiModel.trimmingCharacters(in: .whitespaces).isEmpty && (local || !apiKey.isEmpty)
        case .codexAppServer, .codexExec:
            return codexReady && !codexModelID.isEmpty
        }
    }

    var recognitionReady: Bool {
        switch transcriptionBackend {
        case "apple": LiveTranscriberFactory.supported && recognitionLanguage != "auto"
        case "cloud": cloudConsent && !cloudKey.isEmpty
        default: isModelInstalled
        }
    }

    var liveSetupReadiness: RealtimeSetupReadiness {
        .init(speech: recognitionReady, capture: capturePermissionGranted, microphoneRequired: usesMicrophone,
              microphone: RealtimeSetupReadiness.microphonePermissionAllowsStart(
                isGranted: microphoneAccess == .granted, isUndetermined: microphoneAccess == .undetermined),
              answers: answerEngineReady,
              audioSource: captureTargets.contains { $0.id == selectedTargetID },
              systemAudioRequired: !isOfflineMeeting)
    }
    var recordingReadiness: RecordingReadiness {
        let permission: RecordingPermission = switch microphoneAccess {
        case .granted: .granted
        case .denied: .denied
        case .undetermined: .undetermined
        }
        return .init(speech: recognitionReady, capture: capturePermissionGranted,
                     audioSource: captureTargets.contains { $0.id == selectedTargetID },
                     systemAudioRequired: !isOfflineMeeting, microphoneRequired: usesMicrophone,
                     microphone: permission)
    }
    var aiServiceReadiness: AIServiceReadiness {
        .init(configured: answerEngineReady, verification: engineTest, notesModelSelected: !notesEngine.model.isEmpty)
    }
    var recordingReadinessMessage: String {
        guard let issue = recordingReadiness.issues.first else { return String(localized: "可以开始记录") }
        switch issue {
        case .recognition:
            if transcriptionBackend == "cloud" { return String(localized: "请先配置云端语音识别并确认音频上传说明") }
            if transcriptionBackend == "apple" { return String(localized: "系统语音识别需要 macOS 26，并选择中文或 English") }
            return String(localized: "需要先准备本机语音识别模型")
        case .systemAudioPermission: return String(localized: "允许系统音频权限后即可记录")
        case .audioSource: return String(localized: "请选择有效的声音来源，或刷新后再试")
        case .microphonePermission:
            return microphoneAccess == .undetermined ? String(localized: "开始时会询问麦克风权限") : String(localized: "请在系统设置中允许麦克风")
        }
    }
    /// The same description is shown at the moment of starting and during setup.
    /// It describes this application's behavior, without promising a provider's retention policy.
    var recordingDataRouteSummary: String {
        let audio: String
        switch transcriptionBackend {
        case "cloud":
            audio = String(localized: "声音会实时发送给 \(cloudProvider.title) 识别，并按该服务的条款处理。应用不保存原始录音。")
        case "apple":
            audio = String(localized: "声音由系统语音识别处理。应用不保存原始录音。")
        default:
            audio = String(localized: "声音在这台 Mac 上识别。应用不保存原始录音。")
        }
        guard answerEngineReady else { return audio }
        return audio + "\n" + String(localized: "使用 AI 时，转写、相关资料片段或你选择的截图会发送给所选回答服务。")
    }
    var currentGuideVersion: String {
        OnboardingVersion.guideVersion(Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "1.8.0")
    }
    /// Bumped when the guide's pages change, so an interrupted older guide starts from its first page.
    static let onboardingLayout = 2

    func prepareOnboardingForCurrentVersion() {
        let defaults = UserDefaults.standard
        let completedVersion = defaults.string(forKey: "onboardingGuideCompletedVersion")
        let upgrade = OnboardingVersion.isFeatureUpgrade(
            appVersion: currentGuideVersion, startedVersion: defaults.string(forKey: "onboardingGuideStartedVersion"),
            startedLayout: defaults.object(forKey: "onboardingGuideLayout") as? Int, layout: Self.onboardingLayout)
        let needsGuide = OnboardingVersion.needsGuide(appVersion: currentGuideVersion, completedVersion: completedVersion)
        if upgrade {
            // First launch of a feature release: a guide that was deferred or switched off by an
            // earlier release is armed again, once, and starts from its first page.
            defaults.set(needsGuide, forKey: "onboardingGuideEnabled")
            for key in ["onboardingGuideDismissedVersion", "onboardingGuideDeferredVersion", "onboardingResumeRowDismissedVersion"] {
                defaults.removeObject(forKey: key)
            }
            defaults.set(0, forKey: "quickStartStep")
            defaults.set(currentGuideVersion, forKey: "onboardingGuideStartedVersion")
            defaults.set(Self.onboardingLayout, forKey: "onboardingGuideLayout")
        }
        onboardingGuideEnabled = defaults.bool(forKey: "onboardingGuideEnabled")
        let wasDismissed = defaults.string(forKey: "onboardingGuideDismissedVersion") == currentGuideVersion
        isOnboardingPresented = onboardingGuideEnabled && needsGuide && !wasDismissed
    }

    func setOnboardingGuideEnabled(_ enabled: Bool) {
        let defaults = UserDefaults.standard
        defaults.set(enabled, forKey: "onboardingGuideEnabled")
        onboardingGuideEnabled = enabled
        if enabled {
            defaults.removeObject(forKey: "onboardingGuideDismissedVersion")
            defaults.removeObject(forKey: "onboardingGuideDeferredVersion")
            // Completed guides can be deliberately replayed from the first page.
            if defaults.string(forKey: "onboardingGuideCompletedVersion") == currentGuideVersion {
                defaults.set(0, forKey: "quickStartStep")
            }
            withAnimation(.easeOut(duration: 0.2)) { isOnboardingPresented = true }
        } else {
            withAnimation(.easeOut(duration: 0.2)) { isOnboardingPresented = false }
        }
    }

    /// 稍后继续: closes the guide and keeps its page. The home page offers a way back
    /// (`OnboardingResumeRow`) until the guide is finished or that row is dismissed.
    func deferOnboarding() {
        let defaults = UserDefaults.standard
        defaults.set(currentGuideVersion, forKey: "onboardingGuideDismissedVersion")
        defaults.set(currentGuideVersion, forKey: "onboardingGuideDeferredVersion")
        defaults.removeObject(forKey: "onboardingResumeRowDismissedVersion")
        defaults.set(false, forKey: "onboardingGuideEnabled")
        onboardingGuideEnabled = false
        withAnimation(.easeOut(duration: 0.2)) { isOnboardingPresented = false }
    }

    /// The areas the home page's resume row names: what a meeting still lacks and the answer service.
    var onboardingPendingAreas: [OnboardingPending] {
        OnboardingPending.open(soundReady: recordingReadiness.canRequestStart, answersReady: answerEngineReady)
    }

    /// The scene picked in the guide is the one the home page and the first meeting open in.
    func rememberGuideScene(_ mode: SessionMode) {
        lastScene = mode
        UserDefaults.standard.set(mode.rawValue, forKey: "lastScene")
    }

    func openOnboarding() {
        setOnboardingGuideEnabled(true)
    }

    func finishOnboarding(goTo section: AppSection = .home) {
        UserDefaults.standard.set(true, forKey: "onboardingCompleted")
        UserDefaults.standard.set(currentGuideVersion, forKey: "onboardingGuideCompletedVersion")
        UserDefaults.standard.removeObject(forKey: "onboardingGuideDeferredVersion")
        withAnimation(.easeOut(duration: 0.2)) { isOnboardingPresented = false }
        self.section = section
        Diagnostics.record("onboarding.finished", ["ready": isReadyToStart, "engine": answerEngineReady])
    }

    func openSettings(_ group: SettingsGroup) {
        UserDefaults.standard.set(group.rawValue, forKey: "settingsGroup")
        // During a meeting settings open in their own window.
        select(.settings)
    }

    func refreshSetupStatus() async {
        microphoneAccess = .current()
        guard !DemoMode.isEnabled else { return }
        await refreshTargets()
        await refreshModelStatus()
        await refreshCodexStatus()
    }

    func requestMicrophoneAccess() async {
        if MicrophoneAccess.current() == .undetermined {
            _ = await AVCaptureDevice.requestAccess(for: .audio)
        } else {
            openMicrophoneSettings()
        }
        microphoneAccess = .current()
    }

    /// Keeps the Codex transport (app-server / exec) when switching between Codex and API.
    func selectAnswerBackend(_ choice: AnswerBackend) {
        if choice == .openAICompatible {
            answerBackend = .openAICompatible
        } else if answerBackend == .openAICompatible {
            answerBackend = .codexAppServer
        }
        engineTest = .idle
        saveAnswerSettings()
        if answerBackend == .codexAppServer { Task { await appServer.prewarm() } }
    }

    /// One short request through the configured engine (the same path real answers take).
    /// A failure through an API key is explained in plain words (wrong key, wrong address…).
    func testAnswerEngine() async {
        guard engineTest != .running else { return }
        let model = currentAnswerModel
        guard !model.isEmpty else { engineTest = .failed(String(localized: "请先选择或填写模型")); return }
        if answerBackend != .openAICompatible, !codexInstalled { engineTest = .failed(String(localized: "未检测到 Codex")); return }
        engineTest = .running
        let request = AnswerRequest(system: "这是连通性测试。只回复两个字：收到。", user: "测试", model: model, effort: codexEfforts.first ?? codexReasoning,
                                    serviceTier: activeServiceTier)
        let started = Date()
        do {
            let reply = try await runAnswer(request, backend: answerBackend) { _ in }
            let text = reply.trimmingCharacters(in: .whitespacesAndNewlines)
            engineTest = text.isEmpty ? .failed(String(localized: "模型没有返回文本")) : .passed(seconds: Date().timeIntervalSince(started), reply: text)
        } catch {
            engineTest = .failed(answerBackend == .openAICompatible ? AnswerServiceError.explain(error) : error.localizedDescription)
        }
        var passed = false
        if case .passed = engineTest { passed = true }
        Diagnostics.record("engine.test", ["backend": answerBackend.rawValue, "ok": passed, "service_tier": request.serviceTier ?? "standard"])
    }

    func setupItems(confirmBinding: @escaping () -> Void) -> [SetupItem] {
        var items: [SetupItem] = []
        items.append(SetupItem(
            id: "capture", icon: "rectangle.dashed.badge.record", title: "录屏与系统音频",
            detail: capturePermissionGranted ? "已允许 · 可以听到会议里对方的声音" : "必需：用来采集会议窗口里对方的声音",
            status: capturePermissionGranted ? .done : .required,
            actionTitle: capturePermissionGranted ? nil : "允许",
            action: { Task { await self.requestCapturePermission() } }))
        items.append(SetupItem(
            id: "mic", icon: "mic", title: "麦克风",
            detail: microphoneAccess == .granted ? (isOfflineMeeting ? "已允许 · 线下声音会标为“现场”" : "已允许 · 你说的话标记为“我”")
                : microphoneAccess == .undetermined ? (isOfflineMeeting ? "线下记录需要麦克风；开始时会询问" : "第一次开始记录时才会询问，用于识别你自己说的话")
                : usesMicrophone ? (isOfflineMeeting ? "未允许 · 线下记录需要麦克风" : "未允许 · 请允许，或关闭麦克风采集") : "未允许 · 只记录对方的声音",
            status: microphoneAccess == .granted ? .done : (usesMicrophone ? .required : .recommended),
            actionTitle: microphoneAccess == .granted ? nil : (microphoneAccess == .undetermined ? "允许" : "打开系统设置"),
            action: { Task { await self.requestMicrophoneAccess() } }))
        let recognitionDetail: String
        switch transcriptionBackend {
        case "apple": recognitionDetail = recognitionReady ? "Apple Speech · \(recognitionLanguage == "en" ? "English" : "中文")" : "Apple Speech 需要 macOS 26 并选择中文或 English"
        case "cloud": recognitionDetail = recognitionReady ? "云端流式 · \(cloudProvider.title)" : "云端识别需要填写 API Key 并确认数据说明"
        default:
            let size = missingPackages.reduce(0) { $0 + $1.approximateMegabytes }
            recognitionDetail = isDownloadingModel ? "正在下载 \(Int(modelDownloadProgress * 100))%"
                : recognitionReady ? String(localized: "\(localStreamingModel.shortTitle) · 本机离线识别") : "必需：下载本机识别模型（约 \(max(size, 1)) MB，只需一次）"
        }
        items.append(SetupItem(
            id: "asr", icon: "waveform", title: "语音识别", detail: recognitionDetail,
            status: isDownloadingModel ? .working : recognitionReady ? .done : .required,
            actionTitle: recognitionReady || isDownloadingModel ? nil : (transcriptionBackend == "zipformer" ? "下载" : "设置"),
            action: { self.transcriptionBackend == "zipformer" ? self.downloadModel() : self.openSettings(.recognition) }))
        let engineDetail: String
        if answerBackend == .openAICompatible {
            engineDetail = answerEngineReady ? "API · \(apiModel)" : "必需：选择服务商，粘贴 API Key"
        } else if !codexInstalled {
            engineDetail = "必需：选择服务商，粘贴 API Key"
        } else if !codexReady {
            engineDetail = "必需：在终端运行 codex login 登录后点刷新，或改用 API Key"
        } else {
            engineDetail = "Codex · \(codexModelID) · \(codexReasoning)" + (codexFastMode && codexFastAvailable ? " · Fast" : "")
        }
        items.append(SetupItem(
            id: "engine", icon: "sparkles", title: "回答引擎", detail: engineDetail,
            status: answerEngineReady ? .done : .required,
            actionTitle: answerEngineReady ? nil : "设置",
            action: { self.openSettings(.answers) }))
        let workspace = activeWorkspace
        let materialDetail = String(localized: "资料 \(workspace.documents.count)")
        let materialSuggestion = workspace.documents.isEmpty ? " · \(String(localized: "建议添加议程或背景资料"))" : ""
        items.append(SetupItem(
            id: "workspace", icon: workspace.mode.symbol, title: "当前场景",
            detail: "\(workspace.name) · \(workspace.mode.displayName) · \(materialDetail)\(materialSuggestion)",
            status: workspace.documents.isEmpty ? .recommended : .done,
            actionTitle: "编辑", action: { self.section = .library }))
        let bound = LocalSigning.isBound || Updater.shared.signature?.isAdHoc == false
        items.append(SetupItem(
            id: "signing", icon: "checkmark.shield", title: "本机签名",
            detail: bound ? "已绑定 · 以后更新不用重新授权" : "建议：绑定后，应用内更新不再需要重新授权录屏和麦克风",
            status: bound ? .done : .recommended,
            actionTitle: bound || !Updater.shared.canInstall ? nil : "绑定",
            action: confirmBinding))
        return items
    }
}

// MARK: - Checklist

struct SetupChecklist: View {
    @Environment(AppState.self) private var state
    var exclude: Set<String> = []
    @State private var confirmBinding = false

    var body: some View {
        VStack(spacing: Space.s2) {
            ForEach(state.setupItems(confirmBinding: { confirmBinding = true }).filter { !exclude.contains($0.id) }) { item in
                SetupRow(item: item)
            }
        }
        .confirmationDialog("绑定本机签名并重新打开？", isPresented: $confirmBinding, titleVisibility: .visible) {
            Button("绑定并重新打开") { Updater.shared.bindSignatureAndRelaunch() }
            Button("取消", role: .cancel) { }
        } message: {
            Text("会在本机生成签名证书并重新签名，然后重启应用。重启后请再允许一次录屏与麦克风，以后更新不再需要。")
        }
    }
}

/// A quiet row: status dot, title, one gray line, and an action when there is one.
struct SetupRow: View {
    let item: SetupItem

    var body: some View {
        HStack(spacing: Space.s3) {
            StatusDot(tone: tone)
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Text(LocalizedStringKey(item.title)).font(Typo.label)
                    badge
                }
                Text(LocalizedStringKey(item.detail)).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(2)
            }
            Spacer(minLength: Space.s2)
            if item.status == .working {
                ProgressView().controlSize(.small)
            } else if let title = item.actionTitle, let action = item.action {
                Button(LocalizedStringKey(title), action: action)
                    .buttonStyle(ChipButtonStyle(prominent: item.status == .required))
            }
        }
        .padding(.horizontal, Space.s3).padding(.vertical, Space.s2)
        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
    }

    private var tone: StatusDot.Tone {
        switch item.status {
        case .done: .ready
        case .required: .caution
        case .recommended, .working: .idle
        }
    }

    @ViewBuilder private var badge: some View {
        switch item.status {
        case .required: Text("必需").font(Typo.caption).foregroundStyle(Ink.caution)
        case .recommended: Text("建议").font(Typo.caption).foregroundStyle(Ink.secondary)
        case .done, .working: EmptyView()
        }
    }
}

// MARK: - First-run guide

/// One row of the sound page: a label, one gray line about what it is for, and the control.
private struct GuideRow<Control: View>: View {
    let title: LocalizedStringKey
    let detail: Text
    var tone: StatusDot.Tone?
    let control: () -> Control

    init(_ title: LocalizedStringKey, detail: Text, tone: StatusDot.Tone? = nil, @ViewBuilder control: @escaping () -> Control) {
        self.title = title; self.detail = detail; self.tone = tone; self.control = control
    }

    var body: some View {
        HStack(alignment: .top, spacing: Space.s4) {
            VStack(alignment: .leading, spacing: 3) {
                HStack(spacing: 6) {
                    if let tone { StatusDot(tone: tone) }
                    Text(title).font(Typo.label)
                }
                detail.font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: Space.s3)
            control()
        }
        .padding(.vertical, Space.s3)
    }
}

private struct GuideDivider: View {
    var body: some View { Rectangle().fill(Ink.hairline).frame(height: 0.5) }
}

private struct GuideChipStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View { Chip(configuration: configuration) }
    private struct Chip: View {
        let configuration: ButtonStyleConfiguration
        @State private var hovering = false
        var body: some View {
            configuration.label
                .background(hovering ? Ink.hover : .clear, in: Capsule())
                .scaleEffect(configuration.isPressed ? 0.96 : 1)
                .onHover { hovering = $0 }
                .animation(Motion.hover, value: hovering)
        }
    }
}

/// First run, four short steps, each one decision or one permission: the occasion, the sound,
/// an answer service (optional), and what the cue island does in a meeting. Nothing is asked
/// twice, and nothing is requested before the person presses its button.
struct OnboardingView: View {
    @Environment(AppState.self) private var state
    @AppStorage("quickStartStep", store: UserDefaults.standard) private var stepRaw = 0
    @State private var scene: SessionMode = AppState.shared.lastScene.isGuideChoice ? AppState.shared.lastScene : .interview
    @State private var showSample = false
    @State private var sampleFinished = false
    @State private var starting = false
    @Namespace private var chip

    private var step: OnboardingStep { OnboardingStep.clamped(stepRaw) }

    var body: some View {
        ZStack {
            Ink.paper.ignoresSafeArea()
            VStack(spacing: 0) {
                header
                ScrollView {
                    page
                        .id(step)
                        .transition(.opacity)
                        .frame(maxWidth: 720, alignment: .leading)
                        .frame(maxWidth: .infinity)
                        .padding(.horizontal, Space.s8)
                        .padding(.top, Space.s6)
                        .padding(.bottom, Space.s6)
                }
                footer
            }
        }
        // Read-only checks: no audio permission prompts, downloads, or model calls.
        .task {
            stepRaw = step.rawValue
            if !DemoMode.isEnabled { await state.refreshSetupStatus() }
        }
        // Coming back from System Settings: pick up a permission that was just given.
        .onReceive(NotificationCenter.default.publisher(for: NSApplication.didBecomeActiveNotification)) { _ in
            guard !DemoMode.isEnabled else { return }
            state.microphoneAccess = .current()
            Task { await state.refreshTargets() }
        }
    }

    // MARK: Frame

    private var header: some View {
        HStack(spacing: Space.s3) {
            HStack(spacing: 4) {
                ForEach(OnboardingStep.allCases, id: \.self) { item in
                    Capsule()
                        .fill(item == step ? Ink.ink : (item.rawValue < step.rawValue ? Ink.secondary : Ink.hairline))
                        .frame(width: 22, height: 2)
                }
            }
            .accessibilityHidden(true)
            Text(verbatim: "\(step.position) / \(OnboardingStep.count)")
                .font(Typo.caption.monospacedDigit()).foregroundStyle(Ink.secondary)
            Text(LocalizedStringKey(title(of: step))).font(Typo.label)
            Text(verbatim: timeText(step.estimatedSeconds)).font(Typo.caption).foregroundStyle(Ink.secondary)
            Spacer(minLength: 0)
            Button("稍后继续") { state.deferOnboarding() }
                .buttonStyle(GhostButtonStyle())
                .accessibilityLabel("稍后继续")
                .accessibilityHint("关闭引导，首页会留一个入口")
        }
        .padding(.horizontal, Space.s8)
        .padding(.top, Space.s8)
        .padding(.bottom, Space.s3)
    }

    private var footer: some View {
        HStack(spacing: Space.s3) {
            if let previous = step.previous {
                Button("上一步") { go(previous) }
                    .buttonStyle(GhostButtonStyle(height: Space.controlLarge))
                    .accessibilityLabel("上一步")
            }
            Spacer(minLength: 0)
            switch step {
            case .start:
                Button("先看看") { state.rememberGuideScene(scene); state.finishOnboarding() }
                    .buttonStyle(GhostButtonStyle(height: Space.controlLarge))
                    .accessibilityLabel("先看看")
                    .accessibilityHint("结束引导，回到首页")
                Button { startMeeting() } label: {
                    Text(LocalizedStringKey(starting || state.isChangingSession ? "正在开始…" : "开始第一场"))
                }
                .buttonStyle(PrimaryButtonStyle(height: Space.controlLarge))
                .keyboardShortcut(.defaultAction)
                .disabled(!canStart || starting || state.isChangingSession)
                .accessibilityLabel("开始第一场")
                .accessibilityHint(Text("以“\(scene.displayName)”场景开始记录"))
            case .answers where !state.answerEngineReady:
                Button("跳过") { advance() }
                    .buttonStyle(SecondaryButtonStyle())
                    .keyboardShortcut(.defaultAction)
                    .accessibilityLabel("跳过回答服务")
            default:
                Button("下一步") { advance() }
                    .buttonStyle(PrimaryButtonStyle(height: Space.controlLarge))
                    .keyboardShortcut(.defaultAction)
                    .accessibilityLabel("下一步")
            }
        }
        .padding(.horizontal, Space.s8)
        .padding(.vertical, Space.s4)
        .overlay(alignment: .top) { GuideDivider() }
    }

    private func go(_ target: OnboardingStep) {
        withAnimation(Motion.phase) { stepRaw = target.rawValue }
    }

    private func advance() {
        if step == .scene { state.rememberGuideScene(scene) }
        if let next = step.next { go(next) }
    }

    private func title(of step: OnboardingStep) -> String {
        switch step {
        case .scene: "场合"
        case .sound: "声音"
        case .answers: "回答"
        case .start: "提词与开始"
        }
    }

    private func timeText(_ seconds: Int) -> String {
        seconds < 60 ? String(localized: "约 \(seconds) 秒") : String(localized: "约 \(seconds / 60) 分钟")
    }

    @ViewBuilder private var page: some View {
        switch step {
        case .scene: scenePage
        case .sound: soundPage
        case .answers: answersPage
        case .start: startPage
        }
    }

    private func pageHeader(_ title: LocalizedStringKey, _ subtitle: LocalizedStringKey) -> some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            Text(title).font(Typo.display).fixedSize(horizontal: false, vertical: true)
            Text(subtitle).font(Typo.body).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
        }
    }

    // MARK: 1 · Scene

    private var scenePage: some View {
        VStack(alignment: .leading, spacing: Space.s6) {
            pageHeader("NextThought 按场合帮你开会", "先选一个最常用的场合。每个场合有自己的提示和纪要，开会前随时可以换。")
            sceneSheet
            VStack(alignment: .leading, spacing: Space.s3) {
                Button {
                    withAnimation(Motion.expand) { showSample.toggle() }
                } label: {
                    HStack(spacing: 6) {
                        Image(systemName: "chevron.right").font(.system(size: 9, weight: .semibold))
                            .rotationEffect(.degrees(showSample ? 90 : 0))
                        Text(LocalizedStringKey(showSample ? "收起示例" : "看一段面试示例"))
                    }
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel(showSample ? "收起面试示例" : "看一段面试示例")
                if showSample {
                    Text("示例不录音、不申请权限、不调用模型，与上面选的场合无关。")
                        .font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
                    OnboardingSampleMeeting(finished: $sampleFinished)
                        .transition(.opacity)
                }
            }
        }
    }

    /// The same sheet as the home page's composer, minus the title field: glyph, chips, promise, moments.
    private var sceneSheet: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: Space.s3) {
                SceneGlyph(mode: scene, size: 38)
                    .id(scene).transition(.scale(scale: 0.8).combined(with: .opacity))
                Text(verbatim: scene.displayName).font(.system(size: 19, weight: .medium))
                Spacer(minLength: 0)
            }
            .padding(.horizontal, Space.s6).padding(.top, Space.s6).padding(.bottom, Space.s4)

            FlowLayout(spacing: 4, lineSpacing: 4) {
                ForEach(SessionMode.builtIn) { mode in sceneChip(mode) }
            }
            .padding(.horizontal, Space.s6 - 4)

            VStack(alignment: .leading, spacing: Space.s2) {
                Text(verbatim: scene.theme.promise)
                    .font(.system(size: 17, weight: .medium, design: .serif))
                    .foregroundStyle(Ink.ink)
                    .fixedSize(horizontal: false, vertical: true)
                VStack(alignment: .leading, spacing: 6) {
                    ForEach(scene.theme.moments, id: \.self) { moment in
                        HStack(spacing: 8) {
                            Circle().fill(scene.theme.tint).frame(width: 4, height: 4)
                            Text(verbatim: moment).font(Typo.caption).foregroundStyle(Ink.secondary)
                        }
                    }
                }
            }
            .id(scene)
            .transition(.blurReplace)
            .padding(.horizontal, Space.s6).padding(.top, Space.s6).padding(.bottom, Space.s6)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .background {
            RoundedRectangle(cornerRadius: Space.radiusLarge, style: .continuous)
                .fill(Ink.surface)
                .shadow(color: Ink.contactShadow, radius: 1, x: 0, y: 1)
                .shadow(color: Ink.ambientShadow, radius: 28, x: 0, y: 16)
        }
        .overlay { RoundedRectangle(cornerRadius: Space.radiusLarge, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 0.5) }
        .animation(Motion.snappy, value: scene)
    }

    private func sceneChip(_ mode: SessionMode) -> some View {
        let selected = mode == scene
        return Button {
            scene = mode
            state.rememberGuideScene(mode)
        } label: {
            Text(verbatim: mode.displayName).font(Typo.label)
                .foregroundStyle(selected ? mode.theme.tint : Ink.secondary)
                .padding(.horizontal, 11)
                .frame(height: 30)
                .background {
                    if selected {
                        Capsule().fill(mode.theme.tint.opacity(0.13)).matchedGeometryEffect(id: "selection", in: chip)
                    }
                }
                .contentShape(Capsule())
        }
        .buttonStyle(GuideChipStyle())
        .help(mode.defaultsSummary)
        .accessibilityLabel(mode.displayName)
        .accessibilityAddTraits(selected ? .isSelected : [])
    }

    // MARK: 2 · Sound

    private var soundPage: some View {
        VStack(alignment: .leading, spacing: Space.s6) {
            pageHeader("让它听得到", "只在这里问一次。每一项都要你按下按钮才会请求，不允许也可以先往下看。")
            VStack(spacing: 0) {
                GuideRow("会议在哪里", detail: Text(LocalizedStringKey(state.isOfflineMeeting
                        ? "线下：只用麦克风，声音标为“现场”。"
                        : "线上：听电脑里会议软件播放的声音。"))) {
                    InkSegmented(items: MeetingVenue.allCases.map { (value: $0, title: $0.title) },
                                 selection: Binding(get: { state.meetingVenue }, set: { state.setMeetingVenue($0) }))
                        .fixedSize()
                        .accessibilityLabel("会议在哪里")
                }
                GuideDivider()
                if !state.isOfflineMeeting {
                    GuideRow("系统音频", detail: Text(LocalizedStringKey(state.capturePermissionGranted
                            ? "已允许 · 能听到会议里对方的声音。"
                            : "用来听到对方的声音。macOS 把它叫“屏幕与系统音频录制”，只取声音，不保存画面。允许后系统可能要求重新打开应用，进度会保留。")),
                             tone: state.capturePermissionGranted ? .ready : .caution) {
                        if !state.capturePermissionGranted {
                            Button("允许") { Task { await state.requestCapturePermission() } }
                                .buttonStyle(SecondaryButtonStyle())
                                .accessibilityLabel("允许系统音频")
                        }
                    }
                    GuideDivider()
                }
                microphoneRow
                GuideDivider()
                GuideRow("语音识别", detail: Text(LocalizedStringKey(recognitionLine)), tone: state.recognitionReady ? .ready : .caution) {
                    recognitionControl
                }
                recognitionExtras
            }
            Text(verbatim: state.recordingDataRouteSummary)
                .font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
        }
    }

    private var microphoneTone: StatusDot.Tone {
        guard state.usesMicrophone else { return .idle }
        switch state.microphoneAccess {
        case .granted: return .ready
        case .undetermined: return .idle
        case .denied: return .caution
        }
    }

    private var microphoneLine: String {
        guard state.usesMicrophone else { return "不使用麦克风，也不会询问权限。会议里只记录对方的声音。" }
        let purpose = state.isOfflineMeeting ? "线下会议靠它听现场的声音。" : "用来标出你自己说的话。"
        switch state.microphoneAccess {
        case .granted: return state.isOfflineMeeting ? "已允许 · 现场的声音会标为“现场”。" : "已允许 · 你说的话会标为“我”。"
        case .undetermined: return purpose + "点“允许”时系统会询问一次。"
        case .denied: return state.isOfflineMeeting ? "未允许 · 线下记录需要它，请在系统设置里打开。" : "未允许 · 请在系统设置里打开，或选“只记录对方”。"
        }
    }

    private var microphoneRow: some View {
        GuideRow("麦克风", detail: Text(LocalizedStringKey(microphoneLine)), tone: microphoneTone) {
            VStack(alignment: .trailing, spacing: Space.s2) {
                if !state.isOfflineMeeting {
                    InkSegmented(items: [(value: true, title: LocalizedStringKey("记录我的声音")), (value: false, title: LocalizedStringKey("只记录对方"))],
                                 selection: Binding(get: { state.microphoneEnabled },
                                                    set: { state.microphoneEnabled = $0; state.updateSettings() }))
                        .fixedSize()
                        .accessibilityLabel("是否记录我的声音")
                }
                if state.usesMicrophone, state.microphoneAccess != .granted {
                    Button(LocalizedStringKey(state.microphoneAccess == .denied ? "打开系统设置" : "允许")) {
                        Task { await state.requestMicrophoneAccess() }
                    }
                    .buttonStyle(SecondaryButtonStyle())
                    .accessibilityLabel(state.microphoneAccess == .denied ? "打开系统设置允许麦克风" : "允许麦克风")
                }
            }
        }
    }

    private var recognitionLine: String {
        switch state.transcriptionBackend {
        case "apple":
            return state.recognitionReady ? "系统语音识别已就绪。声音由系统处理，应用不保存录音。" : "系统语音识别需要 macOS 26，并明确选择一种语言。"
        case "cloud":
            return state.recognitionReady ? "云端识别已配置。" : "云端识别需要填写 API Key 并确认音频上传说明。"
        default:
            if state.isDownloadingModel { return "正在下载本机识别模型，下载时可以先看后面的步骤。" }
            if state.isModelInstalled { return "\(state.localStreamingModel.shortTitle) · 在这台 Mac 上离线识别，已就绪。" }
            let size = max(state.missingPackages.reduce(0) { $0 + $1.approximateMegabytes }, 1)
            return String(localized: "把声音转成文字。需要下载本机识别模型，约 \(size) MB，只需一次。")
        }
    }

    @ViewBuilder private var recognitionControl: some View {
        if state.transcriptionBackend == "zipformer" {
            if state.isDownloadingModel {
                HStack(spacing: Space.s3) {
                    ProgressView(value: state.modelDownloadProgress).controlSize(.small).frame(width: 90)
                    Text(verbatim: "\(Int(state.modelDownloadProgress * 100))%").font(Typo.caption.monospacedDigit())
                        .foregroundStyle(Ink.secondary)
                    Button("取消") { state.cancelModelDownload() }
                        .buttonStyle(SecondaryButtonStyle())
                        .accessibilityLabel("取消下载")
                }
            } else if !state.isModelInstalled {
                Button(LocalizedStringKey(downloadFailed ? "重新下载" : "下载")) { state.downloadModel() }
                    .buttonStyle(SecondaryButtonStyle())
                    .disabled(state.modelControlsDisabled)
                    .accessibilityLabel(downloadFailed ? "重新下载语音识别模型" : "下载语音识别模型")
            }
        }
    }

    private var downloadFailed: Bool { state.modelDownloadStatus.hasPrefix("下载失败") }

    @ViewBuilder private var recognitionExtras: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: Space.s2) {
            if state.transcriptionBackend == "zipformer", !state.isDownloadingModel, downloadFailed || state.modelDownloadStatus == "下载已取消。" {
                Text(LocalizedStringKey(state.modelDownloadStatus))
                    .font(Typo.caption).foregroundStyle(downloadFailed ? Ink.caution : Ink.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            if state.transcriptionBackend == "apple" {
                MenuPicker(title: "语言", selection: $state.recognitionLanguage, options: [("zh", "中文"), ("en", "English")])
                    .onChange(of: state.recognitionLanguage) { _, _ in state.updateSettings() }
            } else if state.transcriptionBackend == "cloud" {
                CloudSettings()
            }
            DisclosureGroup("换一种识别方式") {
                VStack(alignment: .leading, spacing: Space.s2) {
                    MenuPicker(title: "识别引擎", selection: $state.transcriptionBackend,
                               options: [("zipformer", String(localized: "本机识别（推荐）")), ("apple", String(localized: "系统识别 · 需 macOS 26")),
                                         ("cloud", String(localized: "云端识别 · 需 API Key"))])
                        .disabled(state.modelControlsDisabled)
                        .onChange(of: state.transcriptionBackend) { _, _ in state.updateSettings(); Task { await state.refreshModelStatus() } }
                    Text("本机模型需要下载并占内存；系统语音受系统版本与语言限制；云端识别会上传会议音频并使用服务额度。")
                        .font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
                }
                .padding(.top, Space.s2)
            }
            .font(Typo.caption)
            .accessibilityLabel("换一种识别方式")
        }
        .padding(.bottom, Space.s3)
    }

    // MARK: 3 · Answers

    private var answersPage: some View {
        VStack(alignment: .leading, spacing: Space.s6) {
            pageHeader("接上一个回答服务", "会中的回答和会后的纪要由它来写。选一个服务商，粘贴 Key，点一下检查。")
            AnswerEngineForm()
            Text("不连接也能记录和转写；回答和会后纪要需要它，可以之后再接。")
                .font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
        }
    }

    // MARK: 4 · Cue island and start

    private var canStart: Bool { state.recordingReadiness.canRequestStart }

    private var startPage: some View {
        VStack(alignment: .leading, spacing: Space.s6) {
            pageHeader("会中，回答在镜头下方", "第一场按“\(scene.displayName)”开始。")
            CueIslandMock(tint: scene.theme.tint)
            VStack(alignment: .leading, spacing: Space.s2) {
                Text("被问到时，回答出现在摄像头下方的提词岛，眼睛不用离开镜头。")
                Text("鼠标停在刘海上，可以暂停、结束或立即回答。")
            }
            .font(Typo.body).foregroundStyle(Ink.ink).fixedSize(horizontal: false, vertical: true)
            if !canStart {
                HStack(spacing: Space.s3) {
                    Text(verbatim: state.recordingReadinessMessage)
                        .font(Typo.caption).foregroundStyle(Ink.caution).fixedSize(horizontal: false, vertical: true)
                    Button("回到声音") { go(.sound) }
                        .buttonStyle(GhostButtonStyle(height: 28))
                        .accessibilityLabel("回到声音这一步")
                }
            }
            if let error = state.errorMessage, !error.isEmpty {
                Text(verbatim: error).font(Typo.caption).foregroundStyle(Ink.caution).fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    private func startMeeting() {
        guard canStart, !starting else { return }
        starting = true
        Task {
            defer { starting = false }
            // Finish before the shared first-recording notice, whose pending start
            // survives dismissing the guide.
            state.rememberGuideScene(scene)
            state.finishOnboarding()
            state.beginNewMeeting(scene: scene)
            // An unsaved series edit asks first; the draft appears once that is settled.
            guard state.meetingDraft != nil else { return }
            await state.startPreparedMeeting()
        }
    }
}

private extension SessionMode {
    /// The guide offers the scenes the home page offers.
    var isGuideChoice: Bool { SessionMode.builtIn.contains(self) }
}

/// What the cue island looks like in a meeting: a black shape hanging from the camera with a
/// lead line and two cue points. Drawn here from plain shapes; the real island is never opened.
private struct CueIslandMock: View {
    let tint: Color

    var body: some View {
        ZStack(alignment: .top) {
            RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous).fill(Ink.sunken)
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Spacer()
                    Circle().fill(Color.white.opacity(0.2)).frame(width: 7, height: 7)
                    Spacer()
                }
                Text(verbatim: String(localized: "能介绍一下你在这个项目里负责的部分吗？"))
                    .font(.system(size: 11, weight: .medium)).foregroundStyle(.white.opacity(0.5)).lineLimit(1)
                Text(verbatim: String(localized: "我负责订单服务的缓存层。"))
                    .font(.system(size: 16, weight: .semibold)).foregroundStyle(.white)
                    .lineLimit(1)
                VStack(alignment: .leading, spacing: 6) {
                    cuePoint(String(localized: "规模：日均 2 亿次读取，P99 降到 18 ms"))
                    cuePoint(String(localized: "我的角色：方案设计和灰度上线由我主导"))
                }
            }
            .padding(.horizontal, 22).padding(.top, 8).padding(.bottom, 18)
            .frame(width: 380, alignment: .leading)
            .background(UnevenRoundedRectangle(bottomLeadingRadius: 26, bottomTrailingRadius: 26, style: .continuous).fill(Color.black))
            .padding(.bottom, Space.s6)
        }
        .frame(maxWidth: .infinity)
        .clipShape(RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
        .overlay { RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 0.5) }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("提词岛示意：摄像头下方的黑色浮层，第一行是回答的要点，下面两条是接着要说的内容。")
    }

    private func cuePoint(_ text: String) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: 8) {
            Circle().fill(tint).frame(width: 5, height: 5)
            Text(verbatim: text).font(.system(size: 13, weight: .medium)).foregroundStyle(.white.opacity(0.8)).lineLimit(1)
        }
    }
}
