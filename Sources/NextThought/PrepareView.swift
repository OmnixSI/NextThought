import AppKit
import SwiftUI
import UniformTypeIdentifiers
import NextThoughtCore

/// 准备页: one page, top to bottom: name and scene of the meeting, what to bring from
/// last time, new materials, and a check before starting (02-scenarios-and-flows.md).
/// The title and scene sit on the one raised sheet; everything else is flat rows
/// between hairlines. The start action is pinned to the window bottom.
struct PrepareView: View {
    @Environment(AppState.self) private var state
    @State private var appeared = false

    private var mode: SessionMode { state.meetingDraft?.mode ?? .general }

    var body: some View {
        VStack(spacing: 0) {
            ScrollView {
                VStack(alignment: .leading, spacing: Space.s10) {
                    PrepareComposer()
                        .appear(appeared, order: 0)
                    if state.draftContinuesSeries {
                        PrepareSection(title: "接着上一场") { SeriesCarryCard() }
                            .transition(.opacity.combined(with: .offset(y: -6)))
                    }
                    PrepareSection(title: "这次新加的资料", note: "可跳过", trailing: { MaterialsActions() }) {
                        MaterialsBlock()
                    }
                    .appear(appeared, order: 2)
                    PrepareSection(title: "开始前检查") { CheckCard() }
                        .appear(appeared, order: 3)
                }
                .animation(Motion.expand, value: state.draftContinuesSeries)
                .frame(maxWidth: Space.content, alignment: .leading)
                .padding(.horizontal, Space.s8)
                .padding(.top, Space.s6)
                .padding(.bottom, Space.s10)
                .frame(maxWidth: .infinity)
            }
            PreparationStartBar()
        }
        .background(alignment: .top) {
            InkWash(tint: mode.theme.tint, animated: false)
                .frame(height: 300)
                .opacity(0.7)
                .animation(.easeInOut(duration: 0.8), value: mode)
        }
        .accessibilityIdentifier("meeting.prepare")
        .disabled(state.isChangingSession)
        .onAppear { appeared = true }
    }
}

// MARK: - Layout pieces

/// Settles into place when the page opens; later sections follow a beat behind.
private struct AppearModifier: ViewModifier {
    let appeared: Bool
    let order: Int
    func body(content: Content) -> some View {
        content
            .opacity(appeared ? 1 : 0)
            .offset(y: appeared ? 0 : 8)
            .animation(Motion.phase.map { $0.delay(Double(order) * 0.05) }, value: appeared)
    }
}

private extension View {
    func appear(_ appeared: Bool, order: Int) -> some View {
        modifier(AppearModifier(appeared: appeared, order: order))
    }

    /// A flat entry inside a section: a hairline rule at its leading edge instead of a box.
    func quietEntry() -> some View {
        padding(.leading, Space.s3)
            .overlay(alignment: .leading) { Rectangle().fill(Ink.hairline).frame(width: 1) }
    }
}

/// A section: overline label (with a note and trailing actions), a hairline, the content.
private struct PrepareSection<Trailing: View, Content: View>: View {
    let title: LocalizedStringKey
    let note: LocalizedStringKey?
    let trailing: () -> Trailing
    let content: () -> Content

    init(title: LocalizedStringKey, note: LocalizedStringKey? = nil,
         @ViewBuilder trailing: @escaping () -> Trailing,
         @ViewBuilder content: @escaping () -> Content) {
        self.title = title; self.note = note
        self.trailing = trailing; self.content = content
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .center, spacing: Space.s2) {
                Text(title).overline()
                if let note { Text(note).font(Typo.caption).foregroundStyle(Ink.tertiary) }
                Spacer(minLength: Space.s2)
                trailing()
            }
            .frame(minHeight: 28)
            Rectangle().fill(Ink.hairline).frame(height: 0.5).padding(.top, Space.s2)
            content().padding(.top, Space.s3)
        }
    }
}

extension PrepareSection where Trailing == EmptyView {
    fileprivate init(title: LocalizedStringKey, note: LocalizedStringKey? = nil, @ViewBuilder content: @escaping () -> Content) {
        self.init(title: title, note: note, trailing: { EmptyView() }, content: content)
    }
}

