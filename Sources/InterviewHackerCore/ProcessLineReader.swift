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
