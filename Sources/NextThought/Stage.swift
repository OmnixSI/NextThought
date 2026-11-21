import AppKit
import SwiftUI
import NextThoughtCore

// MARK: - Scene themes
//
// A scene is the one decision the user makes before a meeting. Each has a pigment
// from the mineral palette of ink painting (indigo, bamboo green, wisteria, ochre,
// azurite, slate, pine, rouge): muted, close in weight, at home on paper. A pigment
// always means "this kind of occasion": on the home page, in the live bar and along
// the edge of the cue island.

struct SceneTheme {
    let tint: Color
    /// The darker end of the glyph gradient.
    let deep: Color
    let symbol: String
    /// What the scene does for you, in one line.
    let promise: String
    /// Three things it sets up, shown when the scene is picked.
    let moments: [String]
}

extension SessionMode {
    var theme: SceneTheme {
        switch self {
        case .interview:
            SceneTheme(tint: .dynamic(light: .hex(0x3f5b8c), dark: .hex(0x93aee0)), deep: .dynamic(light: .hex(0x2c4370), dark: .hex(0x6f8cc4)),
                       symbol: "person.crop.rectangle",
                       promise: String(localized: "被问到时，答案就在镜头旁"),
                       moments: [String(localized: "听到问题自动给出回答"), String(localized: "提词贴着摄像头，不用低头"), String(localized: "会后逐题复盘，存进题库")])
        case .labMeeting:
            SceneTheme(tint: .dynamic(light: .hex(0x4a7d68), dark: .hex(0x8cc7ad)), deep: .dynamic(light: .hex(0x355e4d), dark: .hex(0x66a88c)),
                       symbol: "flask",
                       promise: String(localized: "专心讨论，导师的话一句不漏"),
                       moments: [String(localized: "默认只记录，被问住再求助"), String(localized: "纪要按进展、意见、下一步整理"), String(localized: "鱼骨图梳理卡住的问题")])
        case .academic:
            SceneTheme(tint: .dynamic(light: .hex(0x775a9c), dark: .hex(0xb9a0e0)), deep: .dynamic(light: .hex(0x5a417c), dark: .hex(0x957bc2)),
                       symbol: "graduationcap",
                       promise: String(localized: "跟着讲稿走，问答环节有底"),
                       moments: [String(localized: "认出当前幻灯片，提示这一页要点"), String(localized: "听众提问时自动给出回答"), String(localized: "纪要记下被问了什么、怎么答更好")])
        case .groupProject:
            SceneTheme(tint: .dynamic(light: .hex(0xa5683c), dark: .hex(0xe0a476)), deep: .dynamic(light: .hex(0x824e28), dark: .hex(0xc28555)),
                       symbol: "person.3",
                       promise: String(localized: "谁做什么、哪天交，散会就清楚"),
                       moments: [String(localized: "记下每个人领到的任务"), String(localized: "意见不一致时帮你找折中"), String(localized: "纪要按方案、分工、截止时间整理")])
        case .client:
            SceneTheme(tint: .dynamic(light: .hex(0x2f7a8c), dark: .hex(0x7cc3d4)), deep: .dynamic(light: .hex(0x1f5c6b), dark: .hex(0x57a3b5)),
                       symbol: "person.2",
                       promise: String(localized: "听懂需求，答应的事不落空"),
                       moments: [String(localized: "客户提问时给出有依据的回应"), String(localized: "价格和承诺以资料为准"), String(localized: "纪要列出需求、顾虑和我们答应的事")])
        case .meeting:
            SceneTheme(tint: .dynamic(light: .hex(0x5b6573), dark: .hex(0xa9b3c1)), deep: .dynamic(light: .hex(0x434c58), dark: .hex(0x8691a1)),
                       symbol: "briefcase",
                       promise: String(localized: "决定、待办、风险，会后即得"),
                       moments: [String(localized: "默认只记录，不打扰"), String(localized: "需要时一键求助"), String(localized: "纪要按决定、待办、风险整理")])
        case .lesson:
            SceneTheme(tint: .dynamic(light: .hex(0x5f7d40), dark: .hex(0xa8cb85)), deep: .dynamic(light: .hex(0x475f2d), dark: .hex(0x86ac63)),
                       symbol: "book",
                       promise: String(localized: "只管听，笔记自己长出来"),
                       moments: [String(localized: "跟着课件记录要点"), String(localized: "没听懂的地方随时问"), String(localized: "笔记按提纲、概念、疑问整理")])
        case .discussion:
            SceneTheme(tint: .dynamic(light: .hex(0xa5526b), dark: .hex(0xe395ad)), deep: .dynamic(light: .hex(0x813a51), dark: .hex(0xc4748d)),
                       symbol: "bubble.left.and.bubble.right",
                       promise: String(localized: "各方观点摆清楚，结论自然来"),
                       moments: [String(localized: "区分每个人说了什么"), String(localized: "观点对照表一眼看清分歧"), String(localized: "纪要按议题、观点、结论整理")])
        case .general, .custom:
            SceneTheme(tint: .dynamic(light: .hex(0x6b6862), dark: .hex(0xb5b2ab)), deep: .dynamic(light: .hex(0x4d4a45), dark: .hex(0x93908a)),
                       symbol: self == .custom ? "slider.horizontal.3" : "sparkles",
                       promise: String(localized: "先录下来，其他的之后再说"),
                       moments: [String(localized: "只记录，需要时求助"), String(localized: "回答简短"), String(localized: "纪要按总结、决定、待办整理")])
        }
    }
}

