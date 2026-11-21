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
    var height: CGFloat = UI.controlHeight
    var scalesWithDynamicType = false
    func makeBody(configuration: Configuration) -> some View {
        ControlButtonBody(configuration: configuration,
                          emphasis: configuration.role == .destructive ? .destructive : .primary,
                          height: height, horizontalPadding: height >= Space.controlLarge ? 20 : 14,
                          scalesWithDynamicType: scalesWithDynamicType)
    }
}

struct SecondaryButtonStyle: ButtonStyle {
    var scalesWithDynamicType = false
    func makeBody(configuration: Configuration) -> some View {
        ControlButtonBody(configuration: configuration, emphasis: .secondary,
                          height: UI.controlHeight, scalesWithDynamicType: scalesWithDynamicType)
    }
}

struct ChipButtonStyle: ButtonStyle {
    var prominent = false
    var opaque = false
    func makeBody(configuration: Configuration) -> some View {
        ControlButtonBody(configuration: configuration, emphasis: prominent ? .primary : .quiet,
                          height: UI.chipHeight, horizontalPadding: 10)
    }
}

struct GlassIconButtonStyle: ButtonStyle {
    var size: CGFloat = Hit.icon
    var tint: Color? = nil
    var opaque = false
    func makeBody(configuration: Configuration) -> some View {
        ControlButtonBody(configuration: configuration, emphasis: tint == nil ? .quiet : .primary,
                          height: size, iconOnly: true)
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
    var body: some View { UnderlineTabs(items: items, selection: $selection) }
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
    var accessibilityTitle: String? = nil

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
                Menu {
                    ForEach(options.indices, id: \.self) { index in
                        let option = options[index]
                        Button { selection = option.0 } label: {
                            if option.0 == selection {
                                Label { optionText(option.1) } icon: { Image(systemName: "checkmark") }
                            } else { optionText(option.1) }
                        }
                    }
                } label: {
                    HStack(spacing: 8) {
                        optionText(options.first(where: { $0.0 == selection })?.1 ?? placeholder)
                            .lineLimit(1).truncationMode(.middle)
                        Image(systemName: "chevron.down")
                            .font(.zoomed(size: 10, weight: .medium)).foregroundStyle(Ink.secondary)
                    }
                    .font(.zoomed(size: 13, weight: .medium))
                    .contentShape(RoundedRectangle(cornerRadius: ControlMetrics.radius))
                }
                .menuStyle(.button).buttonStyle(SettingsValueMenuStyle())
                .menuIndicator(.hidden)
                .disabled(options.isEmpty)
                .frame(maxWidth: 260, alignment: .trailing)
                .accessibilityLabel(Text(LocalizedStringKey(accessibilityTitle ?? title ?? placeholder)))
                .accessibilityValue(Text(options.first(where: { $0.0 == selection })?.1 ?? placeholder))
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
                    Image(systemName: "chevron.down")
                        .font(.zoomed(size: 10, weight: .medium))
                        .foregroundStyle(Ink.secondary)
                }
                .font(.zoomed(size: 13, weight: .medium))
                .contentShape(Rectangle())
            }
            .menuStyle(.button)
            .buttonStyle(SecondaryButtonStyle())
            .menuIndicator(.hidden)
            .disabled(options.isEmpty)
            .accessibilityLabel(Text(LocalizedStringKey(accessibilityTitle ?? title ?? placeholder)))
            .fixedSize()
        }
    }
}

/// Quiet value selectors: the setting stays readable; chrome appears on interaction.
private struct SettingsValueMenuStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        ControlButtonBody(configuration: configuration, emphasis: .quiet, horizontalPadding: 10)
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
