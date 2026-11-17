import Foundation
import NextThoughtCore
import NaturalLanguage
import Observation
import SwiftUI
import Translation

/// The destination language is selected from the recognized utterance, never from
/// which audio device produced it.
enum TranslationDirection: CaseIterable, Hashable, Sendable {
    case englishToChinese
    case chineseToEnglish

    var sourceLanguage: Locale.Language {
        Locale.Language(identifier: self == .englishToChinese ? "en" : "zh-Hans")
    }

    var targetLanguage: Locale.Language {
        Locale.Language(identifier: self == .englishToChinese ? "zh-Hans" : "en")
    }

    var title: String { self == .englishToChinese ? "英译中" : "中译英" }

    static func detect(in text: String) -> Self? {
        let value = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty else { return nil }

        var han = 0
        var latin = 0
        for scalar in value.unicodeScalars {
            switch scalar.value {
            case 0x3400...0x4DBF, 0x4E00...0x9FFF, 0xF900...0xFAFF, 0x20000...0x2FA1F:
                han += 1
            case 0x0041...0x005A, 0x0061...0x007A, 0x00C0...0x024F, 0x1E00...0x1EFF:
                latin += 1
            default:
                break
            }
        }

        // Most speech is monolingual. Script counting avoids invoking a language
        // model for the common case and remains cheap on frequently revised partials.
        if han > 0, latin == 0 { return .chineseToEnglish }
        if latin > 0, han == 0 { return .englishToChinese }

        // Most mixed technical speech still has one clearly dominant language.
        // Resolve those cases by counting scripts and reserve NaturalLanguage for
        // genuinely ambiguous text, keeping partial-result detection inexpensive.
        if han > 0, latin > 0 {
            if han * 5 >= latin * 7 { return .chineseToEnglish }
            if latin * 5 >= han * 7 { return .englishToChinese }
            let recognizer = NLLanguageRecognizer()
            recognizer.processString(value)
            if let language = recognizer.dominantLanguage?.rawValue.lowercased() {
                if language.hasPrefix("zh") { return .chineseToEnglish }
                if language.hasPrefix("en") { return .englishToChinese }
            }
            if han != latin { return han > latin ? .chineseToEnglish : .englishToChinese }
        }
        return nil
    }
}

struct LiveTranslationJob: Sendable {
    let source: CaptureSource
    let text: String
    let direction: TranslationDirection
    let isFinal: Bool
    let entryID: UUID?
    let sessionID: UUID?
    let meetingID: UUID
    let revision: LiveTranslationRevision?
}

private enum LiveTranslationEvent: Sendable {
    case prepare
    case job(LiveTranslationJob)
    case drain(UUID)
}

/// Coordinates two local Translation sessions. Audio channels stay independent
/// for display, while each utterance can select either language direction.
@Observable
@MainActor
final class LiveTranslationCoordinator {
    private(set) var isEnabled = false
    private(set) var status = ""
    private(set) var systemPartialTranslation = ""
    private(set) var microphonePartialTranslation = ""

    @ObservationIgnored private var streams: [TranslationDirection: AsyncStream<LiveTranslationEvent>] = [:]
    @ObservationIgnored private var continuations: [TranslationDirection: AsyncStream<LiveTranslationEvent>.Continuation] = [:]
    @ObservationIgnored private var partialTasks: [CaptureSource: Task<Void, Never>] = [:]
    @ObservationIgnored private var partialTaskIDs: [CaptureSource: UUID] = [:]
    @ObservationIgnored private var partialTexts: [CaptureSource: String] = [:]
    @ObservationIgnored private var partialDirections: [CaptureSource: TranslationDirection] = [:]
    @ObservationIgnored private var partialRevisions: [CaptureSource: LiveTranslationRevisionGate] = [:]
    @ObservationIgnored private var readyDirections: Set<TranslationDirection> = []
    @ObservationIgnored private var meetingID = UUID()
    @ObservationIgnored private var drainedTranslationDirections: [UUID: Set<TranslationDirection>] = [:]

