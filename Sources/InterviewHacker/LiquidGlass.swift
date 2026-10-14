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
}

extension View {
    /// Glass surface in a rounded rectangle.
    @ViewBuilder
    func glassSurface(cornerRadius: CGFloat = 20, tint: Color? = nil, interactive: Bool = false) -> some View {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) {
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
        if #available(macOS 26.0, *) {
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

private struct FrostedFallback<S: InsettableShape>: ViewModifier {
    let shape: S
    let tint: Color?
    @Environment(\.colorScheme) private var scheme
    func body(content: Content) -> some View {
        content
            .background {
                ZStack {
                    shape.fill(.regularMaterial)
                    if let tint { shape.fill(tint.opacity(0.22)) }
                    shape.fill(LinearGradient(colors: [.white.opacity(scheme == .dark ? 0.06 : 0.35), .clear], startPoint: .top, endPoint: .center))
                }
            }
            .overlay(shape.strokeBorder(LinearGradient(colors: [.white.opacity(scheme == .dark ? 0.22 : 0.7), .white.opacity(0.05)], startPoint: .top, endPoint: .bottom), lineWidth: 0.8))
            .shadow(color: .black.opacity(scheme == .dark ? 0.35 : 0.08), radius: 12, y: 4)
    }
}

/// Groups nearby glass shapes so they blend and morph together on macOS 26.
struct GlassGroup<Content: View>: View {
    var spacing: CGFloat = 12
    @ViewBuilder var content: Content
    var body: some View {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) {
            GlassEffectContainer(spacing: spacing) { content }
        } else {
            content
        }
        #else
        content
        #endif
    }
}

/// Desktop-through window backdrop with soft brand light, so glass has something to refract.
struct AppBackdrop: View {
    @Environment(\.colorScheme) private var scheme
    var body: some View {
        ZStack {
            VisualEffectBackground(material: .underWindowBackground)
            GeometryReader { proxy in
                let size = proxy.size
                Circle().fill(Brand.accent.opacity(scheme == .dark ? 0.30 : 0.22))
                    .frame(width: size.width * 0.9).blur(radius: 90)
                    .offset(x: -size.width * 0.35, y: -size.height * 0.25)
                Circle().fill(Brand.accent2.opacity(scheme == .dark ? 0.26 : 0.18))
                    .frame(width: size.width * 0.8).blur(radius: 100)
                    .offset(x: size.width * 0.45, y: size.height * 0.45)
                Circle().fill(Color.teal.opacity(scheme == .dark ? 0.14 : 0.12))
                    .frame(width: size.width * 0.6).blur(radius: 100)
                    .offset(x: size.width * 0.5, y: -size.height * 0.1)
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
            .glassCapsule(interactive: true)
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
        Rectangle()
            .fill(.ultraThinMaterial)
            .mask(LinearGradient(colors: [.black, .black.opacity(0.85), .clear],
                                 startPoint: edge == .bottom ? .bottom : .top,
                                 endPoint: edge == .bottom ? .top : .bottom))
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
            .shadow(color: Brand.accent.opacity(0.35), radius: 8, y: 3)
    }
}

private struct ChipSurface: ViewModifier {
    var prominent: Bool
    func body(content: Content) -> some View {
        if prominent { content.background(ProminentFill()) }
        else { content.glassCapsule(interactive: true) }
    }
}
