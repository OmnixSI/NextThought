import AppKit
import SwiftUI
import InterviewHackerCore

/// The live workspace. Wide windows show the conversation and the answer side by
/// side; narrow windows stack them with the answer first.
struct AssistantView: View {
    @Environment(AppState.self) private var state

    var body: some View {
        GeometryReader { proxy in
            let wide = proxy.size.width >= 880
            VStack(spacing: 12) {
                if !state.isRunning { SetupCard() }
                if wide {
                    HStack(alignment: .top, spacing: 12) {
                        ConversationPanel()
                            .frame(width: max(340, proxy.size.width * 0.44))
                        VStack(spacing: 12) {
                            DetectionStrip()
                            ScreenshotCard()
                            AnswerPanel(fillHeight: true)
                        }
                        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
                    }
                    .frame(maxHeight: .infinity)
                } else {
                    DetectionStrip()
                    ScreenshotCard()
                    AnswerPanel(fillHeight: false)
                    ConversationPanel()
                        .frame(minHeight: 220, maxHeight: .infinity)
                }
            }
            .padding(.horizontal, 16).padding(.top, 4).padding(.bottom, 8)
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        }
        .safeAreaInset(edge: .bottom, spacing: 0) { ComposerDock() }
    }
}

// MARK: - Setup (before a session)

private struct SetupCard: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .center, spacing: 12) {
                ZStack {
                    Circle().fill(Brand.gradient)
                    Image(systemName: "waveform.and.mic").font(.system(size: 17, weight: .semibold)).foregroundStyle(.white)
                }
                .frame(width: 40, height: 40)
                VStack(alignment: .leading, spacing: 3) {
                    Text("准备开始").font(.system(size: 16, weight: .semibold))
                    Text(state.readinessText)
                        .font(.system(size: 12))
                        .foregroundStyle(state.isReadyToStart ? Color.green : Color.secondary)
                        .lineLimit(1)
                }
                Spacer(minLength: 8)
                if !state.capturePermissionGranted {
                    Button("允许录屏与系统音频") { Task { await state.requestCapturePermission() } }
                        .buttonStyle(PrimaryButtonStyle())
                } else if state.transcriptionBackend == "zipformer", !state.isModelInstalled {
                    Button(state.isDownloadingModel ? "下载中 \(Int(state.modelDownloadProgress * 100))%" : "下载识别模型") { state.downloadModel() }
                        .buttonStyle(PrimaryButtonStyle()).disabled(state.isDownloadingModel)
                } else {
                    Button { Task { await state.startSession() } } label: {
                        HStack(spacing: 6) {
                            if state.isChangingSession { ProgressView().controlSize(.small).tint(.white) }
                            Label(state.isChangingSession ? "正在启动…" : "开始辅助", systemImage: "play.fill")
                        }
                        .frame(minWidth: 120)
                    }
                    .buttonStyle(PrimaryButtonStyle())
                    .keyboardShortcut(.defaultAction)
                    .disabled(!state.isReadyToStart || state.isPreparingModel || state.isChangingSession)
                }
            }
            if state.isDownloadingModel {
                ProgressView(value: state.modelDownloadProgress).controlSize(.small)
                Text(state.modelDownloadStatus).font(.caption).foregroundStyle(.secondary)
            }
            if !state.answerEngineReady {
                HStack(spacing: 8) {
                    Image(systemName: "exclamationmark.circle").foregroundStyle(.orange)
                    Text("回答引擎还没设置好：可以转写，但不会生成回答。").font(.system(size: 12))
                    Spacer()
                    Button("设置向导") { state.openOnboarding() }.buttonStyle(ChipButtonStyle(prominent: true))
                }
            }
            HStack(spacing: 6) {
                WorkspacePicker()
                Button { state.section = .materials } label: {
                    Label("资料 \(state.documents.filter(\.enabled).count) · 题库 \(state.activeWorkspace.questionBank.count)", systemImage: "doc.text")
                }.buttonStyle(ChipButtonStyle()).help("编辑这个场景的资料、回答要求、经验库与题库")
                Spacer(minLength: 4)
                Image(systemName: "speaker.wave.2.fill").font(.system(size: 11)).foregroundStyle(.secondary)
                MenuPicker(selection: $state.selectedTargetID, options: state.captureTargets.map { ($0.id, $0.title) }, placeholder: "选择音频来源")
                    .frame(maxWidth: 260, alignment: .trailing)
                    .onChange(of: state.selectedTargetID) { _, _ in Task { await state.changeCaptureTarget() } }
                Button { Task { await state.refreshTargets() } } label: { Image(systemName: "arrow.clockwise") }
                    .buttonStyle(GlassIconButtonStyle(size: 28)).help("刷新窗口列表")
                Toggle(isOn: $state.microphoneEnabled) { Image(systemName: "mic.fill") }
                    .toggleStyle(.button).help("同时采集麦克风（我）")
                    .onChange(of: state.microphoneEnabled) { _, _ in state.updateSettings() }
            }
            Text("选会议 App 的窗口只采集它的声音；麦克风单独识别并标为“我”。没戴耳机时，扬声器回声会被自动识别并忽略。")
                .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
        }
        .card()
    }
}

