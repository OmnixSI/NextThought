import Foundation

public struct ShortcutBinding: Codable, Sendable, Equatable {
    public var keyCode: UInt32
    public var command: Bool
    public var shift: Bool
    public var option: Bool
    public var control: Bool
    public init(keyCode: UInt32, command: Bool = true, shift: Bool = true, option: Bool = false, control: Bool = false) {
        self.keyCode = keyCode; self.command = command; self.shift = shift; self.option = option; self.control = control
    }
    public var isSafe: Bool { keyCode <= 127 && (command || option || control) }
    public var label: String {
        let names: [UInt32: String] = [0:"A", 1:"S", 8:"C", 15:"R", 31:"O", 35:"P", 36:"↩", 40:"K", 46:"M", 53:"Esc"]
        return (control ? "⌃" : "") + (option ? "⌥" : "") + (shift ? "⇧" : "") + (command ? "⌘" : "") + (names[keyCode] ?? "Key \(keyCode)")
    }
    public static func validate(_ bindings: [String: ShortcutBinding]) -> String? {
        guard bindings.values.allSatisfy(\.isSafe) else { return "快捷键必须包含 Command、Option 或 Control，不能只使用普通按键。" }
        var seen: [ShortcutBinding] = []
        for binding in bindings.values {
            if seen.contains(binding) { return "两个动作不能使用相同的快捷键。" }
            seen.append(binding)
        }
        return nil
    }
}
