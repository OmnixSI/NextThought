import AppKit
import SwiftUI
import InterviewHackerCore

/// Six groups: “基础” has everything needed to get a session working; the others
/// open with their main options and keep the rest under “高级选项”.
enum SettingsGroup: String, CaseIterable, Identifiable {
    case essentials = "基础", recognition = "识别", answers = "回答", skills = "技能", overlay = "提词器", recording = "数据", more = "更多"
    var id: String { rawValue }
    var symbol: String {
        switch self {
        case .essentials: "checklist"
        case .recognition: "waveform"
        case .answers: "sparkles"
        case .skills: "wand.and.stars"
        case .overlay: "rectangle.topthird.inset.filled"
        case .recording: "tray.full"
        case .more: "ellipsis.circle"
        }
    }

    /// Accepts names stored by older versions (常规 / 更新 / 快捷键).
    init(stored: String) {
        switch stored {
        case "常规": self = .essentials
        case "更新", "快捷键": self = .more
        default: self = SettingsGroup(rawValue: stored) ?? .essentials
        }
    }
}

/// Same shape as “场景”: groups on the left, the chosen group's settings on the right.
struct SettingsView: View {
    @Environment(AppState.self) private var state
    @AppStorage("settingsGroup") private var groupRaw = SettingsGroup.essentials.rawValue

    var body: some View {
        let group = SettingsGroup(stored: groupRaw)
        GeometryReader { proxy in
            let wide = proxy.size.width >= 820
            HStack(alignment: .top, spacing: 12) {
                SettingsSidebar(selection: group, compact: !wide) { groupRaw = $0.rawValue }
                    .frame(width: wide ? 250 : 200)
                ScrollView {
                    VStack(alignment: .leading, spacing: 12) {
                        SettingsPageHeader(group: group)
                        switch group {
                        case .essentials: EssentialSettings()
                        case .recognition: RecognitionSettings()
                        case .answers: AnswerSettings()
                        case .skills: SkillSettings()
                        case .overlay: OverlaySettings()
                        case .recording: RecordingSettings()
                        case .more: MoreSettings()
                        }
                    }
                    .frame(maxWidth: 760, alignment: .leading)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.bottom, 18)
                }
                .id(group)
            }
            .padding(.horizontal, 16).padding(.bottom, 14).padding(.top, 4)
        }
    }
}

extension SettingsGroup {
    var summary: String {
        switch self {
        case .essentials: "使用前检查 · 音频来源 · 外观"
        case .recognition: "识别引擎 · 模型 · 专业词"
        case .answers: "回答引擎 · 联网搜索 · 判断模型"
        case .skills: "加载 SKILL.md · 全局或按场景"
        case .overlay: "位置 · 字号 · 屏幕共享"
        case .recording: "会话记录 · 导入导出 · 备份"
        case .more: "更新 · 权限 · 快捷键 · 手机伴侣"
        }
    }
}

private struct SettingsSidebar: View {
    @Environment(AppState.self) private var state
    let selection: SettingsGroup
    var compact: Bool
    let select: (SettingsGroup) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            VStack(alignment: .leading, spacing: 2) {
                Text("设置").font(.system(size: 15, weight: .semibold))
                Text("先完成“基础”，其余按需调整").font(.system(size: 11)).foregroundStyle(.secondary)
            }
            ScrollView {
                VStack(alignment: .leading, spacing: 6) {
                    ForEach(SettingsGroup.allCases) { group in row(group) }
                }
            }
        }
        .card()
        .frame(maxHeight: .infinity, alignment: .top)
    }

    private func row(_ group: SettingsGroup) -> some View {
        let selected = group == selection
        return HStack(spacing: 10) {
            Image(systemName: group.symbol)
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(selected ? Color.white : Brand.accent)
                .frame(width: 30, height: 30)
                .background(selected ? AnyShapeStyle(Brand.gradient) : AnyShapeStyle(Brand.accent.opacity(0.12)),
                            in: RoundedRectangle(cornerRadius: 8, style: .continuous))
            VStack(alignment: .leading, spacing: 2) {
                Text(group.rawValue).font(.system(size: 13, weight: selected ? .semibold : .medium)).lineLimit(1)
                if !compact {
                    Text(group.summary).font(.system(size: 10.5)).foregroundStyle(.secondary).lineLimit(1)
                }
            }
            Spacer(minLength: 0)
            badge(group)
        }
        .padding(8)
        .background(selected ? Brand.accent.opacity(0.14) : Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 11, style: .continuous))
        .contentShape(Rectangle())
        .onTapGesture { select(group) }
        .help(group.summary)
    }

    @ViewBuilder private func badge(_ group: SettingsGroup) -> some View {
        switch group {
        case .essentials:
            let pending = state.setupItems(confirmBinding: {}).filter { $0.status == .required }.count
            if pending > 0 {
                Text("\(pending)").font(.system(size: 10, weight: .bold)).foregroundStyle(.white)
                    .frame(minWidth: 18, minHeight: 18).background(Color.orange, in: Capsule())
                    .help("还有 \(pending) 项必需设置未完成")
            }
        case .more:
            if Updater.shared.phase == .ready || Updater.shared.phase == .available {
                Circle().fill(Brand.accent).frame(width: 8, height: 8).help("有新版本")
            }
        default: EmptyView()
        }
    }
}

private struct SettingsPageHeader: View {
    let group: SettingsGroup
    var body: some View {
        HStack(spacing: 12) {
            Image(systemName: group.symbol)
                .font(.system(size: 17, weight: .semibold)).foregroundStyle(.white)
                .frame(width: 38, height: 38)
                .background(Brand.gradient, in: RoundedRectangle(cornerRadius: 10, style: .continuous))
            VStack(alignment: .leading, spacing: 2) {
                Text(group.rawValue).font(.system(size: 18, weight: .bold))
                Text(group.summary).font(.system(size: 12)).foregroundStyle(.secondary)
            }
        }
        .padding(.horizontal, 4).padding(.top, 2).padding(.bottom, 2)
    }
}

/// Card with an icon title, shared by every settings group.
struct SettingsSection<Content: View>: View {
    let title: String
    let icon: String
    @ViewBuilder var content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 10) {
                Image(systemName: icon)
                    .font(.system(size: 12, weight: .semibold)).foregroundStyle(.white)
                    .frame(width: 26, height: 26)
                    .background(Brand.gradient, in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                Text(title).font(.system(size: 14, weight: .semibold))
            }
            content
        }
        .font(.system(size: 13))
        .card()
    }
}

/// Collapsed by default; remembers whether the user opened it.
struct AdvancedOptions<Content: View>: View {
    @AppStorage private var expanded: Bool
    private let content: Content

