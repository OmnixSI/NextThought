import AppKit
import SwiftUI
import UniformTypeIdentifiers
import NextThoughtCore

/// 准备页: one page, top to bottom: what kind of meeting, what to bring from last
/// time, new materials, and a check before starting (02-scenarios-and-flows.md).
struct PrepareView: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(spacing: 0) {
            PrepareHeader()
            ScrollView {
                VStack(alignment: .leading, spacing: Space.s10) {
                    TitleBlock()
                    PrepareStep(number: 1, title: "这是什么会", note: typeNote) { TypePicker() }
                    if state.draftContinuesSeries {
                        PrepareStep(number: 2, title: "接着上一场") { SeriesCarryCard() }
                    }
                    PrepareStep(number: state.draftContinuesSeries ? 3 : 2, title: "这次新加的资料", note: "可跳过", trailing: { MaterialsActions() }) {
                        MaterialsBlock()
                    }
                    PrepareStep(number: state.draftContinuesSeries ? 4 : 3, title: "开始前检查") { CheckCard() }
                    HStack {
                        Spacer()
                        StartButton()
                    }
                }
                .frame(maxWidth: Space.content, alignment: .leading)
                .padding(.horizontal, Space.s8)
                .padding(.top, Space.s10)
                .padding(.bottom, Space.s12)
                .frame(maxWidth: .infinity)
            }
        }
    }

    private var typeNote: LocalizedStringKey? {
        guard let draft = state.meetingDraft, !draft.typeChosen, draft.mode != .general else { return nil }
        return "根据标题选好了，可以改"
    }
}

// MARK: - Layout pieces

private struct PrepareHeader: View {
    @Environment(AppState.self) private var state

    var body: some View {
        HStack(spacing: Space.s2) {
            Button("首页") { state.cancelDraft() }
                .buttonStyle(.tapText).font(Typo.caption).foregroundStyle(Ink.secondary)
            Image(systemName: "chevron.right").font(.system(size: 9, weight: .medium)).foregroundStyle(Ink.tertiary)
            Text("准备").font(.system(size: 12, weight: .medium)).foregroundStyle(Ink.ink)
            Spacer()
        }
        .padding(.horizontal, Space.s8)
        .frame(height: 52)
        .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 1) }
    }
}

/// A numbered step: the number in the margin, a title row, then the content.
private struct PrepareStep<Trailing: View, Content: View>: View {
    let number: Int
    let title: LocalizedStringKey
    let note: LocalizedStringKey?
    let trailing: () -> Trailing
    let content: () -> Content

    init(number: Int, title: LocalizedStringKey, note: LocalizedStringKey? = nil,
         @ViewBuilder trailing: @escaping () -> Trailing,
         @ViewBuilder content: @escaping () -> Content) {
        self.number = number; self.title = title; self.note = note
        self.trailing = trailing; self.content = content
    }

    var body: some View {
        HStack(alignment: .top, spacing: 0) {
            Text(verbatim: "\(number)")
                .font(.system(size: 15, design: .serif))
                .foregroundStyle(Ink.tertiary)
                .frame(width: 32, alignment: .leading)
            VStack(alignment: .leading, spacing: Space.s3) {
                HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                    Text(title).font(.system(size: 14, weight: .semibold))
                    if let note { Text(note).font(Typo.caption).foregroundStyle(Ink.secondary) }
                    Spacer(minLength: Space.s2)
                    trailing()
                }
                content()
            }
        }
    }
}

private struct TitleBlock: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            HStack(spacing: Space.s1) {
                Text("会议名称").overline()
                Text("可选").font(Typo.caption).foregroundStyle(Ink.secondary)
            }
            TextField("这场会议叫什么", text: Binding(get: { state.meetingDraft?.title ?? "" },
                                                set: { state.updateDraftTitle($0) }), axis: .vertical)
                .textFieldStyle(.plain)
                .font(Typo.display)
                .lineLimit(1...3)
                .accessibilityLabel("会议名称，可选")
                .accessibilityHint("用于查找会议，可以会后修改")
            Text("用于查找会议；可以现在填写，也可以会后再改。")
                .font(Typo.caption).foregroundStyle(Ink.secondary)
            HStack(spacing: Space.s1) {
                Image(systemName: state.meetingVenue == .online ? "desktopcomputer" : "person.3")
                Text(state.meetingVenue == .online ? "线上会议" : "线下会议")
            }
            .font(Typo.caption)
            .foregroundStyle(Ink.secondary)
        }
    }
}

extension PrepareStep where Trailing == EmptyView {
    init(number: Int, title: LocalizedStringKey, note: LocalizedStringKey? = nil,
         @ViewBuilder content: @escaping () -> Content) {
        self.init(number: number, title: title, note: note, trailing: { EmptyView() }, content: content)
    }
}

// MARK: - 1 · Type

