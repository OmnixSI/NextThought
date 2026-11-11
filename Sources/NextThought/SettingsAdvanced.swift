import AppKit
import SwiftUI
import NextThoughtCore

// MARK: - 高级

/// Everything that is rarely changed, in groups named by area and closed by default.
/// Search and deep links open the group they point at.
struct AdvancedSettings: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text("这里的选项一般不需要改。")
                .font(Typo.caption).foregroundStyle(Ink.secondary)
                .padding(.top, Space.s3).padding(.bottom, Space.s3)
            ForEach(AdvancedArea.allCases) { area in
                AdvancedGroup(area: area) { content(for: area) }
            }
        }
    }

    @ViewBuilder private func content(for area: AdvancedArea) -> some View {
        switch area {
        case .sound: SoundAdvanced()
        case .answers: AnswerAdvanced()
        case .documents: DocumentComponentsAdvanced()
        case .notes: NotesAdvanced()
        case .cue: CueAdvanced()
        case .companion: CompanionSection(headed: false)
        case .updates: UpdateAdvanced()
        case .data: DataMaintenanceAdvanced()
        }
    }
}

// MARK: - 声音与识别

/// The default sound source and microphone (a meeting draft chooses its own on the
/// prepare page), the recognition engine and its options, and the temporary word list.
private struct SoundAdvanced: View {
    @Environment(AppState.self) private var state
    @State private var confirmDelete = false
    @FocusState private var vocabularyFocused: Bool

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: Space.s3) {
            Text("默认声音来源").font(Typo.label)
            Note("准备页里每场会议可以单独选；这里是没有准备页时用的来源。", presentation: .inline)
            if !state.capturePermissionGranted {
                Note("需要音频权限，才能听到会议里的声音。", color: .orange)
                Button("允许录屏与系统音频") { Task { await state.requestCapturePermission() } }
            }
            HStack(spacing: 8) {
                MenuPicker(title: "会议声音来自", selection: $state.selectedTargetID,
                           options: state.captureTargets.map { ($0.id, $0.title) }, placeholder: "选择会议窗口")
                    .disabled(state.isRunning || state.isChangingSession)
                    .onChange(of: state.selectedTargetID) { _, _ in Task { await state.changeCaptureTarget() } }
                Button { Task { await state.refreshTargets() } } label: { Image(systemName: "arrow.clockwise") }
                    .buttonStyle(.borderless).help("重新读取窗口列表").accessibilityLabel("刷新窗口列表")
            }
            SettingToggle(title: "同时识别我说的话（麦克风）", isOn: $state.microphoneEnabled)
                .disabled(state.isRunning || state.isChangingSession)
                .onChange(of: state.microphoneEnabled) { _, _ in state.updateSettings() }
            if state.microphoneEnabled && state.microphoneAccess != .granted {
                Button("允许麦克风") { Task { await state.requestMicrophoneAccess() } }
            }
            Divider()
            Text("识别引擎").font(Typo.label)
            Note("把声音变成文字的方式。推荐本地离线：不上传声音，也不用 Key。", presentation: .inline)
            MenuPicker(title: "引擎", selection: $state.transcriptionBackend, options: [
                ("zipformer", "本地流式 · 离线（推荐）"),
                ("apple", "Apple Speech · 本地 / macOS 26"),
                ("cloud", "云端流式 · 需 API Key"),
            ], localizesOptions: true)
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
            recognitionOptions
            Divider()
            VStack(alignment: .leading, spacing: 4) {
                Text("临时专业词").font(Typo.label)
                TextField("专业词 / 人名，逗号分隔", text: $state.recognitionVocabulary)
                    .textFieldStyle(.plain).padding(.horizontal, 10).padding(.vertical, 8)
                    .frame(minHeight: 36)
                    .background(Ink.sunken, in: RoundedRectangle(cornerRadius: 8))
                    .overlay(RoundedRectangle(cornerRadius: 8).stroke(vocabularyFocused ? Ink.focus : Ink.hairline, lineWidth: vocabularyFocused ? 1.5 : 1))
                    .focused($vocabularyFocused)
                    .onChange(of: state.recognitionVocabulary) { _, _ in state.updateSettings() }
                Note("随手填的人名、项目名，只用来提高识别准确度；长期的词表在 通用 → 术语和人名。", presentation: .inline)
            }
        }
        .task { if !state.isRunning && !state.isChangingSession { await state.refreshTargets() } }
        .confirmationDialog("删除全部本地识别模型？", isPresented: $confirmDelete, titleVisibility: .visible) {
            Button("删除", role: .destructive) { state.deleteModels() }
            Button("取消", role: .cancel) { }
        }
    }

    @ViewBuilder private var recognitionOptions: some View {
        @Bindable var state = state
        if state.transcriptionBackend == "zipformer" {
            VStack(alignment: .leading, spacing: 3) {
                MenuPicker(title: "流式模型", selection: $state.localStreamingModel, options: LocalStreamingModel.allCases.map { ($0, $0.title) }, localizesOptions: true)
                    .disabled(state.modelControlsDisabled)
                    .onChange(of: state.localStreamingModel) { _, _ in state.updateSettings(); Task { await state.refreshModelStatus(); await state.prewarmRecognizer() } }
                Note("边说边出字的识别模型。" + String(localized: String.LocalizationValue(state.localStreamingModel.detail)), presentation: .inline)
            }
            SettingToggle(title: "整句精修", help: "每句说完后，再用更准的离线模型把整句重新识别一遍（推荐）。", isOn: $state.secondPassEnabled)
                .disabled(state.modelControlsDisabled || state.localStreamingModel == .legacy)
                .onChange(of: state.secondPassEnabled) { _, _ in state.updateSettings(); Task { await state.prewarmRecognizer() } }
            if let rtf = state.lastRTF, let seconds = state.lastASRSeconds {
                Note(String(format: "最近处理 %.2f s · RTF %.2f", seconds, rtf) + (state.lastRefineSeconds.map { String(format: " · 精修 %.2f s", $0) } ?? ""), presentation: .inline)
            }
        }
        SettingToggle(title: "后台预加载识别模型", help: "提前把模型放进内存，开始记录几乎瞬间启动，会多占一些内存。", isOn: $state.keepRecognizerWarm)
            .onChange(of: state.keepRecognizerWarm) { _, _ in state.saveRecognizerSettings() }
        if let seconds = state.lastStartupSeconds { Note(String(format: "上次开始用时 %.1f 秒", seconds), presentation: .inline) }
        SettingToggle(title: "忽略扬声器回声（推荐）",
                      help: "不戴耳机时对方的声音会从扬声器传进麦克风；应用比对两路音频，回声不记为“我”。",
                      isOn: $state.echoSuppression)
            .onChange(of: state.echoSuppression) { _, _ in state.updateSettings() }
        SettingToggle(title: "麦克风回声消除（Apple 语音处理，实验）", help: "交给系统在录音时消除回声；少数设备上可能让声音变小。", isOn: $state.microphoneVoiceProcessing)
            .disabled(state.isRunning)
            .onChange(of: state.microphoneVoiceProcessing) { _, _ in state.updateSettings() }
        if state.transcriptionBackend == "zipformer" {
            HStack {
                Link("模型来源：sherpa-onnx X-ASR（Apache-2.0）", destination: URL(string: "https://github.com/Gilgamesh-J/X-ASR")!)
                    .font(.system(size: 11)).foregroundStyle(Color.accentColor)
                Spacer()
                Button("删除全部模型") { confirmDelete = true }.buttonStyle(.borderless).foregroundStyle(.red).disabled(state.modelControlsDisabled)
            }
        }
    }
}

