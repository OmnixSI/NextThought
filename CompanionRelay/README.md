# Optional encrypted companion relay

Build from the repository root with `npm ci --prefix CompanionRelay && npm test --prefix CompanionRelay`.
Run locally with `npm start --prefix CompanionRelay` (binds only 127.0.0.1:8787).
Deploy with `docker compose -f CompanionRelay/compose.yaml up -d --build`, behind an HTTPS/WSS reverse proxy you control. Example Caddy configuration:

```caddy
relay.example.com {
  reverse_proxy 127.0.0.1:8787
}
```

Configure that HTTPS origin in Mac Settings → 手机伴侣. Use a dedicated origin at `/`; path prefixes are unsupported. TLS terminates at your proxy. Enable WebSocket upgrades and keep idle timeouts above 30 seconds. Disable request URL logging; pairing secrets live in fragments, socket credentials are in the first packet, and no content logging is implemented. No public endpoint is configured or deployed by this change.

Rooms expire after 8 hours or when the Mac closes them. Restarting the service discards every room; re-enable the relay and pair again. Limits: 100 rooms, 6 sockets per room, 384 KiB WebSocket payload, 25 packets/second/socket, bounded slow peers, join deadline, ping/pong liveness, HTTP creation limits. The relay retains one current encrypted full state per room in RAM. Commands and acknowledgements are forwarded without persistence. It has no document, transcript, image, account or analytics API. Configure network limits at the reverse proxy for an internet-facing deployment.

Content encryption: AES-256-GCM with random 96-bit nonces; 32-byte random pairing PSK; HKDF-SHA256 salt=room ID and info=`ih-companion-v1/<kind>`; AAD=`<room>/<kind>`. Three keys distinguish `state`, `command`, `ack`. Publisher/subscriber tokens are independent random 256-bit transport credentials. The server never receives the content key. Swift CryptoKit and audited noble libraries share fixed test vectors. The relay can see traffic timing, sizes, peer addresses and availability; a malicious relay can withhold/replay packets but cannot forge encrypted content. Clients reject decreasing state sequences and retired server IDs. Command UUIDs are deduplicated across transports on the Mac.

Pair again after enabling/disabling relay or rotating LAN links. The HTTPS bookmark uses only WSS and never accesses LAN HTTP; the LAN page connects both channels and changes display to relay when LAN stalls. Both pages use the same bundled, vendored browser code and bounded command retry IDs. A closed or unloaded LAN page cannot reopen a dead local address; use the HTTPS bookmark.

Validation: Node real socket encryption/reconnect/auth/expiry tests, native Swift interoperability tests, a real isolated Mac URLSession → Node → subscriber command/ack test and in-app browser rendering/control QA passed. Physical Safari/Chrome network switching, public TLS deployment, prolonged use and independent security review remain release gates. This is a draft implementation, not a claim of audited end-to-end application security.

Rollback: disable relay, stop container, and revert this stage. LAN snapshot/SSE protocol is unchanged; no persistent user data migration occurs. The compiled browser asset is committed for normal Mac builds; edit source under `CompanionRelay/src`, rebuild, and commit both source and bundle.
