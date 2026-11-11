import AppKit
import Foundation
import NextThoughtCore

/// The two ways a meeting starts, remembered while the recording notice is shown.
enum PendingStart: Equatable {
    case prepared
    case quick
    case direct
}

/// Preparing a meeting (首页 → 准备) and starting it.
@MainActor
extension AppState {
    // MARK: Draft

    /// "新会议" (⌘N): opens the preparation page of a new meeting.
    func beginNewMeeting() {
        guard !isRunning, !isChangingSession else { return }
        pendingDraftScene = nil
        WorkspaceEditController.shared.requestNewMeeting(state: self)
    }

    /// A new meeting of a scene picked on the home page.
    func beginNewMeeting(scene mode: SessionMode) {
        guard !isRunning, !isChangingSession else { return }
        pendingDraftScene = mode
        WorkspaceEditController.shared.requestNewMeeting(state: self)
    }

    func beginNewMeetingAfterWorkspaceEdit() {
        guard !isRunning, !isChangingSession else { return }
        if meetingDraft == nil { meetingDraft = MeetingDraft() }
        if let scene = pendingDraftScene {
            pendingDraftScene = nil
            setDraftType(scene)
        }
        section = .home
        MainWindowLayout.bringToFront()
    }

    /// "以此为上一场，准备下一场": a new meeting in the same series as `sessionID`.
    func prepareNextMeeting(after sessionID: UUID) {
        guard !isRunning, !isChangingSession, let meta = archive.sessions.first(where: { $0.id == sessionID }),
              let seriesID = meta.workspaceID, let series = workspaces.workspace(seriesID) else { return }
        pendingDraftScene = nil
        var draft = MeetingDraft()
        draft.title = series.name
        draft.mode = series.mode
        draft.customTypeName = series.customTypeName ?? ""
        draft.customBaseMode = series.customBaseMode ?? .general
        draft.typeChosen = true
        draft.previousSessionID = sessionID
        meetingDraft = draft
        bindDraft(to: seriesID, isNew: false)
        section = .home
    }

    /// Leaves the preparation page. Imported materials stay in their series.
    func cancelDraft() {
        guard !isChangingSession else { return }
        PreparationPreview.shared.cancel()
        meetingDraft = nil
        predictionAnswerID = nil
    }

    /// The series the draft uses now: the bound one, else the suggestion.
    var draftSeries: Workspace? {
        guard let draft = meetingDraft else { return nil }
        return workspaces.workspace(draft.seriesID ?? draft.suggestedSeriesID)
    }

    /// Whether the draft continues an existing series (shows "接着上一场").
    var draftContinuesSeries: Bool {
        guard let draft = meetingDraft, let series = draftSeries, !series.isStanding else { return false }
        return draft.seriesID == nil ? draft.suggestedSeriesID != nil : !draft.seriesIsNew
    }

    func updateDraftTitle(_ title: String) {
        guard var draft = meetingDraft else { return }
        draft.title = title
        if !draft.typeChosen {
            let guessed = MeetingTypeGuesser.guess(title)
            draft.mode = guessed
        }
        if draft.seriesID == nil {
            let match = SeriesMatcher.bestMatch(title: title, mode: draft.mode,
                                                candidates: seriesCandidates(excluding: draft.declinedSeriesIDs))
            if match != draft.suggestedSeriesID { draft.suggestedSeriesID = match; draft.carry = .standard }
        }
        meetingDraft = draft
        // A series made for this meeting follows its title.
        let trimmed = title.trimmingCharacters(in: .whitespacesAndNewlines)
        if draft.seriesIsNew, let id = draft.seriesID, !trimmed.isEmpty, var series = workspaces.workspace(id), series.name != trimmed {
            series.name = trimmed
            workspaces.update(series)
        }
    }

    func setDraftType(_ mode: SessionMode) {
        guard var draft = meetingDraft else { return }
        draft.mode = mode
        draft.typeChosen = true
        meetingDraft = draft
    }

    func setDraftCustomType(name: String? = nil, base: SessionMode? = nil) {
        guard var draft = meetingDraft else { return }
        if let name { draft.customTypeName = name }
        if let base { draft.customBaseMode = base }
        draft.mode = .custom
        draft.typeChosen = true
        meetingDraft = draft
    }

    func setDraftCarry(_ carry: MeetingCarry) {
        meetingDraft?.carry = carry
    }

