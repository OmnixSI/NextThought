import SwiftUI
import InterviewHackerCore

/// The row under a meeting's title: 「纪要 | 提纲 | 鱼骨图 | 观点对照」. The text notes are always
/// there; the other three are views of the same meeting beside it, written the first time they
/// are picked and kept for the next time. A small dot marks the one that suits this kind of
/// meeting; nothing is written or switched by itself.
struct NotesFormatBar: View {
    let id: UUID
    /// "纪要", or "复盘" for a meeting reviewed question by question.
    let firstTitle: String
    /// The view that suits the meeting type (nil: none in particular).
    let recommended: NotesFormat?
    @Binding var selection: NotesFormat
    /// Called with the view that was picked (the page writes it when it does not exist yet).
    let onPick: (NotesFormat) -> Void

    private let order: [NotesFormat] = [.standard, .outline, .fishbone, .viewpoints]
    private let segmentHeight: CGFloat = 32

    var body: some View {
        HStack(spacing: 2) {
            ForEach(order) { format in
                segment(format)
            }
        }
        .padding(2)
        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
        .animation(Motion.expand, value: selection)
        .frame(maxWidth: 440, alignment: .leading)
    }

    private func segment(_ format: NotesFormat) -> some View {
        let selected = format == selection
        let working = StructuredNotesJobs.shared.isRunning(StructuredJobKey(id: id, format: format))
        let title = format == .standard ? firstTitle : format.barTitle
        let suits = format.isStructured && format == recommended
        return Button {
            selection = format
            onPick(format)
        } label: {
            HStack(spacing: Space.s1) {
                Text(verbatim: title)
                    .font(Typo.label)
                    .lineLimit(1)
                    .foregroundStyle(selected ? Ink.ink : Ink.secondary)
                if working {
                    ProgressView().controlSize(.mini)
                } else if suits {
                    Circle().fill(Ink.ai).frame(width: 5, height: 5)
                }
            }
            .frame(maxWidth: .infinity)
            .frame(height: segmentHeight)
            .background {
                if selected {
                    RoundedRectangle(cornerRadius: 5, style: .continuous).fill(Ink.surface)
                        .overlay(RoundedRectangle(cornerRadius: 5, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 0.5))
                }
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .help(suits ? String(localized: "适合这类会议") : "")
        .accessibilityLabel(Text(verbatim: title))
        .accessibilityHint(suits ? Text("适合这类会议") : Text(verbatim: ""))
        .accessibilityAddTraits(selected ? .isSelected : [])
    }
}
