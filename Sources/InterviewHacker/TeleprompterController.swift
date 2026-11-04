import AppKit
import SwiftUI
import InterviewHackerCore

/// Non-activating floating overlay. It never takes keyboard focus from the meeting
/// app, can be made click-through and semi-transparent, and is excluded from
/// screen capture on a best-effort basis (see `applyAppearance`).
@MainActor
final class TeleprompterController {
    static let shared = TeleprompterController()
    private var panel: NSPanel?
    private var panicHidden: [NSWindow] = []
    /// Whether the panel is click-through right now.
    private var clickThroughEnabled = false
    /// A tiny always-interactive panel that floats on top of the click-through
    /// button.
    ///
    /// `NSPanel.ignoresMouseEvents` applies to a whole window, so a
    /// click-through panel cannot keep one control alive on its own. Giving that
    /// control its own window is the only way to let everything else in the
    /// teleprompter pass clicks through while the button itself stays usable.
    private var controlPanel: NSPanel?
    /// Screen frame of the click-through button, in AppKit screen coordinates.
    private var controlScreenFrame: CGRect = .zero
    /// Recomputes the control frame; installed by the view hosting the button.
    var refreshControlFrame: (() -> Void)?
    private var moveObservers: [NSObjectProtocol] = []

    enum Placement: String, CaseIterable, Identifiable {
        case camera, bottom, right
        var id: String { rawValue }
        var title: String {
            switch self {
            case .camera: String(localized: "屏幕顶部居中")
            case .bottom: String(localized: "屏幕底部")
            case .right: String(localized: "右侧")
            }
        }
    }

    var isVisible: Bool { panel?.isVisible == true }

    func toggle() {
        if let panel, panel.isVisible {
            panel.orderOut(nil)
            controlPanel?.orderOut(nil)
            return
        }
        show()
    }

    func show() {
        if panel == nil {
            let next = OverlayPanel(contentRect: NSRect(x: 0, y: 0, width: 640, height: 300), styleMask: [.titled, .closable, .resizable, .nonactivatingPanel, .fullSizeContentView], backing: .buffered, defer: false)
            next.title = String(localized: "InterviewHacker 回答浮窗")
            next.titlebarAppearsTransparent = true
            next.titleVisibility = .hidden
            next.isFloatingPanel = true
            next.level = .floating
            next.hidesOnDeactivate = false
            next.isReleasedWhenClosed = false
            next.becomesKeyOnlyIfNeeded = true
            next.isMovableByWindowBackground = true
            next.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
            next.minSize = NSSize(width: 360, height: 160)
            next.backgroundColor = .clear
            next.isOpaque = false
            next.setFrameAutosaveName("InterviewHacker.Teleprompter")
            if !next.setFrameUsingName("InterviewHacker.Teleprompter") { place(next, .camera) }
            // The glass panel draws its own chrome; hide the floating traffic lights.
            for button in [NSWindow.ButtonType.closeButton, .miniaturizeButton, .zoomButton] {
                next.standardWindowButton(button)?.isHidden = true
            }
            // System window shadow: computed by the window server, free while dragging.
            // A fully transparent window must not cast one, or the border would
            // sit on a dark halo.
            next.hasShadow = !AppState.shared.teleprompterHideBackground
            next.contentView = NSHostingView(rootView: TeleprompterView().environment(AppState.shared))
            panel = next
            controlPanel = makeControlPanel(level: next.level)
            // The control frame is in screen coordinates: it has to follow the
            // panel when the user drags it.
            moveObservers = [NSWindow.didMoveNotification, NSWindow.didResizeNotification].map { name in
                NotificationCenter.default.addObserver(forName: name, object: next, queue: .main) { [weak self] _ in
                    MainActor.assumeIsolated { self?.refreshControlFrame?() }
                }
            }
        }
        applyAppearance()
        applyClickThrough(AppState.shared.clickThrough)
        // Does not activate the app or move keyboard focus away from the meeting.
        panel?.orderFrontRegardless()
        // The control panel has to end up in front of the teleprompter.
        updateControlPanelVisibility()
    }

