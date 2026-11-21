import AppKit
import SwiftUI
import NextThoughtCore

/// The type scale of the live page: the sizes of the system's text styles, at the app's
/// text zoom.
enum LiveSessionTypography {
    static var display: Font { .zoomed(.largeTitle, design: .serif, weight: .medium) }
    static var section: Font { .zoomed(.title3, design: .serif, weight: .medium) }
    static var prompt: Font { .zoomed(.title2, weight: .medium) }
    static var body: Font { .zoomed(.body) }
    static var label: Font { .zoomed(.body, weight: .medium) }
    static var caption: Font { .zoomed(.caption) }
    static var overline: Font { .zoomed(.caption2, weight: .semibold) }
    static var code: Font { .zoomed(.body, design: .monospaced) }
}

/// 会中页 (focus mode): a top bar, the live transcript on the left, the suggested
/// automatic answers or a quiet notes column on the right, and one input row.
/// See 02-scenarios-and-flows.md, "每个功能放在哪".
struct LiveSessionView: View {
    @Environment(AppState.self) private var state
    @State private var showsTranscriptInCompactLayout = true
    @State private var highlightedLineID: UUID?

    var body: some View {
        let _ = (LiveRenderCount.page += 1)
        VStack(spacing: 0) {
            LiveTopBar()
            AutoEndBanner()
            GeometryReader { proxy in
                if proxy.size.width >= 760 {
                    HSplitView {
                        TranscriptColumn(jumpTo: $highlightedLineID)
                            .frame(minWidth: 320, idealWidth: proxy.size.width * 0.55, maxWidth: .infinity)
                        RightColumn(onOpenHighlight: { highlightedLineID = nil; highlightedLineID = $0.lineID; showsTranscriptInCompactLayout = true })
                            .frame(minWidth: 300, idealWidth: proxy.size.width * 0.45, maxWidth: .infinity)
                    }
                } else {
                    VStack(spacing: 0) {
                        HStack {
                            InkSegmented(items: [(value: true, title: "实时转写"), (value: false, title: "辅助")],
                                         selection: $showsTranscriptInCompactLayout,
                                         scalesWithDynamicType: true)
                                .fixedSize()
                                .accessibilityLabel("会中内容")
                            Spacer(minLength: 0)
                        }
                        .padding(.horizontal, Space.s4)
                        .frame(height: 44)
                        .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 0.5) }
                        if showsTranscriptInCompactLayout {
                            TranscriptColumn(jumpTo: $highlightedLineID)
                        } else {
                            RightColumn(onOpenHighlight: { highlightedLineID = nil; highlightedLineID = $0.lineID; showsTranscriptInCompactLayout = true })
                        }
                    }
                }
            }
            LiveComposer()
        }
        .background(Ink.paper)
    }
}

// MARK: - Top bar

private struct LiveTopBar: View {
    @Environment(AppState.self) private var state

    /// The scene this meeting runs in (frozen copy while it runs).
    private var scene: Workspace { state.preparedWorkspace ?? state.activeWorkspace }

