import AppKit
import Foundation
import NextThoughtCore

/// The two ways a meeting starts, remembered while the recording notice is shown.
enum PendingStart: Equatable {
    case prepared
    case quick
}

/// Preparing a meeting (首页 → 准备) and starting it.
@MainActor
extension AppState {
    // MARK: Draft

    /// "新会议" (⌘N): opens the preparation page of a new meeting.
    func beginNewMeeting() {
        guard !isRunning else { return }
        WorkspaceEditController.shared.requestNewMeeting(state: self)
    }

    func beginNewMeetingAfterWorkspaceEdit() {
        guard !isRunning else { return }
        if meetingDraft == nil { meetingDraft = MeetingDraft() }
        section = .home
        MainWindowLayout.bringToFront()
    }

    /// "以此为上一场，准备下一场": a new meeting in the same series as `sessionID`.
    func prepareNextMeeting(after sessionID: UUID) {
        guard !isRunning, let meta = archive.sessions.first(where: { $0.id == sessionID }),
              let seriesID = meta.workspaceID, let series = workspaces.workspace(seriesID) else { return }
        var draft = MeetingDraft()
        draft.title = series.name
        draft.mode = series.mode
        draft.customTypeName = series.customTypeName ?? ""
        draft.customBaseMode = series.customBaseMode ?? .general
        draft.typeChosen = true
        meetingDraft = draft
        bindDraft(to: seriesID, isNew: false)
        section = .home
    }

    /// Leaves the preparation page. Imported materials stay in their series.
    func cancelDraft() {
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
        guard let draft = meetingDraft, draftSeries != nil else { return false }
        return draft.seriesID == nil ? draft.suggestedSeriesID != nil : !draft.seriesIsNew
    }

    func updateDraftTitle(_ title: String) {
        guard var draft = meetingDraft else { return }
        draft.title = title
        if !draft.typeChosen { draft.mode = MeetingTypeGuesser.guess(title) }
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
        if !draft.typeChosen, let id = draft.seriesID, draft.seriesIsNew { applyDraftType(to: id) }
    }

    func setDraftType(_ mode: SessionMode) {
        guard var draft = meetingDraft else { return }
        draft.mode = mode
        draft.typeChosen = true
        meetingDraft = draft
        if let id = draft.seriesID { applyDraftType(to: id) }
    }

    func setDraftCustomType(name: String? = nil, base: SessionMode? = nil) {
        guard var draft = meetingDraft else { return }
        if let name { draft.customTypeName = name }
        if let base { draft.customBaseMode = base }
        draft.mode = .custom
        draft.typeChosen = true
        meetingDraft = draft
        if let id = draft.seriesID { applyDraftType(to: id) }
    }

    func setDraftCarry(_ carry: MeetingCarry) {
        meetingDraft?.carry = carry
    }

    /// "不是同一个系列": forget the suggestion; a bound series is replaced by a new one.
    func declineDraftSeries() {
        guard var draft = meetingDraft, let id = draft.seriesID ?? draft.suggestedSeriesID else { return }
        draft.declinedSeriesIDs.insert(id)
        draft.suggestedSeriesID = nil
        draft.carry = .standard
        let wasBound = draft.seriesID == id
        if wasBound { draft.seriesID = nil; draft.seriesIsNew = false }
        meetingDraft = draft
        if wasBound { _ = ensureDraftSeries() }
    }

    /// "从以前的会议带入…": continue the chosen series.
    func continueDraftSeries(_ id: UUID) {
        guard var draft = meetingDraft, let series = workspaces.workspace(id) else { return }
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
        let name = trimmed.isEmpty
            ? String(localized: "会议 \(Date.now.formatted(date: .abbreviated, time: .shortened))")
            : trimmed
        guard !isRunning else { return nil }
        createWorkspace(mode: draft.mode, name: name)
        let id = activeWorkspace.id
        bindDraft(to: id, isNew: true)
        applyDraftType(to: id)
        return id
    }

