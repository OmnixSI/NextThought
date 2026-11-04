import AppKit
import SwiftUI
import InterviewHackerCore

/// The width of the view a preference was read in (the fishbone sizes itself from it).
struct MeasuredWidthKey: PreferenceKey {
    static var defaultValue: CGFloat = 0
    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) { value = nextValue() }
}

/// "[12:41]" beside a node: jumps to that moment in the transcript.
struct StructuredTimeLink: View {
    let at: Int?
    let onJump: (Int) -> Void

    var body: some View {
        if let at {
            Button { onJump(at) } label: {
                Text(verbatim: "[\(NotesTime.text(at))]").font(.system(size: 12).monospacedDigit()).foregroundStyle(Ink.secondary)
            }
            .buttonStyle(.plain)
            .help("跳到转写里的这句话")
        }
    }
}

private enum StructuredPaneMode: Hashable { case structured, text }

/// The notes tab of a meeting: when a fishbone, outline or viewpoint matrix was written,
/// a small switch "结构图 / 文字纪要" (the structured view first); otherwise only the text
/// notes, as before. A failed structured pass only leaves one quiet line under the text.
struct StructuredNotesPane<TextBody: View>: View {
    let detail: SessionDetail
    let onJump: (Int) -> Void
    private let textBody: TextBody
    @State private var mode: StructuredPaneMode = .structured
    @State private var copied = false

    init(detail: SessionDetail, onJump: @escaping (Int) -> Void, @ViewBuilder textBody: () -> TextBody) {
        self.detail = detail
        self.onJump = onJump
        self.textBody = textBody()
    }

    private var prefs: NotesFormatPrefs { NotesFormatPrefs.shared }

    var body: some View {
        let id = detail.meta.id
        let payload = detail.summary?.structured
        VStack(alignment: .leading, spacing: Space.s6) {
            if prefs.generating.contains(id) {
                HStack(spacing: Space.s2) {
                    ProgressView().controlSize(.small)
                    Text("正在生成结构图…").font(Typo.caption).foregroundStyle(Ink.secondary)
                }
            }
            if let payload, payload.hasContent {
                toolbar(payload)
                if mode == .structured {
                    StructuredNotesContent(payload: payload, onJump: onJump)
                } else {
                    textBody
                }
            } else {
                textBody
                if payload?.failed == true || prefs.failed.contains(id) {
                    Text("结构图未能生成，已显示文字纪要").font(Typo.caption).foregroundStyle(Ink.secondary)
                }
            }
        }
    }

    private func toolbar(_ payload: StructuredNotesPayload) -> some View {
        let items: [(value: StructuredPaneMode, title: LocalizedStringKey)] = [(.structured, "结构图"), (.text, "文字纪要")]
        return HStack(spacing: Space.s2) {
            InkSegmented(items: items, selection: $mode).fixedSize()
            Spacer(minLength: Space.s2)
            if mode == .structured {
                Button {
                    NSPasteboard.general.clearContents()
                    NSPasteboard.general.setString(StructuredNotesMarkdown.render(payload), forType: .string)
                    copied = true
                    Task { try? await Task.sleep(for: .seconds(2)); copied = false }
                } label: {
                    Label(copied ? "已复制" : "复制结构图", systemImage: copied ? "checkmark" : "doc.on.doc")
                        .font(Typo.caption)
                        .foregroundStyle(Ink.secondary)
                }
                .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 8))
                .help("复制为 Markdown")
            }
        }
    }
}

/// One of the three structured views, with the usual footer.
struct StructuredNotesContent: View {
    let payload: StructuredNotesPayload
    let onJump: (Int) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s6) {
            switch payload.format {
            case .fishbone:
                if let fishbone = payload.fishbone { FishboneView(notes: fishbone, onJump: onJump) }
            case .outline:
                if let outline = payload.outline { OutlineView(notes: outline, onJump: onJump) }
            case .viewpoints:
                if let matrix = payload.viewpoints { ViewpointMatrixView(matrix: matrix, onJump: onJump) }
            case .standard:
                EmptyView()
            }
            footer
        }
    }

    private var footer: some View {
        var parts: [String] = []
        if !payload.model.isEmpty { parts.append(String(localized: "由 \(payload.model) 整理")) }
        if payload.droppedCount > 0 { parts.append(String(localized: "去掉了 \(payload.droppedCount) 条没有原话依据的内容")) }
        return Text(verbatim: parts.joined(separator: " · ")).font(Typo.caption).foregroundStyle(Ink.secondary)
    }
}
