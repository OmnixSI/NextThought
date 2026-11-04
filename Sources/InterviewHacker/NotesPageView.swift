import AppKit
import SwiftUI
import InterviewHackerCore

private enum MeetingTab: Hashable { case notes, transcript, answers }

/// One meeting in 所有会议, after it ended: the notes (纪要 / 复盘) written from the
/// transcript, the corrected transcript and the answers given during the meeting.
struct MeetingPage: View {
    @Environment(AppState.self) private var state
    let id: UUID
    var showsBack: Bool
    var onBack: () -> Void
    let onShowInfo: (UUID) -> Void
    let onRename: (UUID) -> Void
    let onDelete: (UUID) -> Void
    let onNavigateToAllMeetings: () -> Void
    let onNavigateToSeries: (UUID) -> Void
    @State private var detail: SessionDetail?
    @State private var tab: MeetingTab = .notes
    @State private var jumpLine: Int?
    @State private var copied = false

    private var archive: SessionArchive { SessionArchive.shared }
    private var meta: SessionMeta? { archive.sessions.first { $0.id == id } }

    var body: some View {
        VStack(spacing: 0) {
            header
            ScrollViewReader { proxy in
                ScrollView {
                    VStack(alignment: .leading, spacing: Space.s6) {
                        titleBlock
                        NotesProgressLine(id: id)
                        if let detail {
                            InkSegmented(items: tabs(detail), selection: $tab).fixedSize()
                            switch tab {
                            case .notes:
                                NotesBody(detail: detail) { seconds in jump(to: seconds, in: detail) }
                            case .transcript:
                                CorrectedTranscript(detail: detail, highlighted: jumpLine)
                            case .answers:
                                SessionAnswersList(detail: detail)
                            }
                        } else {
                            ProgressView().frame(maxWidth: .infinity)
                        }
                    }
                    .frame(maxWidth: Space.content, alignment: .leading)
                    .padding(.horizontal, Space.s8)
                    .padding(.top, Space.s8)
                    .padding(.bottom, Space.s12)
                    .frame(maxWidth: .infinity)
                }
                .onChange(of: jumpLine) { _, line in
                    guard let line else { return }
                    // Wait for the transcript tab to be laid out.
                    DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) {
                        withAnimation(Motion.expand) { proxy.scrollTo("line-\(line)", anchor: .center) }
                    }
                }
            }
        }
        .task(id: reloadKey) {
            await archive.flush()
            detail = await archive.detail(id)
        }
    }

    /// Reload when the notes finish, change, or the live meeting adds lines.
    private var reloadKey: String {
        guard let meta else { return id.uuidString }
        return "\(id)-\(meta.summaryState.rawValue)-\(meta.transcriptCount / 10)-\(meta.answerCount)-\(meta.title ?? "")-\(archive.notesRevision)"
    }

    private func tabs(_ detail: SessionDetail) -> [(value: MeetingTab, title: LocalizedStringKey)] {
        var items: [(value: MeetingTab, title: LocalizedStringKey)] = [(.notes, isReview ? "复盘" : "纪要"), (.transcript, "转写")]
        if !detail.answers.isEmpty { items.append((.answers, "会中回答")) }
        return items
    }

    private var isReview: Bool {
        detail?.notes?.sections.contains { $0.kind == .questions } == true
    }

    private func jump(to seconds: Int, in detail: SessionDetail) {
        let start = detail.meta.startedAt
        let nearest = detail.lines.indices.min { lhs, rhs in
            abs(Int(detail.lines[lhs].date.timeIntervalSince(start)) - seconds) < abs(Int(detail.lines[rhs].date.timeIntervalSince(start)) - seconds)
        }
        tab = .transcript
        jumpLine = nil
        jumpLine = nearest
    }

    // MARK: Header

    private var header: some View {
        HStack(spacing: Space.s2) {
            if showsBack { navigationControls }
            breadcrumbs
            pageActions
        }
        .frame(maxWidth: Space.content, alignment: .leading)
        .padding(.horizontal, Space.s8)
        .frame(maxWidth: .infinity)
        .frame(height: 52)
        .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 1) }
    }

    private var navigationControls: some View {
        HStack(spacing: Space.s1) {
            if showsBack {
                Button { onBack() } label: { Image(systemName: "chevron.left") }
                    .buttonStyle(GlassIconButtonStyle(size: 28))
                    .accessibilityLabel("返回会议列表")
                    .help("返回会议列表")
            }
        }
        .fixedSize(horizontal: true, vertical: false)
    }

    private var breadcrumbs: some View {
        HStack(spacing: Space.s1) {
            Button("所有会议", action: onNavigateToAllMeetings)
                .buttonStyle(.plain).font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize()
                .help("查看所有会议")
            if let seriesID, let seriesName {
                Image(systemName: "chevron.right").font(.system(size: 9)).foregroundStyle(Ink.tertiary)
                Button { onNavigateToSeries(seriesID) } label: {
                    Text(verbatim: seriesName).font(Typo.caption).foregroundStyle(Ink.secondary)
                        .lineLimit(1).truncationMode(.middle)
                }
                .buttonStyle(.plain)
                .help("查看“\(seriesName)”系列中的会议")
            }
            Image(systemName: "chevron.right").font(.system(size: 9)).foregroundStyle(Ink.tertiary)
            Text(currentPageTitle).font(.system(size: 12, weight: .medium))
                .lineLimit(1).fixedSize().help(currentPageTitle)
        }
        .lineLimit(1)
    }

    private var pageActions: some View {
        ViewThatFits(in: .horizontal) {
            pageActionRow(compactTemplate: false)
            pageActionRow(compactTemplate: true)
        }
        .layoutPriority(1)
    }

    private func pageActionRow(compactTemplate: Bool) -> some View {
        HStack(spacing: Space.s2) {
            if tab != .notes {
                Text("纪要操作")
                    .font(Typo.caption)
                    .foregroundStyle(Ink.secondary)
                    .help("模板和复制只作用于纪要，不会更改转写或会中回答")
            }
            TemplateMenu(id: id, detail: detail, compactLabel: compactTemplate)
            MeetingMoreMenu(sessionID: id, onShowInfo: onShowInfo, onRename: onRename, onDelete: onDelete)
            primaryAction
        }
        .fixedSize(horizontal: true, vertical: false)
    }

    private var seriesID: UUID? { meta?.workspaceID }

    private var seriesName: String? {
        guard let meta else { return nil }
        if meta.workspaceDeleted == true { return String(localized: "未分类会议") }
        if let seriesID {
            return state.workspaces.workspace(seriesID)?.name ?? String(localized: "未分类会议")
        }
        if let name = meta.workspaceName, state.workspaces.workspaces.contains(where: { $0.name == name }) {
            return name
        }
        return String(localized: "未分类会议")
    }

    private var currentPageTitle: String {
        switch tab {
        case .notes: isReview ? String(localized: "复盘") : String(localized: "纪要")
        case .transcript: String(localized: "转写")
        case .answers: String(localized: "会中回答")
        }
    }

    @ViewBuilder private var primaryAction: some View {
        if let detail, let summary = detail.summary, isReview, summary.appliedToWorkspace != true,
           let bank = summary.bank, !bank.isEmpty, detail.meta.workspaceID != nil {
            Button("存进题库 · \(bank.count) 题") {
                state.applyReview(summary, sessionID: id, workspaceID: detail.meta.workspaceID)
            }
            .buttonStyle(PrimaryButtonStyle())
            .help("待改进的题和更好的答法存进这个系列的题库，下一场自动带入")
        } else {
            Button {
                if let detail { state.copyNotes(detail) }
                copied = true
                Task { try? await Task.sleep(for: .seconds(2)); copied = false }
            } label: {
                Label(copied ? "已复制" : "复制纪要", systemImage: copied ? "checkmark" : "doc.on.doc")
            }
            .buttonStyle(PrimaryButtonStyle())
            .disabled(detail == nil)
            .accessibilityHint("复制会后纪要，不包含转写内容")
        }
    }

    // MARK: Title

    private var titleBlock: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            Text(verbatim: detail?.summary?.title ?? meta?.displayTitle ?? "")
                .font(Typo.display)
                .textSelection(.enabled)
            if let meta {
                Text(verbatim: state.notesFacts(meta, notes: detail?.notes))
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
            }
        }
    }
}

