import AppKit
import SwiftUI

/// InterviewHacker design system v2 (see the "InterviewHacker" design system artifact).
/// Calm, native macOS: solid neutral surfaces with hairline borders, one solid brand
/// purple for the single primary action and for selection, rounded-rectangle controls.
/// No glass, no gradients, no shadows on cards. The type and view names below are kept
/// from the earlier Liquid Glass layer so every call site keeps compiling.
enum Brand {
    /// Accent for selected icons, links and markers (text-safe on bg and surface).
    static let accent = Color.dynamic(light: 0x5146C8, dark: 0xBDB3FF)
    /// Fill of the primary action, under white text.
    static let primary = Color.dynamic(light: 0x5146C8, dark: 0x5754D8)
    /// Kept for source compatibility; the brand no longer uses a second hue.
    static let accent2 = primary
    /// Kept for source compatibility: a flat fill of `primary` (gradients are retired).
    static let gradient = LinearGradient(colors: [primary, primary], startPoint: .topLeading, endPoint: .bottomTrailing)
    /// Selected rows, current scenario, selected history item.
    static let accentSoft = Color.dynamic(light: 0x5146C8, dark: 0xBDB3FF, lightAlpha: 0.10, darkAlpha: 0.14)
    static let background = Color.dynamic(light: 0xF5F5F9, dark: 0x141419)
    static let surface = Color.dynamic(light: 0xFFFFFF, dark: 0x1E1F27)
    static let sunken = Color.dynamic(light: 0x000000, dark: 0xFFFFFF, lightAlpha: 0.04, darkAlpha: 0.05)
    static let hover = Color.dynamic(light: 0x000000, dark: 0xFFFFFF, lightAlpha: 0.06, darkAlpha: 0.08)
    static let border = Color.dynamic(light: 0xE3E3EC, dark: 0x2F2F3A)

    static var hasLiquidGlass: Bool {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) { return true }
        #endif
        return false
    }
    /// v2 draws no glass. Kept so existing checks still compile.
    static var useLiquidGlass: Bool { false }
}

extension Color {
    /// A color that follows the view's light / dark appearance.
    static func dynamic(light: UInt32, dark: UInt32, lightAlpha: CGFloat = 1, darkAlpha: CGFloat = 1) -> Color {
        func make(_ hex: UInt32, _ alpha: CGFloat) -> NSColor {
            NSColor(srgbRed: CGFloat((hex >> 16) & 0xFF) / 255, green: CGFloat((hex >> 8) & 0xFF) / 255,
                    blue: CGFloat(hex & 0xFF) / 255, alpha: alpha)
        }
        return Color(nsColor: NSColor(name: nil) { appearance in
            appearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua ? make(dark, darkAlpha) : make(light, lightAlpha)
        })
    }
}

extension View {
    /// Solid surface in a rounded rectangle (was glass).
    func glassSurface(cornerRadius: CGFloat = 14, tint: Color? = nil, interactive: Bool = false) -> some View {
        modifier(SolidSurface(shape: RoundedRectangle(cornerRadius: min(cornerRadius, 14), style: .continuous), tint: tint))
    }

    /// Solid surface drawn as a background layer, for containers holding regular controls.
    func glassBackground(cornerRadius: CGFloat = 14, tint: Color? = nil) -> some View {
        modifier(SolidSurface(shape: RoundedRectangle(cornerRadius: min(cornerRadius, 14), style: .continuous), tint: tint))
    }

    /// Small control surface (was a glass capsule): a quiet sunken fill, 8 pt corners.
    func glassCapsule(tint: Color? = nil, interactive: Bool = false) -> some View {
        background {
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .fill(tint.map { $0.opacity(0.14) } ?? Brand.sunken)
        }
    }
}

/// Card surface: solid `surface` with a hairline `border`. An optional tint washes it lightly.
struct SolidSurface<S: InsettableShape>: ViewModifier {
    let shape: S
    let tint: Color?
    func body(content: Content) -> some View {
        content
            .background {
                ZStack {
                    shape.fill(Brand.surface)
                    if let tint { shape.fill(tint.opacity(0.10)) }
                }
            }
            .overlay(shape.strokeBorder(Brand.border, lineWidth: 1))
    }
}

/// Groups nearby controls. v2 has no glass to merge, so this is a plain container.
struct GlassGroup<Content: View>: View {
    var spacing: CGFloat = 12
    @ViewBuilder var content: Content
    var body: some View { content }
}

