import Foundation
import Testing
@testable import NextThoughtCore

@Suite struct CompanionBLETests {
    @Test func shuffledDuplicateAndInterruptedFrames() {
        let source = Data((0..<3000).map { UInt8($0 % 251) })
        let pieces = CompanionBLEProtocol.fragments(source, id: 0x12345678, mtu: 185)
        #expect(pieces.allSatisfy { $0.count <= 185 })
        #expect(Array(pieces[0].prefix(8)) == [0x12,0x34,0x56,0x78,0,0,0,17])
        var receiver = CompanionBLEProtocol.Reassembler()
        #expect(receiver.receive(pieces[0]) == nil)
        #expect(receiver.receive(pieces[0]) == nil)
        var result: (id: UInt32, data: Data)?
        for packet in pieces.dropFirst().reversed() { if let value = receiver.receive(packet) { result = value } }
        #expect(result?.id == 0x12345678); #expect(result?.data == source)
        #expect(receiver.receive(Data(repeating: 0, count: 8)) == nil)
        let small = CompanionBLEProtocol.fragments(Data("new message".utf8), id: 4, mtu: 20)
        _ = receiver.receive(pieces[0]); #expect(receiver.receive(small[0])?.data == Data("new message".utf8))
    }
    @Test func expiryBoundsAndMinimumMTU() {
        let data = Data(repeating: 1, count: 50), start = Date()
        let pieces = CompanionBLEProtocol.fragments(data, id: 9, mtu: 20)
        var receiver = CompanionBLEProtocol.Reassembler()
        _ = receiver.receive(pieces[0], now: start)
        for p in pieces.dropFirst() { #expect(receiver.receive(p, now: start.addingTimeInterval(16)) == nil) }
        #expect(CompanionBLEProtocol.fragments(data, id: 1, mtu: 19).isEmpty)
        #expect(CompanionBLEProtocol.fragments(Data(repeating: 1, count: 400000), id: 1, mtu: 20).isEmpty)
    }
}

@Suite struct CompanionBLEDeliveryTests {
    @Test func lostAcknowledgementRetriesAndLatestStateWins() {
        let start = Date(), first = Data("first".utf8), latest = Data("latest".utf8)
        var outbox = CompanionBLEOutbox(seed: 40)
        outbox.enqueueState(first)
        let packet = outbox.peek(mtu: 20, now: start)!
        #expect(outbox.activeMessageID == 41); outbox.didSend(now: start)
        outbox.enqueueState(Data("superseded".utf8)); outbox.enqueueState(latest)
        #expect(outbox.peek(mtu: 20, now: start.addingTimeInterval(1)) == nil)
        #expect(outbox.peek(mtu: 20, now: start.addingTimeInterval(3)) == packet)
        outbox.didSend(now: start.addingTimeInterval(3))
        outbox.acknowledge(40) // Wrong message cannot advance the queue.
        #expect(outbox.activeMessageID == 41)
        outbox.acknowledge(41)
        let next = outbox.peek(mtu: 20, now: start.addingTimeInterval(4))!
        #expect(Data(next.dropFirst(8)) == latest); #expect(outbox.activeMessageID == 42)
    }
    @Test func boundedRetriesAndSlowContinuousAssembly() {
        let start = Date(); var outbox = CompanionBLEOutbox(seed: 7)
        outbox.enqueueState(Data("old".utf8))
        for attempt in 0..<4 {
            #expect(outbox.peek(mtu: 20, now: start.addingTimeInterval(Double(attempt * 3))) != nil)
            outbox.didSend(now: start.addingTimeInterval(Double(attempt * 3)))
        }
        #expect(outbox.peek(mtu: 20, now: start.addingTimeInterval(12)) == nil); #expect(outbox.failureCount == 1)
        let raw = Data(repeating: 3, count: 80), frames = CompanionBLEProtocol.fragments(raw, id: 5, mtu: 20)
        var receiver = CompanionBLEProtocol.Reassembler(), result: Data?
        for (i, f) in frames.enumerated() { if let value = receiver.receive(f, now: start.addingTimeInterval(Double(i * 4))) { result = value.data } }
        #expect(result == raw) // Total > 15s; active progress keeps the frame alive.
    }
}
