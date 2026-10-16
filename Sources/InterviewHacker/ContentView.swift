import AppKit
import CoreImage.CIFilterBuiltins
import SwiftUI
import UniformTypeIdentifiers
import InterviewHackerCore

/// Root window: a floating top bar (sections, live session controls) over one section.
/// Each section and bar element is its own view, so with Observation a change in the
/// transcript or the streaming answer redraws only the views that display it.
struct ContentView: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        ZStack {
            AppBackdrop()
            SectionStack()
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .safeAreaInset(edge: .top, spacing: 0) { TopBar() }
            if state.isOnboardingPresented {
                OnboardingView().transition(.opacity)
            }
        }
        .frame(minWidth: MainWindowLayout.minimum.width, minHeight: MainWindowLayout.minimum.height)
        .tint(Brand.accent)
        .foregroundStyle(.primary)
        .preferredColorScheme(state.colorScheme)
        .sheet(isPresented: $state.isWindowPickerPresented) { WindowPickerSheet() }
        .sheet(isPresented: $state.isRegionCapturePresented) {
            if let image = state.regionCaptureImage {
                RegionScreenshotPicker(
                    image: image,
                    displays: state.screenshotDisplays,
                    selectedDisplayID: state.selectedScreenshotDisplayID,
                    onDisplaySelected: state.selectScreenshotDisplay,
                    onCancel: state.cancelRegionPicker,
                    onCapture: state.commitRegionScreenshot)
            } else {
                ProgressView("正在读取显示器…").frame(width: 420, height: 240)
            }
        }
        .sheet(isPresented: Binding(get: { state.shareTestImage != nil }, set: { if !$0 { state.shareTestImage = nil } })) {
            ShareTestSheet()
        }
    }
}

// MARK: - Sections

/// Every section visited once stays alive and is only hidden when another is shown:
/// switching is instant (no rebuild, no cross-fade) and scroll positions are kept.
/// Hidden sections are disabled so their text fields and default buttons never take
/// keyboard input.
private struct SectionStack: View {
    @Environment(AppState.self) private var state
    @State private var visited: Set<AppSection> = []

    var body: some View {
        ZStack {
            ForEach(AppSection.allCases) { section in
                let shown = section == state.section
                if shown || visited.contains(section) {
                    page(section)
                        .opacity(shown ? 1 : 0)
                        .allowsHitTesting(shown)
                        .disabled(!shown)
                        .accessibilityHidden(!shown)
                        .zIndex(shown ? 1 : 0)
                }
            }
        }
        .transaction { $0.animation = nil }
        .onChange(of: state.section, initial: true) { _, section in visited.insert(section) }
    }

    @ViewBuilder private func page(_ section: AppSection) -> some View {
        switch section {
        case .assistant: AssistantView()
        case .materials: WorkspacesView()
        case .history: HistoryView()
        case .settings: SettingsView()
        }
    }
}

// MARK: - Top bar

private struct TopBar: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        VStack(spacing: 8) {
            GlassGroup(spacing: 10) {
                HStack(spacing: 10) {
                    // Room for the window's traffic-light buttons (hidden title bar).
                    Color.clear.frame(width: 70, height: 1)
                    GlassTabBar(tabs: AppSection.allCases, selection: $state.section, title: \.rawValue, symbol: \.symbol)
                    Spacer(minLength: 6)
                    UpdateBadge()
                    SessionStatusPill()
                    SessionControls()
                    Button { TeleprompterController.shared.toggle() } label: { Image(systemName: "rectangle.topthird.inset.filled") }
                        .buttonStyle(GlassIconButtonStyle(size: UI.barHeight))
                        .help("显示 / 隐藏提词器 · \(state.shortcutLabel(.toggleTeleprompter))")
                }
            }
            if let message = state.errorMessage {
                HStack(alignment: .top, spacing: 10) {
                    Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.orange)
                    Text(message).font(.callout).textSelection(.enabled).fixedSize(horizontal: false, vertical: true)
                    Spacer(minLength: 0)
                    Button { withAnimation { state.closeError() } } label: { Image(systemName: "xmark") }
                        .buttonStyle(GlassIconButtonStyle(size: 26)).help("关闭提示")
                }
                .padding(12)
                .glassBackground(cornerRadius: 18, tint: Color.orange.opacity(0.25))
                .transition(.move(edge: .top).combined(with: .opacity))
            }
        }
        .padding(.horizontal, 14).padding(.top, 8).padding(.bottom, 10)
        .background(alignment: .top) { ScrollEdgeFade(edge: .top, height: 70).ignoresSafeArea() }
        .animation(.spring(response: 0.35, dampingFraction: 0.85), value: state.errorMessage)
    }
}