    init() {
        for direction in TranslationDirection.allCases {
            let channel = AsyncStream<LiveTranslationEvent>.makeStream(bufferingPolicy: .unbounded)
            streams[direction] = channel.stream
            continuations[direction] = channel.continuation
        }
    }

    /// What the user chose with the switch; nil until they touch it. Untouched, the
    /// interpretation turns itself on when the other side is heard speaking English.
    @ObservationIgnored private var choice = UserDefaults.standard.object(forKey: "liveTranslation") as? Bool
    @ObservationIgnored private var checkedForeign = false

    func setEnabled(_ enabled: Bool, remember: Bool = true) {
        if remember { choice = enabled; UserDefaults.standard.set(enabled, forKey: "liveTranslation") }
        guard isEnabled != enabled else { return }
        isEnabled = enabled
        if enabled {
            status = "正在准备中英互译…"
            // Prepare both directions together so the first utterance in either
            // language does not pay the model setup cost.
            for direction in TranslationDirection.allCases {
                continuations[direction]?.yield(.prepare)
            }
        } else {
            status = ""
            readyDirections.removeAll()
            clearPartial(.system)
            clearPartial(.microphone)
        }
    }

    /// The other side speaks English and nobody chose: a reader of Chinese gets the
    /// meaning without looking for a switch in the middle of a question. Only with the
    /// language packs already on this Mac, so no download sheet opens during a meeting.
    private func turnOnForForeignSpeech(_ source: CaptureSource, _ text: String) {
        guard !isEnabled, choice == nil, !checkedForeign, source == .system,
              Locale.preferredLanguages.first?.hasPrefix("zh") == true,
              TranslationDirection.detect(in: text) == .englishToChinese,
              text.split(whereSeparator: { $0 == " " }).count >= 4 else { return }
        guard #available(macOS 15.0, *) else { return }
        checkedForeign = true
        Task { [weak self] in
            let availability = LanguageAvailability()
            let english = TranslationDirection.englishToChinese, chinese = TranslationDirection.chineseToEnglish
            let there = await availability.status(from: english.sourceLanguage, to: english.targetLanguage) == .installed
            let back = await availability.status(from: chinese.sourceLanguage, to: chinese.targetLanguage) == .installed
            Diagnostics.record("translation.auto", ["installed": there && back])
            guard let self, there, back, self.choice == nil, AppState.shared.isRunning else { return }
            self.setEnabled(true, remember: false)
        }
    }

    func beginMeeting() {
        meetingID = UUID()
        checkedForeign = false
        // The choice of the last meeting holds for this one.
        if choice == true { setEnabled(true, remember: false) }
        clearPartial(.system)
        clearPartial(.microphone)
        if isEnabled { status = readyDirections.count == TranslationDirection.allCases.count ? "自动识别 · 中英互译" : "正在准备中英互译…" }
    }

    func finishMeeting(sessionID: UUID?) {
        clearPartial(.system)
        clearPartial(.microphone)
        if let sessionID {
            AppState.shared.archive.beginFinalTranslationDrain(for: sessionID)
            if isEnabled {
                drainedTranslationDirections[sessionID] = []
                for direction in TranslationDirection.allCases {
                    continuations[direction]?.yield(.drain(sessionID))
                }
            } else {
                Task { await AppState.shared.archive.finishFinalTranslationDrain(for: sessionID) }
            }
        }
        if isEnabled, readyDirections.count == TranslationDirection.allCases.count { status = "自动识别 · 中英互译" }
    }

    func partialTranslation(for source: CaptureSource) -> String {
        source == .system ? systemPartialTranslation : microphonePartialTranslation
    }

    func clearPartial(_ source: CaptureSource) {
        partialTasks[source]?.cancel()
        partialTasks[source] = nil
        partialTaskIDs[source] = nil
        partialTexts[source] = nil
        partialDirections[source] = nil
        partialRevisions[source, default: LiveTranslationRevisionGate()].invalidate()
        setPartialTranslation("", for: source)
    }

