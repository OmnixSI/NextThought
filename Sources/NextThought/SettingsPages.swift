import AppKit
import SwiftUI
import NextThoughtCore

// MARK: - Shared rows

/// A switch with one gray help line under it, for settings whose label alone is not enough.
struct SettingToggle: View {
    let title: String
    var help: String?
    @Binding var isOn: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            Toggle(LocalizedStringKey(title), isOn: $isOn)
            if let help { Note(help, presentation: .inline) }
        }
    }
}

// MARK: - 场景

/// What each scene sets up when a meeting starts. One row per scene, only the
/// choices a scene really changes; everything else follows from the scene itself.
struct SceneSettingsPage: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            SettingsSection(title: "各场景开会时的设置", icon: "square.grid.2x2") {
                VStack(spacing: 0) {
                    ForEach(SessionMode.builtIn) { mode in
                        if mode != SessionMode.builtIn.first { Rectangle().fill(Ink.hairline).frame(height: 0.5) }
                        SceneRow(mode: mode).padding(.vertical, Space.s3)
                    }
                }
                Note("开始一场会时按这里的设置来；会中仍可随时切换。")
            }
        }
    }
}

private struct SceneRow: View {
    @Environment(AppState.self) private var state
    let mode: SessionMode
    private var scenes: SceneSettings { SceneSettings.shared }
    private var customised: Bool {
        scenes.isCustomised(mode)
            || state.defaultNotesTemplateID(for: mode) != MeetingTypeDefaults.for(mode).template.rawValue
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            HStack(spacing: Space.s2) {
                Circle().fill(mode.theme.tint).frame(width: 8, height: 8).accessibilityHidden(true)
                Text(verbatim: mode.displayName).font(Typo.label)
                Spacer(minLength: Space.s2)
                if customised {
                    Button("恢复默认") {
                        scenes.reset(mode)
                        state.setDefaultNotesTemplate(MeetingTypeDefaults.for(mode).template.rawValue, for: mode)
                    }
                    .buttonStyle(ChipButtonStyle())
                    .accessibilityIdentifier("settings.scene.reset." + mode.rawValue)
                }
            }
            HStack(alignment: .top, spacing: Space.s3) {
                column("会中", picker: SceneMenu(
                    selection: Binding(get: { scenes.liveMode(for: mode) }, set: { scenes.setLiveMode($0, for: mode) }),
                    options: [(.prompting, String(localized: "自动回答")), (.notesOnly, String(localized: "只记录"))],
                    accessibilityTitle: String(localized: "会中") + " · " + mode.displayName))
                column("回答", picker: SceneMenu(
                    selection: Binding(get: { scenes.style(for: mode) }, set: { scenes.setStyle($0, for: mode) }),
                    options: [(.concise, String(localized: "简洁")), (.detailed, String(localized: "详细"))],
                    accessibilityTitle: String(localized: "回答") + " · " + mode.displayName))
                column("纪要模板", picker: SceneMenu(
                    selection: Binding(get: { state.defaultNotesTemplateID(for: mode) },
                                       set: { state.setDefaultNotesTemplate($0, for: mode) }),
                    options: state.allNotesTemplates().map { ($0.id, $0.name) },
                    accessibilityTitle: String(localized: "纪要模板") + " · " + mode.displayName))
                    .accessibilityIdentifier("settings.template." + mode.rawValue)
                column("提词", picker: SceneMenu(
                    selection: Binding(get: { scenes.cueStyle(for: mode) }, set: { scenes.setCueStyle($0, for: mode) }),
                    options: [(.keyPoints, String(localized: "要点")), (.script, String(localized: "逐句"))],
                    accessibilityTitle: String(localized: "提词") + " · " + mode.displayName))
            }
        }
    }

    private func column<Picker: View>(_ title: String, picker: Picker) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(LocalizedStringKey(title)).font(Typo.caption).foregroundStyle(Ink.secondary)
            picker.frame(maxWidth: .infinity, alignment: .leading)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

/// A compact value menu that sits at the left of its column.
private struct SceneMenu<Value: Hashable>: View {
    @Binding var selection: Value
    let options: [(Value, String)]
    var accessibilityTitle: String

    var body: some View {
        Menu {
            ForEach(options.indices, id: \.self) { index in
                Button { selection = options[index].0 } label: {
                    if options[index].0 == selection {
                        Label { Text(verbatim: options[index].1) } icon: { Image(systemName: "checkmark") }
                    } else { Text(verbatim: options[index].1) }
                }
            }
        } label: {
            HStack(spacing: 6) {
                Text(verbatim: options.first { $0.0 == selection }?.1 ?? "").lineLimit(1)
                Image(systemName: "chevron.down").font(.system(size: 10, weight: .medium)).foregroundStyle(Ink.secondary)
            }
        }
        .menuStyle(.button).buttonStyle(SecondaryButtonStyle()).menuIndicator(.hidden).fixedSize()
        .accessibilityLabel(Text(verbatim: accessibilityTitle))
        .accessibilityValue(Text(verbatim: options.first { $0.0 == selection }?.1 ?? ""))
    }
}

// MARK: - 声音

