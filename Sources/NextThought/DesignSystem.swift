import AppKit
import SwiftUI

// MARK: - Paper & Ink design tokens
//
// The values mirror docs/redesign/design-system/tokens.json. The interface is 95 %
// paper and ink; neutral gray (`ai`) marks AI-written content, red (`live`) means
// recording, green (`ready`) means ready, amber (`caution`) needs attention.

extension NSColor {
    /// sRGB color from a 0xRRGGBB value.
    static func hex(_ value: UInt32, alpha: CGFloat = 1) -> NSColor {
        NSColor(srgbRed: CGFloat((value >> 16) & 0xff) / 255,
                green: CGFloat((value >> 8) & 0xff) / 255,
                blue: CGFloat(value & 0xff) / 255,
                alpha: alpha)
    }
}

extension Color {
    /// A color with a light and a dark variant, resolved by the view's appearance
    /// (like an asset-catalog color).
    static func dynamic(light: NSColor, dark: NSColor) -> Color {
        Color(nsColor: NSColor(name: nil) { appearance in
            appearance.bestMatch(from: [.aqua, .darkAqua]) == .darkAqua ? dark : light
        })
    }
}

/// Color tokens. `Ink.ink` is the text color and the primary button fill.
enum Ink {
    /// Window background for AppKit (`NSWindow.backgroundColor`).
    static let paperNSColor = NSColor(name: nil) { appearance in
        appearance.bestMatch(from: [.aqua, .darkAqua]) == .darkAqua ? NSColor.hex(0x131312) : NSColor.hex(0xf6f5f2)
    }
    static let paper = Color(nsColor: paperNSColor)
    static let surface = Color.dynamic(light: .hex(0xffffff), dark: .hex(0x1c1c1a))
    static let surfaceRaised = Color.dynamic(light: .hex(0xffffff), dark: .hex(0x242422))
    static let sunken = Color.dynamic(light: .hex(0x1c1b19, alpha: 0.04), dark: .hex(0xecebe8, alpha: 0.05))
    static let hover = Color.dynamic(light: .hex(0x1c1b19, alpha: 0.06), dark: .hex(0xecebe8, alpha: 0.08))
    static let pressed = Color.dynamic(light: .hex(0x1c1b19, alpha: 0.11), dark: .hex(0xecebe8, alpha: 0.14))
    static let ink = Color.dynamic(light: .hex(0x1c1b19), dark: .hex(0xecebe8))
    static let onInk = Color.dynamic(light: .hex(0xffffff), dark: .hex(0x131312))
    static let secondary = Color.dynamic(light: .hex(0x6b6963), dark: .hex(0xa3a19b))
    /// Placeholders and decoration only; never for text that has to be read.
    static let tertiary = Color.dynamic(light: .hex(0x706e68), dark: .hex(0x95938c))
    static let hairline = Color.dynamic(light: .hex(0xe7e5e0), dark: .hex(0x2c2b28))
    /// Neutral gray marks AI-written content; the interface stays grayscale.
    static let ai = Color.dynamic(light: .hex(0x595751), dark: .hex(0xb8b6af))
    static let aiSoft = Color.dynamic(light: .hex(0x1c1b19, alpha: 0.045), dark: .hex(0xecebe8, alpha: 0.07))
    static let promptPanel = Color.dynamic(light: .hex(0xf0efec), dark: .hex(0x242422))
    static let live = Color.dynamic(light: .hex(0xb82e2e), dark: .hex(0xff6b63))
    static let ready = Color.dynamic(light: .hex(0x237542), dark: .hex(0x4cc38a))
    static let caution = Color.dynamic(light: .hex(0x8a5707), dark: .hex(0xf0b24a))
    static let focus = ai
    static let floatShadow = Color.dynamic(light: .hex(0x1c1b19, alpha: 0.12), dark: .hex(0x000000, alpha: 0.5))
}

/// Type scale (01-visual-language.md). Serif styles render as New York, with the
/// system Song face for Chinese.
enum Typo {
    static let display = Font.system(size: 30, weight: .medium, design: .serif)
    static let section = Font.system(size: 19, weight: .medium, design: .serif)
    static let prompt = Font.system(size: 22, weight: .medium)
    static let body = Font.system(size: 15)
    static let label = Font.system(size: 13, weight: .medium)
    static let caption = Font.system(size: 12)
    static let overline = Font.system(size: 11, weight: .semibold)
    static let code = Font.system(size: 13, design: .monospaced)
}

