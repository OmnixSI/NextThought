import AppKit
import SwiftUI
import InterviewHackerCore

/// 会中页 (focus mode): a top bar, the live transcript on the left, the suggested
/// automatic answers or a quiet notes column on the right, and one input row.
/// See 02-scenarios-and-flows.md, "每个功能放在哪".
struct LiveSessionView: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(spacing: 0) {
            LiveTopBar()
            AutoEndBanner()
            GeometryReader { proxy in
                if proxy.size.width >= 760 {
                    HStack(spacing: 0) {
                        TranscriptColumn()
                            .frame(width: max(320, proxy.size.width * 0.46))
                        Rectangle().fill(Ink.hairline).frame(width: 1)
                        RightColumn()
                    }
                } else {
                    VStack(spacing: 0) {
                        RightColumn()
                            .frame(height: proxy.size.height * 0.55)
                        Rectangle().fill(Ink.hairline).frame(height: 1)
                        TranscriptColumn()
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

    var body: some View {
        HStack(spacing: Space.s3) {
            // Room for the window's traffic-light buttons (hidden title bar).
            Color.clear.frame(width: 62, height: 1)
            RecordingTimer()
            Text(verbatim: title)
                .font(Typo.caption).foregroundStyle(Ink.secondary)
                .lineLimit(1)
            if state.isOfflineMeeting {
                Label("现场", systemImage: "person.3").font(Typo.caption).foregroundStyle(Ink.secondary)
                    .help("线下会议：只用麦克风")
            }
            Spacer(minLength: Space.s3)
            InkSegmented(items: [(value: true, title: "自动回答"), (value: false, title: "只记录")],
                         selection: Binding(get: { state.automaticAnswers }, set: { state.setLiveMode(prompting: $0) }))
                .fixedSize()
                .help("自动回答：识别到问题后自动生成参考回答。只记录：不会自动回答；可输入问题或按 \(state.shortcutLabel(.forceAnswer)) 手动请求。")
            LiveTranslationToggle()
            HStack(spacing: Space.s1) {
                if state.insights.screenWatch { ScreenStatusIcon() }
                if state.companionEnabled { PhoneStatusIcon() }
                AnswerSettingsMenu()
                Button { TeleprompterController.shared.toggle() } label: { Image(systemName: "rectangle.topthird.inset.filled") }
                    .buttonStyle(GlassIconButtonStyle(size: 28))
                    .help("回答浮窗 · \(state.shortcutLabel(.toggleTeleprompter))")
                LiveWindowMenu()
            }
            if state.isRunning { PauseResumeButton() }
            Button("结束") { Task { await state.stopSession() } }
                .buttonStyle(SecondaryButtonStyle())
                .disabled(state.isChangingSession)
                .help("结束并整理纪要 · \(state.shortcutLabel(.toggleSession))")
        }
        .padding(.horizontal, Space.s4)
        .frame(height: 52)
        .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 1) }
    }

    private var title: String {
        if let planned = state.plannedMeeting?.title?.trimmingCharacters(in: .whitespacesAndNewlines), !planned.isEmpty { return planned }
        return state.activeWorkspace.name
    }
}

/// Passive elapsed-time and listening-state display.
private struct RecordingTimer: View {
    @Environment(AppState.self) private var state

    var body: some View {
        TimelineView(.periodic(from: .now, by: 1)) { context in
            let elapsed = state.sessionStartedAt.map { Self.elapsed(from: $0, to: context.date) }
            let status = state.isRunning ? (state.isPaused ? "已暂停" : "正在聆听") : "未在聆听"
            HStack(spacing: 6) {
                Circle()
                    .fill(state.isRunning && !state.isPaused ? Ink.live : Ink.tertiary)
                    .frame(width: 7, height: 7)
                    .accessibilityHidden(true)
                if let elapsed {
                    Text(verbatim: elapsed)
                        .font(.system(size: 13, weight: .medium).monospacedDigit())
                        .foregroundStyle(state.isPaused ? Ink.secondary : Ink.ink)
                }
                Text(verbatim: status)
                    .font(Typo.caption)
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
    @FocusState private var isKeyboardFocused: Bool
    @State private var isHovering = false

    private var actionLabel: String { state.isPaused ? "继续聆听" : "暂停聆听" }
    private var title: String { state.isPaused ? "继续" : "暂停" }

    var body: some View {
        Button { Task { await state.togglePause() } } label: {
            Label(title, systemImage: state.isPaused ? "play.fill" : "pause.fill")
        }
        .buttonStyle(SecondaryButtonStyle())
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
    }
}

/// Phone companion: only while it is on. Green dot = connected, amber = waiting.
private struct PhoneStatusIcon: View {
    @Environment(AppState.self) private var state
    var body: some View {
        Image(systemName: "iphone")
            .font(.system(size: 13))
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
            .font(.system(size: 13))
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
                    .font(Typo.label)
                if state.activeServiceTier == "fast" {
                    Image(systemName: "bolt.fill").font(.system(size: 10))
                }
            }
            .foregroundStyle(Ink.secondary)
        }
        .menuStyle(.button)
        .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 8))
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
    @State private var follow = true

