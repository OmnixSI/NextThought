import Foundation
import SwiftUI
import InterviewHackerCore

// MARK: - Meeting plan

/// What to bring from the previous meeting of the series ("接着上一场").
struct MeetingCarry: Equatable {
    /// Materials the series already had before this meeting.
    var materials = true
    /// The question bank: reviewed questions with improved answers.
    var review = true
    /// Lessons ("经验").
    var lessons = true
    /// The full transcript of the previous meeting (long, so off by default).
    var transcript = false

    static let standard = MeetingCarry()
}

/// What the next start uses: set by the preparation page, cleared when the meeting ends.
struct MeetingPlan: Equatable {
    var title: String?
    var carry: MeetingCarry = .standard
    /// Materials of the series before this meeting; `carry.materials == false` leaves them out.
    var baselineDocumentIDs: Set<UUID> = []
    /// The meeting this one continues (for the transcript carry).
    var previousSessionID: UUID?
}

/// The meeting on the preparation page, before it starts (kept in memory only).
struct MeetingDraft: Equatable {
    var title = ""
    var mode: SessionMode = .general
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
    var typeDefaults: MeetingTypeDefaults { MeetingTypeDefaults.for(activeWorkspace.effectiveMode) }

    /// The workspace a starting meeting answers from: the series, minus what the
    /// preparation page left unchecked, plus the previous transcript when asked for.
    /// When anything differs from the series, the retrieval index is rebuilt from
    /// that set (keyword search only: the vector index belongs to the full series).
    func meetingWorkspaceForStart() async -> Workspace {
        let series = activeWorkspace
        guard let plan = plannedMeeting, plan.carry != .standard else { return series }
        var workspace = series
        if !plan.carry.materials {
            workspace.documents.removeAll { plan.baselineDocumentIDs.contains($0.id) }
        }
        if !plan.carry.review { workspace.questionBank = [] }
        if !plan.carry.lessons { workspace.lessons = [] }
        if plan.carry.transcript, let previous = plan.previousSessionID, let detail = await archive.detail(previous), !detail.lines.isEmpty {
            let text = SessionSummaryPrompt.transcriptText(detail.lines, start: detail.meta.startedAt)
            workspace.documents.append(ContextDocument(name: detail.meta.displayTitle,
                                                       pages: SessionSummaryPrompt.chunks(text, limit: 4_000),
                                                       category: "上一场转写")) // l10n-ignore: label for the model
        }
        let documents = workspace.retrievalDocuments
        if documents != series.retrievalDocuments {
            contextIndex = await Task.detached(priority: .userInitiated) {
                HybridContextIndex(documents: documents, semantic: SemanticIndex())
            }.value
            Diagnostics.record("meeting.carry_applied", ["documents": documents.count])
        }
        return workspace
    }
}
