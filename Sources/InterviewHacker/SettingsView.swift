import AppKit
import SwiftUI
import InterviewHackerCore

enum SettingsGroup: String, CaseIterable, Identifiable {
    case general = "常规", recognition = "识别", answers = "回答", overlay = "提词器", recording = "记录", updates = "更新", shortcuts = "快捷键"
    var id: String { rawValue }
    var symbol: String {
        switch self {
        case .general: "switch.2"
        case .recognition: "waveform"
        case .answers: "sparkles"
        case .overlay: "rectangle.topthird.inset.filled"
        case .recording: "tray.full"
        case .updates: "arrow.down.circle"
        case .shortcuts: "keyboard"
        }
    }
}

struct SettingsView: View {
    @Environment(AppState.self) private var state
    @AppStorage("settingsGroup") private var groupRaw = SettingsGroup.general.rawValue

    var body: some View {
        let group = SettingsGroup(rawValue: groupRaw) ?? .general
        VStack(spacing: 10) {
            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: 6) {
                    ForEach(SettingsGroup.allCases) { item in
                        Button { groupRaw = item.rawValue } label: { Label(item.rawValue, systemImage: item.symbol) }
                            .buttonStyle(ChipButtonStyle(prominent: item == group))
                    }
                }
                .padding(.horizontal, 16)
            }
            .frame(maxWidth: 852)
            .frame(maxWidth: .infinity)
            ScrollView {
                VStack(alignment: .leading, spacing: 12) {
                    switch group {
                    case .general: GeneralSettings()
                    case .recognition: RecognitionSettings()
                    case .answers: AnswerSettings()
                    case .overlay: OverlaySettings()
                    case .recording: RecordingSettings()
                    case .updates: UpdateSettings()
                    case .shortcuts: ShortcutSection()
                    }
                }
                .padding(.horizontal, 16).padding(.bottom, 18)
                .frame(maxWidth: 820)
                .frame(maxWidth: .infinity)
            }
        }
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
        .font(.system(size: 12))
        .card()
    }
}

private struct Note: View {
    var text: String
    var color: Color = .secondary
    init(_ text: String, color: Color = .secondary) { self.text = text; self.color = color }
    var body: some View {
        Text(text).font(.system(size: 11)).foregroundStyle(color).fixedSize(horizontal: false, vertical: true)
    }
}

// MARK: - General

private struct GeneralSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "系统权限", icon: "lock.shield") {
            Text(state.capturePermissionStatus).foregroundStyle(state.capturePermissionGranted ? .green : .secondary)
            HStack {
                Button(state.capturePermissionGranted ? "打开系统设置" : "申请录屏与系统音频") {
                    if state.capturePermissionGranted { state.openCapturePermissionSettings() }
                    else { Task { await state.requestCapturePermission() } }
                }
                .buttonStyle(SecondaryButtonStyle())
                Button("刷新状态") { Task { await state.refreshTargets() } }.buttonStyle(.plain)
                Button("麦克风权限") { state.openMicrophoneSettings() }.buttonStyle(.plain).foregroundStyle(Color.accentColor)
            }
            SignatureStatus()
        }
        SettingsSection(title: "捕获目标", icon: "rectangle.on.rectangle") {
            HStack {
                MenuPicker(title: "系统音频来源", selection: $state.selectedTargetID, options: state.captureTargets.map { ($0.id, $0.title) }, placeholder: "选择音频来源")
                    .onChange(of: state.selectedTargetID) { _, _ in Task { await state.changeCaptureTarget() } }
                Button { Task { await state.refreshTargets() } } label: { Image(systemName: "arrow.clockwise") }.buttonStyle(.borderless)
            }
            Toggle("采集麦克风（标记为“我”）", isOn: $state.microphoneEnabled)
                .disabled(state.isRunning || state.isChangingSession)
                .onChange(of: state.microphoneEnabled) { _, _ in state.updateSettings() }
            Note("系统声音标记为“对方”，麦克风标记为“我”，两路分别识别，互相抢话也不会混在一起。自动回答只由对方的提问触发；你在说话时会等你说完再出回答。")
        }
        SettingsSection(title: "外观与窗口", icon: "paintbrush") {
            Picker("外观", selection: $state.appearance) {
                Text("跟随系统").tag("system")
                Text("浅色").tag("light")
                Text("深色").tag("dark")
            }
            .pickerStyle(.segmented)
            .onChange(of: state.appearance) { _, _ in state.updateSettings() }
            Toggle("工作台窗口置顶", isOn: $state.keepOnTop).onChange(of: state.keepOnTop) { _, _ in state.updateSettings() }
            Toggle("减少视觉特效（更流畅，适合轻薄本 / 省电）", isOn: $state.reducedEffects)
                .onChange(of: state.reducedEffects) { _, value in UserDefaults.standard.set(value, forKey: "reducedEffects") }
            Note("关闭液态玻璃的实时折射，改用平面半透明效果。系统“降低透明度”开启时会自动使用。")
        }
        CompanionSection()
    }
}

