import Foundation
import Testing
@testable import InterviewHackerCore

struct CloudTranscriptionTests {
    @Test func sonioxFinalTokensAccumulateAndEndTokenCommits() throws {
        var assembler = CloudTranscriptAssembler(provider: .soniox)
        var updates = try assembler.consume(#"{"tokens":[{"text":"你好","is_final":true,"start_ms":100,"end_ms":400},{"text":" wor","is_final":false,"end_ms":500}]}"#)
        #expect(updates.last == CloudTranscriptUpdate(text: "你好 wor", isFinal: false, startMs: 100, endMs: 500))
        updates = try assembler.consume(#"{"tokens":[{"text":" world","is_final":true,"end_ms":700},{"text":"<end>","is_final":true}]}"#)
        #expect(updates.first == CloudTranscriptUpdate(text: "你好 world", isFinal: true, startMs: 100, endMs: 700))
        // Next utterance starts fresh.
        updates = try assembler.consume(#"{"tokens":[{"text":"Next","is_final":false,"start_ms":900,"end_ms":1000}]}"#)
        #expect(updates.last?.text == "Next")
        #expect(throws: CloudTranscriptError.self) { try assembler.consume(#"{"error_code":401,"error_message":"bad key"}"#) }
    }

    @Test func deepgramInterimThenSpeechFinal() throws {
        var assembler = CloudTranscriptAssembler(provider: .deepgram)
        var updates = try assembler.consume(#"{"type":"Results","is_final":false,"speech_final":false,"start":1.0,"duration":0.5,"channel":{"alternatives":[{"transcript":"你好"}]}}"#)
        #expect(updates == [CloudTranscriptUpdate(text: "你好", isFinal: false, startMs: 1000, endMs: 1500)])
        updates = try assembler.consume(#"{"type":"Results","is_final":true,"speech_final":true,"start":1.0,"duration":0.8,"channel":{"alternatives":[{"transcript":"你好吗"}]}}"#)
        #expect(updates.first?.text == "你好吗")
        #expect(updates.first?.isFinal == true)
    }

    @Test func openAIDeltasAndCompletion() throws {
        var assembler = CloudTranscriptAssembler(provider: .openai)
        _ = try assembler.consume(#"{"type":"conversation.item.input_audio_transcription.delta","item_id":"a","delta":"How "}"#)
        let partial = try assembler.consume(#"{"type":"conversation.item.input_audio_transcription.delta","item_id":"a","delta":"are you"}"#)
        #expect(partial.first?.text == "How are you")
        let final = try assembler.consume(#"{"type":"conversation.item.input_audio_transcription.completed","item_id":"a","transcript":"How are you?"}"#)
        #expect(final == [CloudTranscriptUpdate(text: "How are you?", isFinal: true)])
    }

    @Test func configurationMessagesUseExpectedFormats() throws {
        let soniox = try #require(CloudSTTProvider.soniox.configurationMessage(model: "", language: "auto", vocabulary: ["Kubernetes"]))
        #expect(soniox.contains("pcm_s16le") && soniox.contains("stt-rt-v5") && soniox.contains("Kubernetes"))
        let openai = try #require(CloudSTTProvider.openai.configurationMessage(model: "gpt-transcribe", language: "auto", vocabulary: []))
        #expect(openai.contains("24000") && openai.contains("\"transcription\""))
        #expect(CloudSTTProvider.deepgram.configurationMessage(model: "", language: "zh", vocabulary: []) == nil)
        let url = try #require(CloudSTTProvider.deepgram.endpoint(model: "", language: "zh", vocabulary: ["gRPC"]))
        #expect(url.absoluteString.contains("language=zh-CN") && url.absoluteString.contains("keyterm=gRPC"))
    }

    @Test func resamplerProducesThreeHalvesSamples() {
        let input: [Int16] = [0, 300, 600, 900]
        let output = PCMResampler.upsample16kTo24k(input)
        #expect(output.count == 6)
        #expect(output.first == 0)
        #expect(output[3] == 600)
        #expect(PCMResampler.littleEndianData([1, -1]) == Data([1, 0, 255, 255]))
    }
}

struct CompanionTests {
    @Test func parsesRequestLineAndQuery() {
        let request = CompanionHTTP.parse("POST /action?t=abc&a=shorter HTTP/1.1\r\nHost: x\r\n\r\n")
        #expect(request == CompanionHTTP.Request(method: "POST", path: "/action", query: ["t": "abc", "a": "shorter"]))
        #expect(CompanionHTTP.parse("garbage") == nil)
        #expect(CompanionHTTP.parse("GET noslash HTTP/1.1\r\n") == nil)
    }

    @Test func tokenComparison() {
        #expect(CompanionHTTP.tokenMatches("abcd", "abcd"))
        #expect(!CompanionHTTP.tokenMatches("abce", "abcd"))
        #expect(!CompanionHTTP.tokenMatches(nil, "abcd"))
        #expect(!CompanionHTTP.tokenMatches("", ""))
    }

    @Test func sseFraming() {
        let event = String(decoding: CompanionHTTP.sseEvent(CompanionSnapshot(status: "s", question: "q", answer: "a\nb", answering: false, candidate: "", position: "")), as: UTF8.self)
        #expect(event.hasPrefix("data: {"))
        #expect(event.hasSuffix("\n\n"))
        #expect(event.dropLast(2).contains("\n") == false)
    }
}
