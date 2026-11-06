import Foundation
import Testing
@testable import NextThoughtCore

struct ZipformerStreamingIntegrationTests {
    @Test func bilingualFixturesAndStreamingLifecycle() async throws {
        guard let modelPath = ProcessInfo.processInfo.environment["IH_ZIPFORMER_MODEL_DIR"] else {
            return
        }
        let modelDirectory = URL(fileURLWithPath: modelPath, isDirectory: true)
        let runtime = ZipformerRuntime.shared
        try await runtime.configure(modelDirectory: modelDirectory)

        let english = try WaveFile.read("en")
        let chinese = try WaveFile.read("zh")
        let mixed = try WaveFile.read("mixed")
        #expect(english.sampleRate == 16_000)
        #expect(english.channels == 1)
        #expect(chinese.sampleRate == 16_000)
        #expect(mixed.sampleRate == 16_000)

        let systemStream = try await runtime.makeStream()
        let microphoneStream = try await runtime.makeStream()
        let systemCapture = ZipformerCapture()
        let microphoneCapture = ZipformerCapture()
        let systemStart = Date()
        let microphoneStart = systemStart
        let systemChunks = chunks(english.samples)
        let microphoneChunks = chunks(chinese.samples)
        let microphoneSilenceChunks = 8
        let concurrentChunkCount = max(systemChunks.count, microphoneChunks.count + microphoneSilenceChunks)
        var systemVAD = VoiceActivitySegmenter()
        var microphoneVAD = VoiceActivitySegmenter()

        for index in 0..<concurrentChunkCount {
            try await Task.sleep(for: .milliseconds(100))
            if systemChunks.indices.contains(index) {
                try await accept(systemChunks[index], stream: systemStream, origin: systemStart,
                                 runtime: runtime, capture: systemCapture, vad: &systemVAD)
            }
            if microphoneChunks.indices.contains(index) {
                try await accept(microphoneChunks[index], stream: microphoneStream, origin: microphoneStart,
                                 runtime: runtime, capture: microphoneCapture, vad: &microphoneVAD)
            } else if index < microphoneChunks.count + microphoneSilenceChunks {
                try await accept([Int16](repeating: 0, count: 1_600), stream: microphoneStream,
                                 origin: microphoneStart, runtime: runtime, capture: microphoneCapture,
                                 vad: &microphoneVAD)
            }
        }

        let englishFinal = await runtime.finishStream(systemStream)
        if let englishFinal { systemCapture.append(englishFinal) }
        let chineseTail = await runtime.finishStream(microphoneStream)
        if let chineseTail { microphoneCapture.append(chineseTail) }
        let systemAfterFinish = await runtime.finishStream(systemStream)
        let microphoneAfterFinish = await runtime.finishStream(microphoneStream)

        let englishFinals = systemCapture.finalUpdates
        let chineseFinals = microphoneCapture.finalUpdates
        #expect(!englishFinals.isEmpty, "Stopping the system-audio stream should commit its final tail.")
        #expect(!chineseFinals.isEmpty, "The microphone endpoint should commit its speech.")
        #expect(chineseTail == nil, "The silence endpoint already committed the microphone stream.")
        #expect(systemAfterFinish == nil && microphoneAfterFinish == nil, "A finished stream must not commit twice.")
        let englishText = englishFinals.map(\.text).joined(separator: " ").lowercased()
        let chineseText = chineseFinals.map(\.text).joined(separator: " ")
        #expect(englishText.range(of: "[a-z]", options: .regularExpression) != nil, "English result: \(englishText)")
        #expect(chineseText.contains("数据库"), "Chinese result: \(chineseText)")
        #expect(!englishFinals.isEmpty, "Stopping should submit the remaining English stream tail.")
        #expect(chineseFinals.count == 1, "One speech utterance should produce one final result at the VAD endpoint.")
        #expect(englishText != chineseText, "Two source streams must keep separate decoder state.")
        print("ZIPFORMER_TEXT case=dual-en-stop text=\(englishText)")
        print("ZIPFORMER_TEXT case=dual-zh-endpoint text=\(chineseText)")
        recordMetrics("dual-en-stop", capture: systemCapture, mediaSeconds: english.seconds, audioStart: systemStart)
        recordMetrics("dual-zh-endpoint", capture: microphoneCapture, mediaSeconds: chinese.seconds, audioStart: microphoneStart)

        let shortCapture = try await transcribe(
            Array(english.samples.prefix(3 * english.sampleRate)),
            runtime: runtime,
            endpointSilence: false,
            paced: false)
        #expect(!shortCapture.finalUpdates.isEmpty)
        #expect(shortCapture.finalUpdates.contains { !$0.text.isEmpty })
        recordMetrics("short-stop-tail", capture: shortCapture, mediaSeconds: 3, audioStart: nil)

        try await runtime.configure(modelDirectory: modelDirectory,
                                    hotwords: "system design\ncache invalidation\ndatabase consistency")
        let mixedStart = Date()
        let mixedCapture = try await transcribe(mixed.samples, runtime: runtime, endpointSilence: true, paced: true)
        #expect(!mixedCapture.finalUpdates.isEmpty)
        let mixedText = mixedCapture.finalUpdates.map(\.text).joined(separator: " ").lowercased()
        #expect(mixedText.contains("system") || mixedText.contains("design"), "Mixed-language result: \(mixedText)")
        #expect(mixedText.range(of: "[\\p{Han}]", options: .regularExpression) != nil,
                "A mixed-language result should retain its Chinese component: \(mixedText)")
        #expect(mixedCapture.finalUpdates.count == 1, "The VAD endpoint should submit the mixed utterance once.")
        print("ZIPFORMER_TEXT case=mixed-endpoint text=\(mixedText)")
        recordMetrics("mixed-endpoint", capture: mixedCapture, mediaSeconds: mixed.seconds,
                      audioStart: mixedStart)