// MARK: - Title and scene (the one raised sheet)

private struct PrepareComposer: View {
    @Environment(AppState.self) private var state
    @FocusState private var titleFocused: Bool
    @Namespace private var chip

    var body: some View {
        let draft = state.meetingDraft ?? MeetingDraft()
        let mode = draft.mode
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .top, spacing: Space.s3) {
                SceneGlyph(mode: mode, size: 44)
                    .id(mode).transition(.scale(scale: 0.8).combined(with: .opacity))
                TextField("这场会议叫什么", text: Binding(get: { state.meetingDraft?.title ?? "" },
                                                    set: { state.updateDraftTitle($0) }), axis: .vertical)
                    .textFieldStyle(.plain)
                    .font(Typo.display)
                    .lineLimit(1...3)
                    .focused($titleFocused)
                    .frame(minHeight: 44)
                    .accessibilityLabel("会议名称，可选")
                    .accessibilityHint("用于查找会议，可以会后修改")
                VenuePicker()
                    .frame(minHeight: 44)
            }
            .padding(.horizontal, Space.s6).padding(.top, Space.s6).padding(.bottom, Space.s6)

            TypePicker(chip: chip)
                .padding(.horizontal, Space.s6)
                .padding(.bottom, Space.s6)
        }
        .background {
            RoundedRectangle(cornerRadius: Space.radiusLarge, style: .continuous)
                .fill(Ink.surface)
                .shadow(color: Ink.contactShadow, radius: 1, x: 0, y: 1)
                .shadow(color: Ink.ambientShadow, radius: 20, x: 0, y: 10)
        }
        .overlay {
            RoundedRectangle(cornerRadius: Space.radiusLarge, style: .continuous)
                .strokeBorder(titleFocused ? mode.theme.tint.opacity(0.45) : Ink.hairline, lineWidth: titleFocused ? 1 : 0.5)
        }
        .animation(Motion.snappy, value: mode)
        .animation(Motion.hover, value: titleFocused)
    }
}

// MARK: - Type

private struct TypePicker: View {
    @Environment(AppState.self) private var state
    let chip: Namespace.ID

    var body: some View {
        let draft = state.meetingDraft ?? MeetingDraft()
        let effective = draft.mode == .custom ? draft.customBaseMode : draft.mode
        let theme = effective.theme
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack(alignment: .firstTextBaseline) {
                Text("这是什么会").overline()
                if showsGuessNote(draft) {
                    Text("根据标题选好了，可以改").font(Typo.caption).foregroundStyle(Ink.tertiary)
                        .transition(.opacity)
                }
            }
            FlowLayout(spacing: 4, lineSpacing: 4) {
                ForEach(SessionMode.builtIn) { mode in
                    sceneChip(mode, selected: draft.mode == mode) { state.setDraftType(mode) }
                        .help(mode.defaultsSummary)
                }
                sceneChip(.custom, selected: draft.mode == .custom) { state.setDraftCustomType() }
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
                .transition(.opacity.combined(with: .offset(y: -4)))
            }
            VStack(alignment: .leading, spacing: Space.s2) {
                Text(verbatim: theme.promise)
                    .font(.system(size: 17, weight: .medium, design: .serif))
                    .foregroundStyle(Ink.ink)
                Text(verbatim: summary(draft))
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                    .fixedSize(horizontal: false, vertical: true)
                HStack(spacing: Space.s4) {
                    ForEach(theme.moments, id: \.self) { moment in
                        HStack(spacing: 6) {
                            Circle().fill(theme.tint).frame(width: 4, height: 4)
                            Text(verbatim: moment).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
                        }
                    }
                }
                .padding(.top, 2)
            }
            .padding(.top, Space.s1)
            .id(effective)
            .transition(.blurReplace)
        }
        .animation(Motion.expand, value: draft.mode)
    }

    private func showsGuessNote(_ draft: MeetingDraft) -> Bool {
        !draft.typeChosen && MeetingTypeGuesser.guess(draft.title) != .general
    }

    private func sceneChip(_ mode: SessionMode, selected: Bool, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            HStack(spacing: 4) {
                if mode == .custom {
                    Image(systemName: "plus").font(.system(size: 10, weight: .medium))
                    Text("自定义…")
                } else {
                    Text(verbatim: mode.displayName)
                }
            }
            .font(Typo.label)
            .foregroundStyle(selected ? mode.theme.tint : Ink.secondary)
            .padding(.horizontal, 11)
            .frame(height: 30)
            .background {
                if selected {
                    Capsule().fill(mode.theme.tint.opacity(0.13))
                        .matchedGeometryEffect(id: "selection", in: chip)
                }
            }
            .contentShape(Capsule())
        }
        .buttonStyle(SceneChipStyle())
        .accessibilityAddTraits(selected ? .isSelected : [])
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