/// Active scenario: mode icon and name; switching loads its materials and knowledge.
struct WorkspacePicker: View {
    @Environment(AppState.self) private var state
    var compact = false

    var body: some View {
        Menu {
            ForEach(SessionMode.allCases) { mode in
                let items = state.workspaces.visible.filter { $0.mode == mode }
                if !items.isEmpty {
                    Section(mode.title) {
                        ForEach(items) { workspace in
                            Button { state.switchWorkspace(workspace.id) } label: {
                                if workspace.id == state.activeWorkspace.id { Label(workspace.name, systemImage: "checkmark") } else { Text(workspace.name) }
                            }
                        }
                    }
                }
            }
            Divider()
            Menu("新建场景") {
                ForEach(SessionMode.allCases) { mode in
                    Button { state.createWorkspace(mode: mode); state.section = .materials } label: { Label(mode.title, systemImage: mode.symbol) }
                }
            }
            Button("管理场景…") { state.section = .materials }
        } label: {
            HStack(spacing: 6) {
                Image(systemName: state.sessionMode.symbol).foregroundStyle(Brand.accent)
                Text(state.activeWorkspace.name).lineLimit(1).foregroundStyle(Color.primary)
                if !compact { Text(state.sessionMode.title).foregroundStyle(.secondary) }
                Image(systemName: "chevron.up.chevron.down").font(.system(size: 9, weight: .semibold)).foregroundStyle(.secondary)
            }
            .font(.system(size: 12.5, weight: .medium))
            .padding(.horizontal, 12).padding(.vertical, 6)
            .glassCapsule()
            .contentShape(Capsule())
        }
        .menuStyle(.button)
        .buttonStyle(.plain)
        .menuIndicator(.hidden)
        .fixedSize()
        .help("当前场景：模式、资料、回答要求和经验库都来自这里")
    }
}

// MARK: - Live conversation

private struct ConversationPanel: View {
    @Environment(AppState.self) private var state
    @State private var follow = true

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                Label("实时对话", systemImage: "text.bubble").font(.system(size: 13, weight: .semibold)).lineLimit(1).fixedSize()
                Text("\(state.entries.count)").font(.caption.monospacedDigit()).foregroundStyle(.tertiary)
                Spacer()
                if state.isRunning { WorkspacePicker(compact: true) }
                if state.isRunning {
                    MenuPicker(selection: $state.selectedTargetID, options: state.captureTargets.map { ($0.id, $0.title) }, placeholder: "音频来源")
                        .frame(maxWidth: 170, alignment: .trailing)
                        .onChange(of: state.selectedTargetID) { _, _ in Task { await state.changeCaptureTarget() } }
                }
                Button { follow.toggle() } label: {
                    Image(systemName: follow ? "arrow.down.to.line.compact" : "pause.circle")
                }
                .buttonStyle(GlassIconButtonStyle(size: 26, tint: follow ? Brand.accent : nil))
                .help(follow ? "自动滚动到最新（点击暂停）" : "已暂停自动滚动（点击恢复）")
            }
            TranscriptStatusLine()
            ScrollViewReader { proxy in
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 8) {
                        if state.entries.isEmpty {
                            EmptyConversationHint()
                        }
                        ForEach(state.entries) { entry in
                            ConversationBubble(entry: entry).id(entry.id)
                        }
                        PartialBubbles()
                        Color.clear.frame(height: 1).id("bottom")
                    }
                    .padding(.vertical, 4)
                }
                .scrollIndicators(.automatic)
                .onChange(of: state.entries.count) { _, _ in if follow { proxy.scrollTo("bottom", anchor: .bottom) } }
                .onChange(of: state.partialTranscript.count / 8) { _, _ in if follow { proxy.scrollTo("bottom", anchor: .bottom) } }
                .onChange(of: follow) { _, value in if value { proxy.scrollTo("bottom", anchor: .bottom) } }
            }
        }
        .card()
        .frame(maxHeight: .infinity, alignment: .top)
    }
}