        let silenceCapture = try await transcribe(
            [Int16](repeating: 0, count: 16_000),
            runtime: runtime,
            endpointSilence: false,
            paced: true)
        #expect(silenceCapture.updates.isEmpty, "Silence must not create a transcript.")

        let longSamples = english.samples
            + [Int16](repeating: 0, count: 3_200)
            + chinese.samples
            + [Int16](repeating: 0, count: 3_200)
            + mixed.samples
        let longCapture = try await transcribe(longSamples, runtime: runtime, endpointSilence: false, paced: false)
        #expect(longCapture.finalUpdates.count >= 1)
        #expect(longCapture.finalUpdates.contains { !$0.text.isEmpty })
        recordMetrics("long-stop-tail", capture: longCapture, mediaSeconds: Double(longSamples.count) / 16_000,
                      audioStart: nil)

        if let publicDirectory = ProcessInfo.processInfo.environment["IH_ZIPFORMER_PUBLIC_AUDIO_DIR"] {
            let samples: [(file: String, label: String, needsChinese: Bool, needsEnglish: Bool)] = [
                ("english.wav", "public-en", false, true),
                ("chinese.wav", "public-zh", true, false),
                ("mixed.wav", "public-mixed", true, true)
            ]
            for sample in samples {
                let url = URL(fileURLWithPath: publicDirectory, isDirectory: true)
                    .appendingPathComponent(sample.file)
                let publicAudio = try WaveFile.read(at: url)
                let publicAudioStart = Date()
                let publicCapture = try await transcribe(publicAudio.samples, runtime: runtime,
                    endpointSilence: true, paced: true, audioStart: publicAudioStart)
                let publicText = publicCapture.finalUpdates.map(\.text).joined(separator: " | ")
                #expect(!publicText.isEmpty, "\(sample.label) should produce a final transcript.")
                if sample.needsChinese {
                    #expect(publicText.range(of: "[\\p{Han}]", options: .regularExpression) != nil,
                            "\(sample.label) should retain Chinese: \(publicText)")
                }
                if sample.needsEnglish {
                    #expect(publicText.range(of: "[a-zA-Z]", options: .regularExpression) != nil,
                            "\(sample.label) should retain English: \(publicText)")
                }
                #expect(!publicCapture.finalUpdates.isEmpty,
                        "\(sample.label) should drain and finalize at least one speech segment.")
                print("ZIPFORMER_PUBLIC_AUDIO case=\(sample.label) text=\(publicText)")
                recordMetrics(sample.label, capture: publicCapture,
                    mediaSeconds: publicAudio.seconds, audioStart: publicAudioStart)
            }
        }

        let resetStream = try await runtime.makeStream()
        let resetCapture = ZipformerCapture()
        let resetOrigin = Date()
        let resetOffset = 3 * english.sampleRate
        for chunk in chunks(Array(english.samples.prefix(resetOffset))) {
            runtime.accept(chunk, streamID: resetStream, sourceOrigin: resetOrigin) {
                resetCapture.append($0)
            }
        }
        let deviceSwitchFinal = try await runtime.resetStream(resetStream)
        if let deviceSwitchFinal { resetCapture.append(deviceSwitchFinal) }
        for chunk in chunks(Array(english.samples.dropFirst(resetOffset))) {
            runtime.accept(chunk, streamID: resetStream, sourceOrigin: resetOrigin) {
                resetCapture.append($0)
            }
        }
        let resumedTail = await runtime.finishStream(resetStream)
        if let resumedTail { resetCapture.append(resumedTail) }
        let duplicateTail = await runtime.finishStream(resetStream)
        #expect(deviceSwitchFinal != nil, "A device reset should drain and commit the old stream tail.")
        #expect(resumedTail != nil, "The replacement stream should commit audio received after the reset.")
        #expect(duplicateTail == nil, "Finishing a reset stream twice must not duplicate its final result.")
        #expect(resetCapture.finalUpdates.count == 2, "The reset and resumed tails should each finalize once.")
        print("ZIPFORMER_TEXT case=device-reset finals=\(resetCapture.finalUpdates.map(\.text).joined(separator: " | "))")
        await runtime.unload()
    }

    private func transcribe(_ samples: [Int16], runtime: ZipformerRuntime,
                            endpointSilence: Bool, paced: Bool, audioStart: Date? = nil) async throws -> ZipformerCapture {
        let stream = try await runtime.makeStream()
        let capture = ZipformerCapture()
        let origin = audioStart ?? Date()
        var vad = VoiceActivitySegmenter()
        for chunk in chunks(samples) {
            if paced { try await Task.sleep(for: .milliseconds(100)) }
            try await accept(chunk, stream: stream, origin: origin, runtime: runtime,
                             capture: capture, vad: &vad)
        }
        if endpointSilence {
            for _ in 0..<8 {
                if paced { try await Task.sleep(for: .milliseconds(100)) }
                try await accept([Int16](repeating: 0, count: 1_600), stream: stream,
                                 origin: origin, runtime: runtime, capture: capture, vad: &vad)
            }
        }
        if let final = await runtime.finishStream(stream) { capture.append(final) }
        return capture
    }

    private func accept(_ samples: [Int16], stream: UUID, origin: Date,
                        runtime: ZipformerRuntime, capture: ZipformerCapture,
                        vad: inout VoiceActivitySegmenter) async throws {
        runtime.accept(samples, streamID: stream, sourceOrigin: origin) { capture.append($0) }
        let events = vad.append(samples)
        for event in events where event.kind == .final {
            if let final = try await runtime.commitStream(stream) { capture.append(final) }
        }
    }

    private func chunks(_ samples: [Int16]) -> [[Int16]] {
        stride(from: 0, to: samples.count, by: 1_600).map { start in
            Array(samples[start..<min(start + 1_600, samples.count)])
        }
    }

    private func recordMetrics(_ name: String, capture: ZipformerCapture, mediaSeconds: Double,
                               audioStart: Date?) {
        let updates = capture.updates
        let partials = updates.filter { !$0.hypothesis.isFinal }
        let finals = updates.filter { $0.hypothesis.isFinal }
        let inferenceSeconds = finals.reduce(0) { $0 + $1.hypothesis.inferenceSeconds }
        let rtf = inferenceSeconds / max(0.001, mediaSeconds)
        let firstOutputLag = partials.first.map { $0.receivedAt.timeIntervalSince($0.hypothesis.endedAt) } ?? -1
        let finalOutputLag = finals.last.map { $0.receivedAt.timeIntervalSince($0.hypothesis.endedAt) } ?? -1
        let finalFromSpeechEnd = audioStart.flatMap { start in
            finals.last.map { $0.receivedAt.timeIntervalSince(start.addingTimeInterval(mediaSeconds)) }
        } ?? -1
        print(String(format: "ZIPFORMER_METRIC case=%@ audio_s=%.3f inference_s=%.3f rtf=%.3f updates=%d partials=%d first_output_lag_s=%.3f final_lag_s=%.3f",
                     name, mediaSeconds, inferenceSeconds, rtf, updates.count, partials.count,
                     firstOutputLag, finalOutputLag))
        if finalFromSpeechEnd >= 0 {
            print(String(format: "ZIPFORMER_METRIC case=%@ final_from_speech_end_s=%.3f", name, finalFromSpeechEnd))
        }
    }
}