    var body: some View {
        let _ = (LiveRenderCount.topBar += 1)
        GeometryReader { proxy in
            Group {
                if proxy.size.width < 1_080 {
                    compactControls(showsScene: proxy.size.width >= 900)
                } else {
                    expandedControls
                }
            }
            .padding(.horizontal, Space.s4)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
        .frame(height: 52)
        .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 0.5) }
    }

    private var sceneTag: some View {
        SceneTag(mode: scene.effectiveMode, name: scene.typeDisplayName)
            .fixedSize()
            .transition(.blurReplace)
            .animation(Motion.snappy, value: scene.effectiveMode)
    }

    private var expandedControls: some View {
        HStack(spacing: Space.s3) {
            trafficLightSpace
            RecordingTimer()
            archiveSaveStatus
            quietStatus
            sceneTag
            titleBlock.frame(maxWidth: 220, alignment: .leading).layoutPriority(1)
            if state.isOfflineMeeting { offlineMeetingLabel }
            Spacer(minLength: Space.s3)
            answerModePicker
            teleprompterButton
            moreMenu(includesMode: false)
            if state.isRunning { PauseResumeButton() }
            endSessionButton
        }
    }

    private func compactControls(showsScene: Bool) -> some View {
        HStack(spacing: Space.s2) {
            trafficLightSpace
            RecordingTimer()
            archiveSaveStatus
            quietStatus
            if showsScene { sceneTag }
            titleBlock.frame(maxWidth: 170, alignment: .leading).layoutPriority(1)
            Spacer(minLength: 0)
            moreMenu(includesMode: true)
            if state.isRunning { PauseResumeButton() }
            endSessionButton
        }
    }

    private var trafficLightSpace: some View {
        // Room for the window's traffic-light buttons (hidden title bar).
        Color.clear.frame(width: 62, height: 1)
    }

    private var titleBlock: some View {
        VStack(alignment: .leading, spacing: 1) {
            Text(verbatim: title)
                .font(Font.zoomed(.body, design: .serif).weight(.medium)).foregroundStyle(Ink.ink)
                .lineLimit(1).truncationMode(.middle)
            if state.activeWorkspace.name != title {
                Text(verbatim: state.activeWorkspace.name)
                    .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
                    .lineLimit(1).truncationMode(.middle)
            }
        }
    }

    private var answerModePicker: some View {
        InkSegmented(items: [(value: true, title: "自动回答"), (value: false, title: "只记录")],
                     selection: Binding(get: { state.automaticAnswers }, set: { state.setLiveMode(prompting: $0) }),
                     scalesWithDynamicType: true)
            .fixedSize()
            .overlay(alignment: .topTrailing) {
                if !state.answerEngineReady {
                    Circle().fill(Ink.caution).frame(width: 6, height: 6).offset(x: 2, y: -2)
                        .accessibilityHidden(true)
                }
            }
            .help(state.answerEngineReady
                  ? "自动回答：识别到问题后自动生成参考回答。只记录：不会自动回答；可输入问题或按 \(state.shortcutLabel(.forceAnswer)) 手动请求。"
                  : "还没有连接回答服务，听到问题也不会自动回答")
            .accessibilityHint(state.answerEngineReady ? "" : "还没有连接回答服务，听到问题也不会自动回答")
    }

    /// One quiet line beside the timer for what the island would otherwise be the only place to
    /// say: the coach's nudge, the "看镜头" cue, and that the island stepped aside for screen sharing.
    @ViewBuilder private var quietStatus: some View {
        let sharing = CueIslandController.shared.model.shareAvoiding
        let coach = state.coach
        let item: (text: String, symbol: String)? = {
            guard state.isRunning else { return nil }
            if sharing { return (String(localized: "正在共享这块屏幕，提词岛已收起"), "rectangle.on.rectangle") }
            guard coach.isEnabled else { return nil }
            if coach.shouldLookUp { return (String(localized: "看镜头"), "eye.fill") }
            if let nudge = coach.nudge { return (nudge.message, nudge.symbol) }
            return nil
        }()
        Group {
            if let item {
                HStack(spacing: 5) {
                    Image(systemName: item.symbol).font(.zoomed(size: 10, weight: .semibold))
                    Text(verbatim: item.text).font(LiveSessionTypography.caption).lineLimit(1)
                }
                .foregroundStyle(Ink.caution)
                .id(item.text)
                .transition(.blurReplace)
                .accessibilityElement(children: .combine)
                .accessibilityIdentifier("liveSession.quietStatus")
            }
        }
        .animation(Motion.snappy, value: item?.text)
    }

    private var offlineMeetingLabel: some View {
        Label("现场", systemImage: "person.3").font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
            .help("线下会议：只用麦克风")
    }

    private var teleprompterButton: some View {
        Button { TeleprompterController.shared.toggle() } label: { Image(systemName: "rectangle.topthird.inset.filled") }
            .buttonStyle(GlassIconButtonStyle(size: 28))
            .help("回答浮窗 · \(state.shortcutLabel(.toggleTeleprompter))")
    }

    private var endSessionButton: some View {
        Button("结束") {
            // Not for the click that only brought the app forward.
            guard !FirstMouse.isActivatingClick else { return }
            Task { await state.stopSession() }
        }
            .buttonStyle(SecondaryButtonStyle(scalesWithDynamicType: true))
            .disabled(state.isChangingSession)
            .help("结束并整理纪要 · \(state.shortcutLabel(.toggleSession))")
    }

    /// The one place for the secondary tools: translation, screen and phone status,
    /// answer settings, the live window. The segmented control already carries the
    /// mode in the wide bar, so only the compact bar repeats it here.
    private func moreMenu(includesMode: Bool) -> some View {
        Menu {
            if includesMode {
                Section("会中模式") {
                    Picker("会中模式", selection: Binding(get: { state.automaticAnswers }, set: { state.setLiveMode(prompting: $0) })) {
                        Text("自动回答").tag(true)
                        Text("只记录").tag(false)
                    }
                    .pickerStyle(.inline)
                    .labelsHidden()
                }
            }
            if !state.answerEngineReady {
                Button { state.openSettings(.answers) } label: {
                    Label("连接回答服务…", systemImage: "sparkles")
                }
                .help("还没有连接回答服务，听到问题也不会自动回答")
            }
            Section("提词") {
                Toggle("提词岛一直展开", isOn: Binding(get: { CueIslandController.shared.model.alwaysOpen },
                                                    set: { CueIslandController.shared.model.alwaysOpen = $0 }))
                Toggle("双屏演讲", isOn: Binding(get: { PresenterMode.shared.isEnabled },
                                              set: { PresenterMode.shared.isEnabled = $0 }))
                    .help(PresenterMode.shared.summary)
            }
            Section("回答") {
                Picker("回答语言", selection: Binding(get: { state.answerLanguage }, set: { state.answerLanguage = $0 })) {
                    Text("跟随问题").tag("auto")
                    Text("中文").tag("zh")
                    Text("English").tag("en")
                }
                Toggle("英文回答附中文对照", isOn: Binding(get: { state.answerGloss }, set: { state.answerGloss = $0 }))
            }
            Section("工具") {
                Toggle("实时纪要", isOn: Binding(get: { state.liveNotes.isEnabled }, set: { state.liveNotes.setEnabled($0) }))
                    .help("边开边整理议题、决定和待办，大约每 45 秒更新一次")
                Button(state.liveTranslation.isEnabled ? "关闭同声传译" : "开启同声传译") {
                    state.liveTranslation.setEnabled(!state.liveTranslation.isEnabled)
                }
                .disabled(!supportsLiveTranslation)
                .help(supportsLiveTranslation ? "自动识别我和对方说的中文或英文，并翻译成另一种语言" : "同声传译需要 macOS 15 或更新版本")
                if state.isOfflineMeeting && includesMode { offlineMeetingLabel }
                if state.insights.screenWatch { Label("屏幕感知已开启", systemImage: "display") }
                if state.companionEnabled { Label(state.companionConnectionText, systemImage: "iphone") }
                if includesMode {
                    Button { TeleprompterController.shared.toggle() } label: {
                        Label("回答浮窗", systemImage: "rectangle.topthird.inset.filled")
                    }
                }
                Picker("会中窗口", selection: Binding(get: { state.liveWindow.mode }, set: { state.liveWindow.set($0, byUser: true) })) {
                    ForEach(LiveWindowMode.allCases) { mode in
                        Text(LocalizedStringKey(mode.title)).tag(mode)
                    }
                }
                .pickerStyle(.inline)
                .labelsHidden()
            }
            Section("回答设置") { AnswerSettingsMenu() }
            Divider()
            Button { SettingsRoute.shared.quickPresented = true } label: {
                Label("设置…", systemImage: "gearshape")
            }
        } label: {
            Image(systemName: "ellipsis")
                .font(.zoomed(size: 14, weight: .semibold))
                .frame(width: 28, height: 28)
                .contentShape(Rectangle())
                .overlay(alignment: .topTrailing) {
                    // Something running in the background (translation, screen, phone) shows as a dot.
                    if state.liveTranslation.isEnabled || state.insights.screenWatch || state.companionEnabled || !state.answerEngineReady {
                        Circle().fill(state.answerEngineReady ? Ink.ink : Ink.caution).frame(width: 5, height: 5).offset(x: -3, y: 4)
                            .transition(.scale.combined(with: .opacity))
                    }
                }
                .animation(Motion.snappy, value: state.liveTranslation.isEnabled)
        }
        .menuStyle(.button)
        .buttonStyle(GlassIconButtonStyle(size: 28))
        .menuIndicator(.hidden)
        .fixedSize()
        .quickSettingsPopover()
        .accessibilityLabel("会中设置和工具")
        .help("会中设置和工具")
    }

    private var supportsLiveTranslation: Bool {
        if #available(macOS 15.0, *) { true } else { false }
    }

    private var title: String {
        if let planned = state.plannedMeeting?.title?.trimmingCharacters(in: .whitespacesAndNewlines), !planned.isEmpty { return planned }
        return state.activeWorkspace.name
    }

    @ViewBuilder
    private var archiveSaveStatus: some View {
        if let sessionID = state.activeArchiveID,
           let snapshot = SessionArchive.shared.persistenceBySession[sessionID] {
            HStack(spacing: Space.s1) {
                Image(systemName: snapshot.unqueuedWriteCount > 0 ? "exclamationmark.triangle.fill" : archiveSaveSymbol(snapshot.state))
                if snapshot.state != .saved {
                    Text(snapshot.state == .saving ? "保存中" : "保存受阻")
                        .font(LiveSessionTypography.caption)
                        .lineLimit(1)
                }
                if snapshot.unqueuedWriteCount > 0 {
                    Text(snapshot.dataLossAcknowledged
                         ? "已确认：有 \(snapshot.unqueuedWriteCount) 项内容未能入队，因此未保存。"
                         : "有 \(snapshot.unqueuedWriteCount) 项内容未能入队，因此未保存。")
                        .font(LiveSessionTypography.caption)
                        .lineLimit(2)
                }
                if snapshot.canAcknowledgeDataLoss {
                    Button("确认缺失") {
                        SessionArchive.shared.acknowledgeDataLossAndResume(sessionID)
                    }
                    .buttonStyle(.plain)
                    .help("确认这些写入未保存，并继续保存后续内容。")
                } else if snapshot.state == .blocked, snapshot.pendingEvents > 0 {
                    Button { SessionArchive.shared.retryPersistence(sessionID) } label: {
                        Image(systemName: "arrow.clockwise")
                    }
                    .buttonStyle(GlassIconButtonStyle(size: 24))
                    .help(snapshot.lastFailure ?? "重试保存")
                }
            }
            .foregroundStyle(snapshot.state == .blocked || snapshot.unqueuedWriteCount > 0 ? Ink.caution : Ink.secondary)
            .accessibilityElement(children: .combine)
            .accessibilityLabel(Text(verbatim: snapshot.unqueuedWriteCount > 0
                ? (snapshot.dataLossAcknowledged
                   ? "已确认：有 \(snapshot.unqueuedWriteCount) 项会议内容未保存。"
                   : "有 \(snapshot.unqueuedWriteCount) 项会议内容未保存，需要确认缺失并继续保存。")
                : (snapshot.lastFailure ?? (snapshot.state == .saved ? "会议归档已保存" : snapshot.state == .saving ? "会议归档正在保存" : "会议归档保存受阻"))))
        }
    }

    private func archiveSaveSymbol(_ state: SessionPersistenceSnapshot.State) -> String {
        switch state {
        case .saved: "checkmark.circle.fill"
        case .saving: "arrow.triangle.2.circlepath"
        case .blocked: "exclamationmark.triangle.fill"
        }
    }
}

/// Passive elapsed-time and listening-state display.
private struct RecordingTimer: View {
    @Environment(AppState.self) private var state

    var body: some View {
        TimelineView(.periodic(from: .now, by: 1)) { context in
            let elapsed = state.sessionStartedAt.map { Self.elapsed(from: $0, to: context.date) }
            let status = state.isRunning ? (state.isPaused ? String(localized: "已暂停") : String(localized: "正在聆听")) : String(localized: "未在聆听")
            HStack(spacing: 6) {
                let onAir = state.isRunning && !state.isPaused
                Image(systemName: "circle.fill")
                    .font(.zoomed(size: 7))
                    .foregroundStyle(onAir ? Ink.seal : Ink.tertiary)
                    .symbolEffect(.pulse, options: .repeating, isActive: onAir && !Motion.reduce)
                    .frame(width: 10, height: 10)
                    .accessibilityHidden(true)
                if let elapsed {
                    Text(verbatim: elapsed)
                        .font(Font.zoomed(.callout).weight(.medium).monospacedDigit())
                        .foregroundStyle(state.isPaused ? Ink.secondary : Ink.ink)
                        .contentTransition(.numericText())
                }
                Text(verbatim: status)
                    .font(LiveSessionTypography.caption)
                    .foregroundStyle(Ink.secondary)
            }
            .padding(.horizontal, 6)
            .frame(height: 28)
            .accessibilityElement(children: .ignore)
            .accessibilityLabel(Text(verbatim: "聆听状态：\(status)\(elapsed.map { "，计时 \($0)" } ?? "")"))
            .accessibilityIdentifier("liveSession.recordingStatus")
        }
    }

