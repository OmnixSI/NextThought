import AVFoundation
import CoreMedia
import InterviewHackerCore
import ScreenCaptureKit

final class AudioCaptureService: NSObject, SCStreamOutput, SCStreamDelegate {
    private var stream: SCStream?
    private let audioQueue = DispatchQueue(label: "app.interviewhacker.system-audio")
    private var converter: AVAudioConverter?
    private let outputFormat = AVAudioFormat(commonFormat: .pcmFormatInt16, sampleRate: 16_000, channels: 1, interleaved: true)!
    private var pendingSamples: [Int16] = []
    private var segmenter = VoiceActivitySegmenter()
    private var streamStartedAt = Date()
    private var didLogFormat = false
    private var pcmHandler: (@Sendable ([Int16], Date) -> Void)?
    func setPCMHandler(_ handler: (@Sendable ([Int16], Date) -> Void)?) { audioQueue.sync { pcmHandler = handler } }

    /// Finalize the current utterance without interrupting screen capture.
    func flushUtterance() async -> SpeechSegmentEvent? {
        audioQueue.sync { segmenter.flush().map { stamped($0) } }
    }

    var onSegment: ((SpeechSegmentEvent) -> Void)?
    var onCaptureError: ((String) -> Void)?

    static func availableTargets() async throws -> [CaptureTarget] {
        let content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: true)
        var targets: [CaptureTarget] = []
        for display in content.displays {
            targets.append(CaptureTarget(id: "display:\(display.displayID)", title: String(localized: "整个屏幕 · \(display.width)×\(display.height)")))
        }
        for window in content.windows where window.isOnScreen && !(window.title ?? "").isEmpty {
            let app = window.owningApplication?.applicationName ?? "应用窗口"
            let title = window.title ?? "窗口"
            targets.append(CaptureTarget(id: "window:\(window.windowID)", title: "\(app) · \(title)"))
        }
        return targets
    }

    func startCapture(targetID: String) async throws {
        audioQueue.sync {
            pendingSamples.removeAll(keepingCapacity: true)
            converter = nil
            segmenter.reset()
            didLogFormat = false
            streamStartedAt = Date()
        }
        let content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: true)
        let filter: SCContentFilter
        if targetID.hasPrefix("window:"),
           let rawID = UInt32(targetID.dropFirst("window:".count)),
           let window = content.windows.first(where: { $0.windowID == rawID }) {
            filter = SCContentFilter(desktopIndependentWindow: window)
        } else if targetID.hasPrefix("display:"),
                  let rawID = UInt32(targetID.dropFirst("display:".count)),
                  let display = content.displays.first(where: { $0.displayID == rawID }) {
            filter = SCContentFilter(display: display, excludingWindows: [])
        } else {
            throw AppFailure.capture("所选屏幕或窗口已不可用。请刷新捕获目标并重新选择音源。")
        }

        let configuration = SCStreamConfiguration()
        configuration.capturesAudio = true
        configuration.excludesCurrentProcessAudio = true
        configuration.sampleRate = 48_000
        configuration.channelCount = 2
        configuration.queueDepth = 3

        let nextStream = SCStream(filter: filter, configuration: configuration, delegate: self)
        try nextStream.addStreamOutput(self, type: .audio, sampleHandlerQueue: audioQueue)
        try await nextStream.startCapture()
        stream = nextStream
        Diagnostics.record("capture.started", ["target_id": targetID])
    }

    func stopCapture() async -> [SpeechSegmentEvent] {
        guard let stream else { return [] }
        try? await stream.stopCapture()
        self.stream = nil
        Diagnostics.record("capture.stopped")
        return audioQueue.sync {
            var events: [SpeechSegmentEvent] = []
            if !pendingSamples.isEmpty {
                pcmHandler?(pendingSamples, streamStartedAt)
                events.append(contentsOf: segmenter.append(pendingSamples))
            }
            pendingSamples.removeAll(keepingCapacity: false)
            if let final = segmenter.flush() { events.append(final) }
            converter = nil
            return events.map { stamped($0) }
        }
    }

    func stream(_ stream: SCStream, didOutputSampleBuffer sampleBuffer: CMSampleBuffer, of outputType: SCStreamOutputType) {
        guard outputType == .audio, sampleBuffer.isValid,
              let description = CMSampleBufferGetFormatDescription(sampleBuffer),
              let streamDescription = CMAudioFormatDescriptionGetStreamBasicDescription(description),
              let inputFormat = AVAudioFormat(streamDescription: streamDescription) else { return }

        let frames = CMSampleBufferGetNumSamples(sampleBuffer)
        guard frames > 0,
              let inputBuffer = AVAudioPCMBuffer(pcmFormat: inputFormat, frameCapacity: AVAudioFrameCount(frames)) else { return }
        inputBuffer.frameLength = AVAudioFrameCount(frames)
        if !didLogFormat {
            didLogFormat = true
            streamStartedAt = Date().addingTimeInterval(-Double(frames) / inputFormat.sampleRate)
            Diagnostics.record("capture.format", ["sample_rate": inputFormat.sampleRate, "channels": inputFormat.channelCount, "format_flags": streamDescription.pointee.mFormatFlags, "bits_per_channel": streamDescription.pointee.mBitsPerChannel, "interleaved": inputFormat.isInterleaved, "first_buffer_frames": frames, "output_sample_rate": 16_000, "output_channels": 1, "output_bits": 16])
        }
        let status = CMSampleBufferCopyPCMDataIntoAudioBufferList(
            sampleBuffer,
            at: 0,
            frameCount: Int32(frames),
            into: inputBuffer.mutableAudioBufferList)
        guard status == noErr else { return }

        if converter == nil || converter?.inputFormat != inputFormat {
            converter = AVAudioConverter(from: inputFormat, to: outputFormat)
        }
        guard let converter,
              let outputBuffer = AVAudioPCMBuffer(pcmFormat: outputFormat, frameCapacity: AVAudioFrameCount(Double(frames) * 16_000 / inputFormat.sampleRate + 32)) else { return }

        var didProvideInput = false
        var conversionError: NSError?
        converter.convert(to: outputBuffer, error: &conversionError) { _, inputStatus in
            if didProvideInput {
                inputStatus.pointee = .noDataNow
                return nil
            }
            didProvideInput = true
            inputStatus.pointee = .haveData
            return inputBuffer
        }
        guard conversionError == nil, let channel = outputBuffer.int16ChannelData?.pointee else { return }
        let count = Int(outputBuffer.frameLength)
        pendingSamples.append(contentsOf: UnsafeBufferPointer(start: channel, count: count))
        while pendingSamples.count >= 1_600 {
            let ready = Array(pendingSamples.prefix(1_600))
            pendingSamples.removeFirst(1_600)
            pcmHandler?(ready, streamStartedAt)
            for event in segmenter.append(ready) {
                onSegment?(stamped(event))
            }
        }
    }

    private func stamped(_ event: SpeechSegmentEvent) -> SpeechSegmentEvent {
        var result = event
        result.streamStartedAt = streamStartedAt
        return result
    }

    func stream(_ stream: SCStream, didStopWithError error: Error) {
        onCaptureError?(error.localizedDescription)
    }
}
