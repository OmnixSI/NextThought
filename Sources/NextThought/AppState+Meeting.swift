import Foundation
import SwiftUI
import NextThoughtCore

// MARK: - Meeting plan

/// The meeting on the preparation page, before it starts (kept in memory only).
struct MeetingDraft: Equatable {
    var title = ""
    var mode: SessionMode = .labMeeting
    /// Name and base type of a custom type ("自定义…").
    var customTypeName = ""
    var customBaseMode: SessionMode = .general
    /// The user picked the type: stop guessing it from the title.
    var typeChosen = false
    /// The series the meeting uses once bound (materials are imported into it).
    var seriesID: UUID?
    /// The series was created for this meeting, so it follows the title.
    var seriesIsNew = false
    /// An existing series that looks like the same thing ("接着上一场").
    var suggestedSeriesID: UUID?
    /// Series the user said this meeting does not belong to.
    var declinedSeriesIDs: Set<UUID> = []
    var carry = MeetingCarry.standard
    /// Materials the bound series had before this meeting.
    var baselineDocumentIDs: Set<UUID> = []
    var previousSessionID: UUID?
    /// When the page opened (files added since then are "这次新加的资料").
    var startedAt = Date()
}

/// Where the meeting happens (02-scenarios-and-flows.md, "声音从哪来").
enum MeetingVenue: String, CaseIterable, Identifiable {
    /// Them = everything this Mac plays; me = the microphone.
    case online
    /// Everyone in one room: the microphone only, transcribed as "现场".
    case offline
    var id: String { rawValue }
    var title: LocalizedStringKey { self == .online ? "线上" : "线下" }
}

extension SessionMode {
    /// Localized name for the interface (the Core `title` stays Chinese for prompts).
    var displayName: String {
        switch self {
        case .general: String(localized: "通用")
        case .interview: String(localized: "会议")
        case .academic: String(localized: "汇报 / 答辩")
        case .labMeeting: String(localized: "组会")
        case .meeting: String(localized: "工作会议")
        case .client: String(localized: "客户沟通")
        case .lesson: String(localized: "讲座 / 课程")
        case .discussion: String(localized: "小组讨论")
        case .custom: String(localized: "自定义")
        }
    }

    /// One line on what the type sets up (shown under the type tags, and on hover).
    var defaultsSummary: String {
        switch self {
        case .general: String(localized: "只记录，需要时 ⌘⇧↩ 回答 · 回答简短 · 纪要：总结、决定、待办")
        case .interview: String(localized: "会中自动生成参考回答 · 回答简短 · 会后整理纪要")
        case .academic: String(localized: "会中自动给出回答，跟着讲稿 · 回答简短 · 纪要：听众问了什么、怎么答更好")
        case .labMeeting: String(localized: "只记录，被问到时 ⌘⇧↩ · 回答详细 · 纪要：进展、导师意见、下一步")
        case .meeting: String(localized: "只记录 · 回答简短 · 纪要：决定、待办、风险")
        case .client: String(localized: "会中自动给出回答 · 回答简短 · 纪要：需求、顾虑、我们答应的、下一步")
        case .lesson: String(localized: "只记录 · 回答详细 · 笔记：要点、概念、我的疑问")
        case .discussion: String(localized: "只记录 · 回答简短 · 纪要：各方观点、结论、分工")
        case .custom: String(localized: "选一个类型作为基础，再改名字")
        }
    }

    /// What to add before this kind of meeting (empty when nothing in particular).
    var preparationHint: String {
        switch self {
        case .interview: String(localized: "议程、项目资料")
        case .academic: String(localized: "讲稿、PPT、论文")
        case .labMeeting: String(localized: "本周进展、相关论文")
        case .meeting: String(localized: "议程")
        case .client: String(localized: "产品资料、报价")
        case .lesson: String(localized: "课件")
        case .discussion: String(localized: "讨论题目")
        case .general, .custom: ""
        }
    }
}

extension Workspace {
    /// The type as the user sees it: the custom name, or the built-in name.
    var typeDisplayName: String {
        if mode == .custom, let name = customTypeName?.trimmingCharacters(in: .whitespacesAndNewlines), !name.isEmpty { return name }
        return mode.displayName
    }
}

// MARK: - Series and meeting start

@MainActor
extension AppState {
    /// Every series that could be continued, with when it was last used.
    func seriesCandidates(excluding excluded: Set<UUID> = []) -> [SeriesMatcher.Candidate] {
        workspaces.visible.filter { !excluded.contains($0.id) }.map { workspace in
            let last = lastSession(inSeries: workspace.id)?.startedAt ?? .distantPast
            return SeriesMatcher.Candidate(id: workspace.id, name: workspace.name, mode: workspace.mode,
                                           lastUsed: max(last, workspace.updatedAt))
        }
    }