/// Current code signature and the one-click “bind to this Mac”.
private struct SignatureStatus: View {
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
                Note("macOS 把录屏 / 麦克风权限绑定在应用的签名上。下载的版本是临时签名，每个版本都不同，所以每次更新都要重新授权。绑定后，应用会在本机生成一个只属于这台 Mac 的签名证书并用它重新签名；之后应用内自动更新会用同一证书签名新版本，权限一直有效。首次绑定后需要再授权一次；系统可能请求一次登录密码以信任该证书。")
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
        SettingsSection(title: "语音识别引擎", icon: "waveform") {
            MenuPicker(title: "引擎", selection: $state.transcriptionBackend, options: [
                ("zipformer", "本地流式 · 离线（推荐）"),
                ("apple", "Apple Speech · 本地 / macOS 26"),
                ("cloud", "云端流式 · 需 API Key"),
            ])
            .disabled(state.isRunning || state.isPreparingModel)
            .onChange(of: state.transcriptionBackend) { _, _ in state.updateSettings(); Task { await state.prewarmRecognizer() } }
            if state.transcriptionBackend != "zipformer" {
                MenuPicker(title: "识别语言", selection: $state.recognitionLanguage, options: [("auto", "自动 / 中英混说"), ("zh", "中文"), ("en", "English")])
                    .disabled(state.isRunning || state.isPreparingModel)
                    .onChange(of: state.recognitionLanguage) { _, _ in state.updateSettings(); Task { await state.prewarmRecognizer() } }
            }
            TextField("专业词 / 人名，逗号分隔（热词）", text: $state.recognitionVocabulary)
                .textFieldStyle(.roundedBorder)
                .onChange(of: state.recognitionVocabulary) { _, _ in state.updateSettings() }
            Toggle("后台预加载识别模型（开始辅助几乎瞬间启动）", isOn: $state.keepRecognizerWarm)
                .onChange(of: state.keepRecognizerWarm) { _, _ in state.saveRecognizerSettings() }
            if let seconds = state.lastStartupSeconds {
                Note(String(format: "上次开始辅助用时 %.1f 秒", seconds))
            }
            switch state.transcriptionBackend {
            case "apple": Note("Apple 路径需明确选择中文或 English，首次使用可能下载语言资源。")
            case "cloud": CloudSettings()
            default: EmptyView()
            }
        }
        if state.transcriptionBackend == "zipformer" {
            SettingsSection(title: "本地模型", icon: "cpu") {
                MenuPicker(title: "流式模型", selection: $state.localStreamingModel, options: LocalStreamingModel.allCases.map { ($0, $0.title) })
                    .disabled(state.modelControlsDisabled)
                    .onChange(of: state.localStreamingModel) { _, _ in state.updateSettings(); Task { await state.refreshModelStatus(); await state.prewarmRecognizer() } }
                Note(state.localStreamingModel.detail)
                Toggle("整句精修（二次识别）：每句说完后用离线模型重新识别整句", isOn: $state.secondPassEnabled)
                    .disabled(state.modelControlsDisabled || state.localStreamingModel == .legacy)
                    .onChange(of: state.secondPassEnabled) { _, _ in state.updateSettings(); Task { await state.prewarmRecognizer() } }
                Note("边说边出字用流式模型；一句话结束后，离线模型看到整句再识别一次，错字、中英混说和句尾的问号明显更准（本机约 0.1–0.3 秒）。问题识别也用精修后的文字。")
                HStack {
                    if state.isModelInstalled && state.missingPackages.isEmpty {
                        Label("模型已就绪 · 可离线使用", systemImage: "checkmark.circle.fill").foregroundStyle(.green)
                    } else {
                        let size = state.missingPackages.reduce(0) { $0 + $1.approximateMegabytes }
                        Label(size > 0 ? "需要下载约 \(size) MB" : "需要下载模型", systemImage: "arrow.down.circle").foregroundStyle(.orange)
                    }
                    Spacer()
                    if state.isDownloadingModel {
                        Button("取消下载") { state.cancelModelDownload() }.buttonStyle(SecondaryButtonStyle())
                    } else if !state.missingPackages.isEmpty || !state.isModelInstalled {
                        Button("下载") { state.downloadModel() }.buttonStyle(PrimaryButtonStyle()).disabled(state.modelControlsDisabled)
                    }
                    Button("删除全部模型") { confirmDelete = true }.buttonStyle(.borderless).foregroundStyle(.red).disabled(state.modelControlsDisabled)
                }
                if state.isDownloadingModel { ProgressView(value: state.modelDownloadProgress).controlSize(.small) }
                if !state.modelDownloadStatus.isEmpty { Note(state.modelDownloadStatus) }
                if let rtf = state.lastRTF, let seconds = state.lastASRSeconds {
                    Note(String(format: "最近处理 %.2f s · RTF %.2f", seconds, rtf) + (state.lastRefineSeconds.map { String(format: " · 精修 %.2f s", $0) } ?? ""))
                }
                Link("模型来源：sherpa-onnx X-ASR（Apache-2.0）", destination: URL(string: "https://github.com/Gilgamesh-J/X-ASR")!)
                    .font(.system(size: 11)).foregroundStyle(Color.accentColor)
            }
            .confirmationDialog("删除全部本地识别模型？", isPresented: $confirmDelete, titleVisibility: .visible) {
                Button("删除", role: .destructive) { state.deleteModels() }
                Button("取消", role: .cancel) { }
            }
        }
        SettingsSection(title: "回声与抢话", icon: "speaker.wave.2.bubble.left") {
            Toggle("忽略麦克风里的扬声器回声（推荐）", isOn: $state.echoSuppression)
                .onChange(of: state.echoSuppression) { _, _ in state.updateSettings() }
            Note("不戴耳机时，对方的声音会从扬声器传进麦克风。应用会比对两路音频的节奏和文字：属于回声的部分不记为“我”，也不会因此暂停自动回答。你和对方同时说话时两路仍分别识别。")
            Toggle("麦克风回声消除（Apple 语音处理，实验）", isOn: $state.microphoneVoiceProcessing)
                .disabled(state.isRunning)
                .onChange(of: state.microphoneVoiceProcessing) { _, _ in state.updateSettings() }
            Note("用系统的语音处理在源头消除回声和噪声；输入与输出设备不同（例如外接麦克风 + 内置扬声器）时可能不生效。戴耳机始终是最干净的方式。")
        }
    }
}

