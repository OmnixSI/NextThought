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

/// Files and decoding options of one streaming transducer model.
public struct OnlineTransducerModel: Sendable, Equatable {
    public var directory: URL
    public var encoder: String
    public var decoder: String
    public var joiner: String
    public var tokens: String
    /// SentencePiece vocabulary (`piece<TAB>score`); required for hotwords.
    public var bpeVocabulary: String?
    public var modelingUnit: String

    public init(directory: URL, encoder: String, decoder: String, joiner: String, tokens: String = "tokens.txt",
                bpeVocabulary: String? = nil, modelingUnit: String = "bpe") {
        self.directory = directory; self.encoder = encoder; self.decoder = decoder; self.joiner = joiner
        self.tokens = tokens; self.bpeVocabulary = bpeVocabulary; self.modelingUnit = modelingUnit
    }

    /// sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20 (no punctuation).
    public static func legacyBilingual(at directory: URL) -> Self {
        .init(directory: directory, encoder: ZipformerModelFiles.encoder, decoder: ZipformerModelFiles.decoder,
              joiner: ZipformerModelFiles.joiner, tokens: ZipformerModelFiles.tokens,
              bpeVocabulary: ZipformerModelFiles.bpeVocabulary, modelingUnit: "cjkchar+bpe")
    }

    /// X-ASR zh-en streaming Zipformer (2026-06): punctuation, casing, code-switching.
    public static func xasrStreaming(at directory: URL) -> Self {
        .init(directory: directory, encoder: "encoder.int8.onnx", decoder: "decoder.onnx", joiner: "joiner.int8.onnx",
              bpeVocabulary: "bpe.vocab", modelingUnit: "bpe")
    }

    public var requiredFiles: [String] { [encoder, decoder, joiner, tokens] }
    func path(_ name: String) -> String { directory.appendingPathComponent(name).path }
    var hasVocabulary: Bool { bpeVocabulary.map { FileManager.default.fileExists(atPath: path($0)) } ?? false }
}

/// Files of one non-streaming transducer used to re-decode finished utterances.
public struct OfflineTransducerModel: Sendable, Equatable {
    public var directory: URL
    public var encoder: String
    public var decoder: String
    public var joiner: String
    public var tokens: String
    public var bpeVocabulary: String?
    public var modelingUnit: String

    public init(directory: URL, encoder: String, decoder: String, joiner: String, tokens: String = "tokens.txt",
                bpeVocabulary: String? = nil, modelingUnit: String = "bpe") {
        self.directory = directory; self.encoder = encoder; self.decoder = decoder; self.joiner = joiner
        self.tokens = tokens; self.bpeVocabulary = bpeVocabulary; self.modelingUnit = modelingUnit
    }

    /// X-ASR zh-en offline Zipformer (2026-06): same tokenizer as the streaming model.
    public static func xasrOffline(at directory: URL) -> Self {
        .init(directory: directory, encoder: "encoder-epoch-99-avg-1.int8.onnx", decoder: "decoder-epoch-99-avg-1.onnx",
              joiner: "joiner-epoch-99-avg-1.int8.onnx", bpeVocabulary: "bpe.vocab", modelingUnit: "bpe")
    }

    public var requiredFiles: [String] { [encoder, decoder, joiner, tokens] }
    func path(_ name: String) -> String { directory.appendingPathComponent(name).path }
    var hasVocabulary: Bool { bpeVocabulary.map { FileManager.default.fileExists(atPath: path($0)) } ?? false }
}

enum HotwordsFile {
    static func clean(_ hotwords: String) -> [String] {
        hotwords
            .components(separatedBy: CharacterSet(charactersIn: ",，、\n\r;；"))
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
    }

