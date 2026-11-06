import SwiftUI
import NextThoughtCore

/// The sample meeting of the first-run guide: a scripted replay of a short
/// two-person call in mixed Chinese and English. It plays no audio, asks for no
/// permission and calls no model; the notes beside it are written ahead of time.
enum OnboardingSample {
    enum Speaker { case other, me }
    enum Kind { case decision, todo }

    struct Line: Identifiable {
        let id: Int
        let speaker: Speaker
        /// What was said. Not translated: it is the meeting itself.
        let text: String
        /// Seconds after the replay starts.
        let at: Double
    }

    struct Takeaway: Identifiable {
        let id: Int
        let kind: Kind
        let text: String
        let at: Double
    }

    struct Suggestion {
        let question: String
        let answer: String
        let at: Double
    }

    static let title = "Launch sync · 周四产品同步"
    static let duration: Double = 34

    static let lines: [Line] = [
        Line(id: 0, speaker: .other, text: "OK, let's start with the onboarding revamp. 上周 beta 的数据你们看了吗？", at: 0.8),
        Line(id: 1, speaker: .me, text: "看了。第一天留存 41%，比上一版高 6 个点，主要是权限那一步少掉了一半的人。", at: 5.0),
        Line(id: 2, speaker: .other, text: "That's great. 那这周能把 API Key 的步骤也简化吗？客服一半的工单都是配置问题。", at: 10.5),
        Line(id: 3, speaker: .me, text: "可以，我周三前给一版 prototype。Pricing page 要不要一起改？", at: 16.0),
        Line(id: 4, speaker: .other, text: "先不动 pricing，Q4 review 之后再说。Can you send me the draft by Friday?", at: 21.0),
        Line(id: 5, speaker: .me, text: "没问题，周五中午前发你。", at: 26.5),
        Line(id: 6, speaker: .other, text: "Perfect. 下周二我们再 sync 一下 rollout 的节奏。", at: 29.5),
    ]

    /// The notes grow while the call goes on. Written in the interface language.
    static var takeaways: [Takeaway] {
        [
            Takeaway(id: 0, kind: .decision, text: String(localized: "新手引导上线后，第一天留存 41%，比上一版高 6 个点"), at: 8.5),
            Takeaway(id: 1, kind: .decision, text: String(localized: "配置步骤是客服工单的主要来源，约占一半"), at: 14.0),
            Takeaway(id: 2, kind: .todo, text: String(localized: "我：周三前给出简化 API 步骤的原型"), at: 19.0),
            Takeaway(id: 3, kind: .decision, text: String(localized: "定价页本季度不改，Q4 复盘后再议"), at: 24.0),
            Takeaway(id: 4, kind: .todo, text: String(localized: "我：周五中午前把草稿发给对方"), at: 28.0),
            Takeaway(id: 5, kind: .todo, text: String(localized: "下周二：再对一次上线节奏"), at: 32.0),
        ]
    }

    /// The question the other side asked, and the answer the app would offer.
    static var suggestion: Suggestion {
        Suggestion(question: "那这周能把 API Key 的步骤也简化吗？",
                   answer: String(localized: "可以。做法是预设常用服务商、自动填好地址，粘贴 Key 后一键检查。周三前能给出原型。"),
                   at: 13.0)
    }
}

struct OnboardingSampleMeeting: View {
    /// True once the replay has played to its end (the guide then offers 下一步).
    @Binding var finished: Bool
    @State private var elapsed: Double = 0
    @State private var replay = 0

    private var visibleLines: Int { ScriptTimeline.visibleCount(times: OnboardingSample.lines.map(\.at), at: elapsed) }

    var body: some View {
        let takeaways = OnboardingSample.takeaways
        let visibleTakeaways = ScriptTimeline.visibleCount(times: takeaways.map(\.at), at: elapsed)
        let suggestion = OnboardingSample.suggestion
        VStack(alignment: .leading, spacing: Space.s4) {
            HStack(alignment: .top, spacing: Space.s4) {
                transcript
                    .frame(maxWidth: .infinity)
                VStack(alignment: .leading, spacing: Space.s3) {
                    if elapsed >= suggestion.at { suggestionBlock(suggestion) }
                    notes(Array(takeaways.prefix(visibleTakeaways)))
                }
                .frame(maxWidth: .infinity, alignment: .topLeading)
            }
            footer
        }
        .animation(Motion.expand, value: visibleTakeaways)
        .animation(Motion.expand, value: elapsed >= suggestion.at)
        .task(id: replay) { await play() }
    }

