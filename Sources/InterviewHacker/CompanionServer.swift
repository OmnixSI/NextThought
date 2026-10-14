import Combine
import Foundation
import InterviewHackerCore
import Network
import Security

/// LAN phone companion: the phone opens a tokenized URL (shown as a QR code) to see
/// the current question and streaming answer, and can trigger actions. Off by
/// default; the token rotates every time the service starts and every request is
/// checked. Only the current answer state is shared — no transcript, files or images.
@MainActor
final class CompanionServer {
    static let shared = CompanionServer()

    private var listener: NWListener?
    private var streams: [ObjectIdentifier: NWConnection] = [:]
    private var token = ""
    private var subscription: AnyCancellable?
    private var heartbeat: Task<Void, Never>?
    private var lastSent: CompanionSnapshot?
    private let queue = DispatchQueue(label: "app.interviewhacker.companion")
    private(set) var pairingURL: URL?

    func start(onStatus: @escaping (String, String?) -> Void) {
        stop()
        token = Self.randomToken()
        do {
            let parameters = NWParameters.tcp
            parameters.allowLocalEndpointReuse = true
            let listener = try NWListener(using: parameters, on: NWEndpoint.Port(rawValue: 8_765) ?? .any)
            listener.newConnectionHandler = { [weak self] connection in
                Task { @MainActor in self?.accept(connection) }
            }
            listener.stateUpdateHandler = { [weak self] state in
                Task { @MainActor in
                    guard let self else { return }
                    switch state {
                    case .ready:
                        let port = listener.port?.rawValue ?? 8_765
                        guard let host = Self.lanAddress() else {
                            onStatus("没有找到局域网地址；请连接与手机相同的 Wi‑Fi。", nil); return
                        }
                        let url = URL(string: "http://\(host):\(port)/?t=\(self.token)")
                        self.pairingURL = url
                        onStatus("已开启 · 手机与 Mac 需在同一局域网", url?.absoluteString)
                    case let .failed(error):
                        onStatus("启动失败：\(error.localizedDescription)", nil)
                        self.stop()
                    default: break
                    }
                }
            }
            listener.start(queue: queue)
            self.listener = listener
        } catch {
            onStatus("启动失败：\(error.localizedDescription)", nil)
            return
        }
        // Push the answer state to connected phones whenever the app state changes.
        subscription = AppState.shared.objectWillChange
            .debounce(for: .milliseconds(120), scheduler: RunLoop.main)
            .sink { [weak self] _ in self?.broadcast() }
        heartbeat = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(for: .seconds(15))
                self?.sendToStreams(Data(": ping\n\n".utf8))
            }
        }
        Diagnostics.record("companion.started")
    }

    func stop() {
        subscription = nil
        heartbeat?.cancel(); heartbeat = nil
        listener?.cancel(); listener = nil
        streams.values.forEach { $0.cancel() }
        streams.removeAll()
        token = ""; pairingURL = nil; lastSent = nil
    }

    private func broadcast() {
        guard !streams.isEmpty else { return }
        let snapshot = AppState.shared.companionSnapshot
        guard snapshot != lastSent else { return }
        lastSent = snapshot
        sendToStreams(CompanionHTTP.sseEvent(snapshot))
    }

    private func sendToStreams(_ data: Data) {
        for (key, connection) in streams {
            connection.send(content: data, completion: .contentProcessed { [weak self] error in
                guard error != nil else { return }
                Task { @MainActor in self?.streams.removeValue(forKey: key); connection.cancel() }
            })
        }
    }

    private func accept(_ connection: NWConnection) {
        guard listener != nil else { connection.cancel(); return }
        connection.start(queue: queue)
        connection.receive(minimumIncompleteLength: 1, maximumLength: 8_192) { [weak self] data, _, _, error in
            Task { @MainActor in
                guard let self, error == nil, let data, let head = String(data: data, encoding: .utf8),
                      let request = CompanionHTTP.parse(head) else { connection.cancel(); return }
                self.route(request, connection)
            }
        }
    }

    private func route(_ request: CompanionHTTP.Request, _ connection: NWConnection) {
        guard CompanionHTTP.tokenMatches(request.query["t"], token) else {
            reply(connection, "403 Forbidden", "text/plain; charset=utf-8", Data("配对码无效或已过期，请重新扫码。".utf8))
            return
        }
        switch (request.method, request.path) {
        case ("GET", "/"):
            reply(connection, "200 OK", "text/html; charset=utf-8", Data(CompanionPage.html.utf8),
                  ["Content-Security-Policy: default-src 'self'; script-src 'unsafe-inline'; style-src 'unsafe-inline'"])
        case ("GET", "/events"):
            guard streams.count < 4 else { reply(connection, "429 Too Many Requests", "text/plain", Data()); return }
            let head = "HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nCache-Control: no-store\r\nConnection: keep-alive\r\n\r\n"
            var first = Data(head.utf8)
            first.append(CompanionHTTP.sseEvent(AppState.shared.companionSnapshot))
            let key = ObjectIdentifier(connection)
            streams[key] = connection
            connection.stateUpdateHandler = { [weak self] state in
                if case .cancelled = state { Task { @MainActor in self?.streams.removeValue(forKey: key) } }
                if case .failed = state { Task { @MainActor in self?.streams.removeValue(forKey: key) } }
            }
            connection.send(content: first, completion: .contentProcessed { _ in })
        case ("POST", "/action"):
            guard let action = request.query["a"], CompanionHTTP.allowedActions.contains(action) else {
                reply(connection, "400 Bad Request", "text/plain", Data()); return
            }
            Diagnostics.record("companion.action", ["action": action])
            AppState.shared.performCompanionAction(action)
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
        for item in sequence(first: first, next: { $0.pointee.ifa_next }) {
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
    static let html = #"""
<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<meta name="apple-mobile-web-app-capable" content="yes">
<title>InterviewHacker</title>
<style>
:root{--bg:#0f1115;--card:#181b22;--text:#eef1f6;--muted:#9aa3b2;--accent:#5b8cff;--line:#262b35}
@media (prefers-color-scheme: light){:root{--bg:#f5f6f8;--card:#fff;--text:#14171c;--muted:#5d6675;--line:#e3e6eb}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:16px/1.55 -apple-system,BlinkMacSystemFont,"PingFang SC",sans-serif;padding:12px 16px calc(96px + env(safe-area-inset-bottom))}
header{display:flex;align-items:center;gap:8px;color:var(--muted);font-size:13px}
.dot{width:8px;height:8px;border-radius:50%;background:var(--muted)}.live .dot{background:#34c759}
#q{margin:12px 0 6px;color:var(--muted);font-size:14px}
#a{background:var(--card);border:1px solid var(--line);border-radius:16px;padding:16px;white-space:pre-wrap;word-break:break-word;min-height:40vh}
#a strong{color:var(--accent)}#cand{font-size:13px;color:var(--muted);margin-top:8px}
nav{position:fixed;left:0;right:0;bottom:0;padding:10px 12px calc(10px + env(safe-area-inset-bottom));background:var(--bg);border-top:1px solid var(--line);display:flex;gap:8px;overflow-x:auto}
button{flex:0 0 auto;border:0;border-radius:12px;padding:12px 14px;font-size:15px;background:var(--card);color:var(--text)}
button.primary{background:var(--accent);color:#fff;font-weight:600}
.size{margin-left:auto;display:flex;gap:6px}.size button{padding:4px 10px}
</style></head><body>
<header id="h"><span class="dot"></span><span id="s">连接中…</span><span id="p"></span>
<span class="size"><button onclick="zoom(-2)">A-</button><button onclick="zoom(2)">A+</button></span></header>
<div id="q"></div><div id="a">等待回答…</div><div id="cand"></div>
<nav>
<button class="primary" onclick="act('answer')">回答</button>
<button onclick="act('shorter')">更简短</button><button onclick="act('example')">举例</button>
<button onclick="act('english')">英文版</button><button onclick="act('previous')">上一条</button>
<button onclick="act('next')">下一条</button><button onclick="act('cancel')">取消</button>
</nav>
<script>
const t=new URLSearchParams(location.search).get('t');let size=+(localStorage.getItem('ih-size')||20);
const A=document.getElementById('a');A.style.fontSize=size+'px';
function zoom(d){size=Math.max(14,Math.min(40,size+d));A.style.fontSize=size+'px';localStorage.setItem('ih-size',size)}
function esc(s){return s.replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]))}
function md(s){return esc(s).replace(/\*\*(.+?)\*\*/g,'<strong>$1</strong>').replace(/`([^`]+)`/g,'<code>$1</code>')}
function act(a){fetch('/action?t='+encodeURIComponent(t)+'&a='+a,{method:'POST'}).catch(()=>{})}
function connect(){const es=new EventSource('/events?t='+encodeURIComponent(t));
es.onmessage=e=>{const d=JSON.parse(e.data);document.getElementById('s').textContent=d.status+(d.answering?' · 生成中':'');
document.getElementById('h').className=d.status==='正在聆听'?'live':'';document.getElementById('p').textContent=d.position;
document.getElementById('q').textContent=d.question;A.innerHTML=d.answer?md(d.answer):'等待回答…';
document.getElementById('cand').textContent=d.candidate?('候选：'+d.candidate):''};
es.onerror=()=>{document.getElementById('s').textContent='连接断开，正在重连…'}}
connect();
</script></body></html>
"""#
}