    static func elapsed(from start: Date, to now: Date) -> String {
        let seconds = max(0, Int(now.timeIntervalSince(start)))
        return seconds >= 3_600 ? String(format: "%d:%02d:%02d", seconds / 3_600, seconds / 60 % 60, seconds % 60)
            : String(format: "%02d:%02d", seconds / 60, seconds % 60)
    }
}

/// Always-visible pause and resume action beside the current listening status.
private struct PauseResumeButton: View {
    @Environment(AppState.self) private var state
    @Environment(\.liveSessionAcceptanceScenario) private var acceptanceScenario
    @FocusState private var isKeyboardFocused: Bool
    @State private var isHovering = false

    private var actionLabel: String { state.isPaused ? String(localized: "继续聆听") : String(localized: "暂停聆听") }
    private var title: LocalizedStringKey { state.isPaused ? "继续" : "暂停" }

    var body: some View {
        Button {
            guard !FirstMouse.isActivatingClick else { return }
            Task { await state.togglePause() }
        } label: {
            Label(title, systemImage: state.isPaused ? "play.fill" : "pause.fill")
        }
        .buttonStyle(SecondaryButtonStyle(scalesWithDynamicType: true))
        .focusable()
        .focused($isKeyboardFocused)
        .onHover { isHovering = $0 }
        .overlay {
            RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous)
                .strokeBorder(
                    isKeyboardFocused ? Ink.focus : (isHovering ? Ink.secondary : Color.clear),
                    lineWidth: isKeyboardFocused ? 2 : 1
                )
                .padding(isKeyboardFocused ? -3 : 0)
                .allowsHitTesting(false)
        }
        .disabled(state.isChangingSession)
        .accessibilityLabel(Text(verbatim: actionLabel))
        .accessibilityHint(Text(verbatim: "快捷键 \(state.shortcutLabel(.togglePause))"))
        .accessibilityIdentifier("liveSession.pauseResume")
        .help("\(actionLabel) · \(state.shortcutLabel(.togglePause))")
        .onAppear {
            if DemoMode.isEnabled { isKeyboardFocused = acceptanceScenario == .focus }
        }
    }
}

/// Phone companion: only while it is on. Green dot = connected, amber = waiting.
private struct PhoneStatusIcon: View {
    @Environment(AppState.self) private var state
    var body: some View {
        Image(systemName: "iphone")
            .font(.zoomed(size: 13))
            .foregroundStyle(Ink.secondary)
            .frame(width: 28, height: 28)
            .overlay(alignment: .topTrailing) {
                StatusDot(tone: state.companionConnected ? .ready : .caution, size: 6).offset(x: -6, y: 6)
            }
            .help(state.companionConnectionText)
    }
}

/// Screen awareness / slide following: only while it is on.
private struct ScreenStatusIcon: View {
    @Environment(AppState.self) private var state
    var body: some View {
        Image(systemName: "display")
            .font(.zoomed(size: 13))
            .foregroundStyle(Ink.secondary)
            .frame(width: 28, height: 28)
            .overlay(alignment: .topTrailing) {
                StatusDot(tone: state.insights.screenError == nil ? .ready : .caution, size: 6).offset(x: -5, y: 6)
            }
            .help(state.insights.screenError ?? String(localized: "屏幕感知已开启：跟随讲稿，相关的屏幕文字会用于回答"))
    }
}

/// The model label; opens "回答设置": model, fast mode, web search, answer length.
private struct AnswerSettingsMenu: View {
    @Environment(AppState.self) private var state

    var body: some View {
        Menu {
            Section("模型") {
                Picker("模型", selection: Binding(get: { state.currentAnswerModel }, set: { state.setLiveModel($0) })) {
                    ForEach(modelOptions, id: \.id) { option in
                        Text(verbatim: option.name).tag(option.id)
                    }
                }
                .pickerStyle(.inline)
                .labelsHidden()
            }
            if state.codexFastAvailable {
                Toggle(isOn: Binding(get: { state.codexFastMode }, set: { state.codexFastMode = $0; state.updateSettings() })) {
                    Text("快速模式 ⚡ · \(state.codexFastLabel)")
                }
                .toggleStyle(.automatic)
            }
            Section("联网") {
                Picker("联网", selection: Binding(get: { state.webSearchMode }, set: { state.webSearchMode = $0; state.saveWebSearchSettings() })) {
                    ForEach(WebSearchMode.allCases) { mode in
                        Text(verbatim: mode.displayName).tag(mode)
                    }
                }
                .pickerStyle(.inline)
                .labelsHidden()
            }
            Section("回答长度") {
                Picker("回答长度", selection: Binding(get: { state.liveAnswerStyle }, set: { state.setLiveAnswerStyle($0) })) {
                    Text("要点").tag(AnswerStyle.concise)
                    Text("详细").tag(AnswerStyle.detailed)
                }
                .pickerStyle(.inline)
                .labelsHidden()
            }
            Divider()
            Text("从下一个回答开始生效")
            Button("更多回答设置…") {
                UserDefaults.standard.set(SettingsGroup.answers.rawValue, forKey: "settingsGroup")
                state.select(.settings)
            }
        } label: {
            HStack(spacing: 3) {
                Text(verbatim: state.currentAnswerModel.isEmpty ? String(localized: "选择模型") : state.currentAnswerModel)
                    .font(LiveSessionTypography.label)
                if state.activeServiceTier == "fast" {
                    Image(systemName: "bolt.fill").font(.zoomed(size: 10))
                }
            }
            .foregroundStyle(Ink.secondary)
        }
        .menuStyle(.button)
        .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 8, scalesWithDynamicType: true))
        .menuIndicator(.hidden)
        .fixedSize()
        .help("回答设置")
    }

    private struct ModelOption { let id: String; let name: String }

    private var modelOptions: [ModelOption] {
        var options: [ModelOption]
        if state.answerBackend == .openAICompatible {
            options = state.apiModelChoices.map { ModelOption(id: $0, name: $0) }
        } else {
            options = state.codexModels.map { ModelOption(id: $0.id, name: $0.name) }
        }
        let current = state.currentAnswerModel
        if !current.isEmpty, !options.contains(where: { $0.id == current }) {
            options.insert(ModelOption(id: current, name: current), at: 0)
        }
        return options
    }
}

// MARK: - Transcript (left)

private struct TranscriptColumn: View {
    @Environment(AppState.self) private var state
    @Binding var jumpTo: UUID?
    @Environment(\.liveSessionAcceptanceScenario) private var acceptanceScenario
    @State private var follow = ScrollFollowState()

