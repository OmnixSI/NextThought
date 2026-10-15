import AppKit
import SwiftUI

/// Liquid Glass design system. On macOS 26 this uses the system `glassEffect`
/// material (refraction, specular highlights, interactive response); on macOS 14–15
/// it falls back to vibrant frosted materials with a light rim so the layout and
/// hierarchy stay identical.
enum Brand {
    static let accent = Color(red: 0.40, green: 0.42, blue: 1.0)
    static let accent2 = Color(red: 0.62, green: 0.32, blue: 0.98)
    static let gradient = LinearGradient(colors: [accent, accent2], startPoint: .topLeading, endPoint: .bottomTrailing)
    static var hasLiquidGlass: Bool {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) { return true }
        #endif
        return false
    }
    /// Real glass is GPU-heavy (refraction is re-rendered per frame). It is skipped when
    /// the user asks for fewer effects or the system "Reduce transparency" is on.
    static var useLiquidGlass: Bool {
        hasLiquidGlass && !UserDefaults.standard.bool(forKey: "reducedEffects")
            && !NSWorkspace.shared.accessibilityDisplayShouldReduceTransparency
    }
}

extension View {
    /// Glass surface in a rounded rectangle.
    @ViewBuilder
    func glassSurface(cornerRadius: CGFloat = 20, tint: Color? = nil, interactive: Bool = false) -> some View {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *), Brand.useLiquidGlass {
            self.glassEffect(Self.glass(tint: tint, interactive: interactive), in: .rect(cornerRadius: cornerRadius))
        } else {
            self.modifier(FrostedFallback(shape: RoundedRectangle(cornerRadius: cornerRadius, style: .continuous), tint: tint))
        }
        #else
        self.modifier(FrostedFallback(shape: RoundedRectangle(cornerRadius: cornerRadius, style: .continuous), tint: tint))
        #endif
    }

    /// Glass drawn as a background layer. Use for containers holding regular
    /// controls (pickers, text fields): content inside `glassEffect` is rendered
    /// with glass vibrancy, which makes system pop-up menus unreadable in light mode.
    func glassBackground(cornerRadius: CGFloat = 20, tint: Color? = nil) -> some View {
        background {
            RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                .fill(Color.clear)
                .glassSurface(cornerRadius: cornerRadius, tint: tint)
        }
    }

    /// Glass surface in a capsule (pills, chips, tab bars).
    @ViewBuilder
    func glassCapsule(tint: Color? = nil, interactive: Bool = false) -> some View {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *), Brand.useLiquidGlass {
            self.glassEffect(Self.glass(tint: tint, interactive: interactive), in: .capsule)
        } else {
            self.modifier(FrostedFallback(shape: Capsule(), tint: tint))
        }
        #else
        self.modifier(FrostedFallback(shape: Capsule(), tint: tint))
        #endif
    }

    #if compiler(>=6.2)
    @available(macOS 26.0, *)
    fileprivate static func glass(tint: Color?, interactive: Bool) -> Glass {
        let base = Glass.regular.tint(tint)
        return interactive ? base.interactive() : base
    }
    #endif
}

struct FrostedFallback<S: InsettableShape>: ViewModifier {
    let shape: S
    let tint: Color?
    @Environment(\.colorScheme) private var scheme
    func body(content: Content) -> some View {
        content
            // Flat translucent fill + rim: looks glassy, costs no blur or shadow pass.
            .background {
                ZStack {
                    shape.fill(Color(nsColor: .controlBackgroundColor).opacity(scheme == .dark ? 0.72 : 0.82))
                    if let tint { shape.fill(tint.opacity(0.22)) }
                    shape.fill(LinearGradient(colors: [.white.opacity(scheme == .dark ? 0.05 : 0.3), .clear], startPoint: .top, endPoint: .center))
                }
            }
            .overlay(shape.strokeBorder(LinearGradient(colors: [.white.opacity(scheme == .dark ? 0.18 : 0.8), .white.opacity(0.04)], startPoint: .top, endPoint: .bottom), lineWidth: 0.8))
    }
}

