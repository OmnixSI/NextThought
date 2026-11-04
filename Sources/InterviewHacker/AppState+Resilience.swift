import Foundation
import InterviewHackerCore

/// Keeps a session alive through audio device changes and keeps recognizers warm
/// so "开始辅助" is fast.
@MainActor
extension AppState {
    // MARK: Device changes

    /// System audio stopped (output device switched, display slept, window closed…).
    /// Reconnect with backoff, falling back to whole-screen audio if the window is gone.
    func recoverSystemCapture(_ message: String) {
        guard isRunning, !isPaused, !isStopping, systemRecoveryTask == nil else { return }
        Diagnostics.record("capture.recovering", ["message_characters": message.count])
        transcriptionStatus = "音频设备变化，正在自动重新连接…"
        let epoch = audioEpoch
        systemRecoveryTask = Task { [weak self] in
            defer { self?.systemRecoveryTask = nil }
            for delay in [0.3, 1.0, 2.0, 4.0, 8.0] {
                try? await Task.sleep(for: .seconds(delay))
                guard let self, self.isRunning, !self.isPaused, !self.isStopping, self.audioEpoch == epoch else { return }
                _ = await self.audio.stopCapture()
                await self.liveTranscriber?.reset()
                await self.refreshTargets()
                if !self.captureTargets.contains(where: { $0.id == self.selectedTargetID }) {
                    // The window disappeared: keep listening to the whole screen instead.
                    if let display = self.captureTargets.first(where: { $0.id.hasPrefix("display:") }) ?? self.captureTargets.first {
                        self.selectedTargetID = display.id
                    }
                }
                do {
                    try await self.audio.startCapture(targetID: self.selectedTargetID)
                    self.transcriptionStatus = "已自动重新连接系统音频"
                    Diagnostics.record("capture.recovered")
                    return
                } catch {
                    Diagnostics.record("capture.recover_failed")
                }
            }
            guard let self, self.isRunning, self.audioEpoch == epoch else { return }
            self.errorMessage = "系统音频多次重连失败：\(message)。转写已暂停，可检查音源后点“恢复”。"
            await self.pauseSession()
        }
    }

    /// The microphone could not be (re)started. Keep the session running on system audio.
    func microphoneUnavailable(_ message: String) {
        Diagnostics.record("microphone.unavailable")
        microphonePartialTranscript = ""
        transcriptionStatus = "麦克风暂不可用，继续识别系统声音；接入新设备后会自动恢复"
        if isRunning { errorMessage = message + "。辅助未中断，仍在识别对方声音。" }
    }

    // MARK: Warm start

    /// Loads the local model ahead of time or prepares Apple Speech assets.
    func prewarmRecognizer() async {
        guard keepRecognizerWarm, !isRunning else { return }
        switch transcriptionBackend {
        case "zipformer":
            guard isModelInstalled else { return }
            let started = Date()
            do {
                let hotwords = recognitionHotwords
                try await ZipformerRuntime.shared.configure(model: localStreamingModel.onlineModel, hotwords: hotwords)
                if let refiner = refinerModel {
                    try await OfflineSegmentRecognizer.shared.configure(model: refiner, hotwords: hotwords)
                }
                Diagnostics.record("zipformer.prewarmed", ["seconds": Date().timeIntervalSince(started)])
            } catch {
                Diagnostics.record("zipformer.prewarm_failed")
            }
        case "apple":
            await LiveTranscriberFactory.prewarm(language: recognitionLanguage)
        default:
            break
        }
    }

    /// Unload the recognizer after 15 idle minutes (or at once if warm-keeping is off).
    func scheduleRecognizerIdleShutdown() {
        recognizerIdleTask?.cancel()
        guard keepRecognizerWarm else {
            Task { await ZipformerRuntime.shared.unload(); await OfflineSegmentRecognizer.shared.unload() }
            return
        }
        recognizerIdleTask = Task { [weak self] in
            try? await Task.sleep(for: .seconds(15 * 60))
            guard !Task.isCancelled, let self, !self.isRunning else { return }
            await ZipformerRuntime.shared.unload()
            await OfflineSegmentRecognizer.shared.unload()
            Diagnostics.record("zipformer.idle_unloaded")
        }
    }

    func saveRecognizerSettings() {
        UserDefaults.standard.set(keepRecognizerWarm, forKey: "keepRecognizerWarm")
        if keepRecognizerWarm { Task { await prewarmRecognizer() } }
        else if !isRunning { Task { await ZipformerRuntime.shared.unload(); await OfflineSegmentRecognizer.shared.unload() } }
    }
}