/// The scene's mark: its symbol drawn in its pigment on a faint wash of the same.
struct SceneGlyph: View {
    let mode: SessionMode
    var size: CGFloat = 40

    var body: some View {
        let theme = mode.theme
        RoundedRectangle(cornerRadius: size * 0.3, style: .continuous)
            .fill(theme.tint.opacity(0.13))
            .overlay {
                Image(systemName: theme.symbol)
                    .font(.zoomed(size: size * 0.44, weight: .medium))
                    .foregroundStyle(theme.tint)
            }
            .frame(width: size, height: size)
            .accessibilityHidden(true)
    }
}

/// A small scene tag: pigment dot and name ("● 面试").
struct SceneTag: View {
    let mode: SessionMode
    var name: String? = nil
    var body: some View {
        HStack(spacing: 6) {
            Circle().fill(mode.theme.tint).frame(width: 6, height: 6)
            Text(verbatim: name ?? mode.displayName).font(Typo.caption).foregroundStyle(Ink.secondary)
        }
        .accessibilityElement(children: .combine)
    }
}

// MARK: - Ink wash

/// Pigment bleeding into wet paper at the top of a page: two soft blooms in the
/// scene's colour that breathe slowly. Still under Reduce Motion.
struct InkWash: View {
    var tint: Color
    var animated = true

    var body: some View {
        TimelineView(.animation(minimumInterval: 1 / 20, paused: !animated || Motion.reduce)) { context in
            let time = context.date.timeIntervalSinceReferenceDate
            Canvas { canvas, size in
                canvas.addFilter(.blur(radius: 80))
                let drift = CGFloat(sin(time * 0.18)) * 18
                canvas.fill(Path(ellipseIn: CGRect(x: size.width * 0.58 + drift, y: -120, width: size.width * 0.44, height: 250)),
                            with: .color(tint.opacity(0.2)))
                canvas.fill(Path(ellipseIn: CGRect(x: size.width * 0.08 - drift * 0.6, y: -170, width: size.width * 0.32, height: 230)),
                            with: .color(tint.opacity(0.09)))
            }
        }
        // No edge where the wash meets the title row or the page below it.
        .mask(LinearGradient(stops: [.init(color: .clear, location: 0), .init(color: .black, location: 0.16),
                                     .init(color: .black, location: 0.55), .init(color: .clear, location: 1)],
                             startPoint: .top, endPoint: .bottom))
        .allowsHitTesting(false)
        .accessibilityHidden(true)
    }
}