// MARK: - Progress

/// "正在整理纪要 · 通常 1–3 分钟" with the three steps; or the failure and 重试.
private struct NotesProgressLine: View {
    @Environment(AppState.self) private var state
    let id: UUID
    private var archive: SessionArchive { SessionArchive.shared }

    var body: some View {
        let meta = archive.sessions.first { $0.id == id }
        if id == archive.activeID {
            Text("会议进行中，结束后自动整理纪要。").font(Typo.caption).foregroundStyle(Ink.secondary)
        } else if archive.summarizing.contains(id) {
            VStack(alignment: .leading, spacing: Space.s2) {
                HStack(spacing: Space.s2) {
                    ProgressView().controlSize(.small)
                    Text("正在整理纪要 · 通常 1–3 分钟").font(Typo.body)
                }
                HStack(spacing: Space.s4) {
                    let current = archive.notesSteps[id]?.rawValue ?? 0
                    ForEach(NotesStep.allCases) { step in
                        HStack(spacing: Space.s1) {
                            Image(systemName: step.rawValue < current ? "checkmark" : (step.rawValue == current ? "circle.dotted" : "circle"))
                                .font(.system(size: 10))
                            Text(step.title)
                        }
                        .font(Typo.caption)
                        .foregroundStyle(step.rawValue <= current ? Ink.ink : Ink.secondary)
                    }
                }
            }
            .paperCard()
        } else if meta?.summaryState == .failed || meta?.summaryState == .pending {
            HStack(spacing: Space.s2) {
                Image(systemName: "exclamationmark.triangle").foregroundStyle(Ink.caution)
                Text(verbatim: meta?.summaryError ?? String(localized: "纪要整理已中断 · 可以重试")).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(3)
                Spacer(minLength: Space.s2)
                Button("重试") { state.generateSummary(for: id) }.buttonStyle(SecondaryButtonStyle())
            }
            .paperCard()
        }
    }
}

