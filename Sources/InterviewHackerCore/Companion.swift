import Foundation

/// State mirrored to the paired phone. Deliberately small: no transcript history,
/// documents or screenshots leave the Mac through the companion.
public struct CompanionSnapshot: Codable, Sendable, Equatable {
    public var status: String
    public var question: String
    public var answer: String
    public var answering: Bool
    public var candidate: String
    public var position: String
    public var sessionID: String?
    public var answerID: String?
    public var phase: String?
    public var sources: String?
    public var slide: String?
    public var updateAvailable: Bool?
    public init(status: String, question: String, answer: String, answering: Bool, candidate: String, position: String,
                sessionID: String? = nil, answerID: String? = nil, phase: String? = nil, sources: String? = nil,
                slide: String? = nil, updateAvailable: Bool = false) {
        self.status = status; self.question = question; self.answer = answer
        self.answering = answering; self.candidate = candidate; self.position = position
        self.sessionID = sessionID; self.answerID = answerID; self.phase = phase
        self.sources = sources; self.slide = slide; self.updateAvailable = updateAvailable
    }
}

/// A full replacement, never a text delta. Sequence numbers are scoped to one
/// server lifetime; reconnecting clients may always recover from a single frame.
public struct CompanionEnvelope: Codable, Sendable, Equatable {
    public var version = 1
    public var serverID: String
    public var sequence: UInt64
    public var updatedAt: Double
    public var snapshot: CompanionSnapshot
    public init(serverID: String, sequence: UInt64, snapshot: CompanionSnapshot, now: Date = Date()) {
        self.serverID = serverID; self.sequence = sequence; self.snapshot = snapshot
        updatedAt = now.timeIntervalSince1970 * 1000
    }
    public func sse(event: String? = nil) -> Data {
        let json = (try? JSONEncoder().encode(self)).map { String(decoding: $0, as: UTF8.self) } ?? "{}"
        return Data("id: \(serverID):\(sequence)\n\(event.map { "event: \($0)\n" } ?? "")data: \(json)\n\n".utf8)
    }
}

/// Shared by LAN, relay and Bluetooth. A command can be retried on another
/// transport without generating a second answer or moving two pages.
public struct CompanionCommandCache: Sendable {
    private var commands: [String: Date] = [:]
    public init() {}
    public mutating func accept(_ id: String, now: Date = Date()) -> Bool {
        guard UUID(uuidString: id) != nil else { return false }
        commands = commands.filter { now.timeIntervalSince($0.value) < 300 }
        guard commands[id] == nil else { return false }
        if commands.count >= 1024, let first = commands.min(by: { $0.value < $1.value })?.key { commands[first] = nil }
        commands[id] = now
        return true
    }
}

public enum CompanionHTTP {
    public struct Request: Equatable, Sendable {
        public var method: String
        public var path: String
        public var query: [String: String]
    }

    public static let allowedActions: Set<String> = ["answer", "cancel", "regenerate", "previous", "next", "shorter", "detailed", "example", "english", "chinese", "star", "clarify", "followUps"]

    /// Parses the request line of an HTTP/1.1 request head.
    public static func parse(_ head: String) -> Request? {
        guard let line = head.components(separatedBy: CharacterSet.newlines).first, !line.isEmpty else { return nil }
        let parts = line.split(separator: " ")
        guard parts.count >= 3, parts[2].hasPrefix("HTTP/1."), parts[1].hasPrefix("/"), parts[1].count < 2_048 else { return nil }
        let target = String(parts[1])
        let pieces = target.split(separator: "?", maxSplits: 1)
        var query: [String: String] = [:]
        if pieces.count == 2 {
            for pair in pieces[1].split(separator: "&") {
                let kv = pair.split(separator: "=", maxSplits: 1)
                guard let key = kv.first?.removingPercentEncoding else { continue }
                query[key] = kv.count == 2 ? (String(kv[1]).replacingOccurrences(of: "+", with: " ").removingPercentEncoding ?? "") : ""
            }
        }
        return Request(method: String(parts[0]), path: String(pieces[0]), query: query)
    }

    /// Constant-time token comparison.
    public static func tokenMatches(_ provided: String?, _ expected: String) -> Bool {
        guard let provided, !expected.isEmpty else { return false }
        let a = Array(provided.utf8), b = Array(expected.utf8)
        guard a.count == b.count else { return false }
        var difference: UInt8 = 0
        for index in 0..<a.count { difference |= a[index] ^ b[index] }
        return difference == 0
    }

    public static func response(status: String, contentType: String, body: Data, extraHeaders: [String] = []) -> Data {
        var head = "HTTP/1.1 \(status)\r\nContent-Type: \(contentType)\r\nContent-Length: \(body.count)\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\nConnection: close\r\n"
        for header in extraHeaders { head += header + "\r\n" }
        var data = Data((head + "\r\n").utf8)
        data.append(body)
        return data
    }

    public static func sseEvent(_ snapshot: CompanionSnapshot) -> Data {
        let json = (try? JSONEncoder().encode(snapshot)).map { String(decoding: $0, as: UTF8.self) } ?? "{}"
        return Data("data: \(json)\n\n".utf8)
    }
}