    init(_ id: String, @ViewBuilder content: () -> Content) {
        _expanded = AppStorage(wrappedValue: false, "settings.advanced.\(id)")
        self.content = content()
    }

    var body: some View {
        Button { withAnimation(.snappy(duration: 0.25)) { expanded.toggle() } } label: {
            HStack(spacing: 6) {
                Image(systemName: "chevron.right").font(.system(size: 10, weight: .semibold))
                    .rotationEffect(.degrees(expanded ? 90 : 0))
                Text(expanded ? "收起高级选项" : "高级选项").font(.system(size: 12, weight: .medium))
                Rectangle().fill(Color.primary.opacity(0.08)).frame(height: 1)
            }
            .foregroundStyle(.secondary)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .padding(.horizontal, 4).padding(.top, 4)
        if expanded { content }
    }
}

struct Note: View {
    var text: String
    var color: Color = .secondary
    init(_ text: String, color: Color = .secondary) { self.text = text; self.color = color }
    var body: some View {
        Text(text).font(.system(size: 11.5)).foregroundStyle(color).fixedSize(horizontal: false, vertical: true)
    }
}

// MARK: - Essentials

private struct EssentialSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "使用前检查", icon: "checklist") {
            SetupChecklist()
            HStack {
                Button { state.openOnboarding() } label: { Label("打开新手引导", systemImage: "sparkles") }
                    .buttonStyle(SecondaryButtonStyle())
                Spacer()
                Button("刷新") { Task { await state.refreshSetupStatus() } }.buttonStyle(ChipButtonStyle())
            }
        }
        SettingsSection(title: "音频来源", icon: "speaker.wave.2") {
            HStack {
                MenuPicker(title: "对方的声音", selection: $state.selectedTargetID, options: state.captureTargets.map { ($0.id, $0.title) }, placeholder: "选择会议窗口或屏幕")
                    .onChange(of: state.selectedTargetID) { _, _ in Task { await state.changeCaptureTarget() } }
                Button { Task { await state.refreshTargets() } } label: { Image(systemName: "arrow.clockwise") }.buttonStyle(.borderless)
            }
            Toggle("同时采集麦克风（标记为“我”）", isOn: $state.microphoneEnabled)
                .disabled(state.isRunning || state.isChangingSession)
                .onChange(of: state.microphoneEnabled) { _, _ in state.updateSettings() }
            Note("选会议 App 的窗口只采集它的声音。两路分别识别，抢话也不会混；自动回答只由对方的提问触发。")
        }
        SettingsSection(title: "外观", icon: "paintbrush") {
            Picker("外观", selection: $state.appearance) {
                Text("跟随系统").tag("system")
                Text("浅色").tag("light")
                Text("深色").tag("dark")
            }
            .pickerStyle(.segmented)
            .onChange(of: state.appearance) { _, _ in state.updateSettings() }
            Toggle("工作台窗口置顶", isOn: $state.keepOnTop).onChange(of: state.keepOnTop) { _, _ in state.updateSettings() }
        }
    }
}

/// Current code signature and the one-click “bind to this Mac”.
struct SignatureStatus: View {
    private var updater: Updater { Updater.shared }
    @State private var confirming = false

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            if let signature = updater.signature {
                Label(signature.summary, systemImage: signature.isAdHoc ? "exclamationmark.shield" : "checkmark.shield")
                    .foregroundStyle(signature.isAdHoc ? Color.orange : Color.green)
            }
            if updater.signature?.isAdHoc != false || !LocalSigning.isBound {
                HStack {
                    Button(LocalSigning.isBound ? "重新绑定本机签名" : "绑定本机签名（以后更新不再重新授权）") { confirming = true }
                        .buttonStyle(SecondaryButtonStyle())
                        .disabled(!updater.canInstall || updater.phase == .installing)
                }
                Note("macOS 把录屏 / 麦克风权限绑定在应用签名上。绑定后应用用本机证书重新签名，之后的自动更新沿用同一签名，权限一直有效。绑定后需要再授权一次。")
            }
        }
        .confirmationDialog("绑定本机签名并重新打开？", isPresented: $confirming, titleVisibility: .visible) {
            Button("绑定并重新打开") { updater.bindSignatureAndRelaunch() }
            Button("取消", role: .cancel) { }
        } message: {
            Text("会在本机生成签名证书（保存在 Application Support/InterviewHacker/Signing），重新签名后重启应用。重启后请再允许一次录屏与麦克风，以后更新不再需要。")
        }
    }
}

private struct CompanionSection: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "手机伴侣", icon: "iphone") {
            Toggle("在局域网开启手机显示与遥控", isOn: Binding(get: { state.companionEnabled }, set: { state.setCompanion(enabled: $0) }))
            HStack(spacing: 8) {
                Text("首选端口")
                TextField("8765", value: $state.companionPort, format: .number.grouping(.never))
                    .textFieldStyle(.roundedBorder).frame(width: 80)
                Button("重新生成链接") { state.setCompanion(enabled: true) }.buttonStyle(ChipButtonStyle())
                Text("端口被占用时会自动换用下一个可用端口").font(.caption).foregroundStyle(.secondary)
            }
            Text(state.companionStatus).font(.caption).foregroundStyle(state.companionStatus.hasPrefix("启动失败") ? .orange : .secondary)
            if let address = state.companionAddress {
                HStack(alignment: .top, spacing: 14) {
                    QRCodeView(text: address).frame(width: 132, height: 132)
                    VStack(alignment: .leading, spacing: 6) {
                        Text("用手机相机扫码打开（无需安装 App）。").font(.callout)
                        Text(address).font(.system(size: 11, design: .monospaced)).textSelection(.enabled).foregroundStyle(.secondary)
                        Button("复制链接") {
                            NSPasteboard.general.clearContents(); NSPasteboard.general.setString(address, forType: .string)
                        }.buttonStyle(ChipButtonStyle())
                    }
                }
            }
            Note("手机实时显示当前问题和流式回答，并可一键“回答 / 改短 / 举例 / 英文版 / 上一条”。屏幕共享完全看不到手机。只同步当前回答，不同步转录、资料或截图；链接含一次性随机配对码，关闭后失效。连接为局域网明文 HTTP，请只在可信网络使用。")
        }
    }
}

// MARK: - Recognition