/// Window background: the plain neutral `bg`. Opaque on purpose (see git history).
struct AppBackdrop: View {
    var body: some View {
        Brand.background.ignoresSafeArea()
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

/// Shared sizes so controls line up across pages (design tokens `control-height`, `control-height-sm`).
enum UI {
    /// Controls in the top bar.
    static let barHeight: CGFloat = 32
    /// Standard button height (primary / secondary).
    static let controlHeight: CGFloat = 32
    /// Small buttons inside cards.
    static let chipHeight: CGFloat = 26
    /// Corner radius of buttons, fields and inset rows (`radius-md`).
    static let controlRadius: CGFloat = 8
    /// Corner radius of cards (`radius-lg`).
    static let cardRadius: CGFloat = 14
}

// MARK: Buttons

private struct ControlShape: Shape {
    func path(in rect: CGRect) -> Path { RoundedRectangle(cornerRadius: UI.controlRadius, style: .continuous).path(in: rect) }
}

/// Tracks hover for button styles (a ButtonStyle itself cannot hold state).
private struct HoverBackground<Fill: View>: View {
    var enabled: Bool
    @ViewBuilder var fill: (Bool) -> Fill
    @State private var hovering = false
    var body: some View {
        fill(hovering && enabled).onHover { hovering = $0 }
    }
}

/// The one primary action of an area: solid `primary`, white semibold label.
struct PrimaryButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled
    var height: CGFloat = UI.controlHeight
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 13, weight: .semibold))
            .foregroundStyle(Color.white)
            .padding(.horizontal, 14)
            .frame(height: height)
            .background {
                HoverBackground(enabled: isEnabled) { hover in
                    ControlShape().fill(Brand.primary).brightness(configuration.isPressed ? -0.06 : (hover ? 0.05 : 0))
                }
            }
            .opacity(isEnabled ? 1 : 0.4)
            .contentShape(ControlShape())
    }
}

/// Secondary action: quiet sunken fill, regular label.
struct SecondaryButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 13, weight: .medium))
            .foregroundStyle(.primary)
            .padding(.horizontal, 14)
            .frame(height: UI.controlHeight)
            .background {
                HoverBackground(enabled: isEnabled) { hover in
                    ControlShape().fill(configuration.isPressed || hover ? Brand.hover : Brand.sunken)
                }
            }
            .opacity(isEnabled ? 1 : 0.4)
            .contentShape(ControlShape())
    }
}

/// Small button inside cards. `prominent` uses the primary fill; `opaque` is kept for
/// source compatibility (v2 chips are always opaque).
struct ChipButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled
    var prominent = false
    var opaque = false
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 12, weight: prominent ? .semibold : .medium))
            .lineLimit(1)
            .padding(.horizontal, 10)
            .frame(minHeight: UI.chipHeight)
            .foregroundStyle(prominent && isEnabled ? Color.white : Color.primary)
            .background {
                HoverBackground(enabled: isEnabled) { hover in
                    if prominent && isEnabled { ControlShape().fill(Brand.primary) }
                    else if opaque { ControlShape().fill(Brand.surface).overlay(ControlShape().stroke(Brand.border, lineWidth: 1)) }
                    else { ControlShape().fill(configuration.isPressed || hover ? Brand.hover : Brand.sunken) }
                }
            }
            .opacity(isEnabled ? 1 : 0.4)
            .contentShape(ControlShape())
    }
}

/// Icon-only button: transparent until hovered. `tint` makes it a filled primary action.
struct GlassIconButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled
    var size: CGFloat = UI.controlHeight
    var tint: Color? = nil
    var opaque = false
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 14, weight: .medium))
            .frame(width: size, height: size)
            .foregroundStyle(tint != nil && isEnabled ? Color.white : Color.primary)
            .background {
                HoverBackground(enabled: isEnabled) { hover in
                    if tint != nil && isEnabled { ControlShape().fill(Brand.primary) }
                    else if opaque { ControlShape().fill(Brand.surface).overlay(ControlShape().stroke(Brand.border, lineWidth: 1)) }
                    else { ControlShape().fill(configuration.isPressed || hover ? Brand.hover : Color.clear) }
                }
            }
            .opacity(isEnabled ? 1 : 0.4)
            .contentShape(ControlShape())
    }
}

/// Type-erased button style for conditional styling.
struct AnyButtonStyle: ButtonStyle {
    private let make: (Configuration) -> AnyView
    init<S: ButtonStyle>(_ style: S) { make = { AnyView(style.makeBody(configuration: $0)) } }
    func makeBody(configuration: Configuration) -> some View { make(configuration) }
}

/// Segmented control: a sunken track; the selected item is a raised `surface` block.
private struct SegmentedTrack<Content: View>: View {
    @ViewBuilder var content: Content
    var body: some View {
        HStack(spacing: 2) { content }
            .padding(2)
            .background(RoundedRectangle(cornerRadius: UI.controlRadius, style: .continuous).fill(Brand.sunken))
    }
}

private struct SegmentIndicator: View {
    var body: some View {
        RoundedRectangle(cornerRadius: 6, style: .continuous)
            .fill(Brand.surface)
            .shadow(color: .black.opacity(0.12), radius: 1, y: 1)
    }
}

/// In-page tabs (场景 editor, 记录 detail).
struct PillTabs<Value: Hashable>: View {
    let items: [(value: Value, title: String)]
    @Binding var selection: Value
    @Namespace private var namespace

