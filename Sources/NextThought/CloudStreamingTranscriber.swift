import Foundation
import NextThoughtCore

/// Streaming cloud recognition over a WebSocket (Soniox / OpenAI Realtime / Deepgram).
/// Used only after the user enables cloud recognition and enters their own key.
@MainActor
final class CloudStreamingTranscriber: LiveTranscribing {
    private let provider: CloudSTTProvider
    private let apiKey: String
    private let model: String
    private let vocabulary: [String]
    private var socket: URLSessionWebSocketTask?
    private var receiveTask: Task<Void, Never>?
    private var sink: CloudAudioSink?
    private var generation = UUID()
    private var finished: CheckedContinuation<Void, Never>?
    private let session = URLSession(configuration: .ephemeral)

    init(provider: CloudSTTProvider, apiKey: String, model: String, vocabulary: String) {
        self.provider = provider
        self.apiKey = apiKey.trimmingCharacters(in: .whitespacesAndNewlines)
        self.model = model.trimmingCharacters(in: .whitespacesAndNewlines)
        self.vocabulary = vocabulary.split(whereSeparator: { $0 == "," || $0 == "，" || $0 == "\n" || $0 == "、" })
            .map { $0.trimmingCharacters(in: .whitespaces) }.filter { !$0.isEmpty }
    }

    func start(language: String, onResult: @escaping (String, Bool, Date, Date, Double?, Double?) -> Void,
               onError: @escaping (String) -> Void) async throws {
        await cancel()
        let id = UUID(); generation = id
        guard let url = provider.endpoint(model: model, language: language, vocabulary: vocabulary) else {
            throw AppFailure.transcription("云端识别地址无效。")
        }
        var request = URLRequest(url: url)
        request.setValue(provider.authorizationHeader(apiKey: apiKey), forHTTPHeaderField: "Authorization")
        let task = session.webSocketTask(with: request)
        socket = task
        task.resume()
        if let configuration = provider.configurationMessage(model: model, language: language, vocabulary: vocabulary) {
            try await task.send(.string(configuration))
        }
        let audioSink = CloudAudioSink(task: task, provider: provider) { message in
            Task { @MainActor [weak self] in
                guard self?.generation == id else { return }
                onError(message)
            }
        }
        sink = audioSink
        let provider = provider
        Diagnostics.record("cloud_stt.started", ["provider": provider.rawValue])
        receiveTask = Task { [weak self] in
            var assembler = CloudTranscriptAssembler(provider: provider)
            while !Task.isCancelled {
                do {
                    let message = try await task.receive()
                    guard let self, self.generation == id else { return }
                    let text: String
                    switch message {
                    case let .string(value): text = value
                    case let .data(value): text = String(decoding: value, as: UTF8.self)
                    @unknown default: continue
                    }
                    let origin = audioSink.origin ?? Date()
                    for update in try assembler.consume(text) {
                        let start = update.startMs.map { origin.addingTimeInterval(Double($0) / 1_000) } ?? Date()
                        let end = update.endMs.map { origin.addingTimeInterval(Double($0) / 1_000) } ?? Date()
                        onResult(update.text, update.isFinal, start, max(start, end), nil, nil)
                    }
                } catch {
                    guard let self, self.generation == id else { return }
                    if let continuation = self.finished { self.finished = nil; continuation.resume(); return }
                    if Task.isCancelled { return }
                    onError("云端识别中断：\(error.localizedDescription)。请检查网络与 API Key，或切回本地识别。")
                    return
                }
            }
        }
    }

    func inputHandler() -> (@Sendable ([Int16], Date) -> Void) {
        let sink = sink
        return { samples, origin in sink?.append(samples, origin: origin) }
    }

    func commitUtterance(_ event: SpeechSegmentEvent?) async {
        guard provider == .openai else { return }
        sink?.commit()
    }

    func finish() async {
        guard let socket else { return }
        if provider == .openai {
            sink?.commit()
        } else {
            sink?.close()
            try? await socket.send(.string(provider.closeMessage))
        }
        // Wait briefly for the provider to flush final results, then close.
        await withCheckedContinuation { (continuation: CheckedContinuation<Void, Never>) in
            finished = continuation
            Task { @MainActor [weak self] in
                try? await Task.sleep(for: .seconds(2))
                if let pending = self?.finished { self?.finished = nil; pending.resume() }
            }
        }
        await cancel()
    }

    func cancel() async {
        generation = UUID()
        sink?.close(); sink = nil
        receiveTask?.cancel(); receiveTask = nil
        socket?.cancel(with: .normalClosure, reason: nil); socket = nil
        if let pending = finished { finished = nil; pending.resume() }
    }
}

/// Called on the capture queue. Bounded in-flight sends: if the network cannot keep
/// up, recognition stops with an error instead of silently dropping audio.
private final class CloudAudioSink: @unchecked Sendable {
    private let lock = NSLock()
    private weak var task: URLSessionWebSocketTask?
    private let provider: CloudSTTProvider
    private let onError: @Sendable (String) -> Void
    private var inFlight = 0
    private var samplesPending = 0
    private var closed = false
    private var streamOrigin: Date?
    var origin: Date? { lock.withLock { streamOrigin } }

    init(task: URLSessionWebSocketTask, provider: CloudSTTProvider, onError: @escaping @Sendable (String) -> Void) {
        self.task = task; self.provider = provider; self.onError = onError
    }

    func append(_ samples: [Int16], origin: Date) {
        let accepted: Bool = lock.withLock {
            guard !closed, task != nil else { return false }
            if streamOrigin == nil { streamOrigin = origin }
            guard inFlight < 80 else { closed = true; return false }
            inFlight += 1
            samplesPending += samples.count
            return true
        }
        guard accepted, let task else {
            if lock.withLock({ closed && inFlight >= 80 }) { onError("网络上传速度跟不上实时音频，已停止云端识别。") }
            return
        }
        let message: URLSessionWebSocketTask.Message
        if provider.sendsBinaryAudio {
            message = .data(PCMResampler.littleEndianData(samples))
        } else {
            message = .string(CloudSTTProvider.openAIAppendMessage(PCMResampler.littleEndianData(PCMResampler.upsample16kTo24k(samples))))
        }
        task.send(message) { [weak self] error in
            guard let self else { return }
            self.lock.withLock { self.inFlight -= 1 }
            if error != nil, !self.lock.withLock({ self.closed }) {
                self.close()
                self.onError("云端识别发送失败：\(error!.localizedDescription)")
            }
        }
    }

    func commit() {
        let shouldSend: Bool = lock.withLock {
            guard !closed, samplesPending > 0, task != nil else { return false }
            samplesPending = 0
            return true
        }
        guard shouldSend, let task else { return }
        task.send(.string(#"{"type":"input_audio_buffer.commit"}"#)) { [weak self] error in
            guard let self, error != nil, !self.lock.withLock({ self.closed }) else { return }
            self.close()
            self.onError("OpenAI 转写无法提交音频：\(error!.localizedDescription)")
        }
    }

    func close() { lock.withLock { closed = true } }
}
