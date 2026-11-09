import AppKit
import SwiftUI
import NextThoughtCore

private enum MeetingTab: Hashable { case notes, transcript, answers, personalNotes }
private enum TranscriptVersion: Hashable { case corrected, original }

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
    var initialTarget: SessionContentTarget? = nil
    @State private var detail: SessionDetail?
    @State private var projection: SessionTextProjection?
    @State private var evidenceReferences: [SessionEvidenceReference] = []
    @State private var selectedEvidenceID: String?
    @State private var editingEvidence: SessionEvidenceReference?
    @State private var tab: MeetingTab = .notes
    @State private var transcriptVersion: TranscriptVersion = .corrected
    /// Which view of the notes is shown: the text notes (always the one a meeting opens with)
    /// or a fishbone, outline or viewpoint matrix the user picked. Not saved.
    @State private var format: NotesFormat = .standard
    @State private var jumpLineID: UUID?
    @State private var navigationNotice: String?
    @State private var copied = false
    @State private var evidenceLineID: UUID?
    @State private var evidenceUnavailable = false
    @State private var editingNotes = false
    @State private var confirmsUseAI = false
    @State private var routedTarget: SessionContentTarget?
    @State private var scrollTarget: String?

    private var archive: SessionArchive { SessionArchive.shared }
    private var meta: SessionMeta? { archive.sessions.first { $0.id == id } }

    var body: some View {
        VStack(spacing: 0) {
            header
            ScrollViewReader { proxy in
                ScrollView {
                    VStack(alignment: .leading, spacing: Space.s6) {
                        titleBlock
                        archiveStatus
                        NotesProgressLine(id: id)
                        if let sourceNotice = sourceFreshnessNotice {
                            Label(sourceNotice.text, systemImage: sourceNotice.isWarning ? "exclamationmark.triangle.fill" : "questionmark.circle")
                                .font(Typo.caption)
                                .foregroundStyle(sourceNotice.isWarning ? Ink.caution : Ink.secondary)
                                .fixedSize(horizontal: false, vertical: true)
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }
                        if let detail {
                            if let issue = detail.readIssues.first {
                                HStack(alignment: .top, spacing: Space.s2) {
                                    Image(systemName: "exclamationmark.triangle.fill")
                                    Text(issue.message).textSelection(.enabled)
                                    Spacer(minLength: 0)
                                    Button("详情") { onShowInfo(id) }.buttonStyle(SecondaryButtonStyle())
                                }
                                .font(Typo.caption)
                                .foregroundStyle(Ink.caution)
                            }
                            if let error = detail.userContentError { Text("无法读取用户修订：\(error)。原始内容仍可查看；修订文件不会被覆盖。").font(Typo.caption).foregroundStyle(Ink.caution) }
                            InkSegmented(items: tabs(detail), selection: $tab).fixedSize()
                            if let navigationNotice {
                                Text(verbatim: navigationNotice)
                                    .font(Typo.caption).foregroundStyle(Ink.caution)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                            }
                            switch tab {
                            case .notes:
                                ViewThatFits(in: .horizontal) {
                                    HStack(alignment: .top, spacing: Space.s6) {
                                        notesContent(detail).frame(minWidth: 480, maxWidth: .infinity, alignment: .leading)
                                        evidencePane(detail).frame(width: 300)
                                    }
                                    VStack(alignment: .leading, spacing: Space.s6) {
                                        notesContent(detail)
                                        evidencePane(detail)
                                    }
                                }
                                if let workspaceID = detail.meta.workspaceID {
                                    KnowledgeReviewSection(workspaceID: workspaceID, sessionID: id, summary: detail.summary)
                                }
                            case .transcript:
                                CorrectedTranscript(detail: detail, projection: projection, highlighted: jumpLineID,
                                                   version: $transcriptVersion)
                            case .answers:
                                SessionAnswersList(detail: detail)
                            case .personalNotes:
                                MeetingUserNotes(sessionID: id, highlights: detail.userContent.highlights, onOpenHighlight: { evidenceUnavailable = false; evidenceLineID = $0.lineID })
                                    .frame(minHeight: 340).id("personal-notes")
                                if evidenceLineID != nil || evidenceUnavailable { evidencePane(detail) }
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
                .onChange(of: scrollTarget) { _, target in
                    guard let target else { return }
                    DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) {
                        withAnimation(Motion.expand) { proxy.scrollTo(target, anchor: .center) }
                    }
                }
                .onChange(of: jumpLineID) { _, lineID in
                    guard let lineID else { return }
                    // Wait for the transcript tab to be laid out.
                    DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) {
                        withAnimation(Motion.expand) { proxy.scrollTo("line-\(lineID.uuidString)", anchor: .center) }
                    }
                }
            }
        }
        .onChange(of: archive.summarizing.contains(id)) { _, writing in
            if writing { format = .standard }
        }
        .onChange(of: id) { _, _ in
            format = .standard
            transcriptVersion = .corrected
            tab = .notes; detail = nil; projection = nil; evidenceLineID = nil; evidenceUnavailable = false; routedTarget = nil
        }
        .onChange(of: initialTarget) { _, target in if let detail { route(target, in: detail) } }
        .onChange(of: selectedEvidenceID) { _, _ in evidenceLineID = nil; evidenceUnavailable = false }
        .task(id: reloadKey) {
            _ = await archive.flush(sessionID: id)
            let loaded = await archive.detail(id)
            guard !Task.isCancelled else { return }
            if detail == nil, let loaded, loaded.lines.isEmpty, loaded.summary == nil, loaded.userContent.hasContent { tab = .personalNotes }
            detail = loaded
            if let loaded { archive.primeUserContent(loaded.userContent, for: id); route(initialTarget, in: loaded) }
            let loadedProjection = loaded.map { detail in
                detail.effectiveProjection()
            }
            projection = loadedProjection
            evidenceReferences = loaded.map {
                SessionEvidenceBuilder.build(meta: $0.meta, lines: $0.lines, notes: $0.effectiveNotes, summary: $0.effectiveSummary)
            } ?? []
            if !evidenceReferences.contains(where: { $0.id == selectedEvidenceID }) { selectedEvidenceID = nil }
            if selectedEvidenceID == nil { selectedEvidenceID = evidenceReferences.first?.id }
            StructuredNotesJobs.shared.acknowledge(loaded?.summary, for: id)
            if let target = state.pendingTranscriptNavigation, target.sessionID == id,
               loaded != nil, let loadedProjection {
                revealTranscriptHit(target, using: loadedProjection)
            }
        }
        .onChange(of: state.pendingTranscriptNavigation) { _, target in
            guard let target, target.sessionID == id, detail != nil, let projection else { return }
            revealTranscriptHit(target, using: projection)
        }
        .sheet(isPresented: $editingNotes) { if let detail { MeetingNotesEditor(detail: detail) } }
        .alert("使用最新 AI 原稿替换你的修订？", isPresented: $confirmsUseAI) {
            Button("保留我的修订", role: .cancel) {}
            Button("使用 AI 原稿", role: .destructive) {
                Task {
                    do { _ = try await archive.updateUserContent(for: id) { $0.editedNotes = nil; $0.editedMarkdown = nil; $0.basedOnGeneratedAt = nil } }
                    catch { state.errorMessage = "未能切换版本：\(error.localizedDescription)" }
                }
            }
        }
        .sheet(item: $editingEvidence) { reference in
            NotesItemEditSheet(title: reference.notesItemID == nil ? "编辑结构化纪要条目" : "编辑纪要条目",
                               text: reference.claim) { text, completion in
                if let itemID = reference.notesItemID {
                    archive.updateNotes(id, { notes in
                        for sectionIndex in notes.sections.indices {
                            guard let itemIndex = notes.sections[sectionIndex].items.firstIndex(where: { $0.id == itemID }) else { continue }
                            notes.sections[sectionIndex].items[itemIndex].text = text
                            return true
                        }
                        return false
                    }, completion: completion)
                } else if let target = reference.structuredTarget {
                    archive.updateSummary(id, { summary in
                        SessionEvidenceEditor.updateStructuredNote(in: &summary, target: target, text: text)
                    }, completion: completion)
                } else {
                    completion(.staleTarget)
                }
            }
        }
    }

    @ViewBuilder
    private func notesContent(_ detail: SessionDetail) -> some View {
        notesEditingActions(detail)
        if showsFormatBar(detail) {
            NotesFormatBar(id: id, firstTitle: isReview ? String(localized: "复盘") : String(localized: "纪要"),
                           recommended: recommendedFormat(detail), selection: $format,
                           onPick: { picked in pick(picked, in: detail) })
        }
        StructuredNotesPane(detail: detail, format: shownFormat(detail), onJump: { seconds in jump(to: seconds, in: detail) }) {
            NotesBody(detail: detail, selectedEvidenceID: $selectedEvidenceID) { seconds in jump(to: seconds, in: detail) }
        }
    }

    @ViewBuilder
    private func evidencePane(_ detail: SessionDetail) -> some View {
        if evidenceLineID != nil || evidenceUnavailable {
            MeetingEvidenceSidebar(detail: detail, lineID: evidenceLineID,
                                   onClose: { evidenceLineID = nil; evidenceUnavailable = false },
                                   onOpenTranscript: { index in evidenceLineID = nil; evidenceUnavailable = false; jump(to: detail.lines[index].id) })
        } else {
        SessionEvidencePane(references: evidenceReferences, selectedID: $selectedEvidenceID,
                            onJump: { lineID, variant in jump(to: lineID, variant: variant) },
                            editingEnabled: !archive.summarizing.contains(id),
                            onReview: { reference, decision in
                                guard let claimRevision = reference.claimRevision,
                                      let sourceRevision = reference.sourceRevision else { return }
                                state.reviewSessionEvidence(sessionID: id, evidenceID: reference.id,
                                                            expectedClaimRevision: claimRevision,
                                                            expectedTranscriptRevision: reference.transcriptSourceRevision,
                                                            expectedLineRevision: sourceRevision,
                                                            decision: decision)
                            },
                            onEdit: { reference in
                                editingEvidence = reference
                            })
        }
    }

    private var sourceFreshnessNotice: (text: String, isWarning: Bool)? {
        let statuses = evidenceReferences.map(\.sourceStatus)
        if projection?.summary?.sourceStatus == .stale || statuses.contains(.stale) {
            return ("部分纪要或结构化内容可能已过期；核对依据前请查看当前原话和纪要。", true)
        }
        if projection?.summary?.sourceStatus == .unknown || statuses.contains(.unknown) {
            return ("部分旧内容未记录来源版本；无法确认是否与当前原话和纪要一致。", false)
        }
        return nil
    }

    /// Reload when the notes finish, change, or the live meeting adds lines.
    private var reloadKey: String {
        guard let meta else { return id.uuidString }
        return "\(id)-\(meta.summaryState.rawValue)-\(meta.transcriptCount / 10)-\(meta.answerCount)-\(meta.title ?? "")-\(archive.notesRevision)-\(archive.searchableContentRevision)-\(archive.translationRevision(for: id))"
    }

    private var archiveStatus: some View {
        Group {
            if let snapshot = archive.persistenceBySession[id] {
                VStack(alignment: .leading, spacing: Space.s2) {
                    HStack(spacing: Space.s2) {
                        Image(systemName: snapshot.unqueuedWriteCount > 0 ? "exclamationmark.triangle.fill" : archiveStatusIcon(snapshot.state))
                        Text(archiveStatusText(snapshot))
                            .textSelection(.enabled)
                        Spacer(minLength: 0)
                        if snapshot.canAcknowledgeDataLoss {
                            Button("确认缺失并继续保存") { archive.acknowledgeDataLossAndResume(id) }
                                .buttonStyle(SecondaryButtonStyle())
                        } else if snapshot.state == .blocked, snapshot.pendingEvents > 0 {
                            Button("重试保存") { archive.retryPersistence(id) }
                                .buttonStyle(SecondaryButtonStyle())
                        }
                    }
                    if snapshot.unqueuedWriteCount > 0 {
                        Text(snapshot.dataLossAcknowledged
                             ? "已确认：有 \(snapshot.unqueuedWriteCount) 项内容未能入队，因此未保存。"
                             : "有 \(snapshot.unqueuedWriteCount) 项内容未能入队，因此未保存。确认后可继续保存新内容。")
                            .textSelection(.enabled)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
                .font(Typo.caption)
                .foregroundStyle(snapshot.unqueuedWriteCount > 0 ? Ink.caution : archiveStatusColor(snapshot.state))
                .padding(.horizontal, Space.s3)
                .padding(.vertical, Space.s2)
                .background(archiveStatusColor(snapshot.state).opacity(0.08), in: RoundedRectangle(cornerRadius: Space.radiusSmall))
            }
        }
    }

    private func archiveStatusText(_ snapshot: SessionPersistenceSnapshot) -> String {
        switch snapshot.state {
        case .saved:
            if let date = snapshot.lastPersistedAt {
                return String(localized: "归档已保存 · \(date.formatted(date: .omitted, time: .shortened))")
            }
            return String(localized: "归档已保存")
        case .saving:
            return String(localized: "正在保存会议内容 · \(snapshot.pendingEvents) 项待写入")
        case .blocked:
            return snapshot.lastFailure ?? String(localized: "会议内容保存受阻")
        }
    }

    private func archiveStatusIcon(_ state: SessionPersistenceSnapshot.State) -> String {
        switch state {
        case .saved: "checkmark.circle.fill"
        case .saving: "arrow.triangle.2.circlepath"
        case .blocked: "exclamationmark.triangle.fill"
        }
    }

    private func archiveStatusColor(_ state: SessionPersistenceSnapshot.State) -> Color {
        switch state {
        case .saved: Ink.tertiary
        case .saving: Ink.secondary
        case .blocked: Ink.caution
        }
    }

    private func tabs(_ detail: SessionDetail) -> [(value: MeetingTab, title: LocalizedStringKey)] {
        var items: [(value: MeetingTab, title: LocalizedStringKey)] = [(.notes, isReview ? "复盘" : "纪要"), (.transcript, "转写"), (.personalNotes, "我的笔记")]
        if !detail.answers.isEmpty || !detail.questions.isEmpty || !(detail.summary?.questions.isEmpty ?? true) {
            items.append((.answers, "会中回答"))
        }
        return items
    }

    private var isReview: Bool {
        detail?.effectiveNotes?.sections.contains { $0.kind == .questions } == true
    }

    // MARK: Notes formats

    /// The format bar needs finished text notes to sit beside; while they are written (or the
    /// meeting is still running) only the text notes show.
    private func showsFormatBar(_ detail: SessionDetail) -> Bool {
        detail.summary != nil && !detail.lines.isEmpty && id != archive.activeID && !archive.summarizing.contains(id)
    }

    /// The view on screen: the picked one, or the text notes when the bar is not there.
    private func shownFormat(_ detail: SessionDetail) -> NotesFormat {
        showsFormatBar(detail) ? format : .standard
    }

    /// The view that suits this kind of meeting (marked with a dot, never written by itself).
    private func recommendedFormat(_ detail: SessionDetail) -> NotesFormat? {
        let mode = detail.context?.effectiveMode ?? detail.context?.mode ?? detail.meta.mode
        let suggested = NotesFormat.recommended(for: mode)
        return suggested.isStructured ? suggested : nil
    }

    /// A click on the bar: a view that is saved shows at once, a new one is written now.
    private func pick(_ picked: NotesFormat, in detail: SessionDetail) {
        guard picked.isStructured, detail.summary?.payload(for: picked) == nil,
              !StructuredNotesJobs.shared.isRunning(StructuredJobKey(id: id, format: picked)) else { return }
        state.generateStructuredNotes(for: id, format: picked)
    }

    /// What the copy button copies: the structured view on screen, otherwise the text notes.
    private func copiedFormat(_ detail: SessionDetail) -> NotesFormat {
        let shown = shownFormat(detail)
        return tab == .notes && detail.summary?.payload(for: shown) != nil ? shown : .standard
    }

    private func copyShown(_ detail: SessionDetail) {
        if tab == .personalNotes {
            NSPasteboard.general.clearContents()
            NSPasteboard.general.setString(archive.personalNotes(for: id) + "\n\n" + detail.userContent.highlights.map { "- [\(NotesTime.text($0.offsetSeconds))] \($0.quote)" }.joined(separator: "\n"), forType: .string)
            return
        }
        if tab == .transcript {
            let options = SessionProjectionOptions(transcriptVariant: transcriptVersion == .original ? .original : .corrected, languageView: .source)
            let lines = detail.effectiveProjection(options: options).transcript
            NSPasteboard.general.clearContents()
            NSPasteboard.general.setString(lines.map { "[\(NotesTime.text($0.offset))] \($0.speakerLabel)：\($0.displayText)" }.joined(separator: "\n"), forType: .string)
            return
        }
        if tab == .answers {
            NSPasteboard.general.clearContents()
            NSPasteboard.general.setString(detail.answers.map { "## \($0.question)\n\n" + ($0.isComplete ? "" : "> 未完成的回答片段，请核实后使用。\n\n") + $0.answer }.joined(separator: "\n\n"), forType: .string)
            return
        }
        let shown = copiedFormat(detail)
        if shown.isStructured, let payload = detail.summary?.payload(for: shown) {
            NSPasteboard.general.clearContents()
            NSPasteboard.general.setString(StructuredNotesMarkdown.render(payload), forType: .string)
        } else {
            state.copyNotes(detail)
        }
    }

    private func jump(to seconds: Int, in detail: SessionDetail) {
        let located = SessionTranscriptLocator.index(offsetSeconds: seconds, startedAt: detail.meta.startedAt, lines: detail.lines)
        evidenceLineID = located.map { detail.lines[$0].id }
        evidenceUnavailable = located == nil
    }

    private func route(_ target: SessionContentTarget?, in detail: SessionDetail) {
        guard let target, target != routedTarget else { return }
        routedTarget = target
        switch target {
        case .transcript(let line): tab = .transcript; transcriptVersion = .corrected; jumpLineID = nil; DispatchQueue.main.async { jumpLineID = line }
        case .answer(let answer): tab = .answers; scrollTarget = nil; scrollTarget = "answer-\(answer)"
        case .notes(let item): tab = .notes; format = .standard; scrollTarget = item.map { "note-\($0)" }
        case .personalNotes: tab = .personalNotes
        case .highlight(let mark):
            tab = .personalNotes
            if let highlight = detail.userContent.highlights.first(where: { $0.id == mark }) { evidenceUnavailable = false; evidenceLineID = highlight.lineID }
        }
    }

    private func notesEditingActions(_ detail: SessionDetail) -> some View {
        HStack(spacing: Space.s2) {
            if detail.userContent.editedNotes != nil || detail.userContent.editedMarkdown != nil {
                Label("你的修订版", systemImage: "pencil").font(Typo.caption).foregroundStyle(Ink.secondary)
                if let base = detail.userContent.basedOnGeneratedAt, let latest = detail.notes?.generatedAt, base < latest {
                    Text("AI 原稿已有新版本").font(Typo.caption).foregroundStyle(Ink.ai)
                }
                Button("使用 AI 原稿…") { confirmsUseAI = true }.buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 8))
            }
            Spacer(minLength: Space.s2)
            Button("编辑纪要") { editingNotes = true }.buttonStyle(SecondaryButtonStyle())
                .disabled((detail.effectiveNotes == nil && detail.summary == nil) || detail.userContentError != nil || archive.summarizing.contains(id) || archive.isReadOnlyFallback(id))
        }
    }

    private func jump(to lineID: UUID, variant: SessionEvidenceSourceVariant? = nil) {
        guard detail?.lines.contains(where: { $0.id == lineID }) == true else { navigationNotice = "此原话无法定位。"; return }
        tab = .transcript
        transcriptVersion = variant == .original ? .original : .corrected
        navigationNotice = nil
        jumpLineID = nil
        DispatchQueue.main.async { jumpLineID = lineID }
    }

    private func revealTranscriptHit(_ target: SessionTranscriptNavigationTarget, using projection: SessionTextProjection) {
        let currentHit = target.hit.id.flatMap { id in projection.searchableText.first(where: { $0.id == id }) }
        let currentSourceRevision = currentHit?.sourceRevision
            ?? (target.hit.kind == .notes ? projection.transcriptSourceRevision : nil)
        let notice: String?
        if let savedRevision = target.hit.sourceRevision, let currentSourceRevision,
           savedRevision != currentSourceRevision {
            notice = "这句已更新，显示的是当前版本。"
        } else {
            notice = nil
        }
        if target.hit.kind == .notes, let hitID = target.hit.id,
           let reference = evidenceReferences.first(where: { hitID == $0.id || hitID.hasPrefix($0.id + ":") }) {
            selectedEvidenceID = reference.id
        }
        guard let lineID = target.hit.lineID else {
            jumpLineID = nil
            switch target.hit.kind {
            case .notes:
                navigationNotice = notice
                tab = .notes
            case .question, .answer:
                navigationNotice = nil
                tab = .answers
            default:
                state.pendingTranscriptNavigation = nil
                navigationNotice = "这条搜索结果已不存在，请重新搜索。"
            }
            return
        }
        guard let line = projection.transcript.first(where: { $0.id == lineID }) else {
            state.pendingTranscriptNavigation = nil
            jumpLineID = nil
            navigationNotice = "这条搜索结果已不存在，请重新搜索。"
            return
        }
        let variant: SessionEvidenceSourceVariant? = switch target.hit.kind {
        case .transcript: .original
        case .correctedTranscript: .corrected
        default: nil
        }
        jump(to: line.id, variant: variant)
        navigationNotice = notice
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
            if tab == .notes { TemplateMenu(id: id, detail: detail, compactLabel: compactTemplate) }
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
        case .personalNotes: String(localized: "我的笔记")
        }
    }

    @ViewBuilder private var primaryAction: some View {
        Button {
            if let detail { copyShown(detail) }
            copied = true
            Task { try? await Task.sleep(for: .seconds(2)); copied = false }
        } label: {
            Label(copied ? String(localized: "已复制") : copyTitle,
                  systemImage: copied ? "checkmark" : "doc.on.doc")
        }
        .buttonStyle(PrimaryButtonStyle())
        .disabled(detail == nil)
        .accessibilityHint("复制当前内容")
    }

    private var copyTitle: String {
        switch tab {
        case .notes: detail.map { copiedFormat($0).copyTitle } ?? String(localized: "复制纪要")
        case .transcript: String(localized: "复制转写")
        case .answers: String(localized: "复制会中回答")
        case .personalNotes: String(localized: "复制我的笔记")
        }
    }

    // MARK: Title

    private var titleBlock: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            Text(verbatim: meta?.displayTitle ?? detail?.meta.displayTitle ?? "")
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
    @Binding var selectedEvidenceID: String?
    let onJump: (Int) -> Void
    private var archive: SessionArchive { SessionArchive.shared }

    var body: some View {
        if let markdown = detail.userContent.editedMarkdown {
            MarkdownText(text: markdown, size: 15, lineSpacing: 6)
        } else if let notes = detail.effectiveNotes {
            VStack(alignment: .leading, spacing: Space.s10) {
                ForEach(notes.sections.filter { !$0.items.isEmpty }) { section in
                    NotesSectionView(section: section, selectedEvidenceID: $selectedEvidenceID, onJump: onJump)
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
    @Binding var selectedEvidenceID: String?
    let onJump: (Int) -> Void

    var body: some View {
        switch section.kind {
        case .oneLine:
            VStack(alignment: .leading, spacing: Space.s2) {
                AIHeadingText(text: section.title)
                ForEach(section.items) { item in
                    HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                        Text(verbatim: item.text).id("note-\(item.id)").font(Typo.body).lineSpacing(6).textSelection(.enabled)
                        evidenceButton(item)
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
                        Text(verbatim: item.text).id("note-\(item.id)").font(Typo.body).lineSpacing(6).textSelection(.enabled)
                        TimeLink(time: item.time, onJump: onJump)
                        evidenceButton(item)
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
                            Text(verbatim: item.text).id("note-\(item.id)").font(Typo.body).textSelection(.enabled)
                            TimeLink(time: item.time, onJump: onJump)
                            evidenceButton(item)
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
                                Text(verbatim: item.text).id("note-\(item.id)").font(.system(size: 14, weight: .semibold)).textSelection(.enabled)
                                TimeLink(time: item.time, onJump: onJump)
                                evidenceButton(item)
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

    private func evidenceButton(_ item: NotesItem) -> some View {
        let evidenceID = "notes:\(section.id):\(item.id.uuidString)"
        return Button {
            selectedEvidenceID = evidenceID
        } label: {
            Image(systemName: "quote.bubble")
                .font(.system(size: 11, weight: .medium))
                .foregroundStyle(Ink.secondary)
        }
        .buttonStyle(.plain)
        .help("查看这条内容的原话依据")
        .accessibilityLabel("查看“\(item.text)”的原话依据")
    }
}

private struct SessionEvidencePane: View {
    let references: [SessionEvidenceReference]
    @Binding var selectedID: String?
    let onJump: (UUID, SessionEvidenceSourceVariant?) -> Void
    let editingEnabled: Bool
    let onReview: (SessionEvidenceReference, SessionEvidenceDecision?) -> Void
    let onEdit: (SessionEvidenceReference) -> Void

    private var selected: SessionEvidenceReference? {
        references.first(where: { $0.id == selectedID })
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack(spacing: Space.s2) {
                Image(systemName: "text.quote").foregroundStyle(Ink.secondary)
                Text("原话依据").font(Typo.section)
                Spacer(minLength: 0)
            }
            Text("引文匹配只说明原话中出现过这些词，不代表结论已确认。只有时间点的内容仍需核对。")
                .font(Typo.caption).foregroundStyle(Ink.secondary)
                .fixedSize(horizontal: false, vertical: true)

            if references.isEmpty {
                Text("这份纪要没有可逐项定位的内容。原话仍可在“转写”中查看。")
                    .font(Typo.caption).foregroundStyle(Ink.tertiary)
                    .fixedSize(horizontal: false, vertical: true)
            } else {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: Space.s1) {
                        ForEach(references) { reference in
                            Button { selectedID = reference.id } label: {
                                VStack(alignment: .leading, spacing: 3) {
                                    HStack(spacing: Space.s1) {
                                        Text(verbatim: reference.title).font(.system(size: 10, weight: .semibold))
                                            .foregroundStyle(Ink.secondary).lineLimit(1)
                                        Spacer(minLength: 0)
                                        if let location = reference.location {
                                            Text(verbatim: location).font(.system(size: 10).monospacedDigit())
                                                .foregroundStyle(Ink.tertiary)
                                        }
                                    }
                                    Text(verbatim: reference.claim).font(.system(size: 11)).foregroundStyle(Ink.ink)
                                        .lineLimit(2).multilineTextAlignment(.leading)
                                    Text(supportLabel(reference)).font(.system(size: 9, weight: .medium))
                                        .foregroundStyle(reference.needsConfirmation || reference.support != .quoted ? Ink.caution : Ink.secondary)
                                }
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .padding(Space.s2)
                                .background(selectedID == reference.id ? Ink.hover : Color.clear,
                                            in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
                            }
                            .buttonStyle(.plain)
                            .accessibilityLabel("\(reference.title)：\(reference.claim)，\(supportLabel(reference))")
                        }
                    }
                }
                .frame(maxHeight: 230)
                .background(Ink.sunken.opacity(0.45), in: RoundedRectangle(cornerRadius: Space.radiusSmall))

                if let selected {
                    Rectangle().fill(Ink.hairline).frame(height: 1)
                    Text(verbatim: selected.claim).font(Typo.body).textSelection(.enabled)
                    Text(supportLabel(selected))
                        .font(Typo.caption).foregroundStyle(selected.needsConfirmation || selected.support != .quoted ? Ink.caution : Ink.secondary)
                    Text(reviewLabel(selected))
                        .font(Typo.caption)
                        .foregroundStyle(selected.reviewIsStale ? Ink.caution : Ink.secondary)
                    if let quote = selected.quote, !quote.isEmpty {
                        Text("引用").font(Typo.label).foregroundStyle(Ink.secondary)
                        Text(verbatim: quote).font(Typo.body).textSelection(.enabled)
                    }
                    if let sourceText = selected.sourceText {
                        Text(sourceLabel(selected))
                            .font(Typo.label).foregroundStyle(Ink.secondary)
                        Text(verbatim: sourceText).font(Typo.caption).textSelection(.enabled)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                    HStack(spacing: Space.s2) {
                        if let lineID = selected.lineID {
                            Button(jumpLabel(selected)) { onJump(lineID, selected.sourceVariant) }
                                .buttonStyle(SecondaryButtonStyle())
                        }
                        if selected.notesItemID != nil || selected.structuredTarget != nil {
                            Button("编辑条目") { onEdit(selected) }
                                .buttonStyle(GhostButtonStyle(height: 32, horizontalPadding: 10))
                                .disabled(!editingEnabled)
                        }
                    }
                    if canReview(selected) {
                        Text("请先读上面的转写，再明确判断它是否支持这条内容。核对会绑定当前文本版本；文字变化后需要重新核对。")
                            .font(Typo.caption).foregroundStyle(Ink.secondary)
                            .fixedSize(horizontal: false, vertical: true)
                        HStack(spacing: Space.s2) {
                            Button(selected.reviewDecision == .supported ? "已确认支持" : "确认原话支持") {
                                onReview(selected, .supported)
                            }
                            .buttonStyle(SecondaryButtonStyle())
                            .disabled(!editingEnabled || selected.reviewDecision == .supported)
                            Button(selected.reviewDecision == .unsupported ? "已标记不支持" : "标记原话不支持") {
                                onReview(selected, .unsupported)
                            }
                            .buttonStyle(GhostButtonStyle(height: 32, horizontalPadding: 10))
                            .disabled(!editingEnabled || selected.reviewDecision == .unsupported)
                            if selected.reviewDecision != nil || selected.reviewIsStale {
                                Button("撤销核对") { onReview(selected, nil) }
                                    .buttonStyle(.plain)
                                    .font(Typo.caption)
                                    .disabled(!editingEnabled)
                            }
                        }
                    }
                }
            }
        }
        .padding(Space.s4)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Ink.surface, in: RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 1))
    }

    private func supportLabel(_ reference: SessionEvidenceReference) -> String {
        var label = reference.support.label
        if reference.sourceStatus == .stale { label += String(localized: " · 来源版本已变化") }
        if reference.sourceStatus == .unknown { label += String(localized: " · 来源版本未知") }
        return reference.needsConfirmation ? String(localized: "模型标记待确认 · \(label)") : label
    }

    private func reviewLabel(_ reference: SessionEvidenceReference) -> String {
        if reference.reviewIsStale { return String(localized: "之前的人工核对已失效，需要重新检查。") }
        return switch reference.reviewDecision {
        case .supported: String(localized: "人工已确认：当前原话支持这条内容。")
        case .unsupported: String(localized: "人工已确认：当前原话不支持这条内容。")
        case nil: String(localized: "尚未由你确认内容是否受原话支持。")
        }
    }

    private func sourceLabel(_ reference: SessionEvidenceReference) -> String {
        return switch reference.sourceVariant {
        case .original: String(localized: "匹配的原始转写")
        case .corrected: reference.support == .quoted ? String(localized: "匹配的校正转写") : String(localized: "附近的校正转写（仅供核对）")
        case nil: String(localized: "附近转写（仅供核对）")
        }
    }

    private func jumpLabel(_ reference: SessionEvidenceReference) -> String {
        return switch reference.sourceVariant {
        case .original: String(localized: "查看原始转写")
        case .corrected: reference.support == .quoted ? String(localized: "查看校正转写") : String(localized: "查看附近转写")
        case nil: String(localized: "查看附近转写")
        }
    }

    private func canReview(_ reference: SessionEvidenceReference) -> Bool {
        reference.lineID != nil && reference.sourceText != nil
            && reference.sourceRevision != nil && reference.claimRevision != nil
            && !reference.transcriptSourceRevision.isEmpty
    }
}

private struct NotesItemEditSheet: View {
    @Environment(\.dismiss) private var dismiss
    let title: LocalizedStringKey
    let text: String
    let onSave: (String, @escaping @MainActor (SessionArchiveEditResult) -> Void) -> Void
    @State private var draft: String
    @State private var saveFailure: String?
    @State private var saving = false

    init(title: LocalizedStringKey, text: String,
         onSave: @escaping (String, @escaping @MainActor (SessionArchiveEditResult) -> Void) -> Void) {
        self.title = title
        self.text = text
        self.onSave = onSave
        _draft = State(initialValue: text)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s4) {
            Text(title).font(Typo.section)
            TextEditor(text: $draft)
                .font(Typo.body)
                .frame(minWidth: 440, minHeight: 150)
                .overlay(RoundedRectangle(cornerRadius: Space.radiusSmall).strokeBorder(Ink.hairline, lineWidth: 1))
            if let saveFailure {
                Label(saveFailure, systemImage: "exclamationmark.triangle.fill")
                    .font(Typo.caption).foregroundStyle(Ink.caution)
                    .fixedSize(horizontal: false, vertical: true)
            }
            HStack {
                Spacer()
                Button("取消") { dismiss() }.buttonStyle(GhostButtonStyle()).disabled(saving)
                Button(LocalizedStringKey(saving ? "保存中…" : "保存")) {
                    saving = true
                    saveFailure = nil
                    onSave(draft.trimmingCharacters(in: .whitespacesAndNewlines)) { result in
                        saving = false
                        if result == .saved {
                            dismiss()
                        } else {
                            saveFailure = result.failureMessage
                        }
                    }
                }
                .buttonStyle(PrimaryButtonStyle())
                .disabled(saving || draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
        }
        .padding(Space.s6)
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
    let projection: SessionTextProjection?
    let highlighted: UUID?
    @Binding var version: TranscriptVersion
    /// Find in the transcript; recorded misheard spellings (术语和人名) match too.
    @State private var find = ""
    @State private var findCache = GlossarySearchCache()
    @State private var glossaryDraft: GlossaryDraft?

    var body: some View {
        let projectedLines = projection?.transcript ?? []
        let terms: GlossarySearch? = find.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? nil : findCache.terms(for: find, state: state)
        let shown = projectedLines.filter {
            terms == nil || terms?.appMatch(in: $0.correctedText) != nil || terms?.appMatch(in: $0.originalText) != nil
        }
        let layout = SpeakerLayout(lines: detail.lines, voiceNames: detail.meta.voiceNames, meVoices: Set(detail.meta.meVoices ?? []))
        VStack(alignment: .leading, spacing: Space.s3) {
        if layout.isShown {
            SpeakerStrip(layout: layout,
                         rename: { state.renameVoice($0, to: $1, in: detail.meta.id) },
                         merge: { state.mergeVoices($0, into: $1, in: detail.meta.id) },
                         mark: { state.markVoice($0, isMe: $1, in: detail.meta.id) })
        }
        LazyVStack(alignment: .leading, spacing: Space.s3) {
            if projectedLines.isEmpty {
                Text("这场会议没有转写。").font(Typo.body).foregroundStyle(Ink.secondary)
            } else {
                HStack(spacing: Space.s2) {
                    Text("转写版本").font(Typo.caption).foregroundStyle(Ink.secondary)
                    InkSegmented(items: [(value: TranscriptVersion.corrected, title: "校正转写"),
                                         (value: TranscriptVersion.original, title: "原始转写")],
                                 selection: $version)
                        .fixedSize()
                }
                TranscriptFindField(query: $find, matches: terms == nil ? nil : shown.count)
            }
            ForEach(shown) { line in
                HStack(alignment: .firstTextBaseline, spacing: Space.s3) {
                    Text(verbatim: NotesTime.text(line.offset))
                        .font(.system(size: 11).monospacedDigit()).foregroundStyle(Ink.tertiary)
                        .frame(width: 52, alignment: .leading)
                    Text(line.speaker.displayName)
                        .font(.system(size: 12, weight: line.speaker == .me ? .semibold : .regular))
                        .foregroundStyle(line.speaker == .me ? Ink.ink : Ink.secondary)
                        .frame(width: 40, alignment: .leading)
                        .opacity(layout.hidesRoleLabel(for: line.speaker) ? 0 : 1)
                    VStack(alignment: .leading, spacing: Space.s1) {
                        if layout.hidesRoleLabel(for: line.speaker), layout.starts.contains(line.id) {
                            SpeakerTag(layout: layout, lineID: line.id, speaker: line.speaker)
                        }
                        Text(Self.text(line, version: version))
                            .font(Typo.body).lineSpacing(5)
                            .textSelection(.enabled)
                            .help(version == .corrected ? Self.originals(line.appliedCorrections) : "")
                        if let terms, let hit = terms.appMatch(in: line.originalText), hit.isMisheard {
                            MisheardMark(form: hit.form)
                        }
                        if let translation = line.translationText, !translation.isEmpty {
                            HStack(alignment: .firstTextBaseline, spacing: Space.s1) {
                                Text("译文：\(translation)").font(Typo.caption).foregroundStyle(Ink.ai).lineSpacing(4)
                                if line.translationIsStale {
                                    Text("待更新").font(.system(size: 10, weight: .medium)).foregroundStyle(Ink.caution)
                                }
                            }
                                .textSelection(.enabled)
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                .padding(.vertical, 2)
                .background(highlighted == line.id ? Ink.hover : Color.clear, in: RoundedRectangle(cornerRadius: 4, style: .continuous))
                .id("line-\(line.id.uuidString)")
                .contextMenu {
                    if version == .corrected {
                        ForEach(line.appliedCorrections) { fix in
                            Button(fix.reverted ? "恢复校对：\(fix.original) → \(fix.corrected)" : "撤销校对：\(fix.corrected) → \(fix.original)") {
                                state.toggleCorrection(fix.id, in: detail.meta.id)
                            }
                            .disabled(state.archive.summarizing.contains(detail.meta.id))
                        }
                    }
                    Button(detail.userContent.highlights.contains(where: { $0.lineID == line.id }) ? "取消重点" : "标为重点") {
                        state.toggleHighlight(lineID: line.id, text: version == .original ? line.originalText : line.correctedText,
                                              offsetSeconds: line.offset, sessionID: detail.meta.id)
                    }
                    Button("加入术语表…") { glossaryDraft = GlossaryDraft(line: line.originalText) }
                }
            }
        }
        .sheet(item: $glossaryDraft) { draft in
            AddToGlossarySheet(line: draft.line).environment(state)
        }
        }
    }

    /// The line with active corrections applied and underlined with dots.
    static func text(_ line: SessionProjectedTranscriptLine, version: TranscriptVersion) -> AttributedString {
        let text = version == .original ? line.originalText : line.correctedText
        var attributed = AttributedString(text)
        guard version == .corrected else { return attributed }
        for fix in line.appliedCorrections {
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
