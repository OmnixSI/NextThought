import Foundation
import SherpaOnnx
import SherpaOnnxC

public enum ZipformerModelFiles {
    public static let encoder = "encoder-epoch-99-avg-1.int8.onnx"
    public static let decoder = "decoder-epoch-99-avg-1.onnx"
    public static let joiner = "joiner-epoch-99-avg-1.int8.onnx"
    public static let tokens = "tokens.txt"
    public static let bpeVocabulary = "bpe.vocab"
    public static let sampleRate = 16_000
}

public struct ZipformerHypothesis: Sendable {
    public let text: String
    public let isFinal: Bool
    public let startedAt: Date
    public let endedAt: Date
    public let inferenceSeconds: Double
    public let audioSeconds: Double

    public init(text: String, isFinal: Bool, startedAt: Date, endedAt: Date,
                inferenceSeconds: Double, audioSeconds: Double) {
        self.text = text
        self.isFinal = isFinal
        self.startedAt = startedAt
        self.endedAt = endedAt
        self.inferenceSeconds = inferenceSeconds
        self.audioSeconds = audioSeconds
    }
}

public enum ZipformerRuntimeError: LocalizedError {
    case missingModelFile(String)
    case recognizerCreationFailed
    case streamCreationFailed
    case missingRecognizer
    case activeStreams

    public var errorDescription: String? {
        switch self {
        case .missingModelFile(let name): "Zipformer 模型文件缺失：\(name)"
        case .recognizerCreationFailed: "Sherpa-ONNX 无法加载 Zipformer 模型，请重新下载完整模型文件组。"
        case .streamCreationFailed: "Sherpa-ONNX 无法创建在线识别流。"
        case .missingRecognizer: "Zipformer 运行时尚未加载。"
        case .activeStreams: "Zipformer 正在识别，无法重新加载模型。"
        }
    }
}

/// A single Sherpa recognizer owns independent online streams for the two audio sources.
/// All C API calls share one serial queue, so model inference never blocks capture callbacks.
public final class ZipformerRuntime: @unchecked Sendable {
    public static let shared = ZipformerRuntime()

    private struct StreamState {
        var pointer: OpaquePointer
        var sourceOrigin: Date?
        var samplesReceived = 0
        var utteranceStartSample = 0
        var inferenceSeconds = 0.0
        var lastHypothesis = ""
    }

    private let queue = DispatchQueue(label: "app.interviewhacker.zipformer", qos: .userInitiated)
    private var recognizer: OpaquePointer?
    private var streams: [UUID: StreamState] = [:]
    private var configurationKey: String?

    private init() {}

    public func configure(modelDirectory: URL, hotwords: String = "", threads: Int = 2) async throws {
        try await withCheckedThrowingContinuation { continuation in
            queue.async {
                do {
                    try self.configureNow(modelDirectory: modelDirectory, hotwords: hotwords, threads: threads)
                    continuation.resume()
                } catch {
                    continuation.resume(throwing: error)
                }
            }
        }
    }

    public func makeStream() async throws -> UUID {
        try await withCheckedThrowingContinuation { continuation in
            queue.async {
                guard let recognizer = self.recognizer else {
                    continuation.resume(throwing: ZipformerRuntimeError.missingRecognizer)
                    return
                }
                guard let pointer = SherpaOnnxCreateOnlineStream(recognizer) else {
                    continuation.resume(throwing: ZipformerRuntimeError.streamCreationFailed)
                    return
                }
                let id = UUID()
                self.streams[id] = StreamState(pointer: pointer)
                continuation.resume(returning: id)
            }
        }
    }

