import CryptoKit
import Foundation
import NextThoughtCore

enum ZipformerModelStore {
    static let modelID = "sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20"
    static let revision = "98590b7ed6443e77b714204da2757d75e1a642f4"
    static let modelDirectory: URL = AppPaths.models.appendingPathComponent(modelID, isDirectory: true)

    private struct Asset {
        let name: String
        let sha256: String
        let weight: Double
    }

    // Hashes are SHA-256 values for the files from the immutable model repository revision.
    private static let assets = [
        Asset(name: ZipformerModelFiles.encoder, sha256: "8fa764187a261844f859d7143ebaa563af5d10adfece4c18a8f414c88cba2a9b", weight: 182),
        Asset(name: ZipformerModelFiles.decoder, sha256: "2e3b5ec371f8899ee6acd829fd753ba45772df57a91bdf37cde3136354e7db7d", weight: 14),
        Asset(name: ZipformerModelFiles.joiner, sha256: "1ed689c5ed19dbaa725d9d191bb4822b5f4855a39e1ffd28cbc1f340d25b2ee0", weight: 3.3),
        Asset(name: ZipformerModelFiles.tokens, sha256: "a8e0e4ec53810e433789b54a5c0134a7eaa2ffca595a6334d54c00da858841d3", weight: 0.06),
        Asset(name: ZipformerModelFiles.bpeVocabulary, sha256: "d0b642f3a2eacd5fadefdeff9e0e1358cab729647cbb7fe58cf738e1f7407029", weight: 0.02)
    ]

    static func isInstalled() -> Bool {
        (try? verify(at: modelDirectory)) != nil
    }

    static func verify(at directory: URL) throws {
        for asset in assets {
            let file = directory.appendingPathComponent(asset.name)
            guard let data = try? Data(contentsOf: file, options: .mappedIfSafe) else {
                throw ZipformerModelError.missingFile(asset.name)
            }
            let hash = SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
            guard !asset.sha256.isEmpty, hash == asset.sha256 else {
                throw ZipformerModelError.checksumMismatch(asset.name)
            }
        }
    }

    static func download(progress: @escaping @Sendable (Double, String) -> Void) async throws {
        try FileManager.default.createDirectory(at: modelDirectory, withIntermediateDirectories: true)
        let downloader = ZipformerAssetDownloader()
        defer { downloader.invalidate() }
        let totalWeight = assets.reduce(0) { $0 + $1.weight }
        var completedWeight = 0.0

        for asset in assets {
            try Task.checkCancellation()
            let destination = modelDirectory.appendingPathComponent(asset.name)
            if (try? verify(asset, at: destination)) == true {
                completedWeight += asset.weight
                progress(completedWeight / totalWeight, "已校验 \(asset.name)")
                continue
            }
            try? FileManager.default.removeItem(at: destination)
            let partial = destination.appendingPathExtension("partial")
            try? FileManager.default.removeItem(at: partial)
            let url = URL(string: "https://huggingface.co/csukuangfj/\(modelID)/resolve/\(revision)/\(asset.name)?download=true")!
            let base = completedWeight
            do {
                try await downloader.download(from: url, to: partial) { fraction in
                    progress(min(0.99, (base + asset.weight * fraction) / totalWeight), "正在下载 \(asset.name)")
                }
                try Task.checkCancellation()
                guard try verify(asset, at: partial) else { throw ZipformerModelError.checksumMismatch(asset.name) }
                try FileManager.default.moveItem(at: partial, to: destination)
            } catch {
                try? FileManager.default.removeItem(at: partial)
                if Task.isCancelled { throw CancellationError() }
                if !(error is ZipformerModelError) {
                    throw ZipformerModelError.downloadFailed(asset.name)
                }
                throw error
            }
            completedWeight += asset.weight
            progress(completedWeight / totalWeight, "已校验 \(asset.name)")
        }
        try verify(at: modelDirectory)
    }

    private static func verify(_ asset: Asset, at url: URL) throws -> Bool {
        guard let data = try? Data(contentsOf: url, options: .mappedIfSafe) else { return false }
        let hash = SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
        return !asset.sha256.isEmpty && hash == asset.sha256
    }
}

enum ZipformerModelError: LocalizedError {
    case missingFile(String)
    case checksumMismatch(String)
    case downloadFailed(String)

    var errorDescription: String? {
        switch self {
        case .missingFile(let name): "模型文件组缺少 \(name)，请重新下载。"
        case .checksumMismatch(let name): "\(name) 的 SHA-256 校验失败；已删除未完成文件，请重试。"
        case .downloadFailed(let name): "下载 \(name) 失败。请检查网络后重试。"
        }
    }
}

final class ZipformerAssetDownloader: NSObject, URLSessionDownloadDelegate, @unchecked Sendable {
    private let lock = NSLock()
    private var continuation: CheckedContinuation<Void, Error>?
    private var destination: URL?
    private var currentTask: URLSessionDownloadTask?
    private var receivedFile: URL?
    private var responseError: Error?
    private var progressHandler: (@Sendable (Double) -> Void)?
    private lazy var session = URLSession(configuration: .ephemeral, delegate: self, delegateQueue: OperationQueue())

    func download(from url: URL, to destination: URL, progress: @escaping @Sendable (Double) -> Void) async throws {
        try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { continuation in
                lock.lock()
                self.continuation = continuation
                self.destination = destination
                self.progressHandler = progress
                receivedFile = nil
                responseError = nil
                let task = session.downloadTask(with: url)
                currentTask = task
                lock.unlock()
                task.resume()
            }
        } onCancel: {
            self.cancel()
        }
    }

    func cancel() {
        lock.lock()
        let task = currentTask
        lock.unlock()
        task?.cancel()
    }

    func invalidate() {
        session.invalidateAndCancel()
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask,
                    didWriteData bytesWritten: Int64, totalBytesWritten: Int64,
                    totalBytesExpectedToWrite: Int64) {
        let callback = lock.withLock { progressHandler }
        let fraction = totalBytesExpectedToWrite > 0
            ? Double(totalBytesWritten) / Double(totalBytesExpectedToWrite)
            : 0
        callback?(min(1, max(0, fraction)))
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask,
                    didFinishDownloadingTo location: URL) {
        lock.lock()
        let target = destination
        lock.unlock()
        guard let target else { return }
        do {
            try FileManager.default.moveItem(at: location, to: target)
            lock.withLock { receivedFile = target }
        } catch {
            lock.withLock { receivedFile = nil }
        }
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        lock.lock()
        let continuation = self.continuation
        let receivedFile = self.receivedFile
        var completionError = error ?? responseError
        if completionError == nil, let response = task.response as? HTTPURLResponse,
           !(200..<300).contains(response.statusCode) {
            completionError = URLError(.badServerResponse)
        }
        self.continuation = nil
        currentTask = nil
        destination = nil
        progressHandler = nil
        self.receivedFile = nil
        responseError = nil
        lock.unlock()
        guard let continuation else { return }
        if let completionError { continuation.resume(throwing: completionError) }
        else if receivedFile != nil { continuation.resume() }
        else { continuation.resume(throwing: ZipformerModelError.downloadFailed("模型文件")) }
    }
}