private final class ZipformerCapture: @unchecked Sendable {
    struct Update {
        let hypothesis: ZipformerHypothesis
        let receivedAt: Date
    }

    private let lock = NSLock()
    private var storedUpdates: [Update] = []

    var updates: [Update] {
        lock.lock()
        defer { lock.unlock() }
        return storedUpdates
    }

    var finalUpdates: [ZipformerHypothesis] { updates.map(\.hypothesis).filter(\.isFinal) }

    func append(_ hypothesis: ZipformerHypothesis) {
        lock.lock()
        storedUpdates.append(Update(hypothesis: hypothesis, receivedAt: Date()))
        lock.unlock()
    }
}

private struct WaveFile {
    let sampleRate: Int
    let channels: Int
    let samples: [Int16]
    var seconds: Double { Double(samples.count) / Double(sampleRate) }

    static func read(_ name: String) throws -> WaveFile {
        let root = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        let url = root.appendingPathComponent("Tools/Validation/Fixtures/\(name).wav")
        return try read(at: url)
    }

    static func read(at url: URL) throws -> WaveFile {
        let data = try Data(contentsOf: url)
        let bytes = [UInt8](data)
        guard bytes.count >= 12, String(decoding: bytes[0..<4], as: UTF8.self) == "RIFF",
              String(decoding: bytes[8..<12], as: UTF8.self) == "WAVE" else {
            throw ZipformerFixtureError.invalid("\(url.lastPathComponent) is not a RIFF/WAVE file.")
        }

        var sampleRate = 0
        var channels = 0
        var bitsPerSample = 0
        var pcm: [UInt8]?
        var position = 12
        while position + 8 <= bytes.count {
            let kind = String(decoding: bytes[position..<(position + 4)], as: UTF8.self)
            let size = Int(littleUInt32(bytes, position + 4))
            let start = position + 8
            let end = min(bytes.count, start + size)
            if kind == "fmt ", end - start >= 16 {
                channels = Int(littleUInt16(bytes, start + 2))
                sampleRate = Int(littleUInt32(bytes, start + 4))
                bitsPerSample = Int(littleUInt16(bytes, start + 14))
            } else if kind == "data" {
                pcm = Array(bytes[start..<end])
            }
            position = start + size + (size % 2)
        }
        guard let pcm, sampleRate > 0, channels == 1, bitsPerSample == 16 else {
            throw ZipformerFixtureError.invalid("\(url.lastPathComponent) is not 16 kHz mono Int16 PCM.")
        }
        let samples = stride(from: 0, to: pcm.count - 1, by: 2).map { index in
            Int16(bitPattern: UInt16(pcm[index]) | UInt16(pcm[index + 1]) << 8)
        }
        return WaveFile(sampleRate: sampleRate, channels: channels, samples: samples)
    }

    private static func littleUInt16(_ bytes: [UInt8], _ offset: Int) -> UInt16 {
        UInt16(bytes[offset]) | UInt16(bytes[offset + 1]) << 8
    }

    private static func littleUInt32(_ bytes: [UInt8], _ offset: Int) -> UInt32 {
        UInt32(bytes[offset]) | UInt32(bytes[offset + 1]) << 8
            | UInt32(bytes[offset + 2]) << 16 | UInt32(bytes[offset + 3]) << 24
    }
}

private enum ZipformerFixtureError: Error {
    case invalid(String)
}
