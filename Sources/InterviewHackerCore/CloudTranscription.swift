import Foundation

/// Cloud streaming speech-to-text providers. All of them receive the same 16 kHz
/// mono Int16 audio the local engines use; audio leaves the Mac only after the
/// user explicitly enables a cloud provider and enters their own key.
public enum CloudSTTProvider: String, CaseIterable, Codable, Sendable, Identifiable {
    case soniox, openai, deepgram
    public var id: String { rawValue }
    public var title: String {
        switch self {
        case .soniox: "Soniox stt-rt-v5 · 中英混说最佳"
        case .openai: "OpenAI Realtime 转写"
        case .deepgram: "Deepgram Nova-3 · 单语"
        }
    }
    public var keychainAccount: String { "stt.\(rawValue)" }
    public var defaultModel: String {
        switch self {
        case .soniox: "stt-rt-v5"
        case .openai: "gpt-realtime-whisper"
        case .deepgram: "nova-3"
        }
    }
    /// OpenAI Realtime only accepts 24 kHz PCM.
    public var sampleRate: Int { self == .openai ? 24_000 : 16_000 }

    public func endpoint(model: String, language: String, vocabulary: [String]) -> URL? {
        switch self {
        case .soniox:
            return URL(string: "wss://stt-rt.soniox.com/transcribe-websocket")
        case .openai:
            return URL(string: "wss://api.openai.com/v1/realtime?intent=transcription")
        case .deepgram:
            var components = URLComponents(string: "wss://api.deepgram.com/v1/listen")!
            var items = [
                URLQueryItem(name: "model", value: model.isEmpty ? defaultModel : model),
                URLQueryItem(name: "encoding", value: "linear16"),
                URLQueryItem(name: "sample_rate", value: "16000"),
                URLQueryItem(name: "channels", value: "1"),
                // Deepgram's "multi" does not include Chinese; a single language is required.
                URLQueryItem(name: "language", value: language == "en" ? "en" : "zh-CN"),
                URLQueryItem(name: "interim_results", value: "true"),
                URLQueryItem(name: "endpointing", value: "300"),
                URLQueryItem(name: "utterance_end_ms", value: "1000"),
                URLQueryItem(name: "smart_format", value: "true"),
                URLQueryItem(name: "punctuate", value: "true"),
            ]
            items += vocabulary.prefix(50).map { URLQueryItem(name: "keyterm", value: $0) }
            components.queryItems = items
            return components.url
        }
    }

    public func authorizationHeader(apiKey: String) -> String {
        self == .deepgram ? "Token \(apiKey)" : "Bearer \(apiKey)"
    }

    /// First text message after the socket opens (nil when configuration is in the URL).
    public func configurationMessage(model: String, language: String, vocabulary: [String]) -> String? {
        let languages = language == "auto" ? ["zh", "en"] : [language]
        let object: [String: Any]
        switch self {
        case .soniox:
            var config: [String: Any] = [
                "model": model.isEmpty ? defaultModel : model,
                "audio_format": "pcm_s16le",
                "sample_rate": 16_000,
                "num_channels": 1,
                "language_hints": languages,
                "enable_endpoint_detection": true,
                "enable_language_identification": language == "auto",
            ]
            if !vocabulary.isEmpty { config["context"] = ["terms": Array(vocabulary.prefix(100))] }
            object = config
        case .openai:
            var transcription: [String: Any] = ["model": model.isEmpty ? defaultModel : model]
            if (model.isEmpty ? defaultModel : model) == "gpt-realtime-whisper" {
                transcription["delay"] = "low"
                if language != "auto" { transcription["language"] = language }
            } else {
                transcription["languages"] = languages
                if !vocabulary.isEmpty { transcription["keywords"] = Array(vocabulary.prefix(50)) }
            }
            object = [
                "type": "session.update",
                "session": [
                    "type": "transcription",
                    "audio": ["input": [
                        "format": ["type": "audio/pcm", "rate": 24_000],
                        "transcription": transcription,
                        "turn_detection": ["type": "server_vad", "threshold": 0.5, "prefix_padding_ms": 300, "silence_duration_ms": 500],
                        "noise_reduction": ["type": "near_field"],
                    ] as [String: Any]],
                ] as [String: Any],
            ]
        case .deepgram:
            return nil
        }
        guard let data = try? JSONSerialization.data(withJSONObject: object, options: [.sortedKeys]) else { return nil }
        return String(decoding: data, as: UTF8.self)
    }

