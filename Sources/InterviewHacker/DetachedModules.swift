import AppKit
import SwiftUI

/// Windows for modules popped out of the assistant page (模块 menu). “合并回主窗口”
/// or closing the window docks the module back.
@MainActor
final class DetachedModuleController: NSObject, NSWindowDelegate {
    static let shared = DetachedModuleController()

    private var panels: [AssistantModule: NSPanel] = [:]
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
        panel.isFloatingPanel = MainWindowLayout.effectiveWorkspaceLevel == .floating
        panel.level = MainWindowLayout.effectiveWorkspaceLevel
        panel.hidesOnDeactivate = false
        panel.becomesKeyOnlyIfNeeded = true
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.minSize = NSSize(width: 300, height: module.isCompact ? 120 : 220)
        panel.sharingType = state.capturePrivacy ? .none : .readOnly
        panel.isReleasedWhenClosed = false
        panel.delegate = self
        panel.contentView = NSHostingView(rootView: DetachedModuleView(module: module).environment(state))
        panel.setFrameAutosaveName("InterviewHacker.Module.\(module.rawValue)")
        if !panel.setFrameUsingName("InterviewHacker.Module.\(module.rawValue)") { panel.center() }
        panels[module] = panel
        panel.makeKeyAndOrderFront(nil)
    }

    func close(_ module: AssistantModule) {
        guard let panel = panels.removeValue(forKey: module) else { return }
        closingByDock.insert(module)
        panel.close()
        closingByDock.remove(module)
    }

    func applyPrivacy(_ enabled: Bool) {
        for panel in panels.values { panel.sharingType = enabled ? .none : .readOnly }
    }

    func applyWindowLevel(_ level: NSWindow.Level) {
        for panel in panels.values {
            panel.isFloatingPanel = level == .floating
            panel.level = level
        }
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

    // MARK: NSWindowDelegate

    func windowWillClose(_ notification: Notification) {
        guard let module = module(of: notification.object as? NSWindow), !closingByDock.contains(module) else { return }
        panels[module] = nil
        AppState.shared.dockModule(module)
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
                }
                .padding(.horizontal, 10).padding(.top, 6)
                if module == .answer {
                    ScrollView(.vertical) {
                        AssistantModuleContent(module: module, detached: true)
                            .fixedSize(horizontal: false, vertical: true)
                            .padding(.horizontal, 10).padding(.bottom, 10)
                    }
                    .scrollIndicators(.automatic)
                } else {
                    AssistantModuleContent(module: module, detached: true)
                        .padding(.horizontal, 10).padding(.bottom, 10)
                }
            }
        }
        .tint(Brand.accent)
        .preferredColorScheme(state.colorScheme)
    }
}