/// Spacing, sizes and radii.
enum Space {
    static let s1: CGFloat = 4
    static let s2: CGFloat = 8
    static let s3: CGFloat = 12
    static let s4: CGFloat = 16
    static let s6: CGFloat = 24
    static let s8: CGFloat = 32
    static let s10: CGFloat = 40
    static let s12: CGFloat = 48
    static let control: CGFloat = 32
    static let controlLarge: CGFloat = 40
    static let sidebar: CGFloat = 220
    static let content: CGFloat = 720
    static let radiusSmall: CGFloat = 6
    static let radiusMedium: CGFloat = 10
    static let radiusLarge: CGFloat = 16
}

/// Quiet motion: ease-out, no bounce; nothing moves when Reduce Motion is on.
enum Motion {
    static var reduce: Bool { NSWorkspace.shared.accessibilityDisplayShouldReduceMotion }
    private static func curve(_ duration: Double) -> Animation? {
        reduce ? nil : .timingCurve(0.2, 0.8, 0.2, 1, duration: duration)
    }
    static var hover: Animation? { curve(0.12) }
    static var expand: Animation? { curve(0.2) }
    static var phase: Animation? { curve(0.32) }
}

// MARK: - Modifiers

extension View {
    /// Small category label above a block ("下一场会议", "AI 读到的重点").
    func overline(_ color: Color = Ink.secondary) -> some View {
        font(Typo.overline).tracking(0.66).foregroundStyle(color)
    }

    /// Content card: surface fill, 1 px hairline, radius 10, no shadow.
    func paperCard(padding: CGFloat = Space.s4) -> some View {
        self.padding(padding)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(Ink.surface, in: RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 1))
    }

    /// AI-written block: ai-soft background, no border.
    func aiBlock(padding: CGFloat = Space.s4) -> some View {
        self.padding(padding)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(Ink.aiSoft, in: RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
    }

    /// Floating layer (teleprompter, popovers): raised surface and the float shadow.
    func floatingSurface(cornerRadius: CGFloat = Space.radiusLarge) -> some View {
        background {
            RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                .fill(Ink.surfaceRaised)
                .shadow(color: Ink.floatShadow, radius: 16, x: 0, y: 12)
                .overlay(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 0.5))
        }
    }
}

/// Title of an AI block: a neutral sparkle and overline.
struct AIHeading: View {
    let title: LocalizedStringKey
    var body: some View {
        HStack(spacing: Space.s1) {
            Image(systemName: "sparkle").font(.system(size: 11, weight: .medium))
            Text(title)
        }
        .overline(Ink.ai)
    }
}

/// Status dot. Always paired with words next to it.
struct StatusDot: View {
    enum Tone { case live, ready, caution, idle }
    var tone: Tone
    var size: CGFloat = 7
    var body: some View {
        Circle().fill(color).frame(width: size, height: size)
    }
    private var color: Color {
        switch tone {
        case .live: Ink.live
        case .ready: Ink.ready
        case .caution: Ink.caution
        case .idle: Ink.tertiary
        }
    }
}

// MARK: - Buttons

/// Borderless button that shows the hover color under the pointer.
struct GhostButtonStyle: ButtonStyle {
    var height: CGFloat = Space.control
    var horizontalPadding: CGFloat = 10
    var scalesWithDynamicType = false
    func makeBody(configuration: Configuration) -> some View {
        ControlButtonBody(configuration: configuration, emphasis: .quiet, height: height,
                          horizontalPadding: horizontalPadding, scalesWithDynamicType: scalesWithDynamicType)
    }
}

/// Shared hover and press feedback for full-width navigation rows.
struct InteractiveRowButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        InteractiveRowBody(configuration: configuration)
    }

    private struct InteractiveRowBody: View {
        let configuration: ButtonStyleConfiguration
        @Environment(\.isEnabled) private var isEnabled
        @State private var hovering = false

        var body: some View {
            configuration.label
                .frame(minHeight: Hit.minHeight)
                .background(
                    (isEnabled && (hovering || configuration.isPressed)) ? Ink.hover : Color.clear,
                    in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous)
                )
                .contentShape(Rectangle())
                .onHover { hovering = $0 }
                .animation(Motion.hover, value: hovering)
        }
    }
}

/// Segmented control on a sunken track; the selected segment is a raised surface.
struct InkSegmented<Value: Hashable>: View {
    let items: [(value: Value, title: LocalizedStringKey)]
    @Binding var selection: Value
    var height: CGFloat = Hit.minHeight
    var scalesWithDynamicType = false
    @Environment(\.layoutDirection) private var layoutDirection
    @FocusState private var focusedValue: Value?

