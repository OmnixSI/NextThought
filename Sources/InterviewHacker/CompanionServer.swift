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
    private final class Stream {
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

    /// Starts on the preferred port; if it is taken (another app, or a previous
    /// instance still closing) the next free port is used automatically.
    func start(preferredPort: Int, onStatus: @escaping (String, String?) -> Void) {
        stop()
        token = Self.randomToken()
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
                }
                for (key, stream) in self.streams where stream.sending && Date().timeIntervalSince(stream.sentAt) > 8 {
                    stream.connection.cancel(); self.streams.removeValue(forKey: key)
                }
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
                    self.pairingURL = url
                    let note = index == 0 ? "" : "（端口 \(ports.first ?? 0) 被占用，已改用 \(actual)）"
                    onStatus("已开启 · 手机与 Mac 需在同一局域网\(note)", url?.absoluteString)
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

    func stop() {
        observing = false
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
        guard CompanionHTTP.tokenMatches(request.query["t"], token) else {
            reply(connection, "403 Forbidden", "text/plain; charset=utf-8", Data("配对码无效或已过期，请重新扫码。".utf8))
            return
        }
        switch (request.method, request.path) {
        case ("GET", "/"):
            reply(connection, "200 OK", "text/html; charset=utf-8", Data(CompanionPage.html.utf8),
                  ["Content-Security-Policy: default-src 'self'; script-src 'unsafe-inline'; style-src 'unsafe-inline'"])
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
<header id="h"><span class="dot"></span><span id="s">连接中…</span><span id="mode"></span><span id="p"></span>
<span class="size"><button onclick="zoom(-2)">A-</button><button onclick="zoom(2)">A+</button></span></header>
<div id="q"></div><div id="a">等待回答…</div><div id="cand"></div><div id="sources" style="font-size:12px;color:var(--muted)"></div><button id="update" hidden onclick="act('applyWeb')">显示核查更新</button>
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
let es=null, retry=null, attempt=0, lastEvent=0, lastGood=0, server='', sequence=-1, polling=false, pendingPoll=false;
function connection(label,live=false){document.getElementById('s').textContent=label;document.getElementById('h').className=live?'live':''}
function apply(e,transport){
 if(!e||e.version!==1||!e.snapshot||typeof e.sequence!=='number')return;
 const d=e.snapshot;lastGood=Date.now();
 if(server===e.serverID&&e.sequence<sequence)return;
 server=e.serverID;sequence=e.sequence;connection(transport,true);
 document.getElementById('mode').textContent=d.status+(d.answering?' · 生成中':'');
 document.getElementById('p').textContent=d.position;document.getElementById('q').textContent=d.question;
 A.innerHTML=d.answer?md(d.answer):(d.answering?'正在生成…':'等待回答…');
 document.getElementById('cand').textContent=d.candidate?('候选：'+d.candidate):'';
 document.getElementById('sources').textContent=[d.slide,d.sources].filter(Boolean).join(' · ');
 document.getElementById('update').hidden=!d.updateAvailable;
}
async function request(path,options={}){
 const controller=new AbortController(),timeout=setTimeout(()=>controller.abort(),2500);
 try{const r=await fetch(path,{...options,cache:'no-store',signal:controller.signal});
 if(r.status===403){connection('配对已过期，请重新扫码');if(es)es.close();throw new Error('expired')}
 if(!r.ok)throw new Error('HTTP '+r.status);return r;
 }finally{clearTimeout(timeout)}
}
async function snapshot(){if(pendingPoll||document.hidden)return;pendingPoll=true;
 try{apply(await(await request('/snapshot?t='+encodeURIComponent(t))).json(),'备用连接 · 快照同步')}
 catch{if(Date.now()-lastGood>8000)connection('离线，正在恢复…')}finally{pendingPoll=false}}
function retrySSE(){if(retry)return;const delay=Math.min(8000,500*Math.pow(2,attempt++))*(.8+Math.random()*.4);
 retry=setTimeout(()=>{retry=null;connect()},delay)}
function connect(){if(es)es.close();es=new EventSource('/events?t='+encodeURIComponent(t));
 const received=e=>{try{lastEvent=Date.now();attempt=0;polling=false;apply(JSON.parse(e.data),'已连接 · 实时同步')}catch{}};
 es.onmessage=received;es.addEventListener('heartbeat',received);
 es.onerror=()=>{es.close();polling=true;connection('正在恢复连接…');snapshot();retrySSE()};}
async function act(a){const id=crypto.randomUUID?crypto.randomUUID():'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g,c=>{const r=crypto.getRandomValues(new Uint8Array(1))[0]&15;return(c==='x'?r:(r&3)|8).toString(16)});
 const url='/action?t='+encodeURIComponent(t)+'&a='+a+'&id='+id;
 for(let n=0;n<2;n++){try{await request(url,{method:'POST'});snapshot();return}catch{}}
 connection('操作尚未确认，请检查连接');}
setInterval(()=>{if(document.hidden)return;
 if(Date.now()-lastEvent>6000){polling=true;snapshot();if(es)es.close();retrySSE()}
 else if(polling)snapshot();if(Date.now()-lastGood>8000)connection('离线，正在恢复…')},1000);
setInterval(()=>{if(lastGood&&Date.now()-lastGood<8000)request('/ack?t='+encodeURIComponent(t)+'&seq='+sequence,{method:'POST'}).catch(()=>{})},5000);
function resume(){if(document.hidden)return;lastEvent=0;polling=true;snapshot();connect()}
document.addEventListener('visibilitychange',resume);window.addEventListener('online',resume);window.addEventListener('pageshow',resume);
snapshot();connect();
</script></body></html>
"""#
}
