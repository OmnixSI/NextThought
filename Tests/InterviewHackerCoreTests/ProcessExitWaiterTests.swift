import Foundation
import Testing
@testable import InterviewHackerCore
struct ProcessExitWaiterTests {
    @Test func exitBeforeAwaitIsRecorded() async throws {
        for _ in 0..<20 {
            let process=Process();let completion=ProcessExitWaiter(process)
            process.executableURL=URL(fileURLWithPath:"/usr/bin/true")
            try process.run()
            try await Task.sleep(for:.milliseconds(10))
            #expect(await completion.wait()==0)
        }
    }
}
