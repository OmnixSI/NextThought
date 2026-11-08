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
    @State private var deletingWorkspace: Workspace?
    @State private var isSelectingSeries = false
    @State private var selectedWorkspaceIDs = Set<UUID>()
    @State private var showDeleteSelectedSeries = false
    @AppStorage(AppNavigationBar.meetingsSidebarKey, store: UserDefaults.standard) private var isMeetingListHidden = false
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
    /// Sessions found only through a recorded misheard spelling (术语和人名), with that spelling.
    @State private var misheardMatch: [UUID: String] = [:]
    @State private var searchCache = GlossarySearchCache()
    @State private var isContentSearching = false
    @State private var searchProgress = 0
    @State private var searchTotal = 0

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
                return matchesMetadata(meta, terms) || contentHits[meta.id] != nil
            }
        }
        return ordered(items)
    }

    var body: some View {
        GeometryReader { proxy in
            let wide = proxy.size.width >= 760
            HStack(alignment: .top, spacing: 0) {
                if wide {
                    if !isMeetingListHidden {
                        sessionList
                            .frame(width: 270)
                            .padding(.leading, 16).padding(.top, 4).padding(.bottom, 14)
                        Rectangle().fill(Ink.hairline).frame(width: 1).padding(.vertical, 14)
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
                            onNavigateToSeries: showSeries)
                            .id(selection)
                    } else {
                        archiveOverview(compact: isMeetingListHidden)
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
                        onNavigateToSeries: showSeries)
                        .id(selection)
                } else if isCollectionOverview || isMeetingListHidden {
                    archiveOverview(compact: true)
                } else {
                    sessionList
                        .padding(.horizontal, 16).padding(.top, 4).padding(.bottom, 14)
                }
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
            Button("删除全部会议记录", role: .destructive) { archive.deleteAll(); state.historySelection = nil }
            Button("取消", role: .cancel) { }
        } message: {
            Text("将删除全部 \(archive.sessions.count) 场会议的转写、问题、回答和纪要，不限当前系列，且无法恢复。")
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

    private func archiveOverview(compact: Bool = false) -> some View {
        let isRoot = overviewFolderKey == nil && state.historyWorkspaceFilter == nil
        let folder = selectedOverviewFolder
        let folderSessions = folder.map { folder in
            ordered(visibleSessions.filter { folder.contains($0, workspaces: state.workspaces.workspaces) })
        } ?? visibleSessions
        let title = isRoot ? "所有会议" : (folder?.name ?? String(localized: "未分类会议"))
        let subtitle: String
        if let folder, !isRoot {
            subtitle = folder.workspaceID == nil
                ? String(localized: "未归入系列的会议")
                : "\(folder.modeTitle ?? "会议系列") · 系列共 \(folder.sessions.count) 场会议 · 当前条件匹配 \(folderSessions.count) 场"
        } else if !isRoot {
            subtitle = "系列信息已不可用 · 当前筛选下共 \(folderSessions.count) 场会议"
        } else {
            subtitle = "本机记录 · 当前匹配 \(visibleSessions.count) 场会议、\(visibleWorkspaceSeriesCount) 个系列 · 系列按最近匹配会议排序"
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
            managementDisabled: state.isRunning,
            onOpen: openFromOverview,
            onClearFilters: clearFilters,
            onReturnHome: { state.section = .home },
            searchHits: contentHits)
        return Group {
            if compact {
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
            let modeTitle = workspace?.mode.title ?? sessions.first?.mode.title
            let lastSessionAt = sessions.map(\.startedAt).max()
            let updatedAt = lastSessionAt ?? workspace?.updatedAt ?? .distantPast
            return MeetingSeriesFolder(id: key, workspaceID: id, name: name, modeTitle: modeTitle,
                                       sessions: sessions, lastMeetingAt: lastSessionAt, updatedAt: updatedAt)
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
        state.historySelection = id
    }

    private func beginRenaming(_ id: UUID) {
        renameTitle = archive.sessions.first(where: { $0.id == id })?.displayTitle ?? ""
        renamingSessionID = id
    }

    private func deleteSession(_ id: UUID) {
        archive.delete(id)
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
                HStack(spacing: 6) {
                    Image(systemName: "folder")
                        .font(.system(size: 10, weight: .medium))
                    Text(verbatim: folder.name)
                        .font(.system(size: 11, weight: .medium))
                        .lineLimit(1)
                        .truncationMode(.middle)
                    Spacer(minLength: 4)
                    Text(meetingCount.formatted())
                        .font(.system(size: 10, weight: .medium).monospacedDigit())
                    Image(systemName: "chevron.right")
                        .font(.system(size: 8, weight: .semibold))
                }
                .foregroundStyle(Ink.secondary)
                .padding(.leading, 9)
                .padding(.top, 8)
                .padding(.bottom, 2)
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
                        managementDisabled: state.isRunning,
                        onEdit: { manageFolder(folder) },
                        onDelete: { confirmDeleteFolder(folder) })
                } label: {
                    Image(systemName: "ellipsis")
                        .frame(width: 25, height: 25)
                        .contentShape(Rectangle())
                }
                .menuStyle(.button)
                .buttonStyle(GlassIconButtonStyle(size: 25))
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
                    managementDisabled: state.isRunning,
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
        guard let workspaceID = state.historyWorkspaceFilter else { return "所有会议" }
        return state.workspaces.workspace(workspaceID)?.name ?? String(localized: "未分类会议")
    }

    private var sessionList: some View {
        VStack(alignment: .leading, spacing: 9) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text(sessionListTitle).font(Typo.section).lineLimit(2)
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
                .buttonStyle(GlassIconButtonStyle(size: 28))
                .menuIndicator(.hidden)
                .accessibilityLabel("会议记录操作")
                .disabled(archive.sessions.isEmpty)
                .help("会议记录操作；删除会影响所有系列")
            }
            filterBar
            if isScopedToSeries && currentScopeSessionCount == 0 {
                VStack(spacing: 7) {
                    Image(systemName: "folder")
                        .font(.system(size: 24, weight: .regular))
                        .foregroundStyle(Ink.tertiary)
                    Text("这个系列还没有会议")
                        .font(.system(size: 14, weight: .semibold))
                    Text("开始一场会议后，它会出现在这个系列里。")
                        .font(.system(size: 11)).foregroundStyle(Ink.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                    Button("返回所有会议") { showAllMeetings() }
                        .buttonStyle(ChipButtonStyle(prominent: true))
                        .controlSize(.small)
                }
                .multilineTextAlignment(.center)
                .padding(.horizontal, 12)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if archive.sessions.isEmpty {
                VStack(spacing: 7) {
                    Image(systemName: "clock")
                        .font(.system(size: 24, weight: .regular))
                        .foregroundStyle(Ink.tertiary)
                    Text("还没有会议")
                        .font(.system(size: 14, weight: .semibold))
                    Text("开过的会议会保存在这里；结束时自动整理纪要。")
                        .font(.system(size: 11))
                        .foregroundStyle(Ink.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
                .multilineTextAlignment(.center)
                .padding(.horizontal, 12)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if visibleSessions.isEmpty && visibleSessionGroups.isEmpty {
                if isContentSearching {
                    VStack(spacing: 8) {
                        ProgressView(value: Double(searchProgress), total: Double(max(searchTotal, 1)))
                            .frame(maxWidth: 220)
                        Text(searchIsScopedToSeries ? "正在搜索当前系列内容" : "正在搜索全部会议内容")
                            .font(.system(size: 14, weight: .semibold))
                        Text("已检查 \(searchProgress) / \(searchTotal) 场；完成后才会确定没有匹配项。")
                            .font(.system(size: 11))
                            .foregroundStyle(Ink.secondary)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 12)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else {
                    VStack(spacing: 7) {
                        Image(systemName: "line.3.horizontal.decrease.circle")
                            .font(.system(size: 24, weight: .regular))
                            .foregroundStyle(Ink.tertiary)
                        Text("没有符合条件的记录")
                            .font(.system(size: 14, weight: .semibold))
                        Text(isScopedToSeries
                             ? "当前系列有 \(currentScopeSessionCount) 场会议；当前搜索或筛选没有匹配项。"
                             : "当前搜索或筛选条件下没有记录，可以清除条件后查看全部。")
                            .font(.system(size: 11))
                            .foregroundStyle(Ink.secondary)
                            .fixedSize(horizontal: false, vertical: true)
                        Button("清除搜索与筛选") { clearFilters() }
                            .buttonStyle(ChipButtonStyle(prominent: true))
                            .controlSize(.small)
                            .help("清除搜索、筛选和排序，保留当前系列范围")
                    }
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 12)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            } else {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 10) {
                        ForEach(visibleSessionGroups) { group in
                            VStack(alignment: .leading, spacing: 2) {
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
                                }
                            }
                        }
                    }
                }
            }
        }
        .card()
        .frame(maxHeight: .infinity, alignment: .top)
    }

    // MARK: Search, filter and sort

    /// Search stays visible above the three core controls.
    private var filterBar: some View {
        VStack(alignment: .leading, spacing: 7) {
            searchField
            Text("系列按最近匹配的会议排序；以下排序只作用于组内会议。")
                .font(.system(size: 10))
                .foregroundStyle(Ink.tertiary)
                .fixedSize(horizontal: false, vertical: true)
            HStack(spacing: 6) {
                HistoryFilterPicker(selection: $dateFilter, options: SessionDateFilter.allCases.map { ($0, $0.rawValue) })
                HistoryFilterPicker(selection: $lengthFilter, options: SessionLengthFilter.allCases.map { ($0, $0.rawValue) })
                HistoryFilterPicker(selection: $sortOrder, options: SessionSort.allCases.map { ($0, $0.rawValue) })
            }
            .frame(maxWidth: .infinity)
            HStack {
                Text(searchScopeLabel).font(.system(size: 10)).foregroundStyle(Ink.secondary)
                Text("·").font(.system(size: 10)).foregroundStyle(Ink.tertiary)
                Text(resultCount).font(.system(size: 11)).foregroundStyle(.tertiary)
                Spacer(minLength: 0)
            }
        }
    }

    /// A capsule that mirrors the `MenuPicker` look. The glass is drawn as a
    /// background layer so the text field keeps normal text colors.
    private var searchField: some View {
        HStack(spacing: 6) {
            Image(systemName: "magnifyingglass")
                .font(.system(size: 11, weight: .medium))
                .foregroundStyle(Color.secondary)
            TextField(searchPlaceholder, text: $query)
                .textFieldStyle(.plain)
                .font(.system(size: 12.5))
                .accessibilityLabel(searchScopeLabel)
            if !query.isEmpty {
                Button { query = "" } label: { Image(systemName: "xmark.circle.fill") }
                    .buttonStyle(TapButtonStyle(minWidth: 30, minHeight: 28))
                    .font(.system(size: 11))
                    .foregroundStyle(Color.secondary)
                    .help("清空搜索")
            }
        }
        .padding(.horizontal, 12).padding(.vertical, 6)
        .glassBackground(cornerRadius: 14)
    }

    private var resultCount: String {
        let base = searchIsScopedToSeries
            ? "\(visibleSessions.count) 场会议 · 系列共 \(currentScopeSessionCount) 场"
            : "\(visibleSessions.count) 场会议 · \(visibleWorkspaceSeriesCount) 个系列"
        return isContentSearching ? base + " · 扫描 \(searchProgress)/\(searchTotal) 场" : base
    }

    private var searchIsScopedToSeries: Bool {
        overviewFolderKey != nil || state.historyWorkspaceFilter != nil
    }

    private var searchScopeLabel: String {
        searchIsScopedToSeries ? "搜索范围：当前系列" : "搜索范围：全部会议"
    }

    private var searchPlaceholder: String {
        searchIsScopedToSeries ? "搜索此系列的标题、原话、译文、纪要、问题或回答" : "搜索会议、系列、原话、译文、纪要、问题或回答"
    }

    /// Clear search and filters while keeping the current series scope.
    private func clearFilters() {
        query = ""
        dateFilter = .all
        lengthFilter = .all
        sortOrder = .newest
        contentHits = [:]
        misheardMatch = [:]
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
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            contentHits = [:]
            misheardMatch = [:]
            isContentSearching = false
            searchProgress = 0
            searchTotal = 0
            return
        }
        contentHits = [:]
        misheardMatch = [:]
        isContentSearching = true
        // Search finalized records only. The active session joins the archive automatically when
        // it ends, at which point the task identity changes and this scan runs again.
        let sessionsToSearch = scoped.filter {
            $0.id != archive.activeID && !archive.pendingTranslationSearchSessionIDs.contains($0.id)
        }
        searchProgress = 0
        searchTotal = sessionsToSearch.count
        // A cancelled old run must not clear progress owned by the newer query.
        defer { if !Task.isCancelled { isContentSearching = false } }
        do {
            try await Task.sleep(nanoseconds: 350_000_000)
        } catch {
            // Cancelled: a newer query took over and owns the state now.
            return
        }
        guard !Task.isCancelled else { return }
        var hits: [UUID: SessionSearchHit] = [:]
        let publishEvery = 16
        for (index, meta) in sessionsToSearch.enumerated() {
            if Task.isCancelled { return }
            if let detail = await archive.detail(meta.id) {
                // The typed text plus spellings from this meeting's global and series glossaries.
                let terms = searchCache.terms(for: trimmed, state: state,
                                              workspaceID: glossaryWorkspaceID(for: detail.meta), includeAllWorkspaces: false)
                let fields = SessionContentSearch.fields(meta: detail.meta, lines: detail.lines, questions: detail.questions,
                                                         answers: detail.answers, summary: detail.summary, notes: detail.notes)
                if let hit = SessionContentSearch.firstHit(in: fields, terms: terms) {
                    hits[meta.id] = hit
                }
            }
            let completed = index + 1
            if completed.isMultiple(of: publishEvery) || completed == sessionsToSearch.count {
                guard !Task.isCancelled else { return }
                contentHits = hits
                misheardMatch = Dictionary(uniqueKeysWithValues: hits.compactMap { id, hit in
                    hit.isMisheard ? (id, hit.matchedForm) : nil
                })
                searchProgress = completed
            }
        }
        guard !Task.isCancelled else { return }
        contentHits = hits
        misheardMatch = Dictionary(uniqueKeysWithValues: hits.compactMap { id, hit in
            hit.isMisheard ? (id, hit.matchedForm) : nil
        })
        searchProgress = sessionsToSearch.count
        isContentSearching = false
    }
}

@MainActor
private func historyRowAccessibilityLabel(_ meta: SessionMeta, hit: SessionSearchHit?) -> String {
    let meeting = "打开会议：\(meta.displayTitle)，\(MeetingOutcome.when(meta.startedAt))，\(MeetingOutcome.line(for: meta))"
    guard let hit else { return meeting }
    let location = hit.location.map { " · \($0)" } ?? ""
    return "\(meeting)，命中\(searchHitKindLabel(hit.kind))\(location)：\(hit.excerpt)"
}

private func searchHitKindLabel(_ kind: SessionSearchFieldKind) -> String {
    switch kind {
    case .transcript: "原话"
    case .correctedTranscript: "校正原话"
    case .translation: "译文"
    case .question: "问题"
    case .answer: "回答"
    case .notes: "纪要"
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
                    if option.0 == selection { Label(option.1, systemImage: "checkmark") }
                    else { Text(option.1) }
                }
            }
        } label: {
            HStack(spacing: 3) {
                Text(options.first(where: { $0.0 == selection })?.1 ?? "筛选")
                    .lineLimit(1).minimumScaleFactor(0.72)
                    .frame(maxWidth: .infinity)
                Image(systemName: "chevron.up.chevron.down")
                    .font(.system(size: 8, weight: .semibold)).foregroundStyle(Ink.secondary)
            }
            .font(.system(size: 11, weight: .medium))
            .padding(.horizontal, 7).padding(.vertical, 6)
            .frame(minHeight: Hit.minHeight)
            .glassCapsule()
            .contentShape(Rectangle())
        }
        .menuStyle(.button).buttonStyle(.plain).menuIndicator(.hidden)
        .frame(maxWidth: .infinity)
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

    func withSessions(_ sessions: [SessionMeta]) -> MeetingSeriesFolder {
        MeetingSeriesFolder(id: id, workspaceID: workspaceID, name: name, modeTitle: modeTitle,
                            sessions: sessions, lastMeetingAt: sessions.map(\.startedAt).max(), updatedAt: updatedAt)
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
    let searchHits: [UUID: SessionSearchHit]
    @FocusState private var focusedSessionID: UUID?
    @FocusState private var focusedFolderID: String?

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: Space.s2) {
                if !isRoot {
                    Button("所有会议") { onOpenRoot() }
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
            .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 1) }

            ScrollView {
                VStack(alignment: .leading, spacing: Space.s6) {
                    VStack(alignment: .leading, spacing: Space.s2) {
                        Text(title).font(Typo.display)
                        Text(subtitle).font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                    HStack(spacing: Space.s2) {
                        MetricPill(symbol: "clock", text: "\(sessions.count) 场会议")
                        if isRoot {
                            MetricPill(symbol: "folder", text: "\(folders.filter { $0.workspaceID != nil }.count) 个系列")
                        } else {
                            MetricPill(symbol: "sparkles", text: "\(sessions.filter { $0.summaryState == .ready }.count) 篇已整理")
                        }
                    }
                    VStack(alignment: .leading, spacing: Space.s2) {
                        HStack(spacing: Space.s2) {
                            Text(isRoot ? "会议系列" : "系列中的会议").font(Typo.section)
                            Spacer(minLength: 4)
                            if isRoot && !isSelectingSeries {
                                Button("批量删除") {
                                    selectedWorkspaceIDs.removeAll()
                                    isSelectingSeries = true
                                }
                                .buttonStyle(ChipButtonStyle())
                                .controlSize(.small)
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
                                .buttonStyle(ChipButtonStyle())
                                .controlSize(.small)
                                .disabled(!folders.contains(where: { canSelectSeries($0) }))
                                Button(role: .destructive, action: onDeleteSelectedSeries) {
                                    Label("删除所选", systemImage: "trash")
                                }
                                .buttonStyle(ChipButtonStyle())
                                .controlSize(.small)
                                .disabled(selectedWorkspaceIDs.isEmpty || managementDisabled)
                                Button("完成") {
                                    selectedWorkspaceIDs.removeAll()
                                    isSelectingSeries = false
                                }
                                .buttonStyle(ChipButtonStyle())
                                .controlSize(.small)
                            }
                        }
                    }
                    if isRoot {
                        if folders.isEmpty {
                            if hasUnderlyingRecords {
                                ContentUnavailableView {
                                    Label("没有匹配的系列", systemImage: "magnifyingglass")
                                } description: {
                                    Text("当前搜索或筛选条件下没有系列。清除条件可恢复全部会议系列。")
                                } actions: {
                                    Button("清除搜索与筛选", action: onClearFilters)
                                        .buttonStyle(ChipButtonStyle(prominent: true))
                                }
                                .frame(maxWidth: .infinity, minHeight: 180)
                            } else {
                                ContentUnavailableView {
                                    Label("还没有会议系列", systemImage: "folder")
                                } description: {
                                    Text("开始一场会议后，它会归入对应系列。")
                                } actions: {
                                    Button("返回首页", action: onReturnHome)
                                        .buttonStyle(ChipButtonStyle(prominent: true))
                                }
                                .frame(maxWidth: .infinity, minHeight: 180)
                            }
                        } else {
                            LazyVStack(alignment: .leading, spacing: 0) {
                                ForEach(folders) { folder in folderCard(folder) }
                            }
                        }
                    } else {
                        if selectedFolder?.sessions.isEmpty == true {
                            ContentUnavailableView {
                                Label("这个系列还没有会议", systemImage: "folder")
                            } description: {
                                Text("开始一场会议后，它会显示在这个系列里。")
                            } actions: {
                                Button("返回所有会议", action: onOpenRoot)
                                    .buttonStyle(ChipButtonStyle(prominent: true))
                            }
                            .frame(maxWidth: .infinity, minHeight: 180)
                        } else if selectedFolder == nil {
                            ContentUnavailableView {
                                Label("系列信息已不可用", systemImage: "folder.badge.questionmark")
                            } description: {
                                Text("这个系列可能已被删除。返回所有会议查看仍保留的记录。")
                            } actions: {
                                Button("返回所有会议", action: onOpenRoot)
                                    .buttonStyle(ChipButtonStyle(prominent: true))
                            }
                            .frame(maxWidth: .infinity, minHeight: 180)
                        } else if sessions.isEmpty {
                            ContentUnavailableView {
                                Label(hasActiveFilters ? "当前搜索或筛选下没有会议" : "当前条件下没有会议",
                                      systemImage: "line.3.horizontal.decrease.circle")
                            } description: {
                                Text("这个系列共有 \(selectedFolder?.sessions.count ?? 0) 场会议。清除条件可恢复结果。")
                            } actions: {
                                Button("清除搜索与筛选", action: onClearFilters)
                                    .buttonStyle(ChipButtonStyle(prominent: true))
                                Button("返回所有会议", action: onOpenRoot)
                                    .buttonStyle(ChipButtonStyle())
                            }
                            .frame(maxWidth: .infinity, minHeight: 180)
                        } else {
                            LazyVStack(spacing: 6) {
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
                    Image(systemName: "folder")
                        .font(.system(size: 17, weight: .regular))
                        .foregroundStyle(Ink.ai)
                        .frame(width: 24)
                    VStack(alignment: .leading, spacing: 4) {
                        Text(verbatim: folder.name)
                            .font(Typo.body)
                            .foregroundStyle(Ink.ink)
                            .fixedSize(horizontal: false, vertical: true)
                        Text(folder.modeTitle ?? "会议系列")
                            .font(Typo.caption)
                            .foregroundStyle(Ink.secondary)
                    }
                    Spacer(minLength: 0)
                    VStack(alignment: .trailing, spacing: 3) {
                        Text("\(folder.sessions.count) 场会议")
                            .font(.system(size: 11, weight: .medium).monospacedDigit())
                            .foregroundStyle(Ink.secondary)
                        Text(folder.lastMeetingAt.map { "最近 \($0.formatted(date: .abbreviated, time: .shortened))" } ?? "暂无最近时间")
                            .font(.system(size: 10))
                            .foregroundStyle(Ink.tertiary)
                    }
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
                .padding(.vertical, Space.s4)
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
                    .font(.system(size: 12, weight: .medium))
                    .padding(.horizontal, 10).padding(.vertical, 6)
                    .frame(minHeight: Hit.minHeight)
                    .glassCapsule()
                    .contentShape(Rectangle())
                }
                .menuStyle(.button).buttonStyle(.plain).menuIndicator(.hidden).fixedSize()
                .accessibilityLabel("管理系列")
                .accessibilityHint(Text(verbatim: folder.name))
                .help("管理系列")
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 1) }

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

private struct SessionRow: View {
    let meta: SessionMeta
    var selected: Bool
    var active: Bool
    var showsWorkspace = true
    /// Set when the search found this meeting through a recorded misheard spelling.
    var misheardForm: String? = nil
    var contentHit: SessionSearchHit? = nil

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack(spacing: 6) {
                if active { PulseDot(active: true) }
                Text(verbatim: meta.displayTitle).font(.system(size: 13, weight: .semibold)).lineLimit(1)
                Spacer(minLength: 0)
                summaryIcon
            }
            Text(verbatim: details).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
            if let misheardForm { MisheardMark(form: misheardForm) }
            if let contentHit {
                VStack(alignment: .leading, spacing: 2) {
                    Text(contentHit.location.map { "\(searchHitKindLabel(contentHit.kind)) · \($0)" } ?? searchHitKindLabel(contentHit.kind))
                        .font(.system(size: 9, weight: .medium))
                        .foregroundStyle(Ink.secondary)
                    Text(verbatim: contentHit.excerpt)
                        .font(.system(size: 10))
                        .foregroundStyle(Ink.secondary)
                        .lineLimit(2)
                        .textSelection(.enabled)
                }
            }
        }
        .padding(10)
        .background(selected ? Ink.hover : Color.clear, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
    }

    private var details: String {
        var parts = [MeetingOutcome.when(meta.startedAt), MeetingOutcome.line(for: meta)]
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