// MARK: - 回答服务

/// Reasoning strength and saved providers, the double-check of uncertain questions,
/// how Codex runs, which search service answers use, and screen reading.
private struct AnswerAdvanced: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: Space.s3) {
            Text("推理强度与预设").font(Typo.label)
            Note("推理强度越高，回答越慢也越用心；预设是保存好的服务商设置，可以来回切换。", presentation: .inline)
            if state.answerBackend == .openAICompatible {
                APIEffortRow()
                APIPresetManager()
            } else {
                CodexReasoningPicker()
            }
            Divider()
            VStack(alignment: .leading, spacing: 3) {
                Toggle("不确定的句子交给回答服务二次判断", isOn: $state.verifyUncertainWithModel)
                    .disabled(state.answerBackend != .openAICompatible)
                    .onChange(of: state.verifyUncertainWithModel) { _, _ in state.saveAnswerSettings() }
                Note(state.answerBackend == .openAICompatible
                     ? "本机规则先逐句判断是不是在向你提问；拿不准的句子再让回答服务确认一次，更准，但会多用一点额度。"
                     : "需要使用 API 回答服务才能启用二次判断。", presentation: .inline)
            }
            if state.answerBackend != .openAICompatible {
                VStack(alignment: .leading, spacing: 3) {
                    MenuPicker(title: "Codex 运行方式", selection: $state.answerBackend,
                               options: [(.codexAppServer, String(localized: "常驻服务 · 流式（推荐）")),
                                         (.codexExec, String(localized: "codex exec · 兼容"))])
                        .onChange(of: state.answerBackend) { _, _ in state.saveAnswerSettings() }
                    Note("常驻服务让 Codex 一直开着，回答逐字流式出现，也更快；不可用时自动改用 codex exec，它每次新开一个进程，等完整结果后才显示。", presentation: .inline)
                }
            }
            Divider()
            Text("联网搜索").font(Typo.label)
            WebSearchAdvanced()
            Divider()
            InsightSettings()
        }
    }
}
