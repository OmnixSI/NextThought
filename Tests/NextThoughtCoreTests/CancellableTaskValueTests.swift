import Foundation
import Testing
@testable import NextThoughtCore
@Suite struct CancellableTaskValueTests {
    @Test func cancelledWaitDoesNotCancelWarmResource() async throws {
        let warm = Task<Int,Error> { try await Task.sleep(for: .seconds(2)); return 42 }
        defer { warm.cancel() }
        let waiter = Task { try await CancellableTaskValue.value(warm) }
        try await Task.sleep(for: .milliseconds(30)); let started = Date(); waiter.cancel()
        do { _ = try await waiter.value; Issue.record("cancelled waiter completed") } catch is CancellationError {} catch { Issue.record("wrong cancellation: \(error)") }
        #expect(Date().timeIntervalSince(started) < 0.5); #expect(!warm.isCancelled)
    }
    @Test func completedWarmResourceRemainsReusable() async throws {
        let warm = Task<Int,Error> { 7 }
        #expect(try await CancellableTaskValue.value(warm) == 7)
        #expect(try await CancellableTaskValue.value(warm) == 7)
    }
}
