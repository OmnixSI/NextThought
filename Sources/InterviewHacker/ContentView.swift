import AppKit
import CoreImage.CIFilterBuiltins
import SwiftUI
import UniformTypeIdentifiers
import InterviewHackerCore

/// Root window. Outside a live meeting, global navigation stays in a compact top bar.
/// Live meetings keep their
/// dedicated focus mode (02-scenarios-and-flows.md, "会议中").
struct ContentView: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        ZStack {
            AppBackdrop()
            Group {
                if state.isRunning {
                    FocusModeView()
                        .transition(.opacity)
                } else {
                    MainShell()
                        .transition(.opacity)
                }
            }
            .disabled(state.isOnboardingPresented)
            .accessibilityHidden(state.isOnboardingPresented)
            if #available(macOS 15.0, *) {
                LiveTranslationHosts()
            }
            if state.isOnboardingPresented {
                OnboardingView().transition(.opacity)
            }
        }
        .overlay(alignment: .top) { ErrorBanner() }
        .animation(Motion.phase, value: state.isRunning)
        .frame(minWidth: MainWindowLayout.minimum.width, minHeight: MainWindowLayout.minimum.height)
        .tint(Brand.accent)
        .foregroundStyle(Ink.ink)
        .preferredColorScheme(state.colorScheme)
        // A hard switch between light and dark flashes the whole window. The
        // animation on the appearance value makes the colors cross-fade.
        .animation(.easeInOut(duration: 0.32), value: state.appearance)
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
        // Shown once, before the first recording.
        .alert("开始记录之前", isPresented: Binding(get: { state.recordingNoticePending != nil },
                                              set: { if !$0 { state.recordingNoticePending = nil } })) {
            Button("继续") {
                // Read now: dismissing the alert clears the pending start.
                let pending = state.recordingNoticePending
                Task { await state.confirmRecordingNotice(pending) }
            }
            Button("取消", role: .cancel) { state.recordingNoticePending = nil }
        } message: {
            Text("请确认录音符合当地法律，并告知参会的人。声音只在这台 Mac 上识别，不保存录音。")
        }
    }
}

// MARK: - Shell (outside a meeting)

private struct MainShell: View {
    var body: some View {
        VStack(spacing: 0) {
            AppNavigationBar()
            SectionStack()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
    }
}

/// Every section visited once stays alive and is only hidden when another is shown:
/// switching is instant and scroll positions are kept. Hidden sections are disabled
/// so their text fields and default buttons never take keyboard input.
private struct SectionStack: View {
    @Environment(AppState.self) private var state
    @State private var visited: Set<AppSection> = []

    var body: some View {
        GeometryReader { proxy in
            ZStack {
                ForEach(AppSection.allCases) { section in
                    let shown = section == state.section
                    if shown || visited.contains(section) {
                        page(section)
                            .frame(width: proxy.size.width, height: proxy.size.height)
                            .opacity(shown ? 1 : 0)
                            .allowsHitTesting(shown)
                            .disabled(!shown)
                            .accessibilityHidden(!shown)
                            .zIndex(shown ? 1 : 0)
                    }
                }
            }
        }
        .transaction { $0.animation = nil }
        .onChange(of: state.section, initial: true) { _, section in visited.insert(section) }
    }

    @ViewBuilder private func page(_ section: AppSection) -> some View {
        switch section {
        case .home: HomeView()
        case .meetings: HistoryView().padding(.top, Space.s8)
        case .library: WorkspacesView().padding(.top, Space.s8)
        case .settings: SettingsView().padding(.top, Space.s8)
        }
    }
}

/// Error or warning line, floating at the top of the window in every mode.
private struct ErrorBanner: View {
    @Environment(AppState.self) private var state

    var body: some View {
        if let message = state.errorMessage {
            HStack(alignment: .top, spacing: Space.s2) {
                Image(systemName: "exclamationmark.triangle").foregroundStyle(Ink.caution)
                Text(message).font(Typo.body).textSelection(.enabled).fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: 0)
                Button { state.closeError() } label: { Image(systemName: "xmark") }
                    .buttonStyle(GlassIconButtonStyle(size: 24)).help("关闭提示")
            }
            .padding(Space.s3)
            .frame(maxWidth: 560)
            .floatingSurface(cornerRadius: Space.radiusMedium)
            .padding(.top, Space.s3)
            .transition(.opacity)
        }
    }
}

// MARK: - Focus mode (during a meeting)

/// While a meeting runs the window belongs to it alone.
private struct FocusModeView: View {
    var body: some View {
        LiveSessionView()
    }
}

/// Two small bars: the other side (system audio) and me (microphone). Read at 8 Hz
/// from the audio threads' latest values, so audio never drives SwiftUI directly.
struct LevelMeters: View {
    @Environment(AppState.self) private var state
    var height: CGFloat = 16

    var body: some View {
        TimelineView(.periodic(from: .now, by: 0.125)) { _ in
            let levels = state.echoMonitor.levels
            let echo = state.echoSuppression && state.echoMonitor.microphoneIsEcho
            HStack(spacing: 3) {
                meter(levels.system, color: Ink.secondary).help("对方 · 系统声音")
                meter(levels.microphone, color: echo ? Ink.caution : Ink.ink).help(echo ? "麦克风里主要是扬声器回声（已忽略）" : "我 · 麦克风")
            }
        }
        .frame(height: height)
    }

    private func meter(_ level: Float, color: Color) -> some View {
        ZStack(alignment: .bottom) {
            Capsule().fill(Ink.sunken)
            Capsule().fill(color).frame(height: max(2, CGFloat(level) * height))
        }
        .frame(width: 4, height: height)
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
            Text("下面是 ScreenCaptureKit 截取的整个屏幕，与 Zoom、Teams、Meet 等共享整屏时可能捕获的范围相近。如果能看到回答浮窗，整屏共享时其他参与者也可能看到；实际范围取决于会议软件和共享方式。")
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

/// Listening indicator. Static on purpose: a looping animation forces the window
/// server to recomposite every glass layer in the window ~60–120 times a second.
struct PulseDot: View {
    var active: Bool
    var body: some View {
        Circle().fill(active ? Ink.live : Ink.tertiary).frame(width: 7, height: 7)
            .frame(width: 14, height: 14)
    }
}
