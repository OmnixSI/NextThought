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
    var codexReady: Bool { codexInstalled && codexLoginStatus.lowercased().contains("logged in") }

    var answerEngineReady: Bool {
        switch answerBackend {
        case .openAICompatible:
            let local = apiBaseURL.contains("localhost") || apiBaseURL.contains("127.0.0.1")
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

    func openOnboarding() {
        withAnimation(.easeOut(duration: 0.2)) { isOnboardingPresented = true }
    }

    func finishOnboarding(goTo section: AppSection = .assistant) {
        UserDefaults.standard.set(true, forKey: "onboardingCompleted")
        withAnimation(.easeOut(duration: 0.2)) { isOnboardingPresented = false }
        self.section = section
        Diagnostics.record("onboarding.finished", ["ready": isReadyToStart, "engine": answerEngineReady])
    }

    func openSettings(_ group: SettingsGroup) {
        UserDefaults.standard.set(group.rawValue, forKey: "settingsGroup")
        section = .settings
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
            detail: microphoneAccess == .granted ? "已允许 · 你说的话标记为“我”" : "建议：识别你自己说的话，避免把你的话当成提问",
            status: microphoneAccess == .granted ? .done : .recommended,
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
            actionTitle: "编辑", action: { self.section = .materials }))
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

// MARK: - Onboarding

enum OnboardingStep: Int, CaseIterable {
    case welcome, permissions, recognition, answers, extras, scenario, done

    var title: String {
        switch self {
        case .welcome: "欢迎"
        case .permissions: "权限"
        case .recognition: "识别"
        case .answers: "回答"
        case .extras: "联网与技能"
        case .scenario: "场景"
        case .done: "完成"
        }
    }
}

/// First-run guide: the few things that must be set before the first session.
/// Shown over the window (not as a sheet) so it sizes with the window.
struct OnboardingView: View {
    @Environment(AppState.self) private var state
    @AppStorage("onboardingStep") private var stepRaw = 0

    private var step: OnboardingStep { OnboardingStep(rawValue: stepRaw) ?? .welcome }

    var body: some View {
        ZStack {
            Color.black.opacity(0.38).ignoresSafeArea()
                .onTapGesture { }
            VStack(spacing: 0) {
                header
                Divider().opacity(0.4)
                ScrollView {
                    content
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal, 28).padding(.vertical, 22)
                }
                .scrollBounceBehavior(.basedOnSize)
                Divider().opacity(0.4)
                footer
            }
            .frame(width: 700)
            .frame(maxHeight: 620)
            .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 22, style: .continuous))
            .background(Color(nsColor: .windowBackgroundColor).opacity(0.6), in: RoundedRectangle(cornerRadius: 22, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 22, style: .continuous).strokeBorder(Color.primary.opacity(0.1)))
            .shadow(color: .black.opacity(0.3), radius: 30, y: 12)
            .padding(28)
        }
        .task { await state.refreshSetupStatus() }
    }

    private var header: some View {
        HStack(spacing: 10) {
            ForEach(OnboardingStep.allCases, id: \.rawValue) { item in
                let current = item == step, passed = item.rawValue < step.rawValue
                HStack(spacing: 6) {
                    ZStack {
                        Circle().fill(current ? AnyShapeStyle(Brand.gradient) : AnyShapeStyle(Color.primary.opacity(passed ? 0.18 : 0.08)))
                        if passed {
                            Image(systemName: "checkmark").font(.system(size: 9, weight: .bold))
                        } else {
                            Text("\(item.rawValue + 1)").font(.system(size: 10, weight: .bold)).foregroundStyle(current ? .white : .secondary)
                        }
                    }
                    .frame(width: 20, height: 20)
                    Text(item.title).font(.system(size: 12, weight: current ? .semibold : .regular))
                        .foregroundStyle(current ? .primary : .secondary)
                        .lineLimit(1).fixedSize()
                }
            }
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 16)
    }

    @ViewBuilder private var content: some View {
        switch step {
        case .welcome: WelcomeStep()
        case .permissions: PermissionsStep()
        case .recognition: RecognitionStep()
        case .answers: AnswersStep()
        case .extras: ExtrasStep()
        case .scenario: ScenarioStep()
        case .done: DoneStep()
        }
    }

    private var footer: some View {
        HStack {
            if step != .done {
                Button("跳过引导") { state.finishOnboarding() }.buttonStyle(.plain).foregroundStyle(.secondary)
                    .help("之后可以在 设置 → 基础 → 打开新手引导 重新进入")
            }
            Spacer()
            if step != .welcome {
                Button("上一步") { move(-1) }.buttonStyle(SecondaryButtonStyle())
            }
            if step == .done {
                Button { stepRaw = 0; state.finishOnboarding() } label: { Label("开始使用", systemImage: "arrow.right") }
                    .buttonStyle(PrimaryButtonStyle()).keyboardShortcut(.defaultAction)
            } else {
                Button { move(1) } label: { Text(nextTitle).frame(minWidth: 72) }
                    .buttonStyle(PrimaryButtonStyle()).keyboardShortcut(.defaultAction)
            }
        }
        .padding(.horizontal, 22).padding(.vertical, 14)
    }

    private var nextTitle: String {
        switch step {
        case .welcome: "开始设置"
        case .permissions where !state.capturePermissionGranted: "稍后再说"
        case .recognition where !state.recognitionReady && !state.isDownloadingModel: "稍后再说"
        case .answers where !state.answerEngineReady: "稍后再说"
        default: "下一步"
        }
    }

    private func move(_ delta: Int) {
        withAnimation(.easeInOut(duration: 0.2)) {
            stepRaw = min(max(stepRaw + delta, 0), OnboardingStep.allCases.count - 1)
        }
    }
}

