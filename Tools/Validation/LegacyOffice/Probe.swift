import Foundation
import Darwin

enum AppFailure: Error { case capture(String) }
@main struct Probe {
    static func main() async throws {
        let root = URL(fileURLWithPath: CommandLine.arguments[1]), fake = root.appendingPathComponent("soffice")
        let pidFile = URL(fileURLWithPath: ProcessInfo.processInfo.environment["IH_LEGACY_TEST_PID"]!)
        func source(_ name: String) throws -> URL { let url = root.appendingPathComponent(name + ".ppt"); try Data("authored fixture".utf8).write(to: url); return url }
        let success = try await LegacyOfficeConverter.convert(source("success"), into: root.appendingPathComponent("success-output"), executable: fake)
        guard FileManager.default.fileExists(atPath: success.path) else { fatalError("successful output missing") }
        print("PASS conversion result")
        do { _ = try await LegacyOfficeConverter.convert(source("failure"), into: root.appendingPathComponent("failure-output"), executable: fake); fatalError("failed process accepted") } catch AppFailure.capture { }
        print("PASS process failure")
        try? FileManager.default.removeItem(at: pidFile)
        let slow = try source("slow")
        let conversion = Task { try await LegacyOfficeConverter.convert(slow, into: root.appendingPathComponent("cancel-output"), executable: fake) }
        let until = Date().addingTimeInterval(3)
        var childPID: Int32?
        while childPID == nil, Date() < until {
            if let contents = try? String(contentsOf: pidFile, encoding: .utf8) {
                childPID = Int32(contents.trimmingCharacters(in: .whitespacesAndNewlines))
            }
            if childPID == nil { try await Task.sleep(for: .milliseconds(20)) }
        }
        guard let pid = childPID else { fatalError("child PID missing") }
        let cancelled = Date(); conversion.cancel()
        do { _ = try await conversion.value; fatalError("cancelled conversion succeeded") } catch is CancellationError { }
        guard Date().timeIntervalSince(cancelled) < 1 else { fatalError("cancellation delayed") }
        let terminated = Date().addingTimeInterval(1)
        while kill(pid, 0) == 0, Date() < terminated { try await Task.sleep(for: .milliseconds(20)) }
        guard kill(pid, 0) != 0 else { fatalError("cancelled process left running") }
        print("PASS immediate cancellation terminates child")
        do { _ = try await LegacyOfficeConverter.convert(slow, into: root.appendingPathComponent("timeout-output"), executable: fake, timeout: 0.15); fatalError("timeout accepted") } catch AppFailure.capture { }
        print("PASS timeout")
        if let path = ProcessInfo.processInfo.environment["IH_LEGACY_REAL_PPT"] {
            let actual = try await LegacyOfficeConverter.convert(URL(fileURLWithPath:path), into:root.appendingPathComponent("actual-output"))
            guard try Data(contentsOf:actual).starts(with: [0x50,0x4b,0x03,0x04]) else { fatalError("actual LibreOffice output invalid") }
            print("PASS actual LibreOffice PPT conversion")
        }
    }
}