    var body: some View {
        // 区分对方的每个人: people appear only once the voices have been told apart.
        let layout = state.speakers.isActive && state.entries.contains(where: { $0.voice != nil })
            ? SpeakerLayout(lines: state.liveSpeakerLines, voiceNames: state.voiceNames(for: nil), meVoices: state.speakers.meVoices) : nil
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                Text("实时对话").overline()
                Spacer()
                Button { follow.toggle() } label: {
                    Image(systemName: follow ? "arrow.down.to.line" : "pause")
                }
                .buttonStyle(GlassIconButtonStyle(size: 24))
                .help(follow ? "自动滚动到最新（点按暂停）" : "已暂停自动滚动（点按恢复）")
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
                    LazyVStack(alignment: .leading, spacing: Space.s3) {
                        if state.entries.isEmpty && state.partialTranscript.isEmpty && state.microphonePartialTranscript.isEmpty {
                            Text(state.isOfflineMeeting ? "开始说话后，房间里的声音会实时出现在这里。" : "对方和你说的话会实时出现在这里。")
                                .font(Typo.body).foregroundStyle(Ink.secondary)
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
                }
                // Keep the footer in the scroll view's measured safe area. SwiftUI
                // reserves the status strip (and any candidate rows) at their real
                // height, so the final transcript and translation can scroll above it.
                .safeAreaInset(edge: .bottom, spacing: 0) {
                    VStack(spacing: 0) {
                        CandidateStrip()
                        TranscriptStatus()
                    }
                    .background(Ink.paper)
                }
                .frame(maxHeight: .infinity)
                .onChange(of: state.entries.count) { _, _ in if follow { proxy.scrollTo("bottom", anchor: .bottom) } }
                // Final translations arrive after the transcript row was inserted;
                // follow again when they expand that row, unless the user paused follow.
                .onChange(of: state.entries.last?.translatedText) { _, _ in
                    if follow { proxy.scrollTo("bottom", anchor: .bottom) }
                }
                .onChange(of: state.partialTranscript.count / 8) { _, _ in if follow { proxy.scrollTo("bottom", anchor: .bottom) } }
                .onChange(of: state.microphonePartialTranscript.count / 8) { _, _ in if follow { proxy.scrollTo("bottom", anchor: .bottom) } }
                .onChange(of: state.liveTranslation.systemPartialTranslation) { _, _ in
                    if follow { proxy.scrollTo("bottom", anchor: .bottom) }
                }
                .onChange(of: state.liveTranslation.microphonePartialTranslation) { _, _ in
                    if follow { proxy.scrollTo("bottom", anchor: .bottom) }
                }
                .onChange(of: follow) { _, value in if value { proxy.scrollTo("bottom", anchor: .bottom) } }
            }
            .frame(maxHeight: .infinity)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
    }
}

/// Speaker label column: 对方 / 我 / 现场.
private struct SpeakerLabel: View {
    let speaker: SessionSpeaker
    /// Keeps the column (and the baseline) but shows nothing: the speaker tag above the text names the person.
    var hidden = false
    var body: some View {
        Text(speaker.displayName)
            .font(.system(size: 12, weight: speaker == .me ? .semibold : .regular))
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

    var body: some View {
        if entry.suspectedEcho == true {
            HStack(alignment: .firstTextBaseline, spacing: Space.s3) {
                SpeakerLabel(speaker: entry.speaker)
                Text(verbatim: entry.text).font(Typo.caption).foregroundStyle(Ink.tertiary).lineLimit(1)
                    .strikethrough(color: Ink.tertiary)
                Button("是我说的") { state.markNotEcho(entry.id) }
                    .buttonStyle(.tapText).font(Typo.caption).foregroundStyle(Ink.secondary)
            }
            .help("这句和对方刚说的话很像，当作扬声器回声忽略了，不参与回答。如果是你说的，点“是我说的”。")
        } else {
            let isQuestion = !entry.isMe && state.isQuestionText(entry.text)
            HStack(alignment: .firstTextBaseline, spacing: Space.s3) {
                SpeakerLabel(speaker: entry.speaker, hidden: layout?.hidesRoleLabel(for: entry.speaker) == true)
                VStack(alignment: .leading, spacing: Space.s1) {
                    if let layout, layout.hidesRoleLabel(for: entry.speaker), layout.starts.contains(entry.id) {
                        SpeakerTag(layout: layout, lineID: entry.id, speaker: entry.speaker)
                    }
                    lineText(isQuestion: isQuestion)
                    if let translation = entry.translatedText, !translation.isEmpty {
                        Text("译文：\(translation)").font(Typo.caption).foregroundStyle(Ink.ai).lineSpacing(4)
                    }
                    if hovering && entry.speaker != .me {
                        Button { state.answerEntry(entry) } label: { Label("回答这句", systemImage: "bolt") }
                            .buttonStyle(GhostButtonStyle(height: 24, horizontalPadding: 6))
                    }
                }
                .lineSpacing(5)
                .textSelection(.enabled)
                Spacer(minLength: 0)
            }
            .contentShape(Rectangle())
            .onHover { hovering = $0 }
            .animation(Motion.hover, value: hovering)
        }
    }

    /// The line, with a small neutral "问题" tag when the detector saw a question.
    private func lineText(isQuestion: Bool) -> Text {
        let body = Text(verbatim: entry.text).font(Typo.body).foregroundStyle(Ink.ink)
        guard isQuestion else { return body }
        let tag = Text("问题").font(.system(size: 11, weight: .medium)).foregroundStyle(Ink.ai)
        return tag + Text(verbatim: " ") + body
    }
}

/// The two utterances still being recognized.
private struct PartialLines: View {
    @Environment(AppState.self) private var state
    var body: some View {
        if !state.partialTranscript.isEmpty {
            line(state.partialTranscript, translation: state.liveTranslation.systemPartialTranslation, speaker: .remote)
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
                Text(verbatim: text).font(Typo.body).foregroundStyle(Ink.secondary).lineSpacing(5)
                if !translation.isEmpty {
                    Text("译文：\(translation)").font(Typo.caption).foregroundStyle(Ink.ai).lineSpacing(4)
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
        if live != nil || !suggested.isEmpty {
            VStack(alignment: .leading, spacing: Space.s1) {
                if let live { row(live, dismissible: false) }
                ForEach(suggested) { candidate in row(candidate, dismissible: true) }
            }
            .padding(.horizontal, Space.s6)
            .padding(.vertical, Space.s2)
            .overlay(alignment: .top) { Rectangle().fill(Ink.hairline).frame(height: 1) }
        }
    }

    private func row(_ candidate: QuestionCandidate, dismissible: Bool) -> some View {
        HStack(spacing: Space.s2) {
            Text("可能的问题").font(.system(size: 11, weight: .medium)).foregroundStyle(Ink.ai)
            Text(verbatim: candidate.fullText).font(Typo.caption).foregroundStyle(Ink.ink).lineLimit(1)
            Spacer(minLength: Space.s2)
            Button("回答这句") { state.answerCandidate(candidate) }.buttonStyle(GhostButtonStyle(height: 24, horizontalPadding: 6))
            if dismissible {
                Button { state.dismissCandidate(candidate) } label: { Image(systemName: "xmark") }
                    .buttonStyle(GlassIconButtonStyle(size: 22))
            }
        }
    }
}

/// "本机识别 · 1 句回声已忽略".
private struct TranscriptStatus: View {
    @Environment(AppState.self) private var state

    var body: some View {
        HStack(spacing: Space.s1) {
            Text(engine)
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
        .font(Typo.caption)
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

    var body: some View {
        // 只记录 stays quiet until an answer is asked for.
        if state.automaticAnswers || state.isAnswering || !state.displayedAnswer.isEmpty {
            AnswerColumn()
        } else {
            NotesOnlyColumn()
        }
    }
}

private struct NotesOnlyColumn: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            Text("只记录").overline()
            Text("不打扰，安静地记。结束后自动整理纪要。").font(Typo.body)
            Text(notesHint)
                .font(Typo.caption).foregroundStyle(Ink.secondary)
                .fixedSize(horizontal: false, vertical: true)
            Text("需要帮忙时按 \(state.shortcutLabel(.forceAnswer))，或在下方输入问题。")
                .font(Typo.caption).foregroundStyle(Ink.secondary)
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

/// 建议回答: header with history, the web status when it happens, the answer, three
/// rewrites plus "…", and one line saying what it was based on.
private struct AnswerColumn: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack(spacing: Space.s2) {
                AIHeading(title: state.isAnswering && state.historyCursor == nil ? "正在回答" : "建议回答")
                Spacer()
                if let position = state.historyPositionText {
                    Button { state.showPreviousAnswer() } label: { Image(systemName: "chevron.left") }
                        .buttonStyle(GlassIconButtonStyle(size: 22)).help("上一条 · \(state.shortcutLabel(.previousAnswer))")
                    Text(verbatim: position).font(.system(size: 12).monospacedDigit()).foregroundStyle(Ink.secondary)
                    Button { state.showNextAnswer() } label: { Image(systemName: "chevron.right") }
                        .buttonStyle(GlassIconButtonStyle(size: 22)).help("下一条 · \(state.shortcutLabel(.nextAnswer))")
                }
            }
            WebStatusLine()
            let question = state.displayedQuestion
            if !question.isEmpty {
                Text(verbatim: AnswerRefinement.originalQuestion(of: question))
                    .font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(2)
                    .textSelection(.enabled)
            }
            AnswerText()
            WebSources()
            RefinementRow()
            EvidenceLine()
        }
        .padding(Space.s6)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .background(Ink.promptPanel)
    }
}

/// The answer text, at "prompt" size for a glance. Only this view redraws per delta.
private struct AnswerText: View {
    @Environment(AppState.self) private var state

