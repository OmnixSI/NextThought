import AppKit
import SwiftUI

/// Windows for modules popped out of the assistant page. Moving one over the main
/// window and letting go docks it back where it was dropped; closing it docks it too.
@MainActor
final class DetachedModuleController: NSObject, NSWindowDelegate {
    static let shared = DetachedModuleController()

    private var panels: [AssistantModule: NSPanel] = [:]
    /// Screen point (top-left) for the next window of a module, from a drag-out.
    var pendingOrigin: [AssistantModule: NSPoint?] = [:]
    private var settleTask: Task<Void, Never>?
    /// Closing programmatically (dock) must not dock again from `windowWillClose`.
    private var closingByDock: Set<AssistantModule> = []

    /// Reopens windows saved as detached in the layout (after launch).
    func restore() {
        guard !DemoMode.isEnabled else { return }
        for module in AppState.shared.assistantLayout.detached { open(module) }
    }

    func open(_ module: AssistantModule) {
        if let panel = panels[module] { panel.makeKeyAndOrderFront(nil); return }
        let state = AppState.shared
        let panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: Self.defaultSize(module).width, height: Self.defaultSize(module).height),
                            styleMask: [.titled, .closable, .resizable, .fullSizeContentView, .utilityWindow],
                            backing: .buffered, defer: false)
        panel.title = module.title
        panel.titlebarAppearsTransparent = true
        panel.titleVisibility = .hidden
        panel.isMovableByWindowBackground = true
        panel.isFloatingPanel = UserDefaults.standard.object(forKey: "keepOnTop") as? Bool ?? true
        panel.hidesOnDeactivate = false
        panel.becomesKeyOnlyIfNeeded = true
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.minSize = NSSize(width: 300, height: module.isCompact ? 120 : 220)
        panel.sharingType = state.capturePrivacy ? .none : .readOnly
        panel.isReleasedWhenClosed = false
        panel.delegate = self
        panel.contentView = NSHostingView(rootView: DetachedModuleView(module: module).environment(state))
        panel.setFrameAutosaveName("InterviewHacker.Module.\(module.rawValue)")
        if let origin = pendingOrigin[module] ?? nil {
            panel.setFrameTopLeftPoint(NSPoint(x: origin.x - 40, y: origin.y + 16))
        } else if !panel.setFrameUsingName("InterviewHacker.Module.\(module.rawValue)") {
            panel.center()
        }
        pendingOrigin[module] = nil
        panels[module] = panel
        panel.makeKeyAndOrderFront(nil)
    }

    func close(_ module: AssistantModule) {
        guard let panel = panels.removeValue(forKey: module) else { return }
        closingByDock.insert(module)
        panel.close()
        closingByDock.remove(module)
        if AppState.shared.dockHoverModule == module { AppState.shared.dockHoverModule = nil }
    }

    func applyPrivacy(_ enabled: Bool) {
        for panel in panels.values { panel.sharingType = enabled ? .none : .readOnly }
    }

    private static func defaultSize(_ module: AssistantModule) -> NSSize {
        switch module {
        case .conversation: NSSize(width: 420, height: 560)
        case .question: NSSize(width: 520, height: 160)
        case .answer: NSSize(width: 560, height: 480)
        case .history: NSSize(width: 380, height: 460)
        case .insights: NSSize(width: 440, height: 520)
        }
    }

    private func module(of window: NSWindow?) -> AssistantModule? {
        panels.first { $0.value === window }?.key
    }

    private var mainWindow: NSWindow? { NSApp.windows.first { MainWindowLayout.isMainWindow($0) && $0.isVisible } }

    // MARK: NSWindowDelegate

    func windowWillClose(_ notification: Notification) {
        guard let module = module(of: notification.object as? NSWindow), !closingByDock.contains(module) else { return }
        panels[module] = nil
        AppState.shared.dockModule(module)
    }

    /// While a module window is dragged over the main window, highlight it; once the
    /// drag ends there (button up, no more movement), dock the module at that spot.
    func windowDidMove(_ notification: Notification) {
        guard let panel = notification.object as? NSPanel, let module = module(of: panel), let main = mainWindow else { return }
        let pointer = NSEvent.mouseLocation
        let over = main.frame.contains(pointer) && main.isOnActiveSpace
        AppState.shared.dockHoverModule = over ? module : (AppState.shared.dockHoverModule == module ? nil : AppState.shared.dockHoverModule)
        guard over else { return }
        settleTask?.cancel()
        settleTask = Task { [weak self] in
            // Wait for the drag to finish.
            while NSEvent.pressedMouseButtons & 1 == 1 {
                try? await Task.sleep(for: .milliseconds(80))
                if Task.isCancelled { return }
            }
            guard let self, !Task.isCancelled, self.panels[module] === panel else { return }
            let pointer = NSEvent.mouseLocation
            guard let main = self.mainWindow, main.frame.contains(pointer) else {
                AppState.shared.dockHoverModule = nil
                return
            }
            let local = CGPoint(x: pointer.x - main.frame.minX, y: main.frame.maxY - pointer.y)
            let target = AppState.shared.dropTarget(at: local, windowWidth: main.frame.width, excluding: module)
            AppState.shared.dockHoverModule = nil
            AppState.shared.dockModule(module, column: target.column, index: target.index)
        }
    }
}

/// A popped-out module: a slim header with “合并回主窗口”, then the module itself.
struct DetachedModuleView: View {
    @Environment(AppState.self) private var state
    let module: AssistantModule

    var body: some View {
        ZStack {
            AppBackdrop()
            VStack(spacing: 8) {
                HStack(spacing: 8) {
                    Color.clear.frame(width: 52, height: 1) // traffic lights
                    Label(module.title, systemImage: module.symbol).font(.system(size: 12, weight: .semibold)).foregroundStyle(.secondary)
                    Spacer()
                    Button { state.dockModule(module) } label: { Label("合并回主窗口", systemImage: "arrow.down.right.and.arrow.up.left") }
                        .buttonStyle(ChipButtonStyle())
                        .help("也可以把这个窗口拖回主窗口里松开")
                }
                .padding(.horizontal, 10).padding(.top, 6)
                AssistantModuleContent(module: module, detached: true)
                    .padding(.horizontal, 10).padding(.bottom, 10)
            }
        }
        .tint(Brand.accent)
        .preferredColorScheme(state.colorScheme)
    }
}
