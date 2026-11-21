import AppKit
import SwiftUI
import NextThoughtCore

/// "[12:41]" beside a node: jumps to that moment in the transcript.
struct StructuredTimeLink: View {
    let at: Int?
    let onJump: (Int) -> Void

    var body: some View {
        if let at {
            Button { onJump(at) } label: {
                Text(verbatim: "[\(NotesTime.text(at))]").font(.zoomed(size: 12).monospacedDigit()).foregroundStyle(Ink.secondary)
            }
            .buttonStyle(.plain)
            .help("跳到转写里的这句话")
        }
    }
}

/// The body of the notes tab for the view picked on the format bar: the text notes (always
/// there) or one of the fishbone, outline and viewpoint views. A view that was never written
/// shows its progress while it is written; one that failed says so in one quiet line and
/// offers 重试. The text notes are never replaced by any of them.
struct StructuredNotesPane<TextBody: View>: View {
    let detail: SessionDetail
    let format: NotesFormat
    let onJump: (Int) -> Void
    private let textBody: TextBody

    init(detail: SessionDetail, format: NotesFormat, onJump: @escaping (Int) -> Void, @ViewBuilder textBody: () -> TextBody) {
        self.detail = detail
        self.format = format
        self.onJump = onJump
        self.textBody = textBody()
    }

    var body: some View {
        if format.isStructured {
            StructuredFormatPane(detail: detail, format: format, onJump: onJump)
        } else {
            textBody
        }
    }
}

private struct StructuredFormatPane: View {
    @Environment(AppState.self) private var state
    let detail: SessionDetail
    let format: NotesFormat
    let onJump: (Int) -> Void
    @State private var copied = false

    private var jobs: StructuredNotesJobs { StructuredNotesJobs.shared }

    var body: some View {
        let id = detail.meta.id
        let key = StructuredJobKey(id: id, format: format)
        let running = jobs.isRunning(key)
        let payload = jobs.completed[key] ?? detail.summary?.payload(for: format)
        VStack(alignment: .leading, spacing: Space.s6) {
            if running {
                progress(id)
            }
            if !running, jobs.hasFailed(key) {
                if state.notesEngine.model.isEmpty {
                    HStack {
                        Text("没有可用的纪要模型。请在 设置 → 高级 → 纪要 里选择。").font(Typo.caption)
                        Spacer(minLength: Space.s2)
                        Button("设置") { state.openSettings(.notes) }.buttonStyle(SecondaryButtonStyle())
                    }.paperCard()
                } else {
                    notice(String(localized: "这次没整理成功，纪要不受影响。"), action: String(localized: "重试"), id: id)
                }
            }
            if let payload {
                if payload.sourceRevision != detail.userContent.revision {
                    HStack(spacing: Space.s2) {
                        Text(payload.sourceRevision == nil ? "此视图的来源版本未知，可按最新内容重新整理。" : "笔记或校对已更新，此视图保留上一次的结果。")
                            .font(Typo.caption).foregroundStyle(Ink.caution)
                        Spacer(minLength: Space.s2)
                        Button("更新视图") { state.generateStructuredNotes(for: id, format: format, regenerate: true) }
                            .buttonStyle(SecondaryButtonStyle()).disabled(running)
                    }
                }
                HStack {
                    Spacer(minLength: 0)
                    menu(payload, id: id, running: running)
                }
                StructuredNotesContent(payload: payload, onJump: onJump)
            } else if !running, !jobs.hasFailed(key) {
                notice(String(localized: "还没有整理过。"), action: String(localized: "开始整理"), id: id)
            }
        }
    }

    /// "正在整理成鱼骨图…" with 取消; long meetings take a while, the text notes stay usable.
    private func progress(_ id: UUID) -> some View {
        HStack(spacing: Space.s2) {
            ProgressView().controlSize(.small)
            Text(verbatim: format.progressTitle).font(Typo.body)
            Spacer(minLength: Space.s2)
            Button("取消") { state.cancelStructuredNotes(for: id, format: format) }
                .buttonStyle(GhostButtonStyle(height: 32, horizontalPadding: 10))
        }
        .paperCard()
    }

    private func notice(_ message: String, action: String, id: UUID) -> some View {
        HStack(spacing: Space.s2) {
            Text(verbatim: message).font(Typo.caption).foregroundStyle(Ink.secondary)
            Spacer(minLength: Space.s2)
            Button(action) { state.generateStructuredNotes(for: id, format: format, regenerate: true) }
                .buttonStyle(SecondaryButtonStyle())
                .disabled(detail.lines.isEmpty || state.archive.summarizing.contains(id))
        }
        .paperCard()
    }

    /// "…": write this view again, or copy it.
    private func menu(_ payload: StructuredNotesPayload, id: UUID, running: Bool) -> some View {
        Menu {
            Button("重新生成") { state.generateStructuredNotes(for: id, format: format, regenerate: true) }
                .disabled(running)
            Button(copied ? String(localized: "已复制") : String(localized: "复制为 Markdown")) {
                NSPasteboard.general.clearContents()
                NSPasteboard.general.setString(StructuredNotesMarkdown.render(payload), forType: .string)
                copied = true
                Task { try? await Task.sleep(for: .seconds(2)); copied = false }
            }
        } label: {
            Image(systemName: "ellipsis").font(.zoomed(size: 13, weight: .medium)).foregroundStyle(Ink.secondary)
        }
        .menuStyle(.button)
        .buttonStyle(GhostButtonStyle(height: 32, horizontalPadding: 10))
        .menuIndicator(.hidden)
        .fixedSize()
        .accessibilityLabel("更多")
        .help("更多")
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
