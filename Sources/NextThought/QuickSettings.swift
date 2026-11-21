import AppKit
import SwiftUI
import NextThoughtCore

/// What "设置" means day to day: three states that can need attention and six
/// preferences that change from meeting to meeting. A popover under the gear, one level
/// deep, short enough not to scroll. Everything else is behind 全部设置….
struct QuickSettingsPanel: View {
    @Environment(AppState.self) private var state
    private var updater: Updater { Updater.shared }

    var body: some View {
        @Bindable var state = state
        @Bindable var cue = CueIslandController.shared.model
        @Bindable var coach = state.coach
        VStack(alignment: .leading, spacing: 0) {
            Text("状态").overline().padding(.bottom, Space.s1)
            statusRows
            Rectangle().fill(Ink.hairline).frame(height: 0.5).padding(.vertical, Space.s3)
            Text("偏好").overline().padding(.bottom, Space.s1)
            preference("提词") {
                InkSegmented(items: [(value: CueSurface.island, title: "提词岛"), (value: CueSurface.window, title: "浮窗")],
                             selection: $cue.surface)
                    .fixedSize()
            }
            preference("提词字号") {
                Slider(value: $state.teleprompterFontSize, in: 14...36, step: 1)
                    .frame(width: 132)
                    .onChange(of: state.teleprompterFontSize) { _, _ in state.saveAnswerSettings() }
                    .accessibilityLabel("提词字号")
                Text("\(Int(state.teleprompterFontSize))").font(Font.zoomed(.caption).monospacedDigit()).foregroundStyle(Ink.secondary)
                    .frame(width: 22, alignment: .trailing)
            }
            Toggle("提词岛一直展开", isOn: $cue.alwaysOpen)
                .frame(minHeight: 34)
                .help("会中不用把鼠标移上去，回答一直完整显示 · \(state.shortcutLabel(.toggleCueOpen))")
                .accessibilityIdentifier("quickSettings.cueAlwaysOpen")
            VStack(alignment: .leading, spacing: 0) {
                Toggle("双屏演讲", isOn: Binding(get: { PresenterMode.shared.isEnabled },
                                              set: { PresenterMode.shared.isEnabled = $0 }))
                    .frame(minHeight: 34)
                    .accessibilityIdentifier("quickSettings.presenter")
                Text(verbatim: PresenterMode.shared.summary)
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                    .padding(.bottom, Space.s1)
                if PresenterMode.shared.isEnabled {
                    Toggle("讲的时候显示这一页的讲稿", isOn: Binding(get: { PresenterMode.shared.showsSlideNotes },
                                                         set: { PresenterMode.shared.showsSlideNotes = $0 }))
                        .frame(minHeight: 34)
                        .help("用 PowerPoint 自带的演讲者备注时关掉：提词岛只在被提问时展开")
                        .accessibilityIdentifier("quickSettings.presenterNotes")
                }
            }
            HStack {
                Text("回答语言")
                Spacer()
                Picker("回答语言", selection: $state.answerLanguage) {
                    Text("跟随问题").tag("auto")
                    Text("中文").tag("zh")
                    Text("English").tag("en")
                }
                .labelsHidden().pickerStyle(.segmented).frame(width: 190)
                .accessibilityIdentifier("quickSettings.answerLanguage")
            }
            .frame(minHeight: 34)
            .help("英文答辩或面试选 English：不管问题听成什么语言，回答都用英文")
            Toggle("英文回答附中文对照", isOn: $state.answerGloss)
                .frame(minHeight: 34)
                .help("回答上方用中文写对方问了什么，每句英文下面有中文意思；只帮你理解，不用念")
                .accessibilityIdentifier("quickSettings.answerGloss")
            Toggle("临场教练", isOn: $coach.isEnabled).frame(minHeight: 34)
            VStack(alignment: .leading, spacing: 0) {
                Toggle("留意眼神", isOn: $coach.watchesGaze)
                    .frame(minHeight: 34)
                    .onChange(of: coach.watchesGaze) { _, on in if on { state.coach.startWatching() } }
                if coach.watchesGaze {
                    HStack(spacing: Space.s2) {
                        Text(coach.isCalibrated ? "已校准过" : "还没有校准").font(Typo.caption).foregroundStyle(Ink.secondary)
                        Button("校准") { SettingsRoute.shared.present(.presenter, anchor: "临场教练") }
                            .buttonStyle(GhostButtonStyle(height: 24, horizontalPadding: 6))
                            .accessibilityIdentifier("quickSettings.calibrate")
                        Spacer(minLength: 0)
                    }
                    .padding(.bottom, Space.s1)
                }
            }
            Toggle("会后自动整理纪要", isOn: $state.autoSummarize)
                .frame(minHeight: 34)
                .onChange(of: state.autoSummarize) { _, _ in state.saveNotesSettings() }
            preference("外观") {
                InkSegmented(items: [(value: "system", title: "跟随系统"), (value: "light", title: "浅色"), (value: "dark", title: "深色")],
                             selection: Binding(get: { state.appearance }, set: { state.setAppearance($0) }))
                    .fixedSize()
            }
            Rectangle().fill(Ink.hairline).frame(height: 0.5).padding(.vertical, Space.s3)
            footer
        }
        .font(.zoomed(size: 13))
        .padding(Space.s4)
        .frame(width: 360)
        .background(Ink.paper)
        .foregroundStyle(Ink.ink)
        .tint(Brand.accent)
        .wideHitTargets()
        .accessibilityIdentifier("quickSettings")
    }

