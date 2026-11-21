import SwiftUI

/// A shared visual language for actions; existing style names remain compatible.
enum ControlEmphasis { case primary, secondary, quiet, destructive }

enum ControlMetrics {
    static let radius: CGFloat = 9
    static let height: CGFloat = 32
    static let focusWidth: CGFloat = 2
}

private struct ControlBusyKey: EnvironmentKey { static let defaultValue = false }
private struct ControlInkKey: EnvironmentKey { static let defaultValue = Ink.ink }
extension EnvironmentValues {
    var controlIsBusy: Bool {
        get { self[ControlBusyKey.self] }
        set { self[ControlBusyKey.self] = newValue }
    }
    var controlInk: Color {
        get { self[ControlInkKey.self] }
        set { self[ControlInkKey.self] = newValue }
    }
}

extension View {
    /// Busy actions remain legible while the owner disables repeat activation.
    func controlBusy(_ busy: Bool) -> some View { environment(\.controlIsBusy, busy) }
}

struct ControlButtonBody: View {
    let configuration: ButtonStyleConfiguration
    let emphasis: ControlEmphasis
    var height: CGFloat = ControlMetrics.height
    var horizontalPadding: CGFloat = 12
    var scalesWithDynamicType = false
    var iconOnly = false
    @Environment(\.isEnabled) private var isEnabled
    @Environment(\.isFocused) private var isFocused
    @Environment(\.controlIsBusy) private var busy
    @Environment(\.liveSessionAcceptanceButtonInteraction) private var sampleInteraction
    @State private var hovering = false

    private var available: Bool { isEnabled || busy }
    private var filled: Bool { emphasis == .primary || emphasis == .destructive }
    private var ink: Color {
        guard available else { return Ink.tertiary.opacity(0.65) }
        if filled { return Ink.onInk }
        return configuration.role == .destructive ? Ink.live : Ink.ink
    }
    private var base: Color {
        guard available else { return Ink.sunken }
        switch emphasis {
        case .primary: return Ink.ink
        case .secondary: return Ink.surfaceRaised
        case .quiet: return .clear
        case .destructive: return Ink.live
        }
    }
    private var interaction: Color {
        guard available, !busy else { return .clear }
        let pressed = configuration.isPressed || sampleInteraction == .pressed
        let hover = hovering || sampleInteraction == .hover
        if filled { return Ink.onInk.opacity(pressed ? 0.15 : hover ? 0.07 : 0) }
        return pressed ? Ink.pressed : hover ? Ink.hover : .clear
    }

    var body: some View {
        configuration.label
            .font(scalesWithDynamicType ? Font.zoomed(.body).weight(.medium)
                  : .zoomed(size: height >= Space.controlLarge ? 14 : 13, weight: .medium))
            .lineLimit(1)
            .foregroundStyle(ink)
            .environment(\.controlInk, ink)
            .padding(.horizontal, iconOnly ? 0 : horizontalPadding)
            .frame(width: iconOnly ? max(height, Hit.icon) : nil)
            .frame(minWidth: Hit.minHeight)
            .frame(minHeight: scalesWithDynamicType ? max(height, Hit.minHeight) : nil)
            .frame(height: scalesWithDynamicType ? nil : max(height, iconOnly ? Hit.icon : Hit.minHeight))
            .background {
                RoundedRectangle(cornerRadius: ControlMetrics.radius, style: .continuous)
                    .fill(base)
                    .overlay {
                        // A filled button catches light along its top edge.
                        if filled, available {
                            RoundedRectangle(cornerRadius: ControlMetrics.radius, style: .continuous)
                                .fill(LinearGradient(colors: [Ink.onInk.opacity(0.14), .clear], startPoint: .top, endPoint: .center))
                        }
                    }
                    .overlay {
                        RoundedRectangle(cornerRadius: ControlMetrics.radius, style: .continuous).fill(interaction)
                    }
                    .shadow(color: (filled || emphasis == .secondary) && available ? Ink.contactShadow : .clear,
                            radius: filled ? 3 : 1, x: 0, y: filled ? 2 : 1)
            }
            .overlay {
                RoundedRectangle(cornerRadius: ControlMetrics.radius, style: .continuous)
                    .strokeBorder(emphasis == .secondary && available ? Ink.hairline : .clear, lineWidth: 0.5)
                    .allowsHitTesting(false)
            }
            .overlay {
                RoundedRectangle(cornerRadius: ControlMetrics.radius + 2, style: .continuous)
                    .stroke(isFocused || sampleInteraction == .focused ? Ink.focus : .clear,
                            lineWidth: ControlMetrics.focusWidth)
                    .padding(-3)
                    .allowsHitTesting(false)
            }
            .contentShape(RoundedRectangle(cornerRadius: ControlMetrics.radius, style: .continuous))
            .scaleEffect(configuration.isPressed && available && !busy ? 0.97 : 1)
            .onHover { hovering = $0 }
            .animation(Motion.hover, value: hovering)
            .animation(Motion.snappy, value: configuration.isPressed)
    }
}

/// Reserve both labels and the progress lane so saving never moves adjacent actions.
struct BusyActionLabel: View {
    let title: String
    let busyTitle: String
    let busy: Bool
    @Environment(\.controlInk) private var ink
    var body: some View {
        HStack(spacing: 8) {
            ZStack {
                Color.clear.frame(width: 14, height: 14)
                if busy { ProgressView().controlSize(.small).tint(ink) }
            }.frame(width: 14, height: 14)
            ZStack {
                Text(LocalizedStringKey(title)).opacity(busy ? 0 : 1)
                Text(LocalizedStringKey(busyTitle)).opacity(busy ? 1 : 0)
            }
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(Text(LocalizedStringKey(busy ? busyTitle : title)))
    }
}
