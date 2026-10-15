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

    enum Placement: String, CaseIterable, Identifiable {
        case camera, bottom, right
        var id: String { rawValue }
        var title: String {
            switch self {
            case .camera: "摄像头下方"
            case .bottom: "屏幕底部"
            case .right: "右侧"
            }
        }
    }

    var isVisible: Bool { panel?.isVisible == true }

    func toggle() {
        if let panel, panel.isVisible { panel.orderOut(nil); return }
        show()
    }

    func show() {
        if panel == nil {
            let next = OverlayPanel(contentRect: NSRect(x: 0, y: 0, width: 640, height: 300), styleMask: [.titled, .closable, .resizable, .nonactivatingPanel, .fullSizeContentView], backing: .buffered, defer: false)
            next.title = "InterviewHacker 提词器"
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
            next.hasShadow = true
            next.contentView = NSHostingView(rootView: TeleprompterView().environment(AppState.shared))
            panel = next
        }
        applyAppearance()
        applyClickThrough(AppState.shared.clickThrough)
        // Does not activate the app or move keyboard focus away from the meeting.
        panel?.orderFrontRegardless()
    }

    func showOnlyTeleprompter() {
        show()
        NSApp.windows.filter { !($0 is NSPanel) && $0.isVisible }.forEach { $0.orderOut(nil) }
    }

    func showWorkbench() {
        NSApp.windows.first(where: { !($0 is NSPanel) })?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

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
        // Close to the built-in camera so reading looks like eye contact.
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
        let opacity = min(1, max(0.35, state.teleprompterOpacity))
        panel?.alphaValue = opacity >= 0.99 ? 1 : CGFloat(opacity)
        applyPrivacy(state.capturePrivacy)
    }

    func applyPrivacy(_ enabled: Bool) {
        for window in NSApp.windows { window.sharingType = enabled ? .none : .readOnly }
        panel?.sharingType = enabled ? .none : .readOnly
    }

    func applyClickThrough(_ enabled: Bool) {
        panel?.ignoresMouseEvents = enabled
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
    }
}

/// Panel subclass that can still become key when the user clicks a button in it,
/// without activating the whole application.
private final class OverlayPanel: NSPanel {
    override var canBecomeKey: Bool { true }
}

private struct TeleprompterView: View {
    @Environment(AppState.self) private var state
    @State private var hovering = false

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            header
            if !state.displayedQuestion.isEmpty || state.isAnswering {
                Text(state.displayedQuestion.isEmpty ? state.pendingQuestion : state.displayedQuestion)
                    .font(.system(size: max(12, state.teleprompterFontSize * 0.6), weight: .medium))
                    .foregroundStyle(.secondary).lineLimit(2)
                    .padding(.horizontal, 12).padding(.vertical, 6)
                    .glassCapsule(tint: Brand.accent.opacity(0.15))
            }
            ScrollViewReader { proxy in
                ScrollView {
                    VStack(alignment: .leading, spacing: 0) {
                        if state.displayedAnswer.isEmpty {
                            Text(state.isAnswering ? "正在生成…" : "回答会显示在这里。可拖到摄像头下方或第二块屏幕；\(state.shortcutLabel(.forceAnswer)) 立即回答。")
                                .font(.system(size: state.teleprompterFontSize * 0.75))
                                .foregroundStyle(.secondary)
                        } else {
                            MarkdownText(text: state.displayedAnswer, size: state.teleprompterFontSize, lineSpacing: state.teleprompterFontSize * 0.3).equatable()
                        }
                        Color.clear.frame(height: 1).id("end")
                    }
                }
                .onChange(of: state.answerDraft) { _, _ in
                    if state.isAnswering { proxy.scrollTo("end", anchor: .bottom) }
                }
            }
            if hovering || !state.clickThrough { footer }
        }
        .padding(18)
        .glassBackground(cornerRadius: 26)
        .padding(2)
        .foregroundStyle(.primary)
        .preferredColorScheme(state.colorScheme)
        .onHover { hovering = $0 }
    }

    private var header: some View {
        GlassGroup(spacing: 6) { HStack(spacing: 8) {
            PulseDot(active: state.isRunning && !state.isPaused)
            Text(state.isRunning ? (state.isPaused ? "已暂停" : "正在聆听") : "未采集").font(.caption)
            if state.isAnswering { ProgressView().controlSize(.mini) }
            if let first = state.lastFirstTokenSeconds, !state.isAnswering {
                Text(String(format: "首字 %.1fs", first)).font(.caption2).foregroundStyle(.secondary)
            }
            if let candidate = state.liveCandidate, candidate.state != .triggered {
                Text("· \(candidate.assessment.intent.title)：\(candidate.text)").font(.caption).foregroundStyle(.secondary).lineLimit(1)
            }
            Spacer()
            if let position = state.historyPositionText {
                Button { state.showPreviousAnswer() } label: { Image(systemName: "chevron.left") }.buttonStyle(GlassIconButtonStyle(size: 26))
                Text(position).font(.caption.monospacedDigit()).foregroundStyle(.secondary)
                Button { state.showNextAnswer() } label: { Image(systemName: "chevron.right") }.buttonStyle(GlassIconButtonStyle(size: 26))
            }
            Button { state.teleprompterFontSize = max(14, state.teleprompterFontSize - 2); state.saveAnswerSettings() } label: { Image(systemName: "textformat.size.smaller") }
                .buttonStyle(GlassIconButtonStyle(size: 26)).help("缩小文字")
            Button { state.teleprompterFontSize = min(44, state.teleprompterFontSize + 2); state.saveAnswerSettings() } label: { Image(systemName: "textformat.size.larger") }
                .buttonStyle(GlassIconButtonStyle(size: 26)).help("放大文字")
            Button { state.toggleClickThrough() } label: { Image(systemName: state.clickThrough ? "cursorarrow.slash" : "cursorarrow") }
                .buttonStyle(GlassIconButtonStyle(size: 26, tint: state.clickThrough ? Brand.accent : nil)).help("鼠标穿透 · \(state.shortcutLabel(.toggleClickThrough))")
            Button { TeleprompterController.shared.toggle() } label: { Image(systemName: "xmark") }
                .buttonStyle(GlassIconButtonStyle(size: 26)).help("隐藏提词器 · \(state.shortcutLabel(.toggleTeleprompter))")
        } }
    }

    private var footer: some View {
        VStack(alignment: .leading, spacing: 8) {
            RefinementBar(compact: true)
            HStack {
                Button { state.forceAnswerCurrent() } label: { Label("回答", systemImage: "bolt.fill") }.buttonStyle(ChipButtonStyle(prominent: true))
                Button { state.cancelAnswer() } label: { Label("取消", systemImage: "stop.fill") }.buttonStyle(ChipButtonStyle()).disabled(!state.isAnswering)
                Button { state.copyAnswer() } label: { Label("复制", systemImage: "doc.on.doc") }.buttonStyle(ChipButtonStyle())
                Button("工作台") { TeleprompterController.shared.showWorkbench() }.buttonStyle(ChipButtonStyle())
                Spacer()
                Text("\(state.shortcutLabel(.forceAnswer)) 回答 · \(state.shortcutLabel(.panicHide)) 隐藏").font(.caption2).foregroundStyle(.secondary)
            }
        }
    }
}
