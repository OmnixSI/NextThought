import CryptoKit
import Foundation
import InterviewHackerCore

/// Which local streaming recognizer to use.
enum LocalStreamingModel: String, CaseIterable, Identifiable {
    /// X-ASR zh-en, 480 ms chunks: best accuracy, punctuation, code-switching (default).
    case xasr480
    /// X-ASR zh-en, 160 ms chunks: partial text appears sooner, slightly less accurate.
    case xasr160
    /// The 2023 bilingual Zipformer (no punctuation); kept for existing installs.
    case legacy

    var id: String { rawValue }
    var title: String {
        switch self {
        case .xasr480: "X-ASR 中英 · 480 ms（推荐）"
        case .xasr160: "X-ASR 中英 · 160 ms（出字更快）"
        case .legacy: "Zipformer 双语 2023（旧版）"
        }
    }
    var shortTitle: String {
        switch self {
        case .xasr480, .xasr160: "X-ASR 中英模型"
        case .legacy: "Zipformer 双语模型"
        }
    }
    var detail: String {
        switch self {
        case .xasr480: "2026 年 6 月发布，约 100 万小时训练，自带标点与大小写，中英混说准确率明显高于旧版。"
        case .xasr160: "同一模型的 160 ms 版本：边说边出字更快，准确率略低（WenetSpeech 约 +1.3% CER）。"
        case .legacy: "2023 年的中英双语流式模型，无标点；仅为兼容已下载的用户保留。"
        }
    }
    var package: LocalASRPackage? {
        switch self {
        case .xasr480: .xasrStreaming480
        case .xasr160: .xasrStreaming160
        case .legacy: nil
        }
    }

    var onlineModel: OnlineTransducerModel {
        switch self {
        case .xasr480: .xasrStreaming(at: LocalASRPackage.xasrStreaming480.directory)
        case .xasr160: .xasrStreaming(at: LocalASRPackage.xasrStreaming160.directory)
        case .legacy: .legacyBilingual(at: ZipformerModelStore.modelDirectory)
        }
    }
}

/// Model archives published by sherpa-onnx (github.com/k2-fsa/sherpa-onnx/releases/tag/asr-models).
/// Hashes were computed from the downloaded archives.
enum LocalASRPackage: String, CaseIterable, Identifiable {
    case xasrStreaming480, xasrStreaming160, xasrOffline

    var id: String { rawValue }

    var archiveName: String {
        switch self {
        case .xasrStreaming480: "sherpa-onnx-x-asr-480ms-streaming-zipformer-transducer-zh-en-punct-int8-2026-06-05"
        case .xasrStreaming160: "sherpa-onnx-x-asr-160ms-streaming-zipformer-transducer-zh-en-punct-int8-2026-06-05"
        case .xasrOffline: "sherpa-onnx-x-asr-zipformer-transducer-zh-en-punct-int8-2026-06-03"
        }
    }

    var archiveSHA256: String {
        switch self {
        case .xasrStreaming480: "fa5f63d618e5a01526e275a358bb7772e403f84808a4769fba52cffd8160bf74"
        case .xasrStreaming160: "8a6fca056e1a342546edd78be4d50274e2c01898e7b8ae8fc336f6410319c399"
        case .xasrOffline: "5d02c36d7b44e886b7c8f0d8e051f8713acab96c264bb6ef9e718be39a6a2224"
        }
    }

    var approximateMegabytes: Int { self == .xasrOffline ? 136 : 134 }

    var title: String {
        switch self {
        case .xasrStreaming480: "X-ASR 流式 480 ms"
        case .xasrStreaming160: "X-ASR 流式 160 ms"
        case .xasrOffline: "X-ASR 整句精修"
        }
    }

    /// Files kept from the archive (sizes guard against truncated copies).
    var files: [(name: String, size: Int)] {
        let streaming: [(name: String, size: Int)] = [(name: "encoder.int8.onnx", size: 155_278_641), (name: "decoder.onnx", size: 11_309_084),
                                                       (name: "joiner.int8.onnx", size: 2_581_422), (name: "tokens.txt", size: 58_806)]
        let offline: [(name: String, size: Int)] = [(name: "encoder-epoch-99-avg-1.int8.onnx", size: 161_015_713), (name: "decoder-epoch-99-avg-1.onnx", size: 11_309_084),
                                                     (name: "joiner-epoch-99-avg-1.int8.onnx", size: 2_581_422), (name: "tokens.txt", size: 58_806)]
        return self == .xasrOffline ? offline : streaming
    }

    var downloadURL: URL {
        URL(string: "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/\(archiveName).tar.bz2")!
    }

    var directory: URL {
        AppPaths.models.appendingPathComponent(archiveName, isDirectory: true)
    }

    private var manifestURL: URL { directory.appendingPathComponent("installed.json") }