    /// The latest recorded meeting of a series.
    func lastSession(inSeries id: UUID) -> SessionMeta? {
        archive.sessions.filter { $0.workspaceID == id && $0.id != archive.activeID }.max { $0.startedAt < $1.startedAt }
    }

    func sessionCount(inSeries id: UUID) -> Int {
        archive.sessions.filter { $0.workspaceID == id && $0.id != archive.activeID }.count
    }

    /// The default live mode of the active series' type.
    var typeDefaults: MeetingTypeDefaults { MeetingTypeDefaults.for((preparedWorkspace ?? activeWorkspace).effectiveMode) }

    var draftMeetingPlan: MeetingPlan? {
        guard let draft = meetingDraft else { return nil }
        let series = draftSeries
        return MeetingPlan(title: draft.title, mode: draft.mode, customTypeName: draft.customTypeName,
                           customBaseMode: draft.customBaseMode, carry: draft.carry,
                           baselineDocumentIDs: draft.seriesID == nil ? Set(series?.documents.map(\.id) ?? []) : draft.baselineDocumentIDs,
                           previousSessionID: draft.previousSessionID ?? series.flatMap { lastSession(inSeries: $0.id)?.id })
    }

    /// Preparation and capture use the same frozen single-meeting choices.
    func meetingWorkspace(for plan: MeetingPlan, in series: Workspace) async -> Workspace {
        var previousTranscript: ContextDocument?
        if plan.carry.transcript, let previous = plan.previousSessionID,
           let detail = await archive.detail(previous), !detail.lines.isEmpty {
            let text = SessionSummaryPrompt.transcriptText(detail.effectiveLines, start: detail.meta.startedAt)
            previousTranscript = ContextDocument(name: detail.meta.displayTitle,
                pages: SessionSummaryPrompt.chunks(text, limit: 4_000), category: "上一场转写")
        }
        return plan.preparedWorkspace(from: series, previousTranscript: previousTranscript)
    }

    /// The immediate workspace snapshot a starting meeting answers from.
    /// Optional transcript loading and any changed retrieval index are applied later.
    func meetingWorkspaceForStart() -> Workspace {
        let series = activeWorkspace
        guard let plan = plannedMeeting else { return series }
        return plan.preparedWorkspace(from: series)
    }

    /// Builds the optional, carry-filtered retrieval context after the archive has
    /// started and audio capture can proceed. An empty index is the safe fallback
    /// while a custom filter is being indexed; it cannot leak excluded materials.
    func prepareMeetingContextForStart(workspace: Workspace, plan: MeetingPlan?, epoch: UUID) {
        meetingContextTask?.cancel()
        meetingContextTask = nil
        guard let plan else { return }

        let seriesDocuments = activeWorkspace.retrievalDocuments
        let seriesIndex = contextIndex
        let needsTranscript = plan.carry.transcript && plan.previousSessionID != nil
        let needsFilteredIndex = workspace.retrievalDocuments != seriesDocuments || needsTranscript
        guard needsFilteredIndex else { return }

        // Do not use the unfiltered series index while custom carry options are
        // being applied. The workspace snapshot still carries its chosen profile,
        // lessons, and question bank immediately.
        contextIndex = HybridContextIndex()
        meetingContextTask = Task { [weak self] in
            guard let self else { return }
            var prepared = workspace
            if needsTranscript, let previous = plan.previousSessionID,
               let detail = await self.archive.detail(previous), !detail.lines.isEmpty {
                let text = SessionSummaryPrompt.transcriptText(detail.effectiveLines, start: detail.meta.startedAt)
                prepared.documents.append(ContextDocument(name: detail.meta.displayTitle,
                                                          pages: SessionSummaryPrompt.chunks(text, limit: 4_000),
                                                          category: "上一场转写")) // l10n-ignore: label for the model
            }
            guard !Task.isCancelled, self.audioEpoch == epoch, self.isRunning, !self.isStopping else { return }
            let documents = prepared.retrievalDocuments
            guard documents != seriesDocuments else {
                self.contextIndex = seriesIndex
                self.publishPreparedWorkspace(prepared)
                return
            }
            let index = await Task.detached(priority: .utility) {
                HybridContextIndex(documents: documents, semantic: SemanticIndex())
            }.value
            guard !Task.isCancelled, self.audioEpoch == epoch, self.isRunning, !self.isStopping else { return }
            self.contextIndex = index
            self.publishPreparedWorkspace(prepared)
            Diagnostics.record("meeting.carry_applied", ["documents": documents.count])
        }
    }

    private func publishPreparedWorkspace(_ workspace: Workspace) {
        preparedWorkspace = workspace
        if let sessionID = activeArchiveID {
            _ = updateArchiveContext(workspace, sessionID: sessionID)
        }
    }
}
