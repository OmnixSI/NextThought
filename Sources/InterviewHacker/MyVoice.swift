import AVFoundation
import Foundation
import InterviewHackerCore
import Observation

// 我的声音: the user reads a few sentences once, the app keeps one averaged voiceprint on this Mac
// (`Application Support/InterviewHacker/Voiceprint/me.json`, 0600), and in a meeting the voice that
// matches it is shown as "我" (see `SpeakerVoices`). A voiceprint is biometric data, so: it is only
// ever written to that one file, never uploaded, never put in a backup or an export, and deleting it
// removes the file. The recordings themselves are not kept; only their 256-number voiceprints exist
// while the setup runs.

/// The one file the voiceprint lives in.
enum VoiceProfileStore {
    static var fileURL: URL {
        AppPaths.root.appendingPathComponent("Voiceprint", isDirectory: true).appendingPathComponent("me.json")
    }

    static func load() -> VoiceProfile? {
        guard !DemoMode.isEnabled, let data = try? Data(contentsOf: fileURL) else { return nil }
        return try? JSONLines.decoder().decode(VoiceProfile.self, from: data)
    }

    static func save(_ profile: VoiceProfile) throws {
        let directory = fileURL.deletingLastPathComponent()
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true,
                                                attributes: [.posixPermissions: 0o700])
        try JSONLines.encoder().encode(profile).write(to: fileURL, options: .atomic)
        try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: fileURL.path)
    }

    static func delete() {
        try? FileManager.default.removeItem(at: fileURL.deletingLastPathComponent())
    }
}

/// Collects the 16 kHz samples the microphone service delivers; read after capture stops.
private final class TakeCollector: @unchecked Sendable {
    private let lock = NSLock()
    private var samples: [Int16] = []

    func append(_ chunk: [Int16]) {
        lock.lock(); samples.append(contentsOf: chunk); lock.unlock()
    }

    func floats() -> [Float] {
        lock.lock(); defer { lock.unlock() }
        return samples.map { Float($0) / 32768 }
    }
}

/// Setting up and holding 我的声音. One instance, `MyVoice.shared`.
@MainActor
@Observable
final class MyVoice {
    static let shared = MyVoice()

    /// Sentences read aloud, one recording each. Plain, a little varied in sound, and about seven seconds long.
    static let prompts = [
        "今天上午我们先把上周的进展过一遍，然后讨论接下来两周的安排。",
        "这个问题我的理解是，先确认需求，再决定用哪种方案去做。",
        "Let me walk you through what I did on the project and why.",
        "如果时间允许，我想再补充一下数据和风险这两部分。",
    ]
    /// How long each recording runs.
    static let takeSeconds = 7.0
    /// A recording with less speech than this (after cutting the silence) is asked for again.
    static let minSpeechSeconds = 3.0

    private(set) var profile: VoiceProfile?
    /// Voiceprints of the recordings made so far in this setup.
    private(set) var takes: [[Float]] = []
    private(set) var isRecording = false
    private(set) var isWorking = false
    /// 0...1 while a recording runs.
    private(set) var progress = 0.0
    /// What to tell the user (a recording was too quiet, the microphone is not allowed…).
    var message: String?
    /// The setup is open: sentences are shown and recordings are being made.
    private(set) var isSettingUp = false

    @ObservationIgnored private var model: Voiceprints?
    @ObservationIgnored private var recorder: MicrophoneCaptureService?

    private init() { profile = VoiceProfileStore.load() }

    /// The saved voiceprint if it can be used with this build's model.
    var usableProfile: VoiceProfile? { profile.flatMap { $0.isUsable ? $0 : nil } }
    var isSaved: Bool { profile != nil }
    var modelAvailable: Bool { Voiceprints.isBundled }
    /// The sentence to read now.
    var currentPrompt: String { Self.prompts[min(takes.count, Self.prompts.count - 1)] }

    // MARK: Setup

    func beginSetup() {
        takes = []
        message = nil
        progress = 0
        isSettingUp = true
    }

    func cancelSetup() {
        recorder.map { _ = $0.stopCapture() }
        recorder = nil
        isRecording = false
        isSettingUp = false
        takes = []
        progress = 0
        message = nil
    }

    /// Records the current sentence for `takeSeconds`, turns it into a voiceprint, and, with the last
    /// sentence done, builds and saves the profile.
    func recordTake() async {
        guard isSettingUp, !isRecording, !isWorking, modelAvailable else { return }
        message = nil
        let collector = TakeCollector()
        let service = MicrophoneCaptureService()
        service.setPCMHandler { samples, _ in collector.append(samples) }
        do { try await service.startCapture() } catch {
            message = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
            return
        }
        // The page may have been closed while the microphone was starting.
        guard isSettingUp else { _ = service.stopCapture(); return }
        recorder = service
        isRecording = true
        let started = Date()
        while isRecording, Date().timeIntervalSince(started) < Self.takeSeconds {
            progress = min(1, Date().timeIntervalSince(started) / Self.takeSeconds)
            try? await Task.sleep(nanoseconds: 100_000_000)
        }
        guard isRecording else { return }   // cancelled while recording
        _ = service.stopCapture()
        recorder = nil
        isRecording = false
        progress = 0
        let speech = AudioTimeline.trimmingSilence(collector.floats())
        guard Double(speech.count) / Double(AudioTimeline.sampleRate) >= Self.minSpeechSeconds else {
            message = String(localized: "没听清，声音有点小或太短。靠近麦克风，再读一次。")
            return
        }
        isWorking = true
        defer { isWorking = false }
        let loaded = model
        let embedding: (voiceprint: [Float]?, model: Voiceprints?, failure: String?) = await Task.detached(priority: .userInitiated) {
            do {
                let model = try loaded ?? Voiceprints.load()
                return (model.embed(speech), model, nil)
            } catch {
                return (nil, nil, error.localizedDescription)
            }
        }.value
        if let model = embedding.model { self.model = model }
        guard isSettingUp else { return }   // closed while the voiceprint was being computed
        if let failure = embedding.failure {
            message = String(localized: "声纹模型无法使用：\(failure)")
            return
        }
        guard let voiceprint = embedding.voiceprint else {
            message = String(localized: "没听清，再读一次。")
            return
        }
        takes.append(voiceprint)
        if takes.count >= Self.prompts.count { finishSetup() }
    }

    /// Builds the profile from the recordings. Fewer than three good ones, or ones that don't sound alike, start over.
    private func finishSetup() {
        switch VoiceEnrollment.make(from: takes) {
        case .success(let made):
            do {
                try VoiceProfileStore.save(made)
                profile = made
                isSettingUp = false
                takes = []
            } catch {
                message = String(localized: "保存失败：\(error.localizedDescription)")
                takes = []
            }
        case .failure(.tooFewClips):
            message = String(localized: "录到的声音不够，请重新来一遍。")
            takes = []
        case .failure(.inconsistent):
            message = String(localized: "几次录音听起来不像同一个人，请在安静的地方，只由你一个人重新来一遍。")
            takes = []
        }
    }

    /// Deletes the saved voiceprint.
    func remove() {
        VoiceProfileStore.delete()
        profile = nil
        takes = []
        message = nil
    }
}
