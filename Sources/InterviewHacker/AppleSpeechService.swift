import AVFoundation
import Foundation
import InterviewHackerCore
import Speech

@MainActor
protocol LiveTranscribing: AnyObject {
    func start(language: String, onResult: @escaping (String, Bool, Date, Date, Double?, Double?) -> Void,
               onError: @escaping (String) -> Void) async throws
    func inputHandler() -> (@Sendable ([Int16], Date) -> Void)
    func commitUtterance(_ event: SpeechSegmentEvent?) async
    func finish() async
    func cancel() async
    func reset() async
}

extension LiveTranscribing {
    func commitUtterance(_ event: SpeechSegmentEvent?) async {}
    func reset() async {}
}

enum LiveTranscriberFactory {
    static var supported: Bool {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) { return true }
        #endif
        return false
    }
    @MainActor static func make() -> (any LiveTranscribing)? {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) { return AppleSpeechService() }
        #endif
        return nil
    }

    /// Resolves the locale and installs language assets ahead of the session.
    @MainActor static func prewarm(language: String) async {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) {
            let started = Date()
            _ = try? await AppleSpeechService.preparedLocale(for: language)
            Diagnostics.record("apple_speech.prewarmed", ["seconds": Date().timeIntervalSince(started)])
        }
        #endif
    }
}

#if compiler(>=6.2)
@available(macOS 26.0, *)
@MainActor
final class AppleSpeechService: LiveTranscribing {
    private var analyzer: SpeechAnalyzer?
    private var resultTask: Task<Void, Never>?
    private var sink: SpeechInputSink?
    private var generation = UUID()

    func start(language: String, onResult: @escaping (String, Bool, Date, Date, Double?, Double?) -> Void,
               onError: @escaping (String) -> Void) async throws {
        await cancel()
        let id = UUID(); generation = id
        let locale = try await Self.preparedLocale(for: language)
        let transcriber = SpeechTranscriber(locale: locale, transcriptionOptions: [], reportingOptions: [.volatileResults], attributeOptions: [.audioTimeRange])
        try Task.checkCancellation()
        guard generation == id else { throw CancellationError() }
        guard let format = await SpeechAnalyzer.bestAvailableAudioFormat(compatibleWith: [transcriber]) else {
            throw AppFailure.transcription("Apple Speech 音频格式不可用。")
        }
        let pair = AsyncStream<AnalyzerInput>.makeStream(bufferingPolicy: .bufferingOldest(50))
        let input = try SpeechInputSink(format: format, continuation: pair.continuation) { message in
            Task { @MainActor [weak self] in
                guard self?.generation == id else { return }
                onError(message)
            }
        }
        sink = input
        let engine = SpeechAnalyzer(modules: [transcriber])
        analyzer = engine
        resultTask = Task { [weak self] in
            do {
                for try await result in transcriber.results {
                    guard let self, self.generation == id, !Task.isCancelled else { return }
                    let text = String(result.text.characters).trimmingCharacters(in: .whitespacesAndNewlines)
                    guard !text.isEmpty else { continue }
                    let ranges = result.text.runs.compactMap { $0.audioTimeRange }
                    let start = ranges.map { CMTimeGetSeconds($0.start) }.filter(\.isFinite).min() ?? 0
                    let end = ranges.map { CMTimeGetSeconds(CMTimeRangeGetEnd($0)) }.filter(\.isFinite).max() ?? start
                    let origin = input.origin ?? Date()
                    onResult(text, result.isFinal, origin.addingTimeInterval(start), origin.addingTimeInterval(max(start, end)), nil, nil)
                }
            } catch {
                guard self?.generation == id, !Task.isCancelled else { return }
                onError("Apple 流式识别停止：\(error.localizedDescription)")
            }
        }
        do { try await engine.start(inputSequence: pair.stream) }
        catch { await cancel(); throw error }
    }

    /// Locale resolution and asset installation are slow; do them once per language
    /// per app run (and ahead of time via `LiveTranscriberFactory.prewarm`).
    private static var readyLocales: [String: Locale] = [:]

