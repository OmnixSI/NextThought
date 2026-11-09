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
    func prepareOnboardingForCurrentVersion() {
        let defaults = UserDefaults.standard
        let completedVersion = defaults.string(forKey: "onboardingGuideCompletedVersion")
        if defaults.object(forKey: "onboardingGuideEnabled") == nil {
            defaults.set(OnboardingVersion.needsGuide(appVersion: currentGuideVersion, completedVersion: completedVersion),
                         forKey: "onboardingGuideEnabled")
        }
        onboardingGuideEnabled = defaults.bool(forKey: "onboardingGuideEnabled")
        if defaults.string(forKey: "onboardingGuideStartedVersion") != currentGuideVersion {
            defaults.set(0, forKey: "quickStartStep")
            defaults.set(currentGuideVersion, forKey: "onboardingGuideStartedVersion")
        }
        let needsGuide = OnboardingVersion.needsGuide(appVersion: currentGuideVersion, completedVersion: completedVersion)
        let wasDismissed = defaults.string(forKey: "onboardingGuideDismissedVersion") == currentGuideVersion
        isOnboardingPresented = onboardingGuideEnabled && needsGuide && !wasDismissed
    }

    func setOnboardingGuideEnabled(_ enabled: Bool) {
        let defaults = UserDefaults.standard
        defaults.set(enabled, forKey: "onboardingGuideEnabled")
        onboardingGuideEnabled = enabled
        if enabled {
            defaults.removeObject(forKey: "onboardingGuideDismissedVersion")
            // Completed guides can be deliberately replayed from the first page.
            if defaults.string(forKey: "onboardingGuideCompletedVersion") == currentGuideVersion {
                defaults.set(0, forKey: "quickStartStep")
            }
            withAnimation(.easeOut(duration: 0.2)) { isOnboardingPresented = true }
        } else {
            withAnimation(.easeOut(duration: 0.2)) { isOnboardingPresented = false }
        }
    }

    func deferOnboarding() {
        let defaults = UserDefaults.standard
        defaults.set(currentGuideVersion, forKey: "onboardingGuideDismissedVersion")
        defaults.set(false, forKey: "onboardingGuideEnabled")
        onboardingGuideEnabled = false
        withAnimation(.easeOut(duration: 0.2)) { isOnboardingPresented = false }
    }

    func openOnboarding() {
        setOnboardingGuideEnabled(true)
    }

    func finishOnboarding(goTo section: AppSection = .home) {
        UserDefaults.standard.set(true, forKey: "onboardingCompleted")
        UserDefaults.standard.set(currentGuideVersion, forKey: "onboardingGuideCompletedVersion")
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

/// One row of the guide's checks: a status dot, a short label, then whatever the row says.
private struct GuideRow<Content: View>: View {
    let tone: StatusDot.Tone
    let label: LocalizedStringKey
    let content: () -> Content

    init(tone: StatusDot.Tone, label: LocalizedStringKey, @ViewBuilder content: @escaping () -> Content) {
        self.tone = tone; self.label = label; self.content = content
    }

    var body: some View {
        HStack(alignment: .top, spacing: Space.s3) {
            StatusDot(tone: tone).padding(.top, 5)
            Text(label).font(Typo.label).foregroundStyle(Ink.secondary).frame(width: 64, alignment: .leading)
            content().frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(.horizontal, Space.s4).padding(.vertical, Space.s3)
        .frame(minHeight: 48)
    }
}

/// Rows in one card, a hairline between them.
private struct GuideList<Content: View>: View {
    let content: () -> Content
    init(@ViewBuilder content: @escaping () -> Content) { self.content = content }

    var body: some View {
        VStack(spacing: 0) { content() }
            .background(Ink.surface, in: RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 1))
    }
}

private struct GuideDivider: View {
    var body: some View { Rectangle().fill(Ink.hairline).frame(height: 1) }
}

/// First run, in the order a person needs things: what this is, see it work, allow
/// the sound, record their own voice, connect an answer service, optional materials,
/// start. Nothing is asked before it is explained, and every step but the sound can be passed.
struct OnboardingView: View {
    @Environment(AppState.self) private var state
    @AppStorage("quickStartStep", store: UserDefaults.standard) private var stepRaw = 0
    @State private var sampleFinished = false
    @State private var starting = false

    private var step: OnboardingStep { OnboardingStep.clamped(stepRaw).guideStep }

    var body: some View {
        ZStack {
            Ink.paper.ignoresSafeArea()
            VStack(spacing: 0) {
                header
                ScrollView {
                    page
                        .id(step)
                        .transition(.opacity)
                        .frame(maxWidth: step == .sample ? Space.content : 560, alignment: .leading)
                        .frame(maxWidth: .infinity)
                        .padding(.horizontal, Space.s8)
                        .padding(.top, Space.s8)
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
                ForEach(OnboardingStep.guidedSteps, id: \.self) { item in
                    Capsule()
                        .fill(item == step ? Ink.ink : (item.rawValue < step.rawValue ? Ink.secondary : Ink.hairline))
                        .frame(width: 22, height: 2)
                }
            }
            .accessibilityHidden(true)
            Text(verbatim: "\(step.guidePosition) / \(OnboardingStep.guidedSteps.count)")
                .font(Typo.caption.monospacedDigit()).foregroundStyle(Ink.secondary)
            Text(LocalizedStringKey(title(of: step))).font(Typo.label)
            Text(verbatim: timeText(step.estimatedSeconds)).font(Typo.caption).foregroundStyle(Ink.secondary)
            Spacer(minLength: 0)
            Button("稍后继续") { state.deferOnboarding() }.buttonStyle(GhostButtonStyle())
        }
        .padding(.horizontal, Space.s8)
        .padding(.top, Space.s8)
        .padding(.bottom, Space.s3)
    }

    private var footer: some View {
        HStack(spacing: Space.s3) {
            if let previous = step.guidePrevious {
                Button("上一步") { go(previous) }.buttonStyle(GhostButtonStyle(height: Space.controlLarge))
            }
            Spacer(minLength: 0)
            if step == .start {
                Button("进入首页") { state.finishOnboarding() }.buttonStyle(GhostButtonStyle(height: Space.controlLarge))
                Button { startMeeting() } label: {
                    Text(LocalizedStringKey(starting || state.isChangingSession ? "正在开始…" : "开始记录"))
                }
                .buttonStyle(PrimaryButtonStyle(height: Space.controlLarge))
                .keyboardShortcut(.defaultAction)
                .disabled(!canStart || starting || state.isChangingSession)
            } else {
                Button { advance() } label: { Text(LocalizedStringKey(step == .sample ? "准备声音" : "开始前确认")) }
                    .buttonStyle(PrimaryButtonStyle(height: Space.controlLarge))
                    .keyboardShortcut(.defaultAction)
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
        if let next = step.guideNext { go(next) }
    }

    private func title(of step: OnboardingStep) -> String {
        switch step {
        case .welcome: "欢迎"
        case .sample: "看看组会如何被记下来"
        case .sound: "允许声音"
        case .voice: "你的声音"
        case .answers: "连接回答服务"
        case .materials: "资料"
        case .start: "开始"
        }
    }

    private func timeText(_ seconds: Int) -> String {
        seconds < 60 ? String(localized: "约 \(seconds) 秒") : String(localized: "约 \(seconds / 60) 分钟")
    }

    /// Whether an optional step has nothing left to do (the button then says 下一步, not 跳过).
    private var isDone: Bool {
        switch step {
        case .sample: sampleFinished
        case .answers: state.answerEngineReady
        case .voice: MyVoice.shared.usableProfile != nil
        case .materials: state.onboardingMaterialsPending.isEmpty
        case .welcome, .sound, .start: true
        }
    }

    private var skipTitle: String {
        switch step {
        case .answers: "稍后再接"
        case .voice: "以后再说"
        default: "跳过"
        }
    }

    @ViewBuilder private var page: some View {
        switch step {
        case .welcome: welcome
        case .sample: sample
        case .sound: sound
        case .voice: OnboardingVoicePage()
        case .answers: answers
        case .materials: materials
        case .start: start
        }
    }

    private func pageHeader(_ title: LocalizedStringKey, _ subtitle: LocalizedStringKey) -> some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            Text(title).font(Typo.display).fixedSize(horizontal: false, vertical: true)
            Text(subtitle).font(Typo.body).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
        }
    }

    // MARK: 1 · Welcome

    private var welcome: some View {
        VStack(alignment: .leading, spacing: Space.s8) {
            Text("开会时，坐在你身边的助手").font(Typo.display).fixedSize(horizontal: false, vertical: true)
            VStack(alignment: .leading, spacing: Space.s4) {
                Text("会前，帮你读懂资料。")
                Text("会中，听到对方的问题，递上一句话。")
                Text("会后，留下一份能直接发出去的纪要。")
            }
            .font(Typo.section)
            Text("默认在这台 Mac 上识别声音，不保存录音。先看一场示例，再决定要不要设置。")
                .font(Typo.body).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
        }
    }

    // MARK: 2 · Sample meeting

    private var sample: some View {
        VStack(alignment: .leading, spacing: Space.s6) {
            pageHeader("专注讨论，留下研究的下一步", "这是一段组会示例：进展、导师反馈和待验证的问题会保留下来。示例不录音、不申请权限、不调用模型。")
            OnboardingSampleMeeting(finished: $sampleFinished)
        }
    }

    // MARK: 3 · Sound (the next page, 你的声音, is OnboardingVoicePage)

    private var sound: some View {
        VStack(alignment: .leading, spacing: Space.s6) {
            pageHeader("准备好声音，就能开始", "先选线上或线下；语音识别准备好即可记录，不需要先配置 AI。")
            OnboardingVenuePicker()
            GuideList {
              if !state.isOfflineMeeting {
                GuideRow(tone: state.capturePermissionGranted ? .ready : .caution, label: "系统音频") {
                    HStack(alignment: .top, spacing: Space.s3) {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(LocalizedStringKey(state.capturePermissionGranted ? "已允许 · 可以听到会议里对方的声音" : "用来听到电脑里播放的会议声音"))
                                .font(Typo.body)
                            if !state.capturePermissionGranted {
                                Text("macOS 把它叫“屏幕与系统音频录制”。允许后，系统可能要求重新打开应用，引导会保留进度。")
                                    .font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
                            }
                        }
                        Spacer(minLength: Space.s2)
                        if !state.capturePermissionGranted {
                            Button("允许") { Task { await state.requestCapturePermission() } }.buttonStyle(SecondaryButtonStyle())
                        }
                    }
                }
                GuideDivider()
              }
                GuideRow(tone: microphoneTone, label: "麦克风") {
                    HStack(alignment: .top, spacing: Space.s3) {
                        Text(LocalizedStringKey(state.isOfflineMeeting ? startMicrophoneLine : microphoneLine)).font(Typo.body).fixedSize(horizontal: false, vertical: true)
                        Spacer(minLength: Space.s2)
                        if state.microphoneAccess == .denied {
                            Button("打开系统设置") { Task { await state.requestMicrophoneAccess() } }.buttonStyle(SecondaryButtonStyle())
                        }
                    }
                }
                GuideDivider()
                GuideRow(tone: state.recognitionReady ? .ready : .caution, label: "语音识别") { recognition }
            }
            Text("暂时不允许也没关系：看示例、整理资料、改设置都不受影响。线下会议只需要麦克风。")
                .font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
            Text(verbatim: state.recordingDataRouteSummary)
                .font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
        }
    }

    private var microphoneTone: StatusDot.Tone {
        switch state.microphoneAccess {
        case .granted: .ready
        case .undetermined: .idle
        case .denied: .caution
        }
    }

    private var microphoneLine: String {
        if state.isOfflineMeeting {
            switch state.microphoneAccess {
            case .granted: "已允许 · 线下声音会标为“现场”"
            case .undetermined: "线下会议需要麦克风；开始记录时会询问。"
            case .denied: "未允许 · 线下记录需要麦克风，请在系统设置中开启。"
            }
        } else {
            switch state.microphoneAccess {
            case .granted: "已允许 · 你说的话会标为“我”"
            case .undetermined: "现在不用允许。第一次开始记录时才会询问，用来标出“我”说的话。"
            case .denied: "未允许 · 只能记录对方的声音。需要时到系统设置里打开。"
            }
        }
    }

    @ViewBuilder private var recognition: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: Space.s2) {
            switch state.transcriptionBackend {
            case "zipformer":
                if state.isDownloadingModel {
                    HStack(spacing: Space.s3) {
                        VStack(alignment: .leading, spacing: 6) {
                            Text("正在下载 \(Int(state.modelDownloadProgress * 100))%").font(Typo.body)
                            ProgressView(value: state.modelDownloadProgress).controlSize(.small)
                        }
                        Button("取消") { state.cancelModelDownload() }.buttonStyle(SecondaryButtonStyle())
                    }
                } else if state.isModelInstalled {
                    Text("\(state.localStreamingModel.shortTitle) · 本机离线识别").font(Typo.body)
                    if !state.missingPackages.isEmpty {
                        Text("本机识别已就绪；可选精修组件可稍后下载，不影响开始记录。")
                            .font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
                    }
                } else {
                    let size = max(state.missingPackages.reduce(0) { $0 + $1.approximateMegabytes }, 1)
                    HStack(alignment: .top, spacing: Space.s3) {
                        Text("需要下载本机识别模型，约 \(size) MB，只需一次。下载时可以先看后面的步骤。")
                            .font(Typo.body).fixedSize(horizontal: false, vertical: true)
                        Spacer(minLength: Space.s2)
                        Button("下载") { state.downloadModel() }
                            .buttonStyle(SecondaryButtonStyle()).disabled(state.modelControlsDisabled)
                    }
                }
                if !state.modelDownloadStatus.isEmpty {
                    Text(LocalizedStringKey(state.modelDownloadStatus)).font(Typo.caption).foregroundStyle(Ink.secondary)
                }
            case "apple":
                Text(LocalizedStringKey(state.recognitionReady ? "系统语音识别已就绪" : "系统语音识别需 macOS 26，并明确选择一种语言")).font(Typo.body)
                MenuPicker(title: "语言", selection: $state.recognitionLanguage, options: [("zh", "中文"), ("en", "English")])
                    .onChange(of: state.recognitionLanguage) { _, _ in state.updateSettings() }
            default:
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
        }
    }

    // MARK: 5 · Answers

    private var answers: some View {
        VStack(alignment: .leading, spacing: Space.s6) {
            pageHeader("接上一个回答服务", "会中的参考回答、会后的纪要，都由它来写。选一个服务商，粘贴 Key，点一下检查。")
            AnswerEngineForm()
            if !state.answerEngineReady {
                Text("可以先开始本机记录；连接后才能生成会中回答和会后纪要。")
                    .font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    // MARK: 6 · Materials

    private var materials: some View {
        OnboardingMaterialsPage()
    }

    // MARK: 7 · Start

    private var readiness: RealtimeSetupReadiness { state.liveSetupReadiness }

    /// The microphone is asked for when recording starts, so only a refusal blocks.
    /// In-person meetings always use it, even if the online microphone toggle is off.
    private var microphoneBlocks: Bool { state.usesMicrophone && state.microphoneAccess == .denied }

    private var canStart: Bool { state.recordingReadiness.canRequestStart }

    private var start: some View {
        VStack(alignment: .leading, spacing: Space.s6) {
            pageHeader("选个声音来源，开始",
                       state.isOfflineMeeting ? "线下会议只用麦克风，转写会标为“现场”。" : "默认听这台 Mac 播放的所有声音。打开会议软件后，点“开始记录”。")
            GuideList {
                GuideRow(tone: readiness.systemAudioReady ? .ready : .caution, label: "声音") {
                    HStack(spacing: Space.s3) {
                        OnboardingVenuePicker()
                        if state.isOfflineMeeting {
                            Label("线下会议：只用麦克风", systemImage: "mic")
                                .font(Typo.body).foregroundStyle(Ink.ink)
                        } else if readiness.capture {
                            AudioSourceMenu()
                            if !readiness.audioSource {
                                Text("没有找到可用的声音来源").font(Typo.caption).foregroundStyle(Ink.caution)
                            }
                        } else {
                            Text("还没有允许听会议的声音").font(Typo.body).foregroundStyle(Ink.caution)
                        }
                        Spacer(minLength: 0)
                        if !state.isOfflineMeeting && !readiness.capture {
                            Button("去允许") { go(.sound) }.buttonStyle(GhostButtonStyle(height: 28))
                        }
                    }
                }
                GuideDivider()
                GuideRow(tone: readiness.speech ? .ready : .caution, label: "识别") {
                    HStack(spacing: Space.s3) {
                        Text(LocalizedStringKey(readiness.speech ? "语音识别已就绪" : "语音识别还没准备好"))
                            .font(Typo.body).foregroundStyle(readiness.speech ? Ink.ink : Ink.caution)
                        Spacer(minLength: 0)
                        if !readiness.speech {
                            Button("去准备") { go(.sound) }.buttonStyle(GhostButtonStyle(height: 28))
                        }
                    }
                }
                GuideDivider()
                GuideRow(tone: microphoneBlocks ? .caution : (state.microphoneAccess == .granted ? .ready : .idle), label: "麦克风") {
                    HStack(spacing: Space.s3) {
                        Text(LocalizedStringKey(startMicrophoneLine)).font(Typo.body)
                            .foregroundStyle(microphoneBlocks ? Ink.caution : Ink.ink)
                            .fixedSize(horizontal: false, vertical: true)
                        Spacer(minLength: 0)
                        if microphoneBlocks && state.isOfflineMeeting {
                            Button("打开系统设置") { Task { await state.requestMicrophoneAccess() } }
                                .buttonStyle(GhostButtonStyle(height: 28))
                        } else if microphoneBlocks {
                            Button("只记录对方") { state.microphoneEnabled = false; state.updateSettings() }
                                .buttonStyle(GhostButtonStyle(height: 28))
                        }
                    }
                }
                GuideDivider()
                GuideRow(tone: .idle, label: "AI 可选") {
                    HStack(spacing: Space.s3) {
                        if readiness.answers {
                            Text(verbatim: answerLine).font(Typo.body).lineLimit(1)
                        } else {
                            Text("未配置 · 不影响记录").font(Typo.body).foregroundStyle(Ink.secondary)
                        }
                        Spacer(minLength: 0)
                        Button("设置 AI（可选）") { state.finishOnboarding(); state.openSettings(.answers) }
                            .buttonStyle(GhostButtonStyle(height: 28))
                    }
                }
                GuideDivider()
                GuideRow(tone: .idle, label: "收尾") {
                    AutoEndGuideChoice()
                }
            }
            Text(verbatim: state.recordingDataRouteSummary).font(Typo.caption)
                .foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
            Text("声纹、术语、资料增强与更多设置，可在需要时再开启。")
                .font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
            if let error = state.errorMessage, !error.isEmpty {
                Text(verbatim: error).font(Typo.caption).foregroundStyle(Ink.caution).fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    private var startMicrophoneLine: String {
        if state.isOfflineMeeting {
            switch state.microphoneAccess {
            case .granted: return "已允许 · 麦克风已准备好记录现场声音"
            case .undetermined: return "开始记录时会询问麦克风权限"
            case .denied: return "未允许 · 线下会议需要麦克风权限"
            }
        }
        if !state.microphoneEnabled { return "不使用麦克风 · 只记录对方的声音" }
        switch state.microphoneAccess {
        case .granted: return "已允许 · 你说的话会标为“我”"
        case .undetermined: return "点“开始记录”时会询问，用来标出“我”说的话"
        case .denied: return "未允许，无法记录你说的话"
        }
    }

    private var answerLine: String {
        state.answerBackend == .openAICompatible ? "API · \(state.apiModel)" : "Codex · \(state.codexModelID)"
    }

    private func startMeeting() {
        guard canStart, !starting else { return }
        starting = true
        Task {
            defer { starting = false }
            // Finish before showing the shared first-recording notice, whose
            // pending start survives dismissing the onboarding overlay.
            state.finishOnboarding()
            state.beginNewMeetingAfterWorkspaceEdit()
            state.setDraftType(.labMeeting)
            await state.startPreparedMeeting()
        }
    }
}

/// The onboarding flow can start either kind of session, so make its audio mode
/// explicit before showing the online system-audio controls.
private struct OnboardingVenuePicker: View {
    @Environment(AppState.self) private var state

    var body: some View {
        InkSegmented(items: MeetingVenue.allCases.map { (value: $0, title: $0.title) },
                     selection: Binding(get: { state.meetingVenue }, set: { state.setMeetingVenue($0) }))
            .fixedSize()
    }
}

/// 引导最后一步的一行：会议没人说话时怎么办。选择和 设置 → 会议自动收尾 是同一份。
private struct AutoEndGuideChoice: View {
    @AppStorage(MeetingAutoEnd.modeKey, store: UserDefaults.standard) private var mode = MeetingEndWatch.Mode.prompt.rawValue

    var body: some View {
        HStack(spacing: Space.s3) {
            Text(LocalizedStringKey(line)).font(Typo.body).foregroundStyle(Ink.ink)
                .fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 0)
            Picker("会议没人说话时", selection: $mode) {
                Text("提醒后结束").tag(MeetingEndWatch.Mode.prompt.rawValue)
                Text("直接结束").tag(MeetingEndWatch.Mode.end.rawValue)
                Text("不自动结束").tag(MeetingEndWatch.Mode.off.rawValue)
            }
            .labelsHidden()
            .pickerStyle(.menu)
            .fixedSize()
        }
    }

    private var line: String {
        mode == MeetingEndWatch.Mode.off.rawValue
            ? "会议结束后需要自己点“结束”"
            : "没人说话时自动收尾，可在设置里改时长"
    }
}
