import AppKit
import SwiftUI

// MARK: - Hit targets
//
// Every control answers a click anywhere in a comfortable area around what is drawn,
// not only on the glyph. `Hit.minHeight` is the floor for text controls and `Hit.icon`
// for icon-only buttons. The area is real layout (a frame), not a hit-test trick, so it
// works the same inside stacks, lists and menus.

enum Hit {
    /// Least clickable height of a text button, a chip or a row control.
    static let minHeight: CGFloat = 32
    /// Least clickable width and height of an icon-only button.
    static let icon: CGFloat = 36
    /// Height of a whole-row switch.
    static let row: CGFloat = 36
}

extension View {
    /// Enlarges the clickable area: at least `minWidth` x `minHeight`, and every point
    /// inside it (also the empty ones around a glyph) counts as a click. Apply it to the
    /// label of a `.plain` button; an area added outside the button does not click.
    func hitArea(minWidth: CGFloat = Hit.minHeight, minHeight: CGFloat = Hit.minHeight) -> some View {
        frame(minWidth: minWidth, minHeight: minHeight)
            .contentShape(Rectangle())
    }

    /// Like `hitArea`, with a rounded clickable shape (for controls drawn as a rounded fill).
    func hitArea(cornerRadius: CGFloat, minWidth: CGFloat = Hit.minHeight, minHeight: CGFloat = Hit.minHeight) -> some View {
        frame(minWidth: minWidth, minHeight: minHeight)
            .contentShape(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
    }

    /// The clickable area of an icon-only button (`Hit.icon` square).
    func iconHitArea() -> some View {
        hitArea(minWidth: Hit.icon, minHeight: Hit.icon)
    }

    /// Every switch below this view is clickable along its whole row, and every
    /// disclosure header opens and closes from anywhere along its row.
    func wideHitTargets() -> some View {
        toggleStyle(.wide).disclosureGroupStyle(.wide)
    }
}

// MARK: - Buttons

/// Plain button with a real clickable area: use it where `.plain` or `.borderless` would
/// leave only the glyph clickable. No fill; a pressed button fades a little.
struct TapButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled
    var minWidth: CGFloat = Hit.icon
    var minHeight: CGFloat = Hit.icon

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .frame(minWidth: minWidth, minHeight: minHeight)
            .contentShape(Rectangle())
            .opacity(isEnabled ? (configuration.isPressed ? 0.55 : 1) : 0.4)
            .animation(Motion.hover, value: configuration.isPressed)
    }
}

extension ButtonStyle where Self == TapButtonStyle {
    /// Icon-only button: at least `Hit.icon` square.
    static var tap: TapButtonStyle { TapButtonStyle() }
    /// Small text link: at least `Hit.minHeight` high, as wide as its text.
    static var tapText: TapButtonStyle { TapButtonStyle(minWidth: 0, minHeight: Hit.minHeight) }
}

// MARK: - Switch

/// A switch whose whole row is the click target: the label on the left, the switch on the
/// right, at least `Hit.row` high. Drawn by hand (ink track, paper knob) because a custom
/// `ToggleStyle` cannot reuse the system switch. The row is a button, so Space toggles it
/// when it has keyboard focus, and VoiceOver reads it as an on/off control.
/// `fillsRow` false (`.inline`) keeps the row as wide as its content, for a switch that
/// sits beside other controls in a stack; the area is still `Hit.row` high.
struct WideToggleStyle: ToggleStyle {
    var fillsRow = true

    func makeBody(configuration: Configuration) -> some View {
        WideToggleRow(configuration: configuration, fillsRow: fillsRow)
    }
}

extension ToggleStyle where Self == WideToggleStyle {
    /// Whole-row switch (see `WideToggleStyle`).
    static var wide: WideToggleStyle { WideToggleStyle() }
    /// The same switch, as wide as its content (beside other controls).
    static var inline: WideToggleStyle { WideToggleStyle(fillsRow: false) }
}

private struct WideToggleRow: View {
    let configuration: ToggleStyleConfiguration
    let fillsRow: Bool
    @Environment(\.isEnabled) private var isEnabled
    @State private var hovering = false

    var body: some View {
        Button {
            configuration.isOn.toggle()
        } label: {
            HStack(spacing: Space.s3) {
                if fillsRow {
                    configuration.label
                        .multilineTextAlignment(.leading)
                        .fixedSize(horizontal: false, vertical: true)
                        .frame(maxWidth: .infinity, alignment: .leading)
                } else {
                    configuration.label
                        .multilineTextAlignment(.leading)
                }
                InkSwitch(isOn: configuration.isOn)
            }
            .frame(minHeight: Hit.row)
            .contentShape(Rectangle())
        }
        .buttonStyle(WideToggleRowStyle(hovering: hovering && isEnabled))
        .opacity(isEnabled ? 1 : 0.45)
        .onHover { hovering = $0 }
        .animation(Motion.hover, value: hovering)
        .accessibilityValue(configuration.isOn ? Text("开") : Text("关"))
        .accessibilityAddTraits(.isToggle)
    }
}

/// Row feedback of `WideToggleStyle`: a faint fill under the pointer or while pressed.
private struct WideToggleRowStyle: ButtonStyle {
    var hovering: Bool
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .background(configuration.isPressed || hovering ? Ink.hover : Color.clear,
                        in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
    }
}

// MARK: - Disclosure

/// A disclosure group whose whole header row opens and closes it: chevron, title and the
/// empty space to the right. The content sits under the title, indented past the chevron.
struct WideDisclosureStyle: DisclosureGroupStyle {
    func makeBody(configuration: Configuration) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            Button {
                withAnimation(Motion.expand) { configuration.isExpanded.toggle() }
            } label: {
                HStack(spacing: Space.s2) {
                    Image(systemName: "chevron.right")
                        .font(.system(size: 10, weight: .semibold))
                        .foregroundStyle(Ink.secondary)
                        .rotationEffect(.degrees(configuration.isExpanded ? 90 : 0))
                        .frame(width: 12)
                    configuration.label
                        .multilineTextAlignment(.leading)
                    Spacer(minLength: 0)
                }
                .frame(minHeight: Hit.minHeight)
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .accessibilityAddTraits(.isHeader)
            if configuration.isExpanded {
                configuration.content
                    .padding(.leading, Space.s4 + Space.s1)
            }
        }
    }
}

extension DisclosureGroupStyle where Self == WideDisclosureStyle {
    /// Whole-row disclosure header (see `WideDisclosureStyle`).
    static var wide: WideDisclosureStyle { WideDisclosureStyle() }
}

/// The drawn switch: ink track when on, a quiet gray one when off.
struct InkSwitch: View {
    var isOn: Bool
    var body: some View {
        ZStack {
            Capsule().fill(isOn ? Ink.ink : Ink.tertiary.opacity(0.45))
            Circle()
                .fill(isOn ? Ink.onInk : Ink.surface)
                .frame(width: 16, height: 16)
                .offset(x: isOn ? 7 : -7)
        }
        .frame(width: 34, height: 20)
        .animation(Motion.hover, value: isOn)
        .accessibilityHidden(true)
    }
}
