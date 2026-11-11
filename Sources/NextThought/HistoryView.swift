import AppKit
import SwiftUI
import NextThoughtCore

private struct HistoryContentSearchKey: Equatable {
    let query: String
    let dateFilter: String
    let lengthFilter: String
    let workspaceFilterID: UUID?
    let overviewFolderKey: String?
    let glossaryRevision: String
    let archive: SessionSearchArchiveStamp
}

/// 所有会议: the list on the left, the selected meeting's notes on the right (stacked
/// navigation on narrow windows).
struct HistoryView: View {
    @Environment(AppState.self) private var state
    @FocusState private var focusedSessionID: UUID?
    @State private var workspaceEditor = WorkspaceEditController.shared
    @State private var showDeleteAll = false
    @State private var deletingSessionID: UUID?
    @State private var deletingWorkspace: Workspace?
    @State private var isSelectingSeries = false
    @State private var selectedWorkspaceIDs = Set<UUID>()
    @State private var showDeleteSelectedSeries = false
    @AppStorage(AppNavigationBar.meetingsSidebarKey, store: AppPreferences.standard) private var isMeetingListHidden = false
    @State private var isCollectionOverview = false
    @State private var overviewFolderKey: String?
    @State private var informationTarget: SessionInfoTarget?
    @State private var renamingSessionID: UUID?
    @State private var renameTitle = ""
    @State private var query = ""
    @State private var dateFilter: SessionDateFilter = .all
    @State private var lengthFilter: SessionLengthFilter = .all
    @State private var sortOrder: SessionSort = .newest
    /// First content hit per meeting. Filled in by a cancellable, batched archive search.
    @State private var contentHits: [UUID: SessionSearchHit] = [:]
    /// All matching source fields per meeting, used by the result counter and previous/next controls.
    @State private var contentHitLists: [UUID: [SessionSearchHit]] = [:]
    /// Meetings with unreadable sections stay visible as possible results.
    @State private var contentSearchIssues: [UUID: String] = [:]
    /// Sessions found only through a recorded misheard spelling (术语和人名), with that spelling.
    @State private var misheardMatch: [UUID: String] = [:]
    @State private var searchCache = GlossarySearchCache()
    @State private var isContentSearching = false
    @State private var searchChecked = 0
    @State private var searchTotal = 0
    @State private var searchUnreadable = 0
    @State private var searchGeneration = UUID()
    private static let fullTextSearch = SessionSearch(root: AppPaths.sessions)

    private var archive: SessionArchive { SessionArchive.shared }
    private var selection: UUID? { state.historySelection }
    /// Workspace + date + length filters (no text search): the pool the content
    /// search walks and the pool the list is drawn from.
    private var scoped: [SessionMeta] {
        var items = archive.sessions
        if let overviewFolderKey, let folder = seriesFolders.first(where: { $0.id == overviewFolderKey }) {
            items = items.filter { folder.contains($0, workspaces: state.workspaces.workspaces) }
        } else if let filter = state.historyWorkspaceFilter {
            items = items.filter { $0.workspaceID == filter }
        }
        items = items.filter { dateFilter.matches($0.startedAt) }
        items = items.filter { lengthFilter.matches($0.duration) }
        return items
    }

    /// What the list shows: filters and search applied, then the chosen order.
    private var visibleSessions: [SessionMeta] {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        var items = scoped
        if !trimmed.isEmpty {
            items = items.filter { meta in
                let terms = searchCache.terms(for: trimmed, state: state,
                                              workspaceID: glossaryWorkspaceID(for: meta), includeAllWorkspaces: false)
                return matchesMetadata(meta, terms) || contentHits[meta.id] != nil || contentSearchIssues[meta.id] != nil
            }
        }
        return ordered(items)
    }

    var body: some View {
        GeometryReader { proxy in
            let wide = proxy.size.width >= 760
            HStack(alignment: .top, spacing: 0) {
                if wide {
                    if !isMeetingListHidden && !archive.sessions.isEmpty {
                        sessionList
                            .frame(width: 270)
                            .padding(.leading, Space.s4).padding(.trailing, Space.s2).padding(.top, Space.s1)
                        Rectangle().fill(Ink.hairline).frame(width: 0.5)
                    }
                    if let selection, archive.sessions.contains(where: { $0.id == selection }) {
                        MeetingPage(
                            id: selection,
                            showsBack: false,
                            onBack: returnToMeetingList,
                            onShowInfo: showInformation,
                            onRename: beginRenaming,
                            onDelete: deleteSession,
                            onNavigateToAllMeetings: showAllMeetings,
                            onNavigateToSeries: showSeries,
                            initialTarget: contentHits[selection]?.userContentTarget)
                            .id(selection)
                    } else {
                        archiveOverview(compact: isMeetingListHidden || archive.sessions.isEmpty)
                    }
                } else if let selection, archive.sessions.contains(where: { $0.id == selection }) {
                    MeetingPage(
                        id: selection,
                        showsBack: true,
                        onBack: returnToMeetingList,
                        onShowInfo: showInformation,
                        onRename: beginRenaming,
                        onDelete: deleteSession,
                        onNavigateToAllMeetings: showAllMeetings,
                        onNavigateToSeries: showSeries,
                            initialTarget: contentHits[selection]?.userContentTarget)
                        .id(selection)
                } else if isCollectionOverview || isMeetingListHidden {
                    archiveOverview(compact: true)
                } else {
                    sessionList
                        .padding(.horizontal, Space.s4).padding(.top, Space.s1)
                }
            }
        }
        .safeAreaInset(edge: .top, spacing: 0) {
            VStack(spacing: 0) {
                if !archive.archiveReadIssues.isEmpty {
                    archiveReadIssueNotice
                        .padding(.horizontal, Space.s4)
                        .padding(.top, Space.s2)
                        .padding(.bottom, Space.s2)
                }
                searchHitNavigator
            }
        }
        .onAppear { selectFirstIfNeeded() }
        .onChange(of: state.section) { _, section in
            if section == .meetings {
                selectFirstIfNeeded()
            } else {
                finishSeriesSelection()
            }
        }
        .onChange(of: archive.sessions.count) { _, _ in selectFirstIfNeeded() }
        .onChange(of: state.workspaces.workspaces.count) { _, _ in
            let existingIDs = Set(state.workspaces.workspaces.map(\.id))
            selectedWorkspaceIDs.formIntersection(existingIDs)
        }
        .confirmationDialog("有未保存的修改", isPresented: Binding(
            get: { workspaceEditor.showUnsavedPrompt }, set: { workspaceEditor.showUnsavedPrompt = $0 }), titleVisibility: .visible) {
            Button("保存") { workspaceEditor.saveAndContinue(state) }
            Button("放弃更改", role: .destructive) { workspaceEditor.discardAndContinue(state) }
            Button("继续编辑", role: .cancel) { workspaceEditor.continueEditing() }
        } message: {
            Text("保存后继续刚才的操作；放弃更改会丢弃本次草稿。")
        }
        .confirmationDialog("删除全部会议记录？", isPresented: $showDeleteAll, titleVisibility: .visible) {
            Button("删除会议和依赖内容", role: .destructive) {
                state.deleteAllArchivedMeetings()
                state.historySelection = nil
            }
            .disabled(archive.destructiveOperationDisabledReason != nil)
            if hasRetainableAcceptedContent(from: deleteAllSessionIDs) {
                Button("删除会议，保留已确认的系列内容", role: .destructive) {
                    state.deleteAllArchivedMeetings(retainAcceptedContent: true)
                    state.historySelection = nil
                }
                .disabled(archive.destructiveOperationDisabledReason != nil)
            }
            Button("取消", role: .cancel) { }
        } message: {
            Text("将永久删除所有归档会议的转写、音频、手记、索引、问题、回答和纪要。默认同时删除只依赖这些会议的已确认经验、决定和行动。若选择保留，内容会作为独立经验保存并标记原来源已删除，不保留会议原话。")
        }
        .confirmationDialog("删除这场会议？", isPresented: Binding(
            get: { deletingSessionID != nil }, set: { if !$0 { deletingSessionID = nil } }), titleVisibility: .visible) {
                if let id = deletingSessionID {
                    Button("删除会议和依赖内容", role: .destructive) { confirmDeleteSession(id, retainAcceptedContent: false) }
                        .disabled(archive.destructiveOperationDisabledReason != nil)
                    if hasRetainableAcceptedContent(from: [id]) {
                        Button("删除会议，保留已确认的系列内容", role: .destructive) {
                            confirmDeleteSession(id, retainAcceptedContent: true)
                        }
                        .disabled(archive.destructiveOperationDisabledReason != nil)
                    }
                }
                Button("取消", role: .cancel) { deletingSessionID = nil }
            } message: {
                Text("将永久删除这场会议的转写、音频、手记、索引、问题、回答和纪要。默认同时删除只依赖它的已确认经验、决定和行动。若选择保留，内容会作为独立经验保存并标记原来源已删除，不保留会议原话。")
        }
        .confirmationDialog("删除系列“\(deletingWorkspace?.name ?? "")”？", isPresented: Binding(
            get: { deletingWorkspace != nil },
            set: { if !$0 { deletingWorkspace = nil } }), titleVisibility: .visible) {
                Button("删除系列", role: .destructive) {
                    let workspaceID = deletingWorkspace?.id
                    deletingWorkspace = nil
                    if let workspaceID {
                        workspaceEditor.requestDeleteWorkspace(workspaceID, state: state) {
                            showAllMeetings()
                        }
                    }
                }
                Button("取消", role: .cancel) { deletingWorkspace = nil }
            } message: {
                Text("系列及其会议记录将被永久删除，无法恢复。")
            }
        .confirmationDialog("删除所选系列？", isPresented: $showDeleteSelectedSeries, titleVisibility: .visible) {
            Button("删除所选系列", role: .destructive) { deleteSelectedSeries() }
            Button("取消", role: .cancel) { }
        } message: {
            VStack(alignment: .leading, spacing: 4) {
                Text("所选系列及其会议记录将被永久删除，无法恢复。")
                if !selectedSeriesSummary.isEmpty {
                    Text(verbatim: selectedSeriesSummary)
                        .font(.caption)
                        .lineLimit(3)
                }
            }
        }
        .sheet(item: $informationTarget) { target in
            SessionInfoView(sessionID: target.id)
        }
        .alert("重命名会议", isPresented: Binding(
            get: { renamingSessionID != nil },
            set: { if !$0 { renamingSessionID = nil } })) {
                TextField("标题", text: $renameTitle)
                Button("保存") {
                    if let id = renamingSessionID { archive.rename(id, to: renameTitle) }
                    renamingSessionID = nil
                }
                Button("取消", role: .cancel) { renamingSessionID = nil }
            }
        // Re-runs the content search whenever the query or the filter scope changes;
        // `.task(id:)` cancels the previous run automatically.
        .task(id: contentSearchKey) { await runContentSearch() }
    }

