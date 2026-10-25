import Foundation
import InterviewHackerCore
import Network
import Observation
import Security

/// LAN phone companion: the phone opens a tokenized URL (shown as a QR code) to see
/// the current question and streaming answer, and can trigger actions. Off by
/// default; the token rotates every time the service starts and every request is
/// checked. Only the current answer state is shared — no transcript, files or images.
@MainActor
final class CompanionServer {
    static let shared = CompanionServer()

    private var listener: NWListener?
    @MainActor private final class Stream {
        let connection: NWConnection
        var sending = false
        var pending: Data?
        var sentAt = Date()
        init(_ connection: NWConnection) { self.connection = connection }
    }
    private var streams: [ObjectIdentifier: Stream] = [:]
    private var readers: [ObjectIdentifier: NWConnection] = [:]
    private var readerDeadlines: [ObjectIdentifier: Task<Void, Never>] = [:]
    private var serverID = UUID().uuidString
    private var sequence: UInt64 = 0
    private var envelope: CompanionEnvelope?
    private var commandCache = CompanionCommandCache()
    private var lastHeartbeat = Date.distantPast
    private var token = ""
    private var observing = false
    private var observationGeneration = UUID()
    private var heartbeat: Task<Void, Never>?
    private var lastSent: CompanionSnapshot?
    private let queue = DispatchQueue(label: "app.interviewhacker.companion")
    private(set) var pairingURL: URL?
    private(set) var pairing = CompanionPairing()
    let relay = CompanionRelay()
    let bluetooth = CompanionBluetooth()
    var bluetoothEnabled = false
    func setBluetooth(enabled: Bool) {
        bluetoothEnabled = enabled
        if enabled { bluetooth.start(pairing: pairing); bluetooth.publish(currentEnvelope()) } else { bluetooth.stop() }
    }
    func configureRelay(base: URL) async throws -> URL {
        let p = try await relay.start(base: base, local: pairing)
        pairing = p
        if bluetoothEnabled { bluetooth.start(pairing: pairing) }
        if let local = pairingURL { pairingURL = try p.url(base: local) }
        return try p.url(base: base)
    }
    func disableRelay() {
        relay.stop(); pairing.relay = nil; pairing.subscriber = nil
        if bluetoothEnabled { bluetooth.start(pairing: pairing) }
        if let local = pairingURL { pairingURL = try? pairing.url(base: local) }
    }

    /// Starts on the preferred port; if it is taken (another app, or a previous
    /// instance still closing) the next free port is used automatically.
    func start(preferredPort: Int, onStatus: @escaping (String, String?) -> Void) {
        stop()
        token = Self.randomToken()
        pairing = CompanionPairing(token: token, ble: UUID().uuidString)
        let base = (1_024...65_000).contains(preferredPort) ? preferredPort : 8_765
        let candidates = Array(base..<(base + 20))
        listen(on: candidates, index: 0, onStatus: onStatus)
        serverID = UUID().uuidString; sequence = 0; envelope = nil
        commandCache = CompanionCommandCache()
        // A cheap snapshot check also heals missed Observation notifications.
        heartbeat = Task { [weak self] in
            while !Task.isCancelled {
                guard let self else { return }
                self.broadcast()
                if Date().timeIntervalSince(self.lastHeartbeat) >= 2 {
                    self.lastHeartbeat = Date()
                    self.sendToStreams(self.currentEnvelope().sse(event: "heartbeat"))
                    self.relay.publish(self.currentEnvelope())
                    self.bluetooth.publish(self.currentEnvelope())
                }
                for (key, stream) in self.streams where stream.sending && Date().timeIntervalSince(stream.sentAt) > 8 {
                    stream.connection.cancel(); self.streams.removeValue(forKey: key)
                }
                self.publishClientCount()
                do { try await Task.sleep(for: .milliseconds(150)) } catch { return }
            }
        }
        Diagnostics.record("companion.started")
    }