private struct SceneChipStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View { Chip(configuration: configuration) }
    private struct Chip: View {
        let configuration: ButtonStyleConfiguration
        @State private var hovering = false
        var body: some View {
            configuration.label
                .background(hovering ? Ink.hover : .clear, in: Capsule())
                .scaleEffect(configuration.isPressed ? 0.96 : 1)
                .onHover { hovering = $0 }
                .animation(Motion.hover, value: hovering)
                .animation(Motion.snappy, value: configuration.isPressed)
        }
    }
}

// MARK: - Continue from last time

private struct SeriesCarryCard: View {
    @Environment(AppState.self) private var state

    var body: some View {
        if let series = state.draftSeries {
            let last = state.meetingDraft?.previousSessionID.flatMap { id in state.archive.sessions.first { $0.id == id } }
                ?? state.lastSession(inSeries: series.id)
            let count = state.sessionCount(inSeries: series.id)
            VStack(alignment: .leading, spacing: 0) {
                HStack(alignment: .center, spacing: Space.s3) {
                    Circle().fill(series.effectiveMode.theme.tint).frame(width: 7, height: 7)
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
                .padding(.horizontal, Space.s2).padding(.bottom, Space.s3)
                SeriesBriefingPanel(workspace: series, sessions: state.archive.sessions,
                                    onOpenSession: state.openArchivedMeeting)
                carryRows(series: series, last: last)
            }
        }
    }

    @ViewBuilder
    private func carryRows(series: Workspace, last: SessionMeta?) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            if !series.documents.isEmpty {
                rule
                CarryRow(isOn: binding(\.materials),
                         title: String(localized: "资料 · \(names(series.documents.map(\.name)))"),
                         detail: String(localized: "\(series.documents.count) 份"))
            }
            if series.useKnowledge {
                if !series.questionBank.isEmpty {
                    rule
                    CarryRow(isOn: binding(\.review),
                             title: String(localized: "复盘 · 改进后的答法"),
                             detail: String(localized: "\(series.questionBank.count) 题"))
                }
                if !series.lessons.isEmpty {
                    rule
                    CarryRow(isOn: binding(\.lessons),
                             title: String(localized: "经验 · \(quoted(series.lessons.map(\.text)))"),
                             detail: String(localized: "\(series.lessons.count) 条"))
                }
            } else if !series.questionBank.isEmpty || !series.lessons.isEmpty {
                rule
                Text("系列知识已关闭；经验和答法不会带入。")
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                    .padding(.horizontal, Space.s2).padding(.vertical, Space.s3)
            }
            if let last, last.transcriptCount > 0 {
                rule
                CarryRow(isOn: binding(\.transcript),
                         title: String(localized: "上一场完整转写"),
                         detail: String(localized: "\(max(1, Int(last.duration / 60))) 分钟"))
            }
            if series.documents.isEmpty && (!series.useKnowledge || (series.questionBank.isEmpty && series.lessons.isEmpty))
                && (last?.transcriptCount ?? 0) == 0 {
                rule
                Text("这个系列还没有资料、题库或经验可以带入。").font(Typo.caption).foregroundStyle(Ink.secondary)
                    .padding(.horizontal, Space.s2).padding(.vertical, Space.s3)
            }
            rule
        }
    }

    private var rule: some View { Rectangle().fill(Ink.hairline).frame(height: 0.5) }

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
                        .quietEntry()
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
                            .quietEntry()
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
                            .quietEntry()
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
                                .quietEntry()
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
        .padding(.horizontal, Space.s2)
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
            .foregroundStyle(Ink.secondary).underline()
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
                Text(verbatim: detail).font(Typo.caption).foregroundStyle(Ink.secondary).monospacedDigit()
            }
        }
        .toggleStyle(.wide)
        .padding(.horizontal, Space.s2)
    }
}