    func submitPartial(_ source: CaptureSource, text: String) {
        turnOnForForeignSpeech(source, text)
        guard isEnabled else { return }
        let text = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { clearPartial(source); return }
        guard partialTexts[source] != text else { return }
        partialTexts[source] = text

        if let detected = TranslationDirection.detect(in: text) {
            partialDirections[source] = detected
        }
        guard partialDirections[source] != nil else { return }
        partialRevisions[source, default: LiveTranslationRevisionGate()].revise()
        schedulePartialTranslations(for: source)
    }

    func submitFinal(_ source: CaptureSource, text: String, entryID: UUID, sessionID: UUID?) {
        turnOnForForeignSpeech(source, text)
        partialTasks[source]?.cancel()
        partialTasks[source] = nil
        partialTaskIDs[source] = nil
        let direction = TranslationDirection.detect(in: text) ?? partialDirections[source]
        partialRevisions[source, default: LiveTranslationRevisionGate()].invalidate()
        partialTexts[source] = nil
        partialDirections[source] = nil
        // The last partial has already been copied to the final transcript row.
        setPartialTranslation("", for: source)
        guard isEnabled, let direction else { return }
        enqueue(LiveTranslationJob(source: source, text: text, direction: direction, isFinal: true,
                                   entryID: entryID, sessionID: sessionID, meetingID: meetingID, revision: nil))
    }

    private func schedulePartialTranslations(for source: CaptureSource) {
        guard partialTasks[source] == nil else { return }
        let taskID = UUID()
        let meetingID = self.meetingID
        partialTaskIDs[source] = taskID
        partialTasks[source] = Task { [weak self] in
            var throttle = LiveTranslationPartialThrottle(interval: 0.45)
            var lastQueuedRevision: LiveTranslationRevision?
            while !Task.isCancelled {
                guard let self, self.isEnabled, self.meetingID == meetingID,
                      let text = self.partialTexts[source],
                      let direction = self.partialDirections[source],
                      let revision = self.partialRevisions[source]?.current else { break }
                if Self.isUsefulPartial(text), revision != lastQueuedRevision,
                   throttle.shouldDispatch(at: ProcessInfo.processInfo.systemUptime) {
                    self.enqueue(LiveTranslationJob(source: source, text: text, direction: direction, isFinal: false,
                                                    entryID: nil, sessionID: nil, meetingID: meetingID, revision: revision))
                    lastQueuedRevision = revision
                }
                do { try await Task.sleep(for: .milliseconds(80)) } catch { break }
            }
            guard let self, self.partialTaskIDs[source] == taskID else { return }
            self.partialTasks[source] = nil
            self.partialTaskIDs[source] = nil
        }
    }

    private static func isUsefulPartial(_ text: String) -> Bool {
        text.filter(\.isLetter).count >= 2
    }

    private func enqueue(_ job: LiveTranslationJob) {
        continuations[job.direction]?.yield(.job(job))
    }

    @available(macOS 15.0, *)
    func run(_ session: TranslationSession, direction: TranslationDirection) async {
        guard let stream = streams[direction] else { return }
        var prepared = false
        for await event in stream {
            if Task.isCancelled { return }
            switch event {
            case .prepare:
                do {
                    try await session.prepareTranslation()
                    prepared = true
                    readyDirections.insert(direction)
                    updateReadyStatus()
                } catch {
                    prepared = false
                    status = "翻译资源准备失败：\(error.localizedDescription)"
                }
            case let .job(job):
                guard isEnabled else { continue }
                if !job.isFinal, !isLatestPartial(job) { continue }
                do {
                    if !prepared {
                        try await session.prepareTranslation()
                        prepared = true
                        readyDirections.insert(direction)
                        updateReadyStatus()
                    }
                    if !job.isFinal, !isLatestPartial(job) { continue }
                    let response = try await session.translate(job.text)
                    let translation = response.targetText.trimmingCharacters(in: .whitespacesAndNewlines)
                    guard isEnabled, !translation.isEmpty else { continue }
                    if job.isFinal {
                        AppState.shared.receiveLiveTranslation(translation, job: job)
                    } else if acceptPartial(job) {
                        setPartialTranslation(translation, for: job.source)
                    }
                } catch {
                    status = "翻译暂不可用：\(error.localizedDescription)"
                }
            case let .drain(sessionID):
                markTranslationLaneDrained(sessionID, direction: direction)
            }
        }
    }

