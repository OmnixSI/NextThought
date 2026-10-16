import AppKit
import InterviewHackerCore
import SwiftUI

/// Module “屏幕与讲稿”: the slide being presented with its talking points, what is
/// on the screen, and notes worth keeping (待办 / 决定 / 重点).
struct InsightsPanel: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let insights = state.insights
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                Label("屏幕与讲稿", systemImage: AssistantModule.insights.symbol).font(.system(size: 13, weight: .semibold)).lineLimit(1).fixedSize()
                Spacer()
                if insights.isReadingScreen { ProgressView().controlSize(.mini) }
                Button { state.openDecisionSettings() } label: { Image(systemName: "gearshape") }
                    .buttonStyle(GlassIconButtonStyle(size: 26)).help("设置 → 判断")
            }
            ScrollView {
                VStack(alignment: .leading, spacing: 12) {
                    if !insights.screenWatch {
                        ScreenWatchOffHint()
                    } else if let error = insights.screenError {
                        Note(error, color: .orange)
                    }
                    if let slide = insights.slide { SlideSection(slide: slide) }
                    if insights.screenWatch, insights.slide == nil || insights.screen?.kind != .slides { ScreenSection() }
                    if !insights.notes.isEmpty { NotesSection() }
                    if insights.screenWatch, insights.screen == nil, insights.slide == nil, insights.notes.isEmpty, insights.screenError == nil {
                        Note(state.isRunning ? "正在读取屏幕…" : "开始会话后，每 \(Int(insights.screenInterval)) 秒读一次屏幕。")
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
        .card()
        .frame(maxHeight: .infinity, alignment: .top)
    }
}

private struct ScreenWatchOffHint: View {
    @Environment(AppState.self) private var state
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Note("打开“屏幕感知”后，每隔几秒在本机识别屏幕文字：跟随你讲到 PPT 的哪一页并给出这一页的讲稿提示；屏幕上出现题目或代码时，判断是否和对话相关并纳入回答。屏幕文字只在本机识别。")
            Button {
                state.insights.screenWatch = true
                state.insights.save()
                state.startScreenWatch()
            } label: { Label("打开屏幕感知", systemImage: "eye") }
                .buttonStyle(ChipButtonStyle(prominent: true))
        }
    }
}

private struct SlideSection: View {
    @Environment(AppState.self) private var state
    let slide: SlidePage