private struct TranscriptStatusLine: View {
    @Environment(AppState.self) private var state
    var body: some View {
        HStack(spacing: 6) {
            Text(state.isRunning ? state.transcriptionStatus : "未在采集")
            if !state.microphoneEnabled {
                Text("· 麦克风未开启，不会显示你说的话").foregroundStyle(.orange)
                    .help("在“准备开始”卡片里点麦克风按钮，或在 设置 → 基础 → 音频来源 打开")
            } else if state.microphoneAccess == .denied {
                Button("· 麦克风未授权，点此打开系统设置") { state.openMicrophoneSettings() }
                    .buttonStyle(.plain).foregroundStyle(.orange)
            }
            if state.echoDroppedCount > 0 {
                Text("· \(state.echoDroppedCount) 句疑似回声").help("和对方刚说的话几乎一样的麦克风文字被当作扬声器回声，灰色显示、不参与回答；如果是你说的，点那一行的“是我说的”")
            }
            if state.droppedAudioSegments > 0 {
                Text("· 遗漏 \(state.droppedAudioSegments) 段").foregroundStyle(.orange)
            }
        }
        .font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(1)
    }
}

private struct EmptyConversationHint: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("开始辅助后，双方的话会实时出现在这里。").font(.system(size: 12))
            Text("· 左侧灰色为对方（系统声音），右侧彩色为我（麦克风）\n· 识别为问题的句子会标出；把鼠标移到任意一句上可以“回答这句”\n· 原始音频不保存；转录与回答保存在“记录”里")
                .font(.system(size: 11)).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading).padding(.vertical, 10)
    }
}

private struct ConversationBubble: View {
    @Environment(AppState.self) private var state
    let entry: TranscriptEntry
    @State private var hovering = false

    var body: some View {
        if entry.suspectedEcho == true { echoLine } else { bubble }
    }

    /// Microphone text that matched the other side: dimmed, one click to keep it.
    private var echoLine: some View {
        HStack(spacing: 6) {
            Spacer(minLength: 40)
            Image(systemName: "speaker.wave.2").font(.system(size: 10)).foregroundStyle(.tertiary)
            Text(entry.text).font(.system(size: 11.5)).foregroundStyle(.tertiary).lineLimit(1).strikethrough(color: .secondary.opacity(0.4))
            Button("是我说的") { state.markNotEcho(entry.id) }
                .buttonStyle(.plain).font(.system(size: 11, weight: .medium)).foregroundStyle(Brand.accent)
        }
        .help("这句和对方刚说的话很像，被当作扬声器回声忽略了（不参与回答）。如果是你说的，点“是我说的”。")
    }

