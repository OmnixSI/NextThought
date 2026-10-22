import Foundation
import InterviewHackerCore

@MainActor enum CompanionRelaySelfTest {
    static func run(_ options: [String: String]) async throws -> String {
        guard DemoMode.isEnabled, Bundle.main.bundleIdentifier?.hasPrefix("com.interviewhacker.app.ci") == true,
              let endpoint = options["endpoint"], let base = URL(string: endpoint), base.host == "127.0.0.1" else { throw SelfTest.Failure("relay test requires isolated .ci and loopback") }
        let relay = CompanionRelay(); defer { relay.stop() }
        let pair = try await relay.start(base: base, local: CompanionPairing(), loopbackTest: true)
        func connect() async throws -> URLSessionWebSocketTask {
            var c = URLComponents(url: base.appendingPathComponent("socket"), resolvingAgainstBaseURL: false)!; c.scheme = "ws"
            let ws = URLSession.shared.webSocketTask(with: c.url!); ws.resume()
            try await ws.send(.data(JSONEncoder().encode(["room": pair.room, "role": "subscriber", "token": pair.subscriber!])))
            return ws
        }
        func receive<T: Decodable>(_ ws: URLSessionWebSocketTask, _ kind: String, _: T.Type) async throws -> T {
            try await withThrowingTaskGroup(of: T.self) { group in
                group.addTask {
                    while !Task.isCancelled {
                        let m = try await ws.receive(), d: Data
                        switch m { case .data(let data): d = data; case .string(let s): d = Data(s.utf8); @unknown default: continue }
                        if let frame = try? JSONDecoder().decode(CompanionCipherFrame.self, from: d), frame.kind == kind { return try CompanionCrypto.open(frame, as: T.self, pairing: pair) }
                    }
                    throw CancellationError()
                }
                group.addTask { try await Task.sleep(for: .seconds(8)); ws.cancel(with: .goingAway, reason: nil); throw SelfTest.Failure("relay receive timeout") }
                defer { group.cancelAll() }
                return try await group.next()!
            }
        }
        let ws = try await connect(); defer { ws.cancel(with: .goingAway, reason: nil) }
        let envelope = CompanionEnvelope(serverID: "relay-selftest", sequence: 99, snapshot: .init(status: "test", question: "question-secret", answer: "answer-secret", answering: false, candidate: "", position: ""))
        try await Task.sleep(for: .milliseconds(500)); relay.publish(envelope)
        var received = try await receive(ws, "state", CompanionEnvelope.self)
        if received.serverID != envelope.serverID { received = try await receive(ws, "state", CompanionEnvelope.self) }
        guard received == envelope else { throw SelfTest.Failure("relay changed state") }
        let command = CompanionCommand(action: "cancel")
        let frame = try CompanionCrypto.seal(command, kind: "command", pairing: pair)
        try await ws.send(.data(JSONEncoder().encode(frame)))
        let ack = try await receive(ws, "ack", CompanionAcknowledgement.self)
        guard ack.id == command.id else { throw SelfTest.Failure("command ack mismatch") }
        ws.cancel(with: .goingAway, reason: nil)
        let restored = try await connect(); defer { restored.cancel(with: .goingAway, reason: nil) }
        guard try await receive(restored, "state", CompanionEnvelope.self) == envelope else { throw SelfTest.Failure("relay reconnect lost latest") }
        return "Mac URLSession WebSocket -> real Node relay -> subscriber AES-GCM state, command ack and reconnect snapshot passed"
    }
}
