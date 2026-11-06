import CoreBluetooth
import Foundation
import NextThoughtCore

/// Mac peripheral. Notification flow control never blocks the main actor.
/// Text/state/actions only; all complete messages authenticate with the QR PSK.
@MainActor final class CompanionBluetooth: NSObject, @preconcurrency CBPeripheralManagerDelegate {
    private final class Peer {
        let central: CBCentral
        var receive = CompanionBLEProtocol.Reassembler()
        var outbox = CompanionBLEOutbox()
        let created = Date()
        var authenticated = false
        init(_ central: CBCentral) { self.central = central }
    }
    private var manager: CBPeripheralManager?
    private var stateCharacteristic: CBMutableCharacteristic?
    private var pairing: CompanionPairing?
    private var peers: [UUID: Peer] = [:]
    private var ticker: Task<Void, Never>?
    private var latest: CompanionEnvelope?
    var status: (String) -> Void = { _ in }
    func start(pairing: CompanionPairing) {
        stop(); self.pairing = pairing
        manager = CBPeripheralManager(delegate: self, queue: .main)
        ticker = Task { [weak self] in
            while !Task.isCancelled {
                guard let self else { return }
                self.peers = self.peers.filter { $0.value.authenticated || Date().timeIntervalSince($0.value.created) < 15 }
                for peer in self.peers.values where peer.authenticated {
                    self.flush(peer)
                }
                try? await Task.sleep(for: .milliseconds(200))
            }
        }
    }
    func stop() {
        ticker?.cancel(); ticker = nil
        manager?.stopAdvertising(); manager?.removeAllServices(); manager?.delegate = nil; manager = nil
        stateCharacteristic = nil; peers = [:]; pairing = nil; latest = nil
    }
    func publish(_ envelope: CompanionEnvelope) {
        latest = envelope
        guard let bytes = frame(envelope, kind: "state") else { return }
        for peer in peers.values where peer.authenticated { peer.outbox.enqueueState(bytes); flush(peer) }
    }
    private func frame(_ value: some Encodable, kind: String) -> Data? {
        guard let pairing else { return nil }
        return try? JSONEncoder().encode(CompanionCrypto.seal(value, kind: kind, pairing: pairing))
    }
    private func flush(_ peer: Peer) {
        guard peer.authenticated, let manager, let stateCharacteristic else { return }
        let failures = peer.outbox.failureCount
        while let packet = peer.outbox.peek(mtu: peer.central.maximumUpdateValueLength) {
            guard manager.updateValue(packet, for: stateCharacteristic, onSubscribedCentrals: [peer.central]) else { return }
            peer.outbox.didSend()
        }
        if peer.outbox.failureCount > failures { status("蓝牙等待恢复，保留最新状态") }
    }
    func peripheralManagerDidUpdateState(_ peripheral: CBPeripheralManager) {
        guard peripheral === manager else { return }
        guard peripheral.state == .poweredOn else { status(peripheral.state == .unauthorized ? "蓝牙未获授权" : "蓝牙不可用，请检查开关"); return }
        let state = CBMutableCharacteristic(type: CBUUID(string: CompanionBLEProtocol.state), properties: [.notify], value: nil, permissions: [])
        let command = CBMutableCharacteristic(type: CBUUID(string: CompanionBLEProtocol.command), properties: [.write], value: nil, permissions: [.writeable])
        let service = CBMutableService(type: CBUUID(string: CompanionBLEProtocol.service), primary: true)
        service.characteristics = [state, command]; stateCharacteristic = state; peripheral.add(service)
    }
    func peripheralManager(_ peripheral: CBPeripheralManager, didAdd service: CBService, error: Error?) {
        guard peripheral === manager else { return }
        guard error == nil else { status("蓝牙服务启动失败"); return }
        peripheral.startAdvertising([CBAdvertisementDataServiceUUIDsKey: [service.uuid], CBAdvertisementDataLocalNameKey: "NT-" + String((pairing?.ble ?? "").prefix(8))])
        status("蓝牙备用已开启，等待配套 App")
    }
    func peripheralManager(_ peripheral: CBPeripheralManager, central: CBCentral, didSubscribeTo characteristic: CBCharacteristic) {
        guard peripheral === manager, peers.count < 4 else { return }
        peers[central.identifier] = Peer(central)
    }
    func peripheralManager(_ peripheral: CBPeripheralManager, central: CBCentral, didUnsubscribeFrom characteristic: CBCharacteristic) { peers[central.identifier] = nil }
    func peripheralManagerIsReady(toUpdateSubscribers peripheral: CBPeripheralManager) { for peer in peers.values { flush(peer) } }
    func peripheralManager(_ peripheral: CBPeripheralManager, didReceiveWrite requests: [CBATTRequest]) {
        guard peripheral === manager else { return }
        for request in requests {
            guard request.offset == 0, let value = request.value, let peer = peers[request.central.identifier] else { peripheral.respond(to: request, withResult: .unlikelyError); continue }
            peripheral.respond(to: request, withResult: .success)
            guard let assembled = peer.receive.receive(value), let pairing,
                  let encrypted = try? JSONDecoder().decode(CompanionCipherFrame.self, from: assembled.data) else { continue }
            if encrypted.kind == "ack", let ack = try? CompanionCrypto.open(encrypted, as: CompanionAcknowledgement.self, pairing: pairing), ack.id.hasPrefix("ble:"), let id = UInt32(ack.id.dropFirst(4)) {
                peer.outbox.acknowledge(id); flush(peer); continue
            }
            guard encrypted.kind == "command", let command = try? CompanionCrypto.open(encrypted, as: CompanionCommand.self, pairing: pairing), command.isFresh else { continue }
            peer.authenticated = true
            if command.action == "resync", let bytes = frame(latest ?? CompanionServer.shared.currentEnvelope(), kind: "state") { peer.outbox.enqueueState(bytes) }
            else if !CompanionServer.shared.performCommand(command.action, id: command.id) { continue }
            if let ack = frame(CompanionAcknowledgement(id: command.id), kind: "ack") { peer.outbox.enqueueAcknowledgement(ack) }
            status("蓝牙备用已连接"); flush(peer)
        }
    }
}