    @ViewBuilder
    private var searchHitNavigator: some View {
        if let target = state.pendingTranscriptNavigation, target.sessionID == selection {
            HStack(spacing: Space.s2) {
                Text("搜索命中 \(target.selectedIndex + 1)/\(target.hits.count) · \(searchHitKindLabel(target.hit.kind))")
                    .font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
                Text(verbatim: target.hit.excerpt)
                    .font(Typo.caption).foregroundStyle(Ink.ink).lineLimit(1)
                    .frame(maxWidth: .infinity, alignment: .leading)
                Button { moveSearchHit(by: -1) } label: { Image(systemName: "chevron.up") }
                    .buttonStyle(GhostButtonStyle(height: 30, horizontalPadding: 7))
                    .disabled(target.hits.count < 2)
                    .help("上一个搜索命中")
                Button { moveSearchHit(by: 1) } label: { Image(systemName: "chevron.down") }
                    .buttonStyle(GhostButtonStyle(height: 30, horizontalPadding: 7))
                    .disabled(target.hits.count < 2)
                    .help("下一个搜索命中")
            }
            .padding(.horizontal, Space.s4)
            .padding(.vertical, Space.s2)
            .background(Ink.surface)
            .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 0.5) }
            .accessibilityElement(children: .contain)
            .accessibilityLabel("搜索结果导航，共 \(target.hits.count) 个命中")
        }
    }

    private func moveSearchHit(by offset: Int) {
        guard let target = state.pendingTranscriptNavigation, target.sessionID == selection,
              target.hits.count > 1 else { return }
        let next = (target.selectedIndex + offset + target.hits.count) % target.hits.count
        state.pendingTranscriptNavigation = SessionTranscriptNavigationTarget(
            sessionID: target.sessionID, hits: target.hits, selectedIndex: next)
    }

    private func archiveOverview(compact: Bool = false) -> some View {
        let isRoot = overviewFolderKey == nil && state.historyWorkspaceFilter == nil
        let folder = selectedOverviewFolder
        let folderSessions = folder.map { folder in
            ordered(visibleSessions.filter { folder.contains($0, workspaces: state.workspaces.workspaces) })
        } ?? visibleSessions
        let title = isRoot ? String(localized: "所有记录") : (folder?.name ?? String(localized: "未分类会议"))
        let subtitle: String
        if let folder, !isRoot {
            subtitle = folder.workspaceID == nil
                ? String(localized: "未归入系列的会议")
                : String(localized: "\(folder.modeTitle ?? String(localized: "会议系列")) · 系列共 \(folder.sessions.count) 场会议 · 当前条件匹配 \(folderSessions.count) 场")
        } else if !isRoot {
            subtitle = String(localized: "系列信息已不可用 · 当前筛选下共 \(folderSessions.count) 场会议")
        } else {
            subtitle = String(localized: "本机记录 · 当前匹配 \(visibleSessions.count) 场会议、\(visibleWorkspaceSeriesCount) 个系列 · 系列按最近匹配会议排序")
        }
        let overview = MeetingArchiveOverview(
            title: title,
            subtitle: subtitle,
            sessions: isRoot ? visibleSessions : folderSessions,
            folders: isRoot ? visibleSeriesFolders : [],
            isRoot: isRoot,
            isSelectingSeries: $isSelectingSeries,
            selectedWorkspaceIDs: $selectedWorkspaceIDs,
            selectedFolder: isRoot ? nil : folder,
            hasUnderlyingRecords: isRoot ? !archive.sessions.isEmpty : !(folder?.sessions.isEmpty ?? true),
            hasArchiveReadIssues: !archive.archiveReadIssues.isEmpty,
            hasActiveFilters: hasNarrowingFilter,
            onOpenRoot: showAllMeetings,
            onOpenFolder: showFolder,
            canManageFolder: canManageFolder,
            canSelectSeries: canDeleteFolder,
            onToggleSeriesSelection: toggleSeriesSelection,
            onDeleteSelectedSeries: { showDeleteSelectedSeries = true },
            onManageFolder: manageFolder,
            onDeleteFolder: confirmDeleteFolder,
            deleteDisabledReason: { folder in folder.workspaceID.flatMap { state.workspaceDeletionDisabledReason($0) } },
            managementDisabled: state.isRunning || archive.destructiveOperationDisabledReason != nil,
            onOpen: openFromOverview,
            onClearFilters: clearFilters,
            onReturnHome: { state.section = .home },
            onRetryArchiveRead: { archive.load() },
            searchHits: contentHits)
        return Group {
            if !query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                searchResults(compact: compact)
            } else if compact {
                VStack(spacing: 0) {
                    filterBar
                        .padding(.horizontal, 16)
                        .padding(.top, 10)
                        .padding(.bottom, 6)
                    overview
                }
            } else {
                overview
            }
        }
    }

    /// A folder for each saved workspace/series, plus a fallback for legacy sessions
    /// that predate workspace grouping.
    private var seriesFolders: [MeetingSeriesFolder] {
        let workspaces = state.workspaces.workspaces
        var grouped = Dictionary(grouping: archive.sessions) { seriesFolderID(for: $0, workspaces: workspaces) }
        for workspace in workspaces where !workspace.archived {
            let workspaceKey = "workspace:\(workspace.id.uuidString)"
            if grouped[workspaceKey] == nil { grouped[workspaceKey] = [] }
        }

        return grouped.map { key, sessions in
            let id = key.hasPrefix("workspace:") ? UUID(uuidString: String(key.dropFirst("workspace:".count))) : nil
            let workspace = id.flatMap { state.workspaces.workspace($0) }
            let name = workspace?.name ?? String(localized: "未分类会议")
            let modeTitle = workspace?.typeDisplayName ?? sessions.first?.mode.title
            let folderMode = workspace?.effectiveMode ?? sessions.first?.mode ?? .general
            let lastSessionAt = sessions.map(\.startedAt).max()
            let updatedAt = lastSessionAt ?? workspace?.updatedAt ?? .distantPast
            return MeetingSeriesFolder(id: key, workspaceID: id, name: name, modeTitle: modeTitle,
                                       sessions: sessions, lastMeetingAt: lastSessionAt, updatedAt: updatedAt, mode: folderMode)
        }
        .sorted { lhs, rhs in
            lhs.updatedAt == rhs.updatedAt ? lhs.name.localizedStandardCompare(rhs.name) == .orderedAscending : lhs.updatedAt > rhs.updatedAt
        }
    }

    private func seriesFolderID(for session: SessionMeta, workspaces: [Workspace]) -> String {
        if session.workspaceDeleted == true { return "legacy:unclassified" }
        if let workspaceID = session.workspaceID {
            if workspaces.contains(where: { $0.id == workspaceID }) { return "workspace:\(workspaceID.uuidString)" }
            return "legacy:unclassified"
        }
        if let workspaceID = workspaces.first(where: { $0.name == session.workspaceName })?.id {
            return "workspace:\(workspaceID.uuidString)"
        }
        return "legacy:unclassified"
    }

    private var visibleWorkspaceSeriesCount: Int {
        visibleSeriesFolders.filter { $0.workspaceID != nil }.count
    }

    private var selectedOverviewFolder: MeetingSeriesFolder? {
        if let overviewFolderKey, let folder = seriesFolders.first(where: { $0.id == overviewFolderKey }) { return folder }
        if let workspaceID = state.historyWorkspaceFilter {
            return seriesFolders.first(where: { $0.workspaceID == workspaceID })
        }
        return nil
    }

    private var hasNarrowingFilter: Bool {
        !query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            || dateFilter != .all || lengthFilter != .all
    }

    private var isScopedToSeries: Bool {
        overviewFolderKey != nil || state.historyWorkspaceFilter != nil
    }

    private var currentScopeSessionCount: Int {
        if let folder = selectedOverviewFolder { return folder.sessions.count }
        if let workspaceID = state.historyWorkspaceFilter {
            return archive.sessions.filter { $0.workspaceID == workspaceID }.count
        }
        return archive.sessions.count
    }

    private var visibleSeriesFolders: [MeetingSeriesFolder] {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        let candidateFolders = isScopedToSeries ? selectedOverviewFolder.map { [$0] } ?? [] : seriesFolders
        let visible: [MeetingSeriesFolder] = candidateFolders.compactMap { folder -> MeetingSeriesFolder? in
            let matching = ordered(visibleSessions.filter { folder.contains($0, workspaces: state.workspaces.workspaces) })
            let nameMatches = !trimmed.isEmpty && folder.name.localizedCaseInsensitiveContains(trimmed)
            let hasNarrowingFilter = !trimmed.isEmpty || dateFilter != .all || lengthFilter != .all
            guard !matching.isEmpty || nameMatches || !hasNarrowingFilter else { return nil }
            return folder.withSessions(matching)
        }
        return visible.sorted { lhs, rhs in
            let left = lhs.lastMeetingAt ?? .distantPast
            let right = rhs.lastMeetingAt ?? .distantPast
            return left == right
                ? lhs.name.localizedStandardCompare(rhs.name) == .orderedAscending
                : left > right
        }
    }

    /// Keep each series visible once and order groups by their most recent
    /// matching meeting; `sortOrder` is applied only within each series.
    private var visibleSessionGroups: [MeetingSeriesSessionGroup] {
        let workspaces = state.workspaces.workspaces
        let foldersByID = Dictionary(uniqueKeysWithValues: seriesFolders.map { ($0.id, $0) })
        var grouped: [String: [SessionMeta]] = [:]

        for session in visibleSessions {
            let folderID = seriesFolderID(for: session, workspaces: workspaces)
            guard foldersByID[folderID] != nil else { continue }
            grouped[folderID, default: []].append(session)
        }

        let groups: [MeetingSeriesSessionGroup] = visibleSeriesFolders.compactMap { visibleFolder -> MeetingSeriesSessionGroup? in
            let id = visibleFolder.id
            guard let folder = foldersByID[id] else { return nil }
            let sessions = grouped[id] ?? []
            return MeetingSeriesSessionGroup(folder: folder.withSessions(sessions), sessions: sessions)
        }
        return groups.sorted { lhs, rhs in
            let left = lhs.sessions.map(\.startedAt).max() ?? .distantPast
            let right = rhs.sessions.map(\.startedAt).max() ?? .distantPast
            return left == right
                ? lhs.folder.name.localizedStandardCompare(rhs.folder.name) == .orderedAscending
                : left > right
        }
    }

    private func showInformation(_ id: UUID) {
        informationTarget = SessionInfoTarget(id: id)
    }

    private func selectFirstIfNeeded() {
        guard !isCollectionOverview else { return }
        if selection == nil || !visibleSessions.contains(where: { $0.id == selection }) {
            state.historySelection = visibleSessions.first?.id
        }
    }

    private func openFromOverview(_ id: UUID) {
        isCollectionOverview = false
        if let hits = contentHitLists[id], !hits.isEmpty {
            state.pendingTranscriptNavigation = SessionTranscriptNavigationTarget(sessionID: id, hits: hits)
        } else {
            state.pendingTranscriptNavigation = nil
        }
        state.historySelection = id
    }

    private func beginRenaming(_ id: UUID) {
        renameTitle = archive.sessions.first(where: { $0.id == id })?.displayTitle ?? ""
        renamingSessionID = id
    }

    private func deleteSession(_ id: UUID) {
        deletingSessionID = id
    }

    private var deleteAllSessionIDs: [UUID] {
        archive.sessions.filter { $0.id != archive.activeID }.map(\.id)
    }

    private func hasRetainableAcceptedContent(from sessionIDs: [UUID]) -> Bool {
        let deletedIDs = Set(sessionIDs)
        return state.workspaces.workspaces.contains {
            WorkspaceSourceIntegrity.hasRetainableAcceptedContent(in: $0, from: deletedIDs)
        }
    }

    private func confirmDeleteSession(_ id: UUID, retainAcceptedContent: Bool) {
        state.deleteArchivedMeeting(id, retainAcceptedContent: retainAcceptedContent)
        deletingSessionID = nil
        if state.historySelection == id {
            isCollectionOverview = true
            state.historySelection = nil
        }
    }

    private func showAllMeetings() {
        finishSeriesSelection()
        isCollectionOverview = true
        overviewFolderKey = nil
        state.historyWorkspaceFilter = nil
        state.historySelection = nil
    }

    private func showSeries(_ id: UUID) {
        finishSeriesSelection()
        isCollectionOverview = true
        overviewFolderKey = "workspace:\(id.uuidString)"
        state.historyWorkspaceFilter = id
        state.historySelection = nil
    }

    private func showFolder(_ folder: MeetingSeriesFolder) {
        finishSeriesSelection()
        isCollectionOverview = true
        overviewFolderKey = folder.id
        state.historyWorkspaceFilter = folder.workspaceID
        state.historySelection = nil
    }

    private func manageFolder(_ folder: MeetingSeriesFolder) {
        guard !state.isRunning, let id = folder.workspaceID, state.workspaces.workspace(id) != nil else { return }
        workspaceEditor.requestEditWorkspaceInfo(id, state: state)
    }

    private func canManageFolder(_ folder: MeetingSeriesFolder) -> Bool {
        folder.workspaceID.flatMap { state.workspaces.workspace($0) } != nil
    }

    private func canDeleteFolder(_ folder: MeetingSeriesFolder) -> Bool {
        guard let id = folder.workspaceID else { return false }
        return state.workspaceDeletionDisabledReason(id) == nil
    }

    private func confirmDeleteFolder(_ folder: MeetingSeriesFolder) {
        guard let id = folder.workspaceID, let workspace = state.workspaces.workspace(id), canDeleteFolder(folder) else { return }
        deletingWorkspace = workspace
    }

    private var selectedWorkspaceIDsInStore: [UUID] {
        state.workspaces.workspaces.filter { selectedWorkspaceIDs.contains($0.id) }.map(\.id)
    }

    private var selectedSeriesSummary: String {
        state.workspaces.workspaces
            .filter { selectedWorkspaceIDs.contains($0.id) }
            .map(\.name)
            .joined(separator: "、")
    }

    private func toggleSeriesSelection(_ id: UUID) {
        if selectedWorkspaceIDs.contains(id) {
            selectedWorkspaceIDs.remove(id)
        } else if state.workspaceDeletionDisabledReason(id) == nil {
            selectedWorkspaceIDs.insert(id)
        }
    }

    private func finishSeriesSelection() {
        isSelectingSeries = false
        selectedWorkspaceIDs.removeAll()
    }

    private func deleteSelectedSeries() {
        let ids = selectedWorkspaceIDsInStore
        guard !ids.isEmpty else { finishSeriesSelection(); return }
        workspaceEditor.requestDeleteWorkspaces(ids, state: state) {
            finishSeriesSelection()
            showAllMeetings()
        }
    }

    @ViewBuilder
    private func seriesGroupHeader(_ folder: MeetingSeriesFolder, meetingCount: Int) -> some View {
        let header = HStack(spacing: 4) {
            Button { showFolder(folder) } label: {
                HStack(spacing: Space.s2) {
                    SceneGlyph(mode: folder.mode, size: 24)
                    Text(verbatim: folder.name)
                        .font(Typo.label)
                        .foregroundStyle(Ink.ink)
                        .lineLimit(1)
                        .truncationMode(.middle)
                    Spacer(minLength: Space.s1)
                    Text(meetingCount.formatted())
                        .font(Typo.caption.monospacedDigit())
                        .foregroundStyle(Ink.secondary)
                    Image(systemName: "chevron.right")
                        .font(.system(size: 9, weight: .semibold))
                        .foregroundStyle(Ink.tertiary)
                }
                .padding(.horizontal, Space.s3)
                .padding(.vertical, Space.s2)
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .accessibilityLabel("\(folder.name)，\(meetingCount) 场会议")
            .accessibilityIdentifier("history.seriesGroup.\(folder.id)")
            .help("查看系列：\(folder.name)")

            if canManageFolder(folder), !isSelectingSeries {
                Menu {
                    SeriesManagementMenuItems(
                        deleteDisabledReason: folder.workspaceID.flatMap { state.workspaceDeletionDisabledReason($0) },
                        managementDisabled: state.isRunning || archive.destructiveOperationDisabledReason != nil,
                        onEdit: { manageFolder(folder) },
                        onDelete: { confirmDeleteFolder(folder) })
                } label: {
                    Image(systemName: "ellipsis")
                        .frame(width: 25, height: 25)
                        .contentShape(Rectangle())
                }
                .menuStyle(.button)
                .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 0))
                .menuIndicator(.hidden)
                .fixedSize()
                .accessibilityLabel("管理系列")
                .accessibilityHint(Text(verbatim: folder.name))
                .help("管理系列")
            }
        }

        if canManageFolder(folder), !isSelectingSeries {
            header.contextMenu {
                SeriesManagementMenuItems(
                    deleteDisabledReason: folder.workspaceID.flatMap { state.workspaceDeletionDisabledReason($0) },
                    managementDisabled: state.isRunning || archive.destructiveOperationDisabledReason != nil,
                    onEdit: { manageFolder(folder) },
                    onDelete: { confirmDeleteFolder(folder) })
            }
        } else {
            header
        }
    }

    private func returnToMeetingList() {
        isMeetingListHidden = false
        isCollectionOverview = true
        state.historySelection = nil
    }

    private var sessionListTitle: String {
        if let overviewFolder = selectedOverviewFolder { return overviewFolder.name }
        guard let workspaceID = state.historyWorkspaceFilter else { return String(localized: "所有记录") }
        return state.workspaces.workspace(workspaceID)?.name ?? String(localized: "未分类会议")
    }

    private var sessionList: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(sessionListTitle).font(Typo.section).foregroundStyle(Ink.ink).lineLimit(2)
                    Text(state.historyWorkspaceFilter == nil
                         ? (overviewFolderKey != nil ? "这个系列中的会议记录" : (state.recordSessions ? "自动保存 · 不保存录音" : "记录已关闭（设置 → 数据）"))
                         : "这个系列中的会议记录")
                        .font(Typo.caption)
                        .foregroundStyle(state.historyWorkspaceFilter == nil && !state.recordSessions ? Ink.caution : Ink.secondary)
                }
                Spacer()
                Menu {
                    Button(role: .destructive) {
                        showDeleteAll = true
                    } label: {
                        Label("删除全部会议记录", systemImage: "trash")
                    }
                } label: {
                    Image(systemName: "ellipsis")
                        .frame(width: 28, height: 28)
                        .contentShape(Rectangle())
                }
                .menuStyle(.button)
                .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 0))
                .menuIndicator(.hidden)
                .accessibilityLabel("会议记录操作")
                .disabled(archive.sessions.isEmpty || archive.destructiveOperationDisabledReason != nil)
                .help(archive.destructiveOperationDisabledReason ?? "会议记录操作；删除会影响所有系列")
            }
            filterBar
            if !archive.archiveReadIssues.isEmpty && currentScopeSessionCount == 0 {
                archiveUnavailableEmptyState
            } else if isScopedToSeries && currentScopeSessionCount == 0 {
                VStack(spacing: Space.s4) {
                    quietEmpty("这个系列还没有会议", "开始一场会议后，它会出现在这个系列里。")
                    Button("返回所有记录") { showAllMeetings() }
                        .buttonStyle(SecondaryButtonStyle())
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if archive.sessions.isEmpty {
                quietEmpty("还没有会议", "开过的会议会保存在这里；结束时自动整理纪要。")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if isContentSearching && visibleSessions.isEmpty {
                ProgressView("正在搜索 \(searchChecked)/\(searchTotal) 场…")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if visibleSessions.isEmpty && visibleSessionGroups.isEmpty {
                if isContentSearching {
                    VStack(spacing: Space.s3) {
                        ProgressView(value: Double(searchChecked), total: Double(max(searchTotal, 1)))
                            .frame(maxWidth: 220)
                        quietEmpty(searchIsScopedToSeries ? "正在搜索当前系列内容" : "正在搜索全部会议内容",
                                   "已检查 \(searchChecked) / \(searchTotal) 场；完成后才会确定没有匹配项。")
                    }
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else {
                    VStack(spacing: Space.s4) {
                        quietEmpty("没有符合条件的记录",
                                   isScopedToSeries
                                   ? "当前系列有 \(currentScopeSessionCount) 场会议；当前搜索或筛选没有匹配项。"
                                   : "当前搜索或筛选条件下没有记录，可以清除条件后查看全部。")
                        Button("清除搜索与筛选") { clearFilters() }
                            .buttonStyle(SecondaryButtonStyle())
                            .help("清除搜索、筛选和排序，保留当前系列范围")
                    }
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            } else {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: Space.s4) {
                        ForEach(visibleSessionGroups) { group in
                            VStack(alignment: .leading, spacing: 0) {
                                seriesGroupHeader(group.folder, meetingCount: group.sessions.count)

                                ForEach(group.sessions) { meta in
                                    Button { openFromOverview(meta.id) } label: {
                                        SessionRow(meta: meta, selected: meta.id == selection,
                                                   active: meta.id == archive.activeID, showsWorkspace: false,
                                                   misheardForm: misheardMatch[meta.id],
                                                   contentHit: contentHits[meta.id])
                                            .frame(maxWidth: .infinity, alignment: .leading)
                                    }
                                    .buttonStyle(InteractiveRowButtonStyle())
                                    .focused($focusedSessionID, equals: meta.id)
                                    .overlay {
                                        if focusedSessionID == meta.id {
                                            RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous)
                                                .strokeBorder(Ink.focus, lineWidth: 1.5)
                                                .padding(1)
                                        }
                                    }
                                    .accessibilityLabel(historyRowAccessibilityLabel(meta, hit: contentHits[meta.id]))
                                    .accessibilityHint("打开会议纪要和转写")
                                    .accessibilityAddTraits(meta.id == selection ? .isSelected : [])
                                    .accessibilityIdentifier("history.session.\(meta.id.uuidString)")
                                    .contextMenu {
                                        MeetingMenuItems(sessionID: meta.id,
                                                         onShowInfo: showInformation,
                                                         onRename: beginRenaming,
                                                         onDelete: deleteSession)
                                    }
                                    Rectangle().fill(Ink.hairline).frame(height: 0.5)
                                        .padding(.horizontal, Space.s3)
                                }
                            }
                        }
                    }
                }
            }
        }
        .padding(.top, Space.s2)
        .frame(maxHeight: .infinity, alignment: .top)
    }

    /// One serif line, one caption line, centred; no icon, no box.
    private func quietEmpty(_ title: LocalizedStringKey, _ detail: LocalizedStringKey, tone: Color = Ink.secondary) -> some View {
        VStack(spacing: Space.s2) {
            Text(title).font(Typo.section).foregroundStyle(Ink.ink)
            Text(detail).font(Typo.caption).foregroundStyle(tone).fixedSize(horizontal: false, vertical: true)
        }
        .multilineTextAlignment(.center)
        .padding(.horizontal, Space.s3)
    }

    // MARK: Search, filter and sort

    /// Search stays visible above the three core controls.
    private var filterBar: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            searchField
            HStack(spacing: Space.s1) {
                HistoryFilterPicker(selection: $dateFilter, options: SessionDateFilter.allCases.map { ($0, $0.rawValue) })
                HistoryFilterPicker(selection: $lengthFilter, options: SessionLengthFilter.allCases.map { ($0, $0.rawValue) })
                HistoryFilterPicker(selection: $sortOrder, options: SessionSort.allCases.map { ($0, $0.rawValue) })
                Spacer(minLength: 0)
            }
            .help("系列按最近匹配的会议排序；以下排序只作用于组内会议。")
            Text(verbatim: "\(searchScopeLabel) · \(resultCount)")
                .font(Typo.caption).foregroundStyle(Ink.secondary)
                .lineLimit(2).fixedSize(horizontal: false, vertical: true)
            if let affected = scoped.first(where: { contentSearchIssues[$0.id] != nil }) {
                HStack(alignment: .top, spacing: 7) {
                    Image(systemName: "exclamationmark.triangle.fill")
                    Text("有 \(contentSearchIssues.count) 场会议内容无法完整读取，搜索结果可能不全。")
                        .fixedSize(horizontal: false, vertical: true)
                    Spacer(minLength: 0)
                    Button("查看会议") { openFromOverview(affected.id) }
                        .buttonStyle(SecondaryButtonStyle())
                        .fixedSize()
                }
                .font(Typo.caption)
                .foregroundStyle(Ink.caution)
                .accessibilityElement(children: .contain)
            }
        }
    }

    private var archiveReadIssueNotice: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            HStack(alignment: .top, spacing: Space.s2) {
                Image(systemName: "exclamationmark.triangle.fill")
                    .foregroundStyle(Ink.caution)
                    .accessibilityHidden(true)
                VStack(alignment: .leading, spacing: 4) {
                    Text("会议记录未能完整读取")
                        .font(.system(size: 12, weight: .semibold))
                    ForEach(Array(archive.archiveReadIssues.enumerated()), id: \.offset) { _, issue in
                        Text(issue.message)
                            .font(.system(size: 11))
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
                Spacer(minLength: 0)
            }
            Button("重试读取") { archive.load() }
                .buttonStyle(SecondaryButtonStyle())
                .controlSize(.small)
        }
        .foregroundStyle(Ink.ink)
        .padding(Space.s3)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Ink.caution.opacity(0.10), in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
        .accessibilityElement(children: .contain)
    }

    private var archiveUnavailableEmptyState: some View {
        VStack(spacing: Space.s4) {
            quietEmpty("无法确认会议记录为空", "归档目录或会议元数据读取失败。修复读取问题并重试后，才能确认此处没有会议。", tone: Ink.caution)
            Button("重试读取") { archive.load() }
                .buttonStyle(SecondaryButtonStyle())
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    /// A capsule that mirrors the `MenuPicker` look. The glass is drawn as a
    /// background layer so the text field keeps normal text colors.
    private var searchField: some View {
        HStack(spacing: Space.s2) {
            Image(systemName: "magnifyingglass")
                .font(.system(size: 11, weight: .medium))
                .foregroundStyle(Ink.secondary)
            TextField(LocalizedStringKey(searchPlaceholder), text: $query)
                .textFieldStyle(.plain)
                .font(Typo.label)
                .lineLimit(1)
                .accessibilityLabel(searchScopeLabel)
            if !query.isEmpty {
                Button { query = "" } label: { Image(systemName: "xmark.circle.fill") }
                    .buttonStyle(TapButtonStyle(minWidth: 30, minHeight: 28))
                    .font(.system(size: 11))
                    .foregroundStyle(Ink.secondary)
                    .help("清空搜索")
            }
        }
        .padding(.horizontal, Space.s3)
        .frame(height: Space.control + 2)
        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
    }

    private var resultCount: String {
        let base = searchIsScopedToSeries
            ? String(localized: "\(visibleSessions.count) 场会议 · 系列共 \(currentScopeSessionCount) 场")
            : String(localized: "\(visibleSessions.count) 场会议 · \(visibleWorkspaceSeriesCount) 个系列")
        return isContentSearching ? base + String(localized: " · 已搜索 \(searchChecked)/\(searchTotal) 场") : base
    }

    private var searchIsScopedToSeries: Bool {
        overviewFolderKey != nil || state.historyWorkspaceFilter != nil
    }

    private var searchScopeLabel: String {
        searchIsScopedToSeries ? String(localized: "搜索范围：当前系列") : String(localized: "搜索范围：全部记录")
    }

    private var searchPlaceholder: String {
        searchIsScopedToSeries ? "搜索此系列的转写、纪要、笔记" : "搜索全部转写、纪要、笔记"
    }

    /// Clear search and filters while keeping the current series scope.
    private func clearFilters() {
        query = ""
        dateFilter = .all
        lengthFilter = .all
        sortOrder = .newest
        contentHits = [:]
        contentHitLists = [:]
        contentSearchIssues = [:]
        misheardMatch = [:]
        state.pendingTranscriptNavigation = nil
    }

    /// Metadata-only match: always available, no disk access.
    private func matchesMetadata(_ meta: SessionMeta, _ terms: GlossarySearch) -> Bool {
        let fields = [meta.displayTitle, meta.title ?? "", meta.workspaceName ?? "", meta.formerWorkspaceName ?? "",
                      meta.mode.title, meta.audioSource, meta.recognizer]
        return fields.contains { terms.appMatch(in: $0) != nil }
    }

    /// Sort is applied after filtering. Rows that compare equal keep their
    /// original order, so the list never shuffles for no reason.
    private func ordered(_ items: [SessionMeta]) -> [SessionMeta] {
        items.enumerated()
            .sorted { lhs, rhs in
                let left = sortOrder.ascendingKey(lhs.element)
                let right = sortOrder.ascendingKey(rhs.element)
                return left == right ? lhs.offset < rhs.offset : left < right
            }
            .map(\.element)
    }

    /// Re-runs the content search when the query or the filter scope changes.
    private var contentSearchKey: HistoryContentSearchKey {
        HistoryContentSearchKey(query: query,
                                dateFilter: dateFilter.rawValue,
                                lengthFilter: lengthFilter.rawValue,
                                workspaceFilterID: state.historyWorkspaceFilter,
                                overviewFolderKey: overviewFolderKey,
                                glossaryRevision: state.glossaryRevision,
                                archive: SessionSearchArchiveStamp(sessions: archive.sessions,
                                                                  activeSessionID: archive.activeID,
                                                                  pendingTranslationSessionIDs: archive.pendingTranslationSearchSessionIDs,
                                                                  searchableContentRevision: archive.searchableContentRevision))
    }

    /// Archived corrections and aliases belong to the session's own series. An unclassified or
    /// deleted series uses only the global table, avoiding unrelated series aliases.
    private func glossaryWorkspaceID(for meta: SessionMeta) -> UUID? {
        guard meta.workspaceDeleted != true else { return nil }
        if let id = meta.workspaceID, state.workspaces.workspaces.contains(where: { $0.id == id }) { return id }
        for name in [meta.workspaceName, meta.formerWorkspaceName].compactMap({ $0 }) {
            if let workspace = state.workspaces.workspaces.first(where: { $0.name == name }) { return workspace.id }
        }
        return nil
    }

    /// Content text lives in the session files, so it is loaded off the main thread one meeting
    /// at a time. Results are published in small batches; changing the query cancels the old scan.
    @MainActor
    private func runContentSearch() async {
        let generation = UUID(); searchGeneration = generation
        contentHits = [:]; contentHitLists = [:]; contentSearchIssues = [:]; misheardMatch = [:]
        searchUnreadable = 0; searchChecked = 0
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { isContentSearching = false; return }
        let sessionsToSearch = scoped.filter { $0.id != archive.activeID && !archive.pendingTranslationSearchSessionIDs.contains($0.id) }
        let ids = sessionsToSearch.map(\.id)
        searchTotal = ids.count; isContentSearching = true
        defer { if searchGeneration == generation { isContentSearching = false } }
        do {
            try await Task.sleep(for: .milliseconds(350))
            try Task.checkCancellation()
            let terms = searchCache.terms(for: trimmed, state: state)
            var termsBySession: [UUID: GlossarySearch] = [:]
            var demo: [UUID: SessionSearchSnapshot] = [:]
            var initialIssues: [UUID: String] = [:]
            for meta in sessionsToSearch {
                try Task.checkCancellation()
                termsBySession[meta.id] = searchCache.terms(for: trimmed, state: state,
                    workspaceID: glossaryWorkspaceID(for: meta), includeAllWorkspaces: false)
                if archive.persistenceBySession[meta.id]?.pendingEvents ?? 0 > 0, !(await archive.flush(sessionID: meta.id)) {
                    initialIssues[meta.id] = archive.persistenceBySession[meta.id]?.lastFailure ?? String(localized: "归档尚未全部保存。")
                }
                if DemoMode.isEnabled || archive.isReadOnlyFallback(meta.id), let detail = await archive.detail(meta.id) {
                    var snapshot = SessionSearchSnapshot(meta: detail.meta, lines: detail.lines, questions: detail.questions, answers: detail.answers,
                        notes: detail.notes, summary: detail.summary, user: detail.userContent)
                    snapshot.readIssues = detail.readIssues.map(\.message)
                    demo[meta.id] = snapshot
                }
            }
            let readIssues = initialIssues
            let result = try await Self.fullTextSearch.search(ids: ids, terms: terms,
                revision: archive.searchRevision &+ archive.searchableContentRevision,
                termsBySession: termsBySession, demo: demo) { result in
                Task { @MainActor in
                    guard searchGeneration == generation else { return }
                    acceptSearch(result, initialIssues: readIssues)
                }
            }
            guard !Task.isCancelled, searchGeneration == generation else { return }
            acceptSearch(result, initialIssues: readIssues)
        } catch is CancellationError { }
        catch {
            if searchGeneration == generation {
                contentSearchIssues = Dictionary(uniqueKeysWithValues: ids.map { ($0, error.localizedDescription) })
                searchUnreadable = ids.count
            }
        }
    }

    private func acceptSearch(_ result: SessionSearchProgress, initialIssues: [UUID: String] = [:]) {
        guard result.checked >= searchChecked else { return }
        searchChecked = result.checked; searchTotal = result.total
        contentHits = result.hits; contentHitLists = result.hitLists
        contentSearchIssues = initialIssues.merging(result.issues) { $0 + "\n" + $1 }
        searchUnreadable = contentSearchIssues.count
        misheardMatch = result.hits.compactMapValues(\.misheardForm)
    }

    private func searchResults(compact: Bool) -> some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            if compact { filterBar }
            Text("搜索结果").font(Typo.display)
            Text(isContentSearching ? String(localized: "正在搜索：\(searchChecked)/\(searchTotal) 场") : String(localized: "找到 \(visibleSessions.count) 场记录"))
                .font(Typo.caption).foregroundStyle(Ink.secondary)
            if searchUnreadable > 0 {
                Label("\(searchUnreadable) 场记录未能读取，结果可能不完整。", systemImage: "exclamationmark.triangle")
                    .font(Typo.caption).foregroundStyle(Ink.caution)
            }
            if visibleSessions.isEmpty {
                if isContentSearching { ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity) }
                else {
                    VStack(spacing: Space.s4) {
                        quietEmpty("没有匹配的内容", "可换一个词，或清除日期和时长筛选。")
                        Button("清除搜索与筛选") { clearFilters() }.buttonStyle(SecondaryButtonStyle())
                    }
                    .frame(maxWidth: .infinity, minHeight: 180)
                }
            } else {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 0) {
                        ForEach(visibleSessions) { meta in
                            Button { openFromOverview(meta.id) } label: {
                                SessionRow(meta: meta, selected: false, active: meta.id == archive.activeID,
                                    misheardForm: misheardMatch[meta.id], contentHit: contentHits[meta.id])
                                    .frame(maxWidth: .infinity, alignment: .leading)
                            }.buttonStyle(InteractiveRowButtonStyle())
                            Rectangle().fill(Ink.hairline).frame(height: 0.5)
                        }
                    }
                }
            }
        }.padding(Space.s6).frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

}