private struct RecognitionSettings: View {
    @Environment(AppState.self) private var state
    @State private var confirmDelete = false

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "语音识别", icon: "waveform") {
            MenuPicker(title: "引擎", selection: $state.transcriptionBackend, options: [
                ("zipformer", "本地流式 · 离线（推荐）"),
                ("apple", "Apple Speech · 本地 / macOS 26"),
                ("cloud", "云端流式 · 需 API Key"),
            ])
            .disabled(state.isRunning || state.isPreparingModel)
            .onChange(of: state.transcriptionBackend) { _, _ in state.updateSettings(); Task { await state.prewarmRecognizer() } }
            switch state.transcriptionBackend {
            case "zipformer": ModelDownloadRow()
            case "apple":
                LanguagePicker()
                Note("Apple 路径需明确选择中文或 English，首次使用可能下载语言资源。")
            default:
                LanguagePicker()
                CloudSettings()
            }
            TextField("专业词 / 人名，逗号分隔（提高识别准确度）", text: $state.recognitionVocabulary)
                .textFieldStyle(.roundedBorder)
                .onChange(of: state.recognitionVocabulary) { _, _ in state.updateSettings() }
        }
        AdvancedOptions("recognition") {
            if state.transcriptionBackend == "zipformer" {
                SettingsSection(title: "本地模型", icon: "cpu") {
                    MenuPicker(title: "流式模型", selection: $state.localStreamingModel, options: LocalStreamingModel.allCases.map { ($0, $0.title) })
                        .disabled(state.modelControlsDisabled)
                        .onChange(of: state.localStreamingModel) { _, _ in state.updateSettings(); Task { await state.refreshModelStatus(); await state.prewarmRecognizer() } }
                    Note(state.localStreamingModel.detail)
                    Toggle("整句精修：每句说完后用离线模型重新识别整句（推荐）", isOn: $state.secondPassEnabled)
                        .disabled(state.modelControlsDisabled || state.localStreamingModel == .legacy)
                        .onChange(of: state.secondPassEnabled) { _, _ in state.updateSettings(); Task { await state.prewarmRecognizer() } }
                    if let rtf = state.lastRTF, let seconds = state.lastASRSeconds {
                        Note(String(format: "最近处理 %.2f s · RTF %.2f", seconds, rtf) + (state.lastRefineSeconds.map { String(format: " · 精修 %.2f s", $0) } ?? ""))
                    }
                    HStack {
                        Link("模型来源：sherpa-onnx X-ASR（Apache-2.0）", destination: URL(string: "https://github.com/Gilgamesh-J/X-ASR")!)
                            .font(.system(size: 11)).foregroundStyle(Color.accentColor)
                        Spacer()
                        Button("删除全部模型") { confirmDelete = true }.buttonStyle(.borderless).foregroundStyle(.red).disabled(state.modelControlsDisabled)
                    }
                }
                .confirmationDialog("删除全部本地识别模型？", isPresented: $confirmDelete, titleVisibility: .visible) {
                    Button("删除", role: .destructive) { state.deleteModels() }
                    Button("取消", role: .cancel) { }
                }
            }
            SettingsSection(title: "启动与回声", icon: "speaker.wave.2.bubble.left") {
                Toggle("后台预加载识别模型（开始几乎瞬间启动）", isOn: $state.keepRecognizerWarm)
                    .onChange(of: state.keepRecognizerWarm) { _, _ in state.saveRecognizerSettings() }
                if let seconds = state.lastStartupSeconds { Note(String(format: "上次开始用时 %.1f 秒", seconds)) }
                Toggle("忽略麦克风里的扬声器回声（推荐）", isOn: $state.echoSuppression)
                    .onChange(of: state.echoSuppression) { _, _ in state.updateSettings() }
                Toggle("麦克风回声消除（Apple 语音处理，实验）", isOn: $state.microphoneVoiceProcessing)
                    .disabled(state.isRunning)
                    .onChange(of: state.microphoneVoiceProcessing) { _, _ in state.updateSettings() }
                Note("不戴耳机时对方的声音会从扬声器传进麦克风；应用比对两路音频的节奏和文字，回声不记为“我”。戴耳机始终最干净。")
            }
        }
    }
}

/// Model status with download / cancel, shared by settings, the setup checklist and onboarding.
struct ModelDownloadRow: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                if state.isModelInstalled && state.missingPackages.isEmpty {
                    Label("\(state.localStreamingModel.shortTitle)已就绪 · 可离线使用", systemImage: "checkmark.circle.fill").foregroundStyle(.green)
                } else {
                    let size = state.missingPackages.reduce(0) { $0 + $1.approximateMegabytes }
                    Label(size > 0 ? "需要下载识别模型（约 \(size) MB，只需一次）" : "需要下载识别模型", systemImage: "arrow.down.circle").foregroundStyle(.orange)
                }
                Spacer()
                if state.isDownloadingModel {
                    Button("取消") { state.cancelModelDownload() }.buttonStyle(SecondaryButtonStyle())
                } else if !state.missingPackages.isEmpty || !state.isModelInstalled {
                    Button("下载") { state.downloadModel() }.buttonStyle(PrimaryButtonStyle()).disabled(state.modelControlsDisabled)
                }
            }
            if state.isDownloadingModel { ProgressView(value: state.modelDownloadProgress).controlSize(.small) }
            if !state.modelDownloadStatus.isEmpty { Note(state.modelDownloadStatus) }
        }
    }
}

private struct LanguagePicker: View {
    @Environment(AppState.self) private var state
    var body: some View {
        @Bindable var state = state
        MenuPicker(title: "识别语言", selection: $state.recognitionLanguage, options: [("auto", "自动 / 中英混说"), ("zh", "中文"), ("en", "English")])
            .disabled(state.isRunning || state.isPreparingModel)
            .onChange(of: state.recognitionLanguage) { _, _ in state.updateSettings(); Task { await state.prewarmRecognizer() } }
    }
}

struct CloudSettings: View {
    @Environment(AppState.self) private var state
    var body: some View {
        @Bindable var state = state
        MenuPicker(title: "服务", selection: $state.cloudProvider, options: CloudSTTProvider.allCases.map { ($0, $0.title) })
            .disabled(state.isRunning)
            .onChange(of: state.cloudProvider) { _, _ in state.loadCloudKey(); state.saveAnswerSettings() }
        TextField("模型（留空使用 \(state.cloudProvider.defaultModel)）", text: $state.cloudModel)
            .textFieldStyle(.roundedBorder)
            .onChange(of: state.cloudModel) { _, _ in state.saveAnswerSettings() }
        SecureField("\(state.cloudProvider.rawValue.capitalized) API Key（保存在系统钥匙串）", text: $state.cloudKey)
            .textFieldStyle(.roundedBorder)
            .onChange(of: state.cloudKey) { _, _ in state.saveCloudKey() }
        Toggle("我了解：会议音频会实时上传到所选服务商并按其条款处理", isOn: $state.cloudConsent)
            .onChange(of: state.cloudConsent) { _, _ in state.saveAnswerSettings() }
        Note("云端识别通常几百毫秒出字；Soniox 在中英混说上最稳，Deepgram 需指定单一语言。请确认参与者同意录音与转写。")
    }
}

