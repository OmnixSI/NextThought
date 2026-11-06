import CryptoKit
import Foundation

/// One file inside a downloadable model bundle.
///
/// `sha256` is the hash of the file at the pinned revision, so a download that
/// was cut in half, or a mirror that served something else, is caught before the
/// model is used.
struct ManagedModelFile {
    /// Path relative to the model directory, e.g. `UnlimitedOcrDecoder.mlpackage/Manifest.json`.
    let path: String
    let sha256: String
    let size: Int64
    /// Share of the progress bar this file occupies. Small files (configs, tokenizers) get a
    /// small share so the bar does not stall at 99% while a 3 GB weight file is still coming.
    let weight: Double

    init(path: String, sha256: String, size: Int64, weight: Double) {
        self.path = path
        self.sha256 = sha256
        self.size = size
        self.weight = weight
    }
}

/// A model the app can download, verify, use and delete.
///
/// Everything the app knows about a downloadable model lives here: where it
/// comes from, how big it is, which files make it up and under which license it
/// is offered. The files themselves stay in the app data directory, so deleting
/// the model is a matter of removing one folder.
struct ManagedModelDescriptor {
    enum Kind {
        case ocr
        case embedding
        case layout
    }

    /// Directory name under `AppPaths/models`.
    let id: String
    let name: String
    let kind: Kind
    /// Hugging Face repository the files come from.
    let repository: String
    /// Pinned revision. The hashes below belong to exactly this revision.
    let revision: String
    let files: [ManagedModelFile]
    /// One line for the settings list.
    let summary: String
    let license: String
    /// Link to the model card.
    let homepage: String
    /// Longer explanation shown under the download button.
    let note: String
    /// Minimum macOS the runtime needs.
    let minimumOS: String

    var directory: URL {
        let root = AppEnvironment.values["NEXTTHOUGHT_DOCUMENT_MODELS_DIR"].map { URL(fileURLWithPath: $0, isDirectory: true) } ?? AppPaths.models
        return root.appendingPathComponent(id, isDirectory: true)
    }

    var totalSize: Int64 { files.reduce(0) { $0 + $1.size } }

    /// Human readable size, e.g. `3.81 GB`.
    var formattedSize: String {
        ByteCountFormatter.string(fromByteCount: totalSize, countStyle: .file)
    }

    /// Direct download of one file at the pinned revision.
    func downloadURL(for file: ManagedModelFile) -> URL? {
        let escaped = file.path.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? file.path
        return URL(
            string: "https://huggingface.co/\(repository)/resolve/\(revision)/\(escaped)?download=true")
    }
}

/// Downloads, verifies and removes model bundles.
///
/// The flow is the same for every model: a file that is already present and
/// matches its hash is kept, everything else is downloaded to a `.partial`
/// file, hashed, and only then moved into place. A cancelled or failed
/// download leaves no half-written file behind.
enum ManagedModelStore {
    /// True when every file of the model is present and matches its hash.
    static func isInstalled(_ descriptor: ManagedModelDescriptor) -> Bool {
        let marker = descriptor.directory.appendingPathComponent("verified.json")
        guard let data = try? Data(contentsOf: marker),
              let value = try? JSONSerialization.jsonObject(with: data) as? [String: String],
              value["revision"] == descriptor.revision else { return false }
        return descriptor.files.allSatisfy { file in
            let url = descriptor.directory.appendingPathComponent(file.path)
            return (try? url.resourceValues(forKeys: [.fileSizeKey]).fileSize) == Int(file.size)
        }
    }

    /// Which files are missing or damaged. Empty means the model is complete.
    static func missingFiles(_ descriptor: ManagedModelDescriptor) -> [String] {
        var missing: [String] = []
        for file in descriptor.files {
            let url = descriptor.directory.appendingPathComponent(file.path)
            if sha256(of: url) != file.sha256 { missing.append(file.path) }
        }
        return missing
    }

    /// Checks every file of the model against its pinned hash.
    static func verify(_ descriptor: ManagedModelDescriptor, at directory: URL) throws {
        for file in descriptor.files {
            let url = directory.appendingPathComponent(file.path)
            guard let hash = sha256(of: url) else { throw ManagedModelError.missingFile(file.path) }
            guard hash == file.sha256 else { throw ManagedModelError.checksumMismatch(file.path) }
        }
    }