    var body: some View {
        let _ = (LiveRenderCount.transcript += 1)
        // 区分对方的每个人: people appear only once the voices have been told apart.
        let layout = state.speakers.isActive && state.entries.contains(where: { $0.voice != nil })
            ? SpeakerLayout(lines: state.liveSpeakerLines, voiceNames: state.voiceNames(for: nil), meVoices: state.speakers.meVoices) : nil
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                Text("实时对话").overline()
                Spacer()
                Button {
                    if follow.isFollowing { follow.userScrolledAway() }
                    else { follow.returnToLatest() }
                } label: {
                    Image(systemName: follow.isFollowing ? "arrow.down.to.line" : "pause")
                }
                .buttonStyle(GlassIconButtonStyle(size: 24))
                .help(follow.isFollowing ? "自动跟随最新内容（点按暂停）" : "已暂停跟随（点按回到现场）")
                .accessibilityLabel(follow.isFollowing ? "暂停跟随最新内容" : "回到现场并恢复跟随")
            }
            .padding(.horizontal, Space.s6)
            .padding(.top, Space.s4)
            if state.insights.screenWatch && state.typeDefaults.followsSlides {
                InsightsPanel()
                    .frame(height: 240)
                    .padding(.horizontal, Space.s4)
                    .padding(.top, Space.s3)
            }
            if let layout, layout.isShown {
                SpeakerStrip(layout: layout,
                             rename: { state.renameVoice($0, to: $1, in: nil) },
                             merge: { state.mergeVoices($0, into: $1, in: nil) },
                             mark: { state.markVoice($0, isMe: $1, in: nil) })
                    .padding(.horizontal, Space.s6)
                    .padding(.top, Space.s2)
            }
            ScrollViewReader { proxy in
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: Space.s4) {
                        if state.entries.isEmpty && state.partialTranscript.isEmpty && state.microphonePartialTranscript.isEmpty {
                            Text(state.isOfflineMeeting ? "开始说话后，房间里的声音会实时出现在这里。" : "对方和你说的话会实时出现在这里。")
                                .font(LiveSessionTypography.body).foregroundStyle(Ink.secondary)
                                .padding(.top, Space.s2)
                        }
                        ForEach(state.entries) { entry in
                            TranscriptLine(entry: entry, layout: layout).id(entry.id)
                        }
                        PartialLines()
                        Color.clear.frame(height: 1).id("bottom")
                    }
                    .padding(.horizontal, Space.s6)
                    .padding(.vertical, Space.s4)
                    .background(ScrollFollowObserver(
                        onScrollAway: { follow.userScrolledAway() },
                        onReachedLatest: { follow.reachedLatest() },
                        onTextSelection: { follow.userSelectedText() }
                    ))
                }
                // Keep the footer in the scroll view's measured safe area. SwiftUI
                // reserves the status strip (and any candidate rows) at their real
                // height, so the final transcript and translation can scroll above it.
                .safeAreaInset(edge: .bottom, spacing: 0) {
                    VStack(spacing: 0) {
                        if follow.hasNewContent {
                            Button {
                                withAnimation(Motion.expand) {
                                    follow.returnToLatest()
                                    proxy.scrollTo("bottom", anchor: .bottom)
                                }
                            } label: {
                                Label("新增 \(follow.pendingItemCount) 句 · 回到现场", systemImage: "arrow.down.to.line.compact")
                            }
                            .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 10, scalesWithDynamicType: true))
                            .environment(\.liveSessionAcceptanceButtonInteraction,
                                         DemoMode.isEnabled && acceptanceScenario == .selectionFocus ? .focused : .normal)
                            .padding(.vertical, Space.s2)
                        }
                        CandidateStrip()
                        TranscriptStatus()
                    }
                    .background(Ink.paper)
                }
                .frame(maxHeight: .infinity)
                .onChange(of: state.entries.count) { oldCount, newCount in
                    if follow.isFollowing {
                        proxy.scrollTo("bottom", anchor: .bottom)
                        // A lazy stack only knows the height of a new row after it has laid it
                        // out: the first jump can stop short of it. Once more, a frame later.
                        DispatchQueue.main.async { if follow.isFollowing { proxy.scrollTo("bottom", anchor: .bottom) } }
                    }
                    else { follow.receivedNewContent(count: newCount - oldCount) }
                }
                // Final translations arrive after the transcript row was inserted;
                // follow again when they expand that row, unless the user paused follow.
                .onChange(of: state.entries.last?.translatedText) { _, _ in
                    if follow.isFollowing { proxy.scrollTo("bottom", anchor: .bottom) }
                }
                .onChange(of: state.partialTranscript.count / 8) { _, _ in if follow.isFollowing { proxy.scrollTo("bottom", anchor: .bottom) } }
                .onChange(of: state.microphonePartialTranscript.count / 8) { _, _ in if follow.isFollowing { proxy.scrollTo("bottom", anchor: .bottom) } }
                .onChange(of: state.liveTranslation.systemPartialTranslation) { _, _ in
                    if follow.isFollowing { proxy.scrollTo("bottom", anchor: .bottom) }
                }
                .onChange(of: state.liveTranslation.microphonePartialTranslation) { _, _ in
                    if follow.isFollowing { proxy.scrollTo("bottom", anchor: .bottom) }
                }
                .onChange(of: follow.isFollowing) { _, value in if value { proxy.scrollTo("bottom", anchor: .bottom) } }
                .onChange(of: jumpTo) { _, line in
                    if let line { follow.userScrolledAway(); withAnimation(Motion.expand) { proxy.scrollTo(line, anchor: .center) } }
                }
            }
            .frame(maxHeight: .infinity)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        .onAppear {
            guard DemoMode.isEnabled else { return }
            switch acceptanceScenario {
            case .readingHistory, .streamingHistory:
                follow.userScrolledAway()
                follow.receivedNewContent(count: 6)
            case .selection, .selectionFocus:
                follow.userSelectedText()
                follow.receivedNewContent(count: 2)
            default:
                break
            }
        }
    }
}

/// Speaker label column: 对方 / 我 / 现场.
private struct SpeakerLabel: View {
    let speaker: SessionSpeaker
    /// Keeps the column (and the baseline) but shows nothing: the speaker tag above the text names the person.
    var hidden = false
    var body: some View {
        Text(speaker.displayName)
            .font(Font.zoomed(.caption).weight(.medium))
            .foregroundStyle(speaker == .me ? Ink.ink : Ink.secondary)
            .frame(width: 40, alignment: .leading)
            .opacity(hidden ? 0 : 1)
            .accessibilityHidden(hidden)
    }
}

private struct TranscriptLine: View {
    @Environment(AppState.self) private var state
    let entry: TranscriptEntry
    /// Set once people are told apart (区分对方的每个人).
    var layout: SpeakerLayout? = nil
    @State private var hovering = false
    @FocusState private var answerButtonFocused: Bool

    var body: some View {
        let _ = (LiveRenderCount.line += 1)
        if entry.suspectedEcho == true {
            HStack(alignment: .firstTextBaseline, spacing: Space.s3) {
                SpeakerLabel(speaker: entry.speaker)
                Text(verbatim: entry.text).font(LiveSessionTypography.caption).foregroundStyle(Ink.tertiary).lineLimit(1)
                    .strikethrough(color: Ink.tertiary)
                Button("是我说的") { state.markNotEcho(entry.id) }
                    .buttonStyle(.tapText).font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
            }
            .help("这句和对方刚说的话很像，当作扬声器回声忽略了，不参与回答。如果是你说的，点“是我说的”。")
        } else {
            let isQuestion = !entry.isMe && state.isQuestionText(entry.text)
            HStack(alignment: .firstTextBaseline, spacing: Space.s3) {
                VStack(alignment: .leading, spacing: Space.s1) {
                    SpeakerLabel(speaker: entry.speaker, hidden: layout?.hidesRoleLabel(for: entry.speaker) == true)
                    if isQuestion { QuestionMark() }
                }
                VStack(alignment: .leading, spacing: Space.s1) {
                    if let layout, layout.hidesRoleLabel(for: entry.speaker), layout.starts.contains(entry.id) {
                        SpeakerTag(layout: layout, lineID: entry.id, speaker: entry.speaker)
                    }
                    Text(verbatim: entry.text).font(LiveSessionTypography.body).foregroundStyle(Ink.ink)
                    if let translation = entry.translatedText, !translation.isEmpty {
                        Text("译文：\(translation)").font(LiveSessionTypography.caption).foregroundStyle(Ink.ai).lineSpacing(4)
                    }
                }
                .lineSpacing(6)
                .textSelection(.enabled)
                .frame(maxWidth: .infinity, alignment: .leading)
                Spacer(minLength: 0)
                if entry.speaker != .me {
                    Button { state.answerEntry(entry) } label: { Image(systemName: "bolt") }
                        .buttonStyle(GlassIconButtonStyle(size: 24))
                        .opacity(hovering || answerButtonFocused ? 1 : 0)
                        .allowsHitTesting(hovering || answerButtonFocused)
                        .focused($answerButtonFocused)
                        .accessibilityLabel("回答这句")
                        .help("回答这句")
                } else {
                    Color.clear.frame(width: 24, height: 24).accessibilityHidden(true)
                }
                if let sessionID = state.activeArchiveID {
                    Button {
                        state.toggleHighlight(lineID: entry.id, text: entry.text,
                                              offsetSeconds: Int(max(0, entry.date.timeIntervalSince(state.sessionStartedAt ?? entry.date))), sessionID: sessionID)
                    } label: { Image(systemName: state.archive.isHighlighted(entry.id, in: sessionID) ? "star.fill" : "star") }
                        .buttonStyle(GlassIconButtonStyle(size: 28))
                        .accessibilityLabel("标记或取消重点")
                        .help("标记这句话，稍后在笔记里查看")
                }
            }
            .contentShape(Rectangle())
            .onHover { hovering = $0 }
            .accessibilityAction(named: Text("回答这句")) { state.answerEntry(entry) }
            .contextMenu {
                Button("回答这句") { state.answerEntry(entry) }
                if let sessionID = state.activeArchiveID {
                    Button("标记或取消重点") {
                        state.toggleHighlight(lineID: entry.id, text: entry.text,
                                              offsetSeconds: Int(max(0, entry.date.timeIntervalSince(state.sessionStartedAt ?? entry.date))), sessionID: sessionID)
                    }
                }
                // The other side or the room: which of them said it, or was it me.
                if entry.speaker != .me, state.speakers.isActive {
                    Divider()
                    LineSpeakerMenu(layout: layout) { state.assignLine(entry.id, to: $0, in: nil) }
                }
            }
            .animation(Motion.hover, value: hovering)
        }
    }
}