    // MARK: Pieces

    private var transcript: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack {
                Text("实时转写").overline()
                Spacer(minLength: 0)
                SampleTag()
            }
            ScrollViewReader { proxy in
                ScrollView(.vertical, showsIndicators: false) {
                    VStack(alignment: .leading, spacing: Space.s3) {
                        ForEach(OnboardingSample.lines.prefix(visibleLines)) { line in
                            VStack(alignment: .leading, spacing: 2) {
                                Text(LocalizedStringKey(line.speaker == .me ? "我" : "对方"))
                                    .font(Typo.caption)
                                    .foregroundStyle(line.speaker == .me ? Ink.ink : Ink.secondary)
                                Text(verbatim: line.text)
                                    .font(Typo.body)
                                    .foregroundStyle(Ink.ink)
                                    .fixedSize(horizontal: false, vertical: true)
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .id(line.id)
                            .transition(.opacity)
                        }
                    }
                    .animation(Motion.expand, value: visibleLines)
                }
                .onChange(of: visibleLines) { _, count in
                    guard count > 0 else { return }
                    withAnimation(Motion.expand) { proxy.scrollTo(OnboardingSample.lines[count - 1].id, anchor: .bottom) }
                }
            }
            .frame(height: 280)
        }
        .paperCard()
        .accessibilityElement(children: .contain)
        .accessibilityLabel("示例对话")
    }

    private func suggestionBlock(_ suggestion: OnboardingSample.Suggestion) -> some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            AIHeading(title: "对方在问你")
            Text(verbatim: suggestion.question)
                .font(Typo.body).foregroundStyle(Ink.secondary)
                .fixedSize(horizontal: false, vertical: true)
            Text(verbatim: suggestion.answer)
                .font(Typo.body).foregroundStyle(Ink.ink)
                .fixedSize(horizontal: false, vertical: true)
        }
        .aiBlock()
        .transition(.opacity)
    }

    private func notes(_ items: [OnboardingSample.Takeaway]) -> some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack {
                AIHeading(title: "实时纪要")
                Spacer(minLength: 0)
                SampleTag()
            }
            if items.isEmpty {
                Text("听到结论和待办后，会在这里一条条记下来。")
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            ForEach(items) { item in
                HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                    Text(LocalizedStringKey(item.kind == .todo ? "待办" : "结论"))
                        .font(Typo.caption).foregroundStyle(Ink.ai)
                        .frame(width: 32, alignment: .leading)
                    Text(verbatim: item.text)
                        .font(Typo.body).foregroundStyle(Ink.ink)
                        .fixedSize(horizontal: false, vertical: true)
                }
                .transition(.opacity)
            }
        }
        .aiBlock()
        .accessibilityElement(children: .contain)
        .accessibilityLabel("示例纪要")
    }

    private var footer: some View {
        HStack(spacing: Space.s3) {
            Capsule().fill(Ink.hairline).frame(height: 2)
                .overlay(alignment: .leading) {
                    GeometryReader { geometry in
                        Capsule().fill(Ink.secondary)
                            .frame(width: geometry.size.width * ScriptTimeline.progress(elapsed: elapsed, duration: OnboardingSample.duration))
                    }
                }
                .accessibilityHidden(true)
            if finished {
                Button { replay += 1 } label: { Label("重播", systemImage: "arrow.counterclockwise") }
                    .buttonStyle(GhostButtonStyle(height: 28))
            }
        }
        .frame(height: 28)
    }

    // MARK: Playback

    /// Advances the clock; cancelled with the view, so leaving the page stops it.
    @MainActor private func play() async {
        elapsed = 0
        finished = false
        let clock = ContinuousClock()
        let start = clock.now
        while !Task.isCancelled {
            let seconds = (clock.now - start) / .seconds(1)
            elapsed = min(seconds, OnboardingSample.duration)
            if seconds >= OnboardingSample.duration {
                finished = true
                break
            }
            try? await Task.sleep(for: .milliseconds(120))
        }
    }
}

/// Marks everything on the sample page as a demonstration.
struct SampleTag: View {
    var body: some View {
        Text("示例")
            .font(Typo.overline)
            .foregroundStyle(Ink.secondary)
            .padding(.horizontal, 6).padding(.vertical, 1)
            .overlay(RoundedRectangle(cornerRadius: 4, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 1))
    }
}