private struct StepTitle: View {
    let title: String
    let subtitle: String
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title).font(.system(size: 20, weight: .bold))
            Text(subtitle).font(.system(size: 13)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
        }
        .padding(.bottom, 14)
    }
}

private struct WelcomeStep: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack(spacing: 16) {
                ZStack {
                    RoundedRectangle(cornerRadius: 16, style: .continuous).fill(Brand.gradient)
                    Image(systemName: "waveform.and.mic").font(.system(size: 28, weight: .semibold)).foregroundStyle(.white)
                }
                .frame(width: 64, height: 64)
                VStack(alignment: .leading, spacing: 4) {
                    Text("欢迎使用 InterviewHacker").font(.system(size: 22, weight: .bold))
                    Text("面试、学术会议、商务会议的实时提词助手").font(.system(size: 13)).foregroundStyle(.secondary)
                }
            }
            HStack(alignment: .top, spacing: 12) {
                feature("ear", "听对方说话", "本机实时转写会议里的声音，中英混说也能识别")
                feature("questionmark.bubble", "识别提问", "判断“这是不是在问我”，问完约 0.6 秒开始回答")
                feature("text.bubble", "给出回答", "结合简历、资料和经验库，逐字给出能直接说的回答")
            }
            VStack(alignment: .leading, spacing: 8) {
                Text("接下来几步，大约 2 分钟").font(.system(size: 13, weight: .semibold))
                ForEach(Array(["允许录屏与麦克风", "下载语音识别模型（约 270 MB，只需一次）", "连接回答引擎（Codex 或 API Key，可存多家预设）",
                               "可选：联网搜索、导入自己的技能、调整助手页布局", "建一个场景，放入你的资料"].enumerated()), id: \.offset) { index, text in
                    HStack(spacing: 8) {
                        Text("\(index + 1)").font(.system(size: 11, weight: .bold)).foregroundStyle(Brand.accent)
                            .frame(width: 20, height: 20).background(Brand.accent.opacity(0.14), in: Circle())
                        Text(text).font(.system(size: 13))
                    }
                }
                Text("所有选项之后都可以在 设置 → 基础 里修改。").font(.system(size: 11)).foregroundStyle(.secondary).padding(.top, 4)
            }
        }
    }

    private func feature(_ icon: String, _ title: String, _ detail: String) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Image(systemName: icon).font(.system(size: 18, weight: .semibold)).foregroundStyle(Brand.accent)
            Text(title).font(.system(size: 13, weight: .semibold))
            Text(detail).font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(12)
        .background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
    }
}

private struct PermissionsStep: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            StepTitle(title: "允许录屏与麦克风", subtitle: "macOS 需要你允许一次。录屏权限只用来采集会议窗口的声音，音频只在本机识别、不保存。")
            SetupChecklist(exclude: ["asr", "engine", "workspace"])
            Text("允许录屏后，系统可能要求重新打开应用；重新打开后会回到这一步。绑定本机签名会重启应用，之后再允许一次即可，以后更新都不用再授权。")
                .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                .padding(.top, 6)
        }
    }
}