/// The detector saw a question: a small capsule in the scene's pigment.
private struct QuestionMark: View {
    @Environment(AppState.self) private var state
    var body: some View {
        let tint = (state.preparedWorkspace ?? state.activeWorkspace).effectiveMode.theme.tint
        Text("问题")
            .font(Font.zoomed(.caption2).weight(.medium))
            .foregroundStyle(tint)
            .padding(.horizontal, 7).padding(.vertical, 2)
            .background(tint.opacity(0.13), in: Capsule())
            .accessibilityLabel("问题")
            .transition(.scale(scale: 0.8).combined(with: .opacity))
    }
}

/// The two utterances still being recognized.
private struct PartialLines: View {
    @Environment(AppState.self) private var state
    var body: some View {
        let _ = (LiveRenderCount.partial += 1)
        if !state.partialTranscript.isEmpty {
            line(state.partialTranscript, translation: state.liveTranslation.systemPartialTranslation, speaker: .remote)
                .liveSessionAcceptanceFrame("partial-transcript")
        }
        if !state.microphonePartialTranscript.isEmpty {
            line(state.microphonePartialTranscript, translation: state.liveTranslation.microphonePartialTranslation,
                 speaker: state.isOfflineMeeting ? .room : .me)
        }
    }

    private func line(_ text: String, translation: String, speaker: SessionSpeaker) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: Space.s3) {
            SpeakerLabel(speaker: speaker)
            VStack(alignment: .leading, spacing: Space.s1) {
                Text(verbatim: text).font(LiveSessionTypography.body).foregroundStyle(Ink.secondary).lineSpacing(6)
                if !translation.isEmpty {
                    Text("译文：\(translation)").font(LiveSessionTypography.caption).foregroundStyle(Ink.ai).lineSpacing(4)
                }
            }
            Spacer(minLength: 0)
        }
    }
}

/// Questions the detector is following or was unsure about, one click to answer.
private struct CandidateStrip: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let live = state.liveCandidate.flatMap { candidate -> QuestionCandidate? in
            candidate.state == .triggered || candidate.assessment.intent == .statement ? nil : candidate
        }
        let suggested = Array(state.suggestedQuestions.suffix(2).reversed())
        let tint = (state.preparedWorkspace ?? state.activeWorkspace).effectiveMode.theme.tint
        Group {
            if live != nil || !suggested.isEmpty {
                VStack(alignment: .leading, spacing: 0) {
                    if let live { row(live, dismissible: false, tint: tint) }
                    ForEach(suggested) { candidate in row(candidate, dismissible: true, tint: tint) }
                }
                .padding(.horizontal, Space.s6)
                .padding(.vertical, Space.s1)
                .overlay(alignment: .top) { Rectangle().fill(Ink.hairline).frame(height: 0.5) }
                .transition(.move(edge: .bottom).combined(with: .opacity))
            }
        }
        .animation(Motion.expand, value: suggested.count + (live == nil ? 0 : 1))
    }

    private func row(_ candidate: QuestionCandidate, dismissible: Bool, tint: Color) -> some View {
        HStack(spacing: Space.s2) {
            Circle().fill(tint).frame(width: 6, height: 6).accessibilityHidden(true)
            Text("可能的问题").font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
            Text(verbatim: candidate.fullText).font(LiveSessionTypography.caption).foregroundStyle(Ink.ink).lineLimit(1)
            Spacer(minLength: Space.s2)
            Button("回答这句") { state.answerCandidate(candidate) }
                .buttonStyle(GhostButtonStyle(height: 26, horizontalPadding: 8, scalesWithDynamicType: true))
            if dismissible {
                Button { state.dismissCandidate(candidate) } label: { Image(systemName: "xmark") }
                    .buttonStyle(GlassIconButtonStyle(size: 22))
            }
        }
        .frame(minHeight: 32)
    }
}

/// "本机识别 · 1 句回声已忽略".
private struct TranscriptStatus: View {
    @Environment(AppState.self) private var state

    var body: some View {
        HStack(spacing: Space.s1) {
            Text(state.isOfflineMeeting ? "现场麦克风" : state.audioSourceTitle(for: state.selectedTargetID))
            Text(engine)
            if let id = state.activeArchiveID, let error = state.archive.storageErrors[id] {
                Label("写入失败 · 部分记录未保存", systemImage: "exclamationmark.triangle").foregroundStyle(Ink.caution).help(error)
            }
            if state.usesMicrophone && !state.isOfflineMeeting { Text("· 麦克风") }
            if state.echoDroppedCount > 0 && !state.isOfflineMeeting {
                Text("· \(state.echoDroppedCount) 句回声已忽略")
            }
            if state.droppedAudioSegments > 0 {
                Text("· 遗漏 \(state.droppedAudioSegments) 段").foregroundStyle(Ink.caution)
            }
            if state.liveTranslation.isEnabled, !state.liveTranslation.status.isEmpty {
                Text("· \(state.liveTranslation.status)")
            }
            if !state.microphoneEnabled && !state.isOfflineMeeting {
                Text("· 麦克风未开启，不会记下你说的话").foregroundStyle(Ink.caution)
            } else if state.microphoneAccess == .denied {
                Button("· 麦克风未授权，打开系统设置") { state.openMicrophoneSettings() }
                    .buttonStyle(.tapText).foregroundStyle(Ink.caution)
            }
            Spacer(minLength: 0)
            Image(systemName: "lock")
                .help(state.privacySummary)
        }
        .font(LiveSessionTypography.caption)
        .foregroundStyle(Ink.secondary)
        .lineLimit(1)
        .padding(.horizontal, Space.s6)
        .frame(height: 36)
        .help(state.transcriptionStatus)
    }

    private var engine: LocalizedStringKey {
        switch state.transcriptionBackend {
        case "cloud": "云端识别"
        case "apple": "Apple 识别"
        default: "本机识别"
        }
    }
}

// MARK: - Right column

private struct RightColumn: View {
    @Environment(AppState.self) private var state
    let onOpenHighlight: (SessionHighlight) -> Void
    private enum Tab: Hashable { case myNotes, answers, liveNotes }
    @State private var tab: Tab = .myNotes
    @State private var highlights: [SessionHighlight] = []
    private var showsAnswer: Bool { tab == .answers }

