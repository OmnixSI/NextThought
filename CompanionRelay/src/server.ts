import {createServer} from 'node:http';
import {randomBytes, timingSafeEqual} from 'node:crypto';
import {readFile} from 'node:fs/promises';
import {WebSocketServer, WebSocket} from 'ws';

// No body logging, database, document API, or content decryption on this process.
type Room = {publisher: string; subscriber: string; expires: number; latest?: string; sockets: Map<WebSocket, string>};
export function createRelay(options: {ttl?: number; capacity?: number; assets?: URL} = {}) {
  const rooms = new Map<string, Room>(), limits = new Map<string, {at: number; count: number}>();
  const ttl = options.ttl ?? 8 * 3600_000, capacity = options.capacity ?? 100;
  const assets = options.assets ?? new URL('../../Resources/Companion/', import.meta.url);
  const secret = () => randomBytes(32).toString('base64url');
  const matches = (a: unknown, b: string) => typeof a === 'string' && Buffer.byteLength(a) === Buffer.byteLength(b) && timingSafeEqual(Buffer.from(a), Buffer.from(b));
  const destroy = (id: string, room: Room) => { for (const ws of room.sockets.keys()) ws.close(4001, 'room expired'); rooms.delete(id); };
  const server = createServer(async (req, res) => {
    res.setHeader('Cache-Control', 'no-store'); res.setHeader('Referrer-Policy', 'no-referrer');
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self'; style-src 'unsafe-inline'; connect-src 'self' wss:; frame-ancestors 'none'; base-uri 'none'");
    const path = req.url?.split('?')[0];
    if (req.method === 'GET' && (path === '/' || path === '/phone.js')) {
      try { res.setHeader('Content-Type', path === '/' ? 'text/html; charset=utf-8' : 'text/javascript; charset=utf-8'); res.end(await readFile(new URL(path === '/' ? 'index.html' : 'phone.js', assets))); }
      catch { res.writeHead(503); res.end(); } return;
    }
    if (req.method === 'GET' && path === '/health') { res.end('ok'); return; }
    if (req.method === 'POST' && path === '/rooms') {
      if (limits.size >= 10000 || rooms.size >= capacity) { res.writeHead(429); res.end(); return; }
      const ip = req.socket.remoteAddress ?? '', now = Date.now();
      let rate = limits.get(ip); if (!rate || now - rate.at > 60000) { rate = {at: now, count: 0}; limits.set(ip, rate); }
      if (++rate.count > 5 || rooms.size >= capacity) { res.writeHead(429); res.end(); return; }
      const room = {publisher: secret(), subscriber: secret(), expires: now + ttl, sockets: new Map<WebSocket, string>()};
      const id = secret(); rooms.set(id, room); res.setHeader('Content-Type', 'application/json');
      res.end(JSON.stringify({room: id, publisher: room.publisher, subscriber: room.subscriber, expires: room.expires})); return;
    }
    const id = path?.match(/^\/rooms\/([A-Za-z0-9_-]+)\/close$/)?.[1], room = id && rooms.get(id);
    if (req.method === 'POST' && id && room && matches(req.headers.authorization, 'Bearer ' + room.publisher)) { destroy(id, room); res.writeHead(204); res.end(); return; }
    res.writeHead(404); res.end();
  });
  server.headersTimeout = 5000; server.requestTimeout = 10000; server.keepAliveTimeout = 5000; server.maxHeadersCount = 32;
  const wss = new WebSocketServer({noServer: true, maxPayload: 384 * 1024, perMessageDeflate: false});
  server.on('upgrade', (req, socket, head) => {
    if (req.url !== '/socket' || wss.clients.size >= capacity * 6) { socket.destroy(); return; }
    wss.handleUpgrade(req, socket, head, ws => wss.emit('connection', ws, req));
  });
  wss.on('connection', ws => {
    let joined: Room | undefined, role = '', rateAt = Date.now(), count = 0, alive = true;
    const deadline = setTimeout(() => { if (!joined) ws.close(4003, 'join required'); }, 5000);
    ws.on('pong', () => { alive = true; });
    const heartbeat = setInterval(() => { if (!alive) { ws.terminate(); return; } alive = false; ws.ping(); }, 15000);
    ws.on('close', () => { clearTimeout(deadline); clearInterval(heartbeat); joined?.sockets.delete(ws); });
    ws.on('error', () => ws.terminate());
    ws.on('message', raw => {
      if (Date.now() - rateAt >= 1000) { rateAt = Date.now(); count = 0; }
      if (++count > 25) { ws.close(4008, 'rate limit'); return; }
      let packet: any; try { packet = JSON.parse(raw.toString()); } catch { ws.close(4002); return; }
      if (!joined) {
        const candidate = rooms.get(packet.room);
        if (!candidate || candidate.expires < Date.now() || !['publisher', 'subscriber'].includes(packet.role) ||
            !matches(packet.token, packet.role === 'publisher' ? candidate.publisher : candidate.subscriber) || candidate.sockets.size >= 6) { ws.close(4003); return; }
        joined = candidate; role = packet.role;
        if (role === 'publisher') for (const [peer, r] of joined.sockets) if (r === role) peer.close(4000, 'publisher replaced');
        joined.sockets.set(ws, role); clearTimeout(deadline);
        ws.send(JSON.stringify({joined: true}));
        if (role === 'subscriber' && joined.latest) ws.send(joined.latest);
        return;
      }
      // Whitelist the opaque protocol; reject extra fields and oversized payloads.
      if (packet.v !== 1 || !['state','command','ack'].includes(packet.kind) ||
          Object.keys(packet).some(k => !['v','kind','n','body'].includes(k)) ||
          typeof packet.n !== 'string' || !/^[A-Za-z0-9+/]{16}$/.test(packet.n) ||
          typeof packet.body !== 'string' || packet.body.length > 350000 || !/^[A-Za-z0-9+/]+={0,2}$/.test(packet.body) ||
          (role === 'publisher' ? packet.kind === 'command' : packet.kind !== 'command')) { ws.close(4002); return; }
      const frame = JSON.stringify(packet);
      if (packet.kind === 'state') joined.latest = frame;
      for (const [peer, r] of joined.sockets) if (r !== role && peer.readyState === WebSocket.OPEN) {
        if (peer.bufferedAmount > 512 * 1024) peer.terminate(); else peer.send(frame);
      }
    });
  });
  const cleanup = setInterval(() => {
    for (const [id, room] of rooms) if (room.expires < Date.now()) destroy(id, room);
    for (const [ip, rate] of limits) if (Date.now() - rate.at > 60000) limits.delete(ip);
  }, Math.min(30000, ttl));
  server.on('close', () => { clearInterval(cleanup); for (const ws of wss.clients) ws.terminate(); wss.close(); });
  return {server, rooms}; // exposed only to local tests, never HTTP
}
if (process.argv[1]?.endsWith('/server.js')) {
  const {server} = createRelay(); server.listen(Number(process.env.PORT ?? 8787), process.env.HOST ?? '127.0.0.1');
}
