import CryptoKit
import Foundation

/// Same directional HKDF and AES-256-GCM format on Swift, Kotlin and the web.
public struct CompanionPairing: Codable, Sendable, Equatable {
    public var v = 1
    public var lan: String?
    public var token: String?
    public var relay: String?
    public var room: String
    public var subscriber: String?
    public var key: String
    public var ble: String?
    public init(lan: String? = nil, token: String? = nil, relay: String? = nil, room: String = UUID().uuidString,
                subscriber: String? = nil, key: String? = nil, ble: String? = nil) {
        self.lan = lan; self.token = token; self.relay = relay; self.room = room; self.subscriber = subscriber
        self.key = key ?? SymmetricKey(size: .bits256).withUnsafeBytes { Data($0).base64EncodedString() }; self.ble = ble
    }
    public func url(base: URL) throws -> URL {
        var c = URLComponents(url: base, resolvingAgainstBaseURL: false)!
        c.query = nil
        c.fragment = "p=" + (try JSONEncoder().encode(self)).base64EncodedString().replacingOccurrences(of: "+", with: "-").replacingOccurrences(of: "/", with: "_").replacingOccurrences(of: "=", with: "")
        return c.url!
    }
}
public struct CompanionCipherFrame: Codable, Sendable {
    public var v = 1
    public var kind: String
    public var n: String
    public var body: String
}
public enum CompanionCrypto {
    public enum Failure: Error { case invalidKey, invalidFrame }
    static func derived(_ pairing: CompanionPairing, kind: String) throws -> SymmetricKey {
        guard let data = Data(base64Encoded: pairing.key), data.count == 32 else { throw Failure.invalidKey }
        return HKDF<SHA256>.deriveKey(inputKeyMaterial: SymmetricKey(data: data), salt: Data(pairing.room.utf8),
            info: Data("ih-companion-v1/\(kind)".utf8), outputByteCount: 32)
    }
    public static func seal<T: Encodable>(_ value: T, kind: String, pairing: CompanionPairing, nonce: Data? = nil) throws -> CompanionCipherFrame {
        guard ["state", "command", "ack"].contains(kind) else { throw Failure.invalidFrame }
        let iv = try nonce.map { try AES.GCM.Nonce(data: $0) } ?? AES.GCM.Nonce()
        let plaintext = try JSONEncoder().encode(value)
        guard plaintext.count <= 256 * 1024 else { throw Failure.invalidFrame }
        let box = try AES.GCM.seal(plaintext, using: derived(pairing, kind: kind), nonce: iv, authenticating: Data("\(pairing.room)/\(kind)".utf8))
        return .init(kind: kind, n: iv.withUnsafeBytes { Data($0).base64EncodedString() }, body: (box.ciphertext + box.tag).base64EncodedString())
    }
    public static func open<T: Decodable>(_ frame: CompanionCipherFrame, as: T.Type, pairing: CompanionPairing) throws -> T {
        guard frame.v == 1, ["state", "command", "ack"].contains(frame.kind), frame.body.count <= 350000,
              let nonce = Data(base64Encoded: frame.n), nonce.count == 12,
              let body = Data(base64Encoded: frame.body), body.count >= 16 else { throw Failure.invalidFrame }
        let box = try AES.GCM.SealedBox(nonce: AES.GCM.Nonce(data: nonce), ciphertext: body.dropLast(16), tag: body.suffix(16))
        let data = try AES.GCM.open(box, using: derived(pairing, kind: frame.kind), authenticating: Data("\(pairing.room)/\(frame.kind)".utf8))
        return try JSONDecoder().decode(T.self, from: data)
    }
}
public struct CompanionCommand: Codable, Sendable {
    public var id: String
    public var action: String
    public var createdAt: Double
    public init(id: String = UUID().uuidString, action: String, now: Date = Date()) {
        self.id = id; self.action = action; createdAt = now.timeIntervalSince1970 * 1000
    }
    public var isFresh: Bool { abs(Date().timeIntervalSince1970 * 1000 - createdAt) < 30000 }
}
public struct CompanionAcknowledgement: Codable, Sendable {
    public var id: String
    public init(id: String) { self.id = id }
}