    private func bindDraft(to id: UUID, isNew: Bool) {
        guard var draft = meetingDraft, let series = workspaces.workspace(id) else { return }
        draft.seriesID = id
        draft.seriesIsNew = isNew
        draft.baselineDocumentIDs = isNew ? [] : Set(series.documents.map(\.id))
        meetingDraft = draft
        if activeWorkspace.id != id { switchWorkspace(id) }
    }

    /// Writes the draft's type (and its answer length) into the bound series.
    private func applyDraftType(to id: UUID) {
        guard let draft = meetingDraft, var series = workspaces.workspace(id) else { return }
        let changed = series.mode != draft.mode
        if changed {
            workspaces.setMode(draft.mode, for: id)
            series = workspaces.workspace(id) ?? series
        }
        if draft.mode == .custom {
            series.customTypeName = draft.customTypeName
            series.customBaseMode = draft.customBaseMode
        }
        if changed || draft.mode == .custom {
            series.style = MeetingTypeDefaults.for(series.effectiveMode).style
            workspaces.update(series)
        }
        if changed, id == activeWorkspace.id { tracker.mode = activeWorkspace.effectiveMode }
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
        guard ensureDraftSeries() != nil else { return }
        // Binding or creating the series rebuilds its index, and `ask` refuses to run
        // while that is in progress: wait for it (as starting does).
        Task {
            for _ in 0..<100 where isIndexingDocuments {
                try? await Task.sleep(for: .milliseconds(100))
            }
            predictQuestions()
            predictionAnswerID = isAnswering ? currentAnswerID : nil
        }
    }

    // MARK: Starting

    /// "开始" on the preparation page.
    func startPreparedMeeting() async {
        guard meetingDraft != nil, !isRunning, !isChangingSession else { return }
        if !UserDefaults.standard.bool(forKey: "recordingNoticeShown") { recordingNoticePending = .prepared; return }
        // Binding the series updates the draft (baseline, new or existing): read it after.
        guard let seriesID = ensureDraftSeries(), let draft = meetingDraft else { return }
        plannedMeeting = MeetingPlan(title: draft.title, carry: draft.carry,
                                     baselineDocumentIDs: draft.baselineDocumentIDs,
                                     previousSessionID: draft.seriesIsNew ? nil : lastSession(inSeries: seriesID)?.id)
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
        plannedMeeting = MeetingPlan()
        await startWithPermissions()
        if !isRunning && !isChangingSession { plannedMeeting = nil }
    }

    /// The first start shows a short notice about recording consent.
    func confirmRecordingNotice(_ pending: PendingStart?) async {
        guard let pending else { return }
        UserDefaults.standard.set(true, forKey: "recordingNoticeShown")
        recordingNoticePending = nil
        switch pending {
        case .prepared: await startPreparedMeeting()
        case .quick: await recordNow()
        }
    }

    /// Asks for screen-and-audio permission in place (instead of an onboarding
    /// step), waits for a series switch to finish indexing, then starts.
    private func startWithPermissions() async {
        if !capturePermissionGranted, meetingVenue == .online {
            await requestCapturePermission()
            guard capturePermissionGranted else {
                errorMessage = String(localized: "需要允许“屏幕与系统音频录制”才能听到会议里的声音。允许后回到这里点开始。")
                return
            }
        }
        if microphoneEnabled || meetingVenue == .offline, MicrophoneAccess.current() == .undetermined {
            await requestMicrophoneAccess()
        }
        for _ in 0..<100 where isIndexingDocuments {
            try? await Task.sleep(for: .milliseconds(100))
        }
        await startSession()
    }

    /// A general series that collects quick recordings ("随手记录").
    private func quickNotesSeries() -> UUID {
        let key = "quickNotesWorkspaceID"
        if let raw = UserDefaults.standard.string(forKey: key), let id = UUID(uuidString: raw),
           let series = workspaces.workspace(id), !series.archived {
            return id
        }
        let previous = activeWorkspace.id
        let created = workspaces.create(mode: .general, name: String(localized: "随手记录"))
        UserDefaults.standard.set(created.id.uuidString, forKey: key)
        // `create` activates the new series; `recordNow` switches to it properly.
        workspaces.activate(previous)
        return created.id
    }
}
