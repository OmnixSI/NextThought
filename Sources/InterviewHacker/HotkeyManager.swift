import Carbon.HIToolbox
import Foundation
import InterviewHackerCore

enum HotkeyAction: UInt32, CaseIterable, Identifiable {
    case captureWindow = 1, captureRegion, toggleSession, regenerate, forceAnswer, togglePause, toggleTeleprompter, cancelAnswer
    var id: UInt32 { rawValue }
    var title: String {
        switch self {
        case .captureWindow: "窗口截图"
        case .captureRegion: "区域截图"
        case .toggleSession: "开始 / 停止辅助"
        case .regenerate: "重新生成上一题"
        case .forceAnswer: "立即回答最新转写"
        case .togglePause: "暂停 / 恢复聆听"
        case .toggleTeleprompter: "显示 / 隐藏提词器"
        case .cancelAnswer: "取消当前回答"
        }
    }
    var defaultBinding: ShortcutBinding {
        let keys: [UInt32] = [0, 1, 46, 15, 36, 35, 31, 53]
        return ShortcutBinding(keyCode: keys[Int(rawValue) - 1])
    }
}

final class HotkeyManager {
    private var handler: EventHandlerRef?
    private var hotkeys: [EventHotKeyRef] = []
    private var actions: [UInt32: () -> Void] = [:]

    func register(actions: [HotkeyAction: () -> Void], overrides: [String: ShortcutBinding] = [:]) -> [String] {
        unregister()
        self.actions = Dictionary(uniqueKeysWithValues: actions.map { ($0.key.rawValue, $0.value) })

        var eventType = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        let installStatus = InstallEventHandler(GetApplicationEventTarget(), hotkeyEventHandler, 1, &eventType, Unmanaged.passUnretained(self).toOpaque(), &handler)
        guard installStatus == noErr else { return ["无法安装快捷键监听器，错误码 \(installStatus)。"] }

        let bindings = Dictionary(uniqueKeysWithValues: HotkeyAction.allCases.map {
            (String($0.rawValue), overrides[String($0.rawValue)] ?? $0.defaultBinding)
        })
        if let error = ShortcutBinding.validate(bindings) { return [error] }
        var errors: [String] = []
        for action in HotkeyAction.allCases where actions[action] != nil {
            let binding = bindings[String(action.rawValue)] ?? action.defaultBinding
            let title = "\(binding.label) \(action.title)"
            var modifiers: UInt32 = 0
            if binding.command { modifiers |= UInt32(cmdKey) }
            if binding.shift { modifiers |= UInt32(shiftKey) }
            if binding.option { modifiers |= UInt32(optionKey) }
            if binding.control { modifiers |= UInt32(controlKey) }
            var reference: EventHotKeyRef?
            let identifier = EventHotKeyID(signature: OSType(0x49484B52), id: action.rawValue)
            let status = RegisterEventHotKey(binding.keyCode, modifiers, identifier, GetApplicationEventTarget(), 0, &reference)
            if status == noErr, let reference {
                hotkeys.append(reference)
            } else {
                errors.append("\(title) 注册失败，错误码 \(status)。可能与其他应用冲突。")
            }
        }
        return errors
    }

    deinit { unregister() }

    func unregister() {
        for hotkey in hotkeys {
            UnregisterEventHotKey(hotkey)
        }
        hotkeys.removeAll()
        if let handler {
            RemoveEventHandler(handler)
            self.handler = nil
        }
        actions.removeAll()
    }

    func perform(_ id: UInt32) {
        actions[id]?()
    }
}

private let hotkeyEventHandler: EventHandlerProcPtr = { _, event, userData in
    guard let event, let userData else { return OSStatus(eventNotHandledErr) }
    var identifier = EventHotKeyID()
    let status = GetEventParameter(
        event,
        EventParamName(kEventParamDirectObject),
        EventParamType(typeEventHotKeyID),
        nil,
        MemoryLayout<EventHotKeyID>.size,
        nil,
        &identifier)
    guard status == noErr else { return status }
    let manager = Unmanaged<HotkeyManager>.fromOpaque(userData).takeUnretainedValue()
    manager.perform(identifier.id)
    return noErr
}
