import Foundation
import Testing
@testable import InterviewHackerCore

struct WhisperModelTests {
    @Test func catalogCoversLanguagesSizesAndQuantization() {
        #expect(Set(WhisperModel.catalog.map(\.id)).count == WhisperModel.catalog.count)
        for family in ["tiny", "base", "small", "medium", "large-v3", "large-v3-turbo"] {
            #expect(WhisperModel.named(family) != nil)
        }
        #expect(WhisperModel.named(WhisperModel.defaultID)?.bytes == 574_041_195)
        #expect(WhisperModel.named("base.en")?.englishOnly == true)
        #expect(WhisperModel.named("base")?.englishOnly == false)
        for model in WhisperModel.catalog {
            #expect(model.bytes > 0)
            #expect(model.sha256.count == 64)
            #expect(WhisperModel.matching(URL(fileURLWithPath: model.filename)) == model)
            #expect(!model.summary.isEmpty)
        }
    }

    @Test func rejectsTruncationWrongModelAndTampering() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let url = directory.appendingPathComponent("custom.bin")
        var data = Data([0x6c, 0x6d, 0x67, 0x67])
        data.append(Data(repeating: 0, count: 1_000_000))
        try data.write(to: url)
        let checksum = try WhisperModelFile.sha256(at: url)
        try checksum.write(to: url.appendingPathExtension("sha256"), atomically: true, encoding: .utf8)
        try WhisperModelFile.verify(at: url)
        #expect(throws: Error.self) { try WhisperModelFile.verify(at: url, expected: WhisperModel.named("base")) }
        data[data.count - 1] = 1
        try data.write(to: url)
        #expect(throws: Error.self) { try WhisperModelFile.verify(at: url) }
        try Data([0x6c, 0x6d, 0x67, 0x67]).write(to: url)
        #expect(throws: Error.self) { try WhisperModelFile.verify(at: url) }
        try Data(repeating: 1, count: 1_000_004).write(to: url)
        #expect(throws: Error.self) { try WhisperModelFile.verify(at: url) }
    }
}