    var body: some View {
        ScrollViewReader { proxy in
            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    let text = state.displayedAnswer
                    if text.isEmpty {
                        Text(state.isAnswering ? "正在生成…" : "对方提问时，这里会自动给出建议回答。也可以在下方输入问题，或按 \(state.shortcutLabel(.forceAnswer)) 立即回答。")
                            .font(Typo.body).foregroundStyle(Ink.secondary)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    } else {
                        MarkdownText(text: text, size: 20, lineSpacing: 9).equatable()
                    }
                    Color.clear.frame(height: 1).id("answer-end")
                }
            }
            .frame(maxHeight: .infinity)
            .onChange(of: state.answerDraft.count / 40) { _, _ in
                if state.isAnswering, state.historyCursor == nil { proxy.scrollTo("answer-end", anchor: .bottom) }
            }
        }
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
                        .buttonStyle(GhostButtonStyle(height: 22, horizontalPadding: 6))
                case let .failed(message):
                    Text("联网没有完成").foregroundStyle(Ink.caution).help(message)
                    Button("重试") { state.lookUpDisplayedAnswer() }
                        .buttonStyle(GhostButtonStyle(height: 22, horizontalPadding: 6))
                }
                Spacer(minLength: 0)
            }
            .font(Typo.caption)
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
                        .font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                }
            }
        }
    }
}

