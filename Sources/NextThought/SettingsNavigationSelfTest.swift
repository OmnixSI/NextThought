import AppKit
import Foundation
import NextThoughtCore

/// Uses the real menu/deep-link navigation and a real settings window, without
/// audio, services or accessibility automation. Interactive UI coverage is separate.
@MainActor
enum SettingsNavigationSelfTest {
    static func run(_ options: [String: String]) async throws -> String {
        try requireIsolatedDemo()
        _ = NSApplication.shared
        // A CLI-launched runner has no SwiftUI App lifecycle to finish launching
        // AppKit. Start it explicitly before asserting window visibility.
        NSApp.setActivationPolicy(.regular)
        if !NSApp.isRunning { NSApp.finishLaunching() }
        let state = AppState.shared
        let editor = WorkspaceEditController.shared
        guard !editor.showUnsavedPrompt, !editor.showRestorePrompt else {
            throw SelfTest.Failure("settings-navigation requires no pending editor transition")
        }
        let defaults = UserDefaults.standard
        let keys = ["settingsGroup", "NSWindow Frame NextThought.Settings"]
        let savedPreferences = keys.map { defaults.object(forKey: $0) }
        let oldAppearance = state.appearance
        let oldSection = state.section, oldRunning = state.isRunning
        let oldMeetingDraft = state.meetingDraft, oldError = state.errorMessage
        let oldDraft = editor.draft, oldOriginal = editor.original, oldTarget = editor.target
        let oldEntryEditing = editor.isEntryEditing
        let existingWindow = settingsWindows.first
        let wasVisible = existingWindow?.isVisible == true
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
            if !wasVisible { settingsWindows.first?.orderOut(nil) }
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

        defaults.set(SettingsGroup.essentials.rawValue, forKey: "settingsGroup")
        state.select(.settings)
        try checkWorkspace(running: false)
        guard settingsWindows.count == 1, let first = settingsWindows.first,
              first.isVisible, first.contentView != nil,
              first.contentMinSize.width >= 880, first.contentMinSize.height >= 600,
              first.styleMask.contains(.resizable) else {
            let details = settingsWindows.map {
                "visible=\($0.isVisible), content=\($0.contentView != nil), contentMin=\($0.contentMinSize), frameMin=\($0.minSize), style=\($0.styleMask.rawValue)"
            }.joined(separator: "; ")
            throw SelfTest.Failure("Settings did not open as one independent, resizable window (count=\(settingsWindows.count); \(details))")
        }
        let identity = ObjectIdentifier(first)
        state.openSettingsFromMenu()
        state.select(.settings)
        guard settingsWindows.count == 1, settingsWindows.first.map(ObjectIdentifier.init) == identity else {
            throw SelfTest.Failure("menu and workspace Settings entries created different windows")
        }
        try checkWorkspace(running: false)

        let pages = SettingsPage.allCases
        let destinations = SettingsGroup.allCases
        guard pages.count == 8, Set(pages.map(\.rawValue)).count == 8 else {
            throw SelfTest.Failure("Settings must have exactly eight distinct pages")
        }
        // Every name an older version stored must still land on a page, and a page name on itself.
        for group in destinations where SettingsPage(stored: group.rawValue) != group.page {
            throw SelfTest.Failure("stored destination " + group.rawValue + " does not open its page")
        }
        for page in pages where SettingsPage(stored: page.rawValue) != page {
            throw SelfTest.Failure("page name " + page.rawValue + " does not open itself")
        }
        guard SettingsPage(stored: "常规") == .general, SettingsPage(stored: "更新") == .general,
              SettingsPage(stored: "快捷键") == .general, SettingsPage(stored: "unknown-destination") == .general else {
            throw SelfTest.Failure("legacy settings names do not map to the new pages")
        }

        // A fake running flag also exercises the live-meeting route. It does not
        // start capture, and prevents the recognition view from probing devices.
        state.isRunning = true
        var routes: [[String: String]] = []
        for group in destinations {
            state.openSettings(group)
            try checkWorkspace(running: true)
            guard defaults.string(forKey: "settingsGroup") == group.rawValue,
                  settingsWindows.count == 1, settingsWindows.first.map(ObjectIdentifier.init) == identity else {
                throw SelfTest.Failure("Settings deep link failed for " + group.rawValue)
            }
            first.contentView?.layoutSubtreeIfNeeded()
            routes.append(["group": group.rawValue, "page": group.page.rawValue])
            await Task.yield()
        }

        let searches: [(String, SettingsPage)] = [
            ("appearance", .general), ("minutes", .notes), ("terms", .sound),
            ("template", .scenes), ("template", .notes), ("backup", .data),
            ("推理强度", .answers), ("二次判断", .answers), ("摄像头", .data), ("麦克风", .sound), ("字幕", .presenter)
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

        // Closing hides the reusable window; opening a deep link must revive it,
        // preserve the dirty project, and select the requested destination.
        first.close()
        state.openSettings(.notes)
        guard settingsWindows.count == 1, let reopened = settingsWindows.first,
              ObjectIdentifier(reopened) == identity, reopened.isVisible,
              defaults.string(forKey: "settingsGroup") == SettingsGroup.notes.rawValue else {
            throw SelfTest.Failure("reopening Settings lost its window identity or deep-link destination")
        }
        try checkWorkspace(running: true)

        if let output = options["output"] {
            let directory = URL(fileURLWithPath: output, isDirectory: true)
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            var captures: [[String: Any]] = []
            state.isRunning = false
            for appearance in ["light", "dark"] {
                state.appearance = appearance; state.applyAppearance()
                for size in [NSSize(width: 980, height: 720), NSSize(width: 880, height: 600)] {
                    for (page, name) in [(SettingsPage.general, "general"), (.scenes, "scenes"), (.notes, "notes")] {
                        defaults.set(page.rawValue, forKey: "settingsGroup")
                        reopened.setContentSize(size)
                        try await Task.sleep(for: .milliseconds(160))
                        guard let content = reopened.contentView else { throw SelfTest.Failure("missing settings content view") }
                        content.layoutSubtreeIfNeeded()
                        guard let bitmap = content.bitmapImageRepForCachingDisplay(in: content.bounds) else {
                            throw SelfTest.Failure("could not capture native settings content")
                        }
                        content.cacheDisplay(in: content.bounds, to: bitmap)
                        guard let png = bitmap.representation(using: .png, properties: [:]) else {
                            throw SelfTest.Failure("could not encode native settings screenshot")
                        }
                        let filename = "settings-" + name + "-" + appearance + "-" + String(Int(size.width)) + ".png"
                        try png.write(to: directory.appendingPathComponent(filename), options: .atomic)
                        captures.append(["path": filename, "appearance": appearance,
                                         "width": Int(size.width), "height": Int(size.height)])
                    }
                }
            }
            let report: [String: Any] = ["routes": routes, "queries": searches.map(\.0),
                                       "windowReused": true, "dirtyProjectPreserved": true,
                                       "meetingDraftPreserved": true, "modelCalls": 0, "captures": captures]
            try JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys])
                .write(to: directory.appendingPathComponent("settings-navigation.json"), options: .atomic)
        }
        return "independent Settings window; menu/deep-link/reopen reuse; dirty project and meeting drafts preserved; 12 legacy destinations map onto 8 pages; appearance/minutes/terms/template/backup search; no devices, models or AX automation"
    }

    private static var settingsWindows: [NSWindow] {
        NSApp.windows.filter { $0.identifier == SettingsWindow.identifier }
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