    private func markTranslationLaneDrained(_ sessionID: UUID, direction: TranslationDirection) {
        guard var drained = drainedTranslationDirections[sessionID] else { return }
        drained.insert(direction)
        guard drained.count == TranslationDirection.allCases.count else {
            drainedTranslationDirections[sessionID] = drained
            return
        }
        drainedTranslationDirections[sessionID] = nil
        Task { await AppState.shared.archive.finishFinalTranslationDrain(for: sessionID) }
    }

    private func isLatestPartial(_ job: LiveTranslationJob) -> Bool {
        guard job.meetingID == meetingID, let revision = job.revision,
              partialDirections[job.source] == job.direction,
              let gate = partialRevisions[job.source] else { return false }
        return gate.isLatest(revision)
    }

    private func acceptPartial(_ job: LiveTranslationJob) -> Bool {
        guard job.meetingID == meetingID, let revision = job.revision,
              partialTexts[job.source] != nil,
              partialDirections[job.source] == job.direction else { return false }
        var gate = partialRevisions[job.source] ?? LiveTranslationRevisionGate()
        guard gate.accept(revision) else { return false }
        partialRevisions[job.source] = gate
        return true
    }

    private func setPartialTranslation(_ value: String, for source: CaptureSource) {
        if source == .system {
            if systemPartialTranslation != value { systemPartialTranslation = value }
        } else if microphonePartialTranslation != value {
            microphonePartialTranslation = value
        }
    }

    private func updateReadyStatus() {
        guard isEnabled else { return }
        if readyDirections.count == TranslationDirection.allCases.count {
            status = "自动识别 · 中英互译"
        } else if status.isEmpty || status.hasPrefix("自动识别") {
            status = "正在准备中英互译…"
        }
    }
}

struct LiveTranslationToggle: View {
    @Environment(AppState.self) private var state

    var body: some View {
        if #available(macOS 15.0, *) {
            if state.liveTranslation.isEnabled {
                Button {
                    state.liveTranslation.setEnabled(false)
                } label: {
                    Label("同声传译", systemImage: "character.bubble.fill")
                }
                .buttonStyle(PrimaryButtonStyle())
                .help("自动识别我和对方说的中文或英文，并翻译成另一种语言")
            } else {
                Button {
                    state.liveTranslation.setEnabled(true)
                } label: {
                    Label("同声传译", systemImage: "character.bubble")
                }
                .buttonStyle(SecondaryButtonStyle())
                .help("自动识别我和对方说的中文或英文，并翻译成另一种语言")
            }
        } else {
            Button("同声传译") {}
                .buttonStyle(SecondaryButtonStyle())
                .disabled(true)
                .help("同声传译需要 macOS 15 或更新版本")
        }
    }
}

@available(macOS 15.0, *)
struct LiveTranslationHosts: View {
    var body: some View {
        ZStack {
            TranslationLaneHost(direction: .englishToChinese)
            TranslationLaneHost(direction: .chineseToEnglish)
        }
        .frame(width: 1, height: 1)
        .opacity(0)
        .allowsHitTesting(false)
        .accessibilityHidden(true)
    }
}

@available(macOS 15.0, *)
private struct TranslationLaneHost: View {
    @Environment(AppState.self) private var state
    let direction: TranslationDirection

    private var configuration: TranslationSession.Configuration {
        TranslationSession.Configuration(source: direction.sourceLanguage, target: direction.targetLanguage)
    }

    var body: some View {
        Color.clear
            .frame(width: 1, height: 1)
            .translationTask(configuration) { session in
                await state.liveTranslation.run(session, direction: direction)
            }
    }
}
