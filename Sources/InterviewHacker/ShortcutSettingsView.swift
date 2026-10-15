import AppKit
import SwiftUI
import InterviewHackerCore

struct ShortcutSettingsView: View {
    @Environment(AppState.self) private var state
    @State private var recording: HotkeyAction?
    var body: some View {
        VStack(spacing: 10) {
            ForEach(HotkeyAction.allCases) { action in
                HStack {
                    Text(action.title)
                    Spacer()
                    Button(state.shortcutLabel(action)) { recording = action }.font(.system(.callout, design: .monospaced))
                }
            }
            Button("恢复默认快捷键") { state.resetShortcuts() }
        }
        .sheet(item: $recording, onDismiss: { state.registerHotkeys() }) { action in
            VStack(spacing: 16) {
                Text("设置快捷键：\(action.title)").font(.headline)
                Text("按下组合键；至少包含 Command、Option 或 Control。Esc 取消。")
                    .font(.callout).foregroundStyle(.secondary)
                KeyRecorder { binding in
                    if let binding { state.setShortcut(action, binding: binding) }
                    recording = nil
                }.frame(height: 60)
                Button("取消") { recording = nil }.keyboardShortcut(.cancelAction)
            }.padding(24).frame(width: 440).onAppear { state.suspendShortcuts() }
        }
    }
}

private struct KeyRecorder: NSViewRepresentable {
    var onRecord: (ShortcutBinding?) -> Void
    func makeNSView(context: Context) -> RecorderView {
        let view = RecorderView(); view.onRecord = onRecord; return view
    }
    func updateNSView(_ view: RecorderView, context: Context) { view.onRecord = onRecord }
    final class RecorderView: NSView {
        var onRecord: ((ShortcutBinding?) -> Void)?
        override var acceptsFirstResponder: Bool { true }
        override func viewDidMoveToWindow() { super.viewDidMoveToWindow(); window?.makeFirstResponder(self) }
        override func keyDown(with event: NSEvent) {
            if event.keyCode == 53 { onRecord?(nil); return }
            let flags = event.modifierFlags.intersection(.deviceIndependentFlagsMask)
            onRecord?(ShortcutBinding(keyCode: UInt32(event.keyCode), command: flags.contains(.command), shift: flags.contains(.shift), option: flags.contains(.option), control: flags.contains(.control)))
        }
        override func performKeyEquivalent(with event: NSEvent) -> Bool {
            guard window?.firstResponder === self else { return false }
            keyDown(with: event); return true
        }
    }
}