private struct RecognitionStep: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 10) {
            StepTitle(title: "选择语音识别", subtitle: "推荐本机识别：离线、免费、中英混说准确，音频不离开这台 Mac。")
            option("zipformer", "cpu", "本机识别（推荐）", "X-ASR 中英模型，带标点，每句再整句精修；需下载约 270 MB")
            option("apple", "apple.logo", "Apple Speech", "系统自带，需 macOS 26，只能识别单一语言")
            option("cloud", "cloud", "云端识别", "Soniox / OpenAI / Deepgram，需要 API Key，音频会上传")
            Group {
                switch state.transcriptionBackend {
                case "zipformer": ModelDownloadRow()
                case "apple":
                    MenuPicker(title: "识别语言", selection: $state.recognitionLanguage, options: [("zh", "中文"), ("en", "English")])
                        .onChange(of: state.recognitionLanguage) { _, _ in state.updateSettings() }
                    if !LiveTranscriberFactory.supported { Note("这台 Mac 的系统版本低于 macOS 26，请选择本机识别。", color: .orange) }
                default: CloudSettings()
                }
            }
            .padding(.top, 6)
        }
    }

    private func option(_ value: String, _ icon: String, _ title: String, _ detail: String) -> some View {
        let selected = state.transcriptionBackend == value
        return Button {
            state.transcriptionBackend = value
            state.updateSettings()
            Task { await state.prewarmRecognizer() }
        } label: {
            HStack(spacing: 12) {
                Image(systemName: selected ? "largecircle.fill.circle" : "circle")
                    .foregroundStyle(selected ? Brand.accent : .secondary)
                Image(systemName: icon).frame(width: 20)
                VStack(alignment: .leading, spacing: 2) {
                    Text(title).font(.system(size: 13, weight: .semibold))
                    Text(detail).font(.system(size: 11)).foregroundStyle(.secondary)
                }
                Spacer()
            }
            .padding(10)
            .background(selected ? Brand.accent.opacity(0.1) : Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).strokeBorder(selected ? Brand.accent.opacity(0.5) : .clear))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .disabled(state.modelControlsDisabled)
    }
}

private struct AnswersStep: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            StepTitle(title: "连接回答引擎", subtitle: "有 ChatGPT 账号就用 Codex（不额外付费，可开 Fast 模式更快）；也可以填任意 OpenAI 兼容服务的 API Key，多家服务商可以各存一个预设，随时切换。")
            AnswerEngineForm()
            EngineTestRow().padding(.top, 4)
        }
    }
}

/// Optional extras: web search, skills, the assistant layout.
private struct ExtrasStep: View {
    @Environment(AppState.self) private var state
    @State private var editing: Skill?
    private var library: SkillLibrary { SkillLibrary.shared }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            StepTitle(title: "联网、技能与布局（可选）", subtitle: "都可以先跳过，之后在设置里随时打开。")
            WebSearchSettings()
            SettingsSection(title: "技能", icon: "wand.and.stars") {
                Note("技能是一份 Markdown 说明（如 SKILL.md）：你的说话方式、答题套路、领域知识。这里导入的技能用于所有场景；只给某个场景用的，之后在 场景 → 资料与要求 → 技能 里勾选。")
                HStack(spacing: 8) {
                    Button { library.importFiles(global: true) } label: { Label("导入 .md 文件…", systemImage: "square.and.arrow.down") }
                        .buttonStyle(SecondaryButtonStyle())
                    Button { editing = library.create(global: true) } label: { Label("从模板新建", systemImage: "plus") }
                        .buttonStyle(SecondaryButtonStyle())
                    Spacer()
                }
                if !library.skills.isEmpty {
                    Text("已有：" + library.skills.map(\.name).joined(separator: "、"))
                        .font(.system(size: 12)).foregroundStyle(.secondary).lineLimit(2)
                }
            }
            SettingsSection(title: "助手页布局", icon: "rectangle.3.group") {
                Note("助手页由 实时对话、当前问题、建议回答、回答记录 四个模块组成。鼠标移到模块顶部会出现把手：拖动换位置，拖出窗口变成独立小窗，拖回来就合并；模块之间的空隙可以拖动调整大小。")
                HStack(spacing: 6) {
                    Text("先选一个布局：").foregroundStyle(.secondary)
                    ForEach(AssistantLayout.Preset.allCases) { preset in
                        Button(preset.shortTitle) { state.updateLayout { $0 = preset.layout } }
                            .buttonStyle(ChipButtonStyle(prominent: state.assistantLayout == preset.layout))
                    }
                }
            }
            Text("想让一个更快的模型来判断“这句是不是在问我”“要不要联网”，可以在 设置 → 回答 → 高级选项 → 判断模型（JEV）里接入。")
                .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
        }
        .sheet(item: $editing) { skill in SkillEditor(skill: skill) { library.update($0) } }
    }
}

private struct ScenarioStep: View {
    @Environment(AppState.self) private var state
    @State private var mode: SessionMode = .interview
    @State private var name = ""
    @State private var created: String?