    var body: some View {
        let insights = state.insights
        let notes = insights.slideNotes[slide.key]
        let covered = insights.slideCovered[slide.key] ?? []
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 6) {
                Image(systemName: "rectangle.on.rectangle").foregroundStyle(Brand.accent)
                Text("第 \(slide.page) / \(slide.pageCount) 页").font(.system(size: 13, weight: .semibold)).monospacedDigit()
                Text(slide.documentName).font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(1)
                Spacer()
                if insights.generatingNotes.contains(slide.key) { ProgressView().controlSize(.mini) }
                Button("重新生成") { state.prepareSlideNotes(slide, force: true) }
                    .buttonStyle(ChipButtonStyle()).disabled(insights.generatingNotes.contains(slide.key))
            }
            if let notes {
                ForEach(Array(notes.points.enumerated()), id: \.offset) { index, point in
                    let done = covered.contains(index)
                    let next = !done && AnswerPoints.next(after: covered, count: notes.points.count) == index
                    HStack(alignment: .firstTextBaseline, spacing: 6) {
                        Image(systemName: done ? "checkmark.circle.fill" : next ? "arrow.right.circle.fill" : "circle")
                            .font(.system(size: 11)).foregroundStyle(done ? .green : next ? Brand.accent : .secondary)
                        Text(point).font(.system(size: next ? 14 : 13, weight: next ? .semibold : .regular))
                            .foregroundStyle(done ? .secondary : .primary)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
                if !notes.transition.isEmpty {
                    Text("过渡：\(notes.transition)").font(.system(size: 12)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                }
                if !notes.questions.isEmpty {
                    Text("听众可能会问（点一下提前准备回答）").font(.system(size: 11, weight: .medium)).foregroundStyle(.secondary)
                    ForEach(notes.questions, id: \.self) { question in
                        Button { state.ask(question, screenshot: nil) } label: {
                            Label(question, systemImage: "questionmark.bubble").font(.system(size: 12)).multilineTextAlignment(.leading)
                        }
                        .buttonStyle(.plain).foregroundStyle(Brand.accent)
                    }
                }
            } else if let error = insights.slideNotesError, !insights.generatingNotes.contains(slide.key) {
                Note(error, color: .orange)
            } else {
                Note("正在根据这一页和你的资料写讲稿提示…")
            }
        }
        .padding(10)
        .background(Brand.accent.opacity(0.06), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }
}

private struct ScreenSection: View {
    @Environment(AppState.self) private var state
    @State private var expanded = false

    var body: some View {
        if let screen = state.insights.screen {
            VStack(alignment: .leading, spacing: 6) {
                HStack(spacing: 6) {
                    Image(systemName: "display").foregroundStyle(.secondary)
                    Text(screen.kind.map { "屏幕：\($0.title)" } ?? "屏幕").font(.system(size: 12, weight: .semibold))
                    Text(screen.isRelevant ? "与对话相关，回答会参考" : "与对话无关，不纳入回答")
                        .font(.system(size: 11)).foregroundStyle(screen.isRelevant ? Brand.accent : .secondary)
                    Spacer()
                    TimelineView(.periodic(from: screen.capturedAt, by: 5)) { context in
                        Text("\(Int(context.date.timeIntervalSince(screen.capturedAt))) 秒前").font(.system(size: 10)).monospacedDigit().foregroundStyle(.tertiary)
                    }
                }
                if state.insights.screenQuestionOffered {
                    Button { state.answerScreenQuestion() } label: { Label("回答屏幕上的题目", systemImage: "viewfinder") }
                        .buttonStyle(ChipButtonStyle(prominent: true))
                }
                if !screen.text.isEmpty {
                    Text(screen.text).font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(expanded ? 30 : 3).textSelection(.enabled)
                    Button(expanded ? "收起" : "展开识别的文字") { expanded.toggle() }.buttonStyle(.plain).font(.system(size: 11)).foregroundStyle(Brand.accent)
                }
                if !screen.judgedByModel {
                    Note("判断模型没有运行：按“屏幕文字是否出现在最近的对话里”判断相关性。")
                }
            }
        }
    }
}

private struct NotesSection: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let notes = state.insights.notes
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text("待办 · 决定 · 重点").font(.system(size: 12, weight: .semibold))
                Text("\(notes.count)").font(.caption.monospacedDigit()).foregroundStyle(.tertiary)
                Spacer()
                Button("复制") {
                    NSPasteboard.general.clearContents()
                    NSPasteboard.general.setString(state.insights.notesMarkdown, forType: .string)
                }
                .buttonStyle(ChipButtonStyle())
            }
            ForEach(Array(notes.suffix(30).reversed())) { note in
                HStack(alignment: .firstTextBaseline, spacing: 6) {
                    Image(systemName: note.kind.symbol).font(.system(size: 11)).foregroundStyle(Brand.accent)
                    Text(note.kind.title).font(.system(size: 11, weight: .medium)).foregroundStyle(.secondary)
                    Text(note.text).font(.system(size: 12)).fixedSize(horizontal: false, vertical: true).textSelection(.enabled)
                    Spacer(minLength: 0)
                    Text(note.isMine ? "我" : "对方").font(.system(size: 10)).foregroundStyle(.tertiary)
                }
            }
        }
    }
}

/// Under the answer: question kind, how much has been said, and the next sentence.
struct AnswerProgressStrip: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let insights = state.insights
        let kind = state.displayedRecordID.flatMap { insights.questionKinds[$0] }
        let tracking = insights.answerProgress && state.isRunning && state.historyCursor == nil && !state.isAnswering
            && insights.answerID == state.currentAnswerID && insights.answerPoints.count >= 2
        if kind != nil || tracking {
            HStack(spacing: 8) {
                if let kind { MetricPill(symbol: "tag", text: kind.title, tint: Brand.accent) }
                if tracking {
                    let covered = insights.answerCovered.count
                    MetricPill(symbol: "checkmark.circle", text: "已讲 \(min(covered, insights.answerPoints.count))/\(insights.answerPoints.count)",
                               tint: covered >= insights.answerPoints.count ? .green : .secondary)
                    if let next = insights.nextAnswerPoint {
                        Text("下一句：\(insights.answerPoints[next])").font(.system(size: 12, weight: .medium)).foregroundStyle(Brand.accent).lineLimit(1)
                    }
                }
                Spacer(minLength: 0)
            }
        }
    }
}

/// The other side is wrapping up: offer to stop and review.
struct WrapUpBanner: View {
    @Environment(AppState.self) private var state

    var body: some View {
        if state.insights.wrapUpSuggested, state.isRunning {
            HStack(spacing: 10) {
                Image(systemName: "flag.checkered").foregroundStyle(Brand.accent)
                Text("对方好像在收尾了。要停止记录并整理复盘吗？").font(.system(size: 13, weight: .medium))
                Spacer()
                Button("继续记录") { state.dismissWrapUp() }.buttonStyle(ChipButtonStyle())
                Button("停止并复盘") { state.stopAfterWrapUp() }.buttonStyle(ChipButtonStyle(prominent: true))
            }
            .padding(.horizontal, 12).padding(.vertical, 8)
            .background(Brand.accent.opacity(0.10), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
        }
    }
}