    /// "不是同一个系列": forget the suggestion; a bound series is replaced by a new one.
    func declineDraftSeries() {
        guard var draft = meetingDraft, let id = draft.seriesID ?? draft.suggestedSeriesID else { return }
        draft.declinedSeriesIDs.insert(id)
        draft.suggestedSeriesID = nil
        draft.previousSessionID = nil
        draft.carry = .standard
        let wasBound = draft.seriesID == id
        if wasBound { draft.seriesID = nil; draft.seriesIsNew = false }
        meetingDraft = draft
        if wasBound { _ = ensureDraftSeries() }
    }

    /// "从以前的会议带入…": continue the chosen series.
    func continueDraftSeries(_ id: UUID) {
        guard var draft = meetingDraft, let series = workspaces.workspace(id) else { return }
        pendingDraftScene = nil
        draft.declinedSeriesIDs.remove(id)
        draft.suggestedSeriesID = id
        if !draft.typeChosen {
            draft.mode = series.mode
            draft.customTypeName = series.customTypeName ?? ""
            draft.customBaseMode = series.customBaseMode ?? .general
        }
        meetingDraft = draft
        bindDraft(to: id, isNew: false)
    }

    /// The series the meeting writes into; created (or the suggestion accepted) on
    /// first need: adding materials or starting.
    @discardableResult
    func ensureDraftSeries() -> UUID? {
        guard let draft = meetingDraft else { return nil }
        if let id = draft.seriesID, workspaces.workspace(id) != nil {
            if activeWorkspace.id != id { switchWorkspace(id) }
            return id
        }
        if let suggested = draft.suggestedSeriesID, workspaces.workspace(suggested) != nil {
            bindDraft(to: suggested, isNew: false)
            return suggested
        }
        let trimmed = draft.title.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !isRunning else { return nil }
        if trimmed.isEmpty {
            // Untitled meetings of a scene share one standing series instead of one per date.
            if let standing = standingSeries(for: draft) {
                bindDraft(to: standing.id, isNew: false)
                // Earlier untitled meetings are unrelated: nothing is "the previous one".
                meetingDraft?.previousSessionID = nil
                return standing.id
            }
            createWorkspace(mode: draft.mode, name: standingSeriesName(for: draft))
            let id = activeWorkspace.id
            bindDraft(to: id, isNew: true)
            applyDraftType(to: id)
            markStanding(id)
            meetingDraft?.seriesIsNew = false
            meetingDraft?.previousSessionID = nil
            return id
        }
        createWorkspace(mode: draft.mode, name: trimmed)
        let id = activeWorkspace.id
        bindDraft(to: id, isNew: true)
        applyDraftType(to: id)
        return id
    }

    /// "面试 · 未命名": the standing series for untitled meetings of the draft's scene.
    private func standingSeriesName(for draft: MeetingDraft) -> String {
        let scene = draft.mode == .custom && !draft.customTypeName.isEmpty ? draft.customTypeName : draft.mode.title
        return String(localized: "\(scene) · 未命名")
    }

    private func standingSeries(for draft: MeetingDraft) -> Workspace? {
        let quick = UserDefaults.standard.string(forKey: "quickNotesWorkspaceID").flatMap(UUID.init(uuidString:))
        return workspaces.workspaces.first {
            $0.isStanding && !$0.archived && $0.id != quick && $0.mode == draft.mode
                && ($0.mode != .custom || ($0.customTypeName ?? "") == draft.customTypeName)
        }
    }

    private func markStanding(_ id: UUID) {
        guard var series = workspaces.workspace(id), !series.isStanding else { return }
        series.standing = true
        workspaces.update(series)
    }

    private func bindDraft(to id: UUID, isNew: Bool) {
        guard var draft = meetingDraft, let series = workspaces.workspace(id) else { return }
        if draft.seriesID != id {
            draft.baselineDocumentIDs = isNew ? [] : Set(series.documents.map(\.id))
        }
        if isNew {
            draft.previousSessionID = nil
        } else if !archive.sessions.contains(where: { $0.id == draft.previousSessionID && $0.workspaceID == id }) {
            draft.previousSessionID = lastSession(inSeries: id)?.id
        }
        draft.seriesID = id
        draft.seriesIsNew = isNew
        meetingDraft = draft
        if activeWorkspace.id != id { switchWorkspace(id) }
    }