/// 更简短 · 举例 · 英文版, the rest in "…".
private struct RefinementRow: View {
    @Environment(AppState.self) private var state
    private static let shown: [AnswerRefinement] = [.shorter, .example, .english]

    var body: some View {
        let hasAnswer = !state.displayedAnswer.isEmpty
        HStack(spacing: Space.s1) {
            ForEach(Self.shown) { item in
                Button { state.refine(item) } label: { Text(verbatim: item.displayName) }
                    .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 8))
                    .disabled(!hasAnswer || state.isAnswering)
                    .help(item == .shorter ? "把回答改短 · \(state.shortcutLabel(.refineShorter))" : "")
            }
            AnswerMoreMenu()
            Spacer(minLength: 0)
            if state.isAnswering {
                Button("停止生成") { state.cancelAnswer() }
                    .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 8))
                    .help("取消回答 · \(state.shortcutLabel(.cancelAnswer))")
            }
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
        .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 8))
        .menuIndicator(.hidden)
        .fixedSize()
        .help("更多")
    }
}

/// "依据：资料 2 · 联网 3"; amber when the answer had limits.
private struct EvidenceLine: View {
    @Environment(AppState.self) private var state
    @State private var showsDetails = false

    var body: some View {
        if let id = state.displayedRecordID,
           let evidence = state.answerCoordinator.evidenceHistory[id] ?? state.answerHistory.first(where: { $0.id == id })?.evidence {
            Button { showsDetails.toggle() } label: {
                HStack(spacing: Space.s1) {
                    if !evidence.notes.isEmpty { Image(systemName: "exclamationmark.triangle") }
                    Text(verbatim: summary(evidence))
                }
                .font(Typo.caption)
                .foregroundStyle(evidence.notes.isEmpty ? Ink.secondary : Ink.caution)
            }
            .buttonStyle(.tapText)
            .popover(isPresented: $showsDetails, arrowEdge: .bottom) {
                EvidenceDetails(evidence: evidence).frame(width: 380)
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
                Text(verbatim: evidence.retrieval).font(Typo.caption).foregroundStyle(Ink.secondary)
                ForEach(Array(evidence.materials.enumerated()), id: \.offset) { _, item in
                    VStack(alignment: .leading, spacing: 2) {
                        Text(verbatim: item.citation).font(.system(size: 12, weight: .semibold))
                        Text(verbatim: item.text).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(6)
                    }
                }
                if let screen = evidence.screen {
                    Label { Text(verbatim: screen.source) } icon: { Image(systemName: "display") }.font(Typo.caption)
                }
                ForEach(evidence.webSources, id: \.url) { source in
                    if let url = URL(string: source.url) {
                        Link(destination: url) { Text(verbatim: source.title).lineLimit(1) }.font(Typo.caption)
                    }
                }
                ForEach(evidence.notes, id: \.self) { note in
                    Label { Text(verbatim: note) } icon: { Image(systemName: "exclamationmark.triangle") }
                        .font(Typo.caption).foregroundStyle(Ink.caution)
                }
            }
            .padding(Space.s4)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .frame(maxHeight: 360)
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
                    Text("截图已就绪，可以补充要求后发送").font(Typo.caption).foregroundStyle(Ink.secondary)
                    Spacer()
                    Button("取消") { state.clearScreenshot() }.buttonStyle(GhostButtonStyle(height: 28))
                    Button("分析截图") { state.answerScreenshot() }.buttonStyle(SecondaryButtonStyle())
                }
            }
            HStack(spacing: Space.s2) {
                HStack(spacing: Space.s2) {
                    TextField("", text: $state.questionInput, prompt: placeholder, axis: .vertical)
                        .lineLimit(1...4)
                        .textFieldStyle(.plain)
                        .font(Typo.body)
                        .focused($focused)
                        .onSubmit { submit() }
                    ShortcutHint(text: "⌘L")
                }
                .padding(.horizontal, Space.s3)
                .frame(minHeight: 36)
                .background(Ink.surface, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 1))
                Menu {
                    Button("框选区域") { state.captureScreenshot(.region) }
                    Button("选择窗口") { state.captureScreenshot(.window) }
                } label: {
                    Image(systemName: "camera.viewfinder")
                }
                .menuStyle(.button)
                .buttonStyle(GlassIconButtonStyle(size: 32))
                .menuIndicator(.hidden)
                .fixedSize()
                .help("截图提问 · 区域 \(state.shortcutLabel(.captureRegion)) · 窗口 \(state.shortcutLabel(.captureWindow))")
                if state.automaticAnswers {
                    Button { state.forceAnswerCurrent() } label: {
                        HStack(spacing: 6) {
                            Image(systemName: "bolt.fill").font(.system(size: 11))
                            Text("立即回答")
                            ShortcutHint(text: state.shortcutLabel(.forceAnswer), onInk: true)
                        }
                    }
                    .buttonStyle(PrimaryButtonStyle())
                    .help("回答最新的问题或转写，不等自动判断")
                }
            }
        }
        .padding(.horizontal, Space.s4)
        .padding(.vertical, Space.s3)
        .overlay(alignment: .top) { Rectangle().fill(Ink.hairline).frame(height: 1) }
        .background {
            // ⌘L focuses the input.
            Button("") { focused = true }
                .keyboardShortcut("l", modifiers: .command)
                .opacity(0)
                .allowsHitTesting(false)
                .accessibilityHidden(true)
        }
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