/// Groups nearby glass shapes so they blend and morph together on macOS 26.
struct GlassGroup<Content: View>: View {
    var spacing: CGFloat = 12
    @ViewBuilder var content: Content
    var body: some View {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *), Brand.useLiquidGlass {
            GlassEffectContainer(spacing: spacing) { content }
        } else {
            content
        }
        #else
        content
        #endif
    }
}

/// Window backdrop with soft brand light, so glass has something to refract.
/// Deliberately opaque: macOS 26 glass controls pick their text color from what is
/// behind them, and a see-through window would make that depend on the wallpaper.
struct AppBackdrop: View {
    @Environment(\.colorScheme) private var scheme
    var body: some View {
        ZStack {
            Color(nsColor: .windowBackgroundColor)
            // Radial gradients instead of large blurred shapes: same soft glow,
            // no full-window Gaussian blur on every redraw or resize.
            GeometryReader { proxy in
                let size = proxy.size
                let radius = max(size.width, size.height)
                ZStack {
                    RadialGradient(colors: [Brand.accent.opacity(scheme == .dark ? 0.30 : 0.20), .clear],
                                   center: UnitPoint(x: 0.1, y: 0.15), startRadius: 0, endRadius: radius * 0.6)
                    RadialGradient(colors: [Brand.accent2.opacity(scheme == .dark ? 0.24 : 0.16), .clear],
                                   center: UnitPoint(x: 0.95, y: 0.95), startRadius: 0, endRadius: radius * 0.6)
                    RadialGradient(colors: [Color.teal.opacity(scheme == .dark ? 0.12 : 0.10), .clear],
                                   center: UnitPoint(x: 0.95, y: 0.2), startRadius: 0, endRadius: radius * 0.45)
                }
            }
            .allowsHitTesting(false)
        }
        .ignoresSafeArea()
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

// MARK: Buttons

/// Prominent (tinted) glass button for primary actions.
struct PrimaryButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 13, weight: .semibold))
            .foregroundStyle(.white)
            .padding(.horizontal, 16)
            .frame(height: 36)
            .background(ProminentFill())
            .scaleEffect(configuration.isPressed ? 0.97 : 1)
            .animation(.spring(response: 0.25, dampingFraction: 0.7), value: configuration.isPressed)
    }
}

/// Clear glass button for secondary actions.
struct SecondaryButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 13, weight: .medium))
            .foregroundStyle(.primary)
            .padding(.horizontal, 14)
            .frame(height: 36)
            .glassCapsule()
            .scaleEffect(configuration.isPressed ? 0.97 : 1)
            .animation(.spring(response: 0.25, dampingFraction: 0.7), value: configuration.isPressed)
    }
}

/// Small glass chip; prominent chips are tinted with the brand color.
struct ChipButtonStyle: ButtonStyle {
    var prominent = false
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 12, weight: prominent ? .semibold : .medium))
            .lineLimit(1)
            .padding(.horizontal, 11).padding(.vertical, 6)
            .foregroundStyle(prominent ? Color.white : Color.primary)
            .modifier(ChipSurface(prominent: prominent))
            .scaleEffect(configuration.isPressed ? 0.96 : 1)
            .animation(.spring(response: 0.25, dampingFraction: 0.7), value: configuration.isPressed)
    }
}

/// Round glass icon button (toolbar-style).
struct GlassIconButtonStyle: ButtonStyle {
    var size: CGFloat = 34
    var tint: Color? = nil
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 14, weight: .medium))
            .frame(width: size, height: size)
            .foregroundStyle(tint == nil ? Color.primary : Color.white)
            .modifier(ChipSurface(prominent: tint != nil))
            .scaleEffect(configuration.isPressed ? 0.94 : 1)
            .animation(.spring(response: 0.25, dampingFraction: 0.7), value: configuration.isPressed)
    }
}

/// Type-erased button style for conditional styling.
struct AnyButtonStyle: ButtonStyle {
    private let make: (Configuration) -> AnyView
    init<S: ButtonStyle>(_ style: S) { make = { AnyView(style.makeBody(configuration: $0)) } }
    func makeBody(configuration: Configuration) -> some View { make(configuration) }
}

