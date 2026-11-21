import Foundation
import Observation
import NextThoughtCore

/// Keeps the notes of the meeting in progress: every 45 seconds with new speech, one
/// request to the answer service returns the whole updated notes (`LiveNotes`,
/// `LiveNotesPrompt`). A failed or odd reply leaves the notes on show as they are. The
/// full notes are still written from the whole transcript when the meeting ends.
@MainActor
@Observable
final class LiveNotesEngine {
    enum Status: Equatable {
        case off
        case waiting
        case updating
        /// The last update did not come back; the next one tries again.
        case failed
        /// No answer service is connected.
        case noEngine
    }

    private(set) var notes = LiveNotes()
    private(set) var status: Status = .off
    /// Points, decisions and to-dos the last update added ("新").
    private(set) var fresh: Set<String> = []
    private(set) var updatedAt: Date?
    private(set) var isEnabled = false

    @ObservationIgnored private var lastAttempt = Date.distantPast
    @ObservationIgnored private var lastCheck = Date.distantPast
    @ObservationIgnored private var meeting = UUID()
    @ObservationIgnored private var inFlight = false

    /// Notes for screenshots and walkthroughs (demo mode).
    func installDemo(_ notes: LiveNotes, fresh: Set<String>) {
        self.notes = notes; self.fresh = fresh
        isEnabled = true; status = .waiting; updatedAt = Date()
    }

    /// A meeting starts.
    func begin(enabled: Bool) {
        meeting = UUID()
        notes = LiveNotes(); fresh = []; updatedAt = nil
        lastAttempt = Date(); inFlight = false
        isEnabled = enabled
        status = enabled ? .waiting : .off
    }

    /// Turned on or off during the meeting.
    func setEnabled(_ enabled: Bool) {
        guard enabled != isEnabled else { return }
        isEnabled = enabled
        status = enabled ? .waiting : .off
        if enabled { lastAttempt = .distantPast }
    }

    func end() {
        meeting = UUID()
        isEnabled = false
        if status != .off { status = notes.isEmpty ? .off : .waiting }
    }

    /// Called from the meeting's ticker; looks every two seconds.
    func tick(_ state: AppState) {
        guard isEnabled, state.isRunning, !state.isPaused, !inFlight, Date().timeIntervalSince(lastCheck) >= 2 else { return }
        lastCheck = Date()
        guard state.answerEngineReady else { status = .noEngine; return }
        if status == .noEngine { status = .waiting }
        let lines = state.entries.filter { $0.suspectedEcho != true }.count
        guard LiveNotesSchedule.isDue(notes: notes, lines: lines, sinceLastAttempt: Date().timeIntervalSince(lastAttempt)) else { return }
        update(state)
    }

    /// "现在更新": not waiting for the interval.
    func updateNow(_ state: AppState) {
        guard isEnabled, !inFlight, state.answerEngineReady else { return }
        update(state)
    }

    private func update(_ state: AppState) {
        let entries = state.entries.filter { $0.suspectedEcho != true }
        guard entries.count > notes.covered else { return }
        let began = state.sessionStartedAt ?? entries.first?.date ?? Date()
        let layout = SpeakerLayout(lines: state.liveSpeakerLines, voiceNames: state.voiceNames(for: nil), meVoices: state.speakers.meVoices)
        let lines = entries.map { entry -> LiveNotesPrompt.Line in
            let who: String
            if entry.isMe || layout.mine.contains(entry.id) { who = "我" }
            else if let number = layout.lineNumbers[entry.id] { who = SpeakerLabels.shortName(layout.label(number)) }
            else { who = entry.onSite == true ? "现场" : "对方" }
            return .init(seconds: Int(max(0, entry.date.timeIntervalSince(began))), speaker: who, text: entry.text)
        }
        let workspace = state.liveWorkspace
        let language = state.liveNotesLanguage
        let prompt = LiveNotesPrompt.user(notes: notes, lines: lines, language: language,
                                          domain: workspace.fields["domain"]?.trimmingCharacters(in: .whitespacesAndNewlines) ?? "")
        let model = state.currentAnswerModel, backend = state.answerBackend
        guard !model.isEmpty else { status = .noEngine; return }
        let request = AnswerRequest(system: LiveNotesPrompt.system, user: prompt, model: model,
                                    effort: backend == .openAICompatible ? state.codexReasoning : "low", serviceTier: state.activeServiceTier)
        let previous = notes, covered = lines.count, id = meeting, started = Date()
        inFlight = true
        lastAttempt = started
        status = .updating
        // One side request at a time: the slide notes use the same connection.
        let before = state.insights.sideChain
        state.insights.sideChain = Task { [weak self, weak state] in
            await before?.value
            guard let self, let state else { return }
            defer { self.inFlight = false }
            guard self.meeting == id, !Task.isCancelled else { return }
            do {
                let reply = try await state.runWebAnswer(request, backend: backend, forSlides: true)
                guard self.meeting == id else { return }
                guard let next = LiveNotesPrompt.parse(reply, previous: previous, lines: covered) else {
                    Diagnostics.record("livenotes.unreadable", ["characters": reply.count])
                    self.status = .failed
                    return
                }
                self.fresh = previous.isEmpty ? [] : next.statements.subtracting(previous.statements)
                self.notes = next
                self.updatedAt = Date()
                self.status = .waiting
                Diagnostics.record("livenotes.updated", ["lines": covered, "topics": next.topics.count, "seconds": Date().timeIntervalSince(started)])
            } catch {
                guard self.meeting == id, !Task.isCancelled else { return }
                Diagnostics.record("livenotes.failed")
                self.status = .failed
            }
        }
    }
}

@MainActor
extension AppState {
    /// The language live notes are written in: the notes setting, else the app's own.
    var liveNotesLanguage: String {
        switch notesLanguage {
        case "zh": return "简体中文"
        case "en": return "English"
        default: return AppLanguage.interfaceIsEnglish ? "English" : "简体中文"
        }
    }

    // MARK: What a meeting uses

    /// The scene of the meeting about to start (the draft's, the planned one's, the series').
    private var upcomingMode: SessionMode {
        meetingDraft?.mode ?? plannedMeeting?.effectiveMode ?? activeWorkspace.effectiveMode
    }

    private var featuresKey: String { "meetingFeatures." + (meetingDraft?.seriesID ?? activeWorkspace.id).uuidString }

    /// What the scene starts a meeting with when nothing was chosen. In a room people
    /// talk face to face: answers stay quiet unless asked for.
    var standardMeetingFeatures: MeetingFeatures {
        MeetingFeatures.standard(prompting: SceneSettings.shared.liveMode(for: upcomingMode) == .prompting && !isOfflineMeeting,
                                 speakers: speakers.enabled && speakers.modelAvailable)
    }

    /// What the next meeting uses: what the series used last time, else the scene's defaults.
    var meetingFeatures: MeetingFeatures {
        get {
            _ = meetingFeaturesRevision
            guard let data = UserDefaults.standard.data(forKey: featuresKey),
                  let saved = try? JSONDecoder().decode(MeetingFeatures.self, from: data) else { return standardMeetingFeatures }
            return saved
        }
        set {
            if let data = try? JSONEncoder().encode(newValue) { UserDefaults.standard.set(data, forKey: featuresKey) }
            meetingFeaturesRevision &+= 1
        }
    }

    /// Back to what the scene starts with.
    func resetMeetingFeatures() {
        UserDefaults.standard.removeObject(forKey: featuresKey)
        meetingFeaturesRevision &+= 1
    }
}