@MainActor
private func historyRowAccessibilityLabel(_ meta: SessionMeta, hit: SessionSearchHit?) -> String {
    let meeting = String(localized: "打开会议：\(meta.displayTitle)，\(MeetingOutcome.when(meta.startedAt))，\(MeetingOutcome.line(for: meta))")
    guard let hit else { return meeting }
    let location = hit.location.map { " · \($0)" } ?? ""
    let freshness = searchHitFreshnessLabel(hit).map { "，\($0)" } ?? ""
    return String(localized: "\(meeting)，命中\(searchHitKindLabel(hit.kind))\(location)\(freshness)：\(hit.excerpt)")
}

private func searchHitFreshnessLabel(_ hit: SessionSearchHit) -> String? {
    if hit.kind == .translation && hit.isStale == true { return String(localized: "译文可能已过期") }
    switch hit.sourceStatus {
    case .some(.stale): return String(localized: "纪要内容可能已过期")
    case .some(.unknown): return String(localized: "纪要内容的来源版本不明")
    case .some(.current), .none: return nil
    }
}

private func searchHitKindLabel(_ kind: SessionSearchFieldKind) -> String {
    switch kind {
    case .transcript: String(localized: "原话")
    case .correctedTranscript: String(localized: "校正原话")
    case .translation: String(localized: "译文")
    case .question: String(localized: "问题")
    case .answer: String(localized: "回答")
    case .notes: String(localized: "纪要")
    }
}