    private func listen(on ports: [Int], index: Int, onStatus: @escaping (String, String?) -> Void) {
        // Last resort: let the system pick any free port.
        let port: NWEndpoint.Port = index < ports.count ? (NWEndpoint.Port(rawValue: UInt16(ports[index])) ?? .any) : .any
        let parameters = NWParameters.tcp
        parameters.allowLocalEndpointReuse = true
        let listener: NWListener
        do {
            listener = try NWListener(using: parameters, on: port)
        } catch {
            if index <= ports.count { listen(on: ports, index: index + 1, onStatus: onStatus) }
            else { onStatus("启动失败：\(error.localizedDescription)", nil) }
            return
        }
        listener.newConnectionHandler = { [weak self] connection in
            Task { @MainActor in self?.accept(connection) }
        }
        listener.stateUpdateHandler = { [weak self] state in
            Task { @MainActor in
                guard let self, self.listener === listener else { return }
                switch state {
                case .ready:
                    let actual = listener.port?.rawValue ?? 0
                    guard let host = Self.lanAddress() else {
                        onStatus("没有找到局域网地址；请把 Mac 和手机连到同一个 Wi‑Fi 后点“重新生成链接”。", nil); return
                    }
                    let url = URL(string: "http://\(host):\(actual)/?t=\(self.token)")
                    self.pairing.lan = "http://\(host):\(actual)"
                    self.pairingURL = try? self.pairing.url(base: url!)
                    let note = index == 0 ? "" : "（端口 \(ports.first ?? 0) 被占用，已改用 \(actual)）"
                    onStatus("已开启 · 手机与 Mac 需在同一局域网\(note)", self.pairingURL?.absoluteString)
                case let .failed(error):
                    listener.cancel()
                    if Self.isAddressInUse(error), index <= ports.count {
                        Diagnostics.record("companion.port_in_use", ["port": index < ports.count ? ports[index] : 0])
                        self.listen(on: ports, index: index + 1, onStatus: onStatus)
                    } else {
                        onStatus("启动失败：\(error.localizedDescription)", nil)
                        self.stop()
                    }
                default: break
                }
            }
        }
        self.listener = listener
        listener.start(queue: queue)
    }

    private static func isAddressInUse(_ error: NWError) -> Bool {
        if case let .posix(code) = error { return code == .EADDRINUSE }
        return false
    }

    /// Phones streaming the live page right now; shown as the phone status in the
    /// live page's top bar.
    var clientCount: Int { streams.count }

    private func publishClientCount() {
        let state = AppState.shared
        let count = streams.count
        guard state.companionClientCount != count else { return }
        state.companionClientCount = count
        if count > 0 { state.companionHadClient = true }
    }

    func stop() {
        AppState.shared.companionClientCount = 0
        AppState.shared.companionHadClient = false
        observing = false
        relay.stop(); bluetooth.stop(); bluetoothEnabled = false
        heartbeat?.cancel(); heartbeat = nil
        listener?.cancel(); listener = nil
        streams.values.forEach { $0.connection.cancel() }; streams.removeAll()
        readers.values.forEach { $0.cancel() }; readers.removeAll()
        readerDeadlines.values.forEach { $0.cancel() }; readerDeadlines.removeAll()
        token = ""; pairingURL = nil; lastSent = nil; envelope = nil
    }

    func currentEnvelope() -> CompanionEnvelope {
        let snapshot = AppState.shared.companionSnapshot
        if envelope == nil || snapshot != envelope?.snapshot {
            sequence += 1
            envelope = CompanionEnvelope(serverID: serverID, sequence: sequence, snapshot: snapshot)
        }
        return envelope!
    }

    private func broadcast() {
        let current = currentEnvelope()
        guard current.snapshot != lastSent else { return }
        lastSent = current.snapshot
        sendToStreams(current.sse())
        relay.publish(current)
        bluetooth.publish(current)
    }

    private func sendToStreams(_ data: Data) {
        for stream in streams.values { send(data, to: stream) }
    }

    private func send(_ data: Data, to stream: Stream) {
        if stream.sending { stream.pending = data; return }
        stream.sending = true; stream.sentAt = Date()
        let key = ObjectIdentifier(stream.connection)
        stream.connection.send(content: data, completion: .contentProcessed { [weak self, weak stream] error in
            Task { @MainActor in
                guard let self, let stream, self.streams[key] === stream else { return }
                stream.sending = false
                if error != nil { self.streams[key] = nil; stream.connection.cancel(); return }
                if let pending = stream.pending { stream.pending = nil; self.send(pending, to: stream) }
            }
        })
    }

    private func accept(_ connection: NWConnection) {
        guard listener != nil, readers.count < 32 else { connection.cancel(); return }
        let key = ObjectIdentifier(connection)
        readers[key] = connection
        readerDeadlines[key] = Task { [weak self] in
            do { try await Task.sleep(for: .seconds(5)) } catch { return }
            self?.finishReading(connection); connection.cancel()
        }
        connection.start(queue: queue)
        readHead(connection, accumulated: Data())
    }

    private func finishReading(_ connection: NWConnection) {
        let key = ObjectIdentifier(connection)
        readers[key] = nil; readerDeadlines.removeValue(forKey: key)?.cancel()
    }

    private func readHead(_ connection: NWConnection, accumulated: Data) {
        connection.receive(minimumIncompleteLength: 1, maximumLength: 4096) { [weak self] data, _, complete, error in
            Task { @MainActor in
                guard let self, self.readers[ObjectIdentifier(connection)] != nil else { return }
                var head = accumulated; if let data { head.append(data) }
                guard error == nil, head.count <= 16384 else { self.finishReading(connection); connection.cancel(); return }
                if let range = head.range(of: Data("\r\n\r\n".utf8)),
                   let text = String(data: head[..<range.upperBound], encoding: .utf8), let request = CompanionHTTP.parse(text) {
                    self.finishReading(connection); self.route(request, connection)
                } else if complete { self.finishReading(connection); connection.cancel() }
                else { self.readHead(connection, accumulated: head) }
            }
        }
    }