    private static let details: [SessionMode: String] = [
        .interview: "简历、JD、公司、八股面经；会后逐题复盘",
        .academic: "论文、PPT、讲稿；应对听众提问",
        .meeting: "议程、报价、我方立场；记录决定与承诺",
        .lesson: "课件、笔记；整理知识点",
        .general: "任何需要实时提词的场合",
    ]

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            StepTitle(title: "建立第一个场景", subtitle: "每场面试、每次报告各用一套资料和经验库。先选模式起个名字，资料可以稍后在“场景”页添加。")
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 196), spacing: 8)], spacing: 8) {
                ForEach(SessionMode.allCases) { item in
                    let selected = item == mode
                    Button { mode = item } label: {
                        HStack(alignment: .top, spacing: 10) {
                            Image(systemName: item.symbol).font(.system(size: 15, weight: .semibold))
                                .foregroundStyle(selected ? .white : Brand.accent)
                                .frame(width: 30, height: 30)
                                .background(selected ? AnyShapeStyle(Brand.gradient) : AnyShapeStyle(Brand.accent.opacity(0.12)), in: RoundedRectangle(cornerRadius: 8, style: .continuous))
                            VStack(alignment: .leading, spacing: 2) {
                                Text(item.title).font(.system(size: 13, weight: .semibold))
                                Text(Self.details[item] ?? "").font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                            }
                            Spacer(minLength: 0)
                        }
                        .padding(10)
                        .frame(maxWidth: .infinity, minHeight: 64, alignment: .topLeading)
                        .background(selected ? Brand.accent.opacity(0.1) : Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                        .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).strokeBorder(selected ? Brand.accent.opacity(0.5) : .clear))
                        .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                }
            }
            HStack {
                TextField(placeholder, text: $name).textFieldStyle(.roundedBorder).onSubmit(create)
                Button("创建并使用", action: create).buttonStyle(PrimaryButtonStyle())
            }
            if let created {
                Label("已创建「\(created)」并设为当前场景", systemImage: "checkmark.circle.fill").font(.system(size: 12)).foregroundStyle(.green)
            } else {
                Text("当前场景：\(state.activeWorkspace.name)（\(state.sessionMode.title)）。不新建也可以，直接下一步。")
                    .font(.system(size: 11)).foregroundStyle(.secondary)
            }
        }
    }

    private var placeholder: String {
        switch mode {
        case .interview: "例如：字节后端二面"
        case .academic: "例如：ACL 2026 口头报告"
        case .meeting: "例如：周一客户例会"
        case .lesson: "例如：机器学习第 5 讲"
        case .general: "例如：日常会议"
        }
    }

    private func create() {
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        state.createWorkspace(mode: mode, name: trimmed.isEmpty ? nil : trimmed)
        created = state.activeWorkspace.name
        name = ""
    }
}

private struct DoneStep: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            StepTitle(title: state.isReadyToStart && state.answerEngineReady ? "一切就绪" : "还差几项",
                      subtitle: state.isReadyToStart && state.answerEngineReady
                          ? "点“开始使用”，在助手页选择会议窗口后点“开始”。"
                          : "标为“必需”的项目完成后才能正常使用；之后可以随时在 设置 → 基础 查看。")
            SetupChecklist()
            VStack(alignment: .leading, spacing: 6) {
                Text("常用快捷键").font(.system(size: 12, weight: .semibold)).padding(.top, 8)
                shortcut(state.shortcutLabel(.forceAnswer), "立即回答最新的问题")
                shortcut(state.shortcutLabel(.toggleTeleprompter), "显示 / 隐藏提词器（可放在摄像头下方）")
                shortcut(state.shortcutLabel(.panicHide), "一键隐藏全部窗口")
                Text("小技巧").font(.system(size: 12, weight: .semibold)).padding(.top, 8)
                Text("· 需要最新信息的问题会先给快速回答，联网结果查好后自动替换，可切回原回答。\n· 鼠标移到助手页模块顶部，拖动把手可以换位置或拖出成独立窗口；输入框旁的“布局”按钮可以换预设。\n· 你说的话被当成回声时会灰色显示，点“是我说的”恢复。")
                    .font(.system(size: 12)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    private func shortcut(_ keys: String, _ text: String) -> some View {
        HStack(spacing: 10) {
            Text(keys).font(.system(size: 11, weight: .semibold, design: .rounded))
                .padding(.horizontal, 8).padding(.vertical, 3)
                .background(Color.primary.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
                .frame(minWidth: 70, alignment: .leading)
            Text(text).font(.system(size: 12)).foregroundStyle(.secondary)
        }
    }
}