    public func accept(_ samples: [Int16], streamID: UUID, sourceOrigin: Date,
                       onUpdate: @escaping @Sendable (ZipformerHypothesis) -> Void) {
        guard !samples.isEmpty else { return }
        queue.async {
            guard let recognizer = self.recognizer, var state = self.streams[streamID] else { return }
            if state.sourceOrigin == nil { state.sourceOrigin = sourceOrigin }
            let started = ProcessInfo.processInfo.systemUptime
            let normalized = samples.map { Float($0) / 32_768 }
            normalized.withUnsafeBufferPointer { buffer in
                SherpaOnnxOnlineStreamAcceptWaveform(
                    state.pointer,
                    Int32(ZipformerModelFiles.sampleRate),
                    buffer.baseAddress,
                    Int32(buffer.count))
            }
            while SherpaOnnxIsOnlineStreamReady(recognizer, state.pointer) != 0 {
                SherpaOnnxDecodeOnlineStream(recognizer, state.pointer)
            }
            state.inferenceSeconds += ProcessInfo.processInfo.systemUptime - started
            state.samplesReceived += samples.count

            let text = self.currentText(recognizer: recognizer, stream: state.pointer)
            if !text.isEmpty, text != state.lastHypothesis {
                state.lastHypothesis = text
                onUpdate(self.hypothesis(text: text, state: state, isFinal: false))
            }
            self.streams[streamID] = state
        }
    }

    /// Flushes one VAD-delimited utterance and starts a fresh stream for the same source.
    public func commitStream(_ streamID: UUID) async throws -> ZipformerHypothesis? {
        try await withCheckedThrowingContinuation { continuation in
            queue.async {
                guard let recognizer = self.recognizer,
                      var state = self.streams[streamID] else {
                    continuation.resume(returning: nil)
                    return
                }
                guard let replacement = SherpaOnnxCreateOnlineStream(recognizer) else {
                    continuation.resume(throwing: ZipformerRuntimeError.streamCreationFailed)
                    return
                }
                let old = state.pointer
                let final = self.finishCurrentSegment(recognizer: recognizer, state: &state)
                SherpaOnnxDestroyOnlineStream(old)
                state.pointer = replacement
                state.utteranceStartSample = state.samplesReceived
                state.inferenceSeconds = 0
                state.lastHypothesis = ""
                self.streams[streamID] = state
                continuation.resume(returning: final)
            }
        }
    }

    /// Ends a stream after queued audio has been processed and returns its final hypothesis once.
    public func finishStream(_ streamID: UUID) async -> ZipformerHypothesis? {
        await withCheckedContinuation { continuation in
            queue.async {
                guard let recognizer = self.recognizer,
                      var state = self.streams.removeValue(forKey: streamID) else {
                    continuation.resume(returning: nil)
                    return
                }
                let final = self.finishCurrentSegment(recognizer: recognizer, state: &state)
                SherpaOnnxDestroyOnlineStream(state.pointer)
                continuation.resume(returning: final)
            }
        }
    }

    /// Ends the old device stream and starts a new one while preserving the source identity.
    public func resetStream(_ streamID: UUID) async throws -> ZipformerHypothesis? {
        try await commitStream(streamID)
    }

    public func destroyStream(_ streamID: UUID) async {
        await withCheckedContinuation { continuation in
            queue.async {
                if let state = self.streams.removeValue(forKey: streamID) {
                    SherpaOnnxDestroyOnlineStream(state.pointer)
                }
                continuation.resume()
            }
        }
    }

    public func unload() async {
        await withCheckedContinuation { continuation in
            queue.async {
                for state in self.streams.values {
                    SherpaOnnxDestroyOnlineStream(state.pointer)
                }
                self.streams.removeAll()
                if let recognizer = self.recognizer {
                    SherpaOnnxDestroyOnlineRecognizer(recognizer)
                }
                self.recognizer = nil
                self.configurationKey = nil
                continuation.resume()
            }
        }
    }

