import AppKit
import SwiftUI

struct InterviewHackerApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    var body: some Scene {
        WindowGroup {
            // Created on first use (after NSApplication exists), not in App.init.
            ContentView()
                .environment(AppState.shared)
        }
        .windowStyle(.hiddenTitleBar)
        .defaultSize(width: MainWindowLayout.ideal.width, height: MainWindowLayout.ideal.height)
        .windowResizability(.contentMinSize)
        .commands {
            // One main window: ⌘N starts a new meeting instead of opening a window.
            CommandGroup(replacing: .newItem) {
                Button("新会议") { AppState.shared.beginNewMeeting() }
                    .keyboardShortcut("n", modifiers: .command)
                    .disabled(AppState.shared.isRunning)
                Button("立即记录") { Task { await AppState.shared.recordNow() } }
                    .keyboardShortcut("r", modifiers: [.command, .shift])
                    .disabled(AppState.shared.isRunning || AppState.shared.isChangingSession)
            }
            CommandGroup(replacing: .appSettings) {
                Button("设置…") { AppState.shared.openSettingsFromMenu() }
                    .keyboardShortcut(",", modifiers: .command)
            }
            CommandGroup(after: .windowSize) {
                Button("恢复最佳窗口大小") { MainWindowLayout.resetFrontWindow() }
                    .keyboardShortcut("0", modifiers: [.command, .option])
            }
        }
    }
}

/// The main window opens at the size the README screenshots use (wide enough for
/// conversation and answer side by side), centred on the screen with the pointer,
/// every launch: a frame dragged small or half off-screen last time never comes back.
@MainActor
enum MainWindowLayout {
    static let ideal = NSSize(width: 1120, height: 820)
    /// Below this the side-by-side layout breaks down and labels start to wrap.
    static let minimum = NSSize(width: 960, height: 660)
    private static var configured: Set<ObjectIdentifier> = []

    static func isMainWindow(_ window: NSWindow) -> Bool {
        !(window is NSPanel) && !window.isSheet && window.sheetParent == nil
            && window.identifier != SettingsWindow.identifier
            && window.contentViewController != nil && window.styleMask.contains(.titled)
    }

    /// Styles a main window once and gives it the ideal frame.
    static func configure(_ window: NSWindow) {
        guard isMainWindow(window), configured.insert(ObjectIdentifier(window)).inserted else { return }
        window.title = "InterviewHacker"
        window.titleVisibility = .hidden
        window.titlebarAppearsTransparent = true
        window.isMovableByWindowBackground = true
        let keepOnTop = UserDefaults.standard.object(forKey: "keepOnTop") as? Bool ?? true
        window.level = keepOnTop ? .floating : .normal
        window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        window.minSize = minimum
        window.backgroundColor = Ink.paperNSColor
        window.isOpaque = true
        window.isRestorable = false
        window.setFrameAutosaveName("")
        reset(window)
    }

    static func reset(_ window: NSWindow) {
        if window.styleMask.contains(.fullScreen) { return }
        window.setFrame(idealFrame(on: screen(for: window)), display: true, animate: window.isVisible)
    }

    /// Shows the main window and activates the app.
    static func bringToFront() {
        NSApp.windows.first(where: isMainWindow)?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    static func resetFrontWindow() {
        let window = NSApp.keyWindow.flatMap { isMainWindow($0) ? $0 : nil } ?? NSApp.windows.first(where: isMainWindow)
        if let window { reset(window) }
    }

    /// Ideal size clamped to the visible area (menu bar and Dock excluded), centred.
    static func idealFrame(on screen: NSScreen?) -> NSRect {
        guard let visible = screen?.visibleFrame else { return NSRect(origin: .zero, size: ideal) }
        let margin: CGFloat = 24
        let width = max(minimum.width, min(ideal.width, visible.width - margin * 2))
        let height = max(minimum.height, min(ideal.height, visible.height - margin * 2))
        return NSRect(x: (visible.midX - width / 2).rounded(), y: (visible.midY - height / 2).rounded(), width: width, height: height)
    }

    private static func screen(for window: NSWindow) -> NSScreen? {
        let pointer = NSEvent.mouseLocation
        return NSScreen.screens.first { NSMouseInRect(pointer, $0.frame, false) } ?? window.screen ?? NSScreen.main
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationWillTerminate(_ notification: Notification) {
        AppState.shared.prepareForTermination()
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        // A window reopened from the Dock after closing is a new window: style and size it too.
        NotificationCenter.default.addObserver(forName: NSWindow.didBecomeKeyNotification, object: nil, queue: .main) { note in
            guard let window = note.object as? NSWindow else { return }
            MainActor.assumeIsolated { MainWindowLayout.configure(window) }
        }
        DispatchQueue.main.async {
            for window in NSApp.windows { MainWindowLayout.configure(window) }
            AppState.shared.applyAppearance()
            TeleprompterController.shared.applyPrivacy(AppState.shared.capturePrivacy)
            DemoMode.apply()
        }
    }
}