private struct HistoryFilterPicker<Value: Hashable>: View {
    @Binding var selection: Value
    let options: [(Value, String)]

    var body: some View {
        Menu {
            ForEach(options.indices, id: \.self) { index in
                let option = options[index]
                Button { selection = option.0 } label: {
                    if option.0 == selection { Label(LocalizedStringKey(option.1), systemImage: "checkmark") }
                    else { Text(LocalizedStringKey(option.1)) }
                }
            }
        } label: {
            let isDefault = options.first.map { $0.0 == selection } ?? true
            HStack(spacing: 3) {
                Text(LocalizedStringKey(options.first(where: { $0.0 == selection })?.1 ?? "筛选"))
                    .lineLimit(1)
                Image(systemName: "chevron.down")
                    .font(.system(size: 8, weight: .semibold))
            }
            .font(Typo.caption.weight(.medium))
            .foregroundStyle(isDefault ? Ink.secondary : Ink.ink)
            .padding(.horizontal, Space.s2)
            .frame(minHeight: Hit.minHeight)
            .background(isDefault ? Color.clear : Ink.sunken, in: Capsule())
            .contentShape(Capsule())
        }
        .menuStyle(.button).buttonStyle(.plain).menuIndicator(.hidden)
        .fixedSize()
        .help(options.first(where: { $0.0 == selection })?.1 ?? "筛选")
    }
}

