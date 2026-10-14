import AppKit
import SwiftUI

@MainActor
final class TeleprompterController {
    static let shared = TeleprompterController()
    private var panel: NSPanel?
    func toggle() {
        if let panel, panel.isVisible { panel.orderOut(nil); return }
        show()
    }
    func show() {
        if panel == nil {
            let next = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 620, height: 290), styleMask: [.titled, .closable, .resizable, .nonactivatingPanel, .fullSizeContentView], backing: .buffered, defer: false)
            next.title = "InterviewHacker 提词器"
            next.titlebarAppearsTransparent = true
            next.isFloatingPanel = true; next.level = .floating
            next.hidesOnDeactivate = false; next.isReleasedWhenClosed = false
            next.isMovableByWindowBackground = true
            next.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
            next.minSize = NSSize(width: 420, height: 210)
            next.setFrameAutosaveName("InterviewHacker.Teleprompter")
            if !next.setFrameUsingName("InterviewHacker.Teleprompter"), let screen = NSScreen.main {
                let area = screen.visibleFrame
                next.setFrameOrigin(NSPoint(x: area.midX - 310, y: area.maxY - 310))
            }
            next.contentView = NSHostingView(rootView: TeleprompterView().environmentObject(AppState.shared))
            panel = next
        }
        applyPrivacy(AppState.shared.capturePrivacy)
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
    func applyPrivacy(_ enabled: Bool) {
        // Best effort ONLY. ScreenCaptureKit and capture software may ignore this.
        panel?.sharingType = enabled ? .none : .readOnly
        for window in NSApp.windows where !(window is NSPanel) { window.sharingType = enabled ? .none : .readOnly }
    }
}

private struct TeleprompterView: View {
    @EnvironmentObject var state: AppState
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Circle().fill(state.isRunning && !state.isPaused ? Color.green : Color.secondary).frame(width: 8, height: 8)
                Text(state.isRunning ? (state.isPaused ? "已暂停" : "正在聆听") : "未采集").font(.caption)
                if state.isAnswering { ProgressView().controlSize(.small); Text("正在生成下一条回答").font(.caption) }
                Spacer()
                Button("工作台") { TeleprompterController.shared.showWorkbench() }.buttonStyle(.borderless)
                Button { state.teleprompterFontSize = max(16, state.teleprompterFontSize - 2) } label: { Image(systemName: "textformat.size.smaller") }.help("缩小文字")
                Button { state.teleprompterFontSize = min(36, state.teleprompterFontSize + 2) } label: { Image(systemName: "textformat.size.larger") }.help("放大文字")
            }
            if !state.currentQuestion.isEmpty { Text(state.displayedQuestion).font(.callout).foregroundStyle(.secondary).lineLimit(2) }
            ScrollView {
                Text(state.displayedAnswer.isEmpty ? "回答会显示在这里。可拖动到摄像头附近，或移动到第二块屏幕。" : state.displayedAnswer)
                    .font(.system(size: state.teleprompterFontSize, weight: .medium))
                    .lineSpacing(7).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading)
            }
            HStack {
                Button("回答最新转写") { state.forceAnswerCurrent() }.buttonStyle(.borderedProminent)
                Button("取消生成") { state.cancelAnswer() }.disabled(!state.isAnswering)
                Spacer()
                Text("\(state.shortcutLabel(.forceAnswer)) 触发 · \(state.shortcutLabel(.toggleTeleprompter)) 隐藏").font(.caption2).foregroundStyle(.secondary)
            }
        }
        .padding(18).padding(.top, 18)
        .background(Color(nsColor: .windowBackgroundColor))
        .foregroundStyle(.primary)
        .preferredColorScheme(state.darkMode ? .dark : .light)
    }
}
