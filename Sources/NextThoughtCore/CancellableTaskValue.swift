import Foundation

/// Wait for shared prewarming without making an individual cancelled request
/// wait for it, or cancelling the shared resource other callers will reuse.
public enum CancellableTaskValue {
    private final class Gate<Value: Sendable>: @unchecked Sendable {
        let lock = NSLock()
        var continuation: CheckedContinuation<Value, Error>?
        var result: Result<Value, Error>?
        func attach(_ continuation: CheckedContinuation<Value, Error>) {
            let ready = lock.withLock { () -> Result<Value, Error>? in
                if let result { return result }; self.continuation = continuation; return nil
            }
            if let ready { continuation.resume(with: ready) }
        }
        func finish(_ result: Result<Value, Error>) {
            let target = lock.withLock { () -> CheckedContinuation<Value, Error>? in
                guard self.result == nil else { return nil }
                self.result = result; let target = continuation; continuation = nil; return target
            }
            target?.resume(with: result)
        }
    }
    public static func value<T: Sendable>(_ task: Task<T, Error>) async throws -> T {
        let gate = Gate<T>()
        return try await withTaskCancellationHandler {
            try Task.checkCancellation()
            return try await withCheckedThrowingContinuation { continuation in
                gate.attach(continuation)
                Task { gate.finish(await task.result) }
            }
        } onCancel: { gate.finish(.failure(CancellationError())) }
    }
}