private struct SessionInfoTarget: Identifiable {
    let id: UUID
}

private struct MeetingSeriesFolder: Identifiable {
    let id: String
    let workspaceID: UUID?
    let name: String
    let modeTitle: String?
    let sessions: [SessionMeta]
    let lastMeetingAt: Date?
    let updatedAt: Date
    var mode: SessionMode = .general

    func withSessions(_ sessions: [SessionMeta]) -> MeetingSeriesFolder {
        MeetingSeriesFolder(id: id, workspaceID: workspaceID, name: name, modeTitle: modeTitle,
                            sessions: sessions, lastMeetingAt: sessions.map(\.startedAt).max(), updatedAt: updatedAt, mode: mode)
    }

    func contains(_ session: SessionMeta, workspaces: [Workspace]) -> Bool {
        let isUnclassified = id == "legacy:unclassified"
        if session.workspaceDeleted == true {
            return isUnclassified
        }
        if let sessionWorkspaceID = session.workspaceID {
            if workspaces.contains(where: { $0.id == sessionWorkspaceID }) {
                return workspaceID == sessionWorkspaceID
            }
            return isUnclassified
        }
        let resolvedID = workspaces.first(where: { $0.name == session.workspaceName })?.id
        if let workspaceID { return resolvedID == workspaceID }
        return resolvedID == nil && isUnclassified
    }
}