private struct TypePicker: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let draft = state.meetingDraft ?? MeetingDraft()
        VStack(alignment: .leading, spacing: Space.s3) {
            FlowLayout(spacing: Space.s2, lineSpacing: Space.s2) {
                ForEach(SessionMode.builtIn) { mode in
                    Button { state.setDraftType(mode) } label: { Text(verbatim: mode.displayName) }
                        .buttonStyle(TagButtonStyle(selected: draft.mode == mode))
                        .help(mode.defaultsSummary)
                }
                Button { state.setDraftCustomType() } label: {
                    HStack(spacing: 4) {
                        Image(systemName: "plus").font(.system(size: 10, weight: .medium))
                        Text("自定义…")
                    }
                }
                .buttonStyle(TagButtonStyle(selected: draft.mode == .custom))
                .help(SessionMode.custom.defaultsSummary)
            }
            if draft.mode == .custom {
                HStack(spacing: Space.s3) {
                    TextField("类型名称，例如：导师一对一", text: Binding(get: { state.meetingDraft?.customTypeName ?? "" },
                                                                 set: { state.setDraftCustomType(name: $0) }))
                        .textFieldStyle(.roundedBorder)
                        .frame(maxWidth: 260)
                    Picker("基于", selection: Binding(get: { state.meetingDraft?.customBaseMode ?? .general },
                                                    set: { state.setDraftCustomType(base: $0) })) {
                        ForEach(SessionMode.builtIn) { mode in Text(verbatim: mode.displayName).tag(mode) }
                    }
                    .fixedSize()
                }
            }
            Text(verbatim: summary(draft))
                .font(Typo.caption).foregroundStyle(Ink.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    private func summary(_ draft: MeetingDraft) -> String {
        let effective = draft.mode == .custom ? draft.customBaseMode : draft.mode
        let name = draft.mode == .custom && !draft.customTypeName.isEmpty ? draft.customTypeName : draft.mode.displayName
        var line = String(localized: "\(name)：\(effective.defaultsSummary)")
        let hint = effective.preparationHint
        if !hint.isEmpty { line += String(localized: " · 建议放：\(hint)") }
        return line
    }
}

/// A type tag: selected tags use the ink fill, the others a hairline outline.
struct TagButtonStyle: ButtonStyle {
    var selected: Bool
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(Typo.label)
            .lineLimit(1)
            .foregroundStyle(selected ? Ink.onInk : Ink.ink)
            .padding(.horizontal, 12)
            .frame(minWidth: Hit.minHeight)
            .frame(height: Hit.minHeight)
            .background(selected ? Ink.ink : (configuration.isPressed ? Ink.hover : Ink.surface),
                        in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            .overlay {
                if !selected {
                    RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 1)
                }
            }
            .contentShape(RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            .animation(Motion.hover, value: selected)
    }
}

// MARK: - 2 · Continue from last time

private struct SeriesCarryCard: View {
    @Environment(AppState.self) private var state

    var body: some View {
        if let series = state.draftSeries {
            let last = state.lastSession(inSeries: series.id)
            let count = state.sessionCount(inSeries: series.id)
            VStack(alignment: .leading, spacing: 0) {
                HStack(alignment: .top, spacing: Space.s3) {
                    Image(systemName: "square.stack.3d.up").foregroundStyle(Ink.secondary).padding(.top, 2)
                    VStack(alignment: .leading, spacing: 2) {
                        Text("这是「\(series.name)」的第 \(count + 1) 场").font(Typo.body)
                        if let last {
                            Text("上一场：\(last.displayTitle) · \(last.startedAt.formatted(date: .abbreviated, time: .omitted))")
                                .font(Typo.caption).foregroundStyle(Ink.secondary)
                        } else {
                            Text("这个系列还没有开过会").font(Typo.caption).foregroundStyle(Ink.secondary)
                        }
                    }
                    Spacer(minLength: Space.s2)
                    Button("不是同一个系列") { state.declineDraftSeries() }
                        .buttonStyle(GhostButtonStyle(height: 28))
                }
                .padding(Space.s4)
                SeriesBriefingPanel(workspace: series, sessions: state.archive.sessions,
                                    onOpenSession: state.openArchivedMeeting)
                Rectangle().fill(Ink.hairline).frame(height: 1).padding(.horizontal, Space.s4)
                VStack(alignment: .leading, spacing: Space.s3) {
                    if !series.documents.isEmpty {
                        CarryRow(isOn: binding(\.materials),
                                 title: String(localized: "资料 · \(names(series.documents.map(\.name)))"),
                                 detail: String(localized: "\(series.documents.count) 份"))
                    }
                    if series.useKnowledge {
                        if !series.questionBank.isEmpty {
                            CarryRow(isOn: binding(\.review),
                                     title: String(localized: "复盘 · 改进后的答法"),
                                     detail: String(localized: "\(series.questionBank.count) 题"))
                        }
                        if !series.lessons.isEmpty {
                            CarryRow(isOn: binding(\.lessons),
                                     title: String(localized: "经验 · \(quoted(series.lessons.map(\.text)))"),
                                     detail: String(localized: "\(series.lessons.count) 条"))
                        }
                    } else if !series.questionBank.isEmpty || !series.lessons.isEmpty {
                        Text("系列知识已关闭；经验和答法不会带入。")
                            .font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                    if let last, last.transcriptCount > 0 {
                        CarryRow(isOn: binding(\.transcript),
                                 title: String(localized: "上一场完整转写"),
                                 detail: String(localized: "\(max(1, Int(last.duration / 60))) 分钟"))
                    }
                    if series.documents.isEmpty && (!series.useKnowledge || (series.questionBank.isEmpty && series.lessons.isEmpty))
                        && (last?.transcriptCount ?? 0) == 0 {
                        Text("这个系列还没有资料、题库或经验可以带入。").font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                }
                .padding(Space.s4)
            }
            .paperCard(padding: 0)
        }
    }

    private func binding(_ keyPath: WritableKeyPath<MeetingCarry, Bool>) -> Binding<Bool> {
        Binding(get: { (state.meetingDraft?.carry ?? .standard)[keyPath: keyPath] },
                set: { value in
                    var carry = state.meetingDraft?.carry ?? .standard
                    carry[keyPath: keyPath] = value
                    state.setDraftCarry(carry)
                })
    }

    private func names(_ items: [String]) -> String {
        let shown = items.prefix(2).joined(separator: "、")
        return items.count > 2 ? shown + "…" : shown
    }

    private func quoted(_ items: [String]) -> String {
        items.prefix(2).map { "“\($0.prefix(14))”" }.joined()
    }
}

/// An offline, source-linked snapshot of this series. Only reviewed knowledge is
/// presented as reusable guidance; pending candidates remain visibly separate.
private struct SeriesBriefingPanel: View {
    @Environment(AppState.self) private var state
    let workspace: Workspace
    let sessions: [SessionMeta]
    let onOpenSession: (UUID) -> Void
    @State private var evidenceReferences: [SessionEvidenceReference] = []
    @State private var isLoadingEvidence = true

    private var briefing: SeriesBriefing {
        SeriesBriefingBuilder.build(workspace: workspace, sessions: sessions,
                                    evidenceReferences: evidenceReferences)
    }

    private var evidenceLoadKey: String {
        let archiveRevision = "\(state.archive.searchableContentRevision)|\(state.archive.notesRevision)"
        let items = (workspace.seriesItems ?? []).map { "\($0.sourceSessionID.uuidString)|\($0.evidenceID)|\($0.claimRevision)|\($0.sourceRevision)" }
            .sorted().joined(separator: ";")
        let candidates = (workspace.seriesChangeCandidates ?? []).filter { $0.status == .pending }
            .map { "\($0.sourceSessionID.uuidString)|\($0.id)" }
            .sorted().joined(separator: ";")
        return archiveRevision + "|" + items + "|" + candidates
    }

    var body: some View {
        let briefing = briefing
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack(alignment: .firstTextBaseline) {
                Text("系列简报").font(Typo.label)
                Spacer()
                Text("本机 · 已保存内容").font(Typo.caption).foregroundStyle(Ink.secondary)
            }
            if briefing.latestSessions.isEmpty {
                Text("这个系列还没有已归档的会议。").font(Typo.caption).foregroundStyle(Ink.secondary)
            } else {
                VStack(alignment: .leading, spacing: Space.s1) {
                    Text("最近会议 · 共 \(briefing.sessionCount) 场").font(Typo.caption).foregroundStyle(Ink.secondary)
                    ForEach(briefing.latestSessions) { meta in
                        Button { onOpenSession(meta.id) } label: {
                            HStack(spacing: Space.s2) {
                                Text(verbatim: meta.displayTitle).lineLimit(1)
                                Spacer(minLength: 0)
                                Text(meta.startedAt.formatted(date: .abbreviated, time: .omitted))
                                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                                Image(systemName: "arrow.up.right").font(.system(size: 9, weight: .medium))
                                    .foregroundStyle(Ink.tertiary)
                            }
                            .font(Typo.caption)
                            .contentShape(Rectangle())
                        }
                        .buttonStyle(.plain)
                        .help("打开这场会议的归档和原话")
                    }
                }
            }

            if isLoadingEvidence && (!(workspace.seriesItems ?? []).isEmpty || !(workspace.seriesChangeCandidates ?? []).isEmpty) {
                Label("正在核对本机原话和版本…", systemImage: "arrow.triangle.2.circlepath")
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
            }

            if briefing.knowledgeEnabled, !isLoadingEvidence, !briefing.staleItems.isEmpty {
                VStack(alignment: .leading, spacing: Space.s2) {
                    Text("需要重新确认 · 不会带入下一场")
                        .font(Typo.caption).foregroundStyle(Ink.caution)
                    ForEach(briefing.staleItems) { item in
                        VStack(alignment: .leading, spacing: Space.s1) {
                            Text(verbatim: item.text).font(Typo.caption).textSelection(.enabled)
                            seriesSourceStatusLabel(item)
                            HStack {
                                Text("第 \(item.revision) 版")
                                    .font(Typo.caption).foregroundStyle(Ink.tertiary)
                                Spacer()
                                if item.sourceMissing != true, let source = sourceSession(item.sourceSessionID) {
                                    sourceButton(source)
                                }
                            }
                        }
                        .padding(Space.s2)
                        .background(Ink.sunken.opacity(0.5), in: RoundedRectangle(cornerRadius: Space.radiusSmall))
                    }
                }
            }

            if briefing.knowledgeEnabled {
                if !briefing.lessons.isEmpty {
                    VStack(alignment: .leading, spacing: Space.s1) {
                        Text("已确认的经验").font(Typo.caption).foregroundStyle(Ink.secondary)
                        ForEach(briefing.lessons) { lesson in
                            HStack(alignment: .top, spacing: Space.s1) {
                                Text("•").foregroundStyle(Ink.tertiary)
                                Text(verbatim: lesson.text).lineLimit(2)
                                Spacer(minLength: 0)
                                if let source = sourceSession(lesson.sourceSessionIDs, fallback: lesson.sourceSessionID) {
                                    sourceButton(source)
                                }
                            }
                            .font(Typo.caption)
                        }
                    }
                }
                if !briefing.questionBank.isEmpty {
                    VStack(alignment: .leading, spacing: Space.s1) {
                        Text("已确认的答法").font(Typo.caption).foregroundStyle(Ink.secondary)
                        ForEach(briefing.questionBank) { item in
                            HStack(alignment: .top, spacing: Space.s1) {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text(verbatim: item.question).fontWeight(.medium).lineLimit(1)
                                    Text(verbatim: item.answer).foregroundStyle(Ink.secondary).lineLimit(2)
                                }
                                Spacer(minLength: 0)
                                if let source = sourceSession(item.knownAnswerSourceSessionID) {
                                    sourceButton(source)
                                }
                            }
                            .font(Typo.caption)
                        }
                    }
                }
                if briefing.lessons.isEmpty && briefing.questionBank.isEmpty {
                    if briefing.decisions.isEmpty && briefing.openActions.isEmpty && briefing.resolvedActions.isEmpty
                        && briefing.staleItems.isEmpty
                        && !(isLoadingEvidence && !(workspace.seriesItems ?? []).isEmpty) {
                        Text("还没有已确认的系列内容。新建议会先等待你审核。")
                            .font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                }
                if !briefing.decisions.isEmpty {
                    VStack(alignment: .leading, spacing: Space.s2) {
                        Text("当前决定").font(Typo.caption).foregroundStyle(Ink.secondary)
                        ForEach(briefing.decisions) { item in
                            VStack(alignment: .leading, spacing: Space.s1) {
                                Text(verbatim: item.text).font(Typo.caption).textSelection(.enabled)
                                if item.sourceMissing == true {
                                    seriesSourceStatusLabel(item)
                                } else if let quote = item.sourceQuote, !quote.isEmpty {
                                    Text("依据：“\(quote)”\(item.location.map { " · \($0)" } ?? "")")
                                        .font(Typo.caption).foregroundStyle(Ink.secondary).textSelection(.enabled)
                                }
                                HStack {
                                    Text("第 \(item.revision) 版").font(Typo.caption).foregroundStyle(Ink.tertiary)
                                    Spacer()
                                    if item.sourceMissing != true, let source = sourceSession(item.sourceSessionID) { sourceButton(source) }
                                }
                                if (item.sourceMissing != true || item.independentlyRetained == true), !item.history.isEmpty {
                                    DisclosureGroup("变化记录（\(item.history.count)）") {
                                        ForEach(item.history.sorted { $0.revision < $1.revision }) { revision in
                                            HStack(alignment: .top, spacing: Space.s2) {
                                                Text("v\(revision.revision)").font(Typo.caption).foregroundStyle(Ink.tertiary)
                                                Text(verbatim: revision.text).font(Typo.caption).textSelection(.enabled)
                                                Spacer(minLength: 0)
                                                if revision.sourceMissing == true {
                                                    missingSeriesEvidenceLabel
                                                } else if let source = sourceSession(revision.sourceSessionID) {
                                                    sourceButton(source)
                                                }
                                            }
                                        }
                                    }
                                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                                }
                            }
                            .padding(Space.s2)
                            .background(Ink.sunken.opacity(0.5), in: RoundedRectangle(cornerRadius: Space.radiusSmall))
                        }
                    }
                }
                if !briefing.openActions.isEmpty {
                    VStack(alignment: .leading, spacing: Space.s2) {
                        Text("未完成事项").font(Typo.caption).foregroundStyle(Ink.secondary)
                        ForEach(briefing.openActions) { item in
                            VStack(alignment: .leading, spacing: Space.s1) {
                                Text(verbatim: item.text).font(Typo.caption).textSelection(.enabled)
                                if item.sourceMissing == true {
                                    seriesSourceStatusLabel(item)
                                } else if let quote = item.sourceQuote, !quote.isEmpty {
                                    Text("依据：“\(quote)”\(item.location.map { " · \($0)" } ?? "")")
                                        .font(Typo.caption).foregroundStyle(Ink.secondary).textSelection(.enabled)
                                }
                                HStack(spacing: Space.s2) {
                                    if let owner = item.owner, !owner.isEmpty { Text("负责人：\(owner)") }
                                    if let due = item.due, !due.isEmpty { Text("期限：\(due)") }
                                    Spacer(minLength: 0)
                                    if item.sourceMissing != true, let source = sourceSession(item.sourceSessionID) { sourceButton(source) }
                                }
                                .font(Typo.caption).foregroundStyle(Ink.secondary)
                                if item.sourceMissing != true || item.independentlyRetained == true {
                                    HStack(spacing: Space.s2) {
                                    if item.actionState == nil || item.actionState == .open {
                                        Button("进行中") {
                                            state.updateSeriesActionState(.inProgress, itemID: item.id,
                                                                          expectedRevision: item.revision,
                                                                          workspaceID: workspace.id)
                                        }
                                        .buttonStyle(GhostButtonStyle(height: 30, horizontalPadding: 8))
                                    }
                                    Button("完成") {
                                        state.updateSeriesActionState(.done, itemID: item.id,
                                                                      expectedRevision: item.revision,
                                                                      workspaceID: workspace.id)
                                    }
                                    .buttonStyle(GhostButtonStyle(height: 30, horizontalPadding: 8))
                                    Button("取消") {
                                        state.updateSeriesActionState(.cancelled, itemID: item.id,
                                                                      expectedRevision: item.revision,
                                                                      workspaceID: workspace.id)
                                    }
                                    .buttonStyle(GhostButtonStyle(height: 30, horizontalPadding: 8))
                                    }
                                }
                            }
                            .padding(Space.s2)
                            .background(Ink.sunken.opacity(0.5), in: RoundedRectangle(cornerRadius: Space.radiusSmall))
                        }
                    }
                }
                if !briefing.resolvedActions.isEmpty {
                    DisclosureGroup("已完成或取消（\(briefing.resolvedActions.count)）") {
                        VStack(alignment: .leading, spacing: Space.s2) {
                            ForEach(briefing.resolvedActions) { item in
                                VStack(alignment: .leading, spacing: Space.s1) {
                                    HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                                        Text(verbatim: item.text).font(Typo.caption).textSelection(.enabled)
                                        Spacer(minLength: 0)
                                        Text(actionStateLabel(item.actionState))
                                            .font(Typo.caption).foregroundStyle(Ink.tertiary)
                                    }
                                    if item.sourceMissing == true { seriesSourceStatusLabel(item) }
                                    if let due = item.due, !due.isEmpty {
                                        Text("期限：\(due)").font(Typo.caption).foregroundStyle(Ink.secondary)
                                    }
                                    HStack {
                                        if (item.sourceMissing != true || item.independentlyRetained == true)
                                            && (item.actionState == .done || item.actionState == .cancelled) {
                                            Button("重新打开") {
                                                state.updateSeriesActionState(.open, itemID: item.id,
                                                                              expectedRevision: item.revision,
                                                                              workspaceID: workspace.id)
                                            }
                                            .buttonStyle(GhostButtonStyle(height: 30, horizontalPadding: 8))
                                        }
                                        Spacer()
                                        if item.sourceMissing != true, let source = sourceSession(item.sourceSessionID) { sourceButton(source) }
                                    }
                                }
                                .padding(Space.s2)
                                .background(Ink.sunken.opacity(0.5), in: RoundedRectangle(cornerRadius: Space.radiusSmall))
                            }
                        }
                        .padding(.top, Space.s2)
                    }
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                }
                if !briefing.pendingSeriesChanges.isEmpty {
                    Label("\(briefing.pendingSeriesChanges.count) 条决定或行动待确认；尚未用于简报。", systemImage: "hourglass")
                        .font(Typo.caption).foregroundStyle(Ink.caution)
                        .fixedSize(horizontal: false, vertical: true)
                }
            } else {
                Text("系列知识已关闭；本简报不会带入经验或答法。")
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
            }

            if !briefing.pendingCandidates.isEmpty {
                Label("\(briefing.pendingCandidates.count) 条建议待确认，不会作为已确认内容带入。", systemImage: "hourglass")
                    .font(Typo.caption).foregroundStyle(Ink.caution)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .padding(.horizontal, Space.s4)
        .padding(.bottom, Space.s4)
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .contain)
        .task(id: evidenceLoadKey) {
            isLoadingEvidence = true
            let validIDs = Set(sessions.filter { $0.workspaceID == workspace.id && $0.workspaceDeleted != true }.map(\.id))
            let storedItemSources = (workspace.seriesItems ?? []).map(\.sourceSessionID)
            let candidateSources = (workspace.seriesChangeCandidates ?? []).filter { $0.status == .pending }
                .map(\.sourceSessionID)
            let sourceIDs = Set(storedItemSources + candidateSources).intersection(validIDs)
            var loaded: [SessionEvidenceReference] = []
            for sessionID in sourceIDs.sorted(by: { $0.uuidString < $1.uuidString }) {
                guard !Task.isCancelled, let detail = await state.archive.detail(sessionID),
                      detail.meta.workspaceID == workspace.id, detail.meta.workspaceDeleted != true else { continue }
                loaded.append(contentsOf: SessionEvidenceBuilder.build(meta: detail.meta, lines: detail.lines,
                                                                       notes: detail.notes, summary: detail.summary))
            }
            guard !Task.isCancelled else { return }
            evidenceReferences = loaded
            isLoadingEvidence = false
        }
    }

    private func sourceSession(_ sourceIDs: [UUID]?, fallback: UUID? = nil) -> UUID? {
        let ids = Set(sourceIDs ?? []).union(fallback.map { [$0] } ?? [])
        return sessions
            .filter { ids.contains($0.id) && $0.workspaceID == workspace.id && $0.workspaceDeleted != true }
            .max { $0.startedAt < $1.startedAt }?.id
    }

    private func sourceSession(_ id: UUID?) -> UUID? {
        guard let id else { return nil }
        return sessions.first(where: { $0.id == id && $0.workspaceID == workspace.id && $0.workspaceDeleted != true })?.id
    }

    private func sourceButton(_ sessionID: UUID) -> some View {
        Button("来源") { onOpenSession(sessionID) }
            .font(.system(size: 10, weight: .medium))
            .buttonStyle(.plain)
            .foregroundStyle(Brand.accent)
            .help("打开产生这条已确认内容的会议")
    }

    private func actionStateLabel(_ state: WorkspaceSeriesItem.ActionState?) -> String {
        switch state {
        case .done: "已完成"
        case .cancelled: "已取消"
        case .superseded: "已替代"
        case .open, .inProgress, .none: "已结束"
        }
    }

    private var missingSeriesEvidenceLabel: some View {
        Label("依据缺失 · 待重新确认", systemImage: "exclamationmark.triangle.fill")
            .font(Typo.caption).foregroundStyle(Ink.caution)
            .fixedSize(horizontal: false, vertical: true)
    }

    @ViewBuilder
    private func seriesSourceStatusLabel(_ item: WorkspaceSeriesItem) -> some View {
        if item.independentlyRetained == true {
            Label("已作为独立内容保留 · 原来源会议已删除", systemImage: "checkmark.circle")
                .font(Typo.caption).foregroundStyle(Ink.secondary)
                .fixedSize(horizontal: false, vertical: true)
        } else if item.sourceMissing == true {
            missingSeriesEvidenceLabel
        } else {
            Label("原话或纪要版本已变化 · 需要重新确认", systemImage: "exclamationmark.triangle.fill")
                .font(Typo.caption).foregroundStyle(Ink.caution)
                .fixedSize(horizontal: false, vertical: true)
        }
    }
}

private struct CarryRow: View {
    @Binding var isOn: Bool
    let title: String
    let detail: String

    var body: some View {
        Toggle(isOn: $isOn) {
            HStack(spacing: Space.s2) {
                Text(verbatim: title).font(Typo.body).lineLimit(1)
                Spacer(minLength: Space.s2)
                Text(verbatim: detail).font(Typo.caption).foregroundStyle(Ink.secondary)
            }
        }
        .toggleStyle(.wide)
    }
}

// MARK: - 3 · New materials

private struct MaterialsActions: View {
    @Environment(AppState.self) private var state
    @State private var isImporterPresented = false

    private var allowedTypes: [UTType] { DocumentImporter.supportedExtensions.compactMap { UTType(filenameExtension: $0) } }

    var body: some View {
        HStack(spacing: Space.s1) {
            let others = state.workspaces.visible.filter { $0.id != state.meetingDraft?.seriesID }
            if !others.isEmpty {
                Menu {
                    ForEach(others) { series in
                        Button(series.name) { state.continueDraftSeries(series.id) }
                    }
                } label: {
                    Text("从以前的会议带入…")
                }
                .menuStyle(.button)
                .buttonStyle(GhostButtonStyle(height: 28))
                .menuIndicator(.hidden)
                .fixedSize()
                .help("选择一个以前的系列，带入它的资料、复盘和经验")
            }
            Button { isImporterPresented = true } label: { Label("添加", systemImage: "plus") }
                .buttonStyle(GhostButtonStyle(height: 28))
        }
        .fileImporter(isPresented: $isImporterPresented, allowedContentTypes: allowedTypes, allowsMultipleSelection: true) { result in
            switch result {
            case .success(let urls): state.importDraftDocuments(urls)
            case .failure(let error): state.errorMessage = error.localizedDescription
            }
        }
    }
}

private struct MaterialsBlock: View {
    @Environment(AppState.self) private var state
    @State private var dropTargeted = false

    var body: some View {
        let jobs = state.draftJobs
        VStack(alignment: .leading, spacing: Space.s3) {
            if jobs.isEmpty {
                dropZone
            } else {
                VStack(spacing: Space.s2) {
                    ForEach(jobs) { job in FileRow(job: job) }
                }
            }
            if hasMaterials { MaterialsInsight() }
        }
        .onDrop(of: [.fileURL], isTargeted: $dropTargeted) { providers in
            load(providers)
            return true
        }
    }

    private var hasMaterials: Bool {
        guard let series = state.draftSeries else { return false }
        return !series.documents.filter(\.enabled).isEmpty || !series.materialText.isEmpty
    }

    private var dropZone: some View {
        let hint = (state.meetingDraft?.mode == .custom ? state.meetingDraft?.customBaseMode : state.meetingDraft?.mode)?.preparationHint ?? ""
        return VStack(spacing: Space.s1) {
            Text("把文件拖到这里，或点右上角“添加”").font(Typo.body).foregroundStyle(Ink.secondary)
            if !hint.isEmpty {
                Text("建议放：\(hint)").font(Typo.caption).foregroundStyle(Ink.secondary)
            }
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, Space.s6)
        .background {
            RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous)
                .strokeBorder(dropTargeted ? Ink.ink : Ink.hairline, style: StrokeStyle(lineWidth: 1, dash: [5, 4]))
        }
    }

    private func load(_ providers: [NSItemProvider]) {
        Task { @MainActor in
            var urls: [URL] = []
            for provider in providers {
                guard let item = try? await provider.loadItem(forTypeIdentifier: UTType.fileURL.identifier) else { continue }
                let url = (item as? URL) ?? (item as? Data).flatMap { URL(dataRepresentation: $0, relativeTo: nil) }
                guard let url else { continue }
                urls.append(contentsOf: DocumentImporter.expand(url))
            }
            if urls.isEmpty {
                state.errorMessage = String(localized: "拖入的文件类型不受支持。")
            } else {
                state.importDraftDocuments(urls)
            }
        }
    }
}

/// One material being read: 读取中 → 已读懂.
private struct FileRow: View {
    @Environment(AppState.self) private var state
    let job: DocumentJob

    var body: some View {
        HStack(spacing: Space.s3) {
            Text(verbatim: format)
                .font(.system(size: 9, weight: .semibold))
                .foregroundStyle(Ink.secondary)
                .frame(width: 32, height: 32)
                .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            VStack(alignment: .leading, spacing: 6) {
                Text(verbatim: job.fileName).font(Typo.body).lineLimit(1)
                if !job.stage.terminal {
                    if let total = job.total, total > 0 {
                        ProgressView(value: Double(min(job.completed, total)), total: Double(total)).controlSize(.small)
                    } else {
                        ProgressView().progressViewStyle(.linear).controlSize(.small)
                    }
                }
            }
            Spacer(minLength: Space.s2)
            status
        }
        .padding(.horizontal, Space.s3).padding(.vertical, Space.s2)
        .paperCard(padding: 0)
    }

    @ViewBuilder private var status: some View {
        switch job.stage {
        case .ready:
            Label("已读懂", systemImage: "checkmark").font(Typo.caption).foregroundStyle(Ink.ready)
        case .warning:
            Label("已读懂 · 需核对", systemImage: "exclamationmark.triangle").font(Typo.caption).foregroundStyle(Ink.caution)
                .help(job.message)
        case .failed, .cancelled:
            HStack(spacing: Space.s2) {
                Text(job.stage == .failed ? "没读出来" : "已取消").font(Typo.caption).foregroundStyle(Ink.caution).help(job.message)
                Button("重试") { state.documentPreparation.retry(job.id) }.buttonStyle(GhostButtonStyle(height: 24, horizontalPadding: 6))
            }
        default:
            Text("读取中").font(Typo.caption).foregroundStyle(Ink.secondary)
        }
    }

    private var format: String {
        let ext = (job.fileName as NSString).pathExtension.uppercased()
        return ext.isEmpty ? "DOC" : String(ext.prefix(4))
    }
}

/// "AI 读到的重点" and "可能会问": written on request from the series' materials.
private struct MaterialsInsight: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            AIHeading(title: "AI 读到的重点")
            if showsAnswer {
                let text = state.displayedAnswer
                if text.isEmpty {
                    HStack(spacing: Space.s2) {
                        ProgressView().controlSize(.small)
                        Text("正在读资料…").font(Typo.body).foregroundStyle(Ink.secondary)
                    }
                } else {
                    MarkdownText(text: text, size: 14, lineSpacing: 5).equatable()
                }
            } else {
                Text("读完资料后，AI 写出它读到的 3 条重点和 5 道可能会问的题，可以先在心里过一遍。")
                    .font(Typo.body).foregroundStyle(Ink.secondary)
                    .fixedSize(horizontal: false, vertical: true)
                Button("读一遍资料") { state.readDraftMaterials() }
                    .buttonStyle(SecondaryButtonStyle())
                    .disabled(state.isAnswering || !state.answerEngineReady)
                    .help(state.answerEngineReady ? "用回答服务读一遍这场会议的资料" : "先连接回答服务")
            }
        }
        .aiBlock()
    }

    private var showsAnswer: Bool {
        guard let id = state.predictionAnswerID else { return false }
        return state.currentAnswerID == id
    }
}

