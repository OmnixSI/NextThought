import InterviewHackerCore
import Foundation
import Darwin

final class LocalWhisperService: @unchecked Sendable {
    static let runtimeName = "whisper-server"

    private struct Worker {
        var process: Process
        var port: UInt16
        var modelPath: String
        var logHandle: FileHandle?
    }

    private let lock = NSLock()
    private var worker: Worker?
    private var session = URLSession(configuration: .ephemeral)

    static var modelsDirectory: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        return base.appendingPathComponent("InterviewHacker/models", isDirectory: true)
    }

    static var defaultModelURL: URL { modelsDirectory.appendingPathComponent(WhisperModel.named(WhisperModel.defaultID)!.filename) }

    static func findExecutable() -> String? {
        let paths: [String?] = [
            Bundle.main.bundleURL.appendingPathComponent("Contents/Helpers/\(runtimeName)").path,
            Bundle.main.resourceURL?.appendingPathComponent(runtimeName).path,
            URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
                .appendingPathComponent(".build/whisper-runtime/bin/\(runtimeName)").path
        ]
        let candidates = paths.compactMap { $0 }
        return candidates.first(where: { FileManager.default.isExecutableFile(atPath: $0) })
    }

    static func isValidModel(at path: String) -> Bool {
        do {
            try verifyModel(at: URL(fileURLWithPath: path))
            return true
        } catch {
            return false
        }
    }

    static func downloadModel(_ model: WhisperModel, to destination: URL, progress: @escaping @Sendable (Double) -> Void = { _ in }) async throws {
        let operation = ModelDownloadOperation(model: model, destination: destination, progress: progress)
        try await withTaskCancellationHandler {
            try await operation.start()
        } onCancel: {
            operation.cancel()
        }
    }

    func prepare(modelPath: String) async throws -> TimeInterval {
        try Task.checkCancellation()
        guard let executable = Self.findExecutable() else {
            throw AppFailure.noTranscriber
        }
        if let existing = currentWorker(), existing.process.isRunning, existing.modelPath == modelPath {
            return 0
        }
        try await Task.detached(priority: .utility) {
            try Self.verifyModel(at: URL(fileURLWithPath: modelPath))
        }.value
        await stopWorker()

        let port = try Self.availablePort()
        let process = Process()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = [
            "--model", modelPath,
            "--host", "127.0.0.1",
            "--port", String(port),
            "--threads", "6",
            "--language", "auto",
        ]
        process.standardOutput = FileHandle.nullDevice
        // Whisper's raw stderr may include decoded text. Do not retain it in a
        // background log when history saving is disabled (or otherwise).
        let logHandle: FileHandle? = nil
        process.standardError = FileHandle.nullDevice
        do {
            try process.run()
        } catch {
            try? logHandle?.close()
            throw AppFailure.transcription("无法启动应用内的 Whisper 识别组件：\(error.localizedDescription)")
        }

        let nextWorker = Worker(process: process, port: port, modelPath: modelPath, logHandle: logHandle)
        setWorker(nextWorker)
        Diagnostics.record("whisper.started", ["pid": process.processIdentifier, "runtime": "whisper.cpp v1.9.4"])

        let startedAt = Date()
        let endpoint = URL(string: "http://127.0.0.1:\(port)/")!
        while Date().timeIntervalSince(startedAt) < 90 {
            if Task.isCancelled { await stopWorker(); throw CancellationError() }
            guard process.isRunning else {
                await stopWorker()
                throw AppFailure.transcription("Whisper 识别组件启动失败。请检查模型是否完整，然后重试。")
            }
            var request = URLRequest(url: endpoint, timeoutInterval: 2)
            request.httpMethod = "GET"
            if let (_, response) = try? await currentSession().data(for: request),
               (response as? HTTPURLResponse)?.statusCode == 200 {
                Diagnostics.record("whisper.ready", ["pid": process.processIdentifier, "seconds": Date().timeIntervalSince(startedAt)])
                return Date().timeIntervalSince(startedAt)
            }
            try await Task.sleep(for: .milliseconds(250))
        }
        await stopWorker()
        throw AppFailure.transcription("Whisper 模型加载超过 90 秒。请关闭其他占用大量内存的应用后重试。")
    }

    func transcribe(_ samples: [Int16], modelPath: String, language: String, vocabulary: String = "") async throws -> String {
        guard !samples.isEmpty else { return "" }
        if currentWorker()?.modelPath != modelPath {
            _ = try await prepare(modelPath: modelPath)
        }
        guard let worker = currentWorker(), worker.process.isRunning else {
            throw AppFailure.transcription("本地识别组件未就绪。请停止并重新开始辅助。")
        }

        var request = URLRequest(url: URL(string: "http://127.0.0.1:\(worker.port)/inference")!, timeoutInterval: 180)
        request.httpMethod = "POST"
        let boundary = "InterviewHacker-\(UUID().uuidString)"
        request.setValue("multipart/form-data; boundary=\(boundary)", forHTTPHeaderField: "Content-Type")
        request.httpBody = Self.makeMultipart(samples: samples, boundary: boundary, language: language, vocabulary: vocabulary)
        let startedAt = Date()
        Diagnostics.record("asr.started", ["samples": samples.count, "audio_seconds": Double(samples.count) / 16_000, "language": language, "pid": worker.process.processIdentifier])

        do {
            let (data, response) = try await currentSession().data(for: request)
            try Task.checkCancellation()
            guard let http = response as? HTTPURLResponse, (200..<300).contains(http.statusCode) else {
                let body = String(data: data, encoding: .utf8)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
                throw AppFailure.transcription(body.isEmpty ? "Whisper 识别失败，请稍后重试。" : body)
            }
            guard let result = try JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let text = result["text"] as? String else {
                throw AppFailure.transcription("本地识别组件返回了无法读取的结果。")
            }
            Diagnostics.record("asr.completed", ["seconds": Date().timeIntervalSince(startedAt), "characters": text.count, "pid": worker.process.processIdentifier])
            return text.trimmingCharacters(in: .whitespacesAndNewlines)
        } catch is CancellationError {
            throw CancellationError()
        } catch let error as AppFailure {
            throw error
        } catch {
            throw AppFailure.transcription("本地识别请求失败：\(error.localizedDescription)")
        }
    }

    func transcribe(_ samples: [Int16], executablePath: String, modelPath: String) async throws -> String {
        try await transcribe(samples, modelPath: modelPath, language: "auto")
    }

    func cancelAll() {
        lock.lock()
        let oldSession = session
        session = URLSession(configuration: .ephemeral)
        lock.unlock()
        oldSession.invalidateAndCancel()
    }

    func shutdown() async {
        cancelAll()
        await stopWorker()
    }

    func shutdownImmediately() {
        cancelAll()
        let oldWorker = takeWorker()
        if oldWorker?.process.isRunning == true { oldWorker?.process.terminate() }
        try? oldWorker?.logHandle?.close()
    }

    private func currentWorker() -> Worker? {
        lock.lock()
        defer { lock.unlock() }
        return worker
    }

    private func setWorker(_ nextWorker: Worker?) {
        lock.lock()
        worker = nextWorker
        lock.unlock()
    }

    private func takeWorker() -> Worker? {
        lock.lock()
        defer { lock.unlock() }
        let oldWorker = worker
        worker = nil
        return oldWorker
    }

    private func currentSession() -> URLSession {
        lock.lock()
        defer { lock.unlock() }
        return session
    }

    private func stopWorker() async {
        let oldWorker = takeWorker()
        defer { try? oldWorker?.logHandle?.close() }
        guard let process = oldWorker?.process, process.isRunning else { return }
        process.terminate()
        await Task.detached(priority: .utility) {
            let deadline = ProcessInfo.processInfo.systemUptime + 2
            while process.isRunning && ProcessInfo.processInfo.systemUptime < deadline { try? await Task.sleep(for: .milliseconds(50)) }
            if process.isRunning { Darwin.kill(process.processIdentifier, SIGKILL) }
            process.waitUntilExit()
        }.value
    }

    private static func availablePort() throws -> UInt16 {
        let fd = socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else { throw AppFailure.transcription("无法为本地识别组件分配端口。") }
        defer { close(fd) }

        var address = sockaddr_in()
        address.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        address.sin_family = sa_family_t(AF_INET)
        address.sin_port = 0
        address.sin_addr = in_addr(s_addr: in_addr_t(INADDR_LOOPBACK).bigEndian)
        let bindResult = withUnsafePointer(to: &address) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                bind(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        guard bindResult == 0 else { throw AppFailure.transcription("无法为本地识别组件保留端口。") }

        var result = sockaddr_in()
        var length = socklen_t(MemoryLayout<sockaddr_in>.size)
        let nameResult = withUnsafeMutablePointer(to: &result) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                getsockname(fd, $0, &length)
            }
        }
        guard nameResult == 0 else { throw AppFailure.transcription("无法读取本地识别端口。") }
        return UInt16(bigEndian: result.sin_port)
    }

    private static func makeMultipart(samples: [Int16], boundary: String, language: String, vocabulary: String) -> Data {
        var body = Data()
        let newline = "\r\n"

        func addField(_ name: String, _ value: String) {
            body.append(Data("--\(boundary)\r\nContent-Disposition: form-data; name=\"\(name)\"\r\n\r\n\(value)\r\n".utf8))
        }

        body.append(Data("--\(boundary)\r\nContent-Disposition: form-data; name=\"file\"; filename=\"segment.wav\"\r\nContent-Type: audio/wav\r\n\r\n".utf8))
        body.append(makeWave(samples))
        body.append(Data(newline.utf8))
        addField("language", language)
        addField("prompt", String(vocabulary.prefix(1_200)))
        addField("response_format", "verbose_json")
        addField("temperature", "0.0")
        addField("no_language_probabilities", "true")
        addField("translate", "false")
        body.append(Data("--\(boundary)--\r\n".utf8))
        return body
    }

    private static func makeWave(_ samples: [Int16]) -> Data {
        let byteCount = UInt32(samples.count * MemoryLayout<Int16>.size)
        var data = Data()
        data.append(contentsOf: Array("RIFF".utf8))
        data.appendLittleEndian(byteCount + 36)
        data.append(contentsOf: Array("WAVEfmt ".utf8))
        data.appendLittleEndian(UInt32(16))
        data.appendLittleEndian(UInt16(1))
        data.appendLittleEndian(UInt16(1))
        data.appendLittleEndian(UInt32(16_000))
        data.appendLittleEndian(UInt32(32_000))
        data.appendLittleEndian(UInt16(2))
        data.appendLittleEndian(UInt16(16))
        data.append(contentsOf: Array("data".utf8))
        data.appendLittleEndian(byteCount)
        for sample in samples {
            data.appendLittleEndian(UInt16(bitPattern: sample))
        }
        return data
    }

    fileprivate static func verifyModel(at url: URL) throws {
        try WhisperModelFile.verify(at: url, expected: WhisperModel.matching(url))
    }

}