    // MARK: Status

    @ViewBuilder private var statusRows: some View {
        QuickStatusRow(symbol: "sparkles", title: "回答服务",
                       detail: state.answerEngineReady
                           ? String(localized: "已连接 · ") + (state.currentAnswerModel.isEmpty ? "" : state.currentAnswerModel)
                           : String(localized: "未连接"),
                       tone: state.answerEngineReady ? .ready : .caution,
                       actionTitle: state.answerEngineReady ? nil : "连接…") {
            SettingsRoute.shared.present(.answers, anchor: "回答服务")
        }
        let soundMissing = missingSound
        QuickStatusRow(symbol: "speaker.wave.2", title: "声音",
                       detail: soundMissing.isEmpty ? String(localized: "系统声音和麦克风已允许")
                           : String(localized: "还没有允许：") + soundMissing.joined(separator: "、"),
                       tone: soundMissing.isEmpty ? .ready : .caution,
                       actionTitle: soundMissing.isEmpty ? nil : "允许") { repairSound() }
        speechRow
    }

    private var missingSound: [String] {
        var missing: [String] = []
        if !state.capturePermissionGranted { missing.append(String(localized: "系统声音")) }
        if state.microphoneAccess != .granted { missing.append(String(localized: "麦克风")) }
        return missing
    }

    /// The same repairs the settings page offers.
    private func repairSound() {
        if !state.capturePermissionGranted {
            Task { await state.requestCapturePermission() }
        } else if state.microphoneAccess == .undetermined {
            Task { await state.requestMicrophoneAccess() }
        } else {
            state.openMicrophoneSettings()
        }
    }

    @ViewBuilder private var speechRow: some View {
        switch state.transcriptionBackend {
        case "zipformer":
            if state.isDownloadingModel {
                QuickStatusRow(symbol: "waveform", title: "语音识别",
                               detail: String(localized: "正在下载 · ") + "\(Int(state.modelDownloadProgress * 100))%",
                               tone: .idle, progress: state.modelDownloadProgress, actionTitle: "取消") { state.cancelModelDownload() }
            } else if state.isModelInstalled && state.missingPackages.isEmpty {
                QuickStatusRow(symbol: "waveform", title: "语音识别", detail: String(localized: "本机模型已就绪"), tone: .ready)
            } else {
                let size = state.missingPackages.reduce(0) { $0 + $1.approximateMegabytes }
                QuickStatusRow(symbol: "waveform", title: "语音识别",
                               detail: size > 0 ? String(localized: "需要下载（约 \(size) MB）") : String(localized: "需要下载"),
                               tone: .caution, actionTitle: "下载") { state.downloadModel() }
            }
        case "apple":
            QuickStatusRow(symbol: "waveform", title: "语音识别", detail: String(localized: "Apple 语音识别（本机）"), tone: .ready)
        default:
            QuickStatusRow(symbol: "waveform", title: "语音识别", detail: String(localized: "云端识别 · ") + state.cloudProvider.title, tone: .idle)
        }
    }