// MARK: - New materials

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
        VStack(alignment: .leading, spacing: Space.s4) {
            if jobs.isEmpty {
                dropZone
                    .transition(.opacity)
            } else {
                VStack(spacing: 0) {
                    ForEach(jobs) { job in
                        FileRow(job: job)
                            .transition(.opacity.combined(with: .offset(y: -6)))
                        Rectangle().fill(Ink.hairline).frame(height: 0.5)
                    }
                }
            }
            if hasMaterials { MaterialsInsight() }
        }
        .animation(Motion.expand, value: jobs.count)
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
            Image(systemName: "tray.and.arrow.down")
                .font(.system(size: 18, weight: .light))
                .foregroundStyle(dropTargeted ? Ink.ink : Ink.tertiary)
                .padding(.bottom, Space.s1)
            Text("把文件拖到这里，或点右上角“添加”").font(Typo.body).foregroundStyle(Ink.secondary)
            if !hint.isEmpty {
                Text("建议放：\(hint)").font(Typo.caption).foregroundStyle(Ink.tertiary)
            }
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, Space.s6)
        .background {
            RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous)
                .fill(dropTargeted ? Ink.hover : Color.clear)
        }
        .overlay {
            RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous)
                .strokeBorder(dropTargeted ? Ink.secondary : Ink.hairline, style: StrokeStyle(lineWidth: 1, dash: [4, 4]))
        }
        .animation(Motion.hover, value: dropTargeted)
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

