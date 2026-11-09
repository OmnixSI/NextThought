import SwiftUI

enum OperationTone {
    case progress, success, warning, failure
    var color: Color {
        switch self {
        case .progress: Ink.secondary
        case .success: Ink.ready
        case .warning: Ink.caution
        case .failure: Ink.live
        }
    }
    var symbol: String {
        switch self {
        case .progress: "clock"
        case .success: "checkmark.circle"
        case .warning: "exclamationmark.triangle"
        case .failure: "exclamationmark.circle"
        }
    }
}

/// Persistent results stay attached to the operation, with both words and a cue.
struct OperationFeedback: View {
    let message: String
    let tone: OperationTone
    var body: some View {
        HStack(alignment: .top, spacing: 8) {
            Image(systemName: tone.symbol).frame(width: 16).accessibilityHidden(true)
            Text(LocalizedStringKey(message)).textSelection(.enabled)
                .fixedSize(horizontal: false, vertical: true)
        }
        .font(Typo.caption).foregroundStyle(tone.color)
        .padding(.vertical, 6)
        .accessibilityElement(children: .combine)
    }
}
