import Foundation
import Testing
@testable import InterviewHackerCore

/// The byte-level BPE tokenizer is checked against a fixture that was produced
/// with the reference implementation (Hugging Face `tokenizers`), so the app
/// splits and merges exactly like the model expects.
struct ByteLevelBPETokenizerTests {
    private func fixtureURL(_ name: String) -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .appendingPathComponent("Fixtures")
            .appendingPathComponent(name)
    }

    private struct Case: Decodable {
        let text: String
        let ids: [Int]
    }

    private func loadCases() throws -> [Case] {
        let data = try Data(contentsOf: fixtureURL("byte-bpe-fixture-expected.json"))
        return try JSONDecoder().decode([Case].self, from: data)
    }

    @Test func encodesLikeTheReferenceImplementation() throws {
        let tokenizer = try ByteLevelBPETokenizer(
            jsonURL: fixtureURL("byte-bpe-fixture.json"),
            configuration: .init(endOfTextToken: -1, appendEndOfText: false))
        for testCase in try loadCases() {
            #expect(
                tokenizer.encode(testCase.text) == testCase.ids,
                "wrong tokens for \(testCase.text)")
        }
    }

    @Test func decodesBackToTheOriginalText() throws {
        let tokenizer = try ByteLevelBPETokenizer(
            jsonURL: fixtureURL("byte-bpe-fixture.json"),
            configuration: .init(endOfTextToken: -1, appendEndOfText: false))
        for testCase in try loadCases() {
            #expect(tokenizer.decode(testCase.ids) == testCase.text)
        }
    }

    /// The end-of-text token is the token the embedding model pools its vector
    /// from, so it has to be appended to every text.
    @Test func appendsTheEndOfTextToken() throws {
        let tokenizer = try ByteLevelBPETokenizer(
            jsonURL: fixtureURL("byte-bpe-fixture.json"),
            configuration: .init(endOfTextToken: 42, appendEndOfText: true))
        #expect(tokenizer.encode("hello").last == 42)
    }

    /// The tokenizer of the embedding model that ships with the app, checked
    /// against the ids the reference implementation produced. The check only
    /// runs when the model is installed.
    @Test func installedEmbeddingTokenizerMatchesTheReference() throws {
        let tokenizerURL = URL(fileURLWithPath: NSHomeDirectory())
            .appendingPathComponent("Library/Application Support/InterviewHacker/models")
            .appendingPathComponent("qwen3-embedding-0-6b-coreml/tokenizer/tokenizer.json")
        guard FileManager.default.fileExists(atPath: tokenizerURL.path) else { return }
        let tokenizer = try ByteLevelBPETokenizer(jsonURL: tokenizerURL)
        let english = tokenizer.encode("Tell me about the CAP theorem")
        #expect(english.last == 151_643)
        #expect(Array(english.prefix(6)) == [40451, 752, 911, 279, 26101, 57817])
        let chinese = tokenizer.encode("请介绍 Kubernetes 的调度器")
        #expect(Array(chinese.prefix(3)) == [14880, 100157, 66374])
    }
}