/// Listening state, elapsed time and live input levels.
private struct SessionStatusPill: View {
    @Environment(AppState.self) private var state

    var body: some View {
        HStack(spacing: 7) {
            PulseDot(active: state.isRunning && !state.isPaused)
            if state.isRunning, let started = state.sessionStartedAt {
                // Only this text redraws every second.
                TimelineView(.periodic(from: .now, by: 1)) { context in
                    Text(Self.elapsed(from: started, to: context.date))
                        .font(.system(size: 13, weight: .medium).monospacedDigit())
                        .foregroundStyle(.secondary)
                }
                if !state.isPaused { LevelMeters() }
            } else {
                Text(state.isChangingSession ? "正在启动…" : "未开始")
                    .font(.system(size: 13, weight: .medium)).foregroundStyle(.secondary)
            }
        }
        .padding(.horizontal, 14).frame(height: UI.barHeight)
        .glassCapsule()
        .help(state.isRunning ? state.transcriptionStatus : state.readinessText)
    }

    static func elapsed(from start: Date, to now: Date) -> String {
        let seconds = max(0, Int(now.timeIntervalSince(start)))
        return seconds >= 3_600 ? String(format: "%d:%02d:%02d", seconds / 3_600, seconds / 60 % 60, seconds % 60)
            : String(format: "%02d:%02d", seconds / 60, seconds % 60)
    }
}

/// Two small bars: the other side (system audio) and me (microphone). Read at 8 Hz
/// from the audio threads' latest values, so audio never drives SwiftUI directly.
struct LevelMeters: View {
    @Environment(AppState.self) private var state

    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.125)) { _ in
            let levels = state.echoMonitor.levels
            let echo = state.echoSuppression && state.echoMonitor.microphoneIsEcho
            HStack(spacing: 3) {
                meter(levels.system, color: .secondary).help("对方 · 系统声音")
                meter(levels.microphone, color: echo ? .orange : Brand.accent).help(echo ? "麦克风里主要是扬声器回声（已忽略）" : "我 · 麦克风")
            }
        }
        .frame(height: 16)
    }

    private func meter(_ level: Float, color: Color) -> some View {
        ZStack(alignment: .bottom) {
            Capsule().fill(Color.primary.opacity(0.1))
            Capsule().fill(color).frame(height: max(2, CGFloat(level) * 16))
        }
        .frame(width: 4, height: 16)
    }
}

/// Start / pause / stop, always reachable from every section.
private struct SessionControls: View {
    @Environment(AppState.self) private var state

    var body: some View {
        HStack(spacing: 6) {
            if !state.isRunning {
                Button { Task { await state.startSession() } } label: {
                    HStack(spacing: 6) {
                        if state.isChangingSession { ProgressView().controlSize(.small).tint(.white) }
                        Label(state.isChangingSession ? "启动中" : "开始", systemImage: "play.fill")
                    }
                }
                .buttonStyle(PrimaryButtonStyle(height: UI.barHeight))
                .disabled(!state.isReadyToStart || state.isPreparingModel || state.isChangingSession)
                .help(state.isReadyToStart ? "开始实时辅助 · \(state.shortcutLabel(.toggleSession))" : state.readinessText)
            } else {
                Button { Task { await state.togglePause() } } label: { Image(systemName: state.isPaused ? "play.fill" : "pause.fill") }
                    .buttonStyle(GlassIconButtonStyle(size: UI.barHeight, tint: state.isPaused ? Brand.accent : nil))
                    .help((state.isPaused ? "恢复" : "暂停") + " · \(state.shortcutLabel(.togglePause))")
                Button { Task { await state.stopSession() } } label: { Image(systemName: "stop.fill") }
                    .buttonStyle(GlassIconButtonStyle(size: UI.barHeight))
                    .help("停止并保存记录 · \(state.shortcutLabel(.toggleSession))")
            }
        }
        .disabled(state.isChangingSession && state.isRunning)
    }
}