private struct CloudSettings: View {
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
        Note("云端流式识别通常在说话的同时出字（几百毫秒）。Soniox 在同一句中英混说上最稳；OpenAI 转写为 24 kHz 自动重采样；Deepgram 需指定单一语言。请确认会议参与者同意录音与转写。")
    }
}

// MARK: - Answers

private struct AnswerSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "回答引擎", icon: "sparkles") {
            MenuPicker(title: "引擎", selection: $state.answerBackend, options: AnswerBackend.allCases.map { ($0, $0.title) })
                .disabled(state.isAnswering)
                .onChange(of: state.answerBackend) { _, value in
                    state.saveAnswerSettings()
                    if value == .codexAppServer { Task { await state.appServer.prewarm() } }
                }
            if state.answerBackend == .openAICompatible {
                HStack {
                    TextField("API 地址，例如 https://api.openai.com/v1", text: $state.apiBaseURL)
                        .textFieldStyle(.roundedBorder)
                        .onChange(of: state.apiBaseURL) { _, _ in state.saveAnswerSettings() }
                    Menu("常用") {
                        Button("OpenAI") { state.apiBaseURL = "https://api.openai.com/v1" }
                        Button("DeepSeek") { state.apiBaseURL = "https://api.deepseek.com/v1" }
                        Button("通义千问 DashScope") { state.apiBaseURL = "https://dashscope.aliyuncs.com/compatible-mode/v1" }
                        Button("Moonshot Kimi") { state.apiBaseURL = "https://api.moonshot.cn/v1" }
                        Button("Groq") { state.apiBaseURL = "https://api.groq.com/openai/v1" }
                        Button("OpenRouter") { state.apiBaseURL = "https://openrouter.ai/api/v1" }
                        Button("本机 Ollama") { state.apiBaseURL = "http://localhost:11434/v1" }
                        Button("本机 LM Studio") { state.apiBaseURL = "http://localhost:1234/v1" }
                    }.fixedSize()
                }
                TextField("模型 ID（与服务商一致）", text: $state.apiModel)
                    .textFieldStyle(.roundedBorder)
                    .onChange(of: state.apiModel) { _, _ in state.saveAnswerSettings() }
                SecureField("API Key（保存在系统钥匙串）", text: $state.apiKey)
                    .textFieldStyle(.roundedBorder)
                    .onSubmit { state.saveAPIKey() }
                    .onChange(of: state.apiKey) { _, _ in state.saveAPIKey() }
                Note("逐字流式输出，首字通常最快。实时问答建议选择低延迟模型；截图题需要支持图像输入的模型。转录片段、检索到的资料和截图会发送到所填服务。")
            } else {
                HStack {
                    VStack(alignment: .leading, spacing: 3) {
                        Text(state.codexLoginStatus).font(.system(size: 13, weight: .medium))
                        Text("Codex CLI · \(state.codexVersion)").font(.system(size: 11)).foregroundStyle(.secondary)
                    }
                    Spacer()
                    Button { Task { await state.refreshCodexStatus() } } label: { Image(systemName: "arrow.clockwise").frame(width: 28, height: 28) }
                        .buttonStyle(SecondaryButtonStyle())
                }
                MenuPicker(title: "回答模型", selection: $state.codexModelID, options: codexModelOptions, placeholder: "请选择模型")
                    .disabled(state.isAnswering)
                    .onChange(of: state.codexModelID) { _, _ in state.normalizeCodexSelection() }
                if state.codexModels.isEmpty {
                    TextField("模型 ID（与 Codex 中一致）", text: $state.codexModelID)
                        .textFieldStyle(.roundedBorder).disabled(state.isAnswering)
                        .onChange(of: state.codexModelID) { _, _ in state.updateSettings() }
                }
                MenuPicker(title: "推理强度", selection: $state.codexReasoning, options: state.codexEfforts.map { ($0, $0) })
                    .disabled(state.isAnswering).onChange(of: state.codexReasoning) { _, _ in state.updateSettings() }
                Note(state.answerBackend == .codexAppServer
                     ? "常驻 `codex app-server` 复用进程与登录，逐字流式显示；不可用时自动回退到 codex exec。实时问答建议推理强度选 low 或更低。"
                     : "每次回答启动一次 codex exec，等待完整结果后显示；仅在常驻服务不可用时使用。")
                if !state.appServerNote.isEmpty { Note(state.appServerNote, color: .orange) }
            }
        }
        SettingsSection(title: "问题识别", icon: "text.bubble") {
            Toggle("自动识别问题并回答", isOn: $state.automaticAnswers).onChange(of: state.automaticAnswers) { _, _ in state.updateSettings() }
            Toggle("不确定的句子交给 API 模型二次判断", isOn: $state.verifyUncertainWithModel)
                .disabled(state.answerBackend != .openAICompatible)
                .onChange(of: state.verifyUncertainWithModel) { _, _ in state.saveAnswerSettings() }
            Note("本地规则逐句判断：区分陈述、未说完、提问、请求（“说一下你对…的理解”“我想了解…”“walk me through…”）和追问；去掉“嗯、那个、um、so”等口头禅；忽略“能听到吗”这类设备确认。以问号或“吗 / 呢”结尾的问题约 0.6 秒内就开始回答；对方补充了后半句时自动用完整问题重新回答；问题前面的铺垫句会一并交给模型。课程场景更严格，避免把讲师的自问自答当成提问。拿不准的句子显示为可点击的“可能的问题”。")
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
            Note("macOS 没有公开 API 能保证窗口不被录屏：此选项对旧式捕获和多数“共享单个窗口”有效，但 macOS 15 起部分基于 ScreenCaptureKit 的整屏共享 / 录屏仍可能拍到。最稳妥的做法是只共享会议窗口或标签页，或者把回答放到手机上（常规 → 手机伴侣）。\(state.shortcutLabel(.panicHide)) 可一键隐藏全部窗口。请遵守会议、考试与面试方的规则。")
        }
    }
}