/// Floating glass tab bar (iOS 26 style) used for the main sections.
struct GlassTabBar<Tab: Hashable & Identifiable>: View {
    let tabs: [Tab]
    @Binding var selection: Tab
    let title: (Tab) -> String
    let symbol: (Tab) -> String
    @Namespace private var namespace

    var body: some View {
        HStack(spacing: 2) {
            ForEach(tabs) { tab in
                let selected = tab == selection
                Button {
                    withAnimation(.spring(response: 0.35, dampingFraction: 0.8)) { selection = tab }
                } label: {
                    Label(title(tab), systemImage: symbol(tab))
                        .font(.system(size: 12.5, weight: selected ? .semibold : .medium))
                        .foregroundStyle(selected ? Color.primary : Color.secondary)
                        .padding(.horizontal, 12).padding(.vertical, 7)
                        .background {
                            if selected {
                                Capsule().fill(Color.primary.opacity(0.1))
                                    .matchedGeometryEffect(id: "tab", in: namespace)
                            }
                        }
                        .contentShape(Capsule())
                }
                .buttonStyle(.plain)
            }
        }
        .padding(4)
        .glassCapsule()
    }
}

/// Soft scroll edge: content fades out under floating glass bars (like macOS 26's
/// scroll edge effect) instead of colliding with them.
struct ScrollEdgeFade: View {
    enum Edge { case top, bottom }
    var edge: Edge
    var height: CGFloat = 36
    var body: some View {
        // Plain color gradient (no material + mask offscreen pass).
        LinearGradient(colors: [Color(nsColor: .windowBackgroundColor), Color(nsColor: .windowBackgroundColor).opacity(0.85), Color(nsColor: .windowBackgroundColor).opacity(0)],
                       startPoint: edge == .bottom ? .bottom : .top,
                       endPoint: edge == .bottom ? .top : .bottom)
            .frame(height: height)
            .allowsHitTesting(false)
    }
}

/// Brand gradient capsule with a glassy top highlight. Used for prominent actions so
/// they stay colored in non-key windows (the teleprompter never becomes key, and
/// system glass tints render desaturated there).
struct ProminentFill: View {
    var body: some View {
        Capsule()
            .fill(Brand.gradient)
            .overlay(Capsule().fill(LinearGradient(colors: [.white.opacity(0.32), .clear], startPoint: .top, endPoint: .center)))
            .overlay(Capsule().strokeBorder(.white.opacity(0.28), lineWidth: 0.7))
    }
}

private struct ChipSurface: ViewModifier {
    var prominent: Bool
    func body(content: Content) -> some View {
        if prominent { content.background(ProminentFill()) }
        else { content.glassCapsule() }
    }
}

/// Drop-down picker built on `Menu`. On macOS 26 the system pop-up `Picker`
/// can render its label white on light glass cards; a `Menu` with our own label
/// keeps normal text colors in both appearances.
struct MenuPicker<Value: Hashable>: View {
    var title: String? = nil
    @Binding var selection: Value
    let options: [(Value, String)]
    var placeholder = "请选择"

    var body: some View {
        HStack(spacing: 8) {
            if let title { Text(title) }
            Menu {
                ForEach(options.indices, id: \.self) { index in
                    let option = options[index]
                    Button { selection = option.0 } label: {
                        if option.0 == selection { Label(option.1, systemImage: "checkmark") } else { Text(option.1) }
                    }
                }
            } label: {
                // Rendered by SwiftUI (plain button style), not by the AppKit pop-up,
                // so the text color follows our appearance on glass cards.
                HStack(spacing: 6) {
                    Text(options.first(where: { $0.0 == selection })?.1 ?? placeholder)
                        .lineLimit(1)
                        .foregroundStyle(Color.primary)
                    Image(systemName: "chevron.up.chevron.down")
                        .font(.system(size: 9, weight: .semibold))
                        .foregroundStyle(Color.secondary)
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
        }
    }
}

extension View {
    /// Surface for content cards. Real glass is reserved for floating controls (as in
    /// Apple's guidance); large glass cards are expensive on smaller GPUs.
    func cardSurface(cornerRadius: CGFloat = 22, tint: Color? = nil) -> some View {
        modifier(FrostedFallback(shape: RoundedRectangle(cornerRadius: cornerRadius, style: .continuous), tint: tint))
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