    private var bubble: some View {
        let isMe = entry.isMe
        let isQuestion = !isMe && state.isQuestionText(entry.text)
        return HStack(alignment: .bottom, spacing: 6) {
            if isMe { Spacer(minLength: 40) }
            VStack(alignment: isMe ? .trailing : .leading, spacing: 3) {
                HStack(spacing: 5) {
                    if isQuestion {
                        Label("问题", systemImage: "questionmark.bubble.fill")
                            .font(.system(size: 9.5, weight: .semibold)).foregroundStyle(Brand.accent)
                    }
                    Text(entry.date.formatted(date: .omitted, time: .standard))
                        .font(.system(size: 9.5).monospacedDigit()).foregroundStyle(.tertiary)
                }
                Text(entry.text)
                    .font(.system(size: 13)).lineSpacing(2)
                    .textSelection(.enabled)
                    .padding(.horizontal, 11).padding(.vertical, 7)
                    .background(bubbleColor(isMe: isMe, isQuestion: isQuestion), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                    .overlay(alignment: isMe ? .bottomLeading : .bottomTrailing) {
                        if hovering && !isMe {
                            Button { state.answerEntry(entry) } label: { Label("回答这句", systemImage: "bolt.fill") }
                                .buttonStyle(ChipButtonStyle(prominent: true))
                                .offset(x: 8, y: 10)
                                .transition(.opacity)
                        }
                    }
            }
            if !isMe { Spacer(minLength: 40) }
        }
        .onHover { hovering = $0 }
        .animation(.easeOut(duration: 0.12), value: hovering)
    }

    private func bubbleColor(isMe: Bool, isQuestion: Bool) -> Color {
        if isMe { return Brand.accent.opacity(0.16) }
        return isQuestion ? Brand.accent.opacity(0.08) : Color.primary.opacity(0.06)
    }
}

/// The two in-progress utterances; only these redraw while someone is speaking.
private struct PartialBubbles: View {
    @Environment(AppState.self) private var state
    var body: some View {
        if !state.partialTranscript.isEmpty { partial(state.partialTranscript, isMe: false) }
        if !state.microphonePartialTranscript.isEmpty { partial(state.microphonePartialTranscript, isMe: true) }
    }

    private func partial(_ text: String, isMe: Bool) -> some View {
        HStack(alignment: .bottom, spacing: 6) {
            if isMe { Spacer(minLength: 40) }
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                Image(systemName: "waveform").font(.system(size: 10)).foregroundStyle(.tertiary)
                Text(text).font(.system(size: 13)).foregroundStyle(.secondary).lineSpacing(2)
            }
            .padding(.horizontal, 11).padding(.vertical, 7)
            .background(RoundedRectangle(cornerRadius: 14, style: .continuous).strokeBorder(Color.primary.opacity(0.12), style: StrokeStyle(lineWidth: 1, dash: [4, 3])))
            if !isMe { Spacer(minLength: 40) }
        }
    }
}

// MARK: - Question detection

/// What the detector is tracking right now, plus uncertain questions to click.
struct DetectionStrip: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let live = state.liveCandidate.flatMap { candidate -> QuestionCandidate? in
            candidate.state == .triggered && state.isAnswering ? nil : candidate
        }
        if live != nil || !state.suggestedQuestions.isEmpty {
            VStack(alignment: .leading, spacing: 6) {
                if let candidate = live {
                    HStack(spacing: 8) {
                        Image(systemName: candidate.isFinal ? "text.bubble" : "waveform").foregroundStyle(Brand.accent)
                        Text(candidate.assessment.intent.title).font(.system(size: 11, weight: .semibold)).foregroundStyle(Brand.accent)
                        ConfidenceBar(score: candidate.assessment.score)
                        Text(candidate.fullText).font(.system(size: 12)).foregroundStyle(.secondary).lineLimit(1)
                        Spacer(minLength: 0)
                        if candidate.state != .triggered, candidate.assessment.intent != .statement {
                            Button("回答") { state.answerCandidate(candidate) }.buttonStyle(ChipButtonStyle(prominent: true))
                        }
                    }
                }
                ForEach(state.suggestedQuestions.reversed()) { candidate in
                    HStack(spacing: 8) {
                        Image(systemName: "questionmark.bubble").foregroundStyle(.orange)
                        Text(candidate.fullText).font(.system(size: 12)).lineLimit(1)
                        Spacer(minLength: 0)
                        Button("回答") { state.answerCandidate(candidate) }.buttonStyle(ChipButtonStyle())
                        Button { state.dismissCandidate(candidate) } label: { Image(systemName: "xmark") }
                            .buttonStyle(.borderless).foregroundStyle(.secondary)
                    }
                }
            }
            .card()
            .transition(.opacity)
        }
    }
}