    private func configureNow(modelDirectory: URL, hotwords: String, threads: Int) throws {
        let required = [ZipformerModelFiles.encoder, ZipformerModelFiles.decoder,
                        ZipformerModelFiles.joiner, ZipformerModelFiles.tokens,
                        ZipformerModelFiles.bpeVocabulary]
        for name in required where !FileManager.default.fileExists(atPath: modelDirectory.appendingPathComponent(name).path) {
            throw ZipformerRuntimeError.missingModelFile(name)
        }
        let cleanedHotwords = hotwords
            .components(separatedBy: CharacterSet(charactersIn: ",\n\r;"))
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
        let key = modelDirectory.standardizedFileURL.path + "|" + cleanedHotwords.joined(separator: "\n")
        if configurationKey == key, recognizer != nil { return }
        guard streams.isEmpty else { throw ZipformerRuntimeError.activeStreams }
        if let recognizer { SherpaOnnxDestroyOnlineRecognizer(recognizer) }
        self.recognizer = nil

        let encoder = modelDirectory.appendingPathComponent(ZipformerModelFiles.encoder).path
        let decoder = modelDirectory.appendingPathComponent(ZipformerModelFiles.decoder).path
        let joiner = modelDirectory.appendingPathComponent(ZipformerModelFiles.joiner).path
        let tokens = modelDirectory.appendingPathComponent(ZipformerModelFiles.tokens).path
        let bpeVocab = modelDirectory.appendingPathComponent(ZipformerModelFiles.bpeVocabulary).path
        let hotwordsFile: String
        if cleanedHotwords.isEmpty {
            hotwordsFile = ""
        } else {
            let url = FileManager.default.temporaryDirectory
                .appendingPathComponent("interviewhacker-zipformer-hotwords-\(UUID().uuidString).txt")
            try cleanedHotwords.joined(separator: "\n").write(to: url, atomically: true, encoding: .utf8)
            hotwordsFile = url.path
        }
        defer {
            if !hotwordsFile.isEmpty { try? FileManager.default.removeItem(atPath: hotwordsFile) }
        }

        let transducer = sherpaOnnxOnlineTransducerModelConfig(encoder: encoder, decoder: decoder, joiner: joiner)
        let model = sherpaOnnxOnlineModelConfig(
            tokens: tokens,
            transducer: transducer,
            numThreads: max(1, min(threads, 4)),
            provider: "cpu",
            modelType: "zipformer",
            modelingUnit: "cjkchar+bpe",
            bpeVocab: bpeVocab)
        let features = sherpaOnnxFeatureConfig(sampleRate: ZipformerModelFiles.sampleRate, featureDim: 80)
        let config = sherpaOnnxOnlineRecognizerConfig(
            featConfig: features,
            modelConfig: model,
            enableEndpoint: false,
            rule1MinTrailingSilence: 2.4,
            rule2MinTrailingSilence: 1.2,
            rule3MinUtteranceLength: 20.0,
            decodingMethod: "modified_beam_search",
            maxActivePaths: 4,
            hotwordsFile: hotwordsFile,
            hotwordsScore: 1.5)
        var mutableConfig = config
        guard let created = withUnsafePointer(to: &mutableConfig, SherpaOnnxCreateOnlineRecognizer) else {
            throw ZipformerRuntimeError.recognizerCreationFailed
        }
        recognizer = created
        configurationKey = key
    }

    private func finishCurrentSegment(recognizer: OpaquePointer,
                                      state: inout StreamState) -> ZipformerHypothesis? {
        SherpaOnnxOnlineStreamInputFinished(state.pointer)
        while SherpaOnnxIsOnlineStreamReady(recognizer, state.pointer) != 0 {
            let started = ProcessInfo.processInfo.systemUptime
            SherpaOnnxDecodeOnlineStream(recognizer, state.pointer)
            state.inferenceSeconds += ProcessInfo.processInfo.systemUptime - started
        }
        let text = currentText(recognizer: recognizer, stream: state.pointer)
        return text.isEmpty ? nil : hypothesis(text: text, state: state, isFinal: true)
    }

    private func currentText(recognizer: OpaquePointer, stream: OpaquePointer) -> String {
        guard let result = SherpaOnnxGetOnlineStreamResult(recognizer, stream) else { return "" }
        defer { SherpaOnnxDestroyOnlineRecognizerResult(result) }
        guard let pointer = result.pointee.text else { return "" }
        return String(cString: pointer)
            .split(whereSeparator: \.isWhitespace)
            .joined(separator: " ")
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func hypothesis(text: String, state: StreamState, isFinal: Bool) -> ZipformerHypothesis {
        let origin = state.sourceOrigin ?? Date()
        let streamStart = Double(state.utteranceStartSample) / Double(ZipformerModelFiles.sampleRate)
        let duration = max(0.001, Double(state.samplesReceived - state.utteranceStartSample)
                           / Double(ZipformerModelFiles.sampleRate))
        return ZipformerHypothesis(
            text: text,
            isFinal: isFinal,
            startedAt: origin.addingTimeInterval(streamStart),
            endedAt: origin.addingTimeInterval(streamStart + duration),
            inferenceSeconds: state.inferenceSeconds,
            audioSeconds: duration)
    }
}
