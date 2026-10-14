import AppKit
import SwiftUI

@main
struct InterviewHackerApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var state = AppState.shared

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(state)
        }
        .windowStyle(.hiddenTitleBar)
        .defaultSize(width: 720, height: 820)
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
                window.minSize = NSSize(width: 580, height: 640)
                window.backgroundColor = .windowBackgroundColor
                window.isOpaque = true
            }
            TeleprompterController.shared.applyPrivacy(AppState.shared.capturePrivacy)
            DemoMode.apply()
        }
    }
}