// MARK: - 4 · Before you start

private struct CheckCard: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(spacing: 0) {
            AudioCheckRow()
            divider
            AnswerCheckRow()
            if state.companionEnabled {
                divider
                PhoneCheckRow()
            }
        }
        .paperCard(padding: 0)
    }

    private var divider: some View {
        Rectangle().fill(Ink.hairline).frame(height: 1)
    }
}

/// One line of the check: status dot, label, content, an optional action.
private struct CheckRow<Content: View>: View {
    let tone: StatusDot.Tone
    let label: LocalizedStringKey
    let content: () -> Content

    init(tone: StatusDot.Tone, label: LocalizedStringKey, @ViewBuilder content: @escaping () -> Content) {
        self.tone = tone; self.label = label; self.content = content
    }

    var body: some View {
        HStack(alignment: .center, spacing: Space.s3) {
            StatusDot(tone: tone)
            Text(label).font(Typo.label).foregroundStyle(Ink.secondary).frame(width: 56, alignment: .leading)
            content()
        }
        .padding(.horizontal, Space.s4)
        .frame(minHeight: 52)
    }
}

private struct AudioCheckRow: View {
    @Environment(AppState.self) private var state

    var body: some View {
        CheckRow(tone: tone, label: "声音") {
            VStack(alignment: .leading, spacing: 6) {
                HStack(spacing: Space.s3) {
                    VenuePicker()
                    if state.meetingVenue == .online {
                        AudioSourceMenu()
                    }
                    Label(state.microphoneEnabled || state.meetingVenue == .offline ? "麦克风 · 我" : "麦克风未开启", systemImage: "mic")
                        .font(Typo.label)
                        .foregroundStyle(state.microphoneEnabled || state.meetingVenue == .offline ? Ink.ink : Ink.secondary)
                        .lineLimit(1)
                    Spacer(minLength: 0)
                    action
                }
                if let problem { Text(verbatim: problem).font(Typo.caption).foregroundStyle(Ink.caution) }
                if state.isDownloadingModel {
                    ProgressView(value: state.modelDownloadProgress).controlSize(.small)
                }
                if state.meetingVenue == .offline {
                    Text("线下会议只用麦克风，转写标为“现场”。笔记本尽量放在桌子中间；人多时建议外接会议麦克风。")
                        .font(Typo.caption).foregroundStyle(Ink.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            .padding(.vertical, Space.s3)
        }
    }

    /// What blocks the audio, in words (nil when it is fine).
    private var problem: String? {
        if state.meetingVenue == .online && !state.capturePermissionGranted {
            return String(localized: "需要允许录屏与系统音频，才能听到会议里的声音")
        }
        if state.transcriptionBackend == "zipformer" && (!state.isModelInstalled || !state.missingPackages.isEmpty) {
            return String(localized: "需要先下载本机识别模型（只需一次）")
        }
        if (state.microphoneEnabled || state.meetingVenue == .offline) && state.microphoneAccess == .denied {
            return String(localized: "麦克风没有授权")
        }
        if !state.recognitionReady { return String(localized: "语音识别还没设置好") }
        return nil
    }

    private var tone: StatusDot.Tone { problem == nil ? .ready : .caution }

    @ViewBuilder private var action: some View {
        if state.meetingVenue == .online && !state.capturePermissionGranted {
            Button("允许") { Task { await state.requestCapturePermission() } }.buttonStyle(GhostButtonStyle(height: 28))
        } else if state.transcriptionBackend == "zipformer" && (!state.isModelInstalled || !state.missingPackages.isEmpty) {
            if state.isDownloadingModel {
                Text(verbatim: "\(Int(state.modelDownloadProgress * 100))%").font(Typo.caption).foregroundStyle(Ink.secondary)
            } else {
                Button("下载") { state.downloadModel() }.buttonStyle(GhostButtonStyle(height: 28))
            }
        } else if (state.microphoneEnabled || state.meetingVenue == .offline) && state.microphoneAccess == .denied {
            Button("打开系统设置") { state.openMicrophoneSettings() }.buttonStyle(GhostButtonStyle(height: 28))
        } else if !state.recognitionReady {
            Button("去设置") { state.openSettings(.recognition) }.buttonStyle(GhostButtonStyle(height: 28))
        }
    }
}

/// 线上 / 线下.
private struct VenuePicker: View {
    @Environment(AppState.self) private var state
    var body: some View {
        InkSegmented(items: MeetingVenue.allCases.map { (value: $0, title: $0.title) },
                     selection: Binding(get: { state.meetingVenue }, set: { state.setMeetingVenue($0) }))
            .fixedSize()
    }
}

/// "电脑声音" by default; one window only as a secondary choice.
struct AudioSourceMenu: View {
    @Environment(AppState.self) private var state

    var body: some View {
        Menu {
            let displays = state.captureTargets.filter { $0.id.hasPrefix("display:") }
            let windows = state.captureTargets.filter { $0.id.hasPrefix("window:") }
            ForEach(displays) { target in
                Button { state.selectCaptureTarget(target.id) } label: {
                    Text(verbatim: displays.count > 1 ? state.audioSourceTitle(for: target.id) + " · " + target.title : state.audioSourceTitle(for: target.id))
                }
            }
            if !windows.isEmpty {
                Menu("只听某个窗口…") {
                    ForEach(windows) { target in
                        Button { state.selectCaptureTarget(target.id) } label: { Text(verbatim: target.title) }
                    }
                }
            }
            Divider()
            Button("刷新") { Task { await state.refreshTargets() } }
        } label: {
            Label {
                Text(verbatim: state.audioSourceTitle(for: state.selectedTargetID))
            } icon: {
                Image(systemName: "speaker.wave.2")
            }
            .font(Typo.label)
            .foregroundStyle(Ink.ink)
            .lineLimit(1)
        }
        .menuStyle(.button)
        .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 6))
        .menuIndicator(.hidden)
        .fixedSize()
        .help("对方的声音从哪里来：默认是这台 Mac 播放的所有声音（App 自己的声音会被排除）")
    }
}

private struct AnswerCheckRow: View {
    @Environment(AppState.self) private var state

