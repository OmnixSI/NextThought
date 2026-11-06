import SwiftUI

/// A quiet status line for the simple settings pages: a dot, a title, one gray
/// line and, when something can be fixed, a single button on the right.
struct SettingsStatusRow: View {
    let tone: StatusDot.Tone
    let title: String
    let detail: String
    var actionTitle: String? = nil
    var action: (() -> Void)? = nil

    var body: some View {
        HStack(spacing: Space.s3) {
            StatusDot(tone: tone)
            VStack(alignment: .leading, spacing: 2) {
                Text(LocalizedStringKey(title)).font(Typo.label)
                Text(LocalizedStringKey(detail))
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: Space.s2)
            if let actionTitle, let action {
                Button(LocalizedStringKey(actionTitle), action: action)
                    .buttonStyle(ChipButtonStyle())
            }
        }
        .padding(.vertical, Space.s1)
        .accessibilityElement(children: .contain)
    }
}

/// A title and one gray line with an icon in front; used where there is nothing to fix.
struct SettingsInfoRow: View {
    let icon: String
    let title: String
    let detail: String

    var body: some View {
        HStack(alignment: .top, spacing: Space.s3) {
            Image(systemName: icon)
                .font(.system(size: 13, weight: .regular)).foregroundStyle(Ink.secondary)
                .frame(width: 18)
            VStack(alignment: .leading, spacing: 2) {
                Text(LocalizedStringKey(title)).font(Typo.label)
                Text(LocalizedStringKey(detail))
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: 0)
        }
    }
}
