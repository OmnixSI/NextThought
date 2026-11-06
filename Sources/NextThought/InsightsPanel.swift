import AppKit
import NextThoughtCore
import SwiftUI

/// Module “屏幕与讲稿”: the slide being presented with its talking points and what is
/// on the screen.
struct InsightsPanel: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let insights = state.insights
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                Label("屏幕与讲稿", systemImage: AssistantModule.insights.symbol).font(.system(size: 13, weight: .semibold)).lineLimit(1).fixedSize()
                Spacer()
                Button("立即识别") { Task { await state.readScreen() } }.buttonStyle(ChipButtonStyle()).disabled(insights.isReadingScreen || !insights.screenWatch)
                if insights.isReadingScreen { ProgressView().controlSize(.mini) }
                Button { state.openInsightSettings() } label: { Image(systemName: "gearshape") }
                    .buttonStyle(GlassIconButtonStyle(size: 26)).help("设置 → 识别 → 屏幕感知")
            }
            ScrollView {
                VStack(alignment: .leading, spacing: 12) {
                    if !insights.screenWatch {
                        ScreenWatchOffHint()
                    } else if let error = insights.screenError {
                        Note(error, color: .orange)
                    }
                    if insights.screenWatch { Note(state.screenContextService.status + " · " + state.screenTargetTitle) }
                    if let slide = insights.slide { SlideSection(slide: slide) }
                    if insights.screenWatch, insights.slide == nil || insights.screen?.kind != .slides { ScreenSection() }
                    if insights.screenWatch, insights.screen == nil, insights.slide == nil, insights.screenError == nil {
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
            Note("打开“屏幕感知”后，每隔几秒在本机识别屏幕文字：跟随你讲到 PPT 的哪一页并给出这一页的讲稿提示；屏幕上出现题目或代码时，判断是否和对话相关并纳入回答。屏幕在本机识别；相关文字与必要的当前画面会提供给回答引擎。")
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
                        .buttonStyle(.tapText).foregroundStyle(Brand.accent)
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
                    Text("已识别 · 是否用于回答见“本次依据”")
                        .font(.system(size: 12)).foregroundStyle(.secondary)
                    Spacer()
                    TimelineView(.periodic(from: screen.capturedAt, by: 5)) { context in
                        Text("\(Int(context.date.timeIntervalSince(screen.capturedAt))) 秒前").font(.system(size: 11)).monospacedDigit().foregroundStyle(.secondary)
                    }
                }
                if !screen.text.isEmpty {
                    Text(screen.text).font(.system(size: 12)).foregroundStyle(.secondary).lineLimit(expanded ? 30 : 3).textSelection(.enabled)
                    Button(expanded ? "收起" : "展开识别的文字") { expanded.toggle() }.buttonStyle(.tapText).font(.system(size: 11)).foregroundStyle(Brand.accent)
                }
            }
        }
    }
}

/// Under the answer: how much has been said, and the next sentence.
struct AnswerProgressStrip: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let insights = state.insights
        let tracking = insights.answerProgress && state.isRunning && state.historyCursor == nil && !state.isAnswering
            && insights.answerID == state.currentAnswerID && insights.answerPoints.count >= 2
        if tracking {
            HStack(spacing: 8) {
                let covered = insights.answerCovered.count
                MetricPill(symbol: "checkmark.circle", text: "已讲 \(min(covered, insights.answerPoints.count))/\(insights.answerPoints.count)",
                           tint: covered >= insights.answerPoints.count ? .green : .secondary)
                if let next = insights.nextAnswerPoint {
                    Text("下一句：\(insights.answerPoints[next])").font(.system(size: 12, weight: .medium)).foregroundStyle(Brand.accent).lineLimit(1)
                }
                Spacer(minLength: 0)
            }
        }
    }
}
