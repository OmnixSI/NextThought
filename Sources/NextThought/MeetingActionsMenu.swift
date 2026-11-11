import SwiftUI
import NextThoughtCore

/// Shared actions for a meeting row's context menu and the detail page's ellipsis.
/// Every operation is keyed by `sessionID`, so a row menu never acts on the
/// currently selected meeting by accident.
struct MeetingMenuItems: View {
    @Environment(AppState.self) private var state

    let sessionID: UUID
    let onShowInfo: (UUID) -> Void
    let onRename: (UUID) -> Void
    let onDelete: (UUID) -> Void

    private var archive: SessionArchive { SessionArchive.shared }
    private var meta: SessionMeta? { archive.sessions.first { $0.id == sessionID } }
    // `.pending` is persisted across launches. Only an in-process task is busy;
    // orphaned pending states must remain retryable.
    private var busy: Bool { archive.summarizing.contains(sessionID) }
    private var fallbackReadOnly: Bool { archive.isReadOnlyFallback(sessionID) }
    private var deletionDisabled: Bool { sessionID == archive.activeID || archive.destructiveOperationDisabledReason != nil || fallbackReadOnly }

    var body: some View {
        Button("重新整理") { NotesRegeneration.shared.request(sessionID: sessionID) }
            .disabled(busy || sessionID == archive.activeID || fallbackReadOnly)
        Menu("纪要语言") {
            Button("简体中文") { NotesRegeneration.shared.request(sessionID: sessionID, english: false) }
            Button("English") { NotesRegeneration.shared.request(sessionID: sessionID, english: true) }
        }
        .disabled(busy || sessionID == archive.activeID || fallbackReadOnly)
        Divider()
        Button("导出 Markdown…") { state.exportSession(sessionID) }
        Button("重命名…") { onRename(sessionID) }
            .disabled(fallbackReadOnly)
        Button("在 Finder 中显示") { archive.revealInFinder(sessionID) }
        Button("详细信息") { onShowInfo(sessionID) }
        Button("以此为上一场，准备下一场") { state.prepareNextMeeting(after: sessionID) }
            .disabled(meta?.workspaceID == nil || state.isRunning)
        Divider()
        Button("删除这场会议", role: .destructive) { onDelete(sessionID) }
            .disabled(deletionDisabled)
            .help(sessionID == archive.activeID ? "正在记录的会议不能删除"
                  : (archive.destructiveOperationDisabledReason ?? (fallbackReadOnly ? "旧版会议记录处于只读恢复状态" : "删除这场会议")))
    }
}

struct MeetingMoreMenu: View {
    let sessionID: UUID
    let onShowInfo: (UUID) -> Void
    let onRename: (UUID) -> Void
    let onDelete: (UUID) -> Void

    var body: some View {
        Menu {
            MeetingMenuItems(sessionID: sessionID,
                             onShowInfo: onShowInfo,
                             onRename: onRename,
                             onDelete: onDelete)
        } label: {
            Image(systemName: "ellipsis")
        }
        .menuStyle(.button)
        .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 8))
        .menuIndicator(.hidden)
        .fixedSize()
        .help("更多")
    }
}