// MARK: - Answers

private struct AnswerSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "回答引擎", icon: "sparkles") {
            AnswerEngineForm()
            EngineTestRow()
        }
        SettingsSection(title: "自动回答", icon: "text.bubble") {
            Toggle("对方提问时自动生成回答", isOn: $state.automaticAnswers).onChange(of: state.automaticAnswers) { _, _ in state.updateSettings() }
            Note("以问号或“吗 / 呢”结尾的问题约 0.6 秒内开始回答；对方补充后半句时自动改答完整问题。拿不准的句子显示为可点击的“可能的问题”。")
        }
        WebSearchSettings()
        AdvancedOptions("answers") {
            JudgeSettings()
            SettingsSection(title: "问题识别", icon: "text.magnifyingglass") {
                Toggle("不确定的句子交给 API 模型二次判断", isOn: $state.verifyUncertainWithModel)
                    .disabled(state.answerBackend != .openAICompatible)
                    .onChange(of: state.verifyUncertainWithModel) { _, _ in state.saveAnswerSettings() }
                Note("本地规则逐句判断：区分陈述、未说完、提问、请求（“说一下你对…的理解”“walk me through…”）和追问；去掉口头禅，忽略“能听到吗”这类设备确认。课程场景更严格，避免把讲师的自问自答当成提问。")
            }
            if state.answerBackend != .openAICompatible {
                SettingsSection(title: "Codex 运行方式", icon: "terminal") {
                    Picker("运行方式", selection: $state.answerBackend) {
                        Text("常驻服务 · 流式（推荐）").tag(AnswerBackend.codexAppServer)
                        Text("codex exec · 兼容").tag(AnswerBackend.codexExec)
                    }
                    .pickerStyle(.segmented)
                    .onChange(of: state.answerBackend) { _, _ in state.saveAnswerSettings() }
                    Note("常驻 `codex app-server` 复用进程与登录，逐字流式显示；不可用时自动回退到 codex exec。exec 每次启动一个进程，等完整结果后显示。")
                }
            }
        }
    }
}

/// Engine choice and its fields; shared by settings and onboarding.
struct AnswerEngineForm: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        Picker("引擎", selection: Binding(get: { state.answerBackend == .openAICompatible ? AnswerBackend.openAICompatible : .codexAppServer },
                                          set: { state.selectAnswerBackend($0) })) {
            Text("Codex（ChatGPT 账号）").tag(AnswerBackend.codexAppServer)
            Text("API Key（任意 OpenAI 兼容服务）").tag(AnswerBackend.openAICompatible)
        }
        .pickerStyle(.segmented)
        .disabled(state.isAnswering)
        if state.answerBackend == .openAICompatible {
            APIPresetForm()
        } else {
            HStack {
                Image(systemName: state.codexReady ? "checkmark.circle.fill" : "exclamationmark.circle")
                    .foregroundStyle(state.codexReady ? .green : .orange)
                VStack(alignment: .leading, spacing: 2) {
                    Text(state.codexLoginStatus).font(.system(size: 13, weight: .medium)).lineLimit(2)
                    Text("Codex CLI · \(state.codexVersion)").font(.system(size: 11)).foregroundStyle(.secondary)
                }
                Spacer()
                Button { Task { await state.refreshCodexStatus() } } label: { Image(systemName: "arrow.clockwise").frame(width: 28, height: 28) }
                    .buttonStyle(SecondaryButtonStyle()).help("重新检测 Codex 登录状态和可用模型")
            }
            if !state.codexInstalled {
                Note("未检测到 Codex：安装 ChatGPT / Codex 桌面客户端，或在终端运行 `npm i -g @openai/codex`，然后 `codex login` 用 ChatGPT 账号登录，再点右侧刷新。", color: .orange)
            }
            HStack(spacing: 10) {
                MenuPicker(title: "模型", selection: $state.codexModelID, options: codexModelOptions, placeholder: "请选择模型")
                    .disabled(state.isAnswering)
                    .onChange(of: state.codexModelID) { _, _ in state.normalizeCodexSelection() }
                MenuPicker(title: "推理强度", selection: $state.codexReasoning, options: state.codexEfforts.map { ($0, $0) })
                    .disabled(state.isAnswering).onChange(of: state.codexReasoning) { _, _ in state.updateSettings() }
            }
            if state.codexModels.isEmpty {
                TextField("模型 ID（与 Codex 中一致）", text: $state.codexModelID)
                    .textFieldStyle(.roundedBorder).disabled(state.isAnswering)
                    .onChange(of: state.codexModelID) { _, _ in state.updateSettings() }
            }
            Toggle(isOn: $state.codexFastMode) {
                HStack(spacing: 6) {
                    Image(systemName: "bolt.fill").foregroundStyle(state.codexFastMode ? Brand.accent : .secondary)
                    Text("Fast 模式")
                    Text(state.codexFastLabel).foregroundStyle(.secondary)
                }
            }
            .disabled(state.isAnswering || !state.codexFastAvailable)
            .onChange(of: state.codexFastMode) { _, _ in state.updateSettings() }
            Note(state.codexFastAvailable
                 ? (state.currentCodexModel?.defaultsToFast == true
                    ? "这个模型默认使用 Fast；关闭后改用标准速度、节省用量。与 Codex 客户端里的“Fast”相同。"
                    : "与 Codex 客户端里的“Fast”相同：回答更快，消耗 ChatGPT 套餐用量更多。实时问答推荐推理强度 low，需要更快时打开 Fast。")
                 : "当前模型或 Codex 版本不支持 Fast 模式（需要 ChatGPT 账号登录和较新的 Codex）。")
            if !state.appServerNote.isEmpty { Note(state.appServerNote, color: .orange) }
        }
    }

    private var codexModelOptions: [(String, String)] {
        var options: [(String, String)] = state.codexModels.map { ($0.id, $0.name) }
        if !state.codexModelID.isEmpty, !state.codexModels.contains(where: { $0.id == state.codexModelID }) {
            options.insert((state.codexModelID, state.codexModelID), at: 0)
        }
        return options
    }
}

