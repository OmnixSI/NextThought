import AppKit
import SwiftUI
import InterviewHackerCore

/// Session archive: list on the left, the selected session on the right (stacked
/// navigation on narrow windows).
struct HistoryView: View {
    @Environment(AppState.self) private var state
    @State private var showDeleteAll = false
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
        if let filter = state.historyWorkspaceFilter {
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
            let wide = proxy.size.width >= 820
            HStack(alignment: .top, spacing: 12) {
                if wide || selection == nil {
                    sessionList.frame(width: wide ? 300 : nil)
                }
                if let selection, archive.sessions.contains(where: { $0.id == selection }) {
                    SessionDetailView(id: selection, showsBack: !wide) { state.historySelection = nil }
                        .id(selection)
                } else if wide {
                    ContentUnavailableView("选择一次会话", systemImage: "rectangle.stack",
                                           description: Text("每次会话的完整转录、问题、提词和 AI 摘要都保存在本机。"))
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                        .card()
                }
            }
            .padding(.horizontal, 16).padding(.bottom, 14).padding(.top, 4)
        }
        .confirmationDialog("删除全部本地记录？", isPresented: $showDeleteAll, titleVisibility: .visible) {
            Button("删除全部记录", role: .destructive) { archive.deleteAll(); state.historySelection = nil }
            Button("取消", role: .cancel) { }
        } message: {
            Text("会删除所有会话的转录、问题、回答和摘要，无法恢复。")
        }
        .onAppear { selectFirstIfNeeded() }
        // The page stays alive between visits; pick up sessions recorded meanwhile.
        .onChange(of: state.section) { _, section in if section == .meetings { selectFirstIfNeeded() } }
        // Re-runs the content search whenever the query or the filter scope changes;
        // `.task(id:)` cancels the previous run automatically.
        .task(id: contentSearchKey) { await runContentSearch() }
    }

    private func selectFirstIfNeeded() {
        if selection == nil || !visibleSessions.contains(where: { $0.id == selection }) {
            state.historySelection = visibleSessions.first?.id
        }
    }

    private var sessionList: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("所有会议").font(Typo.section)
                    Text(state.recordSessions ? "自动保存 · 不含原始音频" : "记录已关闭（设置 → 记录）")
                        .font(.system(size: 11)).foregroundStyle(state.recordSessions ? Color.secondary : Color.orange)
                }
                Spacer()
                Button { showDeleteAll = true } label: { Image(systemName: "trash") }
                    .buttonStyle(GlassIconButtonStyle(size: 28)).foregroundStyle(.red)
                    .disabled(archive.sessions.isEmpty).help("删除全部记录")
            }
            MenuPicker(selection: Binding(get: { state.historyWorkspaceFilter }, set: { state.historyWorkspaceFilter = $0 }),
                       options: [(UUID?.none, String(localized: "全部系列"))] + state.workspaces.workspaces.map { (Optional($0.id), $0.name) })
            filterBar
            if archive.sessions.isEmpty {
                ContentUnavailableView("还没有记录", systemImage: "clock",
                                       description: Text("开始一次辅助后，转录、问题与回答会自动保存在这里；结束时自动生成摘要。"))
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
                                .onTapGesture { state.historySelection = meta.id }
                                .contextMenu {
                                    Button("以此为上一场，准备下一场") { state.prepareNextMeeting(after: meta.id) }
                                        .disabled(meta.workspaceID == nil || state.isRunning)
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

    /// Compact bar above the list: a search field, two filters and the order.
    /// The three menus are split over two rows so nothing is clipped in the
    /// 300 pt column.
    private var filterBar: some View {
        VStack(alignment: .leading, spacing: 6) {
            searchField
            HStack(spacing: 6) {
                MenuPicker(selection: $dateFilter, options: SessionDateFilter.allCases.map { ($0, $0.rawValue) })
                    .fixedSize(horizontal: true, vertical: false)
                MenuPicker(selection: $lengthFilter, options: SessionLengthFilter.allCases.map { ($0, $0.rawValue) })
                    .fixedSize(horizontal: true, vertical: false)
            }
            HStack(spacing: 6) {
                MenuPicker(selection: $sortOrder, options: SessionSort.allCases.map { ($0, $0.rawValue) })
                    .fixedSize(horizontal: true, vertical: false)
                Spacer(minLength: 0)
                if hasActiveFilter {
                    Button("清除筛选") { clearFilters() }.buttonStyle(ChipButtonStyle())
                        .help("清除搜索、筛选、排序和场景")
                }
            }
            Text(resultCount).font(.system(size: 11)).foregroundStyle(.tertiary)
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
        "\(query)|\(dateFilter.rawValue)|\(lengthFilter.rawValue)|\(state.historyWorkspaceFilter?.uuidString ?? "-")"
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
                Text(meta.displayTitle).font(.system(size: 13, weight: .semibold)).lineLimit(1)
                Spacer(minLength: 0)
                summaryIcon
            }
            Text(details).font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(1)
        }
        .padding(10)
        .background(selected ? Brand.accent.opacity(0.14) : Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
    }

    private var details: String {
        var parts = [meta.startedAt.formatted(date: .abbreviated, time: .shortened)]
        if let workspace = meta.workspaceName { parts.insert(workspace, at: 0) }
        if meta.duration >= 60 { parts.append("\(Int(meta.duration / 60)) 分钟") }
        parts.append("\(meta.questionCount) 个问题")
        parts.append("\(meta.answerCount) 条提词")
        return parts.joined(separator: " · ")
    }

    @ViewBuilder private var summaryIcon: some View {
        switch meta.summaryState {
        case .ready: Image(systemName: "sparkles").foregroundStyle(Brand.accent).help("已生成摘要")
        case .pending: ProgressView().controlSize(.mini).help("正在生成摘要")
        case .failed: Image(systemName: "exclamationmark.circle").foregroundStyle(.orange).help(meta.summaryError ?? "摘要生成失败")
        case .none: EmptyView()
        }
    }
}

private enum DetailTab: String, CaseIterable, Identifiable {
    case summary = "摘要", questions = "问题", transcript = "转录", answers = "提词"
    var id: String { rawValue }
}

private struct SessionDetailView: View {
    @Environment(AppState.self) private var state
    let id: UUID
    var showsBack: Bool
    var onBack: () -> Void
    @State private var detail: SessionDetail?
    @State private var tab: DetailTab = .summary
    @State private var renaming = false
    @State private var newTitle = ""

    private var archive: SessionArchive { SessionArchive.shared }
    private var meta: SessionMeta? { archive.sessions.first { $0.id == id } }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            header
            PillTabs(items: DetailTab.allCases.map { (value: $0, title: tabTitle($0)) }, selection: $tab)
            Group {
                if let detail {
                    switch tab {
                    case .summary: SummaryTab(detail: detail)
                    case .questions: QuestionsTab(detail: detail)
                    case .transcript: TranscriptTab(detail: detail)
                    case .answers: AnswersTab(detail: detail)
                    }
                } else {
                    ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
            .frame(maxHeight: .infinity, alignment: .top)
        }
        .card()
        .frame(maxHeight: .infinity, alignment: .top)
        .task(id: reloadKey) { await archive.flush(); detail = await archive.detail(id) }
        .alert("重命名会话", isPresented: $renaming) {
            TextField("标题", text: $newTitle)
            Button("保存") { archive.rename(id, to: newTitle) }
            Button("取消", role: .cancel) { }
        }
    }

    /// Reload when the summary finishes or the live session adds lines.
    private var reloadKey: String {
        guard let meta else { return id.uuidString }
        return "\(id)-\(meta.summaryState.rawValue)-\(meta.transcriptCount / 10)-\(meta.answerCount)-\(meta.title ?? "")"
    }

    private func tabTitle(_ tab: DetailTab) -> String {
        guard let detail else { return tab.rawValue }
        switch tab {
        case .summary: return tab.rawValue
        case .questions: return "问题 \(detail.summary?.questions.count ?? detail.questions.count)"
        case .transcript: return "转录 \(detail.lines.count)"
        case .answers: return "提词 \(detail.answers.count)"
        }
    }

    private var header: some View {
        HStack(alignment: .top, spacing: 8) {
            if showsBack {
                Button { onBack() } label: { Image(systemName: "chevron.left") }.buttonStyle(GlassIconButtonStyle(size: 28))
            }
            VStack(alignment: .leading, spacing: 3) {
                Text(meta?.displayTitle ?? "").font(.system(size: 16, weight: .semibold)).lineLimit(2).textSelection(.enabled)
                if let meta {
                    Text(subtitle(meta)).font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(2)
                }
            }
            Spacer()
            Button { newTitle = meta?.displayTitle ?? ""; renaming = true } label: { Image(systemName: "pencil") }
                .buttonStyle(GlassIconButtonStyle(size: 28)).help("重命名")
            Button { if let detail { state.copySessionMarkdown(detail) } } label: { Image(systemName: "doc.on.doc") }
                .buttonStyle(GlassIconButtonStyle(size: 28)).help("复制为 Markdown").disabled(detail == nil)
            Button { state.exportSession(id) } label: { Image(systemName: "square.and.arrow.up") }
                .buttonStyle(GlassIconButtonStyle(size: 28)).help("导出 Markdown")
            Button { archive.revealInFinder(id) } label: { Image(systemName: "folder") }
                .buttonStyle(GlassIconButtonStyle(size: 28)).help("在 Finder 中显示原始文件")
            Button { archive.delete(id) } label: { Image(systemName: "trash") }
                .buttonStyle(GlassIconButtonStyle(size: 28)).foregroundStyle(.red)
                .disabled(id == archive.activeID).help("删除此记录")
        }
    }

    private func subtitle(_ meta: SessionMeta) -> String {
        var parts = [meta.workspaceName.map { "\(meta.mode.title) · \($0)" } ?? meta.mode.title, meta.startedAt.formatted(date: .complete, time: .shortened)]
        if meta.duration >= 60 { parts.append("\(Int(meta.duration / 60)) 分钟") }
        if !meta.audioSource.isEmpty { parts.append(meta.audioSource) }
        if !meta.recognizer.isEmpty { parts.append(meta.recognizer) }
        return parts.joined(separator: " · ")
    }
}

private struct SummaryTab: View {
    @Environment(AppState.self) private var state
    let detail: SessionDetail
    private var archive: SessionArchive { SessionArchive.shared }

    var body: some View {
        let meta = archive.sessions.first { $0.id == detail.meta.id } ?? detail.meta
        let busy = archive.summarizing.contains(meta.id) || meta.summaryState == .pending
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                if busy {
                    ProgressView().controlSize(.small)
                    Text("正在用 \(state.currentAnswerModel) 整理摘要和问题清单…").font(.caption).foregroundStyle(.secondary)
                } else if meta.summaryState == .failed {
                    Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.orange)
                    Text(meta.summaryError ?? "摘要生成失败").font(.caption).foregroundStyle(.secondary).lineLimit(2)
                } else if let summary = detail.summary {
                    Text("由 \(summary.model) 于 \(summary.generatedAt.formatted(date: .omitted, time: .shortened)) 生成")
                        .font(.caption).foregroundStyle(.tertiary)
                    if summary.appliedToWorkspace == true {
                        Label("已加入场景经验库", systemImage: "checkmark.seal").font(.caption).foregroundStyle(.green)
                    } else if meta.workspaceID != nil, !(summary.lessons ?? []).isEmpty || !(summary.bank ?? []).isEmpty {
                        Button("加入场景经验库") { state.applyReview(summary, sessionID: meta.id, workspaceID: meta.workspaceID) }
                            .buttonStyle(ChipButtonStyle())
                    }
                }
                Spacer()
                Button { state.generateSummary(for: meta.id) } label: {
                    Label(detail.summary == nil ? "生成摘要" : "重新生成", systemImage: "sparkles")
                }
                .buttonStyle(ChipButtonStyle(prominent: detail.summary == nil))
                .disabled(busy || meta.id == archive.activeID || detail.lines.isEmpty)
            }
            ScrollView {
                if let summary = detail.summary {
                    MarkdownText(text: Self.body(of: summary.markdown), size: 14, lineSpacing: 4)
                        .frame(maxWidth: .infinity, alignment: .leading)
                } else if !busy {
                    VStack(alignment: .leading, spacing: 6) {
                        Text(meta.id == archive.activeID ? "会话结束后自动生成摘要。" : "还没有摘要。")
                        Text("摘要包括：会话概要、对方提出的全部问题及我的回答要点、关键信息、待办与复盘建议。用当前的回答引擎生成。")
                            .font(.caption).foregroundStyle(.secondary)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
        }
    }

    /// Drops the “# 标题” block, which is already the page title.
    static func body(of markdown: String) -> String {
        guard let range = markdown.range(of: "## ") else { return markdown }
        return String(markdown[range.lowerBound...])
    }
}

private struct QuestionsTab: View {
    @Environment(AppState.self) private var state
    let detail: SessionDetail

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 8) {
                if let summary = detail.summary, !summary.questions.isEmpty {
                    sectionTitle("AI 整理的问题清单（含未被自动识别的）")
                    ForEach(Array(summary.questions.enumerated()), id: \.element.id) { index, question in
                        VStack(alignment: .leading, spacing: 4) {
                            Text("\(index + 1). \(question.text)").font(.system(size: 13, weight: .medium)).textSelection(.enabled)
                            if let gist = question.answerGist {
                                Text("我的回答要点：\(gist)").font(.system(size: 12)).foregroundStyle(.secondary).textSelection(.enabled)
                            }
                        }
                        .padding(10).frame(maxWidth: .infinity, alignment: .leading)
                        .background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                    }
                }
                if !detail.questions.isEmpty {
                    sectionTitle("会中实时检测到的问题")
                    ForEach(detail.questions) { question in
                        HStack(alignment: .firstTextBaseline, spacing: 8) {
                            Text(offset(question.date)).font(.system(size: 10).monospacedDigit()).foregroundStyle(.tertiary)
                            VStack(alignment: .leading, spacing: 2) {
                                if let leadIn = question.leadIn { Text(leadIn).font(.system(size: 11)).foregroundStyle(.tertiary) }
                                Text(question.text).font(.system(size: 13)).textSelection(.enabled)
                            }
                            Spacer()
                            Text(disposition(question.disposition)).font(.system(size: 10, weight: .medium)).foregroundStyle(.secondary)
                                .padding(.horizontal, 6).padding(.vertical, 2).background(Color.primary.opacity(0.06), in: Capsule())
                        }
                    }
                }
                if detail.questions.isEmpty && (detail.summary?.questions.isEmpty ?? true) {
                    Text("这次会话没有检测到问题。生成摘要后，AI 会从完整转录里再整理一遍。")
                        .font(.callout).foregroundStyle(.secondary)
                }
            }
        }
    }

    private func sectionTitle(_ text: String) -> some View {
        Text(text).font(.system(size: 12, weight: .semibold)).foregroundStyle(.secondary).padding(.top, 4)
    }

    private func offset(_ date: Date) -> String {
        let seconds = max(0, Int(date.timeIntervalSince(detail.meta.startedAt)))
        return String(format: "%02d:%02d", seconds / 60, seconds % 60)
    }

    private func disposition(_ value: String) -> String {
        switch value {
        case "auto": "自动回答"
        case "manual": "手动回答"
        case "deferred": "等我说完"
        case "uncertain": "可能的问题"
        default: "已提示"
        }
    }
}

