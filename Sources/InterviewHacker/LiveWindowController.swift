import AppKit
import Observation
import SwiftUI
import InterviewHackerCore

/// Which of the three live views is on screen during a meeting: the notes window (the
/// live meeting page), the subtitle bar, or nothing. Recording goes on in all three.
///
/// Held by `AppState.liveWindow`. The answer overlay (`TeleprompterController`) is a
/// separate thing with its own shortcut: it is neither hidden nor shown by this mode.
@MainActor
@Observable
final class LiveWindowController {
    private(set) var mode: LiveWindowMode = .notes
    /// Font size of the main line of the subtitle bar.
    var subtitleFontSize = UserDefaults.standard.object(forKey: "subtitleBarFontSize") as? Double ?? 24

    /// Identifies the running session watch; a stale one ends itself.
    @ObservationIgnored private var sessionWatch: UUID?

    static let fontSizeRange: ClosedRange<Double> = 16...44

    func saveSubtitleFontSize() {
        UserDefaults.standard.set(subtitleFontSize, forKey: "subtitleBarFontSize")
    }

    /// The shortcut: notes → subtitles → hidden → notes. Only during a meeting.
    func cycle() {
        guard AppState.shared.isRunning else { return }
        set(mode.next())
    }

    func set(_ next: LiveWindowMode) {
        // Outside a meeting there is nothing to switch: the page is the app.
        guard next == .notes || AppState.shared.isRunning, next != mode else { return }
        let appWasActive = NSApp.isActive
        mode = next
        switch next {
        case .notes:
            SubtitleBarController.shared.stop()
            showMainWindows()
        case .subtitles:
            hideMainWindows(returnFocus: appWasActive)
            SubtitleBarController.shared.start()
        case .hidden:
            SubtitleBarController.shared.stop()
            hideMainWindows(returnFocus: appWasActive)
        }
        if next == .notes { sessionWatch = nil } else { watchSession() }
        Diagnostics.record("live.window_mode", ["mode": next.rawValue])
    }

    /// The page comes back without taking the keyboard from the meeting app, unless
    /// the user is already working in this one.
    private func showMainWindows() {
        for window in NSApp.windows where MainWindowLayout.isMainWindow(window) {
            if NSApp.isActive { window.makeKeyAndOrderFront(nil) } else { window.orderFrontRegardless() }
        }
    }

    /// Takes the page off screen (it stays alive: the translation sessions live in it)
    /// and, when it was the window the user was in, hands the keyboard back to the
    /// app they were in before.
    private func hideMainWindows(returnFocus: Bool) {
        for window in NSApp.windows where MainWindowLayout.isMainWindow(window) { window.orderOut(nil) }
        if returnFocus { NSApp.deactivate() }
    }

    /// When the meeting ends while the page is hidden (the stop shortcut, an error),
    /// bring the page back: the notes are there.
    private func watchSession() {
        let token = UUID()
        sessionWatch = token
        trackSession(token)
    }

    private func trackSession(_ token: UUID) {
        withObservationTracking {
            _ = AppState.shared.isRunning
        } onChange: { [weak self] in
            Task { @MainActor in
                guard let self, self.sessionWatch == token else { return }
                if AppState.shared.isRunning {
                    self.trackSession(token)
                } else {
                    self.set(.notes)
                }
            }
        }
    }
}

/// Small menu in the live top bar: 纪要窗口 / 字幕条 / 隐藏.
struct LiveWindowMenu: View {
    @Environment(AppState.self) private var state

    var body: some View {
        Menu {
            Picker("会中窗口", selection: Binding(get: { state.liveWindow.mode }, set: { state.liveWindow.set($0) })) {
                ForEach(LiveWindowMode.allCases) { mode in
                    Text(LocalizedStringKey(mode.title)).tag(mode)
                }
            }
            .pickerStyle(.inline)
            .labelsHidden()
            Divider()
            Text("按 \(state.shortcutLabel(.toggleLiveWindow)) 在三者之间切换")
        } label: {
            Image(systemName: "captions.bubble")
        }
        .menuStyle(.button)
        .buttonStyle(GlassIconButtonStyle(size: 28))
        .menuIndicator(.hidden)
        .fixedSize()
        .help("会中窗口 · \(state.shortcutLabel(.toggleLiveWindow))")
    }
}