struct WebSearchSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "联网搜索", icon: "globe") {
            Picker("联网", selection: $state.webSearchMode) {
                ForEach(WebSearchMode.allCases) { Text($0.title).tag($0) }
            }
            .pickerStyle(.segmented)
            .onChange(of: state.webSearchMode) { _, _ in state.saveWebSearchSettings() }
            Note("先立刻给出快速回答；需要最新信息（价格、新闻、版本、现任人员、“今天 / 最近”…）时在后台联网，通常 5–20 秒后自动换成联网版，可随时切回原回答。“自动判断”用本地规则，拿不准的交给判断模型（如已设置）。")
            if state.webSearchMode != .off {
                MenuPicker(title: "搜索方式", selection: $state.webSearchProvider, options: WebSearchProvider.allCases.map { ($0, $0.title) })
                    .onChange(of: state.webSearchProvider) { _, _ in state.saveWebSearchSettings() }
                switch state.webSearchProvider {
                case .codex:
                    if !state.codexReady {
                        Note("需要安装 Codex 并用 ChatGPT 账号登录（见上方回答引擎）。", color: .orange)
                    } else {
                        Note("用 Codex 自带的实时网页搜索，再按当前场景写成能直接说的回答；回答引擎是 API 时也可以用它来搜索。")
                    }
                case .tavily:
                    HStack {
                        SecureField("Tavily API Key（保存在系统钥匙串）", text: $state.tavilyKey)
                            .textFieldStyle(.roundedBorder)
                            .onChange(of: state.tavilyKey) { _, _ in state.saveTavilyKey() }
                        Link("获取 Key", destination: URL(string: "https://app.tavily.com")!).font(.system(size: 12))
                    }
                    Note("Tavily 返回网页摘要，交给当前回答引擎整理，回答里会用 [1] [2] 标注来源。问题文本会发送给 Tavily。")
                }
            }
        }
    }
}

/// Optional external judge (“JEV”) for uncertain decisions.
private struct JudgeSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var judge = state.judge
        SettingsSection(title: "判断模型（JEV）", icon: "checkmark.seal") {
            Toggle("启用判断模型", isOn: $judge.enabled).onChange(of: judge.enabled) { _, _ in judge.save() }
            Note("本地规则拿不准时，把“这句是不是在问我”“这个问题要不要联网”交给一个快速模型判断；超过 1.5 秒或出错就按本地规则处理，不会拖慢回答。任何 OpenAI 兼容接口都可以；接口约定见仓库 docs/JUDGE-API.md，方便以后接入自己的 JEV 模型。")
            if judge.enabled {
                TextField("接口地址，例如 https://api.example.com/v1", text: $judge.baseURL).textFieldStyle(.roundedBorder)
                    .onChange(of: judge.baseURL) { _, _ in judge.save() }
                HStack {
                    TextField("模型 ID", text: $judge.model).textFieldStyle(.roundedBorder)
                        .onChange(of: judge.model) { _, _ in judge.save() }
                    SecureField("Key（可留空）", text: $judge.key).textFieldStyle(.roundedBorder)
                        .onChange(of: judge.key) { _, _ in judge.save() }
                }
                HStack(spacing: 16) {
                    Toggle("判断是不是在问我", isOn: $judge.forQuestions).onChange(of: judge.forQuestions) { _, _ in judge.save() }
                    Toggle("判断要不要联网", isOn: $judge.forWebSearch).onChange(of: judge.forWebSearch) { _, _ in judge.save() }
                }
                HStack {
                    Button("测试") { Task { await judge.test() } }.buttonStyle(SecondaryButtonStyle()).disabled(!judge.configured)
                    if !judge.status.isEmpty { Note(judge.status) }
                }
            }
        }
    }
}

/// Saved providers: pick one, or add another from a template; each keeps its own
/// address, key, model and thinking strength.
struct APIPresetForm: View {
    @Environment(AppState.self) private var state
    @State private var renaming = false
    @State private var newName = ""
    @State private var confirmDelete = false

    var body: some View {
        @Bindable var state = state
        HStack(spacing: 8) {
            Text("预设").foregroundStyle(.secondary)
            Menu {
                ForEach(state.apiPresets) { preset in
                    Button { state.selectAPIPreset(preset.id) } label: {
                        if preset.id == state.activeAPIPresetID { Label(preset.name, systemImage: "checkmark") } else { Text(preset.name) }
                    }
                }
                Divider()
                Menu("新建预设") {
                    ForEach(APIPreset.templates, id: \.name) { template in
                        Button(template.name) { state.addAPIPreset(name: template.name, baseURL: template.url) }
                    }
                    Divider()
                    Button("自定义…") { state.addAPIPreset(name: "自定义", baseURL: "https://") }
                }
            } label: {
                Label(state.activeAPIPreset?.name ?? "选择预设", systemImage: "server.rack")
            }
            .fixedSize()
            Button("重命名") { newName = state.activeAPIPreset?.name ?? ""; renaming = true }.buttonStyle(ChipButtonStyle())
            Button("删除") { confirmDelete = true }.buttonStyle(ChipButtonStyle()).disabled(state.apiPresets.count <= 1)
            Spacer()
        }
        .disabled(state.isAnswering)
        .alert("重命名预设", isPresented: $renaming) {
            TextField("名称", text: $newName)
            Button("保存") { if let id = state.activeAPIPresetID { state.renameAPIPreset(id, to: newName) } }
            Button("取消", role: .cancel) { }
        }
        .confirmationDialog("删除预设“\(state.activeAPIPreset?.name ?? "")”？", isPresented: $confirmDelete, titleVisibility: .visible) {
            Button("删除", role: .destructive) { if let id = state.activeAPIPresetID { state.deleteAPIPreset(id) } }
            Button("取消", role: .cancel) { }
        } message: { Text("会同时删除保存在钥匙串里的这个 API Key。") }

        TextField("API 地址，例如 https://api.openai.com/v1", text: $state.apiBaseURL)
            .textFieldStyle(.roundedBorder)
            .onChange(of: state.apiBaseURL) { _, _ in state.saveAnswerSettings() }
        SecureField("API Key（保存在系统钥匙串）", text: $state.apiKey)
            .textFieldStyle(.roundedBorder)
            .onSubmit { state.saveAPIKey() }
            .onChange(of: state.apiKey) { _, _ in state.saveAPIKey() }
        HStack(spacing: 8) {
            TextField("模型 ID（与服务商一致）", text: $state.apiModel)
                .textFieldStyle(.roundedBorder)
                .onChange(of: state.apiModel) { _, _ in state.saveAnswerSettings() }
            if !state.apiModelChoices.isEmpty {
                Menu("选择") {
                    ForEach(state.apiModelChoices, id: \.self) { id in
                        Button(id) { state.apiModel = id; state.saveAnswerSettings() }
                    }
                }
                .fixedSize()
            }
            Button("获取模型列表") { Task { await state.fetchAPIModels() } }.buttonStyle(ChipButtonStyle())
        }
        if !state.apiModelsStatus.isEmpty { Note(state.apiModelsStatus) }
        MenuPicker(title: "思考强度", selection: $state.apiReasoningEffort, options: APIPreset.efforts)
            .onChange(of: state.apiReasoningEffort) { _, _ in state.saveAnswerSettings() }
        Note("思考强度会作为 reasoning_effort 发送，只对支持推理的模型有效（如 OpenAI o / GPT-5 系列）；其他模型选“默认”。实时问答建议 minimal 或 low。转录片段、检索到的资料和截图会发送到这个服务。")
    }
}

