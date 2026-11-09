import Foundation

public enum CompanionBLEProtocol {
    public static let service = "9A8F0001-1C28-4E55-BE7D-C4E9A13277B0"
    public static let state = "9A8F0002-1C28-4E55-BE7D-C4E9A13277B0"
    public static let command = "9A8F0003-1C28-4E55-BE7D-C4E9A13277B0"
    public static let maximumBytes = 384 * 1024
    public static func fragments(_ data: Data, id: UInt32, mtu: Int) -> [Data] {
        guard !data.isEmpty, data.count <= maximumBytes, mtu >= 20 else { return [] }
        let payload = min(512, mtu) - 8, count = (data.count + payload - 1) / payload
        guard count <= 32768 else { return [] }
        return (0..<count).map { index in
            var header = Data([UInt8((id >> 24) & 255), UInt8((id >> 16) & 255), UInt8((id >> 8) & 255), UInt8(id & 255),
                UInt8((index >> 8) & 255), UInt8(index & 255), UInt8((count >> 8) & 255), UInt8(count & 255)])
            header.append(data[(index * payload)..<min(data.count, (index + 1) * payload)])
            return header
        }
    }
    public struct Reassembler: Sendable {
        private var id: UInt32?
        private var count = 0
        private var chunks: [Int: Data] = [:]
        private var bytes = 0
        private var started = Date.distantPast
        public init() {}
        public mutating func receive(_ packet: Data, now: Date = Date()) -> (id: UInt32, data: Data)? {
            guard packet.count > 8, packet.count <= 512 else { return nil }
            let a = Array(packet), message = UInt32(a[0]) << 24 | UInt32(a[1]) << 16 | UInt32(a[2]) << 8 | UInt32(a[3])
            let part = Int(a[4]) << 8 | Int(a[5]), total = Int(a[6]) << 8 | Int(a[7])
            guard total > 0, total <= 32768, part < total else { return nil }
            if id != message || now.timeIntervalSince(started) > 15 {
                id = message; count = total; chunks = [:]; bytes = 0; started = now
            }
            guard count == total else { return nil }
            if chunks[part] == nil { chunks[part] = packet.dropFirst(8); bytes += packet.count - 8; started = now }
            guard bytes <= maximumBytes else { id = nil; chunks = [:]; bytes = 0; return nil }
            guard chunks.count == count else { return nil }
            var data = Data(); data.reserveCapacity(bytes)
            for i in 0..<count { guard let chunk = chunks[i] else { return nil }; data.append(chunk) }
            id = nil; chunks = [:]; bytes = 0
            return (message, data)
        }
    }
}

/// Reliable notification outbox: current transfer + one replaceable latest state.
/// ATT flow control calls peek/didSend; only an authenticated whole-message ACK
/// completes a transfer. A missing ACK replays exactly the same message ID.
public struct CompanionBLEOutbox: Sendable {
    private var state: Data?
    private var acknowledgements: [Data] = []
    private var packets: [Data] = []
    private var cursor = 0
    private var awaiting: Date?
    private var retries = 0
    private var counter: UInt32
    public private(set) var activeMessageID: UInt32?
    public private(set) var failureCount = 0
    public init(seed: UInt32 = UInt32.random(in: 1...UInt32.max)) { counter = seed }
    public mutating func enqueueState(_ data: Data) { if data.count <= CompanionBLEProtocol.maximumBytes { state = data } }
    public mutating func enqueueAcknowledgement(_ data: Data) {
        guard data.count <= CompanionBLEProtocol.maximumBytes else { return }
        if acknowledgements.count >= 8 { acknowledgements.removeFirst() }
        acknowledgements.append(data)
    }
    public mutating func acknowledge(_ id: UInt32) {
        guard activeMessageID == id else { return }
        packets = []; cursor = 0; awaiting = nil; retries = 0; activeMessageID = nil
    }
    public mutating func peek(mtu: Int, now: Date = Date()) -> Data? {
        if let awaiting, now.timeIntervalSince(awaiting) > 2 {
            self.awaiting = nil
            if retries < 3 { retries += 1; cursor = 0 }
            else { packets = []; cursor = 0; retries = 0; activeMessageID = nil; failureCount += 1 }
        }
        if packets.isEmpty {
            let data: Data
            if !acknowledgements.isEmpty { data = acknowledgements.removeFirst() }
            else if let state { data = state; self.state = nil }
            else { return nil }
            counter &+= 1; activeMessageID = counter
            packets = CompanionBLEProtocol.fragments(data, id: counter, mtu: mtu)
            cursor = 0; retries = 0
        }
        guard awaiting == nil, cursor < packets.count else { return nil }
        return packets[cursor]
    }
    public mutating func didSend(now: Date = Date()) {
        guard awaiting == nil, cursor < packets.count else { return }
        cursor += 1
        if cursor == packets.count { awaiting = now }
    }
}
