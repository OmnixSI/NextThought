import AppKit
import CoreGraphics
import Foundation
import NextThoughtCore

/// Where the sound comes from (online / in the room) and the phone's connection, in
/// the words the interface uses.
@MainActor
extension AppState {
    // MARK: Online / offline

    func setMeetingVenue(_ venue: MeetingVenue) {
        guard venue != meetingVenue, !isRunning else { return }
        meetingVenue = venue
        UserDefaults.standard.set(venue.rawValue, forKey: "meetingVenue")
    }

    /// The meeting in progress (or about to start) happens in one room.
    var isOfflineMeeting: Bool { meetingVenue == .offline }

    /// The microphone is recognized: when switched on, and always in the room.
    var usesMicrophone: Bool {
        RealtimeSetupReadiness.microphoneRequired(microphoneEnabled: microphoneEnabled, isOfflineMeeting: isOfflineMeeting)
    }

    // MARK: Capture target

    /// "电脑声音" for a whole display (all system audio, this app excluded);
    /// the window's own title otherwise.
    func audioSourceTitle(for targetID: String) -> String {
        if targetID.hasPrefix("display:") { return String(localized: "电脑声音") }
        if let target = captureTargets.first(where: { $0.id == targetID }) { return target.title }
        return String(localized: "选择声音来源")
    }

    func selectCaptureTarget(_ id: String) {
        guard id != selectedTargetID else { return }
        selectedTargetID = id
        Task { await changeCaptureTarget() }
    }

    /// The main display's target: the default source for online meetings.
    var mainDisplayTargetID: String? {
        let main = "display:\(CGMainDisplayID())"
        if captureTargets.contains(where: { $0.id == main }) { return main }
        return captureTargets.first { $0.id.hasPrefix("display:") }?.id
    }

    // MARK: Phone companion

    /// Relay and Bluetooth report their state as text; this word means connected.
    static let connectedMarker = "已连接" // l10n-ignore: matches CompanionRelay / CompanionBluetooth status text

    /// At least one way to the phone is open: a phone page streaming over the LAN,
    /// the encrypted relay, or Bluetooth.
    var companionConnected: Bool {
        companionEnabled && (companionClientCount > 0
            || (companionRelayEnabled && companionRelayStatus.contains(Self.connectedMarker))
            || (companionBluetoothEnabled && companionBluetoothStatus.contains(Self.connectedMarker)))
    }

    /// "已连接 1 台手机 · 局域网" / "手机已断开 · 正在重连" / "打开手机上的页面扫码连接".
    var companionConnectionText: String {
        guard companionEnabled else { return "" }
        if companionClientCount > 0 {
            return String(localized: "已连接 \(companionClientCount) 台手机 · 局域网")
        }
        if companionRelayEnabled && companionRelayStatus.contains(Self.connectedMarker) { return String(localized: "已连接手机 · 加密中转") }
        if companionBluetoothEnabled && companionBluetoothStatus.contains(Self.connectedMarker) { return String(localized: "已连接手机 · 蓝牙") }
        if companionHadClient { return String(localized: "手机已断开 · 正在重连") }
        return String(localized: "打开手机上的页面扫码连接")
    }
}
