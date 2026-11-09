import AppKit
import SwiftUI

/// Former Liquid Glass layer. The redesign ("paper & ink", docs/redesign) drops
/// glass, gradients and glows; the old names stay so call sites keep compiling, and
/// each one now draws flat paper surfaces with a hairline (see DesignSystem.swift).
enum Brand {
    /// Neutral ink: buttons, selection and navigation are not purple any more.
    static let accent = Ink.ink
    static let accent2 = Ink.ink
    /// Purple is reserved for AI-written content.
    static let ai = Ink.ai
    /// Kept for source compatibility; a solid ink fill, not a gradient.
    static let gradient = LinearGradient(colors: [Ink.ink, Ink.ink], startPoint: .top, endPoint: .bottom)
    static var hasLiquidGlass: Bool { false }
    static var useLiquidGlass: Bool { false }
}

extension View {
    /// Flat surface with a hairline border (was: glass in a rounded rectangle).
    func glassSurface(cornerRadius: CGFloat = 20, tint: Color? = nil, interactive: Bool = false) -> some View {
        modifier(FrostedFallback(shape: RoundedRectangle(cornerRadius: min(cornerRadius, Space.radiusLarge), style: .continuous), tint: tint))
    }

    /// The same surface drawn as a background layer.
    func glassBackground(cornerRadius: CGFloat = 20, tint: Color? = nil) -> some View {
        modifier(FrostedFallback(shape: RoundedRectangle(cornerRadius: min(cornerRadius, Space.radiusMedium), style: .continuous), tint: tint))
    }

    /// Small control background: a sunken rounded rectangle (was: glass capsule).
    func glassCapsule(tint: Color? = nil, interactive: Bool = false) -> some View {
        background {
            RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous)
                .fill(tint.map { $0.opacity(0.12) } ?? Ink.sunken)
        }
    }
}

/// Surface fill plus a 1 px hairline; an optional tint washes the surface lightly.
struct FrostedFallback<S: InsettableShape>: ViewModifier {
    let shape: S
    let tint: Color?
    func body(content: Content) -> some View {
        content
            .background {
                ZStack {
                    shape.fill(Ink.surface)
                    if let tint { shape.fill(tint.opacity(0.06)) }
                }
            }
            .overlay(shape.strokeBorder(Ink.hairline, lineWidth: 1))
    }
}

/// Former glass container; now just its content.
struct GlassGroup<Content: View>: View {
    var spacing: CGFloat = 12
    @ViewBuilder var content: Content
    var body: some View { content }
}

/// Window background: plain paper, no glow.
struct AppBackdrop: View {
    var body: some View {
        Ink.paper.ignoresSafeArea()
    }
}

struct VisualEffectBackground: NSViewRepresentable {
    var material: NSVisualEffectView.Material = .underWindowBackground
    func makeNSView(context: Context) -> NSVisualEffectView {
        let view = NSVisualEffectView()
        view.material = material
        view.blendingMode = .behindWindow
        view.state = .active
        return view
    }
    func updateNSView(_ view: NSVisualEffectView, context: Context) { view.material = material }
}

// MARK: Metrics

/// Shared sizes so controls line up across pages.
enum UI {
    /// Top bar of the live page and its controls.
    static let barHeight: CGFloat = 32
    /// Standard button height (primary / secondary).
    static let controlHeight: CGFloat = 32
    /// Chips and compact buttons; never below the clickable minimum (HitTargets.swift).
    static let chipHeight: CGFloat = Hit.minHeight
}

// MARK: Buttons

/// Primary action: ink fill, on-ink text, radius 6. One per area at most.
struct PrimaryButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled
    @Environment(\.liveSessionAcceptanceButtonInteraction) private var acceptanceInteraction
    var height: CGFloat = UI.controlHeight
    var scalesWithDynamicType = false
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(scalesWithDynamicType
                  ? Font.body.weight(.medium)
                  : .system(size: height >= Space.controlLarge ? 14 : 13, weight: .medium))
            .lineLimit(1)
            .foregroundStyle(isEnabled ? Ink.onInk : Ink.tertiary)
            .padding(.horizontal, height >= Space.controlLarge ? 20 : 14)
            .frame(minWidth: Hit.minHeight)
            .frame(minHeight: scalesWithDynamicType ? max(height, Hit.minHeight) : nil)
            .frame(height: scalesWithDynamicType ? nil : max(height, Hit.minHeight))
            .background(isEnabled ? Ink.ink : Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            .contentShape(RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            .opacity(configuration.isPressed || acceptanceInteraction == .pressed ? 0.85 : 1)
            .overlay {
                RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous)
                    .strokeBorder(acceptanceInteraction == .focused ? Ink.focus : Color.clear, lineWidth: 2)
                    .allowsHitTesting(false)
            }
            .animation(Motion.hover, value: configuration.isPressed)
    }
}