// MARK: - Lift

/// A card that rises to meet the pointer and settles under a press.
struct LiftButtonStyle: ButtonStyle {
    var cornerRadius: CGFloat = Space.radiusMedium
    var glow: Color? = nil

    func makeBody(configuration: Configuration) -> some View {
        Lift(configuration: configuration, cornerRadius: cornerRadius, glow: glow)
    }

    private struct Lift: View {
        let configuration: ButtonStyleConfiguration
        let cornerRadius: CGFloat
        let glow: Color?
        @Environment(\.isEnabled) private var isEnabled
        @Environment(\.isFocused) private var isFocused
        @State private var hovering = false

        var body: some View {
            let raised = hovering && isEnabled && !configuration.isPressed
            configuration.label
                .background {
                    RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                        .fill(Ink.surface)
                        .shadow(color: Ink.contactShadow, radius: 1, x: 0, y: 1)
                        .shadow(color: raised ? (glow ?? Ink.ink).opacity(glow == nil ? 0.1 : 0.22) : Ink.ambientShadow,
                                radius: raised ? 22 : 12, x: 0, y: raised ? 14 : 6)
                }
                .overlay {
                    RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                        .strokeBorder(raised ? (glow ?? Ink.ink).opacity(0.35) : Ink.hairline, lineWidth: raised ? 1 : 0.5)
                }
                .overlay {
                    RoundedRectangle(cornerRadius: cornerRadius + 3, style: .continuous)
                        .stroke(isFocused ? Ink.focus : .clear, lineWidth: ControlMetrics.focusWidth)
                        .padding(-3).allowsHitTesting(false)
                }
                .contentShape(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
                .scaleEffect(configuration.isPressed ? 0.975 : 1)
                .offset(y: raised ? -2 : 0)
                .onHover { hovering = $0 }
                .animation(Motion.snappy, value: raised)
                .animation(Motion.snappy, value: configuration.isPressed)
        }
    }
}

// MARK: - Live voice meter

/// The two voices of a meeting as a living waveform: the other side in the scene
/// colour, the user in ink. Reads the audio threads' latest levels on a timeline,
/// so audio never drives SwiftUI state.
struct VoiceWave: View {
    @Environment(AppState.self) private var state
    var tint: Color = Ink.secondary
    var bars = 5
    var height: CGFloat = 16
    var active = true

    var body: some View {
        TimelineView(.animation(minimumInterval: 1 / 20, paused: !active || Motion.reduce)) { context in
            let levels = state.echoMonitor.levels
            let level = CGFloat(max(levels.system, levels.microphone))
            let mine = levels.microphone > levels.system
            let time = context.date.timeIntervalSinceReferenceDate
            Canvas { canvas, size in
                let spacing: CGFloat = 2.5
                let width = (size.width - spacing * CGFloat(bars - 1)) / CGFloat(bars)
                for index in 0..<bars {
                    // Each bar breathes on its own phase so the meter never looks like a stamp.
                    let wobble = 0.55 + 0.45 * sin(time * 9 + Double(index) * 1.9)
                    let barHeight = max(3, size.height * min(1, level * 1.6) * CGFloat(wobble))
                    let rect = CGRect(x: CGFloat(index) * (width + spacing), y: (size.height - barHeight) / 2,
                                      width: width, height: barHeight)
                    canvas.fill(Path(roundedRect: rect, cornerRadius: width / 2),
                                with: .color(active ? (mine ? Ink.ink : tint) : Ink.tertiary))
                }
            }
        }
        .frame(width: CGFloat(bars) * 3 + CGFloat(bars - 1) * 2.5, height: height)
        .accessibilityHidden(true)
    }
}
