import CryptoKit
import Foundation
import Testing
@testable import NextThoughtCore

struct LocalModelReuseTests {
    private func scratch() throws -> URL {
        let url = FileManager.default.temporaryDirectory.appendingPathComponent("ih-local-assets-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true); return url
    }
    private func spec(_ name: String, _ text: String) -> LocalModelReuse.File {
        .init(path: name, bytes: Int64(text.utf8.count), sha256: SHA256.hash(data: Data(text.utf8)).map { String(format: "%02x", $0) }.joined())
    }
    @Test func hiddenCacheSymlinksAndPartialReuse() throws {
        let root = try scratch(); defer { try? FileManager.default.removeItem(at: root) }
        let cache = root.appendingPathComponent(".cache/huggingface/hub/models--test/snapshots/pinned")
        try FileManager.default.createDirectory(at: cache, withIntermediateDirectories: true)
        let blob = root.appendingPathComponent("blob"); try Data("correct".utf8).write(to: blob)
        try FileManager.default.createSymbolicLink(at: cache.appendingPathComponent("model.safetensors"), withDestinationURL: blob)
        try Data("wrong!!".utf8).write(to: cache.appendingPathComponent("config.json"))
        let search = try LocalModelReuse.search(roots: [root], names: ["model.safetensors"])
        #expect(search.directories.contains { $0.path == cache.path }); #expect(!search.limited)
        let target = root.appendingPathComponent("installed")
        let files = [spec("model.safetensors", "correct"), spec("config.json", "configs")]
        let reused = try LocalModelReuse.reuse(files, from: search.directories, into: target)
        #expect(reused.reusedFiles == 1); #expect(reused.reusedBytes == 7)
        #expect(try Data(contentsOf: target.appendingPathComponent("model.safetensors")) == Data("correct".utf8))
        #expect(!FileManager.default.fileExists(atPath: target.appendingPathComponent("config.json").path))
        #expect(try Data(contentsOf: blob) == Data("correct".utf8))
        #expect(try LocalModelReuse.reuse(files, from: search.directories, into: target).reusedFiles == 0)
    }
    @Test func corruptedDestinationIsReplacedOnlyWithValidData() throws {
        let root = try scratch(); defer { try? FileManager.default.removeItem(at: root) }
        let candidate = root.appendingPathComponent("candidate"), target = root.appendingPathComponent("target")
        for d in [candidate, target] { try FileManager.default.createDirectory(at: d, withIntermediateDirectories: true) }
        try Data("broken!".utf8).write(to: target.appendingPathComponent("model.safetensors"))
        try Data("correct".utf8).write(to: candidate.appendingPathComponent("model.safetensors"))
        let file = spec("model.safetensors", "correct")
        #expect(try LocalModelReuse.reuse([file], from: [candidate], into: target).reusedFiles == 1)
        #expect(try LocalModelReuse.matches(target.appendingPathComponent(file.path), file: file))
        #expect(try FileManager.default.contentsOfDirectory(atPath: target.path) == [file.path])
    }
    @Test func partialNamedRevisionIsDiscoveredWithoutWeightFile() throws {
        let root = try scratch(); defer { try? FileManager.default.removeItem(at: root) }
        let revision = root.appendingPathComponent(".cache/hub/snapshots/pinned-revision")
        try FileManager.default.createDirectory(at: revision, withIntermediateDirectories: true)
        try Data("configs".utf8).write(to: revision.appendingPathComponent("config.json"))
        let search = try LocalModelReuse.search(roots: [root], names: ["model.safetensors", "pinned-revision"])
        let result = try LocalModelReuse.reuse([spec("config.json", "configs")], from: search.directories, into: root.appendingPathComponent("output"))
        #expect(result.reusedFiles == 1)
    }
    @Test func indexedExternalFilesAndBoundedTraversal() throws {
        let root = try scratch(); defer { try? FileManager.default.removeItem(at: root) }
        for n in 0..<4 { try Data().write(to: root.appendingPathComponent("file-\(n)")) }
        let external = root.appendingPathComponent("external/model.safetensors")
        let result = try LocalModelReuse.search(roots: [root], indexedFiles: [external], names: ["model.safetensors"], maximumEntries: 1)
        #expect(result.limited); #expect(result.directories.contains { $0.path == external.deletingLastPathComponent().path })
    }
    @Test func refusesTraversalAndCancellationLeavesNoCommittedFiles() async throws {
        let root = try scratch(); defer { try? FileManager.default.removeItem(at: root) }
        #expect(throws: CocoaError.self) { try LocalModelReuse.reuse([spec("../escape", "x")], from: [root], into: root) }
        let task = Task { () throws -> LocalModelReuse.Search in
            withUnsafeCurrentTask { $0?.cancel() }
            return try LocalModelReuse.search(roots: [root], names: ["model.safetensors"])
        }
        task.cancel()
        do { _ = try await task.value; Issue.record("cancel ignored") } catch is CancellationError {}
        #expect(try FileManager.default.contentsOfDirectory(atPath: root.path).isEmpty)
    }
}
