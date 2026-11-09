import Foundation
import Testing
@testable import CompanionMobileCore

@Suite struct NativeTests {
    @Test @MainActor func pairingAndMonotonicState() throws {
        let p = CompanionPairing(lan: "http://127.0.0.1:8765", token: "local-token", ble: "uuid-1234")
        for scheme in ["nextthought-companion", "interviewhacker-companion"] {
            let link = try p.url(base: URL(string: "\(scheme)://pair")!).absoluteString
            #expect(try NativeCompanionClient.parse(link) == p)
        }
        #expect(throws: (any Error).self) { try NativeCompanionClient.parse("https://invalid.example") }
        let client = NativeCompanionClient()
        func e(_ id: String, _ seq: UInt64, _ question: String, _ answer: String) -> CompanionEnvelope {
            .init(serverID: id, sequence: seq, snapshot: .init(status: "test", question: question, answer: answer, answering: answer.isEmpty, candidate: "", position: ""))
        }
        client.apply(e("one", 5, "new", ""), via: "局域网")
        client.apply(e("one", 4, "old", "old answer"), via: "局域网")
        #expect(client.snapshot?.question == "new"); #expect(client.snapshot?.answer == "")
        client.apply(e("two", 1, "restart", "fresh"), via: "局域网")
        client.apply(e("one", 7, "retired", "wrong"), via: "局域网")
        #expect(client.snapshot?.question == "restart")
        client.stop(); #expect(client.snapshot == nil)
    }
    @Test func cryptoAndBleSharedBytes() throws {
        let p = CompanionPairing(room: "room-one", key: Data(repeating: 7, count: 32).base64EncodedString())
        let frame = try CompanionCrypto.seal("secret 可靠 -3.7 dBm", kind: "state", pairing: p, nonce: Data(repeating: 4, count: 12))
        #expect(frame.body == "4p2rT4wvtodvAZxobxrsTNxx8zyhDXC7TyxE1esTaHBCIhkoLHmlIw==")
        let bytes = try JSONEncoder().encode(frame)
        let pieces = CompanionBLEProtocol.fragments(bytes, id: 47, mtu: 20)
        var assembler = CompanionBLEProtocol.Reassembler(), output: Data?
        for packet in pieces { if let complete = assembler.receive(packet) { output = complete.data } }
        #expect(output == bytes)
    }
}
