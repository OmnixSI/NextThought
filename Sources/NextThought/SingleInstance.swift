import AppKit
import Foundation

/// One NextThought at a time.
///
/// Copies of the app in other folders (a build output, an older checkout) carry the same
/// bundle identifier and use the same data folder, and Spotlight lists them all under
/// one name. Opening one while another ran gave two apps, each with its own windows and
/// cue island, both recording the same meeting into the same files.
enum SingleInstance {
    /// Called before anything is read or drawn: when a copy is already running it is
    /// brought forward and this one leaves.
    static func yieldToRunningCopy() {
        guard let identifier = Bundle.main.bundleIdentifier else { return }
        let me = ProcessInfo.processInfo.processIdentifier
        func others() -> [NSRunningApplication] {
            NSRunningApplication.runningApplications(withBundleIdentifier: identifier)
                .filter { $0.processIdentifier != me && !$0.isTerminated }
        }
        // A relaunch (a new language, an update) starts this copy while the old one is
        // still on its way out: give it a moment before deciding it is here to stay.
        let deadline = Date().addingTimeInterval(4)
        while !others().isEmpty, Date() < deadline { usleep(200_000) }
        guard let running = others().first else { return }
        running.activate(options: [.activateAllWindows])
        exit(0)
    }
}
