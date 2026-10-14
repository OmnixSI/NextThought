import AppKit
import CoreImage.CIFilterBuiltins
import SwiftUI
import UniformTypeIdentifiers
import InterviewHackerCore

struct ContentView: View {
    @EnvironmentObject private var state: AppState
    @State private var showModelDownloadWarning = false
    @State private var showHistoryDeleteWarning = false
    @State private var followTranscript = true
    @State private var transcriptExpanded = true

    var body: some View {
        ZStack {
            AppBackdrop()
            Group {
                switch state.section {
                case .assistant: assistant
                case .materials: MaterialsView()
                case .settings: settings
                case .history: history
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .safeAreaInset(edge: .top, spacing: 0) { topBar }
        }
        .frame(minWidth: 580, minHeight: 640)
        .tint(Brand.accent)
        .foregroundStyle(.primary)
        .preferredColorScheme(state.colorScheme)
        .confirmationDialog("下载本地语音识别模型？", isPresented: $showModelDownloadWarning, titleVisibility: .visible) {
            Button("下载 \(state.selectedModel?.sizeLabel ?? "模型")") { state.downloadModel() }
            Button("取消", role: .cancel) { }
        } message: {
            Text("\(state.selectedModel?.id ?? "") 会保存到本机应用支持目录，下载后自动切换使用。")
        }
        .confirmationDialog("删除全部本地记录？", isPresented: $showHistoryDeleteWarning, titleVisibility: .visible) {
            Button("删除记录", role: .destructive) { state.deleteHistory() }
            Button("取消", role: .cancel) { }
        } message: {
            Text("此操作会删除 InterviewHacker 保存的会话转录和回答。")
        }
        .sheet(isPresented: $state.isWindowPickerPresented) {
            NavigationStack {
                List(state.screenshotWindows) { window in
                    Button {
                        state.captureScreenshot(window)
                    } label: {
                        VStack(alignment: .leading, spacing: 3) {
                            Text(window.title).foregroundStyle(.primary).lineLimit(2)
                            Text(window.owner).font(.caption).foregroundStyle(.secondary)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                }
                .navigationTitle("选择要截图的窗口")
                .toolbar {
                    ToolbarItem(placement: .cancellationAction) {
                        Button("取消") { state.cancelWindowPicker() }
                    }
                }
            }
            .frame(width: 420, height: 520)
        }
        .sheet(isPresented: $state.isRegionCapturePresented) {
            if let image = state.regionCaptureImage {
                RegionScreenshotPicker(
                    image: image,
                    displays: state.screenshotDisplays,
                    selectedDisplayID: state.selectedScreenshotDisplayID,
                    onDisplaySelected: state.selectScreenshotDisplay,
                    onCancel: state.cancelRegionPicker,
                    onCapture: state.commitRegionScreenshot)
            } else {
                ProgressView("正在读取显示器…")
                    .frame(width: 420, height: 240)
            }
        }
        .sheet(isPresented: Binding(get: { state.shareTestImage != nil }, set: { if !$0 { state.shareTestImage = nil } })) {
            VStack(alignment: .leading, spacing: 12) {
                Text("共享可见性测试").font(.headline)
                Text("下面是用 ScreenCaptureKit 截取整个屏幕的结果，与 Zoom / Teams / Meet 等共享整屏时的取图方式相同。如果能看到提词器，说明共享整屏时对方也可能看到；请改为只共享会议窗口或标签页。")
                    .font(.callout).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                if let image = state.shareTestImage {
                    Image(nsImage: image).resizable().scaledToFit().frame(maxHeight: 420)
                        .clipShape(RoundedRectangle(cornerRadius: 8))
                }
                HStack { Spacer(); Button("关闭") { state.shareTestImage = nil }.keyboardShortcut(.defaultAction) }
            }
            .padding(20).frame(width: 640)
        }
    }

    // MARK: Top bar

    private var topBar: some View {
        VStack(spacing: 8) {
            GlassGroup(spacing: 10) {
                HStack(spacing: 10) {
                    // Room for the window's traffic-light buttons (hidden title bar).
                    Color.clear.frame(width: 64, height: 1)
                    GlassTabBar(tabs: AppSection.allCases, selection: $state.section, title: \.rawValue, symbol: \.symbol)
                    Spacer(minLength: 6)
                    HStack(spacing: 6) {
                        PulseDot(active: state.isRunning && !state.isPaused)
                        Text(state.isRunning ? (state.isPaused ? "已暂停" : "聆听中") : "未开始")
                            .font(.system(size: 12, weight: .medium)).foregroundStyle(.secondary)
                    }
                    .padding(.horizontal, 12).frame(height: 34)
                    .glassCapsule()
                    Button { TeleprompterController.shared.toggle() } label: { Image(systemName: "rectangle.topthird.inset.filled") }
                        .buttonStyle(GlassIconButtonStyle())
                        .help("显示 / 隐藏提词器 · \(state.shortcutLabel(.toggleTeleprompter))")
                }
            }
            if let message = state.errorMessage {
                HStack(alignment: .top, spacing: 10) {
                    Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.orange)
                    Text(message).font(.callout).textSelection(.enabled).fixedSize(horizontal: false, vertical: true)
                    Spacer(minLength: 0)
                    Button { withAnimation { state.closeError() } } label: { Image(systemName: "xmark") }
                        .buttonStyle(GlassIconButtonStyle(size: 26)).help("关闭提示")
                }
                .padding(12)
                .glassBackground(cornerRadius: 18, tint: Color.orange.opacity(0.25))
                .transition(.move(edge: .top).combined(with: .opacity))
            }
        }
        .padding(.horizontal, 14).padding(.top, 8).padding(.bottom, 10)
        .background(alignment: .top) { ScrollEdgeFade(edge: .top, height: 70).ignoresSafeArea() }
        .animation(.spring(response: 0.35, dampingFraction: 0.85), value: state.errorMessage)
    }

    // MARK: Assistant

    private var assistant: some View {
        ScrollView {
            VStack(spacing: 12) {
                sessionCard
                if state.liveCandidate != nil || !state.suggestedQuestions.isEmpty {
                    CandidateStrip().card()
                        .transition(.scale(scale: 0.97).combined(with: .opacity))
                }
                if let preview = state.screenshotPreview { screenshotCard(preview) }
                if !state.displayedQuestion.isEmpty || state.isAnswering || !state.displayedAnswer.isEmpty {
                    answerCard.transition(.scale(scale: 0.97).combined(with: .opacity))
                }
                transcriptCard
            }
            .padding(.horizontal, 16).padding(.top, 4).padding(.bottom, 12)
            .animation(.spring(response: 0.4, dampingFraction: 0.85), value: state.isAnswering)
            .animation(.spring(response: 0.4, dampingFraction: 0.85), value: state.liveCandidate?.id)
        }
        .scrollContentBackground(.hidden)
        .safeAreaInset(edge: .bottom, spacing: 0) { dock }
    }

    /// Floating composer dock: question field, screenshots and “answer now”.
    private var dock: some View {
        VStack(spacing: 6) {
            GlassGroup(spacing: 8) {
                HStack(spacing: 8) {
                    questionComposer
                    Button { state.captureScreenshot(.region) } label: { Image(systemName: "viewfinder") }
                        .buttonStyle(GlassIconButtonStyle(size: 40))
                        .help("区域截图提问 · \(state.shortcutLabel(.captureRegion))")
                    Button { state.captureScreenshot(.window) } label: { Image(systemName: "macwindow") }
                        .buttonStyle(GlassIconButtonStyle(size: 40))
                        .help("窗口截图提问 · \(state.shortcutLabel(.captureWindow))")
                    Button { state.forceAnswerCurrent() } label: {
                        Label("立即回答", systemImage: "bolt.fill")
                    }
                    .buttonStyle(PrimaryButtonStyle())
                    .help("回答最新的问题或转写，不等待自动判断 · \(state.shortcutLabel(.forceAnswer))")
                }
            }
            privacyNote
        }
        .padding(.horizontal, 14).padding(.top, 14).padding(.bottom, 10)
        .background(alignment: .bottom) { ScrollEdgeFade(edge: .bottom, height: 110).ignoresSafeArea() }
    }

    private var sessionCard: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(alignment: .center, spacing: 12) {
                ZStack {
                    Circle().fill(Brand.gradient)
                    Image(systemName: state.isRunning && !state.isPaused ? "waveform" : "waveform.and.mic")
                        .font(.system(size: 17, weight: .semibold)).foregroundStyle(.white)
                        .symbolEffect(.variableColor.iterative, isActive: state.isRunning && !state.isPaused)
                }
                .frame(width: 42, height: 42)
                .shadow(color: Brand.accent.opacity(0.45), radius: 10, y: 4)
                VStack(alignment: .leading, spacing: 3) {
                    Text(state.isRunning ? (state.isPaused ? "已暂停" : "正在实时辅助") : "实时辅助")
                        .font(.system(size: 16, weight: .semibold))
                    Text(state.isRunning ? state.statusText : state.readinessText)
                        .font(.system(size: 12))
                        .foregroundStyle(!state.isRunning && state.readinessText == "可以开始" ? Color.green : Color.secondary)
                        .lineLimit(1)
                }
                Spacer(minLength: 8)
                if let rtf = state.lastRTF {
                    MetricPill(symbol: "waveform", text: String(format: "RTF %.2f", rtf), tint: rtf < 0.6 ? .green : rtf < 1 ? .orange : .red)
                        .help("识别耗时 / 音频时长；大于 1 表示识别慢于说话")
                }
                if let first = state.lastFirstTokenSeconds {
                    MetricPill(symbol: "bolt", text: String(format: "首字 %.1fs", first), tint: first < 2 ? .green : .orange)
                        .help("从提问到第一个字出现")
                }
            }
            GlassGroup(spacing: 8) {
                HStack(spacing: 8) {
                    if !state.capturePermissionGranted {
                        Button("允许录屏与系统音频") { Task { await state.requestCapturePermission() } }
                            .buttonStyle(PrimaryButtonStyle())
                    }
                    if !state.isRunning {
                        Button { Task { await state.startSession() } } label: {
                            HStack(spacing: 6) {
                                if state.isChangingSession { ProgressView().controlSize(.small).tint(.white) }
                                Label(state.isChangingSession ? "正在启动…" : "开始辅助", systemImage: "play.fill")
                            }
                            .frame(maxWidth: .infinity)
                        }
                        .buttonStyle(PrimaryButtonStyle())
                        .disabled(state.readinessText != "可以开始" || state.isPreparingModel || state.isChangingSession)
                    } else {
                        Button { Task { if state.isPaused { await state.resumeSession() } else { await state.pauseSession() } } } label: {
                            Label(state.isPaused ? "恢复" : "暂停", systemImage: state.isPaused ? "play.fill" : "pause.fill").frame(maxWidth: .infinity)
                        }
                        .buttonStyle(state.isPaused ? AnyButtonStyle(PrimaryButtonStyle()) : AnyButtonStyle(SecondaryButtonStyle()))
                        Button { Task { await state.stopSession() } } label: {
                            Label("停止", systemImage: "stop.fill").frame(maxWidth: .infinity)
                        }
                        .buttonStyle(SecondaryButtonStyle())
                    }
                }
            }
            HStack(spacing: 6) {
                ForEach(SessionMode.allCases) { mode in
                    Button(mode.title) { state.sessionMode = mode; state.updateSettings() }
                        .buttonStyle(ChipButtonStyle(prominent: state.sessionMode == mode))
                }
                Button { state.section = .materials } label: {
                    Label("资料 \(state.documents.filter(\.enabled).count)", systemImage: "doc.text")
                }.buttonStyle(ChipButtonStyle())
                Spacer(minLength: 4)
                Image(systemName: "speaker.wave.2.fill").font(.system(size: 11)).foregroundStyle(.secondary)
                Picker("音频来源", selection: $state.selectedTargetID) {
                    ForEach(state.captureTargets) { Text($0.title).tag($0.id) }
                }
                .labelsHidden().pickerStyle(.menu).frame(maxWidth: 220)
                .onChange(of: state.selectedTargetID) { _, _ in Task { await state.changeCaptureTarget() } }
                if state.microphoneEnabled {
                    Image(systemName: "mic.fill").font(.system(size: 11)).foregroundStyle(.secondary).help("同时采集麦克风（我）")
                }
            }
            if state.droppedAudioSegments > 0 {
                Label("已遗漏 \(state.droppedAudioSegments) 段音频，转录不完整。可换更快的模型或云端识别。", systemImage: "exclamationmark.triangle")
                    .font(.caption).foregroundStyle(.orange)
            }
        }
        .card()
        .disabled(state.isChangingSession && state.isRunning)
    }

    private var answerCard: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .top, spacing: 8) {
                VStack(alignment: .leading, spacing: 4) {
                    Label(state.isAnswering && state.historyCursor == nil ? "正在回答" : "建议回答", systemImage: "sparkles")
                        .font(.system(size: 12, weight: .semibold)).foregroundStyle(Brand.accent)
                    Text(state.displayedQuestion.isEmpty ? state.pendingQuestion : state.displayedQuestion)
                        .font(.system(size: 13, weight: .medium)).foregroundStyle(.secondary).lineLimit(3).textSelection(.enabled)
                }
                Spacer()
                if let position = state.historyPositionText {
                    HStack(spacing: 4) {
                        Button { state.showPreviousAnswer() } label: { Image(systemName: "chevron.left") }
                            .buttonStyle(GlassIconButtonStyle(size: 26)).help("上一条 · \(state.shortcutLabel(.previousAnswer))")
                        Text(position).font(.caption.monospacedDigit()).foregroundStyle(.secondary)
                        Button { state.showNextAnswer() } label: { Image(systemName: "chevron.right") }
                            .buttonStyle(GlassIconButtonStyle(size: 26)).help("下一条 · \(state.shortcutLabel(.nextAnswer))")
                    }
                }
                if state.isAnswering {
                    ProgressView().controlSize(.small)
                    Button { state.cancelAnswer() } label: { Image(systemName: "xmark") }
                        .buttonStyle(GlassIconButtonStyle(size: 26)).help("取消回答 · \(state.shortcutLabel(.cancelAnswer))")
                }
            }
            ScrollView {
                if state.displayedAnswer.isEmpty {
                    Text("正在生成…").font(.system(size: 16)).foregroundStyle(.secondary).frame(maxWidth: .infinity, alignment: .leading)
                } else {
                    MarkdownText(text: state.displayedAnswer, size: 16, lineSpacing: 5)
                }
            }
            .frame(minHeight: 120, maxHeight: 360)
            RefinementBar()
            HStack(spacing: 8) {
                Button { state.copyAnswer() } label: { Label("复制", systemImage: "doc.on.doc") }.buttonStyle(ChipButtonStyle())
                Button { state.regenerate() } label: { Label("重新生成", systemImage: "arrow.clockwise") }
                    .buttonStyle(ChipButtonStyle()).disabled(state.isAnswering)
                Spacer()
                Text(answerMeta).font(.system(size: 11)).foregroundStyle(.tertiary)
            }
            if !state.appServerNote.isEmpty {
                Text(state.appServerNote).font(.caption).foregroundStyle(.orange)
            }
        }
        .card(tint: Brand.accent)
    }

    private var answerMeta: String {
        var parts: [String] = []
        if let first = state.lastFirstTokenSeconds { parts.append(String(format: "首字 %.1fs", first)) }
        if let total = state.lastAnswerSeconds { parts.append(String(format: "完成 %.1fs", total)) }
        parts.append(state.lastAnswerModel ?? (state.answerBackend == .openAICompatible ? state.apiModel : state.codexModelID))
        return parts.joined(separator: " · ")
    }

    private var transcriptCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                Button { withAnimation(.spring(response: 0.3, dampingFraction: 0.85)) { transcriptExpanded.toggle() } } label: {
                    Label(transcriptTitle, systemImage: transcriptExpanded ? "chevron.down" : "chevron.right")
                        .font(.system(size: 13, weight: .semibold))
                }.buttonStyle(.plain)
                Spacer()
                Text(state.transcriptionStatus).font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(1)
                Toggle("跟随", isOn: $followTranscript).toggleStyle(.switch).controlSize(.mini).font(.caption)
            }
            if transcriptExpanded {
                ScrollViewReader { proxy in
                    ScrollView {
                        VStack(alignment: .leading, spacing: 10) {
                            if state.entries.isEmpty && state.partialTranscript.isEmpty && state.microphonePartialTranscript.isEmpty {
                                Text("开始辅助后，识别的会议语音会出现在这里。原始音频不保存。")
                                    .font(.system(size: 12)).foregroundStyle(.secondary)
                                    .frame(maxWidth: .infinity, alignment: .leading).padding(.vertical, 10)
                            }
                            ForEach(state.entries.suffix(80)) { entry in
                                transcriptLine(entry.text, time: entry.date.formatted(date: .omitted, time: .shortened), partial: false, isMe: entry.source == CaptureSource.microphone.rawValue)
                                    .id(entry.id)
                            }
                            if !state.partialTranscript.isEmpty {
                                transcriptLine(state.partialTranscript, time: "识别中", partial: true, isMe: false).id("partial")
                            }
                            if !state.microphonePartialTranscript.isEmpty {
                                transcriptLine(state.microphonePartialTranscript, time: "识别中", partial: true, isMe: true).id("microphone-partial")
                            }
                        }
                    }
                    .frame(minHeight: 90, maxHeight: 240)
                    .onChange(of: state.partialTranscript) { _, _ in if followTranscript { proxy.scrollTo("partial", anchor: .bottom) } }
                    .onChange(of: state.microphonePartialTranscript) { _, _ in if followTranscript { proxy.scrollTo("microphone-partial", anchor: .bottom) } }
                    .onChange(of: state.entries.count) { _, _ in if followTranscript { proxy.scrollTo(state.entries.last?.id, anchor: .bottom) } }
                }
            }
        }
        .card()
    }

    private var transcriptTitle: String {
        switch state.transcriptionBackend {
        case "apple": "流式转录 · \(state.entries.count)"
        case "cloud": "云端流式转录 · \(state.entries.count)"
        default: "转录 · \(state.entries.count)"
        }
    }

    private func screenshotCard(_ image: NSImage) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Label("截图预览", systemImage: "viewfinder").font(.system(size: 13, weight: .semibold))
                Spacer()
                Button("取消") { state.clearScreenshot() }.buttonStyle(ChipButtonStyle())
                Button("分析截图") { state.answerScreenshot() }.buttonStyle(ChipButtonStyle(prominent: true))
            }
            Image(nsImage: image).resizable().scaledToFit().frame(maxHeight: 150).clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
            Text("可在下方输入框补充要求，例如“只指出 bug”。").font(.caption).foregroundStyle(.secondary)
        }
        .card()
    }

    private var questionComposer: some View {
        HStack(spacing: 8) {
            TextField(state.screenshotPreview == nil ? "输入问题，回车发送…" : "对截图的补充要求（可留空）…", text: $state.questionInput, axis: .vertical)
                .lineLimit(1...4)
                .textFieldStyle(.plain)
                .font(.system(size: 14))
                .onSubmit { state.screenshotPreview == nil ? state.answerInput() : state.answerScreenshot() }
            Button { state.screenshotPreview == nil ? state.answerInput() : state.answerScreenshot() } label: {
                Image(systemName: "arrow.up").font(.system(size: 13, weight: .bold))
            }
            .buttonStyle(GlassIconButtonStyle(size: 28, tint: Brand.accent))
            .disabled(state.screenshotPreview == nil && state.questionInput.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
        }
        .padding(.leading, 16).padding(.trailing, 6).padding(.vertical, 6)
        .frame(minHeight: 40)
        .glassBackground(cornerRadius: 20)
    }

    private var privacyNote: some View {
        HStack(spacing: 5) {
            Image(systemName: "lock.shield.fill").foregroundStyle(.green)
            Text(privacyText).lineLimit(1)
        }
        .font(.system(size: 10.5))
        .foregroundStyle(.secondary)
    }

    private var privacyText: String {
        let audio = state.transcriptionBackend == "cloud" ? "音频发送到 \(state.cloudProvider.title)" : "音频仅在本机识别"
        let answer = state.answerBackend == .openAICompatible ? "回答所需文本发送到所配置的 API" : "回答所需文本与截图交给 Codex"
        return audio + "；" + answer + "。"
    }

    private func transcriptLine(_ text: String, time: String, partial: Bool, isMe: Bool) -> some View {
        HStack(alignment: .top, spacing: 8) {
            Text(time).font(.system(size: 10, design: .rounded)).foregroundStyle(.tertiary).frame(width: 40, alignment: .trailing)
            Text(isMe ? "我" : "对方")
                .font(.system(size: 10, weight: .semibold))
                .foregroundStyle(isMe ? Brand.accent : Color.secondary)
                .padding(.horizontal, 7).padding(.vertical, 2)
                .background((isMe ? Brand.accent : Color.secondary).opacity(0.14), in: Capsule())
            Text(text).font(.system(size: 13)).lineSpacing(2).foregroundStyle(partial ? .secondary : .primary)
                .frame(maxWidth: .infinity, alignment: .leading).textSelection(.enabled)
        }
    }

    // MARK: Settings

    private var settings: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 12) {
                settingsSection("系统权限", icon: "lock.shield") {
                    Text(state.capturePermissionStatus).font(.system(size: 12)).foregroundStyle(state.capturePermissionGranted ? .green : .secondary)
                    HStack {
                        Button(state.capturePermissionGranted ? "打开系统设置" : "申请录屏与系统音频") {
                            if state.capturePermissionGranted { state.openCapturePermissionSettings() }
                            else { Task { await state.requestCapturePermission() } }
                        }
                        .buttonStyle(SecondaryButtonStyle())
                        Button("刷新状态") { Task { await state.refreshTargets() } }.buttonStyle(.plain)
                        Button("麦克风权限") { state.openMicrophoneSettings() }.buttonStyle(.plain).foregroundStyle(Color.accentColor)
                    }
                    Text("授权后如系统要求退出并重新打开，请完成重启。后续更新使用固定安装路径与同一签名身份；不要同时运行多个同名副本。")
                        .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                }

                answerEngineSection
                transcriptionSection
                if state.transcriptionBackend == "whisper" { whisperSection }

                settingsSection("捕获目标", icon: "rectangle.on.rectangle") {
                    Picker("系统音频来源", selection: $state.selectedTargetID) {
                        ForEach(state.captureTargets) { target in Text(target.title).tag(target.id) }
                    }
                    .onChange(of: state.selectedTargetID) { _, _ in Task { await state.changeCaptureTarget() } }
                    Toggle("采集麦克风（标记为“我”）", isOn: $state.microphoneEnabled)
                        .disabled(state.isRunning || state.isChangingSession)
                        .onChange(of: state.microphoneEnabled) { _, _ in state.updateSettings() }
                    Text("系统声音标记为“对方”，麦克风标记为“我”。自动回答只由对方的提问触发；你正在说话时不会自动插入回答。戴耳机可避免对方声音被麦克风再次收录。")
                        .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                }

                settingsSection("问题检测", icon: "text.bubble") {
                    Toggle("自动检测问题并回答", isOn: $state.automaticAnswers).onChange(of: state.automaticAnswers) { _, _ in state.updateSettings() }
                    Toggle("不确定的句子交给 API 模型二次判断", isOn: $state.verifyUncertainWithModel)
                        .disabled(state.answerBackend != .openAICompatible)
                        .onChange(of: state.verifyUncertainWithModel) { _, _ in state.saveAnswerSettings() }
                    Text("第一层是本地规则：区分陈述、未说完、明确提问、请求解释和追问；流式文字修订只更新同一个候选，稳定后才调用模型。课程场景更严格，避免把讲师的自问自答当成提问。拿不准的句子显示为可点击的“可能的问题”。")
                        .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                }

                overlaySection
                companionSection

                settingsSection("记录与外观", icon: "keyboard") {
                    Toggle("保存会话转录和回答", isOn: $state.saveHistory).onChange(of: state.saveHistory) { _, _ in state.updateSettings() }
                    Toggle("工作台窗口置顶", isOn: $state.keepOnTop).onChange(of: state.keepOnTop) { _, _ in state.updateSettings() }
                    Picker("外观", selection: $state.appearance) {
                        Text("跟随系统").tag("system")
                        Text("浅色").tag("light")
                        Text("深色").tag("dark")
                    }
                    .pickerStyle(.segmented)
                    .onChange(of: state.appearance) { _, _ in state.updateSettings() }
                    Divider()
                    Text("全局快捷键").font(.system(size: 13, weight: .semibold))
                    ShortcutSettingsView()
                    ForEach(state.shortcutErrors, id: \.self) { Text($0).font(.system(size: 11)).foregroundStyle(.orange) }
                }
            }
            .padding(.horizontal, 16)
            .padding(.bottom, 18)
        }
    }

    private var answerEngineSection: some View {
        settingsSection("回答引擎", icon: "sparkles") {
            Picker("引擎", selection: $state.answerBackend) {
                ForEach(AnswerBackend.allCases) { Text($0.title).tag($0) }
            }
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
                Text("逐字流式输出，首字通常最快。实时问答建议选择低延迟模型；截图题需要支持图像输入的模型。转录片段、检索到的资料和截图会发送到所填服务。")
                    .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
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
                Picker("回答模型", selection: $state.codexModelID) {
                    if !state.codexModels.contains(where: { $0.id == state.codexModelID }) {
                        Text(state.codexModelID.isEmpty ? "请选择模型" : state.codexModelID).tag(state.codexModelID)
                    }
                    ForEach(state.codexModels) { model in Text(model.name).tag(model.id) }
                }
                .disabled(state.isAnswering)
                .onChange(of: state.codexModelID) { _, _ in state.normalizeCodexSelection() }
                if state.codexModels.isEmpty {
                    TextField("模型 ID（与 Codex 中一致）", text: $state.codexModelID)
                        .textFieldStyle(.roundedBorder).disabled(state.isAnswering)
                        .onChange(of: state.codexModelID) { _, _ in state.updateSettings() }
                }
                Picker("推理强度", selection: $state.codexReasoning) {
                    ForEach(state.codexEfforts, id: \.self) { Text($0).tag($0) }
                }.disabled(state.isAnswering).onChange(of: state.codexReasoning) { _, _ in state.updateSettings() }
                Text(state.answerBackend == .codexAppServer
                     ? "常驻 `codex app-server` 复用进程与登录，逐字流式显示；不可用时自动回退到 codex exec。实时问答建议推理强度选 low 或更低。"
                     : "每次回答启动一次 codex exec，等待完整结果后显示；仅在常驻服务不可用时使用。")
                    .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                if !state.appServerNote.isEmpty { Text(state.appServerNote).font(.caption).foregroundStyle(.orange) }
            }
        }
    }

    private var transcriptionSection: some View {
        settingsSection("语音识别引擎", icon: "waveform") {
            Picker("引擎", selection: $state.transcriptionBackend) {
                Text("Whisper · 本地 / 自动中英文").tag("whisper")
                Text("Apple Speech · 本地流式 / macOS 26").tag("apple")
                Text("云端流式 · 最快 / 需 API Key").tag("cloud")
            }
            .disabled(state.isRunning || state.isPreparingModel)
            .onChange(of: state.transcriptionBackend) { _, _ in state.updateSettings(); Task { await state.prewarmRecognizer() } }
            Picker("识别语言", selection: $state.recognitionLanguage) {
                Text("自动 / 中英混说").tag("auto")
                Text("中文").tag("zh")
                Text("English").tag("en")
            }
            .disabled(state.isRunning || state.isPreparingModel)
            .onChange(of: state.recognitionLanguage) { _, _ in state.updateSettings(); Task { await state.prewarmRecognizer() } }
            Toggle("后台预加载识别模型（开始辅助几乎瞬间启动）", isOn: $state.keepRecognizerWarm)
                .onChange(of: state.keepRecognizerWarm) { _, _ in state.saveRecognizerSettings() }
            if let seconds = state.lastStartupSeconds {
                Text(String(format: "上次开始辅助用时 %.1f 秒", seconds)).font(.caption).foregroundStyle(.secondary)
            }
            TextField("专业词 / 人名，逗号分隔（提升识别准确度）", text: $state.recognitionVocabulary)
                .textFieldStyle(.roundedBorder)
                .onChange(of: state.recognitionVocabulary) { _, _ in state.updateSettings() }
            switch state.transcriptionBackend {
            case "apple":
                Text("Apple 路径需明确选择中文或 English，首次使用可能下载语言资源。")
                    .font(.caption).foregroundStyle(.secondary)
            case "cloud":
                Picker("服务", selection: $state.cloudProvider) {
                    ForEach(CloudSTTProvider.allCases) { Text($0.title).tag($0) }
                }
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
                Text("云端流式识别通常在说话的同时出字（几百毫秒），并能更早判断问题结束。Soniox 在同一句中英混说上最稳；OpenAI 转写为 24 kHz 自动重采样；Deepgram 需指定单一语言。请确认会议参与者同意录音与转写。")
                    .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            default:
                Toggle("说话过程中预览识别（设备够快时）", isOn: $state.liveWhisperPreview)
                    .onChange(of: state.liveWhisperPreview) { _, _ in state.saveAnswerSettings() }
                if let rtf = state.lastRTF, let seconds = state.lastASRSeconds {
                    Text(String(format: "上段 Whisper：%.2f s · RTF %.2f（>1 表示处理慢于音频输入）", seconds, rtf)).font(.caption)
                    if rtf > 0.8 {
                        Text("当前模型在这台 Mac 上偏慢，建议换 small / base 量化模型，或改用 Apple Speech / 云端流式。")
                            .font(.caption).foregroundStyle(.orange)
                    }
                }
            }
            if state.droppedAudioSegments > 0 {
                Text("当前会话有 \(state.droppedAudioSegments) 段音频因积压或停止超时未完成识别；转录并不完整。").font(.caption).foregroundStyle(.orange)
            }
        }
    }

    private var whisperSection: some View {
        settingsSection("本地 Whisper 模型", icon: "cpu") {
            HStack {
                Text("已发现的本地模型").font(.system(size: 12, weight: .semibold))
                Spacer()
                if state.isScanningModels { ProgressView().controlSize(.small) }
                Button("重新扫描") { Task { await state.scanLocalModels() } }.disabled(state.isScanningModels)
            }
            Text(state.modelScanStatus.isEmpty ? "正在扫描常用 Whisper 模型缓存…" : state.modelScanStatus)
                .font(.caption).foregroundStyle(.secondary)
            if !state.discoveredModels.isEmpty {
                ScrollView {
                    VStack(alignment: .leading, spacing: 10) {
                        ForEach(state.discoveredModels) { model in
                            HStack(alignment: .top) {
                                VStack(alignment: .leading, spacing: 3) {
                                    Text(model.name).font(.system(size: 12, weight: .medium))
                                    Text("\(model.format) · \(ByteCountFormatter.string(fromByteCount: model.bytes, countStyle: .binary))").font(.caption).foregroundStyle(.secondary)
                                    Text(model.url.path).font(.system(size: 10)).foregroundStyle(.secondary).lineLimit(2).textSelection(.enabled)
                                }
                                Spacer()
                                Button("使用") { state.useDiscoveredModel(model) }.disabled(state.modelControlsDisabled)
                            }
                        }
                    }
                }.frame(maxHeight: 150)
            }
            Picker("选择模型", selection: $state.selectedModelID) {
                ForEach(WhisperModel.catalog) { model in
                    Text("\(model.id) · \(model.sizeLabel)\(FileManager.default.fileExists(atPath: LocalWhisperService.modelsDirectory.appendingPathComponent(model.filename).path) ? " · 已下载" : "")").tag(model.id)
                }
                ForEach(state.localModelPaths, id: \.self) { name in
                    Text("本地导入 · \(name.replacingOccurrences(of: #"^imported-[A-Fa-f0-9-]{36}-"#, with: "", options: .regularExpression))").tag("local:" + name)
                }
            }
            .disabled(state.modelControlsDisabled)
            Text(state.selectedModel?.summary ?? "本地导入的 Whisper 模型。占用和识别效果取决于所选版本；.en 模型仅支持英语。")
                .font(.system(size: 12)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            Text("当前使用：\(URL(fileURLWithPath: state.modelPath).lastPathComponent)\(state.isModelInstalled ? " · 已校验" : " · 尚未就绪")")
                .font(.system(size: 12)).foregroundStyle(state.isModelInstalled ? .green : .secondary)
            HStack {
                if state.isDownloadingModel {
                    Button("取消下载") { state.cancelModelDownload() }.buttonStyle(SecondaryButtonStyle())
                } else if state.selectedModelExists {
                    Button("使用此模型") { state.useSelectedModel() }.buttonStyle(SecondaryButtonStyle()).disabled(state.modelControlsDisabled)
                    Button("删除副本") { state.deleteModel() }.foregroundStyle(.red).disabled(state.modelControlsDisabled)
                } else if state.selectedModel != nil {
                    Button("下载并使用") { showModelDownloadWarning = true }.buttonStyle(SecondaryButtonStyle()).disabled(state.modelControlsDisabled)
                }
                Button("导入本地模型…") { state.importLocalModel() }.buttonStyle(SecondaryButtonStyle()).disabled(state.modelControlsDisabled)
            }
            if state.isDownloadingModel { ProgressView(value: state.modelDownloadProgress).controlSize(.small) }
            if state.isImportingModel { ProgressView().controlSize(.small) }
            if !state.modelDownloadStatus.isEmpty {
                Text(state.modelDownloadStatus).font(.system(size: 12)).foregroundStyle(.secondary).textSelection(.enabled)
            }
            Text("模型越大通常越准但越慢。实时会议优先选 RTF 明显小于 1 的模型；Q5/Q8 量化可显著降低内存。导入 .pt 需要本机已有 Python Whisper。")
                .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            Link("模型来源与开源说明", destination: URL(string: "https://huggingface.co/ggerganov/whisper.cpp")!)
                .font(.system(size: 11)).foregroundStyle(Color.accentColor)
            if let seconds = state.modelLoadSeconds, seconds > 0 {
                Text(String(format: "本次模型加载 %.1f 秒", seconds)).font(.system(size: 11)).foregroundStyle(.tertiary)
            }
        }
    }

    private var overlaySection: some View {
        settingsSection("提词器与屏幕共享", icon: "rectangle.topthird.inset.filled") {
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
            Text("macOS 没有公开 API 能保证窗口不被录屏：此选项对旧式捕获和多数“共享单个窗口”有效，但 macOS 15 起部分基于 ScreenCaptureKit 的整屏共享 / 录屏仍可能拍到。最稳妥的做法是只共享会议窗口或标签页，或者把回答放到手机上（见下方）。\(state.shortcutLabel(.panicHide)) 可一键隐藏全部窗口。请遵守会议、考试与面试方的规则。")
                .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
        }
    }

    private var companionSection: some View {
        settingsSection("手机伴侣", icon: "iphone") {
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
            Text("手机实时显示当前问题和流式回答，并可一键“回答 / 改短 / 举例 / 英文版 / 上一条”。屏幕共享完全看不到手机。只同步当前回答，不同步转录、资料或截图；链接含一次性随机配对码，关闭后失效。连接为局域网明文 HTTP，请只在可信网络使用。")
                .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
        }
    }

    // MARK: History

    private var history: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text("本地会话记录").font(.system(size: 15, weight: .semibold))
                    Text("记录保存在本机，不含原始音频。在设置中开启保存。").font(.system(size: 12)).foregroundStyle(.secondary)
                }
                Spacer()
                Button("导出当前转录") { state.saveTranscriptExport() }.buttonStyle(SecondaryButtonStyle())
                Button { showHistoryDeleteWarning = true } label: { Image(systemName: "trash") }
                    .buttonStyle(.plain).foregroundStyle(.red).disabled(state.savedSessions.isEmpty).help("删除全部记录")
            }
            if state.savedSessions.isEmpty {
                ContentUnavailableView("没有保存的会话", systemImage: "clock", description: Text("在设置中开启“保存会话转录和回答”。"))
                    .frame(maxHeight: .infinity)
            } else {
                ScrollView {
                    VStack(spacing: 9) {
                        ForEach(state.savedSessions) { session in SessionRow(session: session) }
                    }
                }
            }
        }
        .padding(16)
    }

    private func settingsSection<Content: View>(_ title: String, icon: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 10) {
                Image(systemName: icon)
                    .font(.system(size: 12, weight: .semibold)).foregroundStyle(.white)
                    .frame(width: 26, height: 26)
                    .background(Brand.gradient, in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                Text(title).font(.system(size: 14, weight: .semibold))
            }
            content()
        }
        .font(.system(size: 12))
        .card()
    }
}

