import AppKit
import SwiftUI

/// Workspace navigation and the independent settings window.
@MainActor
extension AppState {
    /// Settings never replace the workspace or interrupt an editor draft.
    func select(_ target: AppSection) {
        if target == .settings {
            SettingsWindow.show()
            return
        }
        WorkspaceEditController.shared.requestAppSection(target, state: self)
    }

    /// ⌘, from the menu bar.
    func openSettingsFromMenu() {
        SettingsWindow.show()
    }
}

/// One reusable, resizable settings window, shared by all entry points.
@MainActor
enum SettingsWindow {
    static let identifier = NSUserInterfaceItemIdentifier("NextThought.SettingsWindow")
    private static var window: NSWindow?
    static var isVisible: Bool { window?.isVisible == true }

    static func show() {
        if let window {
            window.makeKeyAndOrderFront(nil)
            NSApp.activate(ignoringOtherApps: true)
            return
        }
        let next = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 980, height: 720),
                            styleMask: [.titled, .closable, .miniaturizable, .resizable],
                            backing: .buffered, defer: false)
        next.identifier = identifier
        next.title = String(localized: "设置")
        next.isReleasedWhenClosed = false
        next.backgroundColor = Ink.paperNSColor
        next.minSize = NSSize(width: 880, height: 600)
        next.level = MainWindowLayout.effectiveWorkspaceLevel
        next.setFrameAutosaveName("NextThought.Settings")
        next.contentView = NSHostingView(rootView: SettingsWindowContent())
        next.center()
        window = next
        next.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
        MainWindowLayout.refreshLevels()
    }
}

/// Observe appearance in the root view so changes apply immediately in this window.
private struct SettingsWindowContent: View {
    @State private var state = AppState.shared
    var body: some View {
        SettingsView()
            .environment(state)
            .wideHitTargets()
            .preferredColorScheme(state.colorScheme)
            .background(Ink.paper)
    }
}