private struct MeetingSeriesSessionGroup: Identifiable {
    let folder: MeetingSeriesFolder
    let sessions: [SessionMeta]

    var id: String { folder.id }
}

private struct MeetingArchiveOverview: View {
    let title: String
    let subtitle: String
    let sessions: [SessionMeta]
    let folders: [MeetingSeriesFolder]
    let isRoot: Bool
    @Binding var isSelectingSeries: Bool
    @Binding var selectedWorkspaceIDs: Set<UUID>
    let selectedFolder: MeetingSeriesFolder?
    let hasUnderlyingRecords: Bool
    let hasArchiveReadIssues: Bool
    let hasActiveFilters: Bool
    let onOpenRoot: () -> Void
    let onOpenFolder: (MeetingSeriesFolder) -> Void
    let canManageFolder: (MeetingSeriesFolder) -> Bool
    let canSelectSeries: (MeetingSeriesFolder) -> Bool
    let onToggleSeriesSelection: (UUID) -> Void
    let onDeleteSelectedSeries: () -> Void
    let onManageFolder: (MeetingSeriesFolder) -> Void
    let onDeleteFolder: (MeetingSeriesFolder) -> Void
    let deleteDisabledReason: (MeetingSeriesFolder) -> String?
    let managementDisabled: Bool
    let onOpen: (UUID) -> Void
    let onClearFilters: () -> Void
    let onReturnHome: () -> Void
    let onRetryArchiveRead: () -> Void
    let searchHits: [UUID: SessionSearchHit]
    @FocusState private var focusedSessionID: UUID?
    @FocusState private var focusedFolderID: String?

