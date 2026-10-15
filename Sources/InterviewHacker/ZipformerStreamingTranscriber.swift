import Foundation
import InterviewHackerCore

@MainActor
final class ZipformerStreamingTranscriber: LiveTranscribing {
    private let runtime = ZipformerRuntime.shared
    private let modelDirectory: URL
    private let vocabulary: String
    private var streamID: UUID?
    private var generation = UUID()
    private var resultHandler: ((String, Bool, Date, Date, Double?, Double?) -> Void)?
    private var errorHandler: ((String) -> Void)?
    private var lastFinalizedAt = Date.distantPast

    init(modelDirectory: URL = ZipformerModelStore.modelDirectory, vocabulary: String) {
        self.modelDirectory = modelDirectory
        self.vocabulary = vocabulary
    }

    func start(language: String, onResult: @escaping (String, Bool, Date, Date, Double?, Double?) -> Void,
               onError: @escaping (String) -> Void) async throws {
        await cancel()
        try ZipformerModelStore.verify(at: modelDirectory)
        try await runtime.configure(modelDirectory: modelDirectory, hotwords: vocabulary)
        resultHandler = onResult
        errorHandler = onError
        lastFinalizedAt = .distantPast
        generation = UUID()
        streamID = try await runtime.makeStream()
        Diagnostics.record("zipformer.stream_started", ["language": language, "hotwords": !vocabulary.isEmpty])
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

    func commitUtterance() async {
        guard let streamID else { return }
        do {
            if let final = try await runtime.commitStream(streamID) { deliver(final) }
        } catch {
            errorHandler?("Zipformer 提交语音片段失败：\(error.localizedDescription)")
        }
    }

    func reset() async {
        guard let streamID else { return }
        do {
            if let final = try await runtime.resetStream(streamID) { deliver(final) }
        } catch {
            errorHandler?("Zipformer 切换音频设备后重置失败：\(error.localizedDescription)")
        }
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

    private static func normalize(_ rawText: String) -> String {
        var text = rawText.split(whereSeparator: \.isWhitespace).joined(separator: " ")
        let chineseWordBoundary = try? NSRegularExpression(pattern: "([\\p{Han}])\\s+([\\p{Han}])")
        while let expression = chineseWordBoundary,
              let match = expression.firstMatch(in: text, range: NSRange(text.startIndex..., in: text)),
              let range = Range(match.range, in: text),
              let left = Range(match.range(at: 1), in: text),
              let right = Range(match.range(at: 2), in: text) {
            text.replaceSubrange(range, with: text[left] + text[right])
        }
        return text.trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
