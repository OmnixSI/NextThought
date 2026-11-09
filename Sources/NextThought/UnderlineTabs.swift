import SwiftUI

/// Content-width page tabs. The surrounding header owns its separator.
struct UnderlineTabs<Value: Hashable>: View {
    let items: [(value: Value, title: String)]
    @Binding var selection: Value
    var accessibilityLabel: String = "页面分区"

    @Environment(\.layoutDirection) private var layoutDirection
    @Environment(\.isEnabled) private var isEnabled
    @FocusState private var focusedValue: Value?
    @State private var hoveredValue: Value?

    var body: some View {
        HStack(spacing: Space.s3) {
            ForEach(items, id: \.value) { item in
                tab(item)
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel(Text(LocalizedStringKey(accessibilityLabel)))
        .onChange(of: selection) { _, next in
            // External navigation follows selection only while focus is already here.
            if focusedValue != nil, items.contains(where: { $0.value == next }) {
                focusedValue = next
            }
        }
    }

    private func tab(_ item: (value: Value, title: String)) -> some View {
        let selected = selection == item.value
        let hovering = isEnabled && hoveredValue == item.value
        return Button { selection = item.value } label: {
            Text(LocalizedStringKey(item.title))
                .font(Typo.label)
                .foregroundStyle(selected ? Ink.ink : Ink.secondary)
                .lineLimit(1)
                .fixedSize(horizontal: true, vertical: false)
                .padding(.horizontal, Space.s2)
                .frame(minWidth: Hit.minHeight, minHeight: 36)
                .background(hovering ? Ink.hover.opacity(0.5) : Color.clear,
                            in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
                .overlay(alignment: .bottom) {
                    Rectangle()
                        .fill(selected ? Ink.ink : Color.clear)
                        .frame(height: 2)
                        .padding(.horizontal, Space.s2)
                        .allowsHitTesting(false)
                }
                .contentShape(Rectangle())
                .animation(Motion.hover, value: hovering)
                .animation(Motion.expand, value: selected)
        }
        .buttonStyle(.plain)
        .focusable()
        .focused($focusedValue, equals: item.value)
        .focusEffectDisabled()
        .overlay {
            RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous)
                .strokeBorder(focusedValue == item.value ? Ink.focus : Color.clear, lineWidth: 2)
                .allowsHitTesting(false)
        }
        .onHover { hovering in
            if hovering { hoveredValue = item.value }
            else if hoveredValue == item.value { hoveredValue = nil }
        }
        .accessibilityAddTraits(selected ? .isSelected : [])
        .onKeyPress(keys: [.leftArrow, .rightArrow, .home, .end]) { key in
            moveSelection(for: key, from: item.value)
        }
    }

    private func moveSelection(for key: KeyPress, from value: Value) -> KeyPress.Result {
        guard isEnabled,
              key.modifiers.intersection([.command, .control, .option, .shift]).isEmpty,
              let index = items.firstIndex(where: { $0.value == value }) else { return .ignored }
        let next: Int
        switch key.key {
        case .home: next = 0
        case .end: next = items.count - 1
        case .leftArrow, .rightArrow:
            // HStack mirrors in RTL, so the physical arrow direction mirrors too.
            let forward = (key.key == .rightArrow) == (layoutDirection == .leftToRight)
            next = (index + (forward ? 1 : items.count - 1)) % items.count
        default: return .ignored
        }
        selection = items[next].value
        focusedValue = items[next].value
        return .handled
    }
}
