import AppKit
import SwiftUI
import Foundation
import NextThoughtCore

/// Uses the real menu/deep-link navigation and the real in-window settings page, without
/// audio, services, an on-screen window or accessibility automation. The page is drawn
/// into an off-screen window only to capture it. Interactive UI coverage is separate.
@MainActor
enum SettingsNavigationSelfTest {
    static func run(_ options: [String: String]) async throws -> String {
        try requireIsolatedDemo()
        _ = NSApplication.shared
        let state = AppState.shared
        let editor = WorkspaceEditController.shared
        let route = SettingsRoute.shared
        guard !editor.showUnsavedPrompt, !editor.showRestorePrompt else {
            throw SelfTest.Failure("settings-navigation requires no pending editor transition")
        }
        let defaults = UserDefaults.standard
        let keys = ["settingsGroup"]
        let savedPreferences = keys.map { defaults.object(forKey: $0) }
        let oldAppearance = state.appearance
        let oldSection = state.section, oldRunning = state.isRunning
        let oldMeetingDraft = state.meetingDraft, oldError = state.errorMessage
        let oldDraft = editor.draft, oldOriginal = editor.original, oldTarget = editor.target
        let oldEntryEditing = editor.isEntryEditing
        let oldPage = route.page, oldAnchor = route.anchor, wasPresented = route.isPresented
        defer {
            editor.continueEditing()
            editor.draft = oldDraft; editor.original = oldOriginal; editor.target = oldTarget
            editor.isEntryEditing = oldEntryEditing
            state.section = oldSection; state.isRunning = oldRunning
            state.appearance = oldAppearance; state.applyAppearance()
            state.meetingDraft = oldMeetingDraft; state.errorMessage = oldError
            for (key, value) in zip(keys, savedPreferences) {
                if let value { defaults.set(value, forKey: key) }
                else { defaults.removeObject(forKey: key) }
            }
            route.quickPresented = false
            if !wasPresented { route.dismiss() }
            route.show(oldPage, anchor: oldAnchor)
        }

        // A real dirty project draft and an unstarted meeting expose regressions
        // where opening Settings accidentally navigates or commits the workspace.
        let originalWorkspace = state.activeWorkspace
        let originalWorkspaces = state.workspaces.workspaces
        state.section = .library
        state.isRunning = false
        var meeting = MeetingDraft(); meeting.title = "Settings navigation: unstarted research meeting"
        state.meetingDraft = meeting
        editor.begin(.basic, workspace: originalWorkspace)
        var dirty = originalWorkspace
        dirty.name += " — unsaved settings navigation sentinel"
        editor.updateDraft(dirty)
        guard editor.hasChanges else { throw SelfTest.Failure("project-draft fixture is not dirty") }

        func checkWorkspace(running: Bool) throws {
            guard state.section == .library, state.isRunning == running,
                  state.activeWorkspace.id == originalWorkspace.id,
                  state.workspaces.workspaces == originalWorkspaces,
                  state.meetingDraft == meeting,
                  editor.draft == dirty, editor.original == originalWorkspace,
                  editor.target == .basic, editor.hasChanges, !editor.showUnsavedPrompt else {
                throw SelfTest.Failure("Settings changed the workspace, committed/discarded a draft, or requested an unrelated save")
            }
        }

        // Every entry point lands on the in-window page; none creates a window.
        let windowsBefore = NSApp.windows.count
        if route.isPresented { route.dismiss() }
        defaults.set(SettingsPage.notes.rawValue, forKey: "settingsGroup")
        state.select(.settings)
        try checkWorkspace(running: false)
        guard route.isPresented, route.page == .notes, SettingsWindow.isVisible,
              NSApp.windows.count == windowsBefore else {
            throw SelfTest.Failure("select(.settings) did not open the in-window page on the stored page")
        }
        state.openSettingsFromMenu()
        SettingsWindow.show()
        guard route.isPresented, route.page == .notes, NSApp.windows.count == windowsBefore else {
            throw SelfTest.Failure("menu and compatibility entries disagree about the settings page")
        }
        try checkWorkspace(running: false)

        // The quick panel and the full page are separate; opening the page closes the panel.
        route.quickPresented = true
        state.openSettingsFromMenu()
        guard !route.quickPresented else { throw SelfTest.Failure("the quick panel stayed open over the settings page") }

        let pages = SettingsPage.allCases
        let destinations = SettingsGroup.allCases
        guard pages.count == 6, Set(pages.map(\.rawValue)).count == 6 else {
            throw SelfTest.Failure("Settings must have exactly six distinct pages")
        }
        // Every name an older version stored must still land on a page, and a page name on itself.
        for group in destinations where SettingsPage(stored: group.rawValue) != group.page {
            throw SelfTest.Failure("stored destination " + group.rawValue + " does not open its page")
        }
        for page in pages where SettingsPage(stored: page.rawValue) != page {
            throw SelfTest.Failure("page name " + page.rawValue + " does not open itself")
        }
        let legacy: [(String, SettingsDestination)] = [
            ("常规", SettingsDestination(.general)), ("更新", SettingsDestination(.general, "更新")),
            ("快捷键", SettingsDestination(.presenter, "快捷键")), ("unknown-destination", SettingsDestination(.general)),
            ("声音", SettingsDestination(.advanced, AdvancedArea.sound.anchor)),
            ("数据与权限", SettingsDestination(.general, "权限")), ("资料", SettingsDestination(.answers, "资料")),
            ("回答", SettingsDestination(.answers)), ("识别", SettingsDestination(.advanced, AdvancedArea.sound.anchor)),
            ("手机伴侣", SettingsDestination(.advanced, AdvancedArea.companion.anchor)),
            ("屏幕与讲稿", SettingsDestination(.advanced, AdvancedArea.answers.anchor)),
            ("技能", SettingsDestination(.answers, "技能")), ("数据", SettingsDestination(.general, "记录每次会议")),
            ("权限与维护", SettingsDestination(.general, "权限")), ("提词器", SettingsDestination(.presenter)),
        ]
        for (stored, expected) in legacy where SettingsDestination(stored: stored) != expected {
            throw SelfTest.Failure("legacy settings name " + stored + " does not map to " + expected.page.rawValue)
        }

        // A fake running flag also exercises the live-meeting route. It does not
        // start capture, and prevents the recognition view from probing devices.
        state.isRunning = true
        var routes: [[String: String]] = []
        for group in destinations {
            state.openSettings(group)
            try checkWorkspace(running: true)
            guard route.isPresented, route.page == group.page, route.anchor == group.anchor,
                  defaults.string(forKey: "settingsGroup") == group.page.rawValue,
                  NSApp.windows.count == windowsBefore else {
                throw SelfTest.Failure("Settings deep link failed for " + group.rawValue)
            }
            routes.append(["group": group.rawValue, "page": group.page.rawValue, "anchor": group.anchor ?? ""])
            await Task.yield()
        }
        // The same destination twice is still a navigation (the page scrolls again).
        let revision = route.revision
        state.openSettings(.skills)
        state.openSettings(.skills)
        guard route.revision >= revision + 2 else { throw SelfTest.Failure("a repeated deep link did not navigate again") }

        // Search covers every page and every area of 高级, and points at a real anchor.
        for page in pages where !SettingsSearchItem.all.contains(where: { $0.page == page }) {
            throw SelfTest.Failure("no search entry for page " + page.rawValue)
        }
        for area in AdvancedArea.allCases where !SettingsSearchItem.all.contains(where: { $0.anchor == area.anchor }) {
            throw SelfTest.Failure("no search entry opens " + area.rawValue)
        }
        let searches: [(String, SettingsPage)] = [
            ("appearance", .general), ("minutes", .notes), ("terms", .general),
            ("template", .scenes), ("template", .notes), ("backup", .general),
            ("推理强度", .advanced), ("二次判断", .advanced), ("摄像头", .general), ("麦克风", .advanced), ("字幕", .presenter),
            ("Tavily", .advanced), ("联网", .answers), ("语义", .advanced), ("手机", .advanced), ("渠道", .advanced)
        ]
        for (query, expected) in searches {
            let hits = SettingsSearchItem.matching("  " + query.uppercased() + "  ")
            guard hits.contains(where: { $0.page == expected && !$0.anchor.isEmpty }) else {
                throw SelfTest.Failure("Settings search does not reach the expected destination for " + query)
            }
        }
        guard SettingsSearchItem.matching("   ").isEmpty,
              SettingsSearchItem.matching("no-settings-match-7c8ab3").isEmpty else {
            throw SelfTest.Failure("empty or unmatched Settings queries return unrelated results")
        }

        // Closing hides the page; opening a deep link revives it, preserves the dirty
        // project, and selects the requested destination. Another section closes it.
        route.dismiss()
        guard !route.isPresented, !SettingsWindow.isVisible else { throw SelfTest.Failure("the settings page did not close") }
        state.openSettings(.notes)
        guard route.isPresented, route.page == .notes,
              defaults.string(forKey: "settingsGroup") == SettingsPage.notes.rawValue else {
            throw SelfTest.Failure("reopening Settings lost its deep-link destination")
        }
        try checkWorkspace(running: true)
        state.isRunning = false
        state.section = .library
        route.show(.advanced, anchor: AdvancedArea.data.anchor)
        guard route.page == .advanced, route.anchor == AdvancedArea.data.anchor else {
            throw SelfTest.Failure("a jump from inside the page did not change page and section")
        }
        state.select(.home)
        guard !route.isPresented else { throw SelfTest.Failure("choosing another section left the settings page open") }
        state.section = .library

        if let output = options["output"] {
            let directory = URL(fileURLWithPath: output, isDirectory: true)
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            var captures: [[String: Any]] = []
            let size = NSSize(width: 960, height: 660)
            for appearance in ["light", "dark"] {
                state.appearance = appearance; state.applyAppearance()
                for page in pages {
                    route.present(page)
                    let host = NSHostingView(rootView: SettingsPageView().environment(state).wideHitTargets()
                        .preferredColorScheme(state.colorScheme).background(Ink.paper))
                    host.sizingOptions = []
                    // Never ordered front: nothing appears on screen.
                    let window = NSWindow(contentRect: NSRect(origin: .zero, size: size), styleMask: [.titled], backing: .buffered, defer: false)
                    window.isReleasedWhenClosed = false
                    window.contentView = host
                    window.setContentSize(size)
                    try await Task.sleep(for: .milliseconds(240))
                    host.layoutSubtreeIfNeeded()
                    guard let bitmap = host.bitmapImageRepForCachingDisplay(in: host.bounds) else {
                        throw SelfTest.Failure("could not capture the settings page")
                    }
                    host.cacheDisplay(in: host.bounds, to: bitmap)
                    guard let png = bitmap.representation(using: .png, properties: [:]) else {
                        throw SelfTest.Failure("could not encode the settings screenshot")
                    }
                    let filename = "settings-" + String(pages.firstIndex(of: page) ?? 0) + "-" + appearance + ".png"
                    try png.write(to: directory.appendingPathComponent(filename), options: .atomic)
                    captures.append(["path": filename, "page": page.rawValue, "appearance": appearance])
                    window.close()
                }
            }
            let report: [String: Any] = ["routes": routes, "queries": searches.map(\.0),
                                       "dirtyProjectPreserved": true, "meetingDraftPreserved": true,
                                       "modelCalls": 0, "captures": captures]
            try JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys])
                .write(to: directory.appendingPathComponent("settings-navigation.json"), options: .atomic)
        }
        return "in-window Settings page; menu/gear/deep-link/reopen/compat entries open it without a window; dirty project and meeting drafts preserved; \(destinations.count) legacy destinations map onto 6 pages; every page and 高级 area searchable; no devices, models or AX automation"
    }

    private static func requireIsolatedDemo() throws {
        let identifier = Bundle.main.bundleIdentifier ?? ""
        guard identifier.hasPrefix("com.nextthought.app.ci") || identifier.hasPrefix("com.interviewhacker.app.ci"),
              DemoMode.isEnabled else {
            throw SelfTest.Failure("settings-navigation requires a .ci bundle and DemoMode")
        }
        let resolvedRoot = AppPaths.root.standardizedFileURL.resolvingSymlinksInPath()
        let environment = AppEnvironment.values
        let explicit = environment["NEXTTHOUGHT_DATA_DIR"].flatMap { path -> URL? in
            guard !path.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return nil }
            return URL(fileURLWithPath: path, isDirectory: true).standardizedFileURL.resolvingSymlinksInPath()
        }
        let reviewMode = Bundle.main.object(forInfoDictionaryKey: "NextThoughtReviewMode") as? Bool == true
            || Bundle.main.object(forInfoDictionaryKey: "InterviewHackerReviewMode") as? Bool == true
        let reviewRoot = Bundle.main.bundleURL.deletingLastPathComponent()
            .appendingPathComponent(".nextthought-review", isDirectory: true).standardizedFileURL.resolvingSymlinksInPath()
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        let productionRoots = ["NextThought", "InterviewHacker"].map {
            support.appendingPathComponent($0, isDirectory: true).standardizedFileURL.resolvingSymlinksInPath().path
        }
        guard explicit == resolvedRoot || (reviewMode && reviewRoot == resolvedRoot),
              !productionRoots.contains(where: { resolvedRoot.path == $0 || resolvedRoot.path.hasPrefix($0 + "/") }) else {
            throw SelfTest.Failure("settings-navigation requires an explicit isolated DATA_DIR or the .ci review data directory")
        }
    }
}