    /// Initializes a newly-created series. Existing series are never changed by
    /// a single meeting's preparation choices.
    private func applyDraftType(to id: UUID) {
        guard let draft = meetingDraft, draft.seriesIsNew, let series = workspaces.workspace(id),
              let plan = draftMeetingPlan else { return }
        workspaces.update(plan.preparedWorkspace(from: series))
        if id == activeWorkspace.id { tracker.mode = activeWorkspace.effectiveMode }
    }

    /// Adds files to the meeting's series (created if needed).
    func importDraftDocuments(_ urls: [URL]) {
        guard !urls.isEmpty, ensureDraftSeries() != nil else { return }
        importDocuments(urls)
    }

    /// Preparation jobs of this draft's series started since the page opened.
    var draftJobs: [DocumentJob] {
        guard let draft = meetingDraft, let id = draft.seriesID else { return [] }
        return documentPreparation.jobs.filter { $0.workspaceID == id && $0.createdAt >= draft.startedAt }
    }

    /// "AI 读到的重点 · 可能会问": one request over the series' materials; the answer
    /// streams into the preparation page.
    func readDraftMaterials() {
        guard answerEngineReady, ensureDraftSeries() != nil,
              let series = draftSeries, let plan = draftMeetingPlan else { return }
        PreparationPreview.shared.read(series: series, plan: plan, using: self)
    }

    // MARK: Starting

    /// "开始" on the preparation page.
    func startPreparedMeeting() async {
        guard meetingDraft != nil, !isRunning, !isChangingSession else { return }
        if !UserDefaults.standard.bool(forKey: "recordingNoticeShown") { recordingNoticePending = .prepared; return }
        // Binding the series updates the draft (baseline, new or existing): read it after.
        guard ensureDraftSeries() != nil, let plan = draftMeetingPlan else { return }
        plannedMeeting = plan
        PreparationPreview.shared.cancel()
        await startWithPermissions()
        if isRunning {
            meetingDraft = nil
            predictionAnswerID = nil
        } else if !isChangingSession {
            // A second click while the first start is still running must not clear its plan.
            plannedMeeting = nil
        }
    }

    /// "立即记录" (⌘⇧N): no type, no materials; a general series for quick notes.
    func recordNow() async {
        guard !isRunning, !isChangingSession else { return }
        if !UserDefaults.standard.bool(forKey: "recordingNoticeShown") { recordingNoticePending = .quick; return }
        let id = quickNotesSeries()
        if activeWorkspace.id != id { switchWorkspace(id) }
        plannedMeeting = MeetingPlan(carry: .init(materials: false, review: false, lessons: false),
                                     baselineDocumentIDs: Set(activeWorkspace.documents.map(\.id)))
        isQuickRecording = true
        PreparationPreview.shared.cancel()
        await startWithPermissions()
        if isRunning { meetingDraft = nil; predictionAnswerID = nil }
        if !isRunning && !isChangingSession { plannedMeeting = nil; isQuickRecording = false }
    }

    /// The first start shows a short notice about recording consent.
    func confirmRecordingNotice(_ pending: PendingStart?) async {
        guard let pending else { return }
        UserDefaults.standard.set(true, forKey: "recordingNoticeShown")
        recordingNoticePending = nil
        switch pending {
        case .prepared: await startPreparedMeeting()
        case .quick: await recordNow()
        case .direct: await startSession()
        }
    }

    /// Asks for screen-and-audio permission in place (instead of an onboarding
    /// step), then starts without waiting for optional material indexing.
    private func startWithPermissions() async {
        await startSession()
    }

    /// A general series that collects quick recordings ("随手记录").
    private func quickNotesSeries() -> UUID {
        let key = "quickNotesWorkspaceID"
        if let raw = UserDefaults.standard.string(forKey: key), let id = UUID(uuidString: raw),
           let series = workspaces.workspace(id), !series.archived {
            markStanding(id)
            return id
        }
        let previous = activeWorkspace.id
        let created = workspaces.create(mode: .general, name: String(localized: "随手记录"))
        UserDefaults.standard.set(created.id.uuidString, forKey: key)
        markStanding(created.id)
        // `create` activates the new series; `recordNow` switches to it properly.
        workspaces.activate(previous)
        return created.id
    }
}