    var isInstalled: Bool {
        guard FileManager.default.fileExists(atPath: manifestURL.path) else { return false }
        for file in files {
            let path = directory.appendingPathComponent(file.name).path
            guard let attributes = try? FileManager.default.attributesOfItem(atPath: path),
                  (attributes[.size] as? NSNumber)?.intValue == file.size else { return false }
        }
        return true
    }

    /// Downloads, verifies (SHA-256 of the archive), extracts and installs the package.
    func install(progress: @escaping @Sendable (Double, String) -> Void) async throws {
        if isInstalled { progress(1, "\(title) 已就绪"); return }
        let fileManager = FileManager.default
        let staging = fileManager.temporaryDirectory.appendingPathComponent("ih-model-\(UUID().uuidString)", isDirectory: true)
        try fileManager.createDirectory(at: staging, withIntermediateDirectories: true)
        defer { try? fileManager.removeItem(at: staging) }
        let archive = staging.appendingPathComponent("model.tar.bz2")

        let downloader = ZipformerAssetDownloader()
        defer { downloader.invalidate() }
        progress(0, "正在下载 \(title)（约 \(approximateMegabytes) MB）")
        do {
            try await downloader.download(from: downloadURL, to: archive) { fraction in
                progress(min(0.9, fraction * 0.9), "正在下载 \(title)")
            }
        } catch {
            if Task.isCancelled { throw CancellationError() }
            throw LocalASRError.downloadFailed(title, error.localizedDescription)
        }
        try Task.checkCancellation()

        progress(0.91, "正在校验 \(title)")
        let hash = try await Task.detached(priority: .userInitiated) { try Self.sha256(of: archive) }.value
        guard hash == archiveSHA256 else { throw LocalASRError.checksumMismatch(title) }

        progress(0.94, "正在解压 \(title)")
        try await Task.detached(priority: .userInitiated) {
            try Self.run("/usr/bin/tar", ["-xjf", archive.path, "-C", staging.path])
        }.value
        let extracted = staging.appendingPathComponent(archiveName, isDirectory: true)

        let target = directory
        if fileManager.fileExists(atPath: target.path) { try fileManager.removeItem(at: target) }
        try fileManager.createDirectory(at: target, withIntermediateDirectories: true)
        for file in files {
            let source = extracted.appendingPathComponent(file.name)
            guard fileManager.fileExists(atPath: source.path) else { throw LocalASRError.missingFile(file.name) }
            try fileManager.moveItem(at: source, to: target.appendingPathComponent(file.name))
        }
        // Hotwords need the SentencePiece vocabulary that the archive does not include.
        try XASRVocabulary.text.write(to: target.appendingPathComponent("bpe.vocab"), atomically: true, encoding: .utf8)
        let manifest: [String: Any] = ["archive": archiveName, "sha256": archiveSHA256, "installedAt": ISO8601DateFormatter().string(from: Date())]
        try JSONSerialization.data(withJSONObject: manifest).write(to: manifestURL, options: .atomic)
        guard isInstalled else { throw LocalASRError.missingFile(files.map(\.name).joined(separator: ", ")) }
        progress(1, "\(title) 已安装并校验")
    }

    func remove() throws {
        if FileManager.default.fileExists(atPath: directory.path) { try FileManager.default.removeItem(at: directory) }
    }

    private static func sha256(of url: URL) throws -> String {
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        var hasher = SHA256()
        while let chunk = try handle.read(upToCount: 4 << 20), !chunk.isEmpty { hasher.update(data: chunk) }
        return hasher.finalize().map { String(format: "%02x", $0) }.joined()
    }

    private static func run(_ executable: String, _ arguments: [String]) throws {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = arguments
        let errors = Pipe()
        process.standardError = errors
        process.standardOutput = FileHandle.nullDevice
        try process.run()
        process.waitUntilExit()
        guard process.terminationStatus == 0 else {
            let message = String(decoding: errors.fileHandleForReading.readDataToEndOfFile(), as: UTF8.self)
            throw LocalASRError.extractFailed(message.trimmingCharacters(in: .whitespacesAndNewlines))
        }
    }
}

enum LocalASRError: LocalizedError {
    case downloadFailed(String, String)
    case checksumMismatch(String)
    case extractFailed(String)
    case missingFile(String)

    var errorDescription: String? {
        switch self {
        case let .downloadFailed(name, detail): "下载 \(name) 失败：\(detail)。请检查网络（需要能访问 github.com）后重试。"
        case let .checksumMismatch(name): "\(name) 的 SHA-256 校验失败，已丢弃下载文件，请重试。"
        case let .extractFailed(detail): "解压模型失败：\(detail)"
        case let .missingFile(name): "模型文件不完整：\(name)"
        }
    }
}