/// Secondary action: sunken fill, ink text.
struct SecondaryButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled
    @Environment(\.liveSessionAcceptanceButtonInteraction) private var acceptanceInteraction
    var scalesWithDynamicType = false
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(scalesWithDynamicType ? Font.body.weight(.medium) : Typo.label)
            .lineLimit(1)
            .foregroundStyle(isEnabled ? Ink.ink : Ink.tertiary)
            .padding(.horizontal, 12)
            .frame(minWidth: Hit.minHeight)
            .frame(minHeight: scalesWithDynamicType ? max(UI.controlHeight, Hit.minHeight) : nil)
            .frame(height: scalesWithDynamicType ? nil : max(UI.controlHeight, Hit.minHeight))
            .background(configuration.isPressed || acceptanceInteraction == .hover || acceptanceInteraction == .pressed
                        ? Ink.hover : Ink.sunken,
                        in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            .overlay {
                RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous)
                    .strokeBorder(acceptanceInteraction == .focused ? Ink.focus : Color.clear, lineWidth: 2)
                    .allowsHitTesting(false)
            }
            .contentShape(RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            .animation(Motion.hover, value: configuration.isPressed)
    }
}

/// Compact button; `prominent` uses the ink fill.
struct ChipButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled
    var prominent = false
    /// Kept for source compatibility (every chip is opaque now).
    var opaque = false
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 12.5, weight: .medium))
            .lineLimit(1)
            .padding(.horizontal, 10)
            .frame(minWidth: Hit.minHeight, minHeight: max(UI.chipHeight, Hit.minHeight))
            .foregroundStyle(isEnabled ? (prominent ? Ink.onInk : Ink.ink) : Ink.tertiary)
            .modifier(ChipSurface(prominent: prominent && isEnabled, pressed: configuration.isPressed))
            .contentShape(RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            .animation(Motion.hover, value: configuration.isPressed)
    }
}

/// Square icon button: no fill until hovered; `tint` draws the ink fill (an "on" state).
/// The button is never smaller than `Hit.icon`, whatever `size` asks for: the hover fill
/// shows how far the clickable area reaches.
struct GlassIconButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled
    var size: CGFloat = Hit.icon
    var tint: Color? = nil
    /// Kept for source compatibility.
    var opaque = false
    func makeBody(configuration: Configuration) -> some View {
        IconBody(configuration: configuration, isEnabled: isEnabled, size: size, on: tint != nil)
    }

    private struct IconBody: View {
        let configuration: ButtonStyleConfiguration
        let isEnabled: Bool
        let size: CGFloat
        let on: Bool
        @State private var hovering = false
        var body: some View {
            configuration.label
                .font(.system(size: 13, weight: .regular))
                .frame(width: max(size, Hit.icon), height: max(size, Hit.icon))
                .foregroundStyle(isEnabled ? (on ? Ink.onInk : Ink.secondary) : Ink.tertiary)
                .background(fill, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
                .contentShape(Rectangle())
                .onHover { hovering = $0 }
                .animation(Motion.hover, value: hovering)
        }
        private var fill: Color {
            if on && isEnabled { return Ink.ink }
            return (hovering || configuration.isPressed) && isEnabled ? Ink.hover : Color.clear
        }
    }
}

/// Type-erased button style for conditional styling.
struct AnyButtonStyle: ButtonStyle {
    private let make: (Configuration) -> AnyView
    init<S: ButtonStyle>(_ style: S) { make = { AnyView(style.makeBody(configuration: $0)) } }
    func makeBody(configuration: Configuration) -> some View { make(configuration) }
}

/// In-page tabs (series editor, meeting detail): a segmented control on a sunken track.
struct PillTabs<Value: Hashable>: View {
    let items: [(value: Value, title: String)]
    @Binding var selection: Value

    var body: some View {
        HStack(spacing: 0) {
            ForEach(Array(items.enumerated()), id: \.offset) { _, item in
                let selected = item.value == selection
                Button { selection = item.value } label: {
                    Text(LocalizedStringKey(item.title))
                        .font(Typo.label)
                        .lineLimit(1).fixedSize()
                        .foregroundStyle(selected ? Ink.ink : Ink.secondary)
                        .padding(.horizontal, 12)
                        .frame(minWidth: Hit.minHeight, minHeight: UI.chipHeight - 4)
                        .background {
                            if selected {
                                RoundedRectangle(cornerRadius: 5, style: .continuous).fill(Ink.surface)
                                    .overlay(RoundedRectangle(cornerRadius: 5, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 0.5))
                            }
                        }
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
            }
        }
        .padding(2)
        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
        .animation(Motion.expand, value: selection)
    }
}

/// Soft scroll edge: content fades into the paper under a bar.
struct ScrollEdgeFade: View {
    enum Edge { case top, bottom }
    var edge: Edge
    var height: CGFloat = 36
    var body: some View {
        LinearGradient(colors: [Ink.paper, Ink.paper.opacity(0.85), Ink.paper.opacity(0)],
                       startPoint: edge == .bottom ? .bottom : .top,
                       endPoint: edge == .bottom ? .top : .bottom)
            .frame(height: height)
            .allowsHitTesting(false)
    }
}

/// Solid ink fill for prominent actions (stays legible in non-key windows such as
/// the teleprompter).
struct ProminentFill: View {
    var body: some View {
        RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous).fill(Ink.ink)
    }
}

