import SwiftUI

/// Persistent labels belong to the form; the field owns its surface and focus.
struct PaperTextFieldStyle: TextFieldStyle {
    func _body(configuration: TextField<Self._Label>) -> some View {
        PaperFieldBody(field: configuration)
    }
}

private struct PaperFieldBody<Label: View>: View {
    let field: TextField<Label>
    @FocusState private var focused: Bool
    @Environment(\.isEnabled) private var isEnabled
    var body: some View {
        field
            .textFieldStyle(.plain)
            .focused($focused)
            .font(.system(size: 13))
            .foregroundStyle(isEnabled ? Ink.ink : Ink.tertiary)
            .padding(.horizontal, 10)
            .padding(.vertical, 7)
            .frame(minHeight: ControlMetrics.height)
            .background(isEnabled ? Ink.surface : Ink.sunken,
                        in: RoundedRectangle(cornerRadius: ControlMetrics.radius, style: .continuous))
            .overlay {
                RoundedRectangle(cornerRadius: ControlMetrics.radius, style: .continuous)
                    .strokeBorder(focused ? Ink.focus : Ink.hairline, lineWidth: focused ? 2 : 1)
                    .allowsHitTesting(false)
            }
    }
}