    private func quietNote(_ title: LocalizedStringKey, _ detail: LocalizedStringKey) -> some View {
        VStack(spacing: Space.s2) {
            Text(title).font(Typo.section).foregroundStyle(Ink.ink)
            Text(detail).font(Typo.caption).foregroundStyle(Ink.secondary)
        }
        .multilineTextAlignment(.center)
        .frame(maxWidth: .infinity)
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: Space.s2) {
                if !isRoot {
                    Button("所有记录") { onOpenRoot() }
                        .buttonStyle(.tapText).font(Typo.caption).foregroundStyle(Ink.secondary)
                    Image(systemName: "chevron.right").font(.system(size: 9)).foregroundStyle(Ink.tertiary)
                }
                Text(selectedFolder?.name ?? title).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
                Spacer(minLength: Space.s2)
            }
            .frame(maxWidth: Space.content, alignment: .leading)
            .padding(.horizontal, Space.s8)
            .frame(maxWidth: .infinity)
            .frame(height: 52)
            .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 0.5) }

            ScrollView {
                VStack(alignment: .leading, spacing: Space.s6) {
                    VStack(alignment: .leading, spacing: Space.s2) {
                        Text(title).font(Typo.display)
                        Text(subtitle).font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                    HStack(spacing: Space.s3) {
                        Text("\(sessions.count) 场会议")
                        if isRoot {
                            Text("\(folders.filter { $0.workspaceID != nil }.count) 个系列")
                        } else {
                            Text("\(sessions.filter { $0.summaryState == .ready }.count) 篇已整理")
                        }
                    }
                    .font(Typo.caption.monospacedDigit()).foregroundStyle(Ink.secondary)
                    .padding(.top, -Space.s3)
                    VStack(alignment: .leading, spacing: Space.s2) {
                        HStack(spacing: Space.s2) {
                            Text(isRoot ? "会议系列" : "系列中的会议").font(Typo.section).foregroundStyle(Ink.ink)
                            Spacer(minLength: 4)
                            if isRoot && !isSelectingSeries {
                                Button("批量删除") {
                                    selectedWorkspaceIDs.removeAll()
                                    isSelectingSeries = true
                                }
                                .buttonStyle(GhostButtonStyle())
                                .disabled(managementDisabled || !folders.contains(where: { canSelectSeries($0) }))
                                .help(managementDisabled ? "会议中不能管理系列" : "选择多个系列后删除")
                            }
                        }
                        if isRoot && isSelectingSeries {
                            HStack(spacing: Space.s2) {
                                Text("已选 \(selectedWorkspaceIDs.count) 个系列")
                                    .font(Typo.caption)
                                    .foregroundStyle(Ink.secondary)
                                Spacer(minLength: 2)
                                Button("全选当前结果") {
                                    selectedWorkspaceIDs.formUnion(folders.compactMap { folder in
                                        canSelectSeries(folder) ? folder.workspaceID : nil
                                    })
                                }
                                .buttonStyle(GhostButtonStyle())
                                .disabled(!folders.contains(where: { canSelectSeries($0) }))
                                Button(role: .destructive, action: onDeleteSelectedSeries) {
                                    Label("删除所选", systemImage: "trash")
                                }
                                .buttonStyle(GhostButtonStyle())
                                .disabled(selectedWorkspaceIDs.isEmpty || managementDisabled)
                                Button("完成") {
                                    selectedWorkspaceIDs.removeAll()
                                    isSelectingSeries = false
                                }
                                .buttonStyle(GhostButtonStyle())
                            }
                        }
                    }
                    if isRoot {
                        if folders.isEmpty {
                            if hasArchiveReadIssues {
                                VStack(spacing: Space.s4) {
 quietNote("无法确认会议系列为空", "会议归档或元数据读取失败。重试后才能确认是否还有会议系列。")
 HStack(spacing: Space.s2) {
                                    Button("重试读取", action: onRetryArchiveRead)
                                        .buttonStyle(SecondaryButtonStyle())
 }
 }
 .frame(maxWidth: .infinity, minHeight: 180)
                            } else if hasUnderlyingRecords {
                                VStack(spacing: Space.s4) {
 quietNote("没有匹配的系列", "当前搜索或筛选条件下没有系列。清除条件可恢复全部会议系列。")
 HStack(spacing: Space.s2) {
                                    Button("清除搜索与筛选", action: onClearFilters)
                                        .buttonStyle(SecondaryButtonStyle())
 }
 }
 .frame(maxWidth: .infinity, minHeight: 180)
                            } else {
                                VStack(spacing: Space.s4) {
 quietNote("还没有会议系列", "开始一场会议后，它会归入对应系列。")
 HStack(spacing: Space.s2) {
                                    Button("返回首页", action: onReturnHome)
                                        .buttonStyle(SecondaryButtonStyle())
 }
 }
 .frame(maxWidth: .infinity, minHeight: 180)
                            }
                        } else {
                            LazyVStack(alignment: .leading, spacing: 0) {
                                ForEach(folders) { folder in folderCard(folder) }
                            }
                        }
                    } else {
                        if hasArchiveReadIssues && selectedFolder?.sessions.isEmpty == true {
                            VStack(spacing: Space.s4) {
 quietNote("无法确认系列中没有会议", "会议归档或元数据读取失败。重试后才能确认此系列中的会议记录。")
 HStack(spacing: Space.s2) {
                                Button("重试读取", action: onRetryArchiveRead)
                                    .buttonStyle(SecondaryButtonStyle())
 }
 }
 .frame(maxWidth: .infinity, minHeight: 180)
                        } else if selectedFolder?.sessions.isEmpty == true {
                            VStack(spacing: Space.s4) {
 quietNote("这个系列还没有会议", "开始一场会议后，它会显示在这个系列里。")
 HStack(spacing: Space.s2) {
                                Button("返回所有记录", action: onOpenRoot)
                                    .buttonStyle(SecondaryButtonStyle())
 }
 }
 .frame(maxWidth: .infinity, minHeight: 180)
                        } else if selectedFolder == nil {
                            VStack(spacing: Space.s4) {
 quietNote("系列信息已不可用", "这个系列可能已被删除。返回所有记录查看仍保留的记录。")
 HStack(spacing: Space.s2) {
                                Button("返回所有记录", action: onOpenRoot)
                                    .buttonStyle(SecondaryButtonStyle())
 }
 }
 .frame(maxWidth: .infinity, minHeight: 180)
                        } else if sessions.isEmpty {
                            VStack(spacing: Space.s4) {
 quietNote(hasActiveFilters ? "当前搜索或筛选下没有会议" : "当前条件下没有会议", "这个系列共有 \(selectedFolder?.sessions.count ?? 0) 场会议。清除条件可恢复结果。")
 HStack(spacing: Space.s2) {
                                Button("清除搜索与筛选", action: onClearFilters)
                                    .buttonStyle(SecondaryButtonStyle())
                                Button("返回所有记录", action: onOpenRoot)
                                    .buttonStyle(GhostButtonStyle())
 }
 }
 .frame(maxWidth: .infinity, minHeight: 180)
                        } else {
                            LazyVStack(alignment: .leading, spacing: 0) {
                                Rectangle().fill(Ink.hairline).frame(height: 0.5)
                                ForEach(sessions) { meta in
                                    Button { onOpen(meta.id) } label: {
                                        SessionRow(meta: meta, selected: false, active: false,
                                                   contentHit: searchHits[meta.id])
                                            .frame(maxWidth: .infinity, alignment: .leading)
                                    }
                                    .buttonStyle(InteractiveRowButtonStyle())
                                    .focused($focusedSessionID, equals: meta.id)
                                    .overlay {
                                        if focusedSessionID == meta.id {
                                            RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous)
                                                .strokeBorder(Ink.focus, lineWidth: 1.5)
                                                .padding(1)
                                        }
                                    }
                                    .accessibilityLabel(historyRowAccessibilityLabel(meta, hit: searchHits[meta.id]))
                                    .accessibilityHint("打开会议纪要和转写")
                                    .accessibilityIdentifier("history.seriesSession.\(meta.id.uuidString)")
                                    Rectangle().fill(Ink.hairline).frame(height: 0.5)
                                }
                            }
                        }
                    }
                }
                .frame(maxWidth: Space.content, alignment: .leading)
                .padding(.horizontal, Space.s8)
                .padding(.top, Space.s8)
                .padding(.bottom, Space.s12)
                // Center the capped content column just like MeetingPage's notes body.
                .frame(maxWidth: .infinity)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    @ViewBuilder
    private func folderCard(_ folder: MeetingSeriesFolder) -> some View {
        let hasWorkspace = canManageFolder(folder)
        let canSelect = canSelectSeries(folder)
        let isSelected = folder.workspaceID.map { selectedWorkspaceIDs.contains($0) } ?? false
        let row = HStack(spacing: Space.s2) {
            Button {
                if isSelectingSeries, hasWorkspace {
                    guard canSelect || isSelected, let id = folder.workspaceID else { return }
                    onToggleSeriesSelection(id)
                } else {
                    onOpenFolder(folder)
                }
            } label: {
                HStack(alignment: .center, spacing: Space.s3) {
                    SceneGlyph(mode: folder.mode, size: 28)
                    VStack(alignment: .leading, spacing: 2) {
                        Text(verbatim: folder.name)
                            .font(Typo.section)
                            .foregroundStyle(Ink.ink)
                            .lineLimit(1).truncationMode(.tail)
                        Text(folder.modeTitle ?? "会议系列")
                            .font(Typo.caption)
                            .foregroundStyle(Ink.secondary)
                    }
                    Spacer(minLength: 0)
                    VStack(alignment: .trailing, spacing: 2) {
                        Text("\(folder.sessions.count) 场会议")
                            .font(Typo.caption.monospacedDigit())
                            .foregroundStyle(Ink.secondary)
                        Text(folder.lastMeetingAt.map { "最近 \($0.formatted(date: .abbreviated, time: .shortened))" } ?? "暂无最近时间")
                            .font(Typo.caption)
                            .foregroundStyle(Ink.secondary)
                    }
                    .lineLimit(1)
                    if isSelectingSeries && hasWorkspace {
                        Image(systemName: isSelected ? "checkmark.circle.fill" : (canSelect ? "circle" : "minus.circle"))
                            .font(.system(size: 17, weight: .medium))
                            .foregroundStyle(isSelected ? Ink.ai : (canSelect ? Ink.tertiary : Ink.caution))
                    } else {
                        Image(systemName: "chevron.right")
                            .font(.system(size: 10, weight: .semibold))
                            .foregroundStyle(Ink.tertiary)
                    }
                }
                .padding(.horizontal, Space.s2)
                .padding(.vertical, Space.s3)
                .frame(maxWidth: .infinity, alignment: .leading)
                .contentShape(Rectangle())
            }
            .buttonStyle(InteractiveRowButtonStyle())
            .disabled(isSelectingSeries && hasWorkspace && !canSelect && !isSelected)
            .focused($focusedFolderID, equals: folder.id)
            .overlay {
                if focusedFolderID == folder.id {
                    RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous)
                        .strokeBorder(Ink.focus, lineWidth: 1.5)
                        .padding(1)
                }
            }
            .accessibilityLabel(isSelectingSeries && hasWorkspace
                                ? "选择系列"
                                : "打开系列：\(folder.name)，\(folder.sessions.count) 场会议")
            .accessibilityHint(isSelectingSeries && hasWorkspace
                               ? (canSelect ? folder.name : (deleteDisabledReason(folder) ?? folder.name))
                               : (folder.lastMeetingAt.map { "最近会议 \($0.formatted(date: .abbreviated, time: .shortened))" } ?? "这个系列还没有会议"))
            .accessibilityAddTraits(isSelectingSeries && isSelected ? .isSelected : [])
            .accessibilityIdentifier("history.series.\(folder.id)")
            .help(isSelectingSeries && hasWorkspace
                  ? (canSelect ? "选择系列：\(folder.name)" : (deleteDisabledReason(folder) ?? "这个系列暂时无法删除"))
                  : "打开系列：\(folder.name)")

            if hasWorkspace && !isSelectingSeries {
                Menu {
                    managementItems(for: folder)
                } label: {
                    HStack(spacing: 5) {
                        Text("管理")
                        Image(systemName: "ellipsis")
                    }
                    .font(Typo.caption.weight(.medium))
                    .foregroundStyle(Ink.secondary)
                    .padding(.horizontal, Space.s2)
                    .frame(minHeight: Hit.minHeight)
                    .contentShape(Rectangle())
                }
                .menuStyle(.button).buttonStyle(GhostButtonStyle(height: Hit.minHeight, horizontalPadding: 0)).menuIndicator(.hidden).fixedSize()
                .accessibilityLabel("管理系列")
                .accessibilityHint(Text(verbatim: folder.name))
                .help("管理系列")
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 0.5) }

        if hasWorkspace && !isSelectingSeries {
            row.contextMenu { managementItems(for: folder) }
        } else {
            row
        }
    }

    private func managementItems(for folder: MeetingSeriesFolder) -> some View {
        SeriesManagementMenuItems(
            deleteDisabledReason: deleteDisabledReason(folder),
            managementDisabled: managementDisabled,
            onEdit: { onManageFolder(folder) },
            onDelete: { onDeleteFolder(folder) })
    }

}

