import AVFoundation
import AppKit
import SwiftUI
import InterviewHackerCore

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
        default: isModelInstalled && missingPackages.isEmpty
        }
    }

    var liveSetupReadiness: RealtimeSetupReadiness {
        .init(speech: recognitionReady, capture: capturePermissionGranted, microphoneRequired: microphoneEnabled,
              microphone: microphoneAccess == .granted, answers: answerEngineReady,
              audioSource: captureTargets.contains { $0.id == selectedTargetID })
    }
    var currentGuideVersion: String {
        OnboardingVersion.guideVersion(Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "1.8.0")
    }
    func prepareOnboardingForCurrentVersion() {
        let defaults = UserDefaults.standard
        if defaults.string(forKey: "onboardingGuideStartedVersion") != currentGuideVersion {
            defaults.set(0, forKey: "quickStartStep")
            defaults.set(currentGuideVersion, forKey: "onboardingGuideStartedVersion")
        }
        isOnboardingPresented = OnboardingVersion.needsGuide(appVersion: currentGuideVersion, completedVersion: defaults.string(forKey: "onboardingGuideCompletedVersion"))
    }

    func openOnboarding() {
        UserDefaults.standard.set(0, forKey: "quickStartStep")
        withAnimation(.easeOut(duration: 0.2)) { isOnboardingPresented = true }
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
    func testAnswerEngine() async {
        guard engineTest != .running else { return }
        let model = currentAnswerModel
        guard !model.isEmpty else { engineTest = .failed("请先选择或填写模型"); return }
        if answerBackend != .openAICompatible, !codexInstalled { engineTest = .failed("未检测到 Codex"); return }
        engineTest = .running
        let request = AnswerRequest(system: "这是连通性测试。只回复两个字：收到。", user: "测试", model: model, effort: codexEfforts.first ?? codexReasoning,
                                    serviceTier: activeServiceTier)
        let started = Date()
        do {
            let reply = try await runAnswer(request, backend: answerBackend) { _ in }
            let text = reply.trimmingCharacters(in: .whitespacesAndNewlines)
            engineTest = text.isEmpty ? .failed("模型没有返回文本") : .passed(seconds: Date().timeIntervalSince(started), reply: text)
        } catch {
            engineTest = .failed(error.localizedDescription)
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
            detail: microphoneAccess == .granted ? "已允许 · 你说的话标记为“我”" : "用于识别你自己说的话，并与对方区分",
            status: microphoneAccess == .granted ? .done : (microphoneEnabled ? .required : .recommended),
            actionTitle: microphoneAccess == .granted ? nil : (microphoneAccess == .undetermined ? "允许" : "打开系统设置"),
            action: { Task { await self.requestMicrophoneAccess() } }))
        let recognitionDetail: String
        switch transcriptionBackend {
        case "apple": recognitionDetail = recognitionReady ? "Apple Speech · \(recognitionLanguage == "en" ? "English" : "中文")" : "Apple Speech 需要 macOS 26 并选择中文或 English"
        case "cloud": recognitionDetail = recognitionReady ? "云端流式 · \(cloudProvider.title)" : "云端识别需要填写 API Key 并确认数据说明"
        default:
            let size = missingPackages.reduce(0) { $0 + $1.approximateMegabytes }
            recognitionDetail = isDownloadingModel ? "正在下载 \(Int(modelDownloadProgress * 100))%"
                : recognitionReady ? "\(localStreamingModel.shortTitle) · 本机离线识别" : "必需：下载本机识别模型（约 \(max(size, 1)) MB，只需一次）"
        }
        items.append(SetupItem(
            id: "asr", icon: "waveform", title: "语音识别", detail: recognitionDetail,
            status: isDownloadingModel ? .working : recognitionReady ? .done : .required,
            actionTitle: recognitionReady || isDownloadingModel ? nil : (transcriptionBackend == "zipformer" ? "下载" : "设置"),
            action: { self.transcriptionBackend == "zipformer" ? self.downloadModel() : self.openSettings(.recognition) }))
        let engineDetail: String
        if answerBackend == .openAICompatible {
            engineDetail = answerEngineReady ? "API · \(apiModel)" : "必需：填写 API 地址、模型和 Key"
        } else if !codexInstalled {
            engineDetail = "必需：安装 Codex 并用 ChatGPT 账号登录，或改用 API Key"
        } else if !codexReady {
            engineDetail = "必需：在终端运行 codex login 登录后点刷新"
        } else {
            engineDetail = "Codex · \(codexModelID) · \(codexReasoning)" + (codexFastMode && codexFastAvailable ? " · Fast" : "")
        }
        items.append(SetupItem(
            id: "engine", icon: "sparkles", title: "回答引擎", detail: engineDetail,
            status: answerEngineReady ? .done : .required,
            actionTitle: answerEngineReady ? nil : "设置",
            action: { self.openSettings(.answers) }))
        let workspace = activeWorkspace
        items.append(SetupItem(
            id: "workspace", icon: workspace.mode.symbol, title: "当前场景",
            detail: "\(workspace.name) · \(workspace.mode.title) · 资料 \(workspace.documents.count) 份" + (workspace.documents.isEmpty ? "（建议添加简历、JD 等）" : ""),
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
        VStack(spacing: 6) {
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

struct SetupRow: View {
    let item: SetupItem

    var body: some View {
        HStack(spacing: 12) {
            Image(systemName: item.icon)
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(item.status == .done ? Color.green : Brand.accent)
                .frame(width: 30, height: 30)
                .background((item.status == .done ? Color.green : Brand.accent).opacity(0.14), in: RoundedRectangle(cornerRadius: 8, style: .continuous))
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Text(item.title).font(.system(size: 13, weight: .semibold))
                    badge
                }
                Text(item.detail).font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(2)
            }
            Spacer(minLength: 8)
            if item.status == .working {
                ProgressView().controlSize(.small)
            } else if let title = item.actionTitle, let action = item.action {
                Button(title, action: action)
                    .buttonStyle(ChipButtonStyle(prominent: item.status == .required))
            }
        }
        .padding(.horizontal, 10).padding(.vertical, 8)
        .background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }

    @ViewBuilder private var badge: some View {
        switch item.status {
        case .done: Image(systemName: "checkmark.circle.fill").font(.system(size: 11)).foregroundStyle(.green)
        case .required: Text("必需").font(.system(size: 10, weight: .semibold)).foregroundStyle(.orange)
        case .recommended: Text("建议").font(.system(size: 10, weight: .medium)).foregroundStyle(.secondary)
        case .working: EmptyView()
        }
    }
}

// MARK: - Quick start

struct OnboardingView: View {
    @Environment(AppState.self) private var state
    @AppStorage("quickStartStep") private var step = 0
    @State private var choseBuiltIn = false
    private let titles = ["认识助手", "准备语音", "连接对话", "资料可选", "开始辅助"]

    var body: some View {
        GeometryReader { geometry in
            ZStack {
                Color.black.opacity(0.4).ignoresSafeArea()
                VStack(spacing: 0) {
                    HStack {
                        Text("快速开始").font(.system(size: 17, weight: .semibold))
                        Spacer()
                        Text("\(step + 1) / 5 · \(titles[min(max(step, 0), 4)])").foregroundStyle(.secondary)
                    }.padding(22)
                    Divider()
                    ScrollView {
                        VStack(alignment: .leading, spacing: 18) {
                            switch step {
                            case 1: voice
                            case 2: connection
                            case 3: choices
                            case 4: startSession
                            default: overview
                            }
                        }.frame(maxWidth: .infinity, alignment: .leading).padding(24)
                    }
                    Divider()
                    HStack {
                        Button("稍后继续") { state.isOnboardingPresented = false }
                            .buttonStyle(.plain).foregroundStyle(.secondary)
                        Spacer()
                        if step > 0 { Button("上一步") { step -= 1 }.buttonStyle(SecondaryButtonStyle()) }
                        if step < 4 {
                            Button(step == 0 ? "开始" : "下一步") { step += 1 }
                                .buttonStyle(PrimaryButtonStyle()).keyboardShortcut(.defaultAction)
                        } else {
                            Button(state.isChangingSession ? "正在开始…" : !coreReady ? "请先完成核心准备" : !hasAudioSource ? "请先选择会议窗口" : "开始实时辅助") {
                                Task {
                                    await state.startSession()
                                    if state.isRunning { state.finishOnboarding() }
                                }
                            }.buttonStyle(PrimaryButtonStyle()).keyboardShortcut(.defaultAction)
                                .disabled(!coreReady || !hasAudioSource || state.isChangingSession)

                        }
                    }.padding(20)
                }
                .frame(width: min(740, geometry.size.width - 40), height: min(step == 1 || step == 3 ? 640 : 560, geometry.size.height - 40))
                .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 20))
                .overlay(RoundedRectangle(cornerRadius: 20).strokeBorder(Color.primary.opacity(0.12)))
                .shadow(color: .black.opacity(0.3), radius: 24, y: 10)
            }
        }
        // Read-only checks: no audio permission prompts, downloads, or model calls.
        .task {
            step = min(max(step, 0), 4)
            if !DemoMode.isEnabled { await state.refreshSetupStatus() }
        }
    }
    private var overview: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text("听到提问，接住对话").font(.system(size: 26, weight: .bold))
            Text("InterviewHacker 是面试、报告和会议中的实时对话助手：听懂对方的问题，在对话过程中给你可参考的回答。")
                .foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            journey("1", "听会议里的声音", "选中正在开会的软件窗口，持续转写你和对方的对话。")
            journey("2", "识别对方的提问", "判断哪句话是在向你提问，结合上下文理解追问。")
            journey("3", "给出建议回答", "对话服务生成回答，你可以在助手或悬浮提词器里参考。")
            journey("4", "带上背景，再复盘", "简历、JD、论文等资料能帮助回答贴合场景；会后回看记录。")
            Label("必须准备：实时语音与对话回答。OCR 增强、向量检索按需启用。", systemImage: "checklist")
                .font(.system(size: 12)).foregroundStyle(.green)
        }
    }
    private var connection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("连接对话回答服务").font(.system(size: 23, weight: .bold))
            Text("对话回答是核心功能。可以沿用已登录的 Codex，也可以填写自己的 API 服务；生成建议回答会使用所选服务的网络与额度。")
                .foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            AnswerEngineForm(foldSecondary: true)
            EngineTestRow()
            Label(state.answerEngineReady ? "对话设置已就绪，可测试回答" : "尚未连接，开始实时辅助前需要完成", systemImage: state.answerEngineReady ? "checkmark.circle.fill" : "info.circle")
                .foregroundStyle(state.answerEngineReady ? .green : .secondary)
        }
    }
    private var choices: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("资料功能，按需选择").font(.system(size: 23, weight: .bold))
            Text("语音与对话服务准备好后，资料能力可按需增强。系统识别和关键词查找无需额外下载；下面这些不是开始辅助的必需条件。")
                .foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            DocumentChoiceSummary()
            Button("资料采用系统识别 + 关键词（无需额外下载）") {
                state.selectDocumentProvider(.system); state.selectSemanticRetrieval(false); choseBuiltIn = true
            }.buttonStyle(SecondaryButtonStyle())
            if choseBuiltIn { Label("资料已设为无需额外下载的方案", systemImage: "checkmark.circle.fill").foregroundStyle(.green) }
            Text("需要时，在设置 → 资料里选择和安装。")
                .font(.caption).foregroundStyle(.secondary)
        }
    }
    private var coreVoiceReady: Bool {
        state.liveSetupReadiness.voiceReady
    }
    private var coreReady: Bool { state.liveSetupReadiness.coreReady }
    private var hasAudioSource: Bool { state.liveSetupReadiness.audioSource }
    private var voice: some View {
        @Bindable var state = state
        return VStack(alignment: .leading, spacing: 14) {
            Text("先让助手听到会议").font(.system(size: 23, weight: .bold))
            Text("实时语音是核心功能。需要音频权限和一个可用的识别引擎；推荐本机识别，会议音频留在这台 Mac。")
                .foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            readiness("会议声音", state.capturePermissionGranted ? "已允许采集会议窗口声音" : "需要录屏与系统音频权限", ready: state.capturePermissionGranted) {
                Button("允许") { Task { await state.requestCapturePermission() } }
            }
            if state.microphoneEnabled {
                readiness("麦克风", state.microphoneAccess == .granted ? "已允许，用于区分你和对方的话" : "需要麦克风权限，用于识别你说的话", ready: state.microphoneAccess == .granted) {
                    Button("允许") { Task { await state.requestMicrophoneAccess() } }
                }
            }
            Divider()
            Text("语音识别").font(.headline)
            if state.transcriptionBackend == "zipformer" { ModelDownloadRow() }
            else if state.transcriptionBackend == "apple" {
                Text(state.recognitionReady ? "系统语音识别已就绪" : "系统语音识别需 macOS 26，并明确选择一种语言")
                MenuPicker(title: "语言", selection: $state.recognitionLanguage, options: [("zh", "中文"), ("en", "English")])
                    .onChange(of: state.recognitionLanguage) { _, _ in state.updateSettings() }
            } else { CloudSettings() }
            DisclosureGroup("更换语音识别方式") {
                MenuPicker(title: "识别引擎", selection: $state.transcriptionBackend, options: [("zipformer", "本机识别（推荐）"), ("apple", "系统识别 · 需 macOS 26"), ("cloud", "云端识别 · 需 API Key")])
                    .disabled(state.modelControlsDisabled)
                    .onChange(of: state.transcriptionBackend) { _, _ in state.updateSettings(); Task { await state.refreshModelStatus() } }
                Note("本机模型需要下载并占内存；系统语音受系统版本与语言限制；云端识别会上传会议音频并使用服务额度。")
            }
            Text("下载中也可以先看后面的步骤。允许音频权限后，macOS 可能要求重新打开应用；引导会保留进度。")
                .font(.caption).foregroundStyle(.secondary)
        }
    }
    private var startSession: some View {
        @Bindable var state = state
        return VStack(alignment: .leading, spacing: 16) {
            Text("选中会议窗口，开始辅助").font(.system(size: 23, weight: .bold))
            Text("打开 Zoom、腾讯会议或正在通话的软件，选择它的窗口。开始后会实时转写对话，并在对方提问时生成建议回答。")
                .foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            readiness("实时语音", coreVoiceReady ? "识别与音频权限已就绪" : "还有识别或音频权限未准备好", ready: coreVoiceReady) {
                Button("回到语音设置") { step = 1 }
            }
            readiness("对话回答", state.answerEngineReady ? "对话服务已配置" : "需要连接回答服务", ready: state.answerEngineReady) {
                Button("回到对话设置") { step = 2 }
            }
            HStack {
                MenuPicker(title: "会议声音来自", selection: $state.selectedTargetID,
                           options: state.captureTargets.map { ($0.id, $0.title) }, placeholder: "选择会议窗口")
                Button("刷新") { Task { await state.refreshTargets() } }
            }
            if !hasAudioSource { Note("还没有可用的音频来源。请先打开会议软件，并确认音频权限。", color: .orange) }
            Note("最简单的一次使用：选窗口 → 开始实时辅助 → 对方提问 → 在“建议回答”里查看结果。悬浮阅读可打开提词器，会后到“记录”里回看。")
            if let error = state.errorMessage, !error.isEmpty { Note(error, color: .orange) }
        }
    }
    private func readiness<Action: View>(_ title: String, _ detail: String, ready: Bool, @ViewBuilder action: () -> Action) -> some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: ready ? "checkmark.circle.fill" : "exclamationmark.circle").foregroundStyle(ready ? .green : .orange)
            VStack(alignment: .leading, spacing: 3) {
                Text(title).fontWeight(.semibold)
                Text(detail).font(.system(size: 12)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: 8)
            if !ready { action().buttonStyle(SecondaryButtonStyle()) }
        }.padding(12).background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10))
    }
    private func journey(_ number: String, _ title: String, _ detail: String) -> some View {
        HStack(alignment: .top, spacing: 12) {
            Text(number).font(.system(size: 13, weight: .bold)).foregroundStyle(Brand.accent)
                .frame(width: 28, height: 28).background(Brand.accent.opacity(0.12), in: Circle())
            VStack(alignment: .leading, spacing: 4) {
                Text(title).font(.system(size: 14, weight: .semibold))
                Text(detail).font(.system(size: 12)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            }
        }
    }
}
