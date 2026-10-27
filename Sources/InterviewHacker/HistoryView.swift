import AppKit
import SwiftUI
import InterviewHackerCore

/// 所有会议: the list on the left, the selected meeting's notes on the right (stacked
/// navigation on narrow windows).
struct HistoryView: View {
    @Environment(AppState.self) private var state
    @State private var showDeleteAll = false
    @AppStorage(AppNavigationBar.meetingsSidebarKey) private var isMeetingListHidden = false
    @State private var isCollectionOverview = false
    @State private var overviewFolderKey: String?
    @State private var informationTarget: SessionInfoTarget?
    @State private var renamingSessionID: UUID?
    @State private var renameTitle = ""
    @State private var query = ""
    @State private var dateFilter: SessionDateFilter = .all
    @State private var lengthFilter: SessionLengthFilter = .all
    @State private var sortOrder: SessionSort = .newest
    /// Sessions whose question/answer text contains the query. Filled in by the
    /// debounced content search, because that needs a disk read per session.
    @State private var contentMatch: Set<UUID> = []
    @State private var isContentSearching = false

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
            items = items.filter { matchesMetadata($0, trimmed) || contentMatch.contains($0.id) }
        }
        return ordered(items)
    }

    var body: some View {
        GeometryReader { proxy in
            let wide = proxy.size.width >= 760
            HStack(alignment: .top, spacing: 0) {
                if !isMeetingListHidden && (wide || selection == nil) {
                    sessionList
                        .frame(width: wide ? 270 : nil)
                        .padding(.leading, 16).padding(.trailing, wide ? 0 : 16).padding(.top, 4).padding(.bottom, 14)
                    Rectangle().fill(Ink.hairline).frame(width: 1).padding(.vertical, 14)
                }
                if let selection, archive.sessions.contains(where: { $0.id == selection }) {
                    MeetingPage(
                        id: selection,
                        showsBack: !wide,
                        onBack: returnToMeetingList,
                        onShowInfo: showInformation,
                        onRename: beginRenaming,
                        onDelete: deleteSession,
                        onNavigateToAllMeetings: showAllMeetings,
                        onNavigateToSeries: showSeries)
                        .id(selection)
                } else if wide || isMeetingListHidden {
                    archiveOverview
                }
            }
        }
        .onAppear { selectFirstIfNeeded() }
        .onChange(of: state.section) { _, section in
            if section == .meetings { selectFirstIfNeeded() }
        }
        .onChange(of: archive.sessions.count) { _, _ in selectFirstIfNeeded() }
        .confirmationDialog("删除全部会议记录？", isPresented: $showDeleteAll, titleVisibility: .visible) {
            Button("删除全部记录", role: .destructive) { archive.deleteAll(); state.historySelection = nil }
            Button("取消", role: .cancel) { }
        } message: {
            Text("会删除所有会议的转写、问题、提词和纪要，无法恢复。")
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

    private var archiveOverview: some View {
        let isRoot = overviewFolderKey == nil && state.historyWorkspaceFilter == nil
        let folder = selectedOverviewFolder
        let folderSessions = folder.map { folder in
            ordered(visibleSessions.filter { folder.contains($0, workspaces: state.workspaces.workspaces) })
        } ?? visibleSessions
        let title = isRoot ? "所有会议" : (folder?.name ?? "已删除系列")
        let subtitle: String
        if let folder, !isRoot {
            subtitle = "\(folder.modeTitle ?? "会议系列") · 当前筛选下共 \(folderSessions.count) 场会议"
        } else if !isRoot {
            subtitle = "系列信息已不可用 · 当前筛选下共 \(folderSessions.count) 场会议"
        } else {
            subtitle = "会议记录保存在这台 Mac 上 · 当前筛选下共 \(visibleSessions.count) 场会议"
        }
        return MeetingArchiveOverview(
            title: title,
            subtitle: subtitle,
            sessions: isRoot ? visibleSessions : folderSessions,
            folders: isRoot ? visibleSeriesFolders : [],
            isRoot: isRoot,
            selectedFolder: isRoot ? nil : folder,
            onOpenRoot: showAllMeetings,
            onOpenFolder: showFolder,
            onOpen: openFromOverview)
            .padding(.leading, 16).padding(.trailing, 16).padding(.bottom, 14)
    }

    /// A folder for each saved workspace/series, plus a fallback for legacy sessions
    /// that predate workspace grouping.
    private var seriesFolders: [MeetingSeriesFolder] {
        let workspaces = state.workspaces.workspaces
        func workspaceID(for session: SessionMeta) -> UUID? {
            session.workspaceID ?? workspaces.first(where: { $0.name == session.workspaceName })?.id
        }
        func key(for session: SessionMeta) -> String {
            if let id = workspaceID(for: session) { return "workspace:\(id.uuidString)" }
            let name = session.workspaceName?.trimmingCharacters(in: .whitespacesAndNewlines)
            let bucket = name?.isEmpty == false ? name ?? "未分类" : "未分类"
            return "legacy:\(bucket)"
        }

        var grouped = Dictionary(grouping: archive.sessions, by: key)
        for workspace in workspaces where !workspace.archived {
            let workspaceKey = "workspace:\(workspace.id.uuidString)"
            if grouped[workspaceKey] == nil { grouped[workspaceKey] = [] }
        }

        return grouped.map { key, sessions in
            let id = key.hasPrefix("workspace:") ? UUID(uuidString: String(key.dropFirst("workspace:".count))) : nil
            let workspace = id.flatMap { state.workspaces.workspace($0) }
            let fallbackName = sessions.first?.workspaceName
            let name = workspace?.name ?? fallbackName ?? (id == nil ? String(localized: "未分类") : String(localized: "已删除系列"))
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

    private var selectedOverviewFolder: MeetingSeriesFolder? {
        if let overviewFolderKey, let folder = seriesFolders.first(where: { $0.id == overviewFolderKey }) { return folder }
        if let workspaceID = state.historyWorkspaceFilter {
            return seriesFolders.first(where: { $0.workspaceID == workspaceID })
        }
        return nil
    }

    private var visibleSeriesFolders: [MeetingSeriesFolder] {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        return seriesFolders.compactMap { folder in
            let matching = ordered(visibleSessions.filter { folder.contains($0, workspaces: state.workspaces.workspaces) })
            let nameMatches = !trimmed.isEmpty && folder.name.localizedCaseInsensitiveContains(trimmed)
            let hasNarrowingFilter = !trimmed.isEmpty || dateFilter != .all || lengthFilter != .all
            guard !matching.isEmpty || nameMatches || !hasNarrowingFilter else { return nil }
            return folder.withSessions(matching)
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
        isCollectionOverview = true
        overviewFolderKey = nil
        state.historyWorkspaceFilter = nil
        state.historySelection = nil
    }

    private func showSeries(_ id: UUID) {
        isCollectionOverview = true
        overviewFolderKey = "workspace:\(id.uuidString)"
        state.historyWorkspaceFilter = id
        state.historySelection = nil
    }

    private func showFolder(_ folder: MeetingSeriesFolder) {
        isCollectionOverview = true
        overviewFolderKey = folder.id
        state.historyWorkspaceFilter = folder.workspaceID
        state.historySelection = nil
    }

    private func returnToMeetingList() {
        isMeetingListHidden = false
        isCollectionOverview = true
        state.historySelection = nil
    }

    private var sessionListTitle: String {
        if let overviewFolder = selectedOverviewFolder { return overviewFolder.name }
        guard let workspaceID = state.historyWorkspaceFilter else { return "所有会议" }
        return state.workspaces.workspace(workspaceID)?.name ?? "已删除系列"
    }

    private var sessionList: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text(sessionListTitle).font(Typo.section).lineLimit(1)
                    Text(state.historyWorkspaceFilter == nil
                         ? (overviewFolderKey != nil ? "这个系列中的会议记录" : (state.recordSessions ? "自动保存 · 不保存录音" : "记录已关闭（设置 → 数据）"))
                         : "这个系列中的会议记录")
                        .font(Typo.caption)
                        .foregroundStyle(state.historyWorkspaceFilter == nil && !state.recordSessions ? Ink.caution : Ink.secondary)
                }
                Spacer()
                Button { showDeleteAll = true } label: { Image(systemName: "trash") }
                    .buttonStyle(GlassIconButtonStyle(size: 28))
                    .disabled(archive.sessions.isEmpty).help("删除全部会议记录")
            }
            MenuPicker(selection: Binding(get: { state.historyWorkspaceFilter }, set: { id in
                if let id { showSeries(id) } else { showAllMeetings() }
            }),
                       options: [(UUID?.none, String(localized: "全部系列"))] + state.workspaces.workspaces.map { (Optional($0.id), $0.name) })
            filterBar
            if archive.sessions.isEmpty {
                ContentUnavailableView("还没有会议", systemImage: "clock",
                                       description: Text("开过的会议会保存在这里；结束时自动整理纪要。"))
                    .frame(maxHeight: .infinity)
            } else if visibleSessions.isEmpty {
                // Records exist, but the current search/filters exclude all of them.
                ContentUnavailableView {
                    Label("没有符合条件的记录", systemImage: "line.3.horizontal.decrease.circle")
                } description: {
                    Text("当前的搜索或筛选条件下没有记录，可以清除筛选后查看全部。")
                } actions: {
                    Button("清除筛选") { clearFilters() }.buttonStyle(ChipButtonStyle(prominent: true))
                        .help("清除搜索、筛选、排序和场景")
                }
                .frame(maxHeight: .infinity)
            } else {
                ScrollView {
                    LazyVStack(spacing: 6) {
                        ForEach(visibleSessions) { meta in
                            SessionRow(meta: meta, selected: meta.id == selection, active: meta.id == archive.activeID)
                                .contentShape(Rectangle())
                                .onTapGesture { openFromOverview(meta.id) }
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
        .card()
        .frame(maxHeight: .infinity, alignment: .top)
    }

    // MARK: Search, filter and sort

    /// Compact bar above the list: search, then time, length and sort in one row.
    private var filterBar: some View {
        VStack(alignment: .leading, spacing: 6) {
            searchField
            HStack(spacing: 6) {
                HistoryFilterPicker(selection: $dateFilter, options: SessionDateFilter.allCases.map { ($0, $0.rawValue) })
                HistoryFilterPicker(selection: $lengthFilter, options: SessionLengthFilter.allCases.map { ($0, $0.rawValue) })
                HistoryFilterPicker(selection: $sortOrder, options: SessionSort.allCases.map { ($0, $0.rawValue) })
            }
            .frame(maxWidth: .infinity)
            HStack(spacing: 6) {
                Text(resultCount).font(.system(size: 11)).foregroundStyle(.tertiary)
                Spacer(minLength: 0)
                if hasActiveFilter {
                    Button("清除筛选") { clearFilters() }.buttonStyle(ChipButtonStyle())
                        .help("清除搜索、筛选、排序和场景")
                }
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
            TextField("搜索标题、系列或问题", text: $query)
                .textFieldStyle(.plain)
                .font(.system(size: 12.5))
            if !query.isEmpty {
                Button { query = "" } label: { Image(systemName: "xmark.circle.fill") }
                    .buttonStyle(.plain)
                    .font(.system(size: 11))
                    .foregroundStyle(Color.secondary)
                    .help("清空搜索")
            }
        }
        .padding(.horizontal, 12).padding(.vertical, 6)
        .glassBackground(cornerRadius: 14)
    }

    private var resultCount: String {
        let base = "共 \(visibleSessions.count) 条记录"
        return isContentSearching ? base + " · 搜索中…" : base
    }

    private var hasActiveFilter: Bool {
        dateFilter != .all || lengthFilter != .all || sortOrder != .newest
            || !query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    /// Back to "show everything": search box, both filters, the order and the
    /// workspace pick.
    private func clearFilters() {
        query = ""
        dateFilter = .all
        lengthFilter = .all
        sortOrder = .newest
        contentMatch = []
        overviewFolderKey = nil
        state.historyWorkspaceFilter = nil
    }

    /// Metadata-only match: always available, no disk access.
    private func matchesMetadata(_ meta: SessionMeta, _ query: String) -> Bool {
        let fields = [meta.displayTitle, meta.title ?? "", meta.workspaceName ?? "",
                      meta.mode.title, meta.audioSource, meta.recognizer]
        return fields.contains { $0.localizedCaseInsensitiveContains(query) }
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
    private var contentSearchKey: String {
        "\(query)|\(dateFilter.rawValue)|\(lengthFilter.rawValue)|\(state.historyWorkspaceFilter?.uuidString ?? "-")|\(overviewFolderKey ?? "-")"
    }

    /// Content search: the question and answer text lives in the session files,
    /// so it costs a disk read per session. Debounced, limited to the first 200
    /// sessions and cancelled as soon as the query changes again.
    @MainActor
    private func runContentSearch() async {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            contentMatch = []
            isContentSearching = false
            return
        }
        contentMatch = []
        isContentSearching = true
        // A cancelled run must not leave the "搜索中…" label behind.
        defer { isContentSearching = false }
        do {
            try await Task.sleep(nanoseconds: 350_000_000)
        } catch {
            // Cancelled: a newer query took over and owns the state now.
            return
        }
        guard !Task.isCancelled else { return }
        var matches: Set<UUID> = []
        for meta in scoped.prefix(200) {
            if Task.isCancelled { return }
            guard let detail = await archive.detail(meta.id) else { continue }
            let hit = detail.questions.contains { $0.text.localizedCaseInsensitiveContains(trimmed) }
                || detail.answers.contains {
                    $0.question.localizedCaseInsensitiveContains(trimmed)
                        || $0.answer.localizedCaseInsensitiveContains(trimmed)
                }
            if hit { matches.insert(meta.id) }
        }
        guard !Task.isCancelled else { return }
        contentMatch = matches
        isContentSearching = false
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
        let resolvedID = session.workspaceID ?? workspaces.first(where: { $0.name == session.workspaceName })?.id
        if let workspaceID { return resolvedID == workspaceID }
        guard resolvedID == nil else { return false }
        let sessionName = session.workspaceName?.trimmingCharacters(in: .whitespacesAndNewlines)
        return (sessionName?.isEmpty == false ? sessionName ?? "未分类" : "未分类") == name
    }
}

private struct MeetingArchiveOverview: View {
    let title: String
    let subtitle: String
    let sessions: [SessionMeta]
    let folders: [MeetingSeriesFolder]
    let isRoot: Bool
    let selectedFolder: MeetingSeriesFolder?
    let onOpenRoot: () -> Void
    let onOpenFolder: (MeetingSeriesFolder) -> Void
    let onOpen: (UUID) -> Void

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: Space.s2) {
                if !isRoot {
                    Button("所有会议") { onOpenRoot() }
                        .buttonStyle(.plain).font(Typo.caption).foregroundStyle(Ink.secondary)
                    Image(systemName: "chevron.right").font(.system(size: 9)).foregroundStyle(Ink.tertiary)
                }
                Text(selectedFolder?.name ?? title).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
                Spacer(minLength: Space.s2)
            }
            .padding(.horizontal, Space.s6)
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
                            MetricPill(symbol: "folder", text: "\(folders.count) 个系列")
                        } else {
                            MetricPill(symbol: "sparkles", text: "\(sessions.filter { $0.summaryState == .ready }.count) 篇已整理")
                        }
                    }
                    Text(isRoot ? "会议系列" : "系列中的会议").font(Typo.section)
                    if isRoot {
                        if folders.isEmpty {
                            ContentUnavailableView("还没有会议系列", systemImage: "folder",
                                                   description: Text("开始一场会议后，它会归入对应系列。"))
                                .frame(maxWidth: .infinity, minHeight: 180)
                        } else {
                            LazyVGrid(columns: [GridItem(.adaptive(minimum: 220), spacing: Space.s3)],
                                      alignment: .leading, spacing: Space.s3) {
                                ForEach(folders) { folder in folderCard(folder) }
                            }
                        }
                    } else {
                        if sessions.isEmpty {
                            ContentUnavailableView("这个系列里还没有会议", systemImage: "folder",
                                                   description: Text("会议记录会显示在这个系列里。"))
                                .frame(maxWidth: .infinity, minHeight: 180)
                        } else {
                            LazyVStack(spacing: 6) {
                                ForEach(sessions) { meta in
                                    Button { onOpen(meta.id) } label: {
                                        SessionRow(meta: meta, selected: false, active: false)
                                            .frame(maxWidth: .infinity, alignment: .leading)
                                    }
                                    .buttonStyle(.plain)
                                }
                            }
                        }
                    }
                }
                .frame(maxWidth: Space.content, alignment: .leading)
                .padding(.horizontal, Space.s8)
                .padding(.top, Space.s8)
                .padding(.bottom, Space.s12)
                .frame(maxWidth: .infinity, alignment: .topLeading)
            }
        }
        .card()
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
    }

    private func folderCard(_ folder: MeetingSeriesFolder) -> some View {
        Button { onOpenFolder(folder) } label: {
            HStack(alignment: .top, spacing: Space.s3) {
                Image(systemName: "folder.fill")
                    .font(.system(size: 22, weight: .regular))
                    .foregroundStyle(Ink.ai)
                    .frame(width: 28, height: 30)
                VStack(alignment: .leading, spacing: 5) {
                    Text(verbatim: folder.name)
                        .font(Typo.section)
                        .foregroundStyle(Ink.ink)
                        .lineLimit(1)
                    Text(folderSummary(folder))
                        .font(Typo.caption)
                        .foregroundStyle(Ink.secondary)
                        .lineLimit(2)
                }
                Spacer(minLength: 0)
                Image(systemName: "chevron.right")
                    .font(.system(size: 10, weight: .semibold))
                    .foregroundStyle(Ink.tertiary)
                    .padding(.top, 4)
            }
            .padding(Space.s4)
            .frame(maxWidth: .infinity, minHeight: 78, alignment: .leading)
            .glassBackground(cornerRadius: Space.radiusMedium)
            .contentShape(RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
        }
        .buttonStyle(.plain)
        .help("打开系列：\(folder.name)")
    }

    private func folderSummary(_ folder: MeetingSeriesFolder) -> String {
        let count = "\(folder.sessions.count) 场会议"
        guard let latest = folder.lastMeetingAt else { return count + " · 暂无会议" }
        return count + " · 最近 \(latest.formatted(date: .abbreviated, time: .shortened))"
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

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack(spacing: 6) {
                if active { PulseDot(active: true) }
                Text(verbatim: meta.displayTitle).font(.system(size: 13, weight: .semibold)).lineLimit(1)
                Spacer(minLength: 0)
                summaryIcon
            }
            Text(verbatim: details).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
        }
        .padding(10)
        .background(selected ? Ink.hover : Color.clear, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
    }

    private var details: String {
        var parts = [MeetingOutcome.when(meta.startedAt), MeetingOutcome.line(for: meta)]
        if let workspace = meta.workspaceName { parts.insert(workspace, at: 0) }
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
                    Text("这场会议没有生成提词。").font(.callout).foregroundStyle(.secondary)
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