    var body: some View {
        let _ = (LiveRenderCount.rightColumn += 1)
        VStack(spacing: 0) {
            HStack {
                InkSegmented(items: [(value: Tab.liveNotes, title: "实时纪要"), (value: Tab.answers, title: "问答"), (value: Tab.myNotes, title: "我的笔记")], selection: $tab)
                Spacer(minLength: Space.s2)
                if state.isAnswering && !showsAnswer {
                    Label("回答生成中", systemImage: "sparkles").font(LiveSessionTypography.caption).foregroundStyle(Ink.ai)
                        .symbolEffect(.pulse, options: .repeating, isActive: !Motion.reduce)
                        .transition(.blurReplace)
                }
            }
            .padding(.horizontal, Space.s6).padding(.top, Space.s4)
            .animation(Motion.snappy, value: state.isAnswering)
            Group {
                if tab == .liveNotes {
                    LiveNotesColumn()
                } else if showsAnswer {
                    AnswerColumn()
                } else if let id = state.activeArchiveID {
                    MeetingUserNotes(sessionID: id, highlights: highlights, onOpenHighlight: onOpenHighlight)
                        .padding(Space.s6)
                } else {
                    VStack(alignment: .leading, spacing: 0) {
                        Text("此场未保存记录，个人笔记需要开启记录保存。").font(LiveSessionTypography.body).foregroundStyle(Ink.caution).padding(Space.s6)
                        Spacer()
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
            .transition(.opacity)
            .animation(Motion.expand, value: tab)
        }
        // What the meeting was started for is what shows first.
        .onAppear {
            if AppEnvironment.values["NEXTTHOUGHT_DEMO_LIVE_NOTES"] == "1" { tab = .liveNotes }
            else { tab = state.automaticAnswers ? .answers : (state.liveNotes.isEnabled ? .liveNotes : .myNotes) }
        }
        .onChange(of: state.currentAnswerID) { _, _ in if !state.currentAnswerAutomatic { tab = .answers } }
        .task(id: state.archive.notesRevision) {
            if let id = state.activeArchiveID, let content = try? await state.archive.userContent(id) { highlights = content.highlights }
        }
    }
}

/// 实时纪要: what the meeting is about so far, its topics, what was decided and who is
/// to do what; rewritten as the meeting goes on.
private struct LiveNotesColumn: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let engine = state.liveNotes
        let notes = engine.notes
        let tint = (state.preparedWorkspace ?? state.activeWorkspace).effectiveMode.theme.tint
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: Space.s2) {
                if engine.status == .updating { ProgressView().controlSize(.mini) }
                Text(verbatim: statusText(engine)).font(LiveSessionTypography.caption)
                    .foregroundStyle(engine.status == .failed || engine.status == .noEngine ? Ink.caution : Ink.secondary)
                    .contentTransition(.opacity)
                Spacer(minLength: Space.s2)
                if engine.isEnabled {
                    Button("现在更新") { engine.updateNow(state) }
                        .buttonStyle(GhostButtonStyle(height: 24, horizontalPadding: 8, scalesWithDynamicType: true))
                        .disabled(engine.status == .updating || !state.answerEngineReady)
                } else {
                    Button("开启实时纪要") { engine.setEnabled(true) }
                        .buttonStyle(GhostButtonStyle(height: 24, horizontalPadding: 8, scalesWithDynamicType: true))
                }
            }
            .padding(.horizontal, Space.s6).padding(.top, Space.s3)
            ScrollView {
                VStack(alignment: .leading, spacing: Space.s4) {
                    if notes.isEmpty {
                        Text(engine.isEnabled ? "说上几句之后，这里会出现这场会的要点：在谈什么、定了什么、谁要做什么。"
                             : "这场没有开实时纪要。结束后仍会整理完整纪要；想边开边看要点，点右上角开启。")
                            .font(LiveSessionTypography.body).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
                    } else {
                        Text(verbatim: notes.summary).font(LiveSessionTypography.body).foregroundStyle(Ink.ink).lineSpacing(5)
                            .fixedSize(horizontal: false, vertical: true)
                            .padding(Space.s4).frame(maxWidth: .infinity, alignment: .leading)
                            .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
                        ForEach(notes.topics) { topic in
                            VStack(alignment: .leading, spacing: Space.s2) {
                                Text(verbatim: topic.title).font(LiveSessionTypography.label).foregroundStyle(Ink.ink)
                                ForEach(topic.points, id: \.self) { point in bullet(point, fresh: engine.fresh.contains(point), tint: tint) }
                            }
                        }
                        if !notes.decisions.isEmpty {
                            VStack(alignment: .leading, spacing: Space.s2) {
                                Text("定下来的").overline()
                                ForEach(notes.decisions, id: \.self) { item in bullet(item, fresh: engine.fresh.contains(item), tint: tint, symbol: "checkmark") }
                            }
                        }
                        if !notes.todos.isEmpty {
                            VStack(alignment: .leading, spacing: Space.s2) {
                                Text("要做的").overline()
                                ForEach(notes.todos) { todo in
                                    bullet(todo.owner.map { todo.text + "（" + $0 + "）" } ?? todo.text, fresh: engine.fresh.contains(todo.text), tint: tint, symbol: "square")
                                }
                            }
                        }
                    }
                }
                .padding(Space.s6)
                .frame(maxWidth: .infinity, alignment: .leading)
                .textSelection(.enabled)
                .animation(Motion.expand, value: notes)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .accessibilityIdentifier("liveSession.liveNotes")
    }

    private func statusText(_ engine: LiveNotesEngine) -> String {
        switch engine.status {
        case .off: return String(localized: "实时纪要没有开")
        case .updating: return String(localized: "正在更新…")
        case .failed: return String(localized: "这次没更新成功，稍后会再试")
        case .noEngine: return String(localized: "还没有连接回答服务，写不了实时纪要")
        case .waiting:
            guard let at = engine.updatedAt else { return String(localized: "有了内容就开始整理") }
            return String(localized: "更新于 \(at.formatted(date: .omitted, time: .shortened)) · 约每 45 秒一次")
        }
    }

    /// One line of the notes; what the last update added carries the scene's colour.
    private func bullet(_ text: String, fresh: Bool, tint: Color, symbol: String? = nil) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
            if let symbol {
                Image(systemName: symbol).font(.zoomed(size: 9, weight: .semibold)).foregroundStyle(fresh ? tint : Ink.tertiary).frame(width: 12)
            } else {
                Circle().fill(fresh ? tint : Ink.tertiary).frame(width: 4, height: 4).frame(width: 12).offset(y: -3)
            }
            Text(verbatim: text).font(LiveSessionTypography.body).foregroundStyle(Ink.ink).lineSpacing(4)
                .fixedSize(horizontal: false, vertical: true)
        }
    }
}

private struct NotesOnlyColumn: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            Text("只记录").overline()
            Text("不打扰，安静地记。结束后自动整理纪要。").font(LiveSessionTypography.body)
            Text(notesHint)
                .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
                .fixedSize(horizontal: false, vertical: true)
            Text("需要帮忙时按 \(state.shortcutLabel(.forceAnswer))，或在下方输入问题。")
                .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
            Spacer()
        }
        .padding(Space.s6)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private var notesHint: String {
        let template = state.notesTemplate(for: state.activeWorkspace)
        let sections = template.sections.map(\.title).joined(separator: " · ")
        return String(localized: "纪要模板：\(template.name) · \(sections)")
    }
}

/// 建议回答: header with history, the web status when it happens, the question as a
/// quiet caption, then one raised sheet holding the answer, its rewrites (capsule
/// chips) and the line saying what it was based on.
private struct AnswerColumn: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let tint = (state.preparedWorkspace ?? state.activeWorkspace).effectiveMode.theme.tint
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack(spacing: Space.s2) {
                HStack(spacing: Space.s1) {
                    Image(systemName: "sparkle").font(.zoomed(size: 11, weight: .medium)).foregroundStyle(tint)
                    Group {
                        if state.isAnswering && state.historyCursor == nil {
                            Text("正在回答").transition(.blurReplace)
                        } else {
                            Text("建议回答").transition(.blurReplace)
                        }
                    }
                    .overline()
                }
                .animation(Motion.snappy, value: state.isAnswering)
                Spacer()
                if let position = state.historyPositionText {
                    Button { state.showPreviousAnswer() } label: { Image(systemName: "chevron.left") }
                        .buttonStyle(GlassIconButtonStyle(size: 22)).help("上一条 · \(state.shortcutLabel(.previousAnswer))")
                    Text(verbatim: position).font(Font.zoomed(.caption).monospacedDigit()).foregroundStyle(Ink.secondary)
                        .contentTransition(.numericText())
                    Button { state.showNextAnswer() } label: { Image(systemName: "chevron.right") }
                        .buttonStyle(GlassIconButtonStyle(size: 22)).help("下一条 · \(state.shortcutLabel(.nextAnswer))")
                }
            }
            WebStatusLine()
            if !state.isAnswering, let id = state.displayedRecordID,
               let record = state.answerHistory.first(where: { $0.id == id }), let completion = record.completion, completion != .complete {
                Label("未完成的回答片段，请核实后使用", systemImage: "exclamationmark.circle")
                    .font(LiveSessionTypography.caption).foregroundStyle(Ink.caution)
            }
            let question = state.displayedQuestion
            if !question.isEmpty {
                Text(verbatim: AnswerRefinement.originalQuestion(of: question))
                    .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary).lineLimit(2)
                    .textSelection(.enabled)
                    .id(question)
                    .transition(.blurReplace)
            }
            VStack(alignment: .leading, spacing: 0) {
                AnswerText()
                    .padding(.horizontal, Space.s6).padding(.top, Space.s6).padding(.bottom, Space.s3)
                WebSources().padding(.horizontal, Space.s6).padding(.bottom, Space.s2)
                Rectangle().fill(Ink.hairline).frame(height: 0.5)
                VStack(alignment: .leading, spacing: Space.s2) {
                    RefinementRow()
                    EvidenceLine()
                }
                .padding(.horizontal, Space.s4).padding(.vertical, Space.s3)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
            .background {
                RoundedRectangle(cornerRadius: Space.radiusLarge, style: .continuous)
                    .fill(Ink.surface)
                    .shadow(color: Ink.contactShadow, radius: 1, x: 0, y: 1)
                    .shadow(color: Ink.ambientShadow, radius: 20, x: 0, y: 10)
            }
            .overlay(RoundedRectangle(cornerRadius: Space.radiusLarge, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 0.5))
        }
        .animation(Motion.expand, value: state.displayedQuestion.isEmpty)
        .padding(.horizontal, Space.s6).padding(.top, Space.s4).padding(.bottom, Space.s4)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }
}

/// The answer text, at "prompt" size for a glance. Only this view redraws per delta.
private struct AnswerText: View {
    @Environment(AppState.self) private var state
    @State private var follow = ScrollFollowState()

