import AVFoundation
import InterviewHackerCore

/// Independent input channel: microphone audio is never mixed with system output.
final class MicrophoneCaptureService {
    private let engine = AVAudioEngine()
    private let queue = DispatchQueue(label: "app.interviewhacker.microphone")
    private let format = AVAudioFormat(commonFormat: .pcmFormatInt16, sampleRate: 16_000, channels: 1, interleaved: true)!
    private var converter: AVAudioConverter?
    private var segmenter = VoiceActivitySegmenter()
    private var pending: [Int16] = []
    private var startedAt = Date()
    private var accepting = false
    private var tapInstalled = false
    private var configurationObserver: NSObjectProtocol?
    private var pcmHandler: (@Sendable ([Int16], Date) -> Void)?
    var onSegment: ((SpeechSegmentEvent) -> Void)?
    var onCaptureError: ((String) -> Void)?

    func setPCMHandler(_ handler: (@Sendable ([Int16], Date) -> Void)?) { queue.sync { pcmHandler = handler } }

    static func authorize() async throws {
        let authorized: Bool
        switch AVCaptureDevice.authorizationStatus(for: .audio) {
        case .authorized: authorized = true
        case .notDetermined: authorized = await AVCaptureDevice.requestAccess(for: .audio)
        default: authorized = false
        }
        guard authorized else {
            throw AppFailure.capture("麦克风未授权。请在系统设置 → 隐私与安全性 → 麦克风中允许 InterviewHacker，或关闭麦克风采集。")
        }
    }

    func startCapture() async throws {
        try await Self.authorize()
        let input = engine.inputNode
        let inputFormat = input.outputFormat(forBus: 0)
        guard inputFormat.sampleRate > 0, inputFormat.channelCount > 0,
              let nextConverter = AVAudioConverter(from: inputFormat, to: format) else {
            throw AppFailure.capture("没有可用的麦克风，请检查系统输入设备。")
        }
        queue.sync {
            converter = nextConverter
            segmenter.reset(); pending.removeAll()
            startedAt = Date(); accepting = true
        }
        input.installTap(onBus: 0, bufferSize: 4_800, format: inputFormat) { [weak self] buffer, _ in
            guard let self else { return }
            // Consume the tap's buffer before returning; all VAD and conversion state
            // is serialized separately from ScreenCaptureKit's output channel.
            self.queue.sync { self.consume(buffer) }
        }
        tapInstalled = true
        do {
            engine.prepare()
            try engine.start()
            configurationObserver = NotificationCenter.default.addObserver(forName: .AVAudioEngineConfigurationChange, object: engine, queue: nil) { [weak self] _ in
                self?.onCaptureError?("麦克风设备或采样格式已改变，请重新开始辅助。")
            }
        } catch {
            _ = stopCapture()
            throw error
        }
    }

    func stopCapture() -> [SpeechSegmentEvent] {
        if let configurationObserver { NotificationCenter.default.removeObserver(configurationObserver) }
        configurationObserver = nil
        engine.stop()
        if tapInstalled { engine.inputNode.removeTap(onBus: 0); tapInstalled = false }
        return queue.sync {
            accepting = false
            var events: [SpeechSegmentEvent] = []
            if !pending.isEmpty {
                pcmHandler?(pending, startedAt)
                events += segmenter.append(pending)
            }
            pending.removeAll()
            if let event = segmenter.flush() { events.append(event) }
            converter = nil
            return events.map(stamped)
        }
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
