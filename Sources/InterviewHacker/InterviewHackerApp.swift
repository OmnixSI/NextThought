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
        .defaultSize(width: 1080, height: 780)
        .windowResizability(.contentMinSize)
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationWillTerminate(_ notification: Notification) {
        AppState.shared.prepareForTermination()
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        DispatchQueue.main.async {
            for window in NSApp.windows {
                window.title = "InterviewHacker"
                window.titleVisibility = .hidden
                window.titlebarAppearsTransparent = true
                window.isMovableByWindowBackground = true
                let keepOnTop = UserDefaults.standard.object(forKey: "keepOnTop") as? Bool ?? true
                window.level = keepOnTop ? .floating : .normal
                window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
                window.minSize = NSSize(width: 600, height: 620)
                window.backgroundColor = .windowBackgroundColor
                window.isOpaque = true
            }
            AppState.shared.applyAppearance()
            TeleprompterController.shared.applyPrivacy(AppState.shared.capturePrivacy)
            DemoMode.apply()
        }
    }
}