    /// The small always-interactive panel that carries the click-through button
    /// while the teleprompter is click-through.
    private func makeControlPanel(level: NSWindow.Level) -> NSPanel {
        let control = NSPanel(contentRect: .zero, styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        control.isFloatingPanel = true
        control.level = level
        control.hidesOnDeactivate = false
        control.isReleasedWhenClosed = false
        control.becomesKeyOnlyIfNeeded = true
        control.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
        control.backgroundColor = .clear
        control.isOpaque = false
        control.hasShadow = false
        // The point of this window: it is never click-through.
        control.ignoresMouseEvents = false
        let overlay = ControlOverlayView()
        overlay.onClick = { AppState.shared.toggleClickThrough() }
        control.contentView = overlay
        return control
    }

    func showOnlyTeleprompter() {
        // In a meeting the live window mode owns the page and the subtitle bar.
        AppState.shared.liveWindow.set(.hidden)
        show()
        NSApp.windows.filter { !($0 is NSPanel) && $0.isVisible }.forEach { $0.orderOut(nil) }
    }

    /// Brings the main window back (from the subtitle bar or hidden mode too).
    /// Not `first(where: !NSPanel)`: that could be the settings window.
    func showWorkbench() {
        AppState.shared.liveWindow.set(.notes)
        MainWindowLayout.bringToFront()
    }

    /// The emergency "hide everything" shortcut is on.
    var isPanicHidden: Bool { !panicHidden.isEmpty }

    func move(to placement: Placement) {
        show()
        if let panel { place(panel, placement) }
    }

    private func place(_ panel: NSPanel, _ placement: Placement) {
        guard let screen = NSScreen.main else { return }
        let area = screen.visibleFrame
        let size = panel.frame.size
        let origin: NSPoint
        switch placement {
        // Keep the default answer window at the top center of the display.
        case .camera: origin = NSPoint(x: area.midX - size.width / 2, y: area.maxY - size.height - 8)
        case .bottom: origin = NSPoint(x: area.midX - size.width / 2, y: area.minY + 24)
        case .right: origin = NSPoint(x: area.maxX - size.width - 16, y: area.midY - size.height / 2)
        }
        panel.setFrameOrigin(origin)
    }

    /// Opacity and capture exclusion for every app window.
    /// `sharingType = .none` hides windows from legacy capture and many meeting apps,
    /// but Apple states there is no public API that guarantees exclusion; on macOS 15+
    /// ScreenCaptureKit-based recorders may still capture it. Use the share test.
    func applyAppearance() {
        let state = AppState.shared
        // With the background hidden the window itself must stay fully opaque:
        // the opacity slider may not fade the text, the border or the buttons.
        // The transparency then lives in the background layer alone.
        let opacity = state.teleprompterHideBackground ? 1.0 : min(1, max(0.35, state.teleprompterOpacity))
        panel?.alphaValue = opacity >= 0.99 ? 1 : CGFloat(opacity)
        applyPrivacy(state.capturePrivacy)
    }

    func applyPrivacy(_ enabled: Bool) {
        for window in NSApp.windows { window.sharingType = enabled ? .none : .readOnly }
        panel?.sharingType = enabled ? .none : .readOnly
        SubtitleBarController.shared.applyPrivacy(enabled)
    }

    /// Click-through, with one exception: the button that turns it off.
    ///
    /// `ignoresMouseEvents` applies to a whole window, so a click-through panel
    /// cannot keep one control alive on its own. The button therefore gets its
    /// own tiny window that floats above the teleprompter: every other part of
    /// the overlay passes clicks through to the meeting app, while the button
    /// stays pressable — so click-through can always be turned off again.
    func applyClickThrough(_ enabled: Bool) {
        clickThroughEnabled = enabled
        panel?.ignoresMouseEvents = enabled
        updateControlPanelVisibility()
    }

    /// The screen frame of the click-through button.
    func setControlFrame(_ frame: CGRect) {
        // The two corners arrive in opposite orders (screen coordinates grow
        // upwards), so the rect has to be normalized before it is used.
        controlScreenFrame = frame.standardized
        updateControlPanelVisibility()
    }

    /// Shows the control panel exactly over the button while click-through is
    /// on, and hides it otherwise (then the button in the panel itself works).
    private func updateControlPanelVisibility() {
        guard let control = controlPanel else { return }
        guard clickThroughEnabled, controlScreenFrame != .zero else {
            control.orderOut(nil)
            return
        }
        control.setFrame(controlScreenFrame, display: true)
        control.orderFrontRegardless()
    }

    /// One key hides everything; pressing it again restores the same windows.
    func panicHide() {
        if !panicHidden.isEmpty {
            panicHidden.forEach { $0.orderFrontRegardless() }
            panicHidden.removeAll()
            return
        }
        panicHidden = NSApp.windows.filter(\.isVisible)
        panicHidden.forEach { $0.orderOut(nil) }
        controlPanel?.orderOut(nil)
    }
}

/// Panel subclass that can still become key when the user clicks a button in it,
/// without activating the whole application.
private final class OverlayPanel: NSPanel {
    override var canBecomeKey: Bool { true }
}

/// An invisible view that catches the click on the click-through button while
/// the teleprompter itself passes every click through.
private final class ControlOverlayView: NSView {
    var onClick: (() -> Void)?
    override func mouseDown(with event: NSEvent) { onClick?() }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override func hitTest(_ point: NSPoint) -> NSView? { self }
}

private struct TeleprompterView: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            header
            ScrollViewReader { proxy in
                ScrollView {
                    VStack(alignment: .leading, spacing: 0) {
                        if state.displayedAnswer.isEmpty {
                            Text(state.isAnswering ? "正在生成…" : "回答会显示在这里。你也可以按 \(state.shortcutLabel(.forceAnswer)) 手动请求回答。")
                                .font(.system(size: state.teleprompterFontSize * 0.75))
                                .foregroundStyle(Ink.secondary)
                        } else {
                            MarkdownText(text: state.displayedAnswer, size: state.teleprompterFontSize, lineSpacing: state.teleprompterFontSize * 0.45).equatable()
                        }
                        Color.clear.frame(height: 1).id("end")
                    }
                }
                .onChange(of: state.answerDraft) { _, _ in
                    if state.isAnswering { proxy.scrollTo("end", anchor: .bottom) }
                }
            }
            // In click-through mode the panel ignores the mouse, so the footer
            // could never be used; only show it when the panel is clickable.
            if !state.clickThrough { footer }
        }
        .padding(18)
        .background {
            if state.teleprompterHideBackground {
                // The background disappears completely; only the text, the
                // border and the buttons are left, each with its own opacity.
                Color.clear
            } else {
                // A floating layer: raised surface and a hairline; the window
                // server draws the shadow.
                RoundedRectangle(cornerRadius: Space.radiusLarge, style: .continuous)
                    .fill(Ink.surfaceRaised)
                    .overlay(RoundedRectangle(cornerRadius: Space.radiusLarge, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 0.5))
            }
        }
        .overlay {
            if state.teleprompterHideBackground {
                // A fully opaque border keeps the panel readable over a busy screen.
                RoundedRectangle(cornerRadius: Space.radiusLarge, style: .continuous)
                    .strokeBorder(Ink.ink, lineWidth: 1.5)
            }
        }
        .padding(2)
        .foregroundStyle(Ink.ink)
        .preferredColorScheme(state.colorScheme)
        .animation(.easeInOut(duration: 0.32), value: state.appearance)
    }

    /// "● 正在听  如果删除缓存失败了怎么办？" plus the panel controls.
    private var header: some View {
        HStack(spacing: 8) {
            Circle().fill(state.isRunning && !state.isPaused ? Ink.live : Ink.tertiary).frame(width: 7, height: 7)
            Text(state.isRunning ? (state.isPaused ? "已暂停" : "正在听") : "未在听")
                .font(.system(size: 12, weight: .medium))
            let question = state.displayedQuestion.isEmpty ? state.pendingQuestion : state.displayedQuestion
            if !question.isEmpty {
                Text(verbatim: AnswerRefinement.originalQuestion(of: question))
                    .font(.system(size: 12)).foregroundStyle(Ink.secondary).lineLimit(1)
            } else if let candidate = state.liveCandidate, candidate.state != .triggered {
                Text(verbatim: candidate.text).font(.system(size: 12)).foregroundStyle(Ink.secondary).lineLimit(1)
            }
            if state.isAnswering { ProgressView().controlSize(.mini) }
            Spacer()
            if let position = state.historyPositionText {
                Button { state.showPreviousAnswer() } label: { Image(systemName: "chevron.left") }.buttonStyle(GlassIconButtonStyle(size: 24))
                Text(verbatim: position).font(.caption.monospacedDigit()).foregroundStyle(Ink.secondary)
                Button { state.showNextAnswer() } label: { Image(systemName: "chevron.right") }.buttonStyle(GlassIconButtonStyle(size: 24))
            }
            Button { state.teleprompterFontSize = max(14, state.teleprompterFontSize - 2); state.saveAnswerSettings() } label: { Image(systemName: "textformat.size.smaller") }
                .buttonStyle(GlassIconButtonStyle(size: 24)).help("缩小文字")
            Button { state.teleprompterFontSize = min(44, state.teleprompterFontSize + 2); state.saveAnswerSettings() } label: { Image(systemName: "textformat.size.larger") }
                .buttonStyle(GlassIconButtonStyle(size: 24)).help("放大文字")
            // This button is the way out of click-through, so it must keep
            // receiving clicks even when the rest of the panel does not.
            Button { state.toggleClickThrough() } label: { Image(systemName: state.clickThrough ? "cursorarrow.slash" : "cursorarrow") }
                .buttonStyle(GlassIconButtonStyle(size: 24, tint: state.clickThrough ? Brand.accent : nil))
                .background(ControlFrameReporter())
                .help("鼠标穿透 · \(state.shortcutLabel(.toggleClickThrough))")
            Button { TeleprompterController.shared.toggle() } label: { Image(systemName: "xmark") }
                .buttonStyle(GlassIconButtonStyle(size: 24)).help("隐藏回答浮窗 · \(state.shortcutLabel(.toggleTeleprompter))")
        }
    }

    /// Three rewrites, "…" (copy, main window, the rest) and one answer / stop button.
    private var footer: some View {
        HStack(spacing: 4) {
            ForEach([AnswerRefinement.shorter, .example, .english]) { item in
                Button { state.refine(item) } label: { Text(verbatim: item.displayName) }
                    .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 8))
                    .disabled(state.displayedAnswer.isEmpty || state.isAnswering)
            }
            AnswerMoreMenu(includesWorkbench: true)
            Spacer()
            if state.isAnswering {
                Button { state.cancelAnswer() } label: { Label("停止生成", systemImage: "stop.fill") }
                    .buttonStyle(SecondaryButtonStyle())
                    .help("\(state.shortcutLabel(.cancelAnswer))")
            } else {
                Button { state.forceAnswerCurrent() } label: { Label("立即回答", systemImage: "bolt.fill") }
                    .buttonStyle(PrimaryButtonStyle())
                    .help("立即回答 · \(state.shortcutLabel(.forceAnswer)) · \(state.shortcutLabel(.panicHide)) 隐藏全部窗口")
            }
        }
    }
}

