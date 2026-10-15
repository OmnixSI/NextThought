import AppKit
import Foundation
import InterviewHackerCore
import UniformTypeIdentifiers

@MainActor
extension AppState {
    // MARK: Question candidates

    /// Ticks the candidate tracker so a stable streaming question can trigger before
    /// the recognizer finalizes it.
    func startCandidateTicker() {
        candidateTicker?.cancel()
        candidateTicker = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(for: .milliseconds(250))
                guard let self, self.isRunning, !self.isPaused else { continue }
                self.applyCandidateDecision(self.tracker.tick(), allowAutomatic: true)
            }
        }
    }

    /// Only the system-audio channel (the other side) is fed here.
    func processRecognized(_ text: String, isFinal: Bool, key: String?, allowAutomatic: Bool) {
        tracker.mode = sessionMode
        let decision = tracker.ingest(text, isFinal: isFinal, utteranceKey: key)
        if isFinal, decision == .none, liveCandidate?.utteranceKey == key || key == nil { liveCandidate = nil }
        applyCandidateDecision(decision, allowAutomatic: allowAutomatic)
    }

    func applyCandidateDecision(_ decision: CandidateDecision, allowAutomatic: Bool) {
        switch decision {
        case .none:
            break
        case let .updated(candidate):
            liveCandidate = candidate
        case let .trigger(candidate):
            liveCandidate = candidate
            Diagnostics.record("question.triggered", ["intent": candidate.assessment.intent.rawValue, "score": candidate.assessment.score, "final": candidate.isFinal, "revision": candidate.revision])
            if automaticAnswers, allowAutomatic, isRunning, !isPaused, !isStopping, !isUserSpeaking {
                ask(candidate.text, screenshot: nil, recentContext: TranscriptContextBuilder.recent(entries.map { (date: $0.date, text: $0.contextualText) }), isAutomatic: true)
            } else {
                remember(candidate)
            }
        case let .uncertain(candidate):
            remember(candidate)
            if verifyUncertainWithModel, answerBackend == .openAICompatible, automaticAnswers, allowAutomatic {
                verify(candidate)
            }
        }
    }

    /// The user is talking (mic partial present): do not auto-answer over them.
    var isUserSpeaking: Bool { microphoneEnabled && !microphonePartialTranscript.isEmpty }

    private func remember(_ candidate: QuestionCandidate) {
        suggestedQuestions.removeAll { $0.text == candidate.text }
        suggestedQuestions.append(candidate)
        if suggestedQuestions.count > 4 { suggestedQuestions.removeFirst(suggestedQuestions.count - 4) }
    }

    func answerCandidate(_ candidate: QuestionCandidate) {
        _ = tracker.confirm(candidate)
        suggestedQuestions.removeAll { $0.id == candidate.id }
        ask(candidate.text, screenshot: nil)
    }

    func dismissCandidate(_ candidate: QuestionCandidate) {
        suggestedQuestions.removeAll { $0.id == candidate.id }
    }

    /// Second layer: ask a fast API model whether an uncertain utterance needs an answer.
    private func verify(_ candidate: QuestionCandidate) {
        let request = AnswerRequest(
            system: "你判断会议转录中的一句话是否是需要回答的提问或请求。只输出 yes 或 no。",
            user: "上下文：\(String(entries.suffix(4).map(\.contextualText).joined(separator: "\n").suffix(800)))\n待判断：\(candidate.text)",
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
                ask(candidate.text, screenshot: nil, isAutomatic: true)
            }
        }
    }

    // MARK: Streaming recognizers (Apple / cloud)

    func receiveStreamingResult(_ text: String, final: Bool, start: Date, end: Date, source: CaptureSource,
                               inferenceSeconds: Double? = nil, audioSeconds: Double? = nil) {
        if let inferenceSeconds, let audioSeconds {
            lastASRSeconds = inferenceSeconds
            lastRTF = inferenceSeconds / max(0.001, audioSeconds)
            transcriptionStatus = String(format: "Zipformer 流式识别 · 推理 %.2f s · RTF %.2f", inferenceSeconds, lastRTF ?? 0)
        }
        let key = "\(source.rawValue)-\(appleUtteranceCounter[source.rawValue, default: 0])"
        if final {
            (source == .system ? partialThrottle : micPartialThrottle).cancel()
            if source == .system { partialTranscript = "" } else { microphonePartialTranscript = "" }
            entries.append(TranscriptEntry(date: start, source: source.rawValue, text: text, isFinal: true, startedAt: start, endedAt: end))
            entries.sort { $0.date < $1.date }
            if entries.count > 400 { entries.removeFirst(entries.count - 400) }
            if inferenceSeconds == nil {
                transcriptionStatus = transcriptionBackend == "cloud" ? "云端流式识别 · \(cloudProvider.title)" : "Apple Speech · 双路独立识别"
            }
            appleUtteranceCounter[source.rawValue, default: 0] += 1
        } else {
            // Streaming recognizers revise partial text ~10×/s. Each UI change makes the
            // window server recomposite every glass layer, so partials are coalesced to
            // ≤4 updates/s (finals are never delayed).
            (source == .system ? partialThrottle : micPartialThrottle).run { [weak self] in
                guard let self, self.isRunning else { return }
                if source == .system {
                    self.partialTranscript = text
                    if !self.isPaused, !self.isStopping { self.processRecognized(text, isFinal: false, key: key, allowAutomatic: true) }
                } else {
                    self.microphonePartialTranscript = text
                }
            }
            return
        }
        if source == .system, !isPaused, !isStopping {
            processRecognized(text, isFinal: final, key: key, allowAutomatic: true)
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
        UserDefaults.standard.set(verifyUncertainWithModel, forKey: "verifyUncertainWithModel")
        UserDefaults.standard.set(cloudProvider.rawValue, forKey: "cloudProvider")
        if cloudProvider.isRemovedWhisperModelIdentifier(cloudModel) { cloudModel = "" }
        UserDefaults.standard.set(cloudModel, forKey: "cloudModel")
        UserDefaults.standard.set(cloudConsent, forKey: "cloudConsent")
        UserDefaults.standard.set(teleprompterOpacity, forKey: "teleprompterOpacity")
        UserDefaults.standard.set(teleprompterFontSize, forKey: "teleprompterFontSize")
        UserDefaults.standard.set(capturePrivacy, forKey: "capturePrivacy")
        TeleprompterController.shared.applyAppearance()
    }

    func saveAPIKey() { KeychainStore.write(apiKey.trimmingCharacters(in: .whitespacesAndNewlines), account: "answer.api") }
    func saveCloudKey() { KeychainStore.write(cloudKey.trimmingCharacters(in: .whitespacesAndNewlines), account: cloudProvider.keychainAccount) }

    // MARK: Companion

    func setCompanion(enabled: Bool) {
        companionEnabled = enabled
        if enabled {
            UserDefaults.standard.set(companionPort, forKey: "companionPort")
            companionStatus = "正在启动…"
            CompanionServer.shared.start(preferredPort: companionPort) { [weak self] status, address in
                self?.companionStatus = status
                self?.companionAddress = address
            }
        } else {
            CompanionServer.shared.stop()
            companionAddress = nil
            companionStatus = "未开启"
        }
    }

    func performCompanionAction(_ action: String) {
        switch action {
        case "answer": forceAnswerCurrent()
        case "cancel": cancelAnswer()
        case "regenerate": regenerate()
        case "previous": showPreviousAnswer()
        case "next": showNextAnswer()
        default:
            if let refinement = AnswerRefinement(rawValue: action) { refine(refinement) }
        }
    }

    var companionSnapshot: CompanionSnapshot {
        CompanionSnapshot(
            status: isRunning ? (isPaused ? "已暂停" : "正在聆听") : "未采集",
            question: displayedQuestion.isEmpty ? pendingQuestion : displayedQuestion,
            answer: displayedAnswer,
            answering: isAnswering,
            candidate: liveCandidate.map { "\($0.assessment.intent.title) · \($0.text)" } ?? "",
            position: historyPositionText ?? "")
    }

    // MARK: History

    func deleteSession(_ id: UUID) {
        savedSessions.removeAll { $0.id == id }
        persistSessions()
    }

    func exportSession(_ session: SavedSession) {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [UTType(filenameExtension: "md") ?? .plainText]
        panel.nameFieldStringValue = "InterviewHacker-\(session.startedAt.formatted(.iso8601.year().month().day())).md"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        var lines = ["# InterviewHacker 会话 \(session.startedAt.formatted(date: .abbreviated, time: .shortened))", ""]
        if let records = session.records, !records.isEmpty {
            lines.append("## 问答")
            for record in records {
                lines += ["", "### \(record.question)", "", record.answer]
            }
            lines.append("")
        }
        lines.append("## 转录")
        lines += session.entries.map { "- [\($0.date.formatted(date: .omitted, time: .shortened))] \($0.roleLabel)：\($0.text)" }
        do { try lines.joined(separator: "\n").write(to: url, atomically: true, encoding: .utf8) }
        catch { errorMessage = "导出失败：\(error.localizedDescription)" }
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