private struct ConfidenceBar: View {
    var score: Double
    var body: some View {
        ZStack(alignment: .leading) {
            Capsule().fill(Color.primary.opacity(0.1))
            Capsule().fill(score >= 0.6 ? Color.green : score >= 0.4 ? Color.orange : Color.secondary)
                .frame(width: 36 * max(0.05, min(1, score)))
        }
        .frame(width: 36, height: 4)
        .help(String(format: "问题置信度 %.0f%%", score * 100))
    }
}

// MARK: - Answer

private struct AnswerPanel: View {
    @Environment(AppState.self) private var state
    var fillHeight: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            AnswerHeader()
            AnswerBody()
                .frame(minHeight: fillHeight ? 160 : 120, maxHeight: fillHeight ? .infinity : 340)
            RefinementBar()
            AnswerFooter()
        }
        .card(tint: Brand.accent)
        .frame(maxHeight: fillHeight ? .infinity : nil, alignment: .top)
    }
}

private struct AnswerHeader: View {
    @Environment(AppState.self) private var state
    var body: some View {
        HStack(alignment: .top, spacing: 8) {
            VStack(alignment: .leading, spacing: 4) {
                Label(state.isAnswering && state.historyCursor == nil ? "正在回答" : "建议回答", systemImage: "sparkles")
                    .font(.system(size: 12, weight: .semibold)).foregroundStyle(Brand.accent)
                let question = state.displayedQuestion
                if !question.isEmpty {
                    Text(question)
                        .font(.system(size: 13, weight: .medium)).foregroundStyle(.secondary).lineLimit(3).textSelection(.enabled)
                }
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
    }
}

/// The streaming text: the only part of the panel that redraws per delta.
private struct AnswerBody: View {
    @Environment(AppState.self) private var state
    var body: some View {
        ScrollViewReader { proxy in
            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    let text = state.displayedAnswer
                    if text.isEmpty {
                        Text(state.isAnswering ? "正在生成…" : "对方提问时会自动给出回答；也可以在下方输入问题，或按 \(state.shortcutLabel(.forceAnswer)) 立即回答最新的话。")
                            .font(.system(size: 14)).foregroundStyle(.secondary)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    } else {
                        MarkdownText(text: text, size: 16, lineSpacing: 5).equatable()
                    }
                    Color.clear.frame(height: 1).id("answer-end")
                }
            }
            .onChange(of: state.answerDraft.count / 40) { _, _ in
                if state.isAnswering, state.historyCursor == nil { proxy.scrollTo("answer-end", anchor: .bottom) }
            }
        }
    }
}

private struct AnswerFooter: View {
    @Environment(AppState.self) private var state
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 8) {
                Button { state.copyAnswer() } label: { Label("复制", systemImage: "doc.on.doc") }
                    .buttonStyle(ChipButtonStyle()).disabled(state.displayedAnswer.isEmpty)
                Button { state.regenerate() } label: { Label("重新生成", systemImage: "arrow.clockwise") }
                    .buttonStyle(ChipButtonStyle()).disabled(state.isAnswering || state.currentQuestion.isEmpty)
                Spacer()
                Text(meta).font(.system(size: 11)).foregroundStyle(.tertiary).lineLimit(1)
            }
            if !state.appServerNote.isEmpty {
                Text(state.appServerNote).font(.caption).foregroundStyle(.orange)
            }
        }
    }

    private var meta: String {
        var parts: [String] = []
        if let first = state.lastFirstTokenSeconds { parts.append(String(format: "首字 %.1fs", first)) }
        if let total = state.lastAnswerSeconds { parts.append(String(format: "完成 %.1fs", total)) }
        let model = state.lastAnswerModel ?? state.currentAnswerModel
        if !model.isEmpty { parts.append(model) }
        if state.activeServiceTier == "fast" { parts.append("Fast") }
        return parts.joined(separator: " · ")
    }
}

