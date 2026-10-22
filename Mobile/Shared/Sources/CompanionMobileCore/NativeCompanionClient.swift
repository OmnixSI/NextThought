import Combine
import Foundation

@MainActor public final class NativeCompanionClient: ObservableObject {
    @Published public private(set) var snapshot: CompanionSnapshot?
    @Published public private(set) var connection = "请扫描或粘贴 Mac 配对链接"
    @Published public private(set) var bluetoothStatus = "蓝牙未开启"
    public private(set) var pairing: CompanionPairing?
    private var generation = UUID()
    private var tasks: [Task<Void, Never>] = []
    private var relaySocket: URLSessionWebSocketTask?
    private var last: [String: Date] = [:]
    private var server = "", sequence: UInt64 = 0, retired = Set<String>()
    private var pending: [String: CompanionCommand] = [:]
    private var seenNonces = Set<String>(), nonceOrder: [String] = []
    private let bluetooth = BluetoothCentral()
    public init() {}
    public static func parse(_ link: String) throws -> CompanionPairing {
        guard let url = URL(string: link.trimmingCharacters(in: .whitespacesAndNewlines)),
              let fragment = url.fragment, let encoded = URLComponents(string: "https://pair.invalid/?" + fragment)?.queryItems?.first(where: { $0.name == "p" })?.value else { throw ClientFailure.invalidPairing }
        var base64 = encoded.replacingOccurrences(of: "-", with: "+").replacingOccurrences(of: "_", with: "/")
        base64 += String(repeating: "=", count: (4 - base64.count % 4) % 4)
        guard let data = Data(base64Encoded: base64), data.count < 16384 else { throw ClientFailure.invalidPairing }
        let p = try JSONDecoder().decode(CompanionPairing.self, from: data)
        guard p.v == 1, Data(base64Encoded: p.key)?.count == 32, !p.room.isEmpty,
              p.relay.flatMap(URL.init(string:)).map({ $0.scheme == "https" }) ?? true else { throw ClientFailure.invalidPairing }
        return p
    }
    public func start(link: String, enableBluetooth: Bool) throws {
        let p = try Self.parse(link); stop(); pairing = p; connection = "正在连接…"; let g = generation
        bluetooth.onFrame = { [weak self] in self?.encrypted($0, via: "蓝牙备用") }
        bluetooth.onStatus = { [weak self] in self?.bluetoothStatus = $0 }
        bluetooth.onReady = { [weak self] in self?.resync() }
        if enableBluetooth, p.ble != nil { bluetooth.start(p) }
        if p.lan != nil, p.token != nil {
            tasks.append(Task { [weak self] in
                var attempt = 0
                while !Task.isCancelled {
                    guard let self, self.generation == g else { return }
                    do { try await self.events(); attempt = 0 } catch { attempt += 1 }
                    try? await Task.sleep(for: .seconds(min(8, 0.5 * pow(2, Double(min(attempt, 4))))))
                }
            })
        }
        if p.relay != nil {
            tasks.append(Task { [weak self] in
                var attempt = 0
                while !Task.isCancelled {
                    guard let self, self.generation == g else { return }
                    do { try await self.relay(); attempt = 0 } catch { attempt += 1 }
                    try? await Task.sleep(for: .seconds(min(8, 0.5 * pow(2, Double(min(attempt, 4))))))
                }
            })
        }
        tasks.append(Task { [weak self] in
            while !Task.isCancelled {
                guard let self, self.generation == g else { return }
                if Date().timeIntervalSince(self.last["局域网"] ?? .distantPast) > 5 { await self.poll() }
                guard self.generation == g else { return }
                if self.last.values.max().map({ Date().timeIntervalSince($0) > 8 }) ?? true { self.connection = "离线，正在恢复…" }
                for (id, command) in self.pending {
                    if Date().timeIntervalSince1970 * 1000 - command.createdAt > 15000 { self.pending[id] = nil; self.connection = "操作未确认，请检查连接" }
                    else { await self.send(command) }
                }
                try? await Task.sleep(for: .seconds(1))
            }
        })
    }
    public func stop() {
        generation = UUID(); tasks.forEach { $0.cancel() }; tasks = []; bluetooth.stop()
        relaySocket?.cancel(with: .goingAway, reason: nil); relaySocket = nil
        pairing = nil; pending = [:]; last = [:]; server = ""; sequence = 0; retired = []; seenNonces = []; nonceOrder = []
        snapshot = nil; connection = "已断开"; bluetoothStatus = "蓝牙未开启"
    }
    public func resume() { Task { [weak self] in await self?.poll(); self?.resync() } }
    public func action(_ action: String) {
        guard CompanionHTTP.allowedActions.contains(action) else { return }
        let command = CompanionCommand(action: action), g = generation; pending[command.id] = command
        Task { [weak self] in guard let self, self.generation == g else { return }; await self.send(command) }
    }
    private func resync() {
        guard let p = pairing, let frame = try? CompanionCrypto.seal(CompanionCommand(action: "resync"), kind: "command", pairing: p) else { return }
        bluetooth.send(frame)
    }
    private func lanURL(_ path: String, query: [URLQueryItem] = []) -> URL? {
        guard let p = pairing, let base = p.lan, let url = URL(string: base) else { return nil }
        var c = URLComponents(url: url.appendingPathComponent(path), resolvingAgainstBaseURL: false)
        c?.queryItems = [URLQueryItem(name: "t", value: p.token)] + query
        return c?.url
    }
    private func poll() async {
        guard let url = lanURL("snapshot") else { return }; let g = generation
        var r = URLRequest(url: url); r.timeoutInterval = 2.5; r.cachePolicy = .reloadIgnoringLocalCacheData
        if let (data, response) = try? await URLSession.shared.data(for: r), (response as? HTTPURLResponse)?.statusCode == 200,
           let e = try? JSONDecoder().decode(CompanionEnvelope.self, from: data), generation == g { apply(e, via: "局域网") }
    }
    private func events() async throws {
        guard let url = lanURL("events") else { return }; let g = generation
        let (bytes, response) = try await URLSession.shared.bytes(for: URLRequest(url: url))
        guard (response as? HTTPURLResponse)?.statusCode == 200 else { throw ClientFailure.disconnected }
        for try await line in bytes.lines {
            try Task.checkCancellation(); guard generation == g else { return }
            if line.hasPrefix("data: "), let data = String(line.dropFirst(6)).data(using: .utf8), let e = try? JSONDecoder().decode(CompanionEnvelope.self, from: data) { apply(e, via: "局域网") }
        }
    }
    private func relay() async throws {
        guard let p = pairing, let base = p.relay.flatMap(URL.init(string:)) else { return }; let g = generation
        var c = URLComponents(url: base.appendingPathComponent("socket"), resolvingAgainstBaseURL: false)!; c.scheme = "wss"
        let ws = URLSession.shared.webSocketTask(with: c.url!); relaySocket = ws; ws.resume()
        var lastPong = Date()
        let ping = Task {
            while !Task.isCancelled {
                if Date().timeIntervalSince(lastPong) > 8 { ws.cancel(with: .goingAway, reason: nil); return }
                ws.sendPing { error in Task { @MainActor in if error == nil { lastPong = Date() } else { ws.cancel(with: .goingAway, reason: nil) } } }
                try? await Task.sleep(for: .seconds(2))
            }
        }
        defer { ping.cancel() }
        defer { ws.cancel(with: .goingAway, reason: nil); if relaySocket === ws { relaySocket = nil } }
        try await ws.send(.data(JSONEncoder().encode(["room": p.room, "role": "subscriber", "token": p.subscriber ?? ""])))
        while !Task.isCancelled, generation == g {
            let m = try await ws.receive(), data: Data
            switch m { case .data(let d): data = d; case .string(let s): data = Data(s.utf8); @unknown default: continue }
            if let frame = try? JSONDecoder().decode(CompanionCipherFrame.self, from: data) { encrypted(frame, via: "加密中转") }
        }
    }
    private func encrypted(_ frame: CompanionCipherFrame, via transport: String) {
        guard let p = pairing else { return }
        do {
            if frame.kind == "ack" {
                let ack = try CompanionCrypto.open(frame, as: CompanionAcknowledgement.self, pairing: p); pending[ack.id] = nil
            } else if frame.kind == "state" {
                let e = try CompanionCrypto.open(frame, as: CompanionEnvelope.self, pairing: p)
                let key = frame.kind + frame.n
                guard seenNonces.insert(key).inserted else { return }; nonceOrder.append(key)
                if nonceOrder.count > 4096 { seenNonces.remove(nonceOrder.removeFirst()) }
                apply(e, via: transport)
            }
        } catch { connection = "配对校验失败，请重新扫描" }
    }
    public func apply(_ e: CompanionEnvelope, via transport: String) {
        guard e.version == 1, !retired.contains(e.serverID), server != e.serverID || e.sequence >= sequence else { return }
        last[transport] = Date()
        if transport != "局域网", Date().timeIntervalSince(last["局域网"] ?? .distantPast) < 4 { return }
        if !server.isEmpty, server != e.serverID { retired.insert(server) }
        server = e.serverID; sequence = e.sequence; snapshot = e.snapshot; connection = transport + " · 已连接"
    }
    private func send(_ command: CompanionCommand) async {
        let g = generation
        if let url = lanURL("action", query: [.init(name: "a", value: command.action), .init(name: "id", value: command.id)]) {
            var r = URLRequest(url: url); r.httpMethod = "POST"; r.timeoutInterval = 1.5
            if let (_, response) = try? await URLSession.shared.data(for: r), (response as? HTTPURLResponse)?.statusCode == 204, generation == g { pending[command.id] = nil; return }
        }
        guard generation == g, let p = pairing, let frame = try? CompanionCrypto.seal(command, kind: "command", pairing: p) else { return }
        if bluetooth.ready { bluetooth.send(frame) }
        if let relaySocket { try? await relaySocket.send(.data(JSONEncoder().encode(frame))) }
    }
    public enum ClientFailure: LocalizedError {
        case invalidPairing, disconnected
        public var errorDescription: String? { self == .invalidPairing ? "配对链接无效，请复制 Mac 设置中的 App 配对链接。" : "连接中断" }
    }
}