/// Sends a one-line test question through the configured engine.
struct EngineTestRow: View {
    @Environment(AppState.self) private var state

    var body: some View {
        HStack(spacing: 10) {
            Button {
                Task { await state.testAnswerEngine() }
            } label: {
                HStack(spacing: 6) {
                    if state.engineTest == .running { ProgressView().controlSize(.small) }
                    Label("测试回答", systemImage: "play.circle")
                }
            }
            .buttonStyle(SecondaryButtonStyle())
            .disabled(state.engineTest == .running || state.isAnswering)
            switch state.engineTest {
            case .idle: Text("发一句测试问题，确认引擎能正常回答").font(.system(size: 11)).foregroundStyle(.secondary)
            case .running: Text("正在请求…").font(.system(size: 11)).foregroundStyle(.secondary)
            case let .passed(seconds, reply):
                Label(String(format: "可以使用 · %.1f 秒 · “%@”", seconds, String(reply.prefix(24))), systemImage: "checkmark.circle.fill")
                    .font(.system(size: 11)).foregroundStyle(.green).lineLimit(1)
            case let .failed(message):
                Label(message, systemImage: "xmark.octagon.fill").font(.system(size: 11)).foregroundStyle(.orange).lineLimit(2)
            }
            Spacer(minLength: 0)
        }
    }
}

// MARK: - Skills

private struct SkillSettings: View {
    @Environment(AppState.self) private var state
    @State private var editing: Skill?
    @State private var deleting: Skill?
    private var library: SkillLibrary { SkillLibrary.shared }

    var body: some View {
        SettingsSection(title: "技能库", icon: "wand.and.stars") {
            Note("技能是一份 Markdown 说明（例如 SKILL.md）：你希望的说话方式、答题套路、领域知识。打开“所有场景”的技能每次回答都会用；其他技能在 场景 → 资料与要求 → 技能 里按场景勾选。")
            HStack {
                Button { library.importFiles(global: false) } label: { Label("导入 .md 文件…", systemImage: "square.and.arrow.down") }
                    .buttonStyle(PrimaryButtonStyle())
                Button { editing = library.create(global: false) } label: { Label("新建技能", systemImage: "plus") }
                    .buttonStyle(SecondaryButtonStyle())
                Spacer()
            }
            if library.skills.isEmpty {
                Note("还没有技能。可以导入现成的 SKILL.md，或点“新建技能”从模板开始。")
            } else {
                VStack(spacing: 6) {
                    ForEach(library.skills) { skill in row(skill) }
                }
            }
        }
        .sheet(item: $editing) { skill in SkillEditor(skill: skill) { library.update($0) } }
        .confirmationDialog("删除技能“\(deleting?.name ?? "")”？", isPresented: Binding(get: { deleting != nil }, set: { if !$0 { deleting = nil } }), titleVisibility: .visible) {
            Button("删除", role: .destructive) { if let deleting { library.delete(deleting.id) }; deleting = nil }
            Button("取消", role: .cancel) { deleting = nil }
        }
    }

    private func row(_ skill: Skill) -> some View {
        let attached = state.workspaces.workspaces.filter { $0.skillIDs.contains(skill.id) }.count
        return HStack(spacing: 10) {
            Image(systemName: skill.global ? "globe" : "doc.text").foregroundStyle(skill.enabled ? Brand.accent : .secondary).frame(width: 18)
            VStack(alignment: .leading, spacing: 2) {
                Text(skill.name).font(.system(size: 13, weight: .semibold)).foregroundStyle(skill.enabled ? .primary : .secondary)
                Text(skill.summary.isEmpty ? "\(skill.content.count) 字" : skill.summary).font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(1)
                if !skill.global { Text(attached == 0 ? "还没有场景使用" : "\(attached) 个场景在用").font(.system(size: 10.5)).foregroundStyle(.tertiary) }
            }
            Spacer()
            Toggle("所有场景", isOn: Binding(get: { skill.global }, set: { var next = skill; next.global = $0; library.update(next) }))
                .toggleStyle(.checkbox)
            Toggle("启用", isOn: Binding(get: { skill.enabled }, set: { var next = skill; next.enabled = $0; library.update(next) }))
                .toggleStyle(.switch).controlSize(.small).labelsHidden()
            Button("编辑") { editing = skill }.buttonStyle(ChipButtonStyle())
            Button { deleting = skill } label: { Image(systemName: "trash") }.buttonStyle(.borderless).foregroundStyle(.red)
        }
        .padding(.horizontal, 10).padding(.vertical, 8)
        .background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }
}

struct SkillEditor: View {
    @State var skill: Skill
    let save: (Skill) -> Void
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("编辑技能").font(.headline)
            TextField("名称", text: $skill.name).textFieldStyle(.roundedBorder)
            TextField("一句话说明（可选）", text: $skill.summary).textFieldStyle(.roundedBorder)
            TextEditor(text: $skill.content)
                .font(.system(size: 13, design: .monospaced))
                .scrollContentBackground(.hidden)
                .padding(6)
                .background(Color.primary.opacity(0.05), in: RoundedRectangle(cornerRadius: 8))
                .frame(minHeight: 320)
            Toggle("所有场景都使用这个技能", isOn: $skill.global)
            HStack {
                Text("\(skill.content.count) 字 · 单个技能最多使用约 6000 字").font(.system(size: 11)).foregroundStyle(.secondary)
                Spacer()
                Button("取消") { dismiss() }
                Button("保存") { save(skill); dismiss() }.keyboardShortcut(.defaultAction)
            }
        }
        .padding(20)
        .frame(width: 620)
    }
}

// MARK: - Overlay