// MARK: - Recording

private struct RecordingSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "会话记录", icon: "tray.full") {
            Toggle("记录每次会话（转录、识别到的问题、提词与当时的资料要求）", isOn: $state.recordSessions)
                .disabled(state.isRunning)
                .onChange(of: state.recordSessions) { _, _ in state.updateSettings() }
            Toggle("会话结束后自动生成摘要与问题清单", isOn: $state.autoSummarize)
                .disabled(!state.recordSessions)
                .onChange(of: state.autoSummarize) { _, _ in state.updateSettings() }
            Note("记录边说边写入本机（崩溃或强制退出也不会丢），不保存原始音频。摘要用当前的回答引擎生成，会把这次会话的转录发给该引擎；在“记录”里可随时重新生成、导出 Markdown 或删除。")
            HStack {
                Button("打开记录文件夹") {
                    try? FileManager.default.createDirectory(at: SessionArchive.root, withIntermediateDirectories: true)
                    NSWorkspace.shared.open(SessionArchive.root)
                }
                .buttonStyle(SecondaryButtonStyle())
                Button("导出当前转录（纯文本）") { state.saveTranscriptExport() }.buttonStyle(SecondaryButtonStyle()).disabled(state.entries.isEmpty)
            }
        }
    }
}

// MARK: - Updates