private struct ChipSurface: ViewModifier {
    var prominent: Bool
    var pressed: Bool = false
    func body(content: Content) -> some View {
        if prominent {
            content.background(ProminentFill())
        } else {
            content.background(pressed ? Ink.hover : Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
        }
    }
}

/// Drop-down picker built on `Menu`, with our own label so text colors follow the
/// paper theme in both appearances.
struct MenuPicker<Value: Hashable>: View {
    @Environment(\.settingsNativeControls) private var settingsNativeControls
    var title: String? = nil
    @Binding var selection: Value
    let options: [(Value, String)]
    var placeholder = "请选择"
    var localizesOptions = false

    private func optionText(_ value: String) -> Text {
        localizesOptions ? Text(LocalizedStringKey(value)) : Text(verbatim: value)
    }

    @ViewBuilder var body: some View {
        if settingsNativeControls {
            HStack(spacing: 16) {
                if let title {
                    Text(LocalizedStringKey(title)).font(Typo.label)
                    Spacer(minLength: 16)
                }
                Picker(selection: $selection) {
                    if !options.contains(where: { $0.0 == selection }) {
                        Text(LocalizedStringKey(placeholder)).tag(selection)
                    }
                    ForEach(options.indices, id: \.self) { index in
                        optionText(options[index].1).tag(options[index].0)
                    }
                } label: {
                    Text(LocalizedStringKey(title ?? placeholder))
                }
                .labelsHidden().pickerStyle(.menu)
                .disabled(options.isEmpty)
                .frame(minWidth: 140, maxWidth: 260)
                .accessibilityLabel(Text(LocalizedStringKey(title ?? placeholder)))
            }
        } else {
            legacyPicker
        }
    }

    private var legacyPicker: some View {
        HStack(spacing: 8) {
            if let title { Text(LocalizedStringKey(title)) }
            Menu {
                ForEach(options.indices, id: \.self) { index in
                    let option = options[index]
                    Button { selection = option.0 } label: {
                        if option.0 == selection {
                            Label { optionText(option.1) } icon: { Image(systemName: "checkmark") }
                        } else {
                            optionText(option.1)
                        }
                    }
                }
            } label: {
                HStack(spacing: 6) {
                    optionText(options.first(where: { $0.0 == selection })?.1 ?? placeholder)
                        .lineLimit(1)
                        .foregroundStyle(Ink.ink)
                    Image(systemName: "chevron.up.chevron.down")
                        .font(.system(size: 9, weight: .semibold))
                        .foregroundStyle(Ink.secondary)
                }
                .font(.system(size: 12.5, weight: .medium))
                .padding(.horizontal, 10).padding(.vertical, 6)
                .frame(minHeight: Hit.minHeight)
                .glassCapsule()
                .contentShape(Rectangle())
            }
            .menuStyle(.button)
            .buttonStyle(.plain)
            .menuIndicator(.hidden)
            .fixedSize()
        }
    }
}

extension View {
    /// Surface for content cards: surface fill, hairline, radius 10, no shadow.
    func cardSurface(cornerRadius: CGFloat = 10, tint: Color? = nil) -> some View {
        modifier(FrostedFallback(shape: RoundedRectangle(cornerRadius: min(cornerRadius, Space.radiusMedium), style: .continuous), tint: tint))
    }
}

/// Coalesces rapid UI updates (streaming text) to at most one per `interval`;
/// the latest value always wins and is applied when the interval elapses.
@MainActor
final class UIThrottle {
    private let interval: TimeInterval
    private var last = Date.distantPast
    private var pending: (() -> Void)?
    private var scheduled = false

    init(interval: TimeInterval) { self.interval = interval }

    func run(_ work: @escaping () -> Void) {
        let elapsed = Date().timeIntervalSince(last)
        if elapsed >= interval, !scheduled {
            last = Date()
            work()
            return
        }
        pending = work
        guard !scheduled else { return }
        scheduled = true
        DispatchQueue.main.asyncAfter(deadline: .now() + max(0.01, interval - elapsed)) { [weak self] in
            MainActor.assumeIsolated {
                guard let self else { return }
                self.scheduled = false
                self.last = Date()
                let work = self.pending
                self.pending = nil
                work?()
            }
        }
    }

    /// Drop a pending update (e.g. a final result superseded it).
    func cancel() { pending = nil }
}