private struct SessionRow: View {
    @EnvironmentObject var state: AppState
    let session: SavedSession
    @State private var expanded = false

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            HStack {
                Button { withAnimation(.easeOut(duration: 0.15)) { expanded.toggle() } } label: {
                    Label(session.startedAt.formatted(date: .abbreviated, time: .shortened), systemImage: expanded ? "chevron.down" : "chevron.right")
                        .font(.system(size: 13, weight: .semibold))
                }.buttonStyle(.plain)
                Spacer()
                Text("\(session.entries.count) 条转录 · \(session.records?.count ?? session.answers.count) 个回答")
                    .font(.system(size: 11)).foregroundStyle(.secondary)
                Button { state.exportSession(session) } label: { Image(systemName: "square.and.arrow.up") }.buttonStyle(.borderless).help("导出 Markdown")
                Button { state.deleteSession(session.id) } label: { Image(systemName: "trash") }.buttonStyle(.borderless).foregroundStyle(.red).help("删除此记录")
            }
            if expanded {
                ForEach(session.records ?? []) { record in
                    VStack(alignment: .leading, spacing: 4) {
                        Text(record.question).font(.system(size: 12, weight: .medium)).textSelection(.enabled)
                        MarkdownText(text: record.answer, size: 12)
                    }
                    .padding(8)
                    .background(Color.accentColor.opacity(0.06), in: RoundedRectangle(cornerRadius: 8))
                }
                Text(session.entries.map(\.contextualText).joined(separator: "\n"))
                    .font(.system(size: 11)).foregroundStyle(.secondary).textSelection(.enabled).lineLimit(40)
            } else {
                Text(session.entries.map(\.text).joined(separator: " · "))
                    .font(.system(size: 12)).lineLimit(2).foregroundStyle(.secondary)
            }
        }
        .card()
    }
}