/// Parsing, blocked dependencies and optional semantic enhancement are distinct.
private struct FileRow: View {
    @Environment(AppState.self) private var state
    let job: DocumentJob

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
          HStack(spacing: Space.s3) {
            Text(verbatim: format)
                .font(.system(size: 9, weight: .semibold))
                .foregroundStyle(Ink.secondary)
                .frame(width: 32, height: 32)
                .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            VStack(alignment: .leading, spacing: 6) {
                Text(verbatim: job.fileName).font(Typo.body).lineLimit(1)
                if [.receiving, .parsing, .ocr, .validating, .lexical, .embedding].contains(job.stage) {
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
          if [.waiting, .warning, .failed, .paused].contains(job.stage), !job.message.isEmpty {
              Text(LocalizedStringKey(job.message)).font(Typo.caption)
                  .foregroundStyle(job.stage == .paused ? Ink.secondary : Ink.caution)
                  .fixedSize(horizontal: false, vertical: true)
          }
          if job.stage == .waiting {
              HStack(spacing: Space.s2) {
                  Button { state.repairPreparation(job) } label: { Text(verbatim: state.preparationRepairTitle(job)) }
                      .buttonStyle(SecondaryButtonStyle())
                      .disabled(state.documentSetupBusy || !state.documentModelsChecked)
                  Button("取消此文件") { state.documentPreparation.cancel(job.id) }.buttonStyle(GhostButtonStyle())
                  if state.documentSetupBusy { ProgressView().controlSize(.small) }
              }
          } else if !job.stage.terminal && job.stage != .paused {
              Button("取消此文件") { state.documentPreparation.cancel(job.id) }.buttonStyle(GhostButtonStyle())
          }
        }
        .padding(.horizontal, Space.s2).padding(.vertical, Space.s3)
    }

    @ViewBuilder private var status: some View {
        switch job.stage {
        case .ready:
            Label("文字已就绪", systemImage: "checkmark").font(Typo.caption).foregroundStyle(Ink.ready)
        case .warning:
            Label("文字可用 · 需核对", systemImage: "exclamationmark.triangle").font(Typo.caption).foregroundStyle(Ink.caution)
        case .embedding:
            Text("文字已可用，语义增强中").font(Typo.caption).foregroundStyle(Ink.secondary)
        case .waiting:
            Text(job.documentID == nil ? "需要准备组件" : "文字可用 · 等待增强组件")
                .font(Typo.caption).foregroundStyle(Ink.caution)
        case .paused:
            Text(job.documentID == nil ? "会后继续读取" : "文字可用 · 会后继续增强")
                .font(Typo.caption).foregroundStyle(Ink.secondary)
        case .queued:
            Text("排队中").font(Typo.caption).foregroundStyle(Ink.secondary)
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
/// A soft block whose only colour is the scene's pigment.
private struct MaterialsInsight: View {
    @Environment(AppState.self) private var state
    @State private var preview = PreparationPreview.shared

    private var tint: Color { (state.meetingDraft?.mode ?? .general).theme.tint }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack(spacing: Space.s1) {
                Image(systemName: "sparkle").font(.system(size: 11, weight: .medium))
                Text("讨论准备 · AI 可选")
            }
            .overline(tint)
            if showsAnswer {
                let text = preview.text
                if text.isEmpty && preview.isWorking {
                    HStack(spacing: Space.s2) {
                        ProgressView().controlSize(.small)
                        Text("正在读资料…").font(Typo.body).foregroundStyle(Ink.secondary)
                    }
                } else if !text.isEmpty {
                    MarkdownText(text: text, size: 14, lineSpacing: 5).equatable()
                }
                if let error = preview.error {
                    Text(verbatim: error).font(Typo.caption).foregroundStyle(Ink.caution)
                    Button("重试") { state.readDraftMaterials() }.buttonStyle(SecondaryButtonStyle())
                }
                if preview.isWorking {
                    Button("取消生成") { preview.cancel() }.buttonStyle(GhostButtonStyle())
                }
            } else {
                Text("用本场选择的资料整理讨论重点、证据和值得追问的问题。无需 AI 也能开始记录。")
                    .font(Typo.body).foregroundStyle(Ink.secondary)
                    .fixedSize(horizontal: false, vertical: true)
                if state.answerEngineReady {
                    Button("整理讨论重点") { state.readDraftMaterials() }.buttonStyle(SecondaryButtonStyle())
                } else {
                    Button("设置 AI 服务（可选）") { state.openSettings(.answers) }.buttonStyle(GhostButtonStyle())
                }
            }
        }
        .padding(Space.s4)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Ink.aiSoft, in: RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
        .overlay(alignment: .leading) {
            Capsule().fill(tint.opacity(0.7)).frame(width: 2).padding(.vertical, Space.s3)
        }
        .animation(Motion.expand, value: showsAnswer)
        .transition(.opacity.combined(with: .offset(y: -6)))
    }

    private var showsAnswer: Bool {
        guard let series = state.draftSeries, let plan = state.draftMeetingPlan else { return false }
        return preview.isCurrent(series: series, plan: plan)
    }
}

// MARK: - Before you start

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
            divider
        }
    }

    private var divider: some View {
        Rectangle().fill(Ink.hairline).frame(height: 0.5)
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
        .padding(.horizontal, Space.s2)
        .frame(minHeight: 48)
    }
}

private struct AudioCheckRow: View {
    @Environment(AppState.self) private var state