    func performCommand(_ action: String, id: String) -> Bool {
        guard CompanionHTTP.allowedActions.contains(action), UUID(uuidString: id) != nil else { return false }
        if commandCache.accept(id) { AppState.shared.performCompanionAction(action) }
        return true
    }

    private func route(_ request: CompanionHTTP.Request, _ connection: NWConnection) {
        if request.method == "GET", ["/", "/phone.js"].contains(request.path) {
            let name = request.path == "/" ? "index.html" : "phone.js"
            guard let data = CompanionPage.asset(name) else { reply(connection, "503 Service Unavailable", "text/plain", Data("手机页面资源缺失，请重新安装。".utf8)); return }
            reply(connection, "200 OK", request.path == "/" ? "text/html; charset=utf-8" : "text/javascript; charset=utf-8", data,
                ["Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'unsafe-inline'; img-src 'self' data:; connect-src 'self' wss:; frame-ancestors 'none'; base-uri 'none'"])
            return
        }
        guard CompanionHTTP.tokenMatches(request.query["t"], token) else {
            reply(connection, "403 Forbidden", "text/plain; charset=utf-8", Data("配对码无效或已过期，请重新扫码。".utf8))
            return
        }
        switch (request.method, request.path) {
        case ("GET", "/snapshot"):
            reply(connection, "200 OK", "application/json", (try? JSONEncoder().encode(currentEnvelope())) ?? Data())
        case ("POST", "/ack"):
            reply(connection, "204 No Content", "text/plain", Data())
        case ("GET", "/events"):
            guard streams.count < 12 else { reply(connection, "429 Too Many Requests", "text/plain", Data()); return }
            let head = "HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nCache-Control: no-store\r\nConnection: keep-alive\r\nX-Accel-Buffering: no\r\n\r\n"
            var first = Data(head.utf8); first.append(currentEnvelope().sse())
            let key = ObjectIdentifier(connection), stream = Stream(connection)
            streams[key] = stream
            connection.stateUpdateHandler = { [weak self] state in
                if case .cancelled = state { Task { @MainActor in self?.streams[key] = nil } }
                if case .failed = state { Task { @MainActor in self?.streams[key] = nil } }
            }
            send(first, to: stream)
        case ("POST", "/action"):
            guard let action = request.query["a"], let id = request.query["id"], performCommand(action, id: id) else {
                reply(connection, "400 Bad Request", "text/plain", Data()); return
            }
            reply(connection, "204 No Content", "text/plain", Data())
        default:
            reply(connection, "404 Not Found", "text/plain", Data())
        }
    }

    private func reply(_ connection: NWConnection, _ status: String, _ type: String, _ body: Data, _ headers: [String] = []) {
        connection.send(content: CompanionHTTP.response(status: status, contentType: type, body: body, extraHeaders: headers),
                        completion: .contentProcessed { _ in connection.cancel() })
    }

    private static func randomToken() -> String {
        var bytes = [UInt8](repeating: 0, count: 16)
        _ = SecRandomCopyBytes(kSecRandomDefault, bytes.count, &bytes)
        return bytes.map { String(format: "%02x", $0) }.joined()
    }

    /// First private IPv4 address on an active Wi‑Fi/Ethernet interface.
    nonisolated static func lanAddress() -> String? {
        var pointer: UnsafeMutablePointer<ifaddrs>?
        guard getifaddrs(&pointer) == 0, let first = pointer else { return nil }
        defer { freeifaddrs(pointer) }
        var candidates: [(name: String, address: String)] = []
        for item in Swift.sequence(first: first, next: { $0.pointee.ifa_next }) {
            let flags = Int32(item.pointee.ifa_flags)
            guard let address = item.pointee.ifa_addr, address.pointee.sa_family == UInt8(AF_INET),
                  flags & IFF_UP != 0, flags & IFF_LOOPBACK == 0 else { continue }
            var host = [CChar](repeating: 0, count: Int(NI_MAXHOST))
            guard getnameinfo(address, socklen_t(address.pointee.sa_len), &host, socklen_t(host.count), nil, 0, NI_NUMERICHOST) == 0 else { continue }
            let name = String(cString: item.pointee.ifa_name)
            let text = String(cString: host)
            guard text.hasPrefix("192.168.") || text.hasPrefix("10.") || text.range(of: #"^172\.(1[6-9]|2\d|3[01])\."#, options: .regularExpression) != nil else { continue }
            candidates.append((name, text))
        }
        return (candidates.first { $0.name.hasPrefix("en") } ?? candidates.first)?.address
    }
}

enum CompanionPage {
    static func asset(_ name: String) -> Data? {
        let root = Bundle.main.resourceURL?.appendingPathComponent("Companion")
        if let root, let data = try? Data(contentsOf: root.appendingPathComponent(name)) { return data }
        let source = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        return try? Data(contentsOf: source.appendingPathComponent("Resources/Companion/\(name)"))
    }
}