/// "模板：工作会议 ⌄": re-writes the notes with another template.
private struct TemplateMenu: View {
    @Environment(AppState.self) private var state
    let id: UUID
    let detail: SessionDetail?
    var compactLabel = false
    private var archive: SessionArchive { SessionArchive.shared }

    var body: some View {
        let english = detail?.notes.map { $0.language == "en" } ?? state.notesUseEnglish
        let currentID = detail?.notes?.templateID
        let currentName = detail?.notes?.templateName ?? currentTemplateFallback
        Menu {
            ForEach(state.allNotesTemplates(english: english)) { template in
                Button {
                    state.generateSummary(for: id, templateID: template.id, english: english)
                } label: {
                    if template.id == currentID { Label(template.name, systemImage: "checkmark") } else { Text(verbatim: template.name) }
                }
            }
        } label: {
            Group {
                if compactLabel {
                    HStack(spacing: Space.s1) {
                        Image(systemName: "doc.plaintext").font(.system(size: 11))
                        Image(systemName: "chevron.down").font(.system(size: 9))
                    }
                } else {
                    HStack(spacing: Space.s1) {
                        Image(systemName: "doc.plaintext").font(.system(size: 11))
                        Text("模板：\(currentName)")
                            .lineLimit(1).truncationMode(.middle)
                        Image(systemName: "chevron.down").font(.system(size: 9))
                    }
                }
            }
            .font(Typo.caption)
            .foregroundStyle(Ink.secondary)
        }
        .menuStyle(.button)
        .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 8))
        .menuIndicator(.hidden)
        .fixedSize()
        .disabled(archive.summarizing.contains(id) || id == archive.activeID || (detail?.lines.isEmpty ?? true))
        .accessibilityLabel("纪要模板：\(currentName)")
        .accessibilityHint("更换模板并重新整理会议纪要，不会更改转写或会中回答")
        .help("只影响会后纪要，不更改转写或会中回答 · 当前模板：\(currentName)")
    }

    private var currentTemplateFallback: String {
        guard let meta = archive.sessions.first(where: { $0.id == id }) else { return "" }
        let mode = meta.workspaceID.flatMap { state.workspaces.workspace($0)?.effectiveMode } ?? meta.mode
        return state.notesTemplate(id: state.defaultNotesTemplateID(for: mode)).name
    }
}