    var body: some View {
        ZStack(alignment: .bottomTrailing) {
            ScrollViewReader { proxy in
                ScrollView {
                    VStack(alignment: .leading, spacing: 0) {
                        let text = state.displayedAnswer
                        if text.isEmpty {
                            Text(state.isAnswering ? "正在生成…" : "对方提问时，这里会自动给出建议回答。也可以在下方输入问题，或按 \(state.shortcutLabel(.forceAnswer)) 立即回答。")
                                .font(LiveSessionTypography.body).foregroundStyle(Ink.secondary)
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .transition(.blurReplace)
                        } else {
                            MarkdownText(text: text, size: 18, lineSpacing: 8, scalesWithDynamicType: true).equatable()
                                .transition(.blurReplace)
                        }
                        Color.clear.frame(height: 1).id("answer-end")
                    }
                    .animation(Motion.morph, value: state.displayedAnswer.isEmpty)
                    .background(ScrollFollowObserver(
                        onScrollAway: { follow.userScrolledAway() },
                        onReachedLatest: { follow.reachedLatest() },
                        onTextSelection: { follow.userSelectedText() }
                    ))
                }
                .frame(maxHeight: .infinity)
                .onChange(of: state.answerDraft) { oldText, newText in
                    guard state.isAnswering, state.historyCursor == nil, oldText != newText else { return }
                    if follow.isFollowing {
                        if oldText.count / 40 != newText.count / 40 {
                            proxy.scrollTo("answer-end", anchor: .bottom)
                        }
                    } else {
                        follow.receivedNewContent()
                    }
                }
                .onChange(of: follow.isFollowing) { _, value in
                    if value { proxy.scrollTo("answer-end", anchor: .bottom) }
                }
            }

            if !follow.isFollowing {
                Button {
                    withAnimation(Motion.expand) { follow.returnToLatest() }
                } label: {
                    Label(follow.hasNewContent ? "有新回答 · 回到最新" : "回到最新", systemImage: "arrow.down.to.line.compact")
                }
                .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 10, scalesWithDynamicType: true))
                .padding(Space.s2)
            }
        }
        .frame(maxHeight: .infinity)
    }
}

/// Only when a web lookup exists: "正在联网核查…" → "联网版 · 3 个来源 · 看原回答".
private struct WebStatusLine: View {
    @Environment(AppState.self) private var state

    var body: some View {
        if let lookup = state.displayedWebLookup {
            HStack(spacing: Space.s2) {
                Image(systemName: "globe").foregroundStyle(Ink.secondary)
                switch lookup.status {
                case .searching:
                    Text("正在联网核查…").foregroundStyle(Ink.secondary)
                    ProgressView().controlSize(.mini)
                case .done:
                    Text(state.showsWebAnswer ? "联网版 · \(lookup.sources.count) 个来源" : "联网版已就绪 · \(lookup.sources.count) 个来源")
                        .foregroundStyle(Ink.secondary)
                    Button(state.showsWebAnswer ? "看原回答" : "看联网版") { state.toggleWebAnswer() }
                        .buttonStyle(GhostButtonStyle(height: 22, horizontalPadding: 6, scalesWithDynamicType: true))
                case let .failed(message):
                    Text("联网没有完成").foregroundStyle(Ink.caution).help(message)
                    Button("重试") { state.lookUpDisplayedAnswer() }
                        .buttonStyle(GhostButtonStyle(height: 22, horizontalPadding: 6, scalesWithDynamicType: true))
                }
                Spacer(minLength: 0)
            }
            .font(LiveSessionTypography.caption)
        }
    }
}

/// The sources behind the web version, numbered like the [1] marks in the text.
private struct WebSources: View {
    @Environment(AppState.self) private var state

    var body: some View {
        if state.showsWebAnswer, let sources = state.displayedWebLookup?.sources, !sources.isEmpty {
            VStack(alignment: .leading, spacing: 2) {
                ForEach(Array(sources.prefix(6).enumerated()), id: \.offset) { index, source in
                    if let url = URL(string: source.url) {
                        Link(destination: url) {
                            Text(verbatim: "[\(index + 1)] \(source.title)").lineLimit(1)
                        }
                        .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
                    }
                }
            }
        }
    }
}

/// 更简短 · 举例 · 英文版, the rest in "…".
private struct RefinementRow: View {
    @Environment(AppState.self) private var state
    @Environment(\.liveSessionAcceptanceScenario) private var acceptanceScenario
    private static let shown: [AnswerRefinement] = [.shorter, .example, .english]

    var body: some View {
        let hasAnswer = !state.displayedAnswer.isEmpty
        HStack(spacing: Space.s1) {
            ForEach(Self.shown) { item in
                Button { state.refine(item) } label: { Text(verbatim: item.displayName) }
                    .buttonStyle(LiveChipStyle())
                    .disabled(!hasAnswer || state.isAnswering)
                    .help(item == .shorter ? "把回答改短 · \(state.shortcutLabel(.refineShorter))" : "")
            }
            AnswerMoreMenu()
            Spacer(minLength: 0)
            if state.isAnswering {
                Button("停止生成") { state.cancelAnswer() }
                    .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 8, scalesWithDynamicType: true))
                    .environment(\.liveSessionAcceptanceButtonInteraction,
                                 DemoMode.isEnabled && acceptanceScenario == .busyFocus ? .focused : .normal)
                    .help("取消回答 · \(state.shortcutLabel(.cancelAnswer))")
            }
        }
    }
}

/// Capsule chip for a rewrite: hairline outline, hover wash, a quick press.
private struct LiveChipStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View { Chip(configuration: configuration) }
    private struct Chip: View {
        let configuration: ButtonStyleConfiguration
        @Environment(\.isEnabled) private var isEnabled
        @State private var hovering = false
        var body: some View {
            configuration.label
                .font(LiveSessionTypography.label)
                .foregroundStyle(isEnabled ? Ink.ink : Ink.tertiary.opacity(0.65))
                .padding(.horizontal, 12)
                .frame(minHeight: 28)
                .background((isEnabled && (hovering || configuration.isPressed)) ? Ink.hover : .clear, in: Capsule())
                .overlay(Capsule().strokeBorder(Ink.hairline, lineWidth: 0.5))
                .contentShape(Capsule())
                .scaleEffect(configuration.isPressed && isEnabled ? 0.96 : 1)
                .onHover { hovering = $0 }
                .animation(Motion.hover, value: hovering)
                .animation(Motion.snappy, value: configuration.isPressed)
        }
    }
}

/// "…": the other rewrites, regenerate, look it up on the web, copy.
struct AnswerMoreMenu: View {
    @Environment(AppState.self) private var state
    private static let others: [AnswerRefinement] = [.detailed, .star, .chinese, .clarify, .followUps]
    var includesWorkbench = false

    var body: some View {
        let hasAnswer = !state.displayedAnswer.isEmpty
        Menu {
            ForEach(Self.others) { item in
                Button { state.refine(item) } label: { Text(verbatim: item.displayName) }
                    .disabled(!hasAnswer || state.isAnswering)
            }
            Divider()
            Button("重新生成") { state.regenerate() }
                .disabled(state.isAnswering || state.currentQuestion.isEmpty)
            if state.webSearchMode != .off, state.displayedWebLookup == nil {
                Button("联网查一下") { state.lookUpDisplayedAnswer() }
                    .disabled(!hasAnswer || state.displayedQuestion.isEmpty)
            }
            Button("复制回答") { state.copyAnswer() }.disabled(!hasAnswer)
            if includesWorkbench {
                Divider()
                Button("打开主窗口") { TeleprompterController.shared.showWorkbench() }
            }
        } label: {
            Image(systemName: "ellipsis")
        }
        .menuStyle(.button)
        .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 8, scalesWithDynamicType: true))
        .menuIndicator(.hidden)
        .fixedSize()
        .help("更多")
    }
}

/// "依据：资料 2 · 联网 3"; amber when the answer had limits.
private struct EvidenceLine: View {
    @Environment(AppState.self) private var state
    @Environment(\.liveSessionAcceptanceScenario) private var acceptanceScenario
    @State private var showsDetails = false

    var body: some View {
        if let id = state.displayedRecordID,
           let evidence = state.answerCoordinator.evidenceHistory[id] ?? state.answerHistory.first(where: { $0.id == id })?.evidence {
            Button { showsDetails.toggle() } label: {
                HStack(spacing: Space.s1) {
                    if !evidence.notes.isEmpty { Image(systemName: "exclamationmark.triangle") }
                    Text(verbatim: summary(evidence))
                }
                .font(LiveSessionTypography.caption)
                .foregroundStyle(evidence.notes.isEmpty ? Ink.secondary : Ink.caution)
            }
            .buttonStyle(.tapText)
            .popover(isPresented: $showsDetails, arrowEdge: .bottom) {
                EvidenceDetails(evidence: evidence).frame(width: 380)
            }
            .onAppear {
                if DemoMode.isEnabled && acceptanceScenario == .overlay { showsDetails = true }
            }
        }
    }