    var body: some View {
        CheckRow(tone: state.prepareAudioTone, label: "声音") {
            VStack(alignment: .leading, spacing: 6) {
                HStack(spacing: Space.s3) {
                    if state.meetingVenue == .online {
                        AudioSourceMenu()
                    }
                    Label(state.isOfflineMeeting ? "麦克风 · 现场" : state.microphoneEnabled ? "麦克风 · 我" : "麦克风未开启", systemImage: "mic")
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
                // The model download's own status, including why it failed.
                if state.needsRecognitionModelDownload, !state.modelDownloadStatus.isEmpty,
                   state.isDownloadingModel || state.modelDownloadStatus.hasPrefix("下载失败") {
                    Text(verbatim: state.modelDownloadStatus)
                        .font(Typo.caption)
                        .foregroundStyle(state.modelDownloadStatus.hasPrefix("下载失败") ? Ink.caution : Ink.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                        .accessibilityIdentifier("meeting.prepare.modelStatus")
                }
                if state.canMeetWithoutMicrophone, state.microphoneAccess != .granted {
                    microphoneChoice
                } else if state.meetingVenue == .online, !state.microphoneEnabled {
                    HStack(spacing: Space.s2) {
                        Text("这场只记录对方的声音。想连自己的话一起记，可以重新打开麦克风。")
                            .font(Typo.caption).foregroundStyle(Ink.secondary)
                            .fixedSize(horizontal: false, vertical: true)
                        Button("重新打开麦克风") { state.useMicrophoneAgain() }
                            .buttonStyle(GhostButtonStyle(height: 24, horizontalPadding: 8))
                    }
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

    /// The way out of a denied or unanswered microphone prompt: record only the other side.
    private var microphoneChoice: some View {
        HStack(spacing: Space.s2) {
            Button("只记录对方（不用麦克风）") { state.continueWithoutMicrophone() }
                .buttonStyle(GhostButtonStyle(height: 26, horizontalPadding: 8))
                .help("这场不识别你说的话，只记录电脑里播放的声音。以后可以在这里或 设置 → 高级 → 声音与识别 重新打开麦克风。")
            Text("以后可在设置 → 高级 → 声音与识别 重新打开")
                .font(Typo.caption).foregroundStyle(Ink.tertiary)
        }
    }

    /// What blocks the audio, in words (nil when it is fine).
    private var problem: String? {
        state.recordingReadiness.issues.isEmpty ? nil : state.recordingReadinessMessage
    }

    @ViewBuilder private var action: some View {
        if state.isDownloadingModel {
            Text(verbatim: "\(Int(state.modelDownloadProgress * 100))%").font(Typo.caption).foregroundStyle(Ink.secondary).monospacedDigit()
        } else if !state.recordingReadiness.issues.isEmpty {
            Button { state.repairRecordingSetup() } label: { Text(verbatim: state.recordingRepairTitle) }
                .buttonStyle(GhostButtonStyle(height: 28))
        }
    }
}

/// 线上 / 线下: a quiet switch beside the title.
private struct VenuePicker: View {
    @Environment(AppState.self) private var state
    var body: some View {
        InkSegmented(items: MeetingVenue.allCases.map { (value: $0, title: $0.title) },
                     selection: Binding(get: { state.meetingVenue }, set: { state.setMeetingVenue($0) }),
                     height: 30)
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
        CheckRow(tone: state.prepareAITone, label: "AI 可选") {
            VStack(alignment: .leading, spacing: Space.s1) {
              HStack(spacing: Space.s2) {
                if state.answerEngineReady {
                    Text(verbatim: engineLine).font(Typo.label).lineLimit(1)
                    if state.activeServiceTier == "fast" {
                        Label("快速", systemImage: "bolt").font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                } else {
                    Text("未配置 · 不影响记录").font(Typo.label).foregroundStyle(Ink.secondary)
                }
                Spacer(minLength: 0)
                Button(state.answerEngineReady ? "更换" : "去连接") { state.openSettings(.answers) }
                    .buttonStyle(GhostButtonStyle(height: 28))
              }
              if state.answerEngineReady {
                  Text(verificationText).font(Typo.caption)
                      .foregroundStyle(state.prepareAITone == .caution ? Ink.caution : Ink.secondary)
              }
            }
            .padding(.vertical, Space.s2)
        }
    }

    private var engineLine: String {
        let model = state.currentAnswerModel
        return state.answerBackend == .openAICompatible ? "API · \(model)" : "Codex · \(model)"
    }
    private var verificationText: LocalizedStringKey {
        switch state.engineTest {
        case .idle: "已配置 · 尚未检查连接"
        case .running: "正在检查 AI 服务…"
        case .passed: "连接检查通过"
        case .failed: "连接检查未通过 · 记录仍可继续"
        }
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

private extension AppState {
    var prepareAudioTone: StatusDot.Tone {
        if recordingReadiness.canStartRecording { return .ready }
        return recordingReadiness.canRequestStart ? .idle : .caution
    }

    var prepareAITone: StatusDot.Tone {
        switch engineTest {
        case .passed where answerEngineReady: .ready
        case .failed where answerEngineReady: .caution
        default: .idle
        }
    }
}

// MARK: - Start bar

/// Flat bar pinned to the window bottom: small readiness items on the left, the
/// secondary action and the one primary action on the right.
private struct PreparationStartBar: View {
    @Environment(AppState.self) private var state

    var body: some View {
        HStack(spacing: Space.s4) {
            VStack(alignment: .leading, spacing: Space.s1) {
                HStack(spacing: Space.s4) {
                    statusItem(state.prepareAudioTone, "声音")
                    statusItem(state.prepareAITone, "AI 可选")
                    if state.companionEnabled {
                        statusItem(state.companionConnected ? .ready : .caution, "手机")
                    }
                }
                detail
            }
            Spacer(minLength: Space.s2)
            Button("取消") { state.cancelDraft() }
                .buttonStyle(GhostButtonStyle())
            if state.canMeetWithoutMicrophone, state.microphoneAccess == .denied {
                Button("只记录对方") { state.continueWithoutMicrophone() }
                    .buttonStyle(GhostButtonStyle())
                    .help("不用麦克风，只记录电脑里播放的声音。以后可在设置 → 高级 → 声音与识别 重新打开。")
                    .accessibilityLabel("只记录对方（不用麦克风）")
            }
            StartButton()
        }
        .padding(.horizontal, Space.s8).padding(.vertical, Space.s3)
        .background(Ink.paper)
        .overlay(alignment: .top) { Rectangle().fill(Ink.hairline).frame(height: 0.5) }
        .accessibilityIdentifier("meeting.prepare.startBar")
    }

    private func statusItem(_ tone: StatusDot.Tone, _ label: LocalizedStringKey) -> some View {
        HStack(spacing: 6) {
            StatusDot(tone: tone, size: 6)
            Text(label).font(Typo.caption).foregroundStyle(Ink.ink)
        }
        .animation(Motion.hover, value: tone)
    }

    private var detail: some View {
        let ready = state.draftAvailableDocumentCount
        let deferred = state.draftDeferredDocumentCount
        let message = Text(verbatim: state.recordingReadinessMessage)
            .foregroundColor(state.recordingReadiness.canRequestStart ? Ink.secondary : Ink.caution)
        let materials: Text
        if deferred > 0 {
            materials = Text("本场可用 \(ready) 份资料；其余 \(deferred) 份会后继续，本场不参与回答。")
        } else if ready > 0 {
            materials = Text("本场使用 \(ready) 份已就绪资料；文字可用即可开始。")
        } else {
            materials = Text("资料与 AI 都可稍后补充。")
        }
        return (message + Text(verbatim: "  ·  ").foregroundColor(Ink.tertiary) + materials.foregroundColor(Ink.secondary))
            .font(Typo.caption)
            .fixedSize(horizontal: false, vertical: true)
    }
}

private struct StartButton: View {
    @Environment(AppState.self) private var state

    var body: some View {
        Button {
            if state.recordingReadiness.canRequestStart { Task { await state.startPreparedMeeting() } }
            else { state.repairRecordingSetup() }
        } label: {
            HStack(spacing: 7) {
                if state.isChangingSession {
                    ProgressView().controlSize(.small)
                    Text("正在启动…")
                } else if !state.recordingReadiness.canRequestStart {
                    Text(verbatim: state.recordingRepairTitle)
                } else if !state.recordingReadiness.canStartRecording {
                    Circle().fill(Ink.seal).frame(width: 7, height: 7)
                    Text("允许并开始")
                } else {
                    Circle().fill(Ink.seal).frame(width: 7, height: 7)
                    Text("开始")
                }
            }
            .frame(minWidth: 72)
        }
        .buttonStyle(PrimaryButtonStyle(height: Space.controlLarge))
        .disabled(state.isChangingSession)
        .accessibilityIdentifier("meeting.prepare.start")
    }
}
