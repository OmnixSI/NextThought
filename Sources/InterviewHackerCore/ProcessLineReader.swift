import Foundation
import Darwin

/// Each process gets its own blocking reader. FileHandle.bytes shares a Foundation
/// IO actor on macOS: an idle pipe can prevent every other process from being read.
public enum ProcessLineReader {
    public static func lines(from handle: FileHandle) -> AsyncThrowingStream<String, Error> {
        AsyncThrowingStream { continuation in
            Thread.detachNewThread {
                defer { try? handle.close() }
                var buffer = Data()
                do {
                    var chunk = [UInt8](repeating: 0, count: 64 * 1024)
                    while true {
                        // Foundation read(upToCount:) may wait for the requested size;
                        // POSIX read returns as soon as any bytes arrive on this pipe.
                        let count = Darwin.read(handle.fileDescriptor, &chunk, chunk.count)
                        if count == 0 { break }
                        if count < 0 {
                            if errno == EINTR { continue }
                            throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno))
                        }
                        buffer.append(contentsOf: chunk.prefix(count))
                        while let newline = buffer.firstIndex(of: 10) {
                            var line = buffer[..<newline]
                            if line.last == 13 { line = line.dropLast() }
                            if case .terminated = continuation.yield(String(decoding: line, as: UTF8.self)) { return }
                            buffer.removeSubrange(...newline)
                        }
                    }
                    if !buffer.isEmpty { continuation.yield(String(decoding: buffer, as: UTF8.self)) }
                    continuation.finish()
                } catch { continuation.finish(throwing: error) }
            }
        }
    }
}

/// Record completion before launching the child. Avoid Foundation.waitUntilExit's
/// run-loop race when the child exits between EOF and a detached wait.
public final class ProcessExitWaiter: @unchecked Sendable {
    private let lock = NSLock()
    private let signal = DispatchSemaphore(value: 0)
    private var status: Int32?
    private var waiters: [CheckedContinuation<Int32, Never>] = []
    public init(_ process: Process) {
        process.terminationHandler = { [weak self] child in self?.finish(child.terminationStatus) }
    }
    private func finish(_ value: Int32) {
        lock.lock(); status = value; let callbacks = waiters; waiters.removeAll(); lock.unlock()
        signal.signal()
        for callback in callbacks { callback.resume(returning: value) }
    }
    public func waitSynchronously(timeout: TimeInterval) -> Int32? {
        lock.lock(); let ready = status; lock.unlock()
        if let ready { return ready }
        guard signal.wait(timeout: .now() + timeout) == .success else { return nil }
        lock.lock(); defer { lock.unlock() }; return status
    }
    public func wait() async -> Int32 {
        await withCheckedContinuation { callback in
            lock.lock()
            if let status { lock.unlock(); callback.resume(returning: status) }
            else { waiters.append(callback); lock.unlock() }
        }
    }
}