    var body: some View {
        CheckRow(tone: state.answerEngineReady ? .ready : .caution, label: "回答") {
            HStack(spacing: Space.s2) {
                if state.answerEngineReady {
                    Text(verbatim: engineLine).font(Typo.label).lineLimit(1)
                    if state.activeServiceTier == "fast" {
                        Label("快速", systemImage: "bolt").font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                } else {
                    Text(state.checkingCodexStatus && state.answerBackend != .openAICompatible ? "正在检测回答服务…" : "回答服务还没连上")
                        .font(Typo.label).foregroundStyle(Ink.caution)
                }
                Spacer(minLength: 0)
                Button(state.answerEngineReady ? "更换" : "去连接") { state.openSettings(.answers) }
                    .buttonStyle(GhostButtonStyle(height: 28))
            }
        }
    }

    private var engineLine: String {
        let model = state.currentAnswerModel
        return state.answerBackend == .openAICompatible ? "API · \(model)" : "Codex · \(model)"
    }
}

private struct PhoneCheckRow: View {
    @Environment(AppState.self) private var state

    var body: some View {
        CheckRow(tone: state.companionConnected ? .ready : .caution, label: "手机") {
            HStack {
                Text(verbatim: state.companionConnectionText).font(Typo.label).lineLimit(1)
                Spacer(minLength: 0)
            }
        }
    }
}

private struct StartButton: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let jobs = state.draftJobs
        let pending = jobs.filter { !$0.stage.terminal }.count
        Button { Task { await state.startPreparedMeeting() } } label: {
            HStack(spacing: Space.s2) {
                if state.isChangingSession { ProgressView().controlSize(.small) }
                if state.isChangingSession {
                    Text("正在启动…")
                } else if pending > 0 {
                    Text("开始（资料还在读取 \(jobs.count - pending)/\(jobs.count)）")
                } else {
                    Text("开始")
                }
            }
            .frame(minWidth: 96)
        }
        .buttonStyle(PrimaryButtonStyle(height: Space.controlLarge))
        .disabled(state.isChangingSession)
    }
}