    var body: some View {
        HStack(spacing: 0) {
            ForEach(Array(items.enumerated()), id: \.offset) { _, item in
                let selected = item.value == selection
                Button { selection = item.value } label: {
                    Text(item.title)
                        .font(scalesWithDynamicType ? Font.body.weight(.medium) : Typo.label)
                        .lineLimit(1).fixedSize()
                        .foregroundStyle(selected ? Ink.ink : Ink.secondary)
                        .padding(.horizontal, 14)
                        .frame(minWidth: Hit.minHeight, minHeight: max(height, Hit.minHeight) - 4)
                        .background {
                            if selected {
                                RoundedRectangle(cornerRadius: 5, style: .continuous).fill(Ink.surface)
                                    .overlay(RoundedRectangle(cornerRadius: 5, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 0.5))
                            }
                        }
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .focused($focusedValue, equals: item.value)
                .accessibilityAddTraits(selected ? .isSelected : [])
                .overlay {
                    RoundedRectangle(cornerRadius: Space.radiusSmall)
                        .strokeBorder(focusedValue == item.value ? Ink.focus : Color.clear, lineWidth: 2)
                        .allowsHitTesting(false)
                }
                .onKeyPress(keys: [.leftArrow, .rightArrow, .home, .end]) { key in
                    guard key.modifiers.intersection([.command, .option, .control, .shift]).isEmpty,
                          let index = items.firstIndex(where: { $0.value == item.value }) else { return .ignored }
                    let next: Int
                    switch key.key {
                    case .home: next = 0
                    case .end: next = items.count - 1
                    case .leftArrow, .rightArrow:
                        let forward = (key.key == .rightArrow) == (layoutDirection == .leftToRight)
                        next = (index + (forward ? 1 : items.count - 1)) % items.count
                    default: return .ignored
                    }
                    selection = items[next].value; focusedValue = items[next].value
                    return .handled
                }
            }
        }
        .accessibilityElement(children: .contain)
        .padding(2)
        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
        .animation(Motion.expand, value: selection)
    }
}

/// Wraps its subviews onto new lines when a row is full (type tags, refinements).
struct FlowLayout: Layout {
    var spacing: CGFloat = 8
    var lineSpacing: CGFloat = 8

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let rows = rows(subviews: subviews, maxWidth: proposal.width ?? .infinity)
        let width = rows.map(\.width).max() ?? 0
        let height = rows.reduce(0) { $0 + $1.height } + lineSpacing * CGFloat(max(0, rows.count - 1))
        return CGSize(width: min(width, proposal.width ?? width), height: height)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        var y = bounds.minY
        for row in rows(subviews: subviews, maxWidth: bounds.width) {
            var x = bounds.minX
            for item in row.items {
                subviews[item.index].place(at: CGPoint(x: x, y: y + (row.height - item.size.height) / 2),
                                           anchor: .topLeading, proposal: ProposedViewSize(item.size))
                x += item.size.width + spacing
            }
            y += row.height + lineSpacing
        }
    }

    private struct Item { let index: Int; let size: CGSize }
    private struct Row { var items: [Item] = []; var width: CGFloat = 0; var height: CGFloat = 0 }

    private func rows(subviews: Subviews, maxWidth: CGFloat) -> [Row] {
        var result: [Row] = []
        var current = Row()
        for index in subviews.indices {
            let size = subviews[index].sizeThatFits(.unspecified)
            let needed = current.items.isEmpty ? size.width : current.width + spacing + size.width
            if needed > maxWidth, !current.items.isEmpty {
                result.append(current)
                current = Row(items: [Item(index: index, size: size)], width: size.width, height: size.height)
            } else {
                current.items.append(Item(index: index, size: size))
                current.width = needed
                current.height = max(current.height, size.height)
            }
        }
        if !current.items.isEmpty { result.append(current) }
        return result
    }
}

/// A shortcut hint drawn after a button title ("⌘⇧↩").
struct ShortcutHint: View {
    let text: String
    var onInk = false
    var scalesWithDynamicType = false
    var body: some View {
        Text(verbatim: text)
            .font(scalesWithDynamicType
                  ? Font.caption.weight(.medium).monospacedDigit()
                  : .system(size: 11, weight: .medium).monospacedDigit())
            .foregroundStyle(onInk ? Ink.onInk.opacity(0.7) : Ink.secondary)
    }
}