    var body: some View {
        SegmentedTrack {
            ForEach(Array(items.enumerated()), id: \.offset) { _, item in
                let selected = item.value == selection
                Button { selection = item.value } label: {
                    Text(item.title)
                        .font(.system(size: 13, weight: selected ? .semibold : .medium))
                        .lineLimit(1).fixedSize()
                        .foregroundStyle(selected ? Color.primary : Color.secondary)
                        .padding(.horizontal, 14)
                        .frame(height: UI.controlHeight - 4)
                        .background {
                            if selected { SegmentIndicator().matchedGeometryEffect(id: "pill", in: namespace) }
                        }
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
            }
        }
        .animation(.easeOut(duration: 0.15), value: selection)
    }
}

/// Main section switcher in the top bar.
struct GlassTabBar<Tab: Hashable & Identifiable>: View {
    let tabs: [Tab]
    @Binding var selection: Tab
    let title: (Tab) -> String
    let symbol: (Tab) -> String
    /// Icons only (narrow windows); titles move to the tooltip.
    var showsTitles = true
    @Namespace private var namespace

    var body: some View {
        SegmentedTrack {
            ForEach(tabs) { tab in
                let selected = tab == selection
                Button { selection = tab } label: {
                    HStack(spacing: 6) {
                        Image(systemName: symbol(tab))
                            .font(.system(size: 13, weight: .medium))
                            .foregroundStyle(selected ? Brand.accent : Color.secondary)
                        if showsTitles {
                            Text(title(tab))
                                .font(.system(size: 13, weight: selected ? .semibold : .medium))
                                .foregroundStyle(selected ? Color.primary : Color.secondary)
                        }
                    }
                    .lineLimit(1).fixedSize()
                    .padding(.horizontal, showsTitles ? 14 : 10)
                    .frame(height: UI.barHeight - 4)
                    .background {
                        if selected { SegmentIndicator().matchedGeometryEffect(id: "tab", in: namespace) }
                    }
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .help(title(tab))
            }
        }
        .animation(.easeOut(duration: 0.15), value: selection)
    }
}

/// Content fades out under the top bar instead of colliding with it.
struct ScrollEdgeFade: View {
    enum Edge { case top, bottom }
    var edge: Edge
    var height: CGFloat = 36
    var body: some View {
        LinearGradient(colors: [Brand.background, Brand.background.opacity(0.85), Brand.background.opacity(0)],
                       startPoint: edge == .bottom ? .bottom : .top,
                       endPoint: edge == .bottom ? .top : .bottom)
            .frame(height: height)
            .allowsHitTesting(false)
    }
}

/// Solid primary fill for prominent actions (was a gradient capsule).
struct ProminentFill: View {
    var body: some View {
        RoundedRectangle(cornerRadius: UI.controlRadius, style: .continuous).fill(Brand.primary)
    }
}

/// Drop-down picker built on `Menu`. The label reads like text and shows a fill on hover,
/// so a row of pickers doesn't look like a row of buttons.
struct MenuPicker<Value: Hashable>: View {
    var title: String? = nil
    @Binding var selection: Value
    let options: [(Value, String)]
    var placeholder = "请选择"
    @State private var hovering = false

    var body: some View {
        HStack(spacing: 8) {
            if let title { Text(title).foregroundStyle(.secondary) }
            Menu {
                ForEach(options.indices, id: \.self) { index in
                    let option = options[index]
                    Button { selection = option.0 } label: {
                        if option.0 == selection { Label(option.1, systemImage: "checkmark") } else { Text(option.1) }
                    }
                }
            } label: {
                HStack(spacing: 5) {
                    Text(options.first(where: { $0.0 == selection })?.1 ?? placeholder)
                        .lineLimit(1)
                        .foregroundStyle(Color.primary)
                    Image(systemName: "chevron.up.chevron.down")
                        .font(.system(size: 9, weight: .semibold))
                        .foregroundStyle(Color.secondary)
                }
                .font(.system(size: 13, weight: .medium))
                .padding(.horizontal, 8)
                .frame(height: UI.chipHeight)
                .background(RoundedRectangle(cornerRadius: UI.controlRadius, style: .continuous).fill(hovering ? Brand.hover : Color.clear))
                .contentShape(Rectangle())
            }
            .menuStyle(.button)
            .buttonStyle(.plain)
            .menuIndicator(.hidden)
            .fixedSize()
            .onHover { hovering = $0 }
        }
    }
}

extension View {
    /// Surface for content cards: solid `surface`, hairline `border`, 14 pt corners.
    func cardSurface(cornerRadius: CGFloat = UI.cardRadius, tint: Color? = nil) -> some View {
        modifier(SolidSurface(shape: RoundedRectangle(cornerRadius: min(cornerRadius, UI.cardRadius), style: .continuous), tint: tint))
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