private struct ScreenshotCard: View {
    @Environment(AppState.self) private var state
    var body: some View {
        if let image = state.screenshotPreview {
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Label("截图预览", systemImage: "viewfinder").font(.system(size: 13, weight: .semibold))
                    Spacer()
                    Button("取消") { state.clearScreenshot() }.buttonStyle(ChipButtonStyle())
                    Button("分析截图") { state.answerScreenshot() }.buttonStyle(ChipButtonStyle(prominent: true))
                }
                Image(nsImage: image).resizable().scaledToFit().frame(maxHeight: 140)
                    .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
                Text("可在下方输入框补充要求，例如“只指出 bug”。").font(.caption).foregroundStyle(.secondary)
            }
            .card()
        }
    }
}

// MARK: - Composer

private struct ComposerDock: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(spacing: 6) {
            HStack(spacing: 8) {
                // Outside the glass container: a GlassEffectContainer composites merged
                // glass above non-glass content, which would hide the text field.
                QuestionComposer()
                GlassGroup(spacing: 8) { HStack(spacing: 8) {
                    if state.answerBackend != .openAICompatible && state.codexFastAvailable {
                        FastModeToggle()
                    }
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
                } }
            }
            PrivacyNote()
        }
        .padding(.horizontal, 14).padding(.top, 12).padding(.bottom, 10)
        .background(alignment: .bottom) { ScrollEdgeFade(edge: .bottom, height: 100).ignoresSafeArea() }
    }
}

/// Codex Fast mode, switchable mid-session (applies from the next answer).
private struct FastModeToggle: View {
    @Environment(AppState.self) private var state

    var body: some View {
        Button {
            state.codexFastMode.toggle()
            state.updateSettings()
        } label: {
            Label("Fast", systemImage: state.codexFastMode ? "hare.fill" : "hare")
        }
        .buttonStyle(ChipButtonStyle(prominent: state.codexFastMode))
        .help(state.codexFastMode ? "Codex Fast 模式已开启（\(state.codexFastLabel)）· 点按关闭" : "开启 Codex Fast 模式：\(state.codexFastLabel)")
    }
}

private struct QuestionComposer: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        HStack(spacing: 8) {
            ZStack(alignment: .leading) {
                if state.questionInput.isEmpty {
                    Text(state.screenshotPreview == nil ? "输入问题，回车发送…" : "对截图的补充要求（可留空）…")
                        .font(.system(size: 14)).foregroundStyle(Color.secondary).allowsHitTesting(false)
                }
                TextField("", text: $state.questionInput, axis: .vertical)
                    .lineLimit(1...4)
                    .textFieldStyle(.plain)
                    .font(.system(size: 14))
                    .foregroundStyle(Color.primary)
            }
            .onSubmit { submit() }
            Button { submit() } label: {
                Image(systemName: "arrow.up").font(.system(size: 13, weight: .bold))
            }
            .buttonStyle(GlassIconButtonStyle(size: 28, tint: Brand.accent))
            .disabled(state.screenshotPreview == nil && state.questionInput.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
        }
        .padding(.leading, 16).padding(.trailing, 6).padding(.vertical, 6)
        .frame(minHeight: 40)
        .glassBackground(cornerRadius: 20)
    }

    private func submit() {
        if state.screenshotPreview == nil { state.answerInput() } else { state.answerScreenshot() }
    }
}

private struct PrivacyNote: View {
    @Environment(AppState.self) private var state
    var body: some View {
        HStack(spacing: 5) {
            Image(systemName: "lock.shield.fill").foregroundStyle(.green)
            Text(text).lineLimit(1)
        }
        .font(.system(size: 10.5))
        .foregroundStyle(.secondary)
    }

    private var text: String {
        let audio = state.transcriptionBackend == "cloud" ? "音频发送到 \(state.cloudProvider.title)" : "音频仅在本机识别，不保存"
        let answer = state.answerBackend == .openAICompatible ? "回答所需文本发送到所配置的 API" : "回答所需文本与截图交给 Codex"
        let record = state.recordSessions ? "转录与回答保存在本机记录" : "不保存记录"
        return [audio, answer, record].joined(separator: "；") + "。"
    }
}

extension AppState {
    /// Whether a transcript line was (part of) a detected question.
    func isQuestionText(_ text: String) -> Bool {
        questionTexts.contains { $0 == text || (text.count >= 4 && $0.count >= 4 && ($0.hasSuffix(text) || text.hasSuffix($0))) }
    }
}