    // MARK: Preferences

    private func preference<Control: View>(_ title: LocalizedStringKey, @ViewBuilder control: () -> Control) -> some View {
        HStack(spacing: Space.s2) {
            Text(title).lineLimit(1).minimumScaleFactor(0.8)
            Spacer(minLength: Space.s2)
            control()
        }
        .frame(minHeight: 36)
    }

    // MARK: Footer

    private var footer: some View {
        HStack(spacing: Space.s2) {
            Button { SettingsRoute.shared.presentStored() } label: {
                HStack(spacing: 6) {
                    Text("全部设置…").font(Typo.label)
                    Text("⌘,").font(Typo.caption).foregroundStyle(Ink.secondary)
                }
            }
            .buttonStyle(GhostButtonStyle(horizontalPadding: 6))
            .accessibilityIdentifier("quickSettings.all")
            Spacer(minLength: Space.s2)
            Text(verbatim: "v" + updater.currentVersion).font(Typo.caption).foregroundStyle(Ink.secondary)
            Button {
                Task { await updater.check(userInitiated: true) }
            } label: {
                Text(LocalizedStringKey(updater.phase == .checking ? "正在检查…" : "检查更新")).font(Typo.caption)
            }
            .buttonStyle(GhostButtonStyle(height: 26, horizontalPadding: 6))
            .disabled(updater.phase == .checking)
        }
    }
}

/// A state of the app: icon, name, one line, and an action when something can be fixed.
/// The whole row is the action.
private struct QuickStatusRow: View {
    let symbol: String
    let title: LocalizedStringKey
    let detail: String
    let tone: StatusDot.Tone
    var progress: Double? = nil
    var actionTitle: LocalizedStringKey? = nil
    var action: (() -> Void)? = nil
    @State private var hovering = false

    var body: some View {
        Group {
            if let action {
                Button(action: action) { content }.buttonStyle(.plain)
            } else {
                content
            }
        }
        .background(hovering && action != nil ? Ink.hover : Color.clear, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
        .onHover { hovering = $0 }
        .accessibilityElement(children: .combine)
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: Space.s1) {
            HStack(spacing: Space.s3) {
                Image(systemName: symbol).font(.zoomed(size: 15)).foregroundStyle(Ink.secondary).frame(width: 22)
                VStack(alignment: .leading, spacing: 1) {
                    Text(title).font(Typo.label)
                    HStack(spacing: 6) {
                        StatusDot(tone: tone, size: 6)
                        Text(verbatim: detail).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
                    }
                }
                Spacer(minLength: Space.s2)
                if let actionTitle {
                    Text(actionTitle).font(Typo.label)
                        .padding(.horizontal, 10).frame(height: 26)
                        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
                }
            }
            if let progress {
                ProgressView(value: progress).controlSize(.small).padding(.leading, 34)
            }
        }
        .padding(.vertical, 6).padding(.horizontal, 4)
        .contentShape(Rectangle())
    }
}

// MARK: - Popover

extension View {
    /// Anchors the quick settings panel to this view (the gear in the title row, or the
    /// "⋯" button of a running meeting).
    func quickSettingsPopover(arrowEdge: Edge = .bottom) -> some View {
        modifier(QuickSettingsPopover(arrowEdge: arrowEdge))
    }
}

private struct QuickSettingsPopover: ViewModifier {
    @Environment(AppState.self) private var state
    let arrowEdge: Edge

    func body(content: Content) -> some View {
        @Bindable var route = SettingsRoute.shared
        content.popover(isPresented: $route.quickPresented, arrowEdge: arrowEdge) {
            QuickSettingsPanel()
                .environment(state)
                .preferredColorScheme(state.colorScheme)
        }
    }
}
