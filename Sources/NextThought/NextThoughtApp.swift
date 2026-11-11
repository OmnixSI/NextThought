import AppKit
import SwiftUI

struct NextThoughtApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @AppStorage(AppNavigationBar.meetingsSidebarKey, store: AppPreferences.standard) private var meetingsSidebarHidden = false
    @AppStorage(AppNavigationBar.foldersSidebarKey, store: AppPreferences.standard) private var foldersSidebarHidden = false

    private var contextSidebarHidden: Bool {
        AppState.shared.section == .meetings ? meetingsSidebarHidden : foldersSidebarHidden
    }

    private var hasContextSidebar: Bool {
        AppState.shared.section == .meetings || AppState.shared.section == .library
    }

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
                    .keyboardShortcut("n", modifiers: [.command, .shift])
                    .disabled(AppState.shared.isRunning || AppState.shared.isChangingSession)
            }
            CommandGroup(replacing: .appSettings) {
                Button("设置…") { AppState.shared.openSettingsFromMenu() }
                    .keyboardShortcut(",", modifiers: .command)
            }
            CommandGroup(replacing: .sidebar) {
                Button(contextSidebarHidden ? "显示列表" : "隐藏列表") {
                    if AppState.shared.section == .meetings {
                        meetingsSidebarHidden.toggle()
                    } else if AppState.shared.section == .library {
                        foldersSidebarHidden.toggle()
                    }
                }
                .keyboardShortcut("b", modifiers: .command)
                .disabled(AppState.shared.isRunning || !hasContextSidebar)
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
            && window.contentViewController != nil && window.styleMask.contains(.titled)
    }

    /// Styles a main window once and gives it the ideal frame.
    static func configure(_ window: NSWindow) {
        guard isMainWindow(window), configured.insert(ObjectIdentifier(window)).inserted else { return }
        window.title = "NextThought"
        window.titleVisibility = .hidden
        window.titlebarAppearsTransparent = true
        // An empty unified toolbar makes the title row 52 pt tall with the traffic
        // lights centred in it; the navigation and the live bar draw in that row.
        let toolbar = NSToolbar(identifier: "NextThought.Main")
        toolbar.showsBaselineSeparator = false
        window.toolbar = toolbar
        window.toolbarStyle = .unified
        claimTitleRow(of: window)
        for name in [NSWindow.didEnterFullScreenNotification, NSWindow.didExitFullScreenNotification] {
            NotificationCenter.default.addObserver(forName: name, object: window, queue: .main) { [weak window] _ in
                MainActor.assumeIsolated { if let window { claimTitleRow(of: window) } }
            }
        }
        window.isMovableByWindowBackground = true
        window.level = effectiveWorkspaceLevel
        window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        window.minSize = minimum
        window.backgroundColor = Ink.paperNSColor
        window.isOpaque = true
        window.isRestorable = false
        window.setFrameAutosaveName("")
        reset(window)
    }

    /// Gives the title row to the content: the title bar's safe area is cancelled, so
    /// the interface lays out from the top edge of the window.
    ///
    /// Merely ignoring the safe area in SwiftUI is not enough: a scroll view or split
    /// view that starts where the safe area ends still stretches its AppKit view up
    /// into it, on top of the bar, and takes every click meant for the bar's buttons.
    static func claimTitleRow(of window: NSWindow) {
        let titleBar = window.frame.height - window.contentLayoutRect.height
        window.contentView?.additionalSafeAreaInsets = NSEdgeInsets(top: -titleBar, left: 0, bottom: 0, right: 0)
    }

    static var effectiveWorkspaceLevel: NSWindow.Level {
        let keepOnTop = UserDefaults.standard.object(forKey: "keepOnTop") as? Bool ?? true
        return keepOnTop && NSApp?.isActive == true ? .floating : .normal
    }

    /// Workspace windows float only while NextThought is the active app.
    static func refreshLevels() {
        let level = effectiveWorkspaceLevel
        for window in NSApp.windows where isMainWindow(window) {
            window.level = level
        }
        DetachedModuleController.shared.applyWindowLevel(level)
    }

    static func reset(_ window: NSWindow) {
        if window.styleMask.contains(.fullScreen) { return }
        window.setFrame(idealFrame(on: screen(for: window)), display: true, animate: window.isVisible)
    }

    /// Shows the main window and activates the app.
    static func bringToFront() {
        let window = NSApp.windows.first(where: isMainWindow)
        if window?.isMiniaturized == true { window?.deminiaturize(nil) }
        // Hands-on walkthroughs must never take focus from whatever the person is doing.
        if AppEnvironment.values["NEXTTHOUGHT_DEMO_STAGE"] == "1" { window?.orderFront(nil); return }
        window?.makeKeyAndOrderFront(nil)
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

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    private var closing = false
    private var terminationCheckInProgress = false

    func applicationWillTerminate(_ notification: Notification) {
        AppState.shared.prepareForTermination()
    }

    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        guard WorkspaceEditController.shared.shouldTerminateApplication() else { return .terminateCancel }
        guard !terminationCheckInProgress else { return .terminateLater }
        let state = AppState.shared
        // A running meeting ends silently otherwise: ask first (the notes follow on next launch).
        guard state.confirmEndingMeeting(for: .quit) else { return .terminateCancel }
        terminationCheckInProgress = true
        Task { @MainActor in
            guard !state.isChangingSession else {
                state.errorMessage = "会议正在切换，稍后再试一次退出。"
                state.endMeetingConfirmed = false
                terminationCheckInProgress = false
                sender.reply(toApplicationShouldTerminate: false)
                return
            }
            state.beginTerminationPreparation()
            if state.isRunning || state.isPaused { await state.stopSession(preparingForTermination: true) }
            guard !state.isRunning, !state.isPaused, !state.isChangingSession else {
                state.errorMessage = "会议正在切换，稍后再试一次退出。"
                state.resumeAfterFailedTermination()
                terminationCheckInProgress = false
                sender.reply(toApplicationShouldTerminate: false)
                return
            }
            await state.archive.flushUserContent()
            let archiveSaved = await state.archive.flush()
            let saved = archiveSaved && state.archive.userContentDrafts.isEmpty && state.archive.userSaveErrors.isEmpty
            terminationCheckInProgress = false
            if saved {
                state.prepareForTermination()
                sender.reply(toApplicationShouldTerminate: true)
            } else {
                if let pendingSessionID = state.archive.persistenceBySession.first(where: {
                    $0.value.pendingEvents > 0 || $0.value.lastFailure != nil
                })?.key {
                    state.historySelection = pendingSessionID
                    state.section = .meetings
                }
                let snapshots = Array(state.archive.persistenceBySession.values)
                let failure = state.archive.userSaveErrors.values.sorted().first
                    ?? snapshots.first(where: { $0.lastFailure != nil })?.lastFailure
                let hasUnacknowledgedLoss = snapshots.contains(where: \.hasUnacknowledgedDataLoss)
                state.errorMessage = hasUnacknowledgedLoss
                    ? "有会议内容未能入队，因此没有保存。请到会议归档确认缺失并继续保存后，再退出。" + (failure.map { "\n" + $0 } ?? "")
                    : "会议归档尚未全部保存。请重试保存后再退出。" + (failure.map { "\n" + $0 } ?? "")
                state.resumeAfterFailedTermination()
                sender.reply(toApplicationShouldTerminate: false)
            }
        }
        return .terminateLater
    }

    /// A click on the Dock icon while the page is hidden by the subtitle bar or hidden
    /// mode brings the page back (and nothing else opens a second window).
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        let live = AppState.shared.liveWindow
        guard live.mode != .notes else { return true }
        live.set(.notes)
        return false
    }

    func windowShouldClose(_ sender: NSWindow) -> Bool {
        guard MainWindowLayout.isMainWindow(sender) else { return true }
        guard WorkspaceEditController.shared.shouldCloseWindow() else { return false }
        guard !SessionArchive.shared.userContentDrafts.isEmpty else { return true }
        guard !closing else { return false }
        closing = true
        Task {
            await SessionArchive.shared.flushUserContent()
            closing = false
            if SessionArchive.shared.userContentDrafts.isEmpty && SessionArchive.shared.userSaveErrors.isEmpty {
                sender.close()
            } else {
                AppState.shared.errorMessage = "笔记尚未保存，已保留窗口。请重试保存。"
            }
        }
        return false
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        // A window reopened from the Dock after closing is a new window: style and size it too.
        NotificationCenter.default.addObserver(forName: NSWindow.didBecomeKeyNotification, object: nil, queue: .main) { note in
            guard let window = note.object as? NSWindow else { return }
            MainActor.assumeIsolated {
                MainWindowLayout.configure(window)
                if MainWindowLayout.isMainWindow(window) { window.delegate = self }
            }
        }
        NotificationCenter.default.addObserver(forName: NSApplication.didBecomeActiveNotification, object: NSApp, queue: .main) { _ in
            MainActor.assumeIsolated { MainWindowLayout.refreshLevels() }
        }
        NotificationCenter.default.addObserver(forName: NSApplication.didResignActiveNotification, object: NSApp, queue: .main) { _ in
            MainActor.assumeIsolated { MainWindowLayout.refreshLevels() }
        }
        DispatchQueue.main.async {
            for window in NSApp.windows {
                MainWindowLayout.configure(window)
                if MainWindowLayout.isMainWindow(window) { window.delegate = self }
            }
            MainWindowLayout.refreshLevels()
            AppState.shared.applyAppearance()
            TeleprompterController.shared.applyPrivacy(AppState.shared.capturePrivacy)
            CueIslandController.shared.observeSession()
            CueIslandController.shared.refreshPresence()
            DemoMode.apply()
        }
    }
}