private final class ModelDownloadOperation: NSObject, URLSessionDownloadDelegate, @unchecked Sendable {
    private let lock = NSLock()
    private let model: WhisperModel
    private let destination: URL
    private let progress: @Sendable (Double) -> Void
    private var continuation: CheckedContinuation<Void, Error>?
    private var session: URLSession?
    private var task: URLSessionDownloadTask?
    private var isFinished = false
    private var cancellationRequested = false

    init(model: WhisperModel, destination: URL, progress: @escaping @Sendable (Double) -> Void) {
        self.model = model
        self.destination = destination
        self.progress = progress
    }

    func start() async throws {
        try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
            lock.lock()
            if cancellationRequested {
                lock.unlock()
                continuation.resume(throwing: CancellationError())
                return
            }
            self.continuation = continuation
            let session = URLSession(configuration: .ephemeral, delegate: self, delegateQueue: nil)
            self.session = session
            let task = session.downloadTask(with: model.downloadURL)
            self.task = task
            lock.unlock()
            task.resume()
        }
    }

    func cancel() {
        lock.lock()
        cancellationRequested = true
        let task = task
        lock.unlock()
        task?.cancel()
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didWriteData bytesWritten: Int64, totalBytesWritten: Int64, totalBytesExpectedToWrite: Int64) {
        guard totalBytesExpectedToWrite > 0 else { return }
        progress(min(1, Double(totalBytesWritten) / Double(totalBytesExpectedToWrite)))
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didFinishDownloadingTo location: URL) {
        do {
            guard let response = downloadTask.response as? HTTPURLResponse,
                  (200..<300).contains(response.statusCode) else {
                throw AppFailure.transcription("模型服务器返回了下载错误。请检查网络后重试。")
            }
            try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
            let partialURL = destination.appendingPathExtension("partial")
            try? FileManager.default.removeItem(at: partialURL)
            try FileManager.default.moveItem(at: location, to: partialURL)
            try WhisperModelFile.verify(at: partialURL, expected: model)
            lock.lock()
            let cancelled = cancellationRequested
            lock.unlock()
            if cancelled { throw CancellationError() }
            try? FileManager.default.removeItem(at: destination)
            try FileManager.default.moveItem(at: partialURL, to: destination)
            progress(1)
            finish(.success(()))
        } catch {
            try? FileManager.default.removeItem(at: destination.appendingPathExtension("partial"))
            finish(.failure(error))
        }
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        if let error { finish(.failure((error as NSError).code == NSURLErrorCancelled ? CancellationError() : error)) }
    }

    private func finish(_ result: Result<Void, Error>) {
        lock.lock()
        guard !isFinished else {
            lock.unlock()
            return
        }
        isFinished = true
        let nextContinuation = self.continuation
        self.continuation = nil
        let nextSession = session
        self.session = nil
        lock.unlock()
        nextSession?.finishTasksAndInvalidate()
        nextContinuation?.resume(with: result)
    }
}

private extension Data {
    mutating func appendLittleEndian<T: FixedWidthInteger>(_ value: T) {
        var littleEndian = value.littleEndian
        Swift.withUnsafeBytes(of: &littleEndian) { append(contentsOf: $0) }
    }
}