struct QRCodeView: View {
    let text: String
    var body: some View {
        if let image = Self.render(text) {
            Image(nsImage: image).interpolation(.none).resizable().scaledToFit()
                .padding(6).background(Color.white, in: RoundedRectangle(cornerRadius: 8))
        } else {
            Image(systemName: "qrcode").font(.largeTitle)
        }
    }

    static func render(_ text: String) -> NSImage? {
        let filter = CIFilter.qrCodeGenerator()
        filter.message = Data(text.utf8)
        filter.correctionLevel = "M"
        guard let output = filter.outputImage?.transformed(by: CGAffineTransform(scaleX: 8, y: 8)) else { return nil }
        let representation = NSCIImageRep(ciImage: output)
        let image = NSImage(size: representation.size)
        image.addRepresentation(representation)
        return image
    }
}

private extension AppSection {
    var symbol: String {
        switch self {
        case .assistant: "waveform"
        case .materials: "doc.on.doc"
        case .settings: "gearshape"
        case .history: "clock"
        }
    }
}

/// Breathing status dot shown while listening.
struct PulseDot: View {
    var active: Bool
    @State private var pulse = false
    var body: some View {
        ZStack {
            if active {
                Circle().fill(Color.green.opacity(0.35)).frame(width: 14, height: 14)
                    .scaleEffect(pulse ? 1.25 : 0.6).opacity(pulse ? 0 : 1)
            }
            Circle().fill(active ? Color.green : Color.secondary.opacity(0.6)).frame(width: 7, height: 7)
        }
        .frame(width: 14, height: 14)
        .onAppear { withAnimation(.easeOut(duration: 1.4).repeatForever(autoreverses: false)) { pulse = true } }
    }
}
