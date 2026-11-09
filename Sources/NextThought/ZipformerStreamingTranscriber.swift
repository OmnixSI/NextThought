import Foundation
import NextThoughtCore

/// Local two-pass recognizer: a streaming Zipformer produces live partial text, and when
/// the voice-activity segmenter closes an utterance, the offline model re-decodes the
/// whole utterance for the final text (better words, punctuation and “？”).
@MainActor
final class ZipformerStreamingTranscriber: LiveTranscribing {
    private let runtime = ZipformerRuntime.shared
    private let model: OnlineTransducerModel
    private let refiner: OfflineTransducerModel?
    private let vocabulary: String
    private var streamID: UUID?
    private var generation = UUID()
    private var resultHandler: ((String, Bool, Date, Date, Double?, Double?) -> Void)?
    private var errorHandler: ((String) -> Void)?
    private var lastFinalizedAt = Date.distantPast
    /// Absolute sample index up to which audio has been committed (the segmenter
    /// re-sends half a second of overlap when it splits a long monologue).
    private var committedThroughSample = 0
    private(set) var lastRefineSeconds: Double?

    init(model: OnlineTransducerModel, refiner: OfflineTransducerModel?, vocabulary: String) {
        self.model = model
        self.refiner = refiner
        self.vocabulary = vocabulary
    }

    func start(language: String, onResult: @escaping (String, Bool, Date, Date, Double?, Double?) -> Void,
               onError: @escaping (String) -> Void) async throws {
        await cancel()
        try await runtime.configure(model: model, hotwords: vocabulary)
        if let refiner {
            do { try await OfflineSegmentRecognizer.shared.configure(model: refiner, hotwords: vocabulary) }
            catch { Diagnostics.record("refiner.configure_failed") }
        }
        resultHandler = onResult
        errorHandler = onError
        lastFinalizedAt = .distantPast
        committedThroughSample = 0
        generation = UUID()
        streamID = try await runtime.makeStream()
        Diagnostics.record("zipformer.stream_started", ["language": language, "hotwords": !vocabulary.isEmpty, "refine": refiner != nil])
    }

    func inputHandler() -> (@Sendable ([Int16], Date) -> Void) {
        guard let streamID else { return { _, _ in } }
        let id = generation
        let runtime = self.runtime
        return { [weak self] samples, origin in
            runtime.accept(samples, streamID: streamID, sourceOrigin: origin) { [weak self] hypothesis in
                guard let self else { return }
                Task { @MainActor [self] in
                    guard self.generation == id else { return }
                    self.deliver(hypothesis)
                }
            }
        }
    }

    func finish() async {
        guard let streamID else { return }
        self.streamID = nil
        if let final = await runtime.finishStream(streamID) { deliver(final) }
        Diagnostics.record("zipformer.stream_finished")
    }

    func commitUtterance(_ event: SpeechSegmentEvent?) async {
        guard let streamID else { return }
        let id = generation
        let streaming: ZipformerHypothesis?
        do {
            streaming = try await runtime.commitStream(streamID)
        } catch {
            errorHandler?("Zipformer 提交语音片段失败：\(error.localizedDescription)")
            return
        }
        guard generation == id else { return }
        guard refiner != nil, let event, OfflineSegmentRecognizer.shared.isLoaded else {
            if let streaming { deliver(streaming) }
            return
        }
        let samples = newSamples(of: event)
        committedThroughSample = max(committedThroughSample, event.endSample)
        guard let refined = await OfflineSegmentRecognizer.shared.recognize(samples), generation == id else {
            if let streaming { deliver(streaming) }
            return
        }
        lastRefineSeconds = refined.seconds
        Diagnostics.record("refiner.decoded", ["seconds": refined.seconds, "audio_seconds": Double(samples.count) / 16_000, "changed": refined.text != streaming?.text])
        let text = refined.text.isEmpty ? (streaming?.text ?? "") : refined.text
        guard !text.isEmpty else { return }
        let origin = event.streamStartedAt ?? Date()
        let started = streaming?.startedAt ?? origin.addingTimeInterval(Double(event.startSample) / 16_000)
        let ended = streaming?.endedAt ?? origin.addingTimeInterval(Double(event.endSample) / 16_000)
        deliver(ZipformerHypothesis(text: text, isFinal: true, startedAt: started, endedAt: max(ended, started.addingTimeInterval(0.01)),
                                    inferenceSeconds: (streaming?.inferenceSeconds ?? 0) + refined.seconds,
                                    audioSeconds: Double(samples.count) / 16_000))
    }

    func reset() async {
        guard let streamID else { return }
        do {
            if let final = try await runtime.resetStream(streamID) { deliver(final) }
        } catch {
            errorHandler?("Zipformer 切换音频设备后重置失败：\(error.localizedDescription)")
        }
        committedThroughSample = 0
        Diagnostics.record("zipformer.stream_reset")
    }

    func cancel() async {
        generation = UUID()
        if let streamID {
            await runtime.destroyStream(streamID)
            self.streamID = nil
        }
        resultHandler = nil
        errorHandler = nil
    }

    /// Drops the overlap the segmenter repeats after splitting a long utterance.
    private func newSamples(of event: SpeechSegmentEvent) -> [Int16] {
        let alreadyCommitted = max(0, committedThroughSample - event.startSample)
        guard alreadyCommitted > 0 else { return event.samples }
        return Array(event.samples.dropFirst(min(alreadyCommitted, event.samples.count)))
    }

    private func deliver(_ hypothesis: ZipformerHypothesis) {
        if hypothesis.isFinal {
            guard hypothesis.endedAt > lastFinalizedAt else { return }
            lastFinalizedAt = hypothesis.endedAt
        } else if hypothesis.endedAt <= lastFinalizedAt {
            return
        }
        let text = Self.normalize(hypothesis.text)
        guard !text.isEmpty else { return }
        resultHandler?(
            text,
            hypothesis.isFinal,
            hypothesis.startedAt,
            max(hypothesis.startedAt, hypothesis.endedAt),
            hypothesis.inferenceSeconds,
            hypothesis.audioSeconds)
    }

    private static let hanSpace = try? NSRegularExpression(pattern: "(?<=[\\p{Han}])\\s+(?=[\\p{Han}])")
    private static let punctuationSpace = try? NSRegularExpression(pattern: "\\s*([，。？！、；：])\\s*")

    /// Joins Chinese characters the recognizer separated and removes the spaces it puts
    /// around Chinese punctuation (“请解释一下， trade off” → “请解释一下，trade off”).
    static func normalize(_ rawText: String) -> String {
        var text = rawText.split(whereSeparator: \.isWhitespace).joined(separator: " ")
        let range = { NSRange(text.startIndex..., in: text) }
        if let hanSpace { text = hanSpace.stringByReplacingMatches(in: text, range: range(), withTemplate: "") }
        if let punctuationSpace { text = punctuationSpace.stringByReplacingMatches(in: text, range: range(), withTemplate: "$1") }
        return text.trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
