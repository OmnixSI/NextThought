import AppKit
import SwiftUI

/// Sidebar navigation and the separate settings window used during a meeting.
@MainActor
extension AppState {
    /// Selects a sidebar section. During a meeting the sidebar is hidden; settings
    /// then open in their own window instead of replacing the meeting.
    func select(_ target: AppSection) {
        if isRunning, target == .settings {
            SettingsWindow.show()
            return
        }
        section = target
    }

    /// ⌘, from the menu bar.
    func openSettingsFromMenu() {
        if isRunning {
            SettingsWindow.show()
        } else {
            section = .settings
            MainWindowLayout.bringToFront()
        }
    }
}

/// Settings in a small window of their own (⌘, during a meeting), so the meeting page
/// is never replaced.
@MainActor
enum SettingsWindow {
    static let identifier = NSUserInterfaceItemIdentifier("InterviewHacker.SettingsWindow")
    private static var window: NSWindow?

    static func show() {
        if let window {
            window.makeKeyAndOrderFront(nil)
            NSApp.activate(ignoringOtherApps: true)
            return
        }
        let next = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 920, height: 660),
                            styleMask: [.titled, .closable, .miniaturizable, .resizable],
                            backing: .buffered, defer: false)
        next.identifier = identifier
        next.title = String(localized: "设置")
        next.isReleasedWhenClosed = false
        next.backgroundColor = Ink.paperNSColor
        next.minSize = NSSize(width: 820, height: 560)
        // Same level as the main window, so it does not open behind it.
        next.level = AppState.shared.keepOnTop ? .floating : .normal
        next.contentView = NSHostingView(rootView: SettingsView()
            .padding(.top, Space.s3)
            .environment(AppState.shared)
            .preferredColorScheme(AppState.shared.colorScheme)
            .background(Ink.paper))
        next.center()
        window = next
        next.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }
}
