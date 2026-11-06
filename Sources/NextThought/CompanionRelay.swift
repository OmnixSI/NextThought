import Foundation
import NextThoughtCore

@MainActor final class CompanionRelay {
    private var socket: URLSessionWebSocketTask?
    private var task: Task<Void, Never>?
    private var base: URL?
    private var publisher = ""
    private var pending: CompanionEnvelope?
    private var sending = false
    private var generation = UUID()
    private(set) var pairing: CompanionPairing?
    var status: (String) -> Void = { _ in }
    func start(base: URL, local: CompanionPairing, loopbackTest: Bool = false) async throws -> CompanionPairing {
        stop(); let generation = self.generation
        guard (base.scheme == "https" || (loopbackTest && DemoMode.isEnabled && base.host == "127.0.0.1" && Bundle.main.bundleIdentifier?.hasPrefix("com.nextthought.app.ci") == true)), base.user == nil, base.password == nil, base.host != nil else { throw AppFailure.capture("中转入口必须为 HTTPS 地址。") }
        var request = URLRequest(url: base.appendingPathComponent("rooms")); request.httpMethod = "POST"; request.timeoutInterval = 10
        let (data, response) = try await URLSession.shared.data(for: request)
        guard (response as? HTTPURLResponse)?.statusCode == 200 else { throw AppFailure.capture("中转无法创建房间，请检查服务容量或地址。") }
        struct Room: Decodable { var room: String; var publisher: String; var subscriber: String }
        let room = try JSONDecoder().decode(Room.self, from: data)
        guard self.generation == generation else { throw CancellationError() }
        var p = local; p.relay = base.absoluteString; p.room = room.room; p.subscriber = room.subscriber
        self.base = base; self.publisher = room.publisher; pairing = p
        task = Task { [weak self] in
            var attempts = 0
            while !Task.isCancelled {
                guard let self, self.generation == generation else { return }
                do { try await self.connect(); attempts = 0 }
                catch { self.status("中转正在恢复连接") }
                if Task.isCancelled { return }
                attempts += 1
                try? await Task.sleep(for: .seconds(min(8, 0.5 * pow(2, Double(min(4, attempts)))) * Double.random(in: 0.8...1.2)))
            }
        }
        return p
    }
    func stop() {
        generation = UUID(); task?.cancel(); task = nil
        socket?.cancel(with: .goingAway, reason: nil); socket = nil
        if let base, let pairing, !publisher.isEmpty {
            var request = URLRequest(url: base.appendingPathComponent("rooms/\(pairing.room)/close"))
            request.httpMethod = "POST"; request.setValue("Bearer \(publisher)", forHTTPHeaderField: "Authorization"); request.timeoutInterval = 5
            Task { _ = try? await URLSession.shared.data(for: request) }
        }
        pairing = nil; base = nil; publisher = ""; pending = nil; sending = false
    }
    private func connect() async throws {
        guard let base, let pairing else { return }
        var c = URLComponents(url: base.appendingPathComponent("socket"), resolvingAgainstBaseURL: false)!; c.scheme = base.scheme == "https" ? "wss" : "ws"
        let ws = URLSession.shared.webSocketTask(with: c.url!); socket = ws; ws.resume()
        var lastPong = Date()
        let watchdog = Task {
            while !Task.isCancelled {
                if Date().timeIntervalSince(lastPong) > 8 { ws.cancel(with: .goingAway, reason: nil); return }
                ws.sendPing { error in Task { @MainActor in if error == nil { lastPong = Date() } else { ws.cancel(with: .goingAway, reason: nil) } } }
                try? await Task.sleep(for: .seconds(2))
            }
        }
        defer { watchdog.cancel() }
        defer { ws.cancel(with: .goingAway, reason: nil); if socket === ws { socket = nil } }
        let join = ["room": pairing.room, "role": "publisher", "token": publisher]
        try await ws.send(.data(JSONEncoder().encode(join)))
        publish(CompanionServer.shared.currentEnvelope())
        while !Task.isCancelled {
            let message = try await ws.receive()
            let data: Data
            switch message { case .data(let d): data = d; case .string(let s): data = Data(s.utf8); @unknown default: continue }
            if (try? JSONSerialization.jsonObject(with: data) as? [String: Any])?["joined"] as? Bool == true { status("中转已连接 · 端到端加密"); continue }
            guard let frame = try? JSONDecoder().decode(CompanionCipherFrame.self, from: data), frame.kind == "command",
                  let command = try? CompanionCrypto.open(frame, as: CompanionCommand.self, pairing: pairing), command.isFresh,
                  CompanionServer.shared.performCommand(command.action, id: command.id) else { continue }
            let ack = try CompanionCrypto.seal(CompanionAcknowledgement(id: command.id), kind: "ack", pairing: pairing)
            try await ws.send(.data(JSONEncoder().encode(ack)))
        }
    }
    func publish(_ envelope: CompanionEnvelope) {
        pending = envelope
        guard !sending, let socket, let pairing else { return }
        sending = true
        Task { [weak self] in
            guard let self else { return }
            defer { self.sending = false }
            while let value = self.pending, self.socket === socket, !Task.isCancelled {
                self.pending = nil
                do {
                    let frame = try CompanionCrypto.seal(value, kind: "state", pairing: pairing)
                    try await socket.send(.data(JSONEncoder().encode(frame)))
                } catch { socket.cancel(with: .goingAway, reason: nil); return }
            }
        }
    }
}
