import Foundation
import Testing
@testable import InterviewHackerCore

struct ProcessLineReaderTests {
    @Test func idlePipeDoesNotBlockAnotherProcess() async throws {
        let idle = Pipe(), active = Pipe()
        let idleReader = Task {
            for try await _ in ProcessLineReader.lines(from: idle.fileHandleForReading) {}
        }
        defer {
            try? idle.fileHandleForWriting.close()
            try? active.fileHandleForWriting.close()
            idleReader.cancel()
        }
        try await Task.sleep(for: .milliseconds(50))
        let lines = ProcessLineReader.lines(from: active.fileHandleForReading)
        active.fileHandleForWriting.write(Data("second process ready\n".utf8))
        let result = try await withThrowingTaskGroup(of: String?.self) { group in
            group.addTask {
                for try await line in lines { return line }
                return nil
            }
            group.addTask {
                try await Task.sleep(for: .seconds(2))
                return nil
            }
            defer { group.cancelAll() }
            return try await group.next()!
        }
        #expect(result == "second process ready")
    }

    @Test func preservesSplitUTF8CRLFAndFinalUnterminatedLine() async throws {
        let pipe = Pipe()
        let lines = ProcessLineReader.lines(from: pipe.fileHandleForReading)
        let data = Data("联网🙂\r\nlast".utf8)
        for byte in data { pipe.fileHandleForWriting.write(Data([byte])) }
        try pipe.fileHandleForWriting.close()
        var result: [String] = []
        for try await line in lines { result.append(line) }
        #expect(result == ["联网🙂", "last"])
    }
}