    private func summary(_ evidence: EvidenceBundle) -> String {
        var parts = [String(localized: "资料 \(evidence.materials.count)")]
        if evidence.screen != nil { parts.append(String(localized: "屏幕")) }
        if !evidence.webSources.isEmpty { parts.append(String(localized: "联网 \(evidence.webSources.count)")) }
        var line = String(localized: "依据：\(parts.joined(separator: " · "))")
        if !evidence.notes.isEmpty { line += String(localized: " · \(evidence.notes.count) 项限制") }
        return line
    }
}

private struct EvidenceDetails: View {
    let evidence: EvidenceBundle

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Space.s2) {
                Text(verbatim: evidence.retrieval).font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
                ForEach(Array(evidence.materials.enumerated()), id: \.offset) { _, item in
                    VStack(alignment: .leading, spacing: 2) {
                        Text(verbatim: item.citation).font(Font.zoomed(.caption).weight(.semibold))
                        Text(verbatim: item.text).font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary).lineLimit(6)
                    }
                }
                if let screen = evidence.screen {
                    Label { Text(verbatim: screen.source) } icon: { Image(systemName: "display") }.font(LiveSessionTypography.caption)
                }
                ForEach(evidence.webSources, id: \.url) { source in
                    if let url = URL(string: source.url) {
                        Link(destination: url) { Text(verbatim: source.title).lineLimit(1) }.font(LiveSessionTypography.caption)
                    }
                }
                ForEach(evidence.notes, id: \.self) { note in
                    Label { Text(verbatim: note) } icon: { Image(systemName: "exclamationmark.triangle") }
                        .font(LiveSessionTypography.caption).foregroundStyle(Ink.caution)
                }
            }
            .padding(Space.s4)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .frame(maxHeight: 360)
        .background(Ink.surfaceRaised)
    }
}

// MARK: - Bottom input row

private struct LiveComposer: View {
    @Environment(AppState.self) private var state
    @FocusState private var focused: Bool

    var body: some View {
        @Bindable var state = state
        VStack(spacing: Space.s2) {
            if let image = state.screenshotPreview {
                HStack(spacing: Space.s3) {
                    Image(nsImage: image).resizable().scaledToFit().frame(height: 48)
                        .clipShape(RoundedRectangle(cornerRadius: 4, style: .continuous))
                    Text("截图已就绪，可以补充要求后发送").font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
                    Spacer()
                    Button("取消") { state.clearScreenshot() }
                        .buttonStyle(GhostButtonStyle(height: 28, scalesWithDynamicType: true))
                    Button("分析截图") { state.answerScreenshot() }
                        .buttonStyle(SecondaryButtonStyle(scalesWithDynamicType: true))
                }
            }
            HStack(spacing: Space.s3) {
                HStack(spacing: Space.s2) {
                    TextField("", text: $state.questionInput, prompt: placeholder, axis: .vertical)
                        .lineLimit(1...4)
                        .textFieldStyle(.plain)
                        .font(LiveSessionTypography.body)
                        .focused($focused)
                        .onSubmit { submit() }
                    ShortcutHint(text: "⌘L", scalesWithDynamicType: true)
                    Menu {
                        Button("框选区域") { state.captureScreenshot(.region) }
                        Button("选择窗口") { state.captureScreenshot(.window) }
                    } label: {
                        Image(systemName: "camera.viewfinder")
                    }
                    .menuStyle(.button)
                    .buttonStyle(GlassIconButtonStyle(size: 28))
                    .menuIndicator(.hidden)
                    .fixedSize()
                    .help("截图提问 · 区域 \(state.shortcutLabel(.captureRegion)) · 窗口 \(state.shortcutLabel(.captureWindow))")
                    Button { submit() } label: { Image(systemName: "arrow.up.circle.fill") }
                        .buttonStyle(GlassIconButtonStyle(size: 28))
                        .disabled(state.questionInput.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && state.screenshotPreview == nil)
                        .accessibilityLabel("发送问题")
                }
                .padding(.leading, Space.s4).padding(.trailing, Space.s2)
                .frame(minHeight: Space.controlLarge)
                .background {
                    RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous)
                        .fill(Ink.surface)
                        .shadow(color: Ink.contactShadow, radius: 1, x: 0, y: 1)
                        .shadow(color: Ink.ambientShadow, radius: 14, x: 0, y: 8)
                }
                .overlay {
                    RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous)
                        .strokeBorder(focused ? Ink.focus.opacity(0.5) : Ink.hairline, lineWidth: focused ? 1 : 0.5)
                }
                .animation(Motion.hover, value: focused)
                if state.automaticAnswers {
                    Button { state.forceAnswerCurrent() } label: {
                        HStack(spacing: 6) {
                            Image(systemName: "bolt.fill").font(.zoomed(size: 11))
                            Text("立即回答")
                            ShortcutHint(text: state.shortcutLabel(.forceAnswer), onInk: true, scalesWithDynamicType: true)
                        }
                    }
                    .buttonStyle(PrimaryButtonStyle(height: Space.controlLarge, scalesWithDynamicType: true))
                    .accessibilityIdentifier("liveSession.primaryAnswer")
                    .liveSessionAcceptanceFrame("primary-action")
                    .help("回答最新的问题或转写，不等自动判断")
                    .transition(.scale(scale: 0.92, anchor: .trailing).combined(with: .opacity))
                }
            }
            .animation(Motion.snappy, value: state.automaticAnswers)
        }
        .padding(.horizontal, Space.s6)
        .padding(.top, Space.s2)
        .padding(.bottom, Space.s4)
        .background {
            // ⌘L focuses the input.
            Button("") { focused = true }
                .keyboardShortcut("l", modifiers: .command)
                .opacity(0)
                .allowsHitTesting(false)
                .accessibilityHidden(true)
        }
        .liveSessionAcceptanceFrame("composer")
    }

    private var placeholder: Text {
        if state.screenshotPreview != nil { return Text("对截图的补充要求（可留空）") }
        if state.automaticAnswers { return Text("输入问题，回车发送") }
        return Text("需要帮忙？输入问题，或按 \(state.shortcutLabel(.forceAnswer)) 回答对方刚问的")
    }

    private func submit() {
        if state.screenshotPreview == nil { state.answerInput() } else { state.answerScreenshot() }
    }
}

// MARK: - Localized names

extension SessionSpeaker {
    /// 对方 / 我 / 现场.
    var displayName: LocalizedStringKey {
        switch self {
        case .remote: "对方"
        case .me: "我"
        case .room: "现场"
        }
    }
}

extension AnswerRefinement {
    var displayName: String {
        switch self {
        case .shorter: String(localized: "更简短")
        case .detailed: String(localized: "展开")
        case .example: String(localized: "举例")
        case .star: String(localized: "STAR")
        case .english: String(localized: "英文版")
        case .chinese: String(localized: "中文版")
        case .clarify: String(localized: "澄清问题")
        case .followUps: String(localized: "我可以反问")
        }
    }
}

extension WebSearchMode {
    var displayName: String {
        switch self {
        case .off: String(localized: "不联网")
        case .auto: String(localized: "自动判断")
        case .always: String(localized: "每次都查")
        }
    }
}

// MARK: - Live actions

@MainActor
extension AppState {
    /// What leaves this Mac, said plainly (shown on the lock in the status line).
    var privacySummary: String {
        let audio = transcriptionBackend == "cloud"
            ? String(localized: "音频发送到 \(cloudProvider.title) 识别")
            : String(localized: "音频只在本机识别，不保存录音")
        let answer = answerBackend == .openAICompatible
            ? String(localized: "回答所需的文字发送到你配置的 API")
            : String(localized: "回答所需的文字和截图交给 Codex")
        let record = recordSessions ? String(localized: "转写和回答保存在本机") : String(localized: "不保存会议记录")
        return [audio, answer, record].joined(separator: String(localized: "；"))
    }

    /// Automatic answers / notes-only during the meeting (replaces the old preference setting).
    func setLiveMode(prompting: Bool) {
        guard automaticAnswers != prompting else { return }
        automaticAnswers = prompting
        if !prompting { deferredCandidate = nil }
        Diagnostics.record("live.mode", ["prompting": prompting])
    }

    /// The answer length in use (the meeting's frozen copy while it runs).
    var liveAnswerStyle: AnswerStyle { (preparedWorkspace ?? activeWorkspace).style }

    /// Applies from the next answer and is kept for the series.
    func setLiveAnswerStyle(_ style: AnswerStyle) {
        answerStyle = style
        preparedWorkspace?.style = style
    }

    /// Model choice from the answer settings menu.
    func setLiveModel(_ id: String) {
        guard !id.isEmpty, id != currentAnswerModel else { return }
        if answerBackend == .openAICompatible {
            apiModel = id
            saveAnswerSettings()
        } else {
            codexModelID = id
            normalizeCodexSelection()
        }
    }
}