private struct TranscriptTab: View {
    let detail: SessionDetail
    @State private var filter = ""

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            TextField("搜索转录…", text: $filter).textFieldStyle(.roundedBorder)
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 6) {
                    ForEach(lines) { line in
                        HStack(alignment: .firstTextBaseline, spacing: 8) {
                            Text(offset(line.date)).font(.system(size: 10).monospacedDigit()).foregroundStyle(.tertiary).frame(width: 44, alignment: .trailing)
                            Text(line.speaker.label)
                                .font(.system(size: 10, weight: .semibold))
                                .foregroundStyle(line.speaker == .me ? Brand.accent : Color.secondary)
                                .padding(.horizontal, 6).padding(.vertical, 1)
                                .background((line.speaker == .me ? Brand.accent : Color.secondary).opacity(0.14), in: Capsule())
                            Text(line.text).font(.system(size: 13)).textSelection(.enabled)
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }
                    }
                }
            }
        }
    }

    private var lines: [SessionTranscriptLine] {
        let query = filter.trimmingCharacters(in: .whitespaces)
        guard !query.isEmpty else { return detail.lines }
        return detail.lines.filter { $0.text.localizedCaseInsensitiveContains(query) }
    }

    private func offset(_ date: Date) -> String {
        let seconds = max(0, Int(date.timeIntervalSince(detail.meta.startedAt)))
        return String(format: "%02d:%02d", seconds / 60, seconds % 60)
    }
}

private struct AnswersTab: View {
    let detail: SessionDetail
    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 10) {
                if detail.answers.isEmpty {
                    Text("这次会话没有生成提词。").font(.callout).foregroundStyle(.secondary)
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
                    .padding(10)
                    .background(Brand.accent.opacity(0.06), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
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