    static func preparedLocale(for language: String) async throws -> Locale {
        // Locale choice is explicit: this path does not pretend to auto-detect mixed speech.
        guard language == "zh" || language == "en" else {
            throw AppFailure.transcription("Apple 流式识别需要先选择中文或 English；中英混说请使用 Zipformer 或云端识别。")
        }
        if let ready = readyLocales[language] { return ready }
        let requested = Locale(identifier: language == "zh" ? "zh-CN" : "en-US")
        guard let locale = await SpeechTranscriber.supportedLocale(equivalentTo: requested) else {
            throw AppFailure.transcription("这台设备的 Apple Speech 不支持所选语言，请改用 Zipformer 或云端识别。")
        }
        let probe = SpeechTranscriber(locale: locale, transcriptionOptions: [], reportingOptions: [.volatileResults], attributeOptions: [.audioTimeRange])
        if let request = try await AssetInventory.assetInstallationRequest(supporting: [probe]) {
            try await request.downloadAndInstall()
        }
        readyLocales[language] = locale
        return locale
    }

    func inputHandler() -> (@Sendable ([Int16], Date) -> Void) {
        let input = sink
        return { samples, origin in input?.append(samples, origin: origin) }
    }

    func finish() async {
        sink?.close()
        guard let analyzer else { return }
        // Prevent stopping the UI from waiting indefinitely for an engine drain.
        let watchdog = Task {
            try? await Task.sleep(for: .seconds(3))
            guard !Task.isCancelled else { return }
            await analyzer.cancelAndFinishNow()
        }
        try? await analyzer.finalizeAndFinishThroughEndOfInput()
        watchdog.cancel()
        await resultTask?.value
        self.analyzer = nil; sink = nil; resultTask = nil
    }

    func cancel() async {
        generation = UUID()
        sink?.close(); sink = nil
        resultTask?.cancel(); resultTask = nil
        let old = analyzer; analyzer = nil
        await old?.cancelAndFinishNow()
    }
}

/// Called directly on the capture queue. No unbounded MainActor task per audio buffer.
@available(macOS 26.0, *)
private final class SpeechInputSink: @unchecked Sendable {
    private let lock = NSLock()
    private let source: AVAudioFormat
    private let target: AVAudioFormat
    private let converter: AVAudioConverter
    private var continuation: AsyncStream<AnalyzerInput>.Continuation?
    private let onError: @Sendable (String) -> Void
    private var streamOrigin: Date?
    var origin: Date? { lock.lock(); defer { lock.unlock() }; return streamOrigin }

    init(format: AVAudioFormat, continuation: AsyncStream<AnalyzerInput>.Continuation,
         onError: @escaping @Sendable (String) -> Void) throws {
        let source = AVAudioFormat(commonFormat: .pcmFormatInt16, sampleRate: 16_000, channels: 1, interleaved: true)!
        guard let converter = AVAudioConverter(from: source, to: format) else {
            throw AppFailure.transcription("无法转换 Apple Speech 输入格式。")
        }
        self.source = source; self.target = format; self.converter = converter
        self.continuation = continuation; self.onError = onError
    }

    func append(_ samples: [Int16], origin: Date) {
        lock.lock(); defer { lock.unlock() }
        guard let continuation, !samples.isEmpty else { return }
        if streamOrigin == nil { streamOrigin = origin }
        guard let input = AVAudioPCMBuffer(pcmFormat: source, frameCapacity: AVAudioFrameCount(samples.count)),
              let pointer = input.int16ChannelData?.pointee else { fail("无法分配语音输入缓冲。"); return }
        input.frameLength = AVAudioFrameCount(samples.count)
        samples.withUnsafeBufferPointer { values in if let base = values.baseAddress { pointer.update(from: base, count: values.count) } }
        let count = AVAudioFrameCount(ceil(Double(samples.count) * target.sampleRate / source.sampleRate)) + 64
        guard let output = AVAudioPCMBuffer(pcmFormat: target, frameCapacity: count) else { fail("无法分配语音转换缓冲。"); return }
        var supplied = false
        var error: NSError?
        converter.convert(to: output, error: &error) { _, status in
            if supplied { status.pointee = .noDataNow; return nil }
            supplied = true; status.pointee = .haveData; return input
        }
        guard error == nil else { fail("音频格式转换失败，已停止本次转写。"); return }
        guard output.frameLength > 0 else { return }
        if case .dropped = continuation.yield(AnalyzerInput(buffer: output)) {
            fail("Apple Speech 输入积压超过缓冲上限；本次转写已停止，未静默跳过音频。")
        }
    }
    private func fail(_ message: String) { continuation?.finish(); continuation = nil; onError(message) }
    func close() {
        lock.lock(); defer { lock.unlock() }
        continuation?.finish(); continuation = nil
    }
}
#endif