    /// Keep-alive / end-of-stream control messages.
    public var keepAliveMessage: String? {
        switch self {
        case .soniox: #"{"type":"keepalive"}"#
        case .deepgram: #"{"type":"KeepAlive"}"#
        case .openai: nil
        }
    }
    public var closeMessage: String {
        switch self {
        case .soniox: ""
        case .deepgram: #"{"type":"CloseStream"}"#
        case .openai: #"{"type":"input_audio_buffer.commit"}"#
        }
    }

    /// OpenAI expects base64 audio inside JSON; others take binary frames.
    public var sendsBinaryAudio: Bool { self != .openai }

    public static func openAIAppendMessage(_ pcm24k: Data) -> String {
        #"{"type":"input_audio_buffer.append","audio":""# + pcm24k.base64EncodedString() + #""}"#
    }
}

public struct CloudTranscriptUpdate: Equatable, Sendable {
    /// Text of the utterance currently being recognized (final prefix + live tail).
    public var text: String
    /// True when the utterance ended; `text` is then committed and the next update starts fresh.
    public var isFinal: Bool
    public var startMs: Int?
    public var endMs: Int?
    public init(text: String, isFinal: Bool, startMs: Int? = nil, endMs: Int? = nil) {
        self.text = text; self.isFinal = isFinal; self.startMs = startMs; self.endMs = endMs
    }
}

public enum CloudTranscriptError: Error, Equatable, LocalizedError {
    case provider(String)
    public var errorDescription: String? {
        if case let .provider(message) = self { return message }
        return nil
    }
}

/// Turns provider messages into utterance-level updates. Stateful: Soniox sends final
/// tokens once and re-sends the non-final tail; Deepgram sends finalized segments
/// until `speech_final`; OpenAI sends deltas per item and a completed transcript.
public struct CloudTranscriptAssembler: Sendable {
    public let provider: CloudSTTProvider
    private var finalText = ""
    private var startMs: Int?
    private var lastEndMs: Int?
    private var openAIItems: [String: String] = [:]
    private var openAIOrder: [String] = []

    public init(provider: CloudSTTProvider) { self.provider = provider }

