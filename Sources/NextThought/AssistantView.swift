import AppKit
import SwiftUI
import NextThoughtCore

/// Content of the former assistant modules. The live page is LiveSessionView now;
/// these views remain for the detachable module windows (DetachedModules.swift),
/// which have no entry point in the redesigned interface.
/// One module's content, docked or in its own window.
struct AssistantModuleContent: View {
    @Environment(AppState.self) private var state
    let module: AssistantModule
    var detached = false

    var body: some View {
        switch module {
        case .conversation: ConversationPanel()
        case .question:
            if detached {
                DetectionStrip(showsEmpty: true).frame(maxHeight: .infinity, alignment: .top)
            } else {
                DetectionStrip()
            }
        case .answer:
            VStack(spacing: 12) {
                ScreenshotCard()
                    .fixedSize(horizontal: false, vertical: true)
                AnswerPanel(fillHeight: !detached)
            }
        case .history: AnswerHistoryPanel()
        case .insights: InsightsPanel()
        }
    }
}

/// Every answer in this session; click one to show it in the answer module.
private struct AnswerHistoryPanel: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Label("回答记录", systemImage: "clock.arrow.circlepath").font(.system(size: 13, weight: .semibold))
                Text("\(state.answerHistory.count)").font(.caption.monospacedDigit()).foregroundStyle(.tertiary)
                Spacer()
                if state.historyCursor != nil {
                    Button("回到最新") { state.historyCursor = nil }.buttonStyle(ChipButtonStyle())
                }
            }
            if state.answerHistory.isEmpty {
                Text("回答过的问题会按时间列在这里，点一下就能回看当时的回答。")
                    .font(.system(size: 12)).foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
            } else {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 6) {
                        ForEach(Array(state.answerHistory.enumerated().reversed()), id: \.element.id) { index, record in
                            row(record, index: index)
                        }
                    }
                }
            }
        }
        .card()
        .frame(maxHeight: .infinity, alignment: .top)
    }

    private func row(_ record: AnswerRecord, index: Int) -> some View {
        let shown = (state.historyCursor ?? state.answerHistory.count - 1) == index
        let web = state.webLookups[record.id]?.status == .done
        return Button {
            state.historyCursor = index == state.answerHistory.count - 1 ? nil : index
        } label: {
            VStack(alignment: .leading, spacing: 3) {
                HStack(spacing: 6) {
                    Text(record.date.formatted(date: .omitted, time: .shortened)).font(.system(size: 10).monospacedDigit()).foregroundStyle(.tertiary)
                    if web { Image(systemName: "globe").font(.system(size: 9)).foregroundStyle(Brand.accent) }
                    if !record.automatic { Text("手动").font(.system(size: 9.5)).foregroundStyle(.tertiary) }
                }
                Text(AnswerRefinement.originalQuestion(of: record.question)).font(.system(size: 12.5, weight: .semibold)).lineLimit(2)
                Text(record.answer.replacingOccurrences(of: "**", with: "")).font(.system(size: 11.5)).foregroundStyle(.secondary).lineLimit(2)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(8)
            .background(shown ? Brand.accent.opacity(0.14) : Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
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
                    .buttonStyle(.tapText).foregroundStyle(.orange)
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
            Text("开始记录后，参与者的发言会实时出现在这里。").font(.system(size: 12))
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
                .buttonStyle(.tapText).font(.system(size: 11, weight: .medium)).foregroundStyle(Brand.accent)
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
    /// In its own window the strip stays visible with a hint instead of collapsing.
    var showsEmpty = false

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
                            .buttonStyle(.tap).foregroundStyle(.secondary)
                    }
                }
            }
            .card()
            .transition(.opacity)
        } else if showsEmpty {
            Text("对方提问时，识别到的问题和“可能的问题”会出现在这里。")
                .font(.system(size: 12)).foregroundStyle(.secondary)
                .frame(maxWidth: .infinity, alignment: .leading)
                .card()
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
            WebLookupStrip()
            AnswerProgressStrip()
            AnswerBody()
                .frame(minHeight: fillHeight ? 160 : 120, maxHeight: fillHeight ? .infinity : 340)
            WebSourcesList()
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

/// Background web lookup for the answer on screen: searching, updated (switch
/// back and forth), failed — or a button to look it up by hand.
private struct WebLookupStrip: View {
    @Environment(AppState.self) private var state

    var body: some View {
        if let lookup = state.displayedWebLookup {
            HStack(spacing: 8) {
                switch lookup.status {
                case .searching:
                    ProgressView().controlSize(.mini)
                    Text(lookup.answer.isEmpty ? "正在联网查最新信息，先看上面的快速回答…" : "正在接收联网回答…").foregroundStyle(.secondary)
                    TimelineView(.periodic(from: lookup.startedAt, by: 1)) { context in
                        Text("\(Int(context.date.timeIntervalSince(lookup.startedAt))) 秒").monospacedDigit().foregroundStyle(.tertiary)
                    }
                case .done:
                    Image(systemName: "globe").foregroundStyle(Brand.accent)
                    Text(state.showsWebAnswer ? "已显示联网核查结果" : "有核查更新，可切换查看")
                        .foregroundStyle(state.showsWebAnswer ? Brand.accent : .secondary)
                    if let seconds = lookup.seconds { Text(String(format: "%.0f 秒", seconds)).foregroundStyle(.tertiary) }
                    Spacer(minLength: 0)
                    Button(state.showsWebAnswer ? "看原回答" : "看联网版") { state.toggleWebAnswer() }.buttonStyle(ChipButtonStyle())
                case let .failed(message):
                    Image(systemName: "globe").foregroundStyle(.secondary)
                    Text("联网没有完成：\(message)").foregroundStyle(.secondary).lineLimit(1)
                    Spacer(minLength: 0)
                    Button("重试") { state.lookUpDisplayedAnswer() }.buttonStyle(ChipButtonStyle())
                }
                if lookup.status == .searching { Spacer(minLength: 0) }
            }
            .font(.system(size: 12))
            .padding(.horizontal, 10).padding(.vertical, 6)
            .background(Brand.accent.opacity(0.08), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
        }
    }
}

/// Tavily sources behind the web answer, numbered like the [1] marks in the text.
private struct WebSourcesList: View {
    @Environment(AppState.self) private var state

    var body: some View {
        if state.showsWebAnswer, let sources = state.displayedWebLookup?.sources, !sources.isEmpty {
            VStack(alignment: .leading, spacing: 3) {
                ForEach(Array(sources.prefix(6).enumerated()), id: \.offset) { index, source in
                    if let url = URL(string: source.url) {
                        Link(destination: url) {
                            Text("[\(index + 1)] \(source.title)").lineLimit(1)
                        }
                        .font(.system(size: 11)).foregroundStyle(.secondary)
                    }
                }
            }
        }
    }
}

/// Lays its subviews left to right and wraps onto a new line when the row is full.
/// The answer card uses it so a chip label is never truncated and the buttons
/// never get squeezed into an unreadable sliver.
private struct AnswerFooterWrappingHStack: Layout {
    var spacing: CGFloat = 8
    var lineSpacing: CGFloat = 6

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let maxWidth = proposal.width ?? .infinity
        let lines = lines(subviews: subviews, maxWidth: maxWidth)
        let width = lines.map(\.width).max() ?? 0
        let height = lines.reduce(0) { $0 + $1.height } + lineSpacing * CGFloat(max(0, lines.count - 1))
        return CGSize(width: min(width, maxWidth), height: height)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        let lines = lines(subviews: subviews, maxWidth: bounds.width)
        var y = bounds.minY
        for line in lines {
            var x = bounds.minX
            for item in line.items {
                subviews[item.index].place(
                    at: CGPoint(x: x, y: y + (line.height - item.size.height) / 2),
                    anchor: .topLeading,
                    proposal: ProposedViewSize(item.size)
                )
                x += item.size.width + spacing
            }
            y += line.height + lineSpacing
        }
    }

    private struct Item { let index: Int; let size: CGSize }
    private struct Line { var items: [Item] = []; var width: CGFloat = 0; var height: CGFloat = 0 }

    /// Greedy line breaking: a subview that no longer fits starts a new line.
    private func lines(subviews: Subviews, maxWidth: CGFloat) -> [Line] {
        var result: [Line] = []
        var current = Line()
        for index in subviews.indices {
            let size = subviews[index].sizeThatFits(.unspecified)
            let needed = current.items.isEmpty ? size.width : current.width + spacing + size.width
            if needed > maxWidth, !current.items.isEmpty {
                result.append(current)
                current = Line(items: [Item(index: index, size: size)], width: size.width, height: size.height)
            } else {
                current.items.append(Item(index: index, size: size))
                current.width = needed
                current.height = max(current.height, size.height)
            }
        }
        if !current.items.isEmpty { result.append(current) }
        return result
    }
}

private struct AnswerFooter: View {
    @Environment(AppState.self) private var state
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            // The action buttons wrap instead of squeezing each other, and the
            // meta line moved to its own row: that way "重新生成" / "联网查一下"
            // are never clipped and never overlap the timing text.
            AnswerFooterWrappingHStack(spacing: 8, lineSpacing: 6) {
                Button { state.copyAnswer() } label: { Label("复制", systemImage: "doc.on.doc") }
                    .buttonStyle(ChipButtonStyle()).disabled(state.displayedAnswer.isEmpty)
                    .fixedSize(horizontal: true, vertical: false)
                Button { state.regenerate() } label: { Label("重新生成", systemImage: "arrow.clockwise") }
                    .buttonStyle(ChipButtonStyle()).disabled(state.isAnswering || state.currentQuestion.isEmpty)
                    .fixedSize(horizontal: true, vertical: false)
                if state.webSearchMode != .off, state.displayedWebLookup == nil {
                    Button { state.lookUpDisplayedAnswer() } label: { Label("联网查一下", systemImage: "globe") }
                        .buttonStyle(ChipButtonStyle()).disabled(state.displayedAnswer.isEmpty || state.displayedQuestion.isEmpty)
                        .help("用联网搜索的最新信息重新回答这个问题；快速回答保留，可以切换")
                        .fixedSize(horizontal: true, vertical: false)
                }
            }
            if let id = state.displayedRecordID, let evidence = state.answerCoordinator.evidenceHistory[id] ?? state.answerHistory.first(where: { $0.id == id })?.evidence {
                AnswerEvidenceDetails(evidence: evidence).id(id)
            }
            Text(meta)
                .font(.system(size: 11)).foregroundStyle(.tertiary)
                .lineLimit(1).truncationMode(.tail)
                .frame(maxWidth: .infinity, alignment: .trailing)
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

extension AppState {
    /// Whether a transcript line was (part of) a detected question.
    func isQuestionText(_ text: String) -> Bool {
        questionTexts.contains { $0 == text || (text.count >= 4 && $0.count >= 4 && ($0.hasSuffix(text) || text.hasSuffix($0))) }
    }
}

private struct AnswerEvidenceDetails: View {
    let evidence: EvidenceBundle
    @State private var expanded = false
    private var summary: String {
        var parts = ["本次依据", "资料 \(evidence.materials.count)"]
        if evidence.screen != nil { parts.append(evidence.screenImageAttached ? "屏幕已附图" : "屏幕文字") }
        if !evidence.webSources.isEmpty { parts.append("联网 \(evidence.webSources.count)") }
        return parts.joined(separator: " · ")
    }
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            if !evidence.notes.isEmpty {
                Button { expanded = true } label: {
                    Label("本次回答有 \(evidence.notes.count) 项限制 · 查看说明", systemImage: "exclamationmark.triangle")
                        .font(.system(size: 12)).foregroundStyle(.orange)
                        .fixedSize(horizontal: false, vertical: true)
                }.buttonStyle(.tapText)
            }
            DisclosureGroup(summary, isExpanded: $expanded) {
                VStack(alignment: .leading, spacing: 8) {
                    Text(evidence.retrieval).foregroundStyle(.secondary)
                    ForEach(Array(evidence.materials.enumerated()), id: \.offset) { _, item in
                        DisclosureGroup(item.citation) {
                            Text(item.text).textSelection(.enabled).fixedSize(horizontal: false, vertical: true)
                                .frame(maxWidth: .infinity, alignment: .leading).padding(.vertical, 4)
                        }
                    }
                    if let screen = evidence.screen {
                        Label(screen.source + (evidence.screenImageAttached ? " · 已附图" : " · OCR 文字"), systemImage: "display")
                    }
                    if let search = evidence.search { Label(search.reason, systemImage: "globe") }
                    ForEach(evidence.webSources, id: \.url) { source in
                        if let url = URL(string: source.url) { Link(source.title, destination: url) }
                    }
                    ForEach(evidence.notes, id: \.self) { note in
                        Label(note, systemImage: "exclamationmark.triangle").foregroundStyle(.orange)
                    }
                }.frame(maxWidth: .infinity, alignment: .leading).padding(.top, 4)
            }.font(.system(size: 12))
        }
    }
}
