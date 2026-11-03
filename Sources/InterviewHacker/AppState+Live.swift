import AppKit
import Foundation
import InterviewHackerCore
import UniformTypeIdentifiers

@MainActor
extension AppState {
    // MARK: Question candidates

    /// Ticks the candidate tracker so a stable streaming question can trigger before
    /// the recognizer finalizes it, and releases questions held while the user talked.
    func startCandidateTicker() {
        candidateTicker?.cancel()
        candidateTicker = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(for: .milliseconds(200))
                guard let self, self.isRunning, !self.isPaused else { continue }
                self.applyCandidateDecision(self.tracker.tick(), allowAutomatic: true)
                self.releaseDeferredCandidate()
            }
        }
    }

    /// Only the system-audio channel (the other side) is fed here.
    func processRecognized(_ text: String, isFinal: Bool, key: String?, allowAutomatic: Bool) {
        tracker.mode = activeWorkspace.effectiveMode
        let decision = tracker.ingest(text, isFinal: isFinal, utteranceKey: key)
        if isFinal, decision == .none, liveCandidate?.utteranceKey == key || key == nil { liveCandidate = nil }
        applyCandidateDecision(decision, allowAutomatic: allowAutomatic)
    }

    func applyCandidateDecision(_ decision: CandidateDecision, allowAutomatic: Bool) {
        switch decision {
        case .none:
            break
        case let .updated(candidate):
            if liveCandidate != candidate { liveCandidate = candidate }
        case let .trigger(candidate):
            handleTrigger(candidate, allowAutomatic: allowAutomatic)
        case let .uncertain(candidate):
            remember(candidate)
            recordQuestion(candidate, disposition: "uncertain")
            if automaticAnswers, allowAutomatic, verifyUncertainWithModel, answerBackend == .openAICompatible {
                verify(candidate)
            }
        }
    }

    private func handleTrigger(_ candidate: QuestionCandidate, allowAutomatic: Bool) {
            liveCandidate = candidate
            Diagnostics.record("question.triggered", ["intent": candidate.assessment.intent.rawValue, "score": candidate.assessment.score, "final": candidate.isFinal, "revision": candidate.revision, "supersedes": candidate.supersedes != nil, "lead_in": candidate.leadIn != nil])
            let canAnswer = automaticAnswers && allowAutomatic && isRunning && !isPaused && !isStopping
            if canAnswer, isUserSpeaking {
                // Do not talk over the user; answer if they stop within a few seconds.
                deferredCandidate = (candidate, Date())
                remember(candidate)
                recordQuestion(candidate, disposition: "deferred")
            } else if canAnswer {
                answerAutomatically(candidate)
            } else {
                remember(candidate)
                recordQuestion(candidate, disposition: "suggested")
            }
    }

    private func answerAutomatically(_ candidate: QuestionCandidate) {
        // The speaker kept talking after an early trigger: replace that answer with one
        // for the complete question instead of queueing a second answer.
        let supersedesCurrent = candidate.supersedes != nil && (candidate.supersedes == answeringCandidateID
            || (candidate.supersedes == lastAutomaticAnswer?.candidateID && Date().timeIntervalSince(lastAutomaticAnswer?.date ?? .distantPast) < 20))
        recordQuestion(candidate, disposition: "auto")
        ask(candidate.fullText, screenshot: nil,
            recentContext: TranscriptContextBuilder.recent(contextEntries.map { (date: $0.date, text: $0.contextualText) }),
            isAutomatic: true, candidateID: candidate.id, supersede: supersedesCurrent)
        if supersedesCurrent { Diagnostics.record("answer.superseded") }
    }

    private func releaseDeferredCandidate() {
        guard let deferred = deferredCandidate else { return }
        if Date().timeIntervalSince(deferred.date) > 6 { deferredCandidate = nil; return }
        guard !isUserSpeaking else { return }
        deferredCandidate = nil
        suggestedQuestions.removeAll { $0.id == deferred.candidate.id }
        guard automaticAnswers, isRunning, !isPaused, !isStopping else { return }
        _ = tracker.confirm(deferred.candidate)
        ask(deferred.candidate.fullText, screenshot: nil, isAutomatic: true, candidateID: deferred.candidate.id)
    }

    /// The user is talking (mic partial present and not just speaker echo).
    var isUserSpeaking: Bool {
        microphoneEnabled && !microphonePartialTranscript.isEmpty && !(echoSuppression && echoMonitor.microphoneIsEcho)
    }

    private func remember(_ candidate: QuestionCandidate) {
        suggestedQuestions.removeAll { $0.text == candidate.text }
        suggestedQuestions.append(candidate)
        if suggestedQuestions.count > 4 { suggestedQuestions.removeFirst(suggestedQuestions.count - 4) }
    }

    func answerCandidate(_ candidate: QuestionCandidate) {
        _ = tracker.confirm(candidate)
        suggestedQuestions.removeAll { $0.id == candidate.id }
        if deferredCandidate?.candidate.id == candidate.id { deferredCandidate = nil }
        recordQuestion(candidate, disposition: "manual")
        ask(candidate.fullText, screenshot: nil, candidateID: candidate.id)
    }

    func dismissCandidate(_ candidate: QuestionCandidate) {
        suggestedQuestions.removeAll { $0.id == candidate.id }
        if deferredCandidate?.candidate.id == candidate.id { deferredCandidate = nil }
    }

    /// Second layer for uncertain utterances: a fast API model under a short deadline;
    /// the rules stand otherwise.
    private func verify(_ candidate: QuestionCandidate) {
        let request = AnswerRequest(
            system: "你判断会议转录中的一句话是否是需要回答的提问或请求。只输出 yes 或 no。",
            user: "上下文：\(String(contextEntries.suffix(4).map(\.contextualText).joined(separator: "\n").suffix(800)))\n待判断：\(candidate.fullText)",
            model: apiModel, effort: "low", imageURL: nil)
        let client = apiClient, base = apiBaseURL, key = apiKey
        Task {
            let verdict = try? await withThrowingTaskGroup(of: String.self) { group in
                group.addTask { try await client.answer(request, baseURL: base, apiKey: key) { _ in } }
                group.addTask { try await Task.sleep(for: .seconds(3)); throw CancellationError() }
                defer { group.cancelAll() }
                return try await group.next() ?? ""
            }
            guard let verdict, verdict.lowercased().contains("yes"), isRunning, !isPaused, !isAnswering else { return }
            if tracker.confirm(candidate) {
                suggestedQuestions.removeAll { $0.id == candidate.id }
                ask(candidate.fullText, screenshot: nil, isAutomatic: true, candidateID: candidate.id)
            }
        }
    }

    // MARK: Recognizer results

    func receiveStreamingResult(_ text: String, final: Bool, start: Date, end: Date, source: CaptureSource,
                               inferenceSeconds: Double? = nil, audioSeconds: Double? = nil) {
        if let inferenceSeconds, let audioSeconds, source == .system {
            lastASRSeconds = inferenceSeconds
            lastRTF = inferenceSeconds / max(0.001, audioSeconds)
        }
        if final, let local = liveTranscriber as? ZipformerStreamingTranscriber, source == .system, let refine = local.lastRefineSeconds {
            lastRefineSeconds = refine
        }
        let key = "\(source.rawValue)-\(appleUtteranceCounter[source.rawValue, default: 0])"
        if final {
            (source == .system ? partialThrottle : micPartialThrottle).cancel()
            if source == .system { partialTranscript = "" } else { microphonePartialTranscript = "" }
            appleUtteranceCounter[source.rawValue, default: 0] += 1
            var entry = TranscriptEntry(date: start, source: source.rawValue, text: text, isFinal: true, startedAt: start, endedAt: end)
            if liveTranslation.isEnabled {
                let provisionalTranslation = liveTranslation.partialTranslation(for: source)
                if !provisionalTranslation.isEmpty { entry.translatedText = provisionalTranslation }
            }
            // In the room the microphone hears everyone: "现场", and there is no echo to drop.
            if source == .microphone, isOfflineMeeting { entry.onSite = true }
            if source == .microphone, !isOfflineMeeting, echoSuppression, isEcho(text, start: start, end: end) {
                echoDroppedCount += 1
                entry.suspectedEcho = true
                Diagnostics.record("microphone.echo_dropped", ["characters": text.count])
            }
            // Results arrive in order per channel; only a late result needs sorting.
            if let last = entries.last, last.date > start {
                let index = entries.lastIndex { $0.date <= start }.map { $0 + 1 } ?? 0
                entries.insert(entry, at: index)
            } else {
                entries.append(entry)
            }
            if entries.count > 400 { entries.removeFirst(entries.count - 400) }
            if entry.suspectedEcho != true {
                recordTranscript(entry)
                insightsDidReceive(entry)
                liveTranslation.submitFinal(source, text: text, entryID: entry.id, sessionID: activeArchiveID)
            } else {
                liveTranslation.clearPartial(source)
            }
            transcriptionStatus = statusLine
        } else {
            // Streaming recognizers revise partial text ~10×/s; coalesce to ≤5 updates/s.
            // Finals are never delayed.
            (source == .system ? partialThrottle : micPartialThrottle).run { [weak self] in
                guard let self, self.isRunning else { return }
                if source == .system {
                    if self.partialTranscript != text { self.partialTranscript = text }
                    self.liveTranslation.submitPartial(source, text: text)
                    if !self.isPaused, !self.isStopping { self.processRecognized(text, isFinal: false, key: key, allowAutomatic: true) }
                } else {
                    // Speaker echo is not the user talking: keep it out of the transcript.
                    let echo = !self.isOfflineMeeting && self.echoSuppression && self.echoMonitor.microphoneIsEcho
                    let shown = echo ? "" : text
                    if self.microphonePartialTranscript != shown { self.microphonePartialTranscript = shown }
                    if echo { self.liveTranslation.clearPartial(source) }
                    else { self.liveTranslation.submitPartial(source, text: shown) }
                }
            }
            return
        }
        if source == .system, !isPaused, !isStopping {
            processRecognized(text, isFinal: final, key: key, allowAutomatic: true)
        }
    }

    /// Microphone text that repeats what the other side said moments ago, or arrives
    /// while the microphone envelope follows the system audio, is speaker echo.
    private func isEcho(_ text: String, start: Date, end: Date) -> Bool {
        var lines = entries.suffix(16).filter { $0.source == CaptureSource.system.rawValue }
            .map { TimedLine(text: $0.text, start: $0.startedAt ?? $0.date, end: $0.endedAt ?? $0.date) }
        if !partialTranscript.isEmpty { lines.append(TimedLine(text: partialTranscript, start: Date().addingTimeInterval(-4), end: Date())) }
        return EchoTextFilter.isEcho(microphone: text, start: start, end: end, system: lines,
                                     acousticEchoFraction: echoMonitor.echoFraction(from: start, to: end))
    }

    /// Lines that feed answers, summaries and exports (suspected echo left out).
    var contextEntries: [TranscriptEntry] { entries.filter { $0.suspectedEcho != true } }

    /// The user says a dimmed line was really them.
    func markNotEcho(_ id: UUID) {
        guard let index = entries.firstIndex(where: { $0.id == id }), entries[index].suspectedEcho == true else { return }
        entries[index].suspectedEcho = nil
        echoDroppedCount = max(0, echoDroppedCount - 1)
        recordTranscript(entries[index])
        Diagnostics.record("microphone.echo_restored")
    }

    private var statusLine: String {
        switch transcriptionBackend {
        case "zipformer":
            var parts = [localStreamingModel == .legacy ? "Zipformer 本地识别" : "X-ASR 本地识别"]
            if let rtf = lastRTF { parts.append(String(format: "RTF %.2f", rtf)) }
            if refinerModel != nil, let refine = lastRefineSeconds { parts.append(String(format: "精修 %.2f s", refine)) }
            return parts.joined(separator: " · ")
        case "cloud": return "云端流式识别 · \(cloudProvider.title)"
        default: return "Apple Speech · 双路独立识别"
        }
    }

    // MARK: Answer actions

    func refine(_ refinement: AnswerRefinement) {
        let base = displayedQuestion.isEmpty ? currentQuestion : displayedQuestion
        guard !base.isEmpty, !displayedAnswer.isEmpty else { errorMessage = "还没有可以调整的回答。"; return }
        ask(refinement.question(for: base), screenshot: nil)
    }

    func toggleClickThrough() {
        clickThrough.toggle()
        TeleprompterController.shared.applyClickThrough(clickThrough)
    }

    // MARK: Settings

    func loadCloudKey() { cloudKey = KeychainStore.read(cloudProvider.keychainAccount) }

    func saveAnswerSettings() {
        UserDefaults.standard.set(answerBackend.rawValue, forKey: "answerBackend")
        UserDefaults.standard.set(apiBaseURL, forKey: "apiBaseURL")
        UserDefaults.standard.set(apiModel, forKey: "apiModel")
        storeActiveAPIPreset()
        UserDefaults.standard.set(verifyUncertainWithModel, forKey: "verifyUncertainWithModel")
        UserDefaults.standard.set(cloudProvider.rawValue, forKey: "cloudProvider")
        if cloudProvider.isRemovedWhisperModelIdentifier(cloudModel) { cloudModel = "" }
        UserDefaults.standard.set(cloudModel, forKey: "cloudModel")
        UserDefaults.standard.set(cloudConsent, forKey: "cloudConsent")
        UserDefaults.standard.set(teleprompterOpacity, forKey: "teleprompterOpacity")
        UserDefaults.standard.set(teleprompterFontSize, forKey: "teleprompterFontSize")
        UserDefaults.standard.set(teleprompterHideBackground, forKey: "teleprompterHideBackground")
        UserDefaults.standard.set(simplifiedInterface, forKey: "simplifiedInterface")
        UserDefaults.standard.set(!simplifiedInterface, forKey: "advancedSettingsEnabled")
        UserDefaults.standard.set(capturePrivacy, forKey: "capturePrivacy")
        TeleprompterController.shared.applyAppearance()
    }

    func saveAPIKey() {
        KeychainStore.write(apiKey.trimmingCharacters(in: .whitespacesAndNewlines), account: activeAPIPreset?.keyAccount ?? "answer.api")
    }
    func saveCloudKey() { KeychainStore.write(cloudKey.trimmingCharacters(in: .whitespacesAndNewlines), account: cloudProvider.keychainAccount) }

    // MARK: Companion

    func setCompanion(enabled: Bool) {
        companionRelayTask?.cancel(); companionRelayTask = nil
        companionBackupAddress = nil
        companionEnabled = enabled
        if enabled {
            UserDefaults.standard.set(companionPort, forKey: "companionPort")
            companionStatus = String(localized: "正在启动…")
            CompanionServer.shared.start(preferredPort: companionPort) { [weak self] status, address in
                self?.companionStatus = status
                self?.companionAddress = address
                if address != nil, self?.companionBluetoothEnabled == true { self?.setCompanionBluetooth(enabled: true) }
                if address != nil, self?.companionRelayEnabled == true { self?.setCompanionRelay(enabled: true) }
            }
        } else {
            CompanionServer.shared.stop()
            companionAddress = nil
            companionStatus = String(localized: "未开启")
        }
    }

    func setCompanionBluetooth(enabled: Bool) {
        companionBluetoothEnabled = enabled
        CompanionServer.shared.bluetooth.status = { [weak self] in self?.companionBluetoothStatus = $0 }
        CompanionServer.shared.setBluetooth(enabled: enabled && companionEnabled)
        if !enabled { companionBluetoothStatus = String(localized: "未开启") }
    }

    func setCompanionRelay(enabled: Bool) {
        companionRelayTask?.cancel(); companionRelayEnabled = enabled; companionBackupAddress = nil
        CompanionServer.shared.disableRelay()
        companionAddress = CompanionServer.shared.pairingURL?.absoluteString
        guard enabled else { companionRelayStatus = String(localized: "未开启"); return }
        guard companionEnabled, let url = URL(string: companionRelayURL), url.scheme == "https",
              ["", "/"].contains(url.path) else { companionRelayStatus = "请先开启手机伴侣，并填写 HTTPS 服务根地址"; return }
        UserDefaults.standard.set(companionRelayURL, forKey: "companionRelayURL")
        companionRelayStatus = "正在连接中转…"
        CompanionServer.shared.relay.status = { [weak self] in self?.companionRelayStatus = $0 }
        companionRelayTask = Task { [weak self] in
            do {
                let backup = try await CompanionServer.shared.configureRelay(base: url)
                guard !Task.isCancelled, let self, self.companionEnabled, self.companionRelayEnabled else { return }
                self.companionBackupAddress = backup.absoluteString
                self.companionAddress = CompanionServer.shared.pairingURL?.absoluteString
            } catch { self?.companionRelayStatus = "中转未就绪：\(error.localizedDescription)" }
        }
    }

    func performCompanionAction(_ action: String) {
        switch action {
        case "answer": forceAnswerCurrent()
        case "cancel": cancelAnswer()
        case "regenerate": regenerate()
        case "previous": showPreviousAnswer()
        case "next": showNextAnswer()
        case "applyWeb": toggleWebAnswer()
        default:
            if let refinement = AnswerRefinement(rawValue: action) { refine(refinement) }
        }
    }

    var companionSnapshot: CompanionSnapshot {
        CompanionSnapshot(
            status: isRunning ? (isPaused ? "已暂停" : "正在聆听") : "未采集",
            question: displayedQuestion.isEmpty ? pendingQuestion : displayedQuestion,
            answer: displayedAnswer,
            answering: isAnswering && historyCursor == nil,
            candidate: liveCandidate.map { "\($0.assessment.intent.title) · \($0.text)" } ?? "",
            position: historyPositionText ?? "",
            sessionID: activeArchiveID?.uuidString,
            answerID: displayedRecordID?.uuidString,
            phase: historyCursor == nil ? (answerCoordinator.state?.phase.rawValue ?? "waiting") : "complete",
            sources: selectedExcerpts.map(\.citation).joined(separator: " · "),
            slide: insights.slide.map { "\($0.documentName) · \($0.page)" },
            updateAvailable: displayedWebLookup?.status == .done && !showsWebAnswer)
    }

    // MARK: Share-visibility self test

    /// Captures the main display the same way ScreenCaptureKit-based meeting apps do
    /// and shows it, so the user can see whether the overlay would be shared.
    func runShareVisibilityTest() {
        Task {
            do {
                let displays = try await ScreenshotService.availableDisplays()
                guard let display = displays.first else { return }
                TeleprompterController.shared.show()
                try? await Task.sleep(for: .milliseconds(300))
                shareTestImage = try await ScreenshotService.captureDisplay(display.id, hidingAppWindows: false)
            } catch {
                errorMessage = "共享可见性测试失败：\(error.localizedDescription)"
            }
        }
    }
}