// MARK: - Notes

private struct NotesBody: View {
    @Environment(AppState.self) private var state
    let detail: SessionDetail
    let onJump: (Int) -> Void
    private var archive: SessionArchive { SessionArchive.shared }

    var body: some View {
        if let notes = detail.notes {
            VStack(alignment: .leading, spacing: Space.s10) {
                ForEach(notes.sections.filter { !$0.items.isEmpty }) { section in
                    NotesSectionView(section: section, onJump: onJump)
                }
                if notes.sections.allSatisfy({ $0.items.isEmpty }) {
                    Text("这场会议没有整理出内容。").font(Typo.body).foregroundStyle(Ink.secondary)
                }
                footer(notes)
            }
        } else if let summary = detail.summary {
            // Notes written before the structured notes: the Markdown summary.
            MarkdownText(text: Self.body(of: summary.markdown), size: 14, lineSpacing: 6)
        } else if !archive.summarizing.contains(detail.meta.id), detail.meta.id != archive.activeID,
                  detail.meta.summaryState == .none || detail.meta.summaryState == .ready {
            VStack(alignment: .leading, spacing: Space.s3) {
                Text("还没有纪要。").font(Typo.body)
                Button("整理纪要") { state.generateSummary(for: detail.meta.id) }
                    .buttonStyle(SecondaryButtonStyle())
                    .disabled(detail.lines.isEmpty)
            }
        }
    }

    private func footer(_ notes: StructuredNotes) -> some View {
        var parts = [String(localized: "由 \(notes.model) 整理")]
        if notes.droppedCount > 0 { parts.append(String(localized: "去掉了 \(notes.droppedCount) 条没有原话依据的内容")) }
        return Text(verbatim: parts.joined(separator: " · ")).font(Typo.caption).foregroundStyle(Ink.secondary)
    }

    /// Drops the "# 标题" block, which is already the page title.
    static func body(of markdown: String) -> String {
        guard let range = markdown.range(of: "## ") else { return markdown }
        return String(markdown[range.lowerBound...])
    }
}

private struct NotesSectionView: View {
    let section: NotesSectionResult
    let onJump: (Int) -> Void