/// Reports the screen frame of the click-through button, so the panel can tell
/// when the cursor is over it and stay interactive there.
private struct ControlFrameReporter: NSViewRepresentable {
    typealias NSViewType = FrameReportingView

    func makeNSView(context: Context) -> FrameReportingView {
        let view = FrameReportingView()
        // Weak on both sides: the view holds the closure and the singleton
        // controller holds the closure too, so a strong capture would leak the
        // view for the lifetime of the app.
        view.onChange = { [weak view] in
            guard let view, let window = view.window else { return }
            let inWindow = view.convert(view.bounds, to: nil)
            // Two corners, because NSWindow converts points, not rectangles.
            let topLeft = window.convertPoint(toScreen: NSPoint(x: inWindow.minX, y: inWindow.maxY))
            let bottomRight = window.convertPoint(toScreen: NSPoint(x: inWindow.maxX, y: inWindow.minY))
            TeleprompterController.shared.setControlFrame(CGRect(x: topLeft.x, y: topLeft.y,
                                                                 width: bottomRight.x - topLeft.x,
                                                                 height: bottomRight.y - topLeft.y))
        }
        TeleprompterController.shared.refreshControlFrame = { [weak view] in view?.onChange?() }
        return view
    }

    func updateNSView(_ nsView: FrameReportingView, context: Context) {
        nsView.onChange?()
    }
}

/// A view that reports whenever its position in the window changes.
private final class FrameReportingView: NSView {
    var onChange: (() -> Void)?
    override func viewDidMoveToWindow() { super.viewDidMoveToWindow(); onChange?() }
    override func layout() { super.layout(); onChange?() }
}