/// Appears when an update is downloaded and ready to install.
private struct UpdateBadge: View {
    @Environment(AppState.self) private var state
    private var updater: Updater { Updater.shared }

    var body: some View {
        if updater.phase == .ready, let available = updater.available {
            Button { updater.installAndRelaunch() } label: {
                Label("重启以更新", systemImage: "arrow.down.circle.fill")
            }
            .buttonStyle(ChipButtonStyle(prominent: true))
            .disabled(state.isRunning)
            .help(state.isRunning ? "停止辅助后再安装 \(available.displayVersion)" : "安装 \(available.displayVersion) 并重新打开")
        }
    }
}

// MARK: - Sheets

private struct WindowPickerSheet: View {
    @Environment(AppState.self) private var state
    var body: some View {
        NavigationStack {
            List(state.screenshotWindows) { window in
                Button { state.captureScreenshot(window) } label: {
                    VStack(alignment: .leading, spacing: 3) {
                        Text(window.title).foregroundStyle(.primary).lineLimit(2)
                        Text(window.owner).font(.caption).foregroundStyle(.secondary)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
            }
            .navigationTitle("选择要截图的窗口")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("取消") { state.cancelWindowPicker() } }
            }
        }
        .frame(width: 420, height: 520)
    }
}

private struct ShareTestSheet: View {
    @Environment(AppState.self) private var state
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("共享可见性测试").font(.headline)
            Text("下面是用 ScreenCaptureKit 截取整个屏幕的结果，与 Zoom / Teams / Meet 等共享整屏时的取图方式相同。如果能看到提词器，说明共享整屏时对方也可能看到；请改为只共享会议窗口或标签页。")
                .font(.callout).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            if let image = state.shareTestImage {
                Image(nsImage: image).resizable().scaledToFit().frame(maxHeight: 420)
                    .clipShape(RoundedRectangle(cornerRadius: 8))
            }
            HStack { Spacer(); Button("关闭") { state.shareTestImage = nil }.keyboardShortcut(.defaultAction) }
        }
        .padding(20).frame(width: 640)
    }
}

struct QRCodeView: View {
    let text: String
    var body: some View {
        if let image = Self.render(text) {
            Image(nsImage: image).interpolation(.none).resizable().scaledToFit()
                .padding(6).background(Color.white, in: RoundedRectangle(cornerRadius: 8))
        } else {
            Image(systemName: "qrcode").font(.largeTitle)
        }
    }

    static func render(_ text: String) -> NSImage? {
        let filter = CIFilter.qrCodeGenerator()
        filter.message = Data(text.utf8)
        filter.correctionLevel = "M"
        guard let output = filter.outputImage?.transformed(by: CGAffineTransform(scaleX: 8, y: 8)) else { return nil }
        let representation = NSCIImageRep(ciImage: output)
        let image = NSImage(size: representation.size)
        image.addRepresentation(representation)
        return image
    }
}

extension AppSection {
    var symbol: String {
        switch self {
        case .assistant: "waveform"
        case .materials: "square.stack.3d.up"
        case .settings: "gearshape"
        case .history: "clock"
        }
    }
}

/// Listening indicator. Static on purpose: a looping animation forces the window
/// server to recomposite every glass layer in the window ~60–120 times a second.
struct PulseDot: View {
    var active: Bool
    var body: some View {
        ZStack {
            if active { Circle().fill(Color.green.opacity(0.25)).frame(width: 14, height: 14) }
            Circle().fill(active ? Color.green : Color.secondary.opacity(0.6)).frame(width: 7, height: 7)
        }
        .frame(width: 14, height: 14)
    }
}
