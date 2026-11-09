import AVFoundation
import CoreAudio
import NextThoughtCore

/// Independent input channel: microphone audio is never mixed with system output.
/// Follows the system default input device: plugging or unplugging headphones,
/// AirPods or USB microphones restarts the input on the new device automatically.
final class MicrophoneCaptureService {
    private var engine = AVAudioEngine()
    private let queue = DispatchQueue(label: "app.nextthought.microphone")
    private let format = AVAudioFormat(commonFormat: .pcmFormatInt16, sampleRate: 16_000, channels: 1, interleaved: true)!
    private var converter: AVAudioConverter?
    private var segmenter = VoiceActivitySegmenter()
    private var pending: [Int16] = []
    private var startedAt = Date()
    private var accepting = false
    private var tapInstalled = false
    private var wantsCapture = false
    private var configurationObserver: NSObjectProtocol?
    private var defaultDeviceListener: AudioObjectPropertyListenerBlock?
    private var restartWork: DispatchWorkItem?
    private var pcmHandler: (@Sendable ([Int16], Date) -> Void)?
    var onSegment: ((SpeechSegmentEvent) -> Void)?
    /// Called only when the microphone cannot be restarted after several attempts.
    var onCaptureError: ((String) -> Void)?
    /// Called with the new device name after an automatic switch.
    var onDeviceChanged: ((String) -> Void)?
    /// Apple voice processing on the input (acoustic echo cancellation + noise
    /// suppression). Removes the other side's voice coming back from the speakers.
    /// Off by default: it can fail on some device combinations and ducks other audio.
    var voiceProcessing = false

    func setPCMHandler(_ handler: (@Sendable ([Int16], Date) -> Void)?) { queue.sync { pcmHandler = handler } }

    static func authorize() async throws {
        let authorized: Bool
        switch AVCaptureDevice.authorizationStatus(for: .audio) {
        case .authorized: authorized = true
        case .notDetermined: authorized = await AVCaptureDevice.requestAccess(for: .audio)
        default: authorized = false
        }
        guard authorized else {
            throw AppFailure.capture("麦克风未授权。请在系统设置 → 隐私与安全性 → 麦克风中允许 NextThought，或关闭麦克风采集。")
        }
    }

    func startCapture() async throws {
        try await Self.authorize()
        queue.sync {
            segmenter.reset(); pending.removeAll()
            startedAt = Date()
        }
        wantsCapture = true
        installDefaultDeviceListener()
        try startEngine()
    }

    func stopCapture() -> [SpeechSegmentEvent] {
        wantsCapture = false
        restartWork?.cancel(); restartWork = nil
        removeDefaultDeviceListener()
        teardownEngine()
        return queue.sync {
            accepting = false
            var events = drainPending()
            if let event = segmenter.flush() { events.append(event) }
            converter = nil
            return events.map(stamped)
        }
    }

    // MARK: Engine lifecycle

