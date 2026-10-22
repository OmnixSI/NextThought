import Foundation
import Testing
@testable import InterviewHackerCore

@Suite struct CompanionCryptoTests {
    @Test func crossPlatformVectorAndAuthentication() throws {
        let p = CompanionPairing(room: "room-one", key: Data(repeating: 7, count: 32).base64EncodedString())
        let value = "secret 可靠 -3.7 dBm"
        let frame = try CompanionCrypto.seal(value, kind: "state", pairing: p, nonce: Data(repeating: 4, count: 12))
        #expect(frame.n == "BAQEBAQEBAQEBAQE")
        #expect(frame.body == "4p2rT4wvtodvAZxobxrsTNxx8zyhDXC7TyxE1esTaHBCIhkoLHmlIw==")
        #expect(try CompanionCrypto.open(frame, as: String.self, pairing: p) == value)
        var wrong = frame; wrong.kind = "command"
        #expect(throws: (any Error).self) { try CompanionCrypto.open(wrong, as: String.self, pairing: p) }
        var changed = p; changed.room = "wrong"
        #expect(throws: (any Error).self) { try CompanionCrypto.open(frame, as: String.self, pairing: changed) }
    }
    @Test func pairingSecretOnlyInFragmentAndPayloadBound() throws {
        let p = CompanionPairing(lan: "http://192.168.1.2:8765", token: "token", key: Data(repeating: 5, count: 32).base64EncodedString())
        let url = try p.url(base: URL(string: "https://relay.example/?old=secret")!)
        #expect(url.query == nil); #expect(url.fragment?.hasPrefix("p=") == true)
        #expect(throws: (any Error).self) { try CompanionCrypto.seal(String(repeating: "x", count: 300000), kind: "state", pairing: p) }
    }
}
