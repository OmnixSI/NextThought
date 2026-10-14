import Carbon.HIToolbox
import Foundation

enum HotkeyAction: UInt32 {
    case captureWindow = 1
    case captureRegion = 2
    case toggleSession = 3
    case regenerate = 4
}

final class HotkeyManager {
    private var handler: EventHandlerRef?
    private var hotkeys: [EventHotKeyRef] = []
    private var actions: [UInt32: () -> Void] = [:]

    func register(actions: [HotkeyAction: () -> Void]) -> [String] {
        unregister()
        self.actions = Dictionary(uniqueKeysWithValues: actions.map { ($0.key.rawValue, $0.value) })

        var eventType = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        let installStatus = InstallEventHandler(GetApplicationEventTarget(), hotkeyEventHandler, 1, &eventType, Unmanaged.passUnretained(self).toOpaque(), &handler)
        guard installStatus == noErr else { return ["无法安装快捷键监听器，错误码 \(installStatus)。"] }

        let bindings: [(HotkeyAction, Int, String)] = [
            (.captureWindow, kVK_ANSI_A, "⌘⇧A 截取窗口"),
            (.captureRegion, kVK_ANSI_S, "⌘⇧S 区域截图"),
            (.toggleSession, kVK_ANSI_M, "⌘⇧M 开始或停止"),
            (.regenerate, kVK_ANSI_R, "⌘⇧R 重新生成")
        ]
        var errors: [String] = []
        for (action, keyCode, title) in bindings {
            var reference: EventHotKeyRef?
            let identifier = EventHotKeyID(signature: OSType(0x49484B52), id: action.rawValue)
            let status = RegisterEventHotKey(UInt32(keyCode), UInt32(cmdKey | shiftKey), identifier, GetApplicationEventTarget(), 0, &reference)
            if status == noErr, let reference {
                hotkeys.append(reference)
            } else {
                errors.append("\(title) 注册失败，错误码 \(status)。可能与其他应用冲突。")
            }
        }
        return errors
    }

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