    private func startEngine() throws {
        // A fresh engine binds to the *current* default input device.
        let next = AVAudioEngine()
        let input = next.inputNode
        if voiceProcessing {
            do {
                try input.setVoiceProcessingEnabled(true)
                // Keep the meeting audio at full volume while echo cancellation runs.
                if #available(macOS 14.0, *) {
                    input.voiceProcessingOtherAudioDuckingConfiguration = .init(enableAdvancedDucking: false, duckingLevel: .min)
                }
                Diagnostics.record("microphone.voice_processing", ["enabled": true])
            } catch {
                // Different input/output devices or unsupported hardware: continue without it.
                Diagnostics.record("microphone.voice_processing_failed")
            }
        }
        let inputFormat = input.outputFormat(forBus: 0)
        guard inputFormat.sampleRate > 0, inputFormat.channelCount > 0,
              let nextConverter = AVAudioConverter(from: inputFormat, to: format) else {
            throw AppFailure.capture("没有可用的麦克风输入设备。")
        }
        queue.sync { converter = nextConverter; accepting = true }
        input.installTap(onBus: 0, bufferSize: 4_800, format: inputFormat) { [weak self] buffer, _ in
            guard let self else { return }
            // Consume the tap's buffer before returning; VAD and conversion state is
            // serialized separately from ScreenCaptureKit's output channel.
            self.queue.sync { self.consume(buffer) }
        }
        engine = next
        tapInstalled = true
        do {
            next.prepare()
            try next.start()
        } catch {
            teardownEngine()
            throw error
        }
        configurationObserver = NotificationCenter.default.addObserver(forName: .AVAudioEngineConfigurationChange, object: next, queue: nil) { [weak self] _ in
            self?.scheduleRestart(reason: "configuration")
        }
        Diagnostics.record("microphone.started", ["sample_rate": inputFormat.sampleRate, "channels": inputFormat.channelCount])
    }

    private func teardownEngine() {
        if let configurationObserver { NotificationCenter.default.removeObserver(configurationObserver) }
        configurationObserver = nil
        engine.stop()
        if tapInstalled { engine.inputNode.removeTap(onBus: 0); tapInstalled = false }
    }

    /// Debounced: device switches emit several notifications while settling.
    private func scheduleRestart(reason: String) {
        DispatchQueue.main.async { [weak self] in
            guard let self, self.wantsCapture else { return }
            self.restartWork?.cancel()
            let work = DispatchWorkItem { [weak self] in self?.restart(attempt: 1, reason: reason) }
            self.restartWork = work
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.4, execute: work)
        }
    }

    private func restart(attempt: Int, reason: String) {
        guard wantsCapture else { return }
        teardownEngine()
        // Finish the utterance that was in progress on the old device.
        let flushed: [SpeechSegmentEvent] = queue.sync {
            var events = drainPending()
            if let event = segmenter.flush() { events.append(event) }
            return events.map(stamped)
        }
        flushed.forEach { onSegment?($0) }
        do {
            try startEngine()
            Diagnostics.record("microphone.restarted", ["reason": reason, "attempt": attempt])
            onDeviceChanged?(Self.defaultInputName() ?? "新的输入设备")
        } catch {
            guard attempt < 6 else {
                onCaptureError?("麦克风切换设备后无法启动：\(error.localizedDescription)")
                return
            }
            let work = DispatchWorkItem { [weak self] in self?.restart(attempt: attempt + 1, reason: reason) }
            restartWork = work
            DispatchQueue.main.asyncAfter(deadline: .now() + Double(attempt) * 0.5, execute: work)
        }
    }

    // MARK: Default input device tracking

    private static var defaultInputAddress = AudioObjectPropertyAddress(
        mSelector: kAudioHardwarePropertyDefaultInputDevice,
        mScope: kAudioObjectPropertyScopeGlobal,
        mElement: kAudioObjectPropertyElementMain)

    private func installDefaultDeviceListener() {
        guard defaultDeviceListener == nil else { return }
        let block: AudioObjectPropertyListenerBlock = { [weak self] _, _ in
            self?.scheduleRestart(reason: "default_input_changed")
        }
        if AudioObjectAddPropertyListenerBlock(AudioObjectID(kAudioObjectSystemObject), &Self.defaultInputAddress, DispatchQueue.main, block) == noErr {
            defaultDeviceListener = block
        }
    }

    private func removeDefaultDeviceListener() {
        guard let block = defaultDeviceListener else { return }
        AudioObjectRemovePropertyListenerBlock(AudioObjectID(kAudioObjectSystemObject), &Self.defaultInputAddress, DispatchQueue.main, block)
        defaultDeviceListener = nil
    }

    static func defaultInputName() -> String? {
        var deviceID = AudioDeviceID(0)
        var size = UInt32(MemoryLayout<AudioDeviceID>.size)
        guard AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject), &defaultInputAddress, 0, nil, &size, &deviceID) == noErr else { return nil }
        var nameAddress = AudioObjectPropertyAddress(mSelector: kAudioObjectPropertyName, mScope: kAudioObjectPropertyScopeGlobal, mElement: kAudioObjectPropertyElementMain)
        var name: Unmanaged<CFString>?
        size = UInt32(MemoryLayout<Unmanaged<CFString>?>.size)
        guard AudioObjectGetPropertyData(deviceID, &nameAddress, 0, nil, &size, &name) == noErr, let name else { return nil }
        return name.takeRetainedValue() as String
    }

    // MARK: Audio processing (on `queue`)

    private func drainPending() -> [SpeechSegmentEvent] {
        guard !pending.isEmpty else { return [] }
        pcmHandler?(pending, startedAt)
        let events = segmenter.append(pending)
        pending.removeAll()
        return events
    }

    private func consume(_ input: AVAudioPCMBuffer) {
        guard accepting, let converter,
              let output = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: AVAudioFrameCount(ceil(Double(input.frameLength) * 16_000 / input.format.sampleRate)) + 64) else { return }
        var provided = false
        var error: NSError?
        converter.convert(to: output, error: &error) { _, status in
            if provided { status.pointee = .noDataNow; return nil }
            provided = true; status.pointee = .haveData; return input
        }
        guard error == nil, let channel = output.int16ChannelData?.pointee else { return }
        pending += Array(UnsafeBufferPointer(start: channel, count: Int(output.frameLength)))
        while pending.count >= 1_600 {
            let chunk = Array(pending.prefix(1_600)); pending.removeFirst(1_600)
            pcmHandler?(chunk, startedAt)
            segmenter.append(chunk).map(stamped).forEach { onSegment?($0) }
        }
    }

    private func stamped(_ event: SpeechSegmentEvent) -> SpeechSegmentEvent {
        var result = event
        result.streamStartedAt = startedAt
        result.source = .microphone
        return result
    }
}