private struct UpdateSettings: View {
    @Environment(AppState.self) private var state
    @State private var token = ""
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
            Note("从 GitHub Releases 下载，校验 SHA-256 和签名后再安装；点击“重启并安装”后应用会替换自身并重新打开。若已绑定本机签名，新版本会自动用同一签名重新签名，录屏与麦克风权限保持不变。")
        }
        SettingsSection(title: "GitHub 访问", icon: "key") {
            HStack {
                TextField("仓库", text: $updater.repository).textFieldStyle(.roundedBorder).frame(maxWidth: 260)
                    .onSubmit { updater.saveSettings() }
                Spacer()
                Label(updater.hasToken ? "已保存访问令牌" : "未保存访问令牌", systemImage: updater.hasToken ? "checkmark.seal" : "key")
                    .foregroundStyle(updater.hasToken ? .green : .secondary)
            }
            HStack {
                SecureField("访问令牌（fine-grained token，Contents 只读）", text: $token).textFieldStyle(.roundedBorder)
                Button("保存") { updater.saveToken(token); token = "" }.buttonStyle(ChipButtonStyle(prominent: true)).disabled(token.isEmpty)
                if updater.hasToken { Button("清除") { updater.saveToken("") }.buttonStyle(ChipButtonStyle()) }
            }
            Note("仓库是私有的，检查和下载更新需要授权：在 GitHub → Settings → Developer settings → Fine-grained tokens 新建令牌，只勾选该仓库的 Contents: Read-only。令牌保存在系统钥匙串。已用 GitHub CLI 登录（gh auth login）时可以不填。")
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