private struct SeriesManagementMenuItems: View {
    let deleteDisabledReason: String?
    let managementDisabled: Bool
    let onEdit: () -> Void
    let onDelete: () -> Void

    var body: some View {
        Button("编辑系列信息", systemImage: "slider.horizontal.3") { onEdit() }
            .disabled(managementDisabled)
            .help(managementDisabled ? "会议中不能管理系列" : "编辑系列信息")
        Divider()
        Button("删除系列…", systemImage: "trash", role: .destructive) { onDelete() }
            .disabled(deleteDisabledReason != nil)
            .help(deleteDisabledReason ?? "删除系列…")
        if let deleteDisabledReason { Text(deleteDisabledReason) }
    }
}

/// Date window a session has to fall into.
private enum SessionDateFilter: String, CaseIterable, Identifiable {
    case all = "全部时间"
    case today = "今天"
    case week = "近 7 天"
    case month = "近 30 天"
    case year = "本年度"

    var id: String { rawValue }

    func matches(_ date: Date, now: Date = Date()) -> Bool {
        let calendar = Calendar.current
        switch self {
        case .all: return true
        case .today: return calendar.isDateInToday(date)
        case .week: return date >= now.addingTimeInterval(-7 * 24 * 3600)
        case .month: return date >= now.addingTimeInterval(-30 * 24 * 3600)
        case .year: return calendar.component(.year, from: date) == calendar.component(.year, from: now)
        }
    }
}

/// Session length window, in minutes.
private enum SessionLengthFilter: String, CaseIterable, Identifiable {
    case all = "全部时长"
    case underMinute = "1 分钟以内"
    case oneToTen = "1–10 分钟"
    case overTen = "10 分钟以上"

    var id: String { rawValue }

    func matches(_ duration: TimeInterval) -> Bool {
        switch self {
        case .all: return true
        case .underMinute: return duration < 60
        case .oneToTen: return duration >= 60 && duration < 600
        case .overTen: return duration >= 600
        }
    }
}

/// Order of the session list.
private enum SessionSort: String, CaseIterable, Identifiable {
    case newest = "最新优先"
    case oldest = "最早优先"
    case longest = "时长最长"
    case shortest = "时长最短"
    case mostQuestions = "问题最多"

    var id: String { rawValue }

    /// Ascending sort key; the list is sorted by it from small to large.
    func ascendingKey(_ meta: SessionMeta) -> Double {
        switch self {
        case .newest: return -meta.startedAt.timeIntervalSince1970
        case .oldest: return meta.startedAt.timeIntervalSince1970
        case .longest: return -meta.duration
        case .shortest: return meta.duration
        case .mostQuestions: return -Double(meta.questionCount)
        }
    }
}

struct SessionRow: View {
    let meta: SessionMeta
    var selected: Bool
    var active: Bool
    var showsWorkspace = true
    /// Set when the search found this meeting through a recorded misheard spelling.
    var misheardForm: String? = nil
    var contentHit: SessionSearchHit? = nil

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s1) {
            HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                if active {
                    Circle().fill(Ink.seal).frame(width: 7, height: 7)
                } else {
                    Circle().fill(meta.mode.theme.tint).frame(width: 7, height: 7)
                }
                Text(verbatim: meta.displayTitle)
                    .font(Typo.label.weight(selected ? .semibold : .medium))
                    .foregroundStyle(Ink.ink)
                    .lineLimit(1).truncationMode(.tail)
                Spacer(minLength: Space.s2)
                summaryIcon
                Text(verbatim: MeetingOutcome.when(meta.startedAt))
                    .font(Typo.caption).foregroundStyle(Ink.secondary).monospacedDigit().lineLimit(1)
                    .fixedSize()
            }
            Text(verbatim: details).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
                .padding(.leading, 7 + Space.s2)
            if let misheardForm { MisheardMark(form: misheardForm).padding(.leading, 7 + Space.s2) }
            if let contentHit {
                VStack(alignment: .leading, spacing: 2) {
                    let kind = contentHit.userContentTarget == nil ? searchHitKindLabel(contentHit.kind) : String(localized: String.LocalizationValue(contentHit.label))
                    let location = contentHit.location.map { " · \($0)" } ?? ""
                    let freshness = searchHitFreshnessLabel(contentHit).map { " · \($0)" } ?? ""
                    Text("\(kind)\(location)\(freshness)")
                        .font(Typo.overline).foregroundStyle(Ink.secondary)
                    Text(verbatim: contentHit.excerpt)
                        .font(Typo.caption)
                        .foregroundStyle(Ink.secondary)
                        .lineLimit(2)
                        .textSelection(.enabled)
                }
                .padding(.leading, 7 + Space.s2)
            }
        }
        .padding(.horizontal, Space.s3).padding(.vertical, Space.s2)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(selected ? Ink.sunken : Color.clear, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
        .animation(Motion.snappy, value: selected)
    }

    private var details: String {
        var parts = [MeetingOutcome.line(for: meta)]
        if showsWorkspace, let workspace = meta.workspaceName { parts.insert(workspace, at: 0) }
        return parts.joined(separator: " · ")
    }

    @ViewBuilder private var summaryIcon: some View {
        switch meta.summaryState {
        case .ready: Image(systemName: "sparkle").foregroundStyle(Ink.ai).help("纪要已整理好")
        case .pending where SessionArchive.shared.summarizing.contains(meta.id):
            ProgressView().controlSize(.mini).help("正在整理纪要")
        case .pending:
            Image(systemName: "exclamationmark.circle").foregroundStyle(Ink.caution).help("纪要整理已中断 · 可以重试")
        case .failed: Image(systemName: "exclamationmark.circle").foregroundStyle(Ink.caution).help(meta.summaryError ?? "")
        case .none: EmptyView()
        }
    }
}

/// The answers given during a meeting (the 提词 tab of the meeting page).
struct SessionAnswersList: View {
    let detail: SessionDetail
    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            LazyVStack(alignment: .leading, spacing: 10) {
                if detail.answers.isEmpty {
                    Text("这场会议没有生成回答。").font(.callout).foregroundStyle(.secondary)
                }
                ForEach(detail.answers) { answer in
                    VStack(alignment: .leading, spacing: 6) {
                        if !answer.isComplete {
                            Label("未完成的回答片段", systemImage: "exclamationmark.circle")
                                .font(Typo.caption).foregroundStyle(Ink.caution)
                        }
                        HStack {
                            Text(answer.question).font(.system(size: 13, weight: .semibold)).lineLimit(3).textSelection(.enabled)
                            Spacer()
                            Text(answer.automatic ? "自动" : "手动").font(.system(size: 10)).foregroundStyle(.secondary)
                        }
                        if let path = answer.screenshotPath {
                            ArchivedScreenshot(sessionID: detail.meta.id, relativePath: path)
                        }
                        MarkdownText(text: answer.answer, size: 13).equatable()
                        Text(meta(answer)).font(.system(size: 10)).foregroundStyle(.tertiary)
                    }
                    .aiBlock(padding: 12)
                    .id("answer-\(answer.id)")
                }
            }
        }
    }

    private func meta(_ answer: SessionAnswerEvent) -> String {
        var parts = [answer.date.formatted(date: .omitted, time: .standard), answer.model]
        if let first = answer.firstTokenSeconds { parts.append(String(format: "首字 %.1fs", first)) }
        return parts.joined(separator: " · ")
    }
}

/// A screenshot that was archived together with the session. The file is read
/// lazily from disk; when it is gone the view degrades to a muted note instead
/// of showing a broken image.
private struct ArchivedScreenshot: View {
    let sessionID: UUID
    let relativePath: String
    @State private var image: NSImage?
    @State private var missing = false

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            if let image {
                Image(nsImage: image)
                    .resizable()
                    .scaledToFit()
                    .frame(maxWidth: 320, maxHeight: 140)
                    .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .strokeBorder(Color.primary.opacity(0.12), lineWidth: 0.8))
                    .contentShape(Rectangle())
                    .onTapGesture { NSWorkspace.shared.open(fileURL) }
                    .help("点击查看原图")
            } else if missing {
                Text("截图文件已不存在").font(.system(size: 11)).foregroundStyle(.tertiary)
            } else {
                ProgressView().controlSize(.mini)
            }
            HStack(spacing: 4) {
                Image(systemName: "photo").font(.system(size: 9))
                Text(missing ? "截图已归档（文件缺失）" : "截图已归档").font(.system(size: 10))
            }
            .foregroundStyle(.tertiary)
        }
        .task(id: relativePath) { await load() }
    }

    /// The path is relative to the session folder.
    private var fileURL: URL {
        SessionArchive.shared.folder(sessionID).appendingPathComponent(relativePath)
    }

    /// Reading the file happens off the main thread; only the finished image is
    /// handed back.
    @MainActor
    private func load() async {
        let url = fileURL
        let data = await Task.detached(priority: .utility) { () -> Data? in
            try? Data(contentsOf: url)
        }.value
        guard !Task.isCancelled else { return }
        guard let data, let image = NSImage(data: data) else {
            missing = true
            return
        }
        self.image = image
    }
}
