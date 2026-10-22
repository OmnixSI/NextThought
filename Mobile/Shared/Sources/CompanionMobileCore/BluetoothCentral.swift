import CoreBluetooth
import Foundation

@MainActor public final class BluetoothCentral: NSObject, @preconcurrency CBCentralManagerDelegate, @preconcurrency CBPeripheralDelegate {
    private var manager: CBCentralManager?
    private var peripheral: CBPeripheral?
    private var writeCharacteristic: CBCharacteristic?
    private var queue: [Data] = []
    private var writing = false
    private var receiver = CompanionBLEProtocol.Reassembler()
    private var pairing: CompanionPairing?
    private var nextID = UInt32.random(in: 1...UInt32.max)
    private var reconnect: Task<Void, Never>?
    public var onFrame: (CompanionCipherFrame) -> Void = { _ in }
    public var onReady: () -> Void = {}
    public var onStatus: (String) -> Void = { _ in }
    public private(set) var ready = false
    public func start(_ pairing: CompanionPairing) {
        stop(); self.pairing = pairing
        manager = CBCentralManager(delegate: self, queue: .main)
    }
    public func stop() {
        reconnect?.cancel(); reconnect = nil
        manager?.stopScan()
        if let peripheral { manager?.cancelPeripheralConnection(peripheral) }
        manager?.delegate = nil; manager = nil; peripheral?.delegate = nil; peripheral = nil
        writeCharacteristic = nil; queue = []; writing = false; ready = false; pairing = nil
        receiver = .init()
    }
    public func send(_ frame: CompanionCipherFrame) {
        guard ready, let peripheral, let data = try? JSONEncoder().encode(frame), queue.count < 32768 else { return }
        nextID &+= 1
        let packets = CompanionBLEProtocol.fragments(data, id: nextID, mtu: peripheral.maximumWriteValueLength(for: .withResponse))
        queue.append(contentsOf: packets); flush()
    }
    private func flush() {
        guard ready, !writing, let peripheral, let writeCharacteristic, let packet = queue.first else { return }
        writing = true; peripheral.writeValue(packet, for: writeCharacteristic, type: .withResponse)
    }
    private func scan() {
        guard manager?.state == .poweredOn, pairing != nil else { return }
        manager?.scanForPeripherals(withServices: [CBUUID(string: CompanionBLEProtocol.service)], options: [CBCentralManagerScanOptionAllowDuplicatesKey: false])
        onStatus("正在寻找 Mac 蓝牙备用")
    }
    public func centralManagerDidUpdateState(_ central: CBCentralManager) {
        guard central === manager else { return }
        if central.state == .poweredOn { scan() }
        else { ready = false; onStatus(central.state == .unauthorized ? "蓝牙未授权" : "蓝牙未开启") }
    }
    public func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral, advertisementData: [String: Any], rssi RSSI: NSNumber) {
        guard central === manager, self.peripheral == nil, let ble = pairing?.ble else { return }
        let name = advertisementData[CBAdvertisementDataLocalNameKey] as? String ?? peripheral.name ?? ""
        guard name == "IH-" + String(ble.prefix(8)) else { return }
        self.peripheral = peripheral; peripheral.delegate = self; central.stopScan(); central.connect(peripheral)
        reconnect?.cancel()
        reconnect = Task { [weak self, weak peripheral] in
            try? await Task.sleep(for: .seconds(10))
            if !Task.isCancelled, let self, let peripheral, !self.ready { self.manager?.cancelPeripheralConnection(peripheral) }
        }
    }
    public func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral) { guard central === manager else { return }; peripheral.discoverServices([CBUUID(string: CompanionBLEProtocol.service)]) }
    public func centralManager(_ central: CBCentralManager, didFailToConnect peripheral: CBPeripheral, error: Error?) { if central === manager { disconnected() } }
    public func centralManager(_ central: CBCentralManager, didDisconnectPeripheral peripheral: CBPeripheral, error: Error?) { if central === manager { disconnected() } }
    private func disconnected() {
        peripheral?.delegate = nil; peripheral = nil; ready = false; writeCharacteristic = nil; queue = []; writing = false; receiver = .init()
        onStatus("蓝牙正在恢复")
        reconnect?.cancel(); reconnect = Task { [weak self] in try? await Task.sleep(for: .seconds(1)); if !Task.isCancelled { self?.scan() } }
    }
    public func peripheral(_ peripheral: CBPeripheral, didDiscoverServices error: Error?) {
        guard peripheral === self.peripheral, error == nil, let service = peripheral.services?.first else { manager?.cancelPeripheralConnection(peripheral); return }
        peripheral.discoverCharacteristics([CBUUID(string: CompanionBLEProtocol.state), CBUUID(string: CompanionBLEProtocol.command)], for: service)
    }
    public func peripheral(_ peripheral: CBPeripheral, didDiscoverCharacteristicsFor service: CBService, error: Error?) {
        guard peripheral === self.peripheral else { return }
        for c in service.characteristics ?? [] {
            if c.uuid == CBUUID(string: CompanionBLEProtocol.command) { writeCharacteristic = c }
            if c.uuid == CBUUID(string: CompanionBLEProtocol.state) { peripheral.setNotifyValue(true, for: c) }
        }
    }
    public func peripheral(_ peripheral: CBPeripheral, didUpdateNotificationStateFor characteristic: CBCharacteristic, error: Error?) {
        guard peripheral === self.peripheral, error == nil, characteristic.isNotifying, writeCharacteristic != nil else { manager?.cancelPeripheralConnection(peripheral); return }
        ready = true; reconnect?.cancel(); onReady(); flush()
    }
    public func peripheral(_ peripheral: CBPeripheral, didWriteValueFor characteristic: CBCharacteristic, error: Error?) {
        guard peripheral === self.peripheral else { return }
        guard error == nil else { manager?.cancelPeripheralConnection(peripheral); return }
        if !queue.isEmpty { queue.removeFirst() }; writing = false; flush()
    }
    public func peripheral(_ peripheral: CBPeripheral, didUpdateValueFor characteristic: CBCharacteristic, error: Error?) {
        guard peripheral === self.peripheral, error == nil, let data = characteristic.value, let result = receiver.receive(data), let pairing,
              let frame = try? JSONDecoder().decode(CompanionCipherFrame.self, from: result.data) else { return }
        // Authenticate before acknowledging a full frame.
        guard (try? CompanionCrypto.open(frame, as: BLEJSON.self, pairing: pairing)) != nil else { onStatus("蓝牙配对校验失败"); return }
        if let ack = try? CompanionCrypto.seal(CompanionAcknowledgement(id: "ble:\(result.id)"), kind: "ack", pairing: pairing) { send(ack) }
        onFrame(frame)
    }
    private struct BLEJSON: Decodable {} // accepts an authenticated JSON object without retaining it
}