    /// Writes a temporary hotwords file; the caller deletes it after the recognizer is created.
    static func write(_ words: [String]) throws -> String {
        guard !words.isEmpty else { return "" }
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("interviewhacker-hotwords-\(UUID().uuidString).txt")
        try words.joined(separator: "\n").write(to: url, atomically: true, encoding: .utf8)
        return url.path
    }
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
        try await configure(model: .legacyBilingual(at: modelDirectory), hotwords: hotwords, threads: threads)
    }

    public func configure(model: OnlineTransducerModel, hotwords: String = "", threads: Int = 2) async throws {
        try await withCheckedThrowingContinuation { continuation in
            queue.async {
                do {
                    try self.configureNow(model: model, hotwords: hotwords, threads: threads)
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
                let final = self.finishCurrentSegment(recognizer: recognizer, state: &state, tailPadding: 0.8)
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

    private func configureNow(model: OnlineTransducerModel, hotwords: String, threads: Int) throws {
        for name in model.requiredFiles where !FileManager.default.fileExists(atPath: model.path(name)) {
            throw ZipformerRuntimeError.missingModelFile(name)
        }
        // Hotwords need the SentencePiece vocabulary; without it they are skipped.
        let cleanedHotwords = model.hasVocabulary ? HotwordsFile.clean(hotwords) : []
        let key = model.directory.standardizedFileURL.path + "|" + model.encoder + "|" + cleanedHotwords.joined(separator: "\n")
        if configurationKey == key, recognizer != nil { return }
        guard streams.isEmpty else { throw ZipformerRuntimeError.activeStreams }
        if let recognizer { SherpaOnnxDestroyOnlineRecognizer(recognizer) }
        self.recognizer = nil

        let hotwordsFile = try HotwordsFile.write(cleanedHotwords)
        defer {
            if !hotwordsFile.isEmpty { try? FileManager.default.removeItem(atPath: hotwordsFile) }
        }

        let transducer = sherpaOnnxOnlineTransducerModelConfig(
            encoder: model.path(model.encoder), decoder: model.path(model.decoder), joiner: model.path(model.joiner))
        let modelConfig = sherpaOnnxOnlineModelConfig(
            tokens: model.path(model.tokens),
            transducer: transducer,
            numThreads: max(1, min(threads, 4)),
            provider: "cpu",
            modelType: "zipformer",
            modelingUnit: model.modelingUnit,
            bpeVocab: model.hasVocabulary ? model.path(model.bpeVocabulary ?? "") : "")
        let features = sherpaOnnxFeatureConfig(sampleRate: ZipformerModelFiles.sampleRate, featureDim: 80)
        let config = sherpaOnnxOnlineRecognizerConfig(
            featConfig: features,
            modelConfig: modelConfig,
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
                                      state: inout StreamState, tailPadding: Double = 0.4) -> ZipformerHypothesis? {
        // Chunked encoders hold back their right context; a little silence flushes the
        // last words of the utterance instead of cutting them off.
        if tailPadding > 0 {
            let padding = [Float](repeating: 0, count: Int(Double(ZipformerModelFiles.sampleRate) * tailPadding))
            padding.withUnsafeBufferPointer { buffer in
                SherpaOnnxOnlineStreamAcceptWaveform(state.pointer, Int32(ZipformerModelFiles.sampleRate), buffer.baseAddress, Int32(buffer.count))
            }
        }
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

/// Second pass: re-decodes each finished utterance with a non-streaming model, which
/// sees the whole utterance at once and is markedly more accurate than the streaming
/// pass (punctuation, code-switching, the last words of a question).
public final class OfflineSegmentRecognizer: @unchecked Sendable {
    public static let shared = OfflineSegmentRecognizer()

    private let queue = DispatchQueue(label: "app.interviewhacker.offline-asr", qos: .userInitiated)
    private var recognizer: OpaquePointer?
    private var configurationKey: String?
    // Readable from the main thread without waiting for a decode in progress.
    private let loadedLock = NSLock()
    private var loaded = false

    private init() {}

    public var isLoaded: Bool { loadedLock.withLock { loaded } }
    private func setLoaded(_ value: Bool) { loadedLock.withLock { loaded = value } }

    public func configure(model: OfflineTransducerModel, hotwords: String = "", threads: Int = 2) async throws {
        try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
            queue.async {
                do {
                    try self.configureNow(model: model, hotwords: hotwords, threads: threads)
                    continuation.resume()
                } catch {
                    continuation.resume(throwing: error)
                }
            }
        }
    }

    /// Returns the recognized text and the decoding time in seconds.
    public func recognize(_ samples: [Int16]) async -> (text: String, seconds: Double)? {
        guard samples.count >= ZipformerModelFiles.sampleRate / 4 else { return nil }
        return await withCheckedContinuation { continuation in
            queue.async {
                guard let recognizer = self.recognizer, let stream = SherpaOnnxCreateOfflineStream(recognizer) else {
                    continuation.resume(returning: nil); return
                }
                defer { SherpaOnnxDestroyOfflineStream(stream) }
                let started = ProcessInfo.processInfo.systemUptime
                var normalized = samples.map { Float($0) / 32_768 }
                // Same right-context flush as the streaming pass.
                normalized += [Float](repeating: 0, count: ZipformerModelFiles.sampleRate * 3 / 10)
                normalized.withUnsafeBufferPointer { buffer in
                    SherpaOnnxAcceptWaveformOffline(stream, Int32(ZipformerModelFiles.sampleRate), buffer.baseAddress, Int32(buffer.count))
                }
                SherpaOnnxDecodeOfflineStream(recognizer, stream)
                var text = ""
                if let result = SherpaOnnxGetOfflineStreamResult(stream) {
                    if let pointer = result.pointee.text { text = String(cString: pointer) }
                    SherpaOnnxDestroyOfflineRecognizerResult(result)
                }
                let seconds = ProcessInfo.processInfo.systemUptime - started
                continuation.resume(returning: (text.trimmingCharacters(in: .whitespacesAndNewlines), seconds))
            }
        }
    }

    public func unload() async {
        await withCheckedContinuation { continuation in
            queue.async {
                if let recognizer = self.recognizer { SherpaOnnxDestroyOfflineRecognizer(recognizer) }
                self.recognizer = nil
                self.configurationKey = nil
                self.setLoaded(false)
                continuation.resume()
            }
        }
    }

    private func configureNow(model: OfflineTransducerModel, hotwords: String, threads: Int) throws {
        for name in model.requiredFiles where !FileManager.default.fileExists(atPath: model.path(name)) {
            throw ZipformerRuntimeError.missingModelFile(name)
        }
        let cleanedHotwords = model.hasVocabulary ? HotwordsFile.clean(hotwords) : []
        let key = model.directory.standardizedFileURL.path + "|" + cleanedHotwords.joined(separator: "\n")
        if configurationKey == key, recognizer != nil { return }
        if let recognizer { SherpaOnnxDestroyOfflineRecognizer(recognizer) }
        recognizer = nil
        setLoaded(false)
        let hotwordsFile = try HotwordsFile.write(cleanedHotwords)
        defer { if !hotwordsFile.isEmpty { try? FileManager.default.removeItem(atPath: hotwordsFile) } }

        let transducer = sherpaOnnxOfflineTransducerModelConfig(
            encoder: model.path(model.encoder), decoder: model.path(model.decoder), joiner: model.path(model.joiner))
        let modelConfig = sherpaOnnxOfflineModelConfig(
            tokens: model.path(model.tokens),
            transducer: transducer,
            numThreads: max(1, min(threads, 4)),
            provider: "cpu",
            modelType: "transducer",
            modelingUnit: model.modelingUnit,
            bpeVocab: model.hasVocabulary ? model.path(model.bpeVocabulary ?? "") : "")
        let features = sherpaOnnxFeatureConfig(sampleRate: ZipformerModelFiles.sampleRate, featureDim: 80)
        var config = sherpaOnnxOfflineRecognizerConfig(
            featConfig: features,
            modelConfig: modelConfig,
            decodingMethod: "modified_beam_search",
            maxActivePaths: 4,
            hotwordsFile: hotwordsFile,
            hotwordsScore: 1.5)
        guard let created = withUnsafePointer(to: &config, SherpaOnnxCreateOfflineRecognizer) else {
            throw ZipformerRuntimeError.recognizerCreationFailed
        }
        recognizer = created
        configurationKey = key
        setLoaded(true)
    }
}