private struct OverlaySettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "提词器与屏幕共享", icon: "rectangle.topthird.inset.filled") {
            HStack {
                Text("位置")
                ForEach(TeleprompterController.Placement.allCases) { placement in
                    Button(placement.title) { TeleprompterController.shared.move(to: placement) }.buttonStyle(ChipButtonStyle())
                }
            }
            HStack {
                Text("不透明度")
                Slider(value: $state.teleprompterOpacity, in: 0.35...1).onChange(of: state.teleprompterOpacity) { _, _ in state.saveAnswerSettings() }
                Text("\(Int(state.teleprompterOpacity * 100))%").font(.caption.monospacedDigit()).frame(width: 36)
            }
            HStack {
                Text("字号")
                Slider(value: $state.teleprompterFontSize, in: 14...44, step: 1).onChange(of: state.teleprompterFontSize) { _, _ in state.saveAnswerSettings() }
                Text("\(Int(state.teleprompterFontSize))").font(.caption.monospacedDigit()).frame(width: 36)
            }
            Toggle("鼠标穿透（点击穿过提词器）", isOn: Binding(get: { state.clickThrough }, set: { _ in state.toggleClickThrough() }))
            Toggle("尽力从屏幕共享中排除本应用窗口", isOn: $state.capturePrivacy)
                .onChange(of: state.capturePrivacy) { _, _ in state.saveAnswerSettings(); state.updateSettings() }
            HStack {
                Button("共享可见性测试") { state.runShareVisibilityTest() }.buttonStyle(SecondaryButtonStyle())
                Button("仅显示提词器") { TeleprompterController.shared.showOnlyTeleprompter() }.buttonStyle(SecondaryButtonStyle())
            }
            Note("macOS 无法保证窗口不被录屏：只共享会议窗口或标签页最稳妥，或者用手机看回答（更多 → 手机伴侣）。\(state.shortcutLabel(.panicHide)) 一键隐藏全部窗口。")
        }
    }
}

// MARK: - Recording

private struct RecordingSettings: View {
    @Environment(AppState.self) private var state
    @State private var includeSecrets = false
    @State private var applySettings = true
    @State private var backups: [DataBackup.BackupFile] = []
    @State private var restoring: DataBackup.BackupFile?

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "会话记录与复盘", icon: "tray.full") {
            Toggle("记录每次会话（转录、识别到的问题、提词与当时的场景资料）", isOn: $state.recordSessions)
                .disabled(state.isRunning)
                .onChange(of: state.recordSessions) { _, _ in state.updateSettings() }
            Toggle("会话结束后自动生成摘要、问题清单与复盘", isOn: $state.autoSummarize)
                .disabled(!state.recordSessions)
                .onChange(of: state.autoSummarize) { _, _ in state.updateSettings() }
            Toggle("把复盘得到的经验和改进回答自动加入场景的经验库与题库（自我迭代）", isOn: $state.autoApplyLessons)
                .disabled(!state.autoSummarize)
                .onChange(of: state.autoApplyLessons) { _, _ in state.updateSettings() }
            Note("记录边说边写入本机（崩溃或强制退出也不会丢），不保存原始音频。摘要与复盘用当前的回答引擎生成，会把这次会话的转录发给该引擎。")
        }
        SettingsSection(title: "导出与导入", icon: "externaldrive") {
            Note("一个 .ihbackup 文件包含全部场景（资料、经验库、题库、阶段复盘）、全部会话记录和设置。换电脑、重装或想留档时导出；导入只合并，不删除现有数据，同一场景以较新的修改为准。")
            HStack {
                Button("导出全部数据…") { state.exportAllData(includeSecrets: includeSecrets) }.buttonStyle(PrimaryButtonStyle()).disabled(state.isDataBusy)
                Toggle("包含 API Key 与访问令牌", isOn: $includeSecrets).toggleStyle(.checkbox)
            }
            HStack {
                Button("导入…") { state.importData(applySettings: applySettings) }.buttonStyle(SecondaryButtonStyle()).disabled(state.isDataBusy || state.isRunning)
                Toggle("同时导入设置（回答引擎、识别、快捷键等）", isOn: $applySettings).toggleStyle(.checkbox)
            }
            if includeSecrets { Note("导出文件里的密钥是明文，请妥善保管，不要发给别人。", color: .orange) }
            if state.isDataBusy { ProgressView().controlSize(.small) }
            if !state.dataStatus.isEmpty { Note(state.dataStatus, color: .green) }
        }
        SettingsSection(title: "自动备份", icon: "clock.arrow.circlepath") {
            Toggle("每天自动备份一次（另外在安装更新和导入前也会备份）", isOn: $state.autoBackup)
                .onChange(of: state.autoBackup) { _, _ in state.updateSettings() }
            HStack {
                Button("立即备份") { state.backupNow(); refreshSoon() }.buttonStyle(SecondaryButtonStyle()).disabled(state.isDataBusy)
                Button("打开数据文件夹") {
                    try? FileManager.default.createDirectory(at: AppPaths.root, withIntermediateDirectories: true)
                    NSWorkspace.shared.open(AppPaths.root)
                }.buttonStyle(SecondaryButtonStyle())
            }
            if backups.isEmpty {
                Note("还没有自动备份。")
            } else {
                ForEach(backups.prefix(10)) { backup in
                    HStack {
                        Image(systemName: "archivebox").foregroundStyle(.secondary)
                        Text(backup.date.formatted(date: .abbreviated, time: .shortened))
                        Text(reasonTitle(backup.reason)).foregroundStyle(.secondary)
                        Text(ByteCountFormatter.string(fromByteCount: Int64(backup.size), countStyle: .file)).foregroundStyle(.tertiary)
                        Spacer()
                        Button("恢复…") { restoring = backup }.buttonStyle(ChipButtonStyle()).disabled(state.isRunning)
                        Button { NSWorkspace.shared.activateFileViewerSelecting([backup.url]) } label: { Image(systemName: "folder") }.buttonStyle(.borderless)
                    }
                }
            }
            Note("所有数据都在 \(AppPaths.root.path)。更新应用、更换模型或回答接口都不会改动这里的文件；自动备份保留最近 10 份，不含密钥。")
        }
        .onAppear { backups = DataBackup.listBackups() }
        .onChange(of: state.section) { _, section in if section == .settings { backups = DataBackup.listBackups() } }
        .confirmationDialog("从这份备份恢复？", isPresented: Binding(get: { restoring != nil }, set: { if !$0 { restoring = nil } }), titleVisibility: .visible) {
            Button("合并恢复") { if let restoring { state.importArchive(restoring.url, applySettings: false) }; restoring = nil; refreshSoon() }
            Button("取消", role: .cancel) { restoring = nil }
        } message: {
            Text("把备份里的场景和会话记录合并回来；现有数据不会被删除，较新的修改会保留。")
        }
    }

    private func refreshSoon() {
        Task { try? await Task.sleep(for: .seconds(2)); backups = DataBackup.listBackups() }
    }

    private func reasonTitle(_ reason: String) -> String {
        switch reason {
        case "daily": "每日"
        case "update": "更新前"
        case "import": "导入前"
        case "manual": "手动"
        default: reason
        }
    }
}

// MARK: - More