struct SoundSettings: View {
    @Environment(AppState.self) private var state
    @State private var confirmDelete = false
    @FocusState private var vocabularyFocused: Bool

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
            SettingsSection(title: "会议声音与麦克风", icon: "speaker.wave.2") {
                if !state.capturePermissionGranted {
                    Note("需要音频权限，才能听到会议里的声音。", color: .orange)
                    Button("允许录屏与系统音频") { Task { await state.requestCapturePermission() } }
                }
                HStack(spacing: 8) {
                    MenuPicker(title: "会议声音来自", selection: $state.selectedTargetID,
                               options: state.captureTargets.map { ($0.id, $0.title) }, placeholder: "选择会议窗口")
                        .disabled(state.isRunning || state.isChangingSession)
                        .onChange(of: state.selectedTargetID) { _, _ in Task { await state.changeCaptureTarget() } }
                    // The list refreshes itself whenever this page opens; this is for a window opened later.
                    Button { Task { await state.refreshTargets() } } label: { Image(systemName: "arrow.clockwise") }
                        .buttonStyle(.borderless).help("重新读取窗口列表").accessibilityLabel("刷新窗口列表")
                }
                SettingToggle(title: "同时识别我说的话（麦克风）", isOn: $state.microphoneEnabled)
                    .disabled(state.isRunning || state.isChangingSession)
                    .onChange(of: state.microphoneEnabled) { _, _ in state.updateSettings() }
                if state.microphoneEnabled && state.microphoneAccess != .granted {
                    Button("允许麦克风") { Task { await state.requestMicrophoneAccess() } }
                }
            }
            .task { if !state.isRunning && !state.isChangingSession { await state.refreshTargets() } }

            SettingsSection(title: "语音识别", icon: "waveform") {
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
                AdvancedOptions("sound-recognition", title: "更多识别选项") { recognitionOptions }
                    .confirmationDialog("删除全部本地识别模型？", isPresented: $confirmDelete, titleVisibility: .visible) {
                        Button("删除", role: .destructive) { state.deleteModels() }
                        Button("取消", role: .cancel) { }
                    }
            }

            SpeakerSettingsSection()
            GlossarySettingsEntry {
                VStack(alignment: .leading, spacing: 4) {
                    Text("临时专业词").font(Typo.label)
                    TextField("专业词 / 人名，逗号分隔", text: $state.recognitionVocabulary)
                        .textFieldStyle(.plain).padding(.horizontal, 10).padding(.vertical, 8)
                        .frame(minHeight: 36)
                        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: 8))
                        .overlay(RoundedRectangle(cornerRadius: 8).stroke(vocabularyFocused ? Ink.focus : Ink.hairline, lineWidth: vocabularyFocused ? 1.5 : 1))
                        .focused($vocabularyFocused)
                        .onChange(of: state.recognitionVocabulary) { _, _ in state.updateSettings() }
                    Note("随手填的人名、项目名，只用来提高识别准确度；上面管理的词表同样会送进识别，并在转写、回答和纪要里统一写法。", presentation: .inline)
                }
            }
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
                      help: "不戴耳机时对方的声音会从扬声器传进麦克风；应用比对两路音频，回声不记为“我”。戴耳机始终最干净。",
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

// MARK: - 回答

struct AnswerSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
            SettingsSection(title: "回答服务", icon: "sparkles") {
                AnswerServiceChooser()
            }
            WebSearchSettings()
            InsightSettings()
            SkillSettings()
            AdvancedOptions("answers-detection", title: "问题识别与运行方式", card: true) {
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
            }
        }
    }
}

/// One choice at the top, API or Codex, and only the fields of the chosen path below it.
private struct AnswerServiceChooser: View {
    @Environment(AppState.self) private var state
    private enum Provider: String, CaseIterable, Identifiable {
        case api = "API", codex = "Codex"
        var id: String { rawValue }
    }
    @State private var shown: Provider?
    private var current: Provider { shown ?? (state.answerBackend == .openAICompatible ? .api : .codex) }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s4) {
            Picker("回答服务", selection: Binding(get: { current }, set: choose)) {
                ForEach(Provider.allCases) { Text(verbatim: $0.rawValue).tag($0) }
            }
            .pickerStyle(.segmented).labelsHidden().frame(maxWidth: 220)
            .accessibilityLabel("回答服务")
            .accessibilityIdentifier("settings.answerProvider")
            switch current {
            case .api: APIConnectView(showsMoreOptions: false)
            case .codex: CodexEngineSection(showsAdvanced: false)
            }
            AdvancedOptions("answers-provider", title: "推理强度与预设") {
                switch current {
                case .api:
                    APIEffortRow()
                    APIPresetManager()
                case .codex:
                    CodexReasoningPicker()
                    CodexFastModeToggle()
                }
            }
        }
    }

    /// Switches only when the chosen path can already answer; otherwise it shows that
    /// path's setup and leaves the working one in use.
    private func choose(_ next: Provider) {
        shown = next
        switch next {
        case .api:
            let local = APIProviders.isLocal(baseURL: state.apiBaseURL)
            if !state.apiModel.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, !state.apiKey.isEmpty || local {
                state.selectAnswerBackend(.openAICompatible)
            }
        case .codex:
            if state.codexReady { state.selectAnswerBackend(.codexAppServer) }
        }
    }
}