    var body: some View {
        switch section.kind {
        case .oneLine:
            VStack(alignment: .leading, spacing: Space.s2) {
                AIHeadingText(text: section.title)
                ForEach(section.items) { item in
                    HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                        Text(verbatim: item.text).font(Typo.body).lineSpacing(6).textSelection(.enabled)
                        if item.uncertain { ConfirmBadge() }
                    }
                }
            }
            .aiBlock(padding: Space.s6)
        case .items:
            VStack(alignment: .leading, spacing: Space.s3) {
                Text(verbatim: section.title).font(Typo.section)
                ForEach(section.items) { item in
                    HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                        Text(verbatim: item.text).font(Typo.body).lineSpacing(6).textSelection(.enabled)
                        TimeLink(time: item.time, onJump: onJump)
                        if item.uncertain { ConfirmBadge() }
                    }
                }
            }
        case .actions:
            VStack(alignment: .leading, spacing: 0) {
                Text(verbatim: section.title).font(Typo.section).padding(.bottom, Space.s3)
                ForEach(section.items) { item in
                    Rectangle().fill(Ink.hairline).frame(height: 1)
                    HStack(alignment: .firstTextBaseline, spacing: Space.s3) {
                        HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                            Text(verbatim: item.text).font(Typo.body).textSelection(.enabled)
                            TimeLink(time: item.time, onJump: onJump)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                        Text(verbatim: item.owner ?? "—").font(Typo.body).foregroundStyle(Ink.secondary)
                            .frame(width: 72, alignment: .leading)
                        HStack(spacing: Space.s2) {
                            Text(verbatim: item.due ?? "—").font(Typo.body).foregroundStyle(Ink.secondary)
                            if item.uncertain { ConfirmBadge() }
                        }
                        .frame(width: 190, alignment: .leading)
                    }
                    .padding(.vertical, Space.s3)
                }
            }
        case .questions:
            VStack(alignment: .leading, spacing: 0) {
                Text(verbatim: section.title).font(Typo.section).padding(.bottom, Space.s3)
                ForEach(Array(section.items.enumerated()), id: \.element.id) { index, item in
                    Rectangle().fill(Ink.hairline).frame(height: 1)
                    HStack(alignment: .firstTextBaseline, spacing: Space.s3) {
                        Text(verbatim: "\(index + 1)").font(.system(size: 13, design: .serif)).foregroundStyle(Ink.tertiary)
                            .frame(width: 24, alignment: .leading)
                        VStack(alignment: .leading, spacing: Space.s2) {
                            HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                                Text(verbatim: item.text).font(.system(size: 14, weight: .semibold)).textSelection(.enabled)
                                TimeLink(time: item.time, onJump: onJump)
                                VerdictBadge(item: item)
                                if item.uncertain { ConfirmBadge() }
                            }
                            if let answer = item.answer {
                                Text("我答：\(answer)").font(Typo.caption).foregroundStyle(Ink.secondary).textSelection(.enabled)
                            }
                            if item.improves, let better = item.better {
                                VStack(alignment: .leading, spacing: Space.s1) {
                                    AIHeadingText(text: String(localized: "更好的答法"))
                                    Text(verbatim: better).font(Typo.body).lineSpacing(5).textSelection(.enabled)
                                }
                                .aiBlock(padding: Space.s3)
                            }
                        }
                    }
                    .padding(.vertical, Space.s4)
                }
            }
        }
    }
}

/// An AI heading whose text is already localized (template section names).
private struct AIHeadingText: View {
    let text: String
    var body: some View {
        HStack(spacing: Space.s1) {
            Image(systemName: "sparkle").font(.system(size: 11, weight: .medium))
            Text(verbatim: text)
        }
        .overline(Ink.ai)
    }
}

/// "[12:41]": jumps to that moment in the transcript.
private struct TimeLink: View {
    let time: String?
    let onJump: (Int) -> Void
    var body: some View {
        if let time, let seconds = NotesTime.seconds(time) {
            Button { onJump(seconds) } label: {
                Text(verbatim: "[\(time)]").font(.system(size: 12).monospacedDigit()).foregroundStyle(Ink.secondary)
            }
            .buttonStyle(.plain)
            .help("跳到转写里的这句话")
        }
    }
}

private struct ConfirmBadge: View {
    var body: some View {
        Text("待确认")
            .font(.system(size: 11, weight: .semibold))
            .padding(.horizontal, 6).padding(.vertical, 1)
            .background(Ink.caution.opacity(0.18), in: RoundedRectangle(cornerRadius: 4, style: .continuous))
    }
}

private struct VerdictBadge: View {
    let item: NotesItem
    var body: some View {
        if item.improves {
            badge("待改进", color: Ink.caution)
        } else if item.verdict?.lowercased() == "good" {
            badge("答得好", color: Ink.ready)
        }
    }

    private func badge(_ title: LocalizedStringKey, color: Color) -> some View {
        Text(title)
            .font(.system(size: 11, weight: .semibold))
            .padding(.horizontal, 6).padding(.vertical, 1)
            .background(color.opacity(0.18), in: RoundedRectangle(cornerRadius: 4, style: .continuous))
    }
}

// MARK: - Transcript

