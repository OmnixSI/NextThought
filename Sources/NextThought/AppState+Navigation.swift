import AppKit
import SwiftUI

/// Workspace navigation. Settings are a page laid over the main window (see
/// `SettingsRoute`), never a window of their own, so nothing underneath is replaced.
@MainActor
extension AppState {
    func select(_ target: AppSection) {
        if target == .settings {
            SettingsRoute.shared.presentStored()
            return
        }
        // Another destination closes the settings page instead of changing the view behind it.
        SettingsRoute.shared.dismiss()
        WorkspaceEditController.shared.requestAppSection(target, state: self)
    }

    /// ⌘, from the menu bar: the full settings page.
    func openSettingsFromMenu() {
        SettingsRoute.shared.presentStored()
    }

    /// The gear in the title row: the quick panel.
    func toggleQuickSettings() {
        SettingsRoute.shared.quickPresented.toggle()
    }
}

/// Where the settings page and the quick panel are, and which page and section the
/// page shows. Every entry point (menu, gear, deep link, search jump) goes through it.
@MainActor @Observable
final class SettingsRoute {
    static let shared = SettingsRoute()

    private(set) var isPresented = false
    var quickPresented = false
    var page: SettingsPage = .scenes
    /// The section the page scrolls to (and opens, in 高级) when it appears.
    var anchor: String?
    /// Bumped on every navigation so the same destination can be asked for twice.
    private(set) var revision = 0

    private init() {
        let stored = UserDefaults.standard.string(forKey: "settingsGroup") ?? ""
        page = SettingsDestination(stored: stored).page
    }

    /// Opens the page the callers' `settingsGroup` default (old or new name) points at.
    func presentStored() {
        let stored = UserDefaults.standard.string(forKey: "settingsGroup") ?? page.rawValue
        let destination = SettingsDestination(stored: stored)
        present(destination.page, anchor: destination.anchor)
    }

    func present(_ page: SettingsPage, anchor: String? = nil) {
        self.page = page
        self.anchor = anchor
        revision &+= 1
        UserDefaults.standard.set(page.rawValue, forKey: "settingsGroup")
        quickPresented = false
        guard !isPresented else { return }
        withAnimation(Motion.morph) { isPresented = true }
    }

    /// A page chosen inside the settings page; remembered for the next time.
    func show(_ page: SettingsPage, anchor: String? = nil) {
        self.page = page
        self.anchor = anchor
        revision &+= 1
        UserDefaults.standard.set(page.rawValue, forKey: "settingsGroup")
    }

    func dismiss() {
        guard isPresented else { return }
        withAnimation(Motion.morph) { isPresented = false }
        anchor = nil
    }
}

/// The settings used to be a window. Code that still asks for it gets the in-window page.
@MainActor
enum SettingsWindow {
    static let identifier = NSUserInterfaceItemIdentifier("NextThought.SettingsWindow")
    static var isVisible: Bool { SettingsRoute.shared.isPresented }
    static func show() { SettingsRoute.shared.presentStored() }
}