    public mutating func consume(_ message: String) throws -> [CloudTranscriptUpdate] {
        guard let data = message.data(using: .utf8),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return [] }
        switch provider {
        case .soniox: return try consumeSoniox(object)
        case .deepgram: return try consumeDeepgram(object)
        case .openai: return try consumeOpenAI(object)
        }
    }

    private mutating func consumeSoniox(_ object: [String: Any]) throws -> [CloudTranscriptUpdate] {
        if let code = object["error_code"] {
            throw CloudTranscriptError.provider("Soniox 错误 \(code)：\(object["error_message"] as? String ?? "")")
        }
        var updates: [CloudTranscriptUpdate] = []
        var tail = ""
        for token in object["tokens"] as? [[String: Any]] ?? [] {
            let text = token["text"] as? String ?? ""
            let isFinal = token["is_final"] as? Bool ?? false
            if text == "<end>" || text == "<fin>" {
                if !finalText.trimmingCharacters(in: .whitespaces).isEmpty {
                    updates.append(.init(text: clean(finalText), isFinal: true, startMs: startMs, endMs: lastEndMs))
                }
                finalText = ""; startMs = nil; tail = ""
                continue
            }
            if startMs == nil { startMs = token["start_ms"] as? Int }
            if let end = token["end_ms"] as? Int { lastEndMs = end }
            if isFinal { finalText += text } else { tail += text }
        }
        let live = clean(finalText + tail)
        if !live.isEmpty { updates.append(.init(text: live, isFinal: false, startMs: startMs, endMs: lastEndMs)) }
        if object["finished"] as? Bool == true, !clean(finalText).isEmpty {
            updates.append(.init(text: clean(finalText), isFinal: true, startMs: startMs, endMs: lastEndMs))
            finalText = ""; startMs = nil
        }
        return updates
    }

    private mutating func consumeDeepgram(_ object: [String: Any]) throws -> [CloudTranscriptUpdate] {
        if let error = object["err_msg"] as? String ?? (object["type"] as? String == "Error" ? object["description"] as? String : nil) {
            throw CloudTranscriptError.provider("Deepgram 错误：\(error)")
        }
        let type = object["type"] as? String ?? "Results"
        if type == "UtteranceEnd" {
            guard !clean(finalText).isEmpty else { return [] }
            defer { finalText = ""; startMs = nil }
            return [.init(text: clean(finalText), isFinal: true, startMs: startMs, endMs: lastEndMs)]
        }
        guard type == "Results",
              let alternative = ((object["channel"] as? [String: Any])?["alternatives"] as? [[String: Any]])?.first else { return [] }
        let transcript = alternative["transcript"] as? String ?? ""
        let start = (object["start"] as? Double).map { Int($0 * 1_000) }
        let duration = (object["duration"] as? Double).map { Int($0 * 1_000) } ?? 0
        if startMs == nil, !transcript.isEmpty { startMs = start }
        if let start { lastEndMs = start + duration }
        let isFinal = object["is_final"] as? Bool ?? false
        let speechFinal = object["speech_final"] as? Bool ?? false
        if isFinal {
            if !transcript.isEmpty { finalText += (finalText.isEmpty ? "" : " ") + transcript }
            if speechFinal, !clean(finalText).isEmpty {
                defer { finalText = ""; startMs = nil }
                return [.init(text: clean(finalText), isFinal: true, startMs: startMs, endMs: lastEndMs)]
            }
            return clean(finalText).isEmpty ? [] : [.init(text: clean(finalText), isFinal: false, startMs: startMs, endMs: lastEndMs)]
        }
        let live = clean(finalText + (finalText.isEmpty || transcript.isEmpty ? "" : " ") + transcript)
        return live.isEmpty ? [] : [.init(text: live, isFinal: false, startMs: startMs, endMs: lastEndMs)]
    }

    private mutating func consumeOpenAI(_ object: [String: Any]) throws -> [CloudTranscriptUpdate] {
        let type = object["type"] as? String ?? ""
        switch type {
        case "error":
            let message = (object["error"] as? [String: Any])?["message"] as? String ?? "未知错误"
            throw CloudTranscriptError.provider("OpenAI 转写错误：\(message)")
        case "conversation.item.input_audio_transcription.delta":
            guard let item = object["item_id"] as? String, let delta = object["delta"] as? String else { return [] }
            if openAIItems[item] == nil { openAIOrder.append(item) }
            openAIItems[item, default: ""] += delta
            return [.init(text: clean(openAIItems[item] ?? ""), isFinal: false)]
        case "conversation.item.input_audio_transcription.completed":
            guard let item = object["item_id"] as? String else { return [] }
            let transcript = clean(object["transcript"] as? String ?? openAIItems[item] ?? "")
            openAIItems.removeValue(forKey: item)
            openAIOrder.removeAll { $0 == item }
            return transcript.isEmpty ? [] : [.init(text: transcript, isFinal: true)]
        case "conversation.item.input_audio_transcription.failed":
            let message = (object["error"] as? [String: Any])?["message"] as? String ?? "片段转写失败"
            if let item = object["item_id"] as? String { openAIItems.removeValue(forKey: item); openAIOrder.removeAll { $0 == item } }
            throw CloudTranscriptError.provider("OpenAI 转写：\(message)")
        default:
            return []
        }
    }

    private func clean(_ text: String) -> String { text.trimmingCharacters(in: .whitespacesAndNewlines) }
}

/// 16 kHz → 24 kHz linear-interpolation resampler for OpenAI Realtime (2:3).
public enum PCMResampler {
    public static func upsample16kTo24k(_ samples: [Int16]) -> [Int16] {
        guard samples.count > 1 else { return samples }
        let outputCount = samples.count * 3 / 2
        var output = [Int16](repeating: 0, count: outputCount)
        for index in 0..<outputCount {
            let position = Double(index) * 2.0 / 3.0
            let left = Int(position)
            let right = min(left + 1, samples.count - 1)
            let fraction = position - Double(left)
            output[index] = Int16(Double(samples[left]) * (1 - fraction) + Double(samples[right]) * fraction)
        }
        return output
    }

    public static func littleEndianData(_ samples: [Int16]) -> Data {
        var data = Data(capacity: samples.count * 2)
        for sample in samples {
            let value = UInt16(bitPattern: sample).littleEndian
            data.append(UInt8(value & 0xFF)); data.append(UInt8(value >> 8))
        }
        return data
    }
}