    /// Downloads the model if it is not already complete.
    ///
    /// - Parameter progress: called with the overall fraction (0...1) and a short
    ///   Chinese description of the current step.
    static func download(
        _ descriptor: ManagedModelDescriptor,
        searchLocal: Bool = true,
        progress: @escaping @Sendable (Double, String) -> Void
    ) async throws {
        if searchLocal { _ = try await LocalDocumentAssets.reuse(models: [descriptor], runtime: false, verifyInstalled: true) }
        try Task.checkCancellation()
        let directory = descriptor.directory
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let downloader = ModelAssetDownloader()
        defer { downloader.invalidate() }
        let totalWeight = max(0.0001, descriptor.files.reduce(0) { $0 + $1.weight })
        var completedWeight = 0.0

        for file in descriptor.files {
            try Task.checkCancellation()
            let destination = directory.appendingPathComponent(file.path)
            if sha256(of: destination) == file.sha256 {
                completedWeight += file.weight
                progress(completedWeight / totalWeight, "已校验 \(file.path)")
                continue
            }
            try? FileManager.default.removeItem(at: destination)
            try FileManager.default.createDirectory(
                at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
            let partial = destination.appendingPathExtension("partial")
            try? FileManager.default.removeItem(at: partial)
            guard let url = descriptor.downloadURL(for: file) else {
                throw ManagedModelError.downloadFailed(file.path)
            }
            let base = completedWeight
            do {
                try await downloader.download(from: url, to: partial) { fraction in
                    progress(
                        min(0.99, (base + file.weight * fraction) / totalWeight), "正在下载 \(file.path)")
                }
                try Task.checkCancellation()
                guard sha256(of: partial) == file.sha256 else {
                    throw ManagedModelError.checksumMismatch(file.path)
                }
                try FileManager.default.moveItem(at: partial, to: destination)
            } catch {
                try? FileManager.default.removeItem(at: partial)
                if Task.isCancelled { throw CancellationError() }
                if error is ManagedModelError { throw error }
                throw ManagedModelError.downloadFailed(file.path)
            }
            completedWeight += file.weight
            progress(completedWeight / totalWeight, "已校验 \(file.path)")
        }
        try verify(descriptor, at: directory)
        try Task.checkCancellation()
        let marker = try JSONSerialization.data(withJSONObject: ["revision": descriptor.revision])
        try marker.write(to: directory.appendingPathComponent("verified.json"), options: .atomic)
    }

    /// Removes the model from the data directory.
    static func remove(_ descriptor: ManagedModelDescriptor) throws {
        let directory = descriptor.directory
        if FileManager.default.fileExists(atPath: directory.path) {
            try FileManager.default.removeItem(at: directory)
        }
    }

    /// Size of the installed model on disk, 0 when it is not installed.
    static func installedSize(_ descriptor: ManagedModelDescriptor) -> Int64 {
        let directory = descriptor.directory
        guard let enumerator = FileManager.default.enumerator(
            at: directory, includingPropertiesForKeys: [.fileSizeKey], options: [.skipsHiddenFiles])
        else { return 0 }
        var total: Int64 = 0
        for case let url as URL in enumerator {
            let size = (try? url.resourceValues(forKeys: [.fileSizeKey]).fileSize) ?? 0
            total += Int64(size)
        }
        return total
    }

    /// SHA-256 of a file, `nil` when it cannot be read.
    static func sha256(of url: URL) -> String? {
        guard let handle = try? FileHandle(forReadingFrom: url) else { return nil }
        defer { try? handle.close() }
        var hash = SHA256()
        do {
            while let data = try handle.read(upToCount: 4 * 1024 * 1024), !data.isEmpty { hash.update(data: data) }
        } catch { return nil }
        return hash.finalize().map { String(format: "%02x", $0) }.joined()
    }
}

enum ManagedModelError: LocalizedError {
    case missingFile(String)
    case checksumMismatch(String)
    case downloadFailed(String)

    var errorDescription: String? {
        switch self {
        case .missingFile(let name): "模型文件缺少 \(name)，请重新下载。"
        case .checksumMismatch(let name): "\(name) 的 SHA-256 校验失败；已删除未完成文件，请重试。"
        case .downloadFailed(let name): "下载 \(name) 失败。请检查网络后重试。"
        }
    }
}

/// Downloads one file and reports its progress.
///
/// A `URLSession` download writes to a temporary file that only exists while
/// the transfer runs, so the file is moved to the destination as soon as the
/// transfer finished. HTTP errors and cancelled transfers are reported as
/// errors instead of as a successful empty file.
final class ModelAssetDownloader: NSObject, URLSessionDownloadDelegate, @unchecked Sendable {
    private let lock = NSLock()
    private var continuation: CheckedContinuation<Void, Error>?
    private var destination: URL?
    private var currentTask: URLSessionDownloadTask?
    private var cancelled = false
    private var receivedFile: URL?
    private var responseError: Error?
    private var progressHandler: (@Sendable (Double) -> Void)?
    private lazy var session = URLSession(configuration: .ephemeral, delegate: self, delegateQueue: OperationQueue())

    func download(from url: URL, to destination: URL, progress: @escaping @Sendable (Double) -> Void) async throws {
        try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
                lock.lock()
                guard !cancelled && !Task.isCancelled else { lock.unlock(); continuation.resume(throwing: CancellationError()); return }
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
        cancelled = true
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
        else { continuation.resume(throwing: ManagedModelError.downloadFailed("模型文件")) }
    }
}
