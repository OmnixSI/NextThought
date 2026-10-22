import Foundation
import Network
import Testing
@testable import CompanionMobileCore

private final class LANFixture: @unchecked Sendable {
    let listener: NWListener
    private let lock = NSLock()
    private var envelope: CompanionEnvelope
    private var streams: [NWConnection] = []
    private var commands = CompanionCommandCache()
    private var count = 0
    var commandCount: Int { lock.withLock { count } }
    init(_ envelope: CompanionEnvelope) throws { self.envelope = envelope; listener = try NWListener(using: .tcp, on: .any) }
    func start() async throws -> UInt16 {
        listener.newConnectionHandler = { [weak self] connection in
            guard let self else { return }; connection.start(queue: .global())
            connection.receive(minimumIncompleteLength: 1, maximumLength: 4096) { data, _, _, _ in
                guard let data, let r = CompanionHTTP.parse(String(decoding: data, as: UTF8.self)) else { connection.cancel(); return }
                if r.path == "/events" {
                    self.lock.withLock { self.streams.append(connection) }
                    let e = self.lock.withLock { self.envelope }
                    var bytes = Data("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nConnection: keep-alive\r\n\r\n".utf8); bytes.append(e.sse())
                    connection.send(content: bytes, completion: .contentProcessed { _ in })
                } else if r.path == "/snapshot" {
                    let bytes = self.lock.withLock { try! JSONEncoder().encode(self.envelope) }
                    connection.send(content: CompanionHTTP.response(status: "200 OK", contentType: "application/json", body: bytes), completion: .contentProcessed { _ in connection.cancel() })
                } else if r.path == "/action", let id = r.query["id"] {
                    self.lock.withLock { if self.commands.accept(id) { self.count += 1 } }
                    connection.send(content: CompanionHTTP.response(status: "204 No Content", contentType: "text/plain", body: Data()), completion: .contentProcessed { _ in connection.cancel() })
                } else { connection.cancel() }
            }
        }
        return try await withCheckedThrowingContinuation { continuation in
            listener.stateUpdateHandler = { [weak self] state in
                if case .ready = state, let port = self?.listener.port?.rawValue { self?.listener.stateUpdateHandler = nil; continuation.resume(returning: port) }
                if case .failed(let error) = state { self?.listener.stateUpdateHandler = nil; continuation.resume(throwing: error) }
            }
            listener.start(queue: .global())
        }
    }
    func update(_ value: CompanionEnvelope, broadcast: Bool) {
        let targets = lock.withLock { envelope = value; return streams }
        if broadcast { for c in targets { c.send(content: value.sse(), completion: .contentProcessed { _ in }) } }
    }
    func stop() { listener.cancel(); for c in lock.withLock({ streams }) { c.cancel() } }
}
@Suite struct LANIntegrationTests {
    @Test @MainActor func realTCPStateControlAndSilentStreamRecovery() async throws {
        func frame(_ seq: UInt64, _ q: String, _ a: String) -> CompanionEnvelope {
            .init(serverID: "native-lan", sequence: seq, snapshot: .init(status: "fixture", question: q, answer: a, answering: a.isEmpty, candidate: "", position: ""))
        }
        let fixture = try LANFixture(frame(1,"first","first answer")); let port = try await fixture.start(); defer { fixture.stop() }
        let client = NativeCompanionClient(); defer { client.stop() }
        let pairing = CompanionPairing(lan: "http://127.0.0.1:\(port)", token: "fixture-token")
        try client.start(link: pairing.url(base: URL(string: "interviewhacker-companion://pair")!).absoluteString, enableBluetooth: false)
        func wait(_ condition: () -> Bool, timeout: TimeInterval = 3) async throws {
            let start = Date()
            while !condition(), Date().timeIntervalSince(start) < timeout { try await Task.sleep(for: .milliseconds(50)) }
            #expect(condition())
        }
        try await wait { client.snapshot?.question == "first" }
        fixture.update(frame(2,"second",""), broadcast: true)
        try await wait { client.snapshot?.question == "second" }
        #expect(client.snapshot?.answer == "")
        client.action("next"); #expect(client.pendingActions.contains("next")); #expect(client.operationStatus.contains("等待 Mac"))
        client.action("next")
        try await wait { fixture.commandCount == 1 && client.pendingActions.isEmpty }
        #expect(client.operationStatus == "Mac 已确认操作")
        // A half-open SSE remains silent, but HTTP still serves the newest state.
        fixture.update(frame(3,"silent stream recovered","complete"), broadcast: false)
        try await wait({ client.snapshot?.question == "silent stream recovered" }, timeout: 8)
        #expect(client.connection.contains("局域网")); #expect(fixture.commandCount == 1)
    }
}