/// The transcript with the proofreading corrections underlined (dotted); hover shows
/// what was recognized, and a right click undoes a correction.
private struct CorrectedTranscript: View {
    @Environment(AppState.self) private var state
    let detail: SessionDetail
    let highlighted: Int?
    /// Find in the transcript; recorded misheard spellings (术语和人名) match too.
    @State private var find = ""
    @State private var findCache = GlossarySearchCache()
    @State private var glossaryDraft: GlossaryDraft?

    var body: some View {
        let corrections = detail.notes?.corrections ?? []
        let byLine = Dictionary(grouping: corrections, by: \.line)
        let terms: GlossarySearch? = find.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? nil : findCache.terms(for: find, state: state)
        let shown = Array(detail.lines.enumerated()).filter { terms == nil || terms?.appMatch(in: $0.element.text) != nil }
        LazyVStack(alignment: .leading, spacing: Space.s3) {
            if detail.lines.isEmpty {
                Text("这场会议没有转写。").font(Typo.body).foregroundStyle(Ink.secondary)
            } else {
                TranscriptFindField(query: $find, matches: terms == nil ? nil : shown.count)
            }
            ForEach(shown, id: \.element.id) { index, line in
                let fixes = byLine[index] ?? []
                HStack(alignment: .firstTextBaseline, spacing: Space.s3) {
                    Text(verbatim: NotesTime.text(Int(max(0, line.date.timeIntervalSince(detail.meta.startedAt)))))
                        .font(.system(size: 11).monospacedDigit()).foregroundStyle(Ink.tertiary)
                        .frame(width: 52, alignment: .leading)
                    Text(line.speaker.displayName)
                        .font(.system(size: 12, weight: line.speaker == .me ? .semibold : .regular))
                        .foregroundStyle(line.speaker == .me ? Ink.ink : Ink.secondary)
                        .frame(width: 40, alignment: .leading)
                    VStack(alignment: .leading, spacing: Space.s1) {
                        Text(Self.text(line.text, fixes: fixes))
                            .font(Typo.body).lineSpacing(5)
                            .textSelection(.enabled)
                            .help(Self.originals(fixes))
                        if let terms, let hit = terms.appMatch(in: line.text), hit.isMisheard {
                            MisheardMark(form: hit.form)
                        }
                        if let translation = line.translatedText, !translation.isEmpty {
                            Text("译文：\(translation)").font(Typo.caption).foregroundStyle(Ink.ai).lineSpacing(4)
                                .textSelection(.enabled)
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                .padding(.vertical, 2)
                .background(highlighted == index ? Ink.hover : Color.clear, in: RoundedRectangle(cornerRadius: 4, style: .continuous))
                .id("line-\(index)")
                .contextMenu {
                    ForEach(fixes) { fix in
                        Button(fix.reverted ? "恢复校对：\(fix.original) → \(fix.corrected)" : "撤销校对：\(fix.corrected) → \(fix.original)") {
                            state.toggleCorrection(fix.id, in: detail.meta.id)
                        }
                    }
                    Button("加入术语表…") { glossaryDraft = GlossaryDraft(line: line.text) }
                }
            }
        }
        .sheet(item: $glossaryDraft) { draft in
            AddToGlossarySheet(line: draft.line).environment(state)
        }
    }

    /// The line with active corrections applied and underlined with dots.
    static func text(_ original: String, fixes: [TranscriptCorrection]) -> AttributedString {
        var corrected = original
        for fix in fixes where !fix.reverted {
            corrected = corrected.replacingOccurrences(of: fix.original, with: fix.corrected)
        }
        var attributed = AttributedString(corrected)
        for fix in fixes where !fix.reverted {
            if let range = attributed.range(of: fix.corrected) {
                attributed[range][AttributeScopes.SwiftUIAttributes.UnderlineStyleAttribute.self] = Text.LineStyle(pattern: .dot)
            }
        }
        return attributed
    }

    static func originals(_ fixes: [TranscriptCorrection]) -> String {
        let active = fixes.filter { !$0.reverted }
        guard !active.isEmpty else { return "" }
        return String(localized: "原来识别为：\(active.map(\.original).joined(separator: "、"))（右键可以撤销）")
    }
}