private struct MoreSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        UpdateSettings()
        SettingsSection(title: "权限与签名", icon: "lock.shield") {
            Text(state.capturePermissionStatus).foregroundStyle(state.capturePermissionGranted ? .green : .secondary)
            HStack {
                Button("录屏与系统音频…") { state.openCapturePermissionSettings() }.buttonStyle(SecondaryButtonStyle())
                Button("麦克风…") { state.openMicrophoneSettings() }.buttonStyle(SecondaryButtonStyle())
                Button("刷新") { Task { await state.refreshSetupStatus() } }.buttonStyle(ChipButtonStyle())
            }
            SignatureStatus()
        }
        ShortcutSection()
        CompanionSection()
        SettingsSection(title: "性能", icon: "gauge.with.dots.needle.33percent") {
            Toggle("减少视觉特效（更流畅，适合轻薄本 / 省电）", isOn: $state.reducedEffects)
                .onChange(of: state.reducedEffects) { _, value in UserDefaults.standard.set(value, forKey: "reducedEffects") }
            Note("关闭液态玻璃的实时折射，改用平面半透明效果。系统“降低透明度”开启时会自动使用。")
        }
    }
}

// MARK: - Updates

private struct UpdateSettings: View {
    @Environment(AppState.self) private var state
    private var updater: Updater { Updater.shared }

    var body: some View {
        @Bindable var updater = Updater.shared
        SettingsSection(title: "自动更新", icon: "arrow.down.circle") {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("当前版本 \(updater.currentVersion)（#\(updater.currentBuild)）").font(.system(size: 13, weight: .medium))
                    Text(statusText).foregroundStyle(statusColor).lineLimit(3)
                }
                Spacer()
                switch updater.phase {
                case .ready:
                    Button("重启并安装") { updater.installAndRelaunch() }.buttonStyle(PrimaryButtonStyle()).disabled(state.isRunning)
                case .available:
                    Button("下载") { updater.downloadUpdate() }.buttonStyle(PrimaryButtonStyle())
                case .checking, .downloading, .installing:
                    ProgressView().controlSize(.small)
                default:
                    Button("检查更新") { Task { await updater.check(userInitiated: true) } }.buttonStyle(SecondaryButtonStyle())
                }
            }
            if case let .downloading(fraction) = updater.phase { ProgressView(value: fraction).controlSize(.small) }
            if let note = updater.stagedNote { Note(note, color: .orange) }
            if state.isRunning, updater.phase == .ready { Note("正在辅助中：停止后再安装，避免打断会议。", color: .orange) }
            Picker("更新渠道", selection: $updater.channel) {
                ForEach(UpdateChannel.allCases) { Text($0.title).tag($0) }
            }
            .pickerStyle(.segmented)
            .onChange(of: updater.channel) { _, _ in updater.saveSettings(); Task { await updater.check(userInitiated: true) } }
            Toggle("自动检查更新（启动后与每 6 小时；有新版本时在后台下载）", isOn: $updater.automaticChecks)
                .onChange(of: updater.automaticChecks) { _, _ in updater.saveSettings() }
            Note("从 GitHub Releases 下载，校验 SHA-256 和签名后再安装；已绑定本机签名时，更新后权限保持不变。")
        }
        // Shown open when access is the problem; otherwise tucked under 高级选项.
        if updater.needsAccessHelp || !updater.hasToken && updater.phase != .upToDate {
            GitHubAccessSection()
        } else {
            AdvancedOptions("updates") { GitHubAccessSection() }
        }
    }

    private var statusText: String {
        switch updater.phase {
        case .idle: return updater.lastChecked.map { "上次检查 \($0.formatted(date: .omitted, time: .shortened))" } ?? "尚未检查"
        case .checking: return "正在检查…"
        case .upToDate: return "已是最新" + (updater.lastChecked.map { " · \($0.formatted(date: .omitted, time: .shortened)) 检查" } ?? "")
        case .available: return "发现新版本 \(updater.available?.displayVersion ?? "")"
        case let .downloading(fraction): return "正在下载 \(updater.available?.displayVersion ?? "") · \(Int(fraction * 100))%"
        case .ready: return "\(updater.available?.displayVersion ?? "新版本") 已下载并校验，可以安装"
        case .installing: return "正在安装并重新打开…"
        case let .failed(message): return message
        }
    }

    private var statusColor: Color {
        switch updater.phase {
        case .failed: .orange
        case .ready, .available: Brand.accent
        default: .secondary
        }
    }
}

private struct GitHubAccessSection: View {
    @State private var token = ""

    var body: some View {
        @Bindable var updater = Updater.shared
        SettingsSection(title: "GitHub 访问", icon: "key") {
            HStack {
                TextField("仓库", text: $updater.repository).textFieldStyle(.roundedBorder).frame(maxWidth: 260)
                    .onSubmit { updater.saveSettings() }
                Spacer()
                Label(updater.hasToken ? "已保存访问令牌" : "未保存访问令牌", systemImage: updater.hasToken ? "checkmark.seal" : "key")
                    .foregroundStyle(updater.hasToken ? .green : .secondary)
            }
            HStack {
                SecureField("粘贴访问令牌（github_pat_…）", text: $token).textFieldStyle(.roundedBorder)
                Button("保存并检查") {
                    updater.saveToken(token); token = ""
                    Task { await updater.check(userInitiated: true) }
                }
                .buttonStyle(ChipButtonStyle(prominent: true)).disabled(token.isEmpty)
                if updater.hasToken { Button("清除") { updater.saveToken("") }.buttonStyle(ChipButtonStyle()) }
            }
            if let note = updater.accessNote { Note(note, color: .orange) }
            HStack(spacing: 8) {
                Button { NSWorkspace.shared.open(Updater.createTokenURL) } label: { Label("去 GitHub 创建令牌", systemImage: "arrow.up.right.square") }
                    .buttonStyle(SecondaryButtonStyle())
                Text("或在终端运行 gh auth login，就不用填令牌").font(.system(size: 11.5)).foregroundStyle(.secondary)
            }
            VStack(alignment: .leading, spacing: 3) {
                Text("创建令牌时这样填：").font(.system(size: 11.5, weight: .semibold))
                Note("1. Resource owner 选仓库所属的组织 \(updater.repository.split(separator: "/").first.map(String.init) ?? "")（不是个人账号）")
                Note("2. Repository access 选 Only select repositories → \(updater.repository.split(separator: "/").last.map(String.init) ?? "")")
                Note("3. Permissions → Contents 选 Read-only，其余不动；生成后复制 github_pat_ 开头的整串粘贴到上面")
                Note("令牌保存在系统钥匙串。组织要求审批时，需要管理员批准后才能用。")
            }
        }
    }
}

// MARK: - Shortcuts

private struct ShortcutSection: View {
    @Environment(AppState.self) private var state
    var body: some View {
        SettingsSection(title: "全局快捷键", icon: "keyboard") {
            ShortcutSettingsView()
            ForEach(state.shortcutErrors, id: \.self) { Note($0, color: .orange) }
        }
    }
}
