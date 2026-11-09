import Carbon.HIToolbox
import Foundation
import NextThoughtCore

enum HotkeyAction: UInt32, CaseIterable, Identifiable {
    case captureWindow = 1, captureRegion, toggleSession, regenerate, forceAnswer, togglePause, toggleTeleprompter, cancelAnswer
    case copyAnswer, toggleClickThrough, panicHide, previousAnswer, nextAnswer, refineShorter
    /// Explicit raw value: a stored shortcut is keyed by it, so it must not shift when other actions are added.
    case toggleLiveWindow = 40
    var id: UInt32 { rawValue }
    var title: String {
        switch self {
        case .captureWindow: "窗口截图"
        case .captureRegion: "区域截图"
        case .toggleSession: "开始 / 停止记录"
        case .regenerate: "重新生成上一题"
        case .forceAnswer: "立即回答最新转写"
        case .togglePause: "暂停 / 恢复聆听"
        case .toggleTeleprompter: "显示 / 隐藏回答浮窗"
        case .cancelAnswer: "取消当前回答"
        case .copyAnswer: "复制当前回答"
        case .toggleClickThrough: "回答浮窗鼠标穿透"
        case .panicHide: "隐藏 / 恢复所有窗口"
        case .previousAnswer: "上一条回答"
        case .nextAnswer: "下一条回答"
        case .refineShorter: "把回答改短"
        case .toggleLiveWindow: "切换会中窗口（纪要 / 字幕条 / 隐藏）"
        }
    }
    var defaultBinding: ShortcutBinding {
        switch self {
        case .captureWindow: return ShortcutBinding(keyCode: 0)
        case .captureRegion: return ShortcutBinding(keyCode: 1)
        case .toggleSession: return ShortcutBinding(keyCode: 46)
        case .regenerate: return ShortcutBinding(keyCode: 15)
        case .forceAnswer: return ShortcutBinding(keyCode: 36)
        case .togglePause: return ShortcutBinding(keyCode: 35)
        case .toggleTeleprompter: return ShortcutBinding(keyCode: 31)
        case .cancelAnswer: return ShortcutBinding(keyCode: 53)
        case .copyAnswer: return ShortcutBinding(keyCode: 16)
        case .toggleClickThrough: return ShortcutBinding(keyCode: 40)
        case .panicHide: return ShortcutBinding(keyCode: 42)
        // ⌃⌘←/→ avoid the ⌘⇧[ ] tab-switching shortcuts of browsers and editors.
        case .previousAnswer: return ShortcutBinding(keyCode: 123, command: true, shift: false, control: true)
        case .nextAnswer: return ShortcutBinding(keyCode: 124, command: true, shift: false, control: true)
        case .refineShorter: return ShortcutBinding(keyCode: 18)
        // ⌃⌥S: no ⌘, so it cannot collide with the ⌘⇧ defaults above.
        case .toggleLiveWindow: return ShortcutBinding(keyCode: 1, command: false, shift: false, option: true, control: true)
        }
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
