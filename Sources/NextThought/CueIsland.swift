import AppKit
import NextThoughtCore
import Observation
import SwiftUI

// The cue island: suggested answers shown right under the camera.
//
// Reading a window in the middle of the screen makes the eyes drop, and the other
// side sees it. The island hangs from the top edge of the display the camera sits on
// (growing out of the notch on a MacBook), in a narrow column, so the eyes stay
// within a few degrees of the lens while reading. It is a glance surface: clicks
// pass through it unless the pointer rests on it, and it never takes keyboard focus.

enum CueSurface: String, CaseIterable, Identifiable {
    case island, window
    var id: String { rawValue }
    var title: String {
        switch self {
        case .island: String(localized: "提词岛 · 贴着摄像头")
        case .window: String(localized: "浮窗 · 可自由摆放")
        }
    }
}

enum CueWidth: String, CaseIterable, Identifiable {
    case narrow, regular, wide
    var id: String { rawValue }
    var points: CGFloat {
        switch self {
        case .narrow: 440
        case .regular: 540
        case .wide: 660
        }
    }
    var title: String {
        switch self {
        case .narrow: String(localized: "窄")
        case .regular: String(localized: "标准")
        case .wide: String(localized: "宽")
        }
    }
}

enum CueStyleChoice: String, CaseIterable, Identifiable {
    case scene, keyPoints, script
    var id: String { rawValue }
    var title: String {
        switch self {
        case .scene: String(localized: "跟随场景")
        case .keyPoints: String(localized: "要点")
        case .script: String(localized: "逐句")
        }
    }
}

/// Where the island hangs: the notch it grows out of, or the gap under the menu bar.
struct IslandMetrics: Equatable {
    var hasNotch = false
    var notchWidth: CGFloat = 0
    /// Height of the row beside the notch (or of the idle pill).
    var barHeight: CGFloat = 30
    /// Distance from the top of the screen to the island.
    var topOffset: CGFloat = 0
}

@MainActor
@Observable
final class CueIslandModel {
    var surface = CueSurface(rawValue: UserDefaults.standard.string(forKey: "cueSurface") ?? "") ?? .island {
        didSet {
            UserDefaults.standard.set(surface.rawValue, forKey: "cueSurface")
            CueIslandController.shared.refreshPresence()
        }
    }
    var width = CueWidth(rawValue: UserDefaults.standard.string(forKey: "cueWidth") ?? "") ?? .regular {
        didSet { UserDefaults.standard.set(width.rawValue, forKey: "cueWidth") }
    }
    var style = CueStyleChoice(rawValue: UserDefaults.standard.string(forKey: "cueStyle") ?? "") ?? .scene {
        didSet { UserDefaults.standard.set(style.rawValue, forKey: "cueStyle") }
    }
    /// Name of the display the camera sits on; empty picks the built-in display.
    var screenName = UserDefaults.standard.string(forKey: "cueScreen") ?? "" {
        didSet { UserDefaults.standard.set(screenName, forKey: "cueScreen"); CueIslandController.shared.reposition() }
    }

    var metrics = IslandMetrics()
    /// The pointer rests on the island: controls appear and clicks land.
    var hovering = false
    var islandSize: CGSize = .zero
    /// Lines of the current answer the user has already said.
    var covered: Set<Int> = []
    @ObservationIgnored var coverageKey = ""
    /// The answer the user (or time) put away; a new answer opens the island again.
    var collapsedKey: String?
    /// Sample content, for the settings preview and screenshots.
    var preview = false
    var backdrop = false
    /// Outside a meeting, resting the pointer on the notch offers to start one.
    var launcher = UserDefaults.standard.object(forKey: "cueLauncher") as? Bool ?? true {
        didSet { UserDefaults.standard.set(launcher, forKey: "cueLauncher"); CueIslandController.shared.refreshPresence() }
    }
    /// Withdraw into the notch while a meeting app shares the display the island is on.
    var avoidsSharing = UserDefaults.standard.object(forKey: "cueAvoidsSharing") as? Bool ?? true {
        didSet { UserDefaults.standard.set(avoidsSharing, forKey: "cueAvoidsSharing") }
    }
    /// A meeting app is sharing this display right now.
    var shareAvoiding = false
    /// Nothing is drawn: the pointer is tracked over the notch itself.
    @ObservationIgnored var isHidden = true
    /// The island stays open through the meeting: the latest answer in full, without
    /// hovering. ⌘⇧E, the pin on the island, the quick panel or Settings → 提词与教练.
    var alwaysOpen = UserDefaults.standard.bool(forKey: "cueAlwaysOpen") {
        didSet {
            UserDefaults.standard.set(alwaysOpen, forKey: "cueAlwaysOpen")
            if alwaysOpen, AppState.shared.isRunning { CueIslandController.shared.show() }
        }
    }
    /// Dual-screen presenting shapes this meeting (PresenterMode).
    var presenting = false
    /// Open without hovering: chosen by the user, or because the meeting is being presented.
    var keepsOpen: Bool { alwaysOpen || presenting }
    /// Recordings drive the hover state themselves instead of following the pointer.
    @ObservationIgnored var demoHover: Bool?
    /// The deck stays open after a click on its bar, until the next click on the bar or outside.
    var pinned = false
    /// NextThought's own window is the key window on the island's display: the window already
    /// shows the answer, so the island does not open the cue over its top bar by itself.
    var mainWindowFront = false
    /// The user asked for the last cue (上一条) although the window is in front.
    var forceCue = false
    /// The auto-end reminder is counting down: the island takes clicks without a hover dwell.
    var peeking = false
}

@MainActor
final class CueIslandController {
    static let shared = CueIslandController()
    let model = CueIslandModel()
    private var panel: NSPanel?
    private var tracker: Timer?
    private var observers: [NSObjectProtocol] = []
    private static let canvas = CGSize(width: 820, height: 660)

    var isVisible: Bool { panel?.isVisible == true }

    /// In a meeting: show or hide the island. Outside one, on a notch Mac with the launcher on,
    /// open or close the launcher (the island itself stays, as the notch's pointer target).
    func toggle() {
        if !AppState.shared.isRunning, model.surface == .island, model.launcher, hasNotch {
            model.pinned.toggle()
            if model.pinned { show() }
            return
        }
        isVisible ? hide() : show()
    }

    func show() { present(visible: true) }

    /// `visible: false` builds and lays out the panel without putting it on a screen
    /// (off-screen previews).
    func present(visible: Bool) {
        if panel == nil { panel = makePanel() }
        reposition()
        panel?.sharingType = AppState.shared.capturePrivacy ? .none : .readOnly
        guard visible else { return }
        panel?.orderFrontRegardless()
        startTracking()
    }

    func hide() {
        model.preview = false
        model.pinned = false
        // A panel that was taking clicks (deck, peek) goes back to letting them through.
        panel?.ignoresMouseEvents = true
        model.peeking = false
        // With the launcher on, the panel stays (drawing nothing) so the notch keeps
        // answering to the pointer.
        if keepsPresence { model.hovering = false; return }
        panel?.orderOut(nil)
        tracker?.invalidate(); tracker = nil
        model.hovering = false
    }

    /// The island is a standing part of the notch: on when the launcher is.
    private var keepsPresence: Bool {
        model.surface == .island && model.launcher && hasNotch && !AppState.shared.isRunning
    }

    private var hasNotch: Bool {
        (Self.cameraScreen(named: model.screenName)?.safeAreaInsets.top ?? 0) > 0
            || AppEnvironment.values["NEXTTHOUGHT_DEMO_NOTCH"] == "1"
    }

    /// Called at launch and when a preference changes.
    func refreshPresence() {
        guard !model.preview else { return }
        if (model.surface == .island || model.presenting) && (AppState.shared.isRunning || keepsPresence) {
            show()
        } else {
            panel?.ignoresMouseEvents = true
            panel?.orderOut(nil)
            tracker?.invalidate(); tracker = nil
            model.hovering = false; model.pinned = false; model.peeking = false
        }
    }

    /// Follows the meeting: the island is there in every scene (compact until an answer is
    /// asked for) and leaves when the meeting ends.
    func observeSession() {
        let state = AppState.shared
        withObservationTracking { _ = state.isRunning } onChange: { [weak self] in
            Task { @MainActor in
                self?.sessionChanged(running: state.isRunning)
                self?.observeSession()
            }
        }
    }

    private func sessionChanged(running: Bool) {
        let state = AppState.shared
        let mode = (state.preparedWorkspace ?? state.activeWorkspace).effectiveMode
        if running {
            coachArchiveID = state.activeArchiveID
            model.preview = false
            state.coach.begin(mode: mode, startedAt: state.sessionStartedAt ?? Date())
            // A quick recording has no scene of its own: the home page keeps the last real one.
            if mode != .custom, !state.isQuickRecording {
                state.lastScene = mode
                UserDefaults.standard.set(mode.rawValue, forKey: "lastScene")
            }
            model.collapsedKey = nil; model.covered = []; model.forceCue = false
            PresenterMode.shared.meetingStarted()
            PresenterMode.shared.begin()
            if model.surface == .island || model.presenting {
                show()
            } else if state.automaticAnswers {
                // The floating window is the cue surface and nothing else opens it.
                TeleprompterController.shared.showWindow()
            }
        } else {
            PresenterMode.shared.end()
            let summary = state.coach.end()
            if let summary, let id = coachArchiveID { CoachSummaryStore.save(summary, for: id) }
            coachArchiveID = nil
            model.forceCue = false
            // Whatever answer was showing belongs to the meeting that just ended.
            model.collapsedKey = CueIslandView.answerKey(state)
            model.shareAvoiding = false
            if !model.preview { hide() }
            // After a full hide (the toggle shortcut) the launcher has to come back.
            refreshPresence()
        }
    }
    private var coachArchiveID: UUID?

    /// The display the camera sits on.
    static func cameraScreen(named name: String) -> NSScreen? {
        if !name.isEmpty, let chosen = NSScreen.screens.first(where: { $0.localizedName == name }) { return chosen }
        let builtIn = NSScreen.screens.first { screen in
            guard let number = screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber else { return false }
            return CGDisplayIsBuiltin(CGDirectDisplayID(number.uint32Value)) != 0
        }
        return builtIn ?? NSScreen.main ?? NSScreen.screens.first
    }

    func reposition() {
        guard let panel, let screen = Self.cameraScreen(named: model.screenName) else { return }
        var metrics = IslandMetrics()
        let notch = screen.safeAreaInsets.top
        let demoNotch = AppEnvironment.values["NEXTTHOUGHT_DEMO_NOTCH"] == "1"
        if notch > 0 || demoNotch {
            let left = screen.auxiliaryTopLeftArea?.width ?? 0, right = screen.auxiliaryTopRightArea?.width ?? 0
            metrics.hasNotch = true
            // A few points wider than the hardware notch, so no seam shows beside it.
            metrics.notchWidth = demoNotch && notch == 0 ? 186 : max(120, screen.frame.width - left - right + 4)
            metrics.barHeight = demoNotch && notch == 0 ? 32 : notch
            metrics.topOffset = 0
        } else {
            // Under the menu bar, as close to an external camera as the screen allows.
            metrics.barHeight = 30
            metrics.topOffset = screen.frame.maxY - screen.visibleFrame.maxY + 6
        }
        if model.metrics != metrics { model.metrics = metrics }
        let size = Self.canvas
        panel.setFrame(NSRect(x: screen.frame.midX - size.width / 2, y: screen.frame.maxY - size.height,
                              width: size.width, height: size.height), display: true)
    }

    private func makePanel() -> NSPanel {
        let panel = IslandPanel(contentRect: NSRect(origin: .zero, size: Self.canvas),
                                styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.isFloatingPanel = true
        // Above the menu bar, so the island can sit flush with the notch.
        panel.level = NSWindow.Level(rawValue: NSWindow.Level.mainMenu.rawValue + 2)
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]
        panel.hidesOnDeactivate = false
        panel.isReleasedWhenClosed = false
        panel.becomesKeyOnlyIfNeeded = true
        panel.backgroundColor = .clear
        panel.isOpaque = false
        panel.hasShadow = false
        panel.ignoresMouseEvents = true
        panel.appearance = NSAppearance(named: .darkAqua)
        panel.title = String(localized: "NextThought 提词岛")
        panel.contentView = FirstMouseHostingView(rootView: CueIslandView(model: model).environment(AppState.shared))
        observers.append(NotificationCenter.default.addObserver(forName: NSApplication.didChangeScreenParametersNotification,
                                                                object: nil, queue: .main) { [weak self] _ in
            MainActor.assumeIsolated {
                self?.reposition()
                // A display plugged in or out mid-meeting turns dual-screen presenting on or off.
                PresenterMode.shared.displaysChanged()
            }
        })
        return panel
    }

    /// `ignoresMouseEvents` covers a whole window, and a window that ignores the mouse
    /// gets no hover events either. So the pointer is polled while the island shows:
    /// over the island the panel takes clicks, everywhere else they pass through.
    private func startTracking() {
        guard tracker == nil else { return }
        let timer = Timer(timeInterval: 0.08, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.track() }
        }
        RunLoop.main.add(timer, forMode: .common)
        tracker = timer
    }

    private var pointerEntered: Date?
    private var pointerLeft: Date?
    private var lastShareCheck = Date.distantPast

    private func track() {
        guard let panel, panel.isVisible else { return }
        if let forced = model.demoHover {
            if model.hovering != forced { model.hovering = forced }
            return
        }
        let metrics = model.metrics
        // Drawing nothing, the island is the notch itself.
        let state = AppState.shared
        let screen = Self.cameraScreen(named: model.screenName)
        let front = state.isRunning && NSApp.isActive
            && (NSApp.keyWindow.map { MainWindowLayout.isMainWindow($0) && $0.screen == screen } ?? false)
        if front != model.mainWindowFront { model.mainWindowFront = front; model.forceCue = false }
        let peek = state.isRunning && !front && !model.shareAvoiding && state.autoEnd.deadline != nil
        if peek != model.peeking { model.peeking = peek }
        let size = model.isHidden ? CGSize(width: metrics.notchWidth, height: metrics.barHeight) : model.islandSize
        let rect = NSRect(x: panel.frame.midX - size.width / 2,
                          y: panel.frame.maxY - metrics.topOffset - size.height,
                          width: size.width, height: size.height)
        // A little slack, so the pointer does not fall off between rows.
        let inside = size.width > 0 && rect.insetBy(dx: -6, dy: -6).contains(NSEvent.mouseLocation)
        let now = Date()
        if inside {
            pointerLeft = nil
            if pointerEntered == nil { pointerEntered = now }
            // A pointer on its way to the menu bar must not open the island: it has
            // to rest a moment. Once open, it stays open at once.
            if !model.hovering, now.timeIntervalSince(pointerEntered ?? now) >= 0.22 {
                model.hovering = true
                NSHapticFeedbackManager.defaultPerformer.perform(.alignment, performanceTime: .now)
            }
        } else {
            pointerEntered = nil
            if model.hovering {
                if pointerLeft == nil { pointerLeft = now }
                if now.timeIntervalSince(pointerLeft ?? now) >= 0.2 {
                    model.hovering = false
                }
            }
            // A click outside puts a pinned deck away.
            if model.pinned, NSEvent.pressedMouseButtons != 0 { model.pinned = false }
        }
        let wantsClicks = model.hovering || model.peeking || model.pinned
        if panel.ignoresMouseEvents == wantsClicks { panel.ignoresMouseEvents = !wantsClicks }
        if now.timeIntervalSince(lastShareCheck) >= 2 {
            lastShareCheck = now
            let sharing = model.avoidsSharing && AppState.shared.isRunning
                && Self.cameraScreen(named: model.screenName).map(ShareGuard.isShared) == true
            if sharing != model.shareAvoiding { model.shareAvoiding = sharing }
        }
    }
}

/// Tells when a meeting app is sharing a whole display.
///
/// macOS has no public way to keep a window out of a ScreenCaptureKit capture (since
/// 15.4 `sharingType = .none` is ignored by it), so the island steps aside instead.
/// Meeting apps mark the display they share with a border overlay as large as the
/// display; that overlay is what this looks for. Window names are not read, so no
/// Screen Recording permission is involved.
enum ShareGuard {
    static let meetingApps: Set<String> = [
        "us.zoom.xos", "com.microsoft.teams2", "com.microsoft.teams", "com.tencent.meeting",
        "com.tencent.tencentmeeting", "com.bytedance.macos.feishu", "com.electron.lark", "com.larksuite.larkApp",
        "com.alibaba.DingTalkMac", "com.cisco.webexmeetingsapp", "Cisco-Systems.Spark", "com.webex.meetingmanager",
    ]

    static func isShared(_ screen: NSScreen) -> Bool {
        guard let number = screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber,
              let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]]
        else { return false }
        let display = CGDisplayBounds(CGDirectDisplayID(number.uint32Value))
        let own = ProcessInfo.processInfo.processIdentifier
        return windows.contains { window in
            guard let pid = window[kCGWindowOwnerPID as String] as? Int32, pid != own,
                  let layer = window[kCGWindowLayer as String] as? Int, layer > 0,
                  let boundsValue = window[kCGWindowBounds as String] as? [String: Any],
                  let bounds = CGRect(dictionaryRepresentation: boundsValue as CFDictionary),
                  let bundle = NSRunningApplication(processIdentifier: pid)?.bundleIdentifier,
                  meetingApps.contains(bundle) else { return false }
            return coversDisplay(bounds, display: display)
        }
    }

    /// A share border spans the display (some apps leave the menu bar out).
    static func coversDisplay(_ bounds: CGRect, display: CGRect) -> Bool {
        abs(bounds.minX - display.minX) <= 4 && abs(bounds.maxX - display.maxX) <= 4
            && abs(bounds.maxY - display.maxY) <= 4 && bounds.minY - display.minY <= 60 && bounds.minY >= display.minY - 4
    }
}

private final class IslandPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

/// A button in a window that never becomes key must act on the first click.
private final class FirstMouseHostingView<Content: View>: NSHostingView<Content> {
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
}

// MARK: - Shape

/// The island's outline. With a notch its top corners flare outwards, the way the
/// notch itself meets the screen edge; without one it is a rounded tablet.
struct IslandShape: Shape {
    var flare: CGFloat
    var radius: CGFloat

    /// Both radii animate, so the outline morphs instead of cross-fading.
    var animatableData: AnimatablePair<CGFloat, CGFloat> {
        get { AnimatablePair(flare, radius) }
        set { flare = newValue.first; radius = newValue.second }
    }

    func path(in rect: CGRect) -> Path {
        guard flare > 0 else {
            return Path(roundedRect: rect, cornerRadius: min(radius, rect.height / 2), style: .continuous)
        }
        let r = min(radius, rect.height / 2, (rect.width - 2 * flare) / 2)
        var path = Path()
        path.move(to: CGPoint(x: rect.minX, y: rect.minY))
        path.addQuadCurve(to: CGPoint(x: rect.minX + flare, y: rect.minY + flare), control: CGPoint(x: rect.minX + flare, y: rect.minY))
        path.addLine(to: CGPoint(x: rect.minX + flare, y: rect.maxY - r))
        path.addQuadCurve(to: CGPoint(x: rect.minX + flare + r, y: rect.maxY), control: CGPoint(x: rect.minX + flare, y: rect.maxY))
        path.addLine(to: CGPoint(x: rect.maxX - flare - r, y: rect.maxY))
        path.addQuadCurve(to: CGPoint(x: rect.maxX - flare, y: rect.maxY - r), control: CGPoint(x: rect.maxX - flare, y: rect.maxY))
        path.addLine(to: CGPoint(x: rect.maxX - flare, y: rect.minY + flare))
        path.addQuadCurve(to: CGPoint(x: rect.maxX, y: rect.minY), control: CGPoint(x: rect.maxX - flare, y: rect.minY))
        path.closeSubpath()
        return path
    }
}

/// Black like the notch, with the scene's pigment running along the lower edge.
private struct IslandChrome: ViewModifier {
    let shape: IslandShape
    let edge: Color
    let edgeOpacity: Double
    let edgeWidth: CGFloat
    /// No notch to grow from: the island floats under the menu bar.
    let floating: Bool
    let lifted: Bool

    func body(content: Content) -> some View {
        let gradient = LinearGradient(colors: [.clear, edge.opacity(edgeOpacity)],
                                      startPoint: UnitPoint(x: 0.5, y: 0.35), endPoint: .bottom)
        return content
            .clipShape(shape)
            .background {
                shape.fill(.black)
                    .shadow(color: .black.opacity(lifted && floating ? 0.35 : 0.18), radius: 18, x: 0, y: 10)
            }
            .overlay { shape.stroke(gradient, lineWidth: edgeWidth) }
            .overlay { if floating { shape.stroke(.white.opacity(0.1), lineWidth: 0.5) } }
            .contentShape(shape)
            .animation(.easeInOut(duration: 0.4), value: edgeWidth)
    }
}

// MARK: - View

struct CueIslandView: View {
    @Environment(AppState.self) private var state
    let model: CueIslandModel

    /// The island's presentations, in the grammar of a Dynamic Island: `compact` sits
    /// beside the notch, `hearing` and `cue` are live activities that open by
    /// themselves, `deck` and `launcher` open under the pointer.
    enum Phase: Equatable { case hidden, compact, hearing(String), cue, deck, launcher, autoEnd, slide, listening }

    private var mode: SessionMode { (state.preparedWorkspace ?? state.activeWorkspace).effectiveMode }
    private var tint: Color { mode.theme.tint }
    private var answerKey: String { Self.answerKey(state) }
    static func answerKey(_ state: AppState) -> String {
        "\(state.currentAnswerID.map { "\($0)" } ?? "-")|\(state.historyCursor ?? -1)|\(state.displayedQuestion.count)"
    }
    private var answer: String { model.preview && state.displayedAnswer.isEmpty ? Self.sampleAnswer : state.displayedAnswer }
    private var question: String {
        if model.preview && state.displayedQuestion.isEmpty && state.pendingQuestion.isEmpty { return Self.sampleQuestion }
        let text = state.displayedQuestion.isEmpty ? state.pendingQuestion : state.displayedQuestion
        return AnswerRefinement.originalQuestion(of: text)
    }
    /// The scene decides the layout (Settings → 场景); the island no longer offers its own.
    private var cueStyle: CueStyle { SceneSettings.shared.cueStyle(for: mode) }
    private var lines: [CueLine] { CueScript.lines(from: answer, style: cueStyle) }

    private var phase: Phase {
        if model.preview { return .cue }
        // A meeting app is sharing this display: nothing of ours is drawn on it.
        if model.shareAvoiding { return .hidden }
        // Nobody is talking: the meeting ends soon unless the user says otherwise.
        if state.isRunning, model.peeking || Self.demoAutoEnd { return .autoEnd }
        let hasAnswer = !state.displayedAnswer.isEmpty || state.isAnswering
        // Kept open: answers first, then the slide being presented, then the last answer.
        if state.isRunning, model.keepsOpen {
            if model.pinned { return .deck }
            // The other side has the floor: what they are saying, and what it means, come
            // before the script. An answer on show gives way only to a real sentence, and
            // only while it is being said; it is back the moment they stop.
            let theirs = state.partialTranscript.trimmingCharacters(in: .whitespacesAndNewlines).count
            if hasAnswer, model.collapsedKey != answerKey {
                // Between two sentences of a new question the last answer stays away.
                // Pinned open, nothing gives way: what they are saying goes above the answer.
                return (theirs >= 40 || unansweredTurn) && !state.isAnswering && !model.alwaysOpen ? .listening : .cue
            }
            if theirs >= 12 || (theirFloor && state.microphonePartialTranscript.isEmpty) { return .listening }
            let script = model.presenting && PresenterMode.shared.showsSlideNotes
            if script, state.insights.slide != nil { return .slide }
            if let candidate = state.liveCandidate, candidate.state != .triggered, !candidate.text.isEmpty {
                return .hearing(candidate.text)
            }
            // Presenting from PowerPoint's own notes: nothing to show until someone asks.
            if model.presenting, !script, !hasAnswer, !model.alwaysOpen { return .compact }
            return .cue
        }
        // With NextThought's own window in front the answer is already there: stay compact.
        let covered = model.mainWindowFront && !model.forceCue
        if hasAnswer, model.collapsedKey != answerKey, !covered { return .cue }
        if state.isRunning {
            if model.hovering || model.pinned { return .deck }
            if !covered, let candidate = state.liveCandidate, candidate.state != .triggered,
               !candidate.text.isEmpty { return .hearing(candidate.text) }
            return .compact
        }
        return (model.hovering || model.pinned) && model.launcher && model.metrics.hasNotch ? .launcher : .hidden
    }

    var body: some View {
        let phase = phase
        ZStack(alignment: .top) {
            if model.backdrop {
                // A flat desk colour: recordings of it compress well and sit quietly on a page.
                Color(nsColor: .hex(0xe4e1da))
            }
            if phase != .hidden {
                island(phase)
                    .transition(Motion.reduce ? .opacity : .opacity.combined(with: .scale(scale: 0.7, anchor: .top)))
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        .environment(\.colorScheme, .dark)
        // Opening has a little life; closing settles without a bounce.
        .animation(Motion.reduce ? nil : (phase == .compact || phase == .hidden
                                          ? .spring(response: 0.45, dampingFraction: 1)
                                          : .spring(response: 0.42, dampingFraction: 0.8)), value: phase)
        .animation(Motion.expand, value: model.hovering)
        .task(id: "\(answerKey)|\(state.isAnswering)") { await settle() }
        .onChange(of: phase, initial: true) { _, phase in model.isHidden = phase == .hidden }
        .onChange(of: state.entries.count) { _, _ in refreshCoverage() }
        .onChange(of: answerKey) { _, _ in model.covered = []; model.forceCue = false }
        // Line numbers differ between 要点 and 逐句: what was ticked off no longer applies.
        .onChange(of: cueStyle) { _, _ in model.covered = [] }
        .onChange(of: state.isAnswering) { _, answering in if !answering { refreshCoverage() } }
    }

    // MARK: Island

    private func island(_ phase: Phase) -> some View {
        let metrics = model.metrics
        let idle = phase == .compact
        // Closed, the outline is the hardware notch's (6 pt flare, 14 pt corners);
        // open, it relaxes.
        let flare: CGFloat = metrics.hasNotch ? (idle ? 6 : 14) : 0
        let shape = IslandShape(flare: flare, radius: idle ? (metrics.hasNotch ? 14 : 15) : 24)
        let edge: Color = state.coach.shouldLookUp || phase == .autoEnd ? Ink.caution : tint
        return islandContent(phase)
            .frame(width: width(phase))
            .padding(.horizontal, flare)
            .modifier(IslandChrome(shape: shape, edge: edge, edgeOpacity: idle ? 0 : 0.85,
                                   edgeWidth: state.coach.shouldLookUp ? 2 : 1.2,
                                   floating: !metrics.hasNotch, lifted: !idle))
            // The hover bump: the island leans towards the pointer before it opens.
            .scaleEffect(idle && model.hovering && !Motion.reduce ? 1.04 : 1, anchor: .top)
            .background(GeometryReader { proxy in
                Color.clear.onChange(of: proxy.size, initial: true) { _, size in model.islandSize = size }
            })
            .padding(.top, metrics.topOffset)
    }

    private func islandContent(_ phase: Phase) -> some View {
        VStack(spacing: 0) {
            bar(phase)
            if case let .hearing(text) = phase { hearing(text) }
            if phase == .cue { cue }
            if phase == .slide { slideNotes }
            if phase == .listening { listening }
            if phase == .deck { deck }
            if phase == .launcher { launcher }
            if phase == .autoEnd { autoEndPeek }
        }
    }

    private func width(_ phase: Phase) -> CGFloat {
        let metrics = model.metrics
        let ears: CGFloat = metrics.hasNotch ? metrics.notchWidth + 2 * (state.coach.nudge != nil || state.coach.shouldLookUp ? 132 : 76) : 164
        switch phase {
        case .hidden, .compact: return ears
        case .hearing: return max(ears, min(model.width.points, 440))
        case .cue, .slide, .listening: return max(ears, model.width.points)
        case .deck: return max(ears, 470)
        case .launcher: return max(ears, 500)
        case .autoEnd: return max(ears, 470)
        }
    }

    /// The row level with the notch: the meeting's pulse on the left, the clock (or
    /// the coach) on the right.
    private func bar(_ phase: Phase) -> some View {
        let metrics = model.metrics
        return HStack(spacing: 8) {
            HStack(spacing: 7) {
                Circle().fill(state.isRunning && !state.isPaused ? tint : Color.white.opacity(0.35)).frame(width: 7, height: 7)
                VoiceWave(tint: .white.opacity(0.85), bars: 5, height: 13, active: state.isRunning && !state.isPaused)
            }
            // Before a meeting there is no pulse to show.
            .opacity(phase == .launcher ? 0 : 1)
            Spacer(minLength: metrics.hasNotch ? metrics.notchWidth : 8)
            coachEar
        }
        .padding(.horizontal, 14)
        .frame(height: metrics.barHeight)
        // A click on the bar keeps the deck open (or closes it again).
        .contentShape(Rectangle())
        .onTapGesture { togglePinned(phase) }
        .accessibilityElement(children: .contain)
        .accessibilityAction(named: Text(model.pinned ? "收起会议面板" : "展开会议面板")) { togglePinned(phase) }
    }

    private func togglePinned(_ phase: Phase) {
        switch phase {
        case .compact, .deck, .hearing, .launcher: model.pinned.toggle()
        // Kept open, a click on the bar swaps the answer for the meeting deck and back.
        case .cue, .slide, .listening:
            if model.keepsOpen, state.isRunning { model.pinned.toggle() }
        default: break
        }
    }

    @ViewBuilder private var coachEar: some View {
        let coach = state.coach
        if coach.shouldLookUp {
            earLabel(String(localized: "看镜头"), symbol: "eye.fill", color: Ink.caution)
                .transition(.blurReplace)
        } else if let nudge = coach.nudge {
            earLabel(nudge.message, symbol: nudge.symbol, color: Ink.caution)
                .transition(.blurReplace)
        } else {
            HStack(spacing: 6) {
                if model.presenting, let slide = state.insights.slide {
                    Text(verbatim: "\(slide.page)/\(slide.pageCount)")
                        .font(.system(size: 11, weight: .semibold).monospacedDigit())
                        .foregroundStyle(tint)
                        .contentTransition(.numericText())
                        .accessibilityLabel(Text("第 \(slide.page) 页，共 \(slide.pageCount) 页"))
                }
                if coach.isWatching, let gaze = coach.gaze {
                    Image(systemName: gaze.state == .onCamera ? "eye.fill" : "eye.slash")
                        .font(.system(size: 10, weight: .semibold))
                        .foregroundStyle(gaze.state == .onCamera ? Ink.ready : .white.opacity(0.45))
                        .contentTransition(.symbolEffect(.replace))
                }
                if let start = state.sessionStartedAt, state.isRunning {
                    TimelineView(.periodic(from: start, by: 1)) { context in
                        Text(verbatim: Self.clock(from: start, to: context.date))
                            .font(.system(size: 11, weight: .medium).monospacedDigit())
                            .foregroundStyle(.white.opacity(0.7))
                            .contentTransition(.numericText())
                    }
                }
            }
            .transition(.blurReplace)
        }
    }

    private func earLabel(_ text: String, symbol: String, color: Color) -> some View {
        HStack(spacing: 5) {
            Image(systemName: symbol).font(.system(size: 10, weight: .semibold))
            Text(verbatim: text).font(.system(size: 11, weight: .semibold)).lineLimit(1)
        }
        .foregroundStyle(color)
        .symbolEffect(.bounce, value: Motion.reduce ? "" : text)
    }

    /// What the other side has said in this turn: their finished sentences of the last
    /// half minute and the one still being said, with the interpretation of each.
    private var theirTurn: (text: String, translation: String) {
        var texts: [String] = [], translations: [String] = []
        let recent = state.entries.suffix(6).filter { $0.suspectedEcho != true }
        if let last = recent.last, !last.isMe {
            for entry in recent.reversed() {
                guard !entry.isMe, last.date.timeIntervalSince(entry.date) < 30 else { break }
                texts.insert(entry.text, at: 0)
                if let translated = entry.translatedText, !translated.isEmpty { translations.insert(translated, at: 0) }
            }
        }
        let partial = state.partialTranscript.trimmingCharacters(in: .whitespacesAndNewlines)
        if !partial.isEmpty {
            texts.append(partial)
            let translated = state.liveTranslation.partialTranslation(for: .system)
            if !translated.isEmpty { translations.append(translated) }
        }
        return (texts.joined(separator: " "), state.liveTranslation.isEnabled ? translations.joined(separator: " ") : "")
    }

    /// They have said a sentence or more that no answer covers, and I have not replied.
    private var unansweredTurn: Bool {
        let turn = state.questionTurn
        guard turn.unanswered >= 8, let heard = turn.lastHeardAt, Date().timeIntervalSince(heard) < 20 else { return false }
        return !state.isUserSpeaking && !state.holdsFloor
    }

    /// The last thing said was a sentence of theirs, and nothing has answered it yet.
    private var theirFloor: Bool {
        guard let last = state.entries.last(where: { $0.suspectedEcho != true }), !last.isMe else { return false }
        return last.text.count >= 12 && Date().timeIntervalSince(last.date) < 45
    }

    /// Listening to a question: the words as they are heard and, with live interpretation
    /// on, their meaning underneath.
    private var listening: some View {
        let turn = theirTurn
        let size = CGFloat(state.teleprompterFontSize)
        return VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 6) {
                Image(systemName: "waveform").font(.system(size: 11, weight: .semibold)).foregroundStyle(tint)
                    .symbolEffect(.variableColor.iterative, options: .repeating, isActive: !Motion.reduce)
                Text("对方在说").font(.system(size: 12, weight: .medium)).foregroundStyle(.white.opacity(0.5))
                Spacer(minLength: 0)
                if !state.liveTranslation.isEnabled {
                    Button("开启同声传译") { state.liveTranslation.setEnabled(true) }
                        .buttonStyle(.plain).font(.system(size: 11, weight: .medium)).foregroundStyle(tint)
                }
            }
            Text(verbatim: turn.text)
                .font(.system(size: max(13, size * 0.62))).foregroundStyle(.white.opacity(0.78))
                .lineSpacing(2).lineLimit(4).truncationMode(.head)
                .frame(maxWidth: .infinity, alignment: .leading)
            if !turn.translation.isEmpty {
                Text(verbatim: turn.translation)
                    .font(.system(size: size * 0.8, weight: .medium)).foregroundStyle(.white)
                    .lineSpacing(size * 0.12).lineLimit(4).truncationMode(.head)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .contentTransition(.opacity)
            }
            if model.hovering { controls.padding(.top, 4).transition(.opacity.combined(with: .move(edge: .bottom))) }
        }
        .padding(.horizontal, 20).padding(.top, 4).padding(.bottom, 16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .transition(.opacity.combined(with: .scale(scale: 0.96, anchor: .top)))
    }

    /// The same, as a strip above an answer that stays on show.
    private var heard: some View {
        let turn = theirTurn
        let size = CGFloat(state.teleprompterFontSize)
        return VStack(alignment: .leading, spacing: 5) {
            HStack(spacing: 6) {
                Image(systemName: "waveform").font(.system(size: 11, weight: .semibold)).foregroundStyle(tint)
                    .symbolEffect(.variableColor.iterative, options: .repeating, isActive: !Motion.reduce)
                Text("对方在说").font(.system(size: 12, weight: .medium)).foregroundStyle(.white.opacity(0.5))
                Spacer(minLength: 0)
            }
            Text(verbatim: turn.text)
                .font(.system(size: max(13, size * 0.62))).foregroundStyle(.white.opacity(0.78))
                .lineSpacing(2).lineLimit(2).truncationMode(.head)
                .frame(maxWidth: .infinity, alignment: .leading)
            if !turn.translation.isEmpty {
                Text(verbatim: turn.translation)
                    .font(.system(size: size * 0.72, weight: .medium)).foregroundStyle(.white)
                    .lineSpacing(2).lineLimit(3).truncationMode(.head)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .contentTransition(.opacity)
            }
            Rectangle().fill(.white.opacity(0.12)).frame(height: 1).padding(.top, 6)
        }
    }

    /// A question is being asked: its words, as they are heard.
    private func hearing(_ text: String) -> some View {
        HStack(spacing: 8) {
            Image(systemName: "waveform").font(.system(size: 11, weight: .semibold)).foregroundStyle(tint)
                .symbolEffect(.variableColor.iterative, options: .repeating, isActive: !Motion.reduce)
            Text(verbatim: text)
                .font(.system(size: 13, weight: .medium)).foregroundStyle(.white.opacity(0.82))
                .lineLimit(1).truncationMode(.head)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(.horizontal, 18).padding(.top, 2).padding(.bottom, 12)
        .transition(Motion.reduce ? .opacity : .opacity.combined(with: .move(edge: .top)))
    }

    // MARK: Deck

    /// The meeting under the pointer: what was just said, how it is going, and the
    /// four things worth doing without leaving the meeting window.
    private var deck: some View {
        let workspace = state.preparedWorkspace ?? state.activeWorkspace
        return VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 8) {
                Text(verbatim: workspace.name)
                    .font(.system(size: 14, weight: .semibold)).foregroundStyle(.white).lineLimit(1)
                Text(verbatim: workspace.typeDisplayName)
                    .font(.system(size: 11, weight: .medium)).foregroundStyle(tint)
                    .padding(.horizontal, 7).padding(.vertical, 2)
                    .background(tint.opacity(0.18), in: Capsule())
                Spacer(minLength: 0)
                HStack(spacing: 5) {
                    if state.automaticAnswers, !state.isPaused, !state.answerEngineReady {
                        Circle().fill(Ink.caution).frame(width: 5, height: 5)
                    }
                    Text(state.isPaused ? "已暂停" : (state.automaticAnswers ? "自动回答" : "只记录"))
                        .font(.system(size: 11, weight: .medium)).foregroundStyle(.white.opacity(0.5))
                }
                .help(!state.answerEngineReady && state.automaticAnswers ? Self.noAnswerServiceHint : "")
            }
            if let heard = lastHeard {
                HStack(alignment: .firstTextBaseline, spacing: 8) {
                    Text(heard.isMe ? "我" : "对方")
                        .font(.system(size: 11, weight: .semibold)).foregroundStyle(.white.opacity(0.45))
                    VStack(alignment: .leading, spacing: 3) {
                        Text(verbatim: heard.text)
                            .font(.system(size: 13)).foregroundStyle(.white.opacity(0.82))
                            .lineLimit(heard.translation.isEmpty ? 2 : 3).frame(maxWidth: .infinity, alignment: .leading)
                        if !heard.translation.isEmpty {
                            // Live interpretation: what is being said, in the other language, as it is said.
                            Text(verbatim: heard.translation)
                                .font(.system(size: 14, weight: .medium)).foregroundStyle(tint)
                                .lineLimit(3).frame(maxWidth: .infinity, alignment: .leading)
                                .contentTransition(.opacity)
                        }
                    }
                }
                .transition(.blurReplace)
            }
            HStack(spacing: 6) {
                IslandAction(title: String(localized: "立即回答"), symbol: "bolt.fill", prominent: true,
                             help: state.shortcutLabel(.forceAnswer)) {
                    model.collapsedKey = nil
                    state.forceAnswerCurrent()
                }
                if (model.collapsedKey != nil || model.mainWindowFront), !state.displayedAnswer.isEmpty {
                    IslandAction(title: String(localized: "上一条"), symbol: "arrow.uturn.backward", help: String(localized: "再看一眼上一条回答")) {
                        model.collapsedKey = nil
                        model.forceCue = true
                    }
                }
                let unconnected = !state.automaticAnswers && !state.answerEngineReady
                IslandAction(title: state.automaticAnswers ? String(localized: "改为只记录") : String(localized: "改为自动回答"),
                             symbol: state.automaticAnswers ? "pencil.line" : "sparkles", warning: unconnected,
                             help: unconnected ? Self.noAnswerServiceHint
                                 : String(localized: "自动回答：听到问题就给出回答。只记录：需要时再求助。")) {
                    state.setLiveMode(prompting: !state.automaticAnswers)
                }
                Spacer(minLength: 0)
                IslandAction(title: state.isPaused ? String(localized: "继续") : String(localized: "暂停"),
                             symbol: state.isPaused ? "play.fill" : "pause.fill", help: String(localized: "暂停或继续聆听")) {
                    Task { await state.togglePause() }
                }
                IslandAction(title: String(localized: "结束"), symbol: "stop.fill", destructive: true,
                             help: String(localized: "结束并整理纪要 · \(state.shortcutLabel(.toggleSession))")) {
                    Task { await state.stopSession() }
                }
                .disabled(state.isChangingSession)
            }
            if let stats = coachStats {
                Text(verbatim: stats)
                    .font(.system(size: 11).monospacedDigit()).foregroundStyle(.white.opacity(0.45))
                    .contentTransition(.numericText())
            }
        }
        .padding(.horizontal, 18).padding(.top, 6).padding(.bottom, 14)
        .frame(maxWidth: .infinity, alignment: .leading)
        .transition(.opacity.combined(with: .scale(scale: 0.96, anchor: .top)))
    }

    /// NEXTTHOUGHT_DEMO_ISLAND_PHASE=autoend: draws the reminder peek for screenshots.
    static let demoAutoEnd = AppEnvironment.values["NEXTTHOUGHT_DEMO_ISLAND_PHASE"] == "autoend"
    static let noAnswerServiceHint = String(localized: "还没有连接回答服务，听到问题也不会自动回答")

    /// The newest line of the conversation, while it is still being said if possible.
    private var lastHeard: (isMe: Bool, text: String, translation: String)? {
        let translating = state.liveTranslation.isEnabled
        let partial = state.partialTranscript.trimmingCharacters(in: .whitespacesAndNewlines)
        if !partial.isEmpty { return (false, partial, translating ? state.liveTranslation.partialTranslation(for: .system) : "") }
        guard let entry = state.entries.last(where: { $0.suspectedEcho != true }) else { return nil }
        // Only the other side is interpreted here: the user knows what they said themself.
        return (entry.isMe, entry.text, translating && !entry.isMe ? (entry.translatedText ?? "") : "")
    }

    /// "语速 240/分 · 我说了 46% · 看镜头 82%", from what the coach has so far.
    private var coachStats: String? {
        guard state.coach.isEnabled else { return nil }
        var parts: [String] = []
        if let pace = state.coach.snapshot.pace { parts.append(String(localized: "语速 \(Int(pace.rounded()))/分")) }
        if let share = state.coach.snapshot.talkShare { parts.append(String(localized: "我说了 \(Int((share * 100).rounded()))%")) }
        if let contact = state.coach.contactRatio { parts.append(String(localized: "看镜头 \(Int((contact * 100).rounded()))%")) }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }

    // MARK: Auto-end reminder

    /// The meeting is about to end by itself. Shown wherever the user is looking, since the
    /// banner on the live page cannot be seen from the meeting app.
    private var autoEndPeek: some View {
        TimelineView(.periodic(from: .now, by: 1)) { context in
            let left = state.autoEnd.deadline.map { max(0, Int($0.timeIntervalSince(context.date).rounded(.up))) } ?? (Self.demoAutoEnd ? 42 : 0)
            VStack(alignment: .leading, spacing: 10) {
                HStack(spacing: 7) {
                    Image(systemName: "moon.zzz").font(.system(size: 11, weight: .semibold))
                    Text(String(localized: "好像没人说话了，\(left) 秒后结束记录"))
                        .font(.system(size: 13, weight: .medium).monospacedDigit())
                        .contentTransition(.numericText(countsDown: true))
                        .lineLimit(1)
                    Spacer(minLength: 0)
                }
                .foregroundStyle(Ink.caution)
                .animation(Motion.reduce ? nil : .default, value: left)
                HStack(spacing: 6) {
                    IslandAction(title: String(localized: "继续记录"), symbol: "mic.fill", prominent: true,
                                 help: String(localized: "取消倒计时，继续记录这场会议")) { state.keepRecording() }
                    IslandAction(title: String(localized: "现在结束"), symbol: "stop.fill", destructive: true,
                                 help: String(localized: "现在结束并整理纪要")) { state.endMeetingAfterSilence() }
                    Spacer(minLength: 0)
                }
                .disabled(state.isChangingSession)
            }
        }
        .padding(.horizontal, 18).padding(.top, 6).padding(.bottom, 14)
        .frame(maxWidth: .infinity, alignment: .leading)
        .transition(Motion.reduce ? .opacity : .opacity.combined(with: .scale(scale: 0.96, anchor: .top)))
        .accessibilityIdentifier("island.autoEnd")
    }

    // MARK: Launcher

    /// No meeting yet: the notch offers to start one, in any scene, from anywhere.
    private var launcher: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("开始一场").font(.system(size: 11, weight: .semibold)).tracking(0.6).foregroundStyle(.white.opacity(0.45))
            FlowLayout(spacing: 6, lineSpacing: 6) {
                ForEach(SessionMode.builtIn) { mode in
                    Button { Task { await state.startFromIsland(scene: mode) } } label: {
                        HStack(spacing: 6) {
                            Circle().fill(mode.theme.tint).frame(width: 6, height: 6)
                            Text(verbatim: mode.displayName).font(.system(size: 12, weight: .medium))
                        }
                        .padding(.horizontal, 10).frame(height: 28)
                    }
                    .buttonStyle(IslandChipStyle())
                    .help(mode.theme.promise)
                    .accessibilityLabel(Text(verbatim: mode.displayName))
                    .accessibilityHint(Text(verbatim: mode.theme.promise))
                    .disabled(state.isChangingSession)
                }
            }
            HStack(spacing: 6) {
                Text("点一个场景，立即开始。").font(.system(size: 11)).foregroundStyle(.white.opacity(0.45))
                Spacer(minLength: 0)
                IslandAction(title: String(localized: "打开主窗口"), symbol: "macwindow", help: String(localized: "回到 NextThought")) {
                    MainWindowLayout.bringToFront()
                }
            }
        }
        .padding(.horizontal, 18).padding(.top, 6).padding(.bottom, 14)
        .frame(maxWidth: .infinity, alignment: .leading)
        .transition(.opacity.combined(with: .scale(scale: 0.96, anchor: .top)))
    }

    // MARK: Cue

    /// Presenting: the talking points of the slide on the shared display, ticked off as
    /// they are said, and how to get to the next one.
    private var slideNotes: some View {
        let insights = state.insights
        let size = CGFloat(state.teleprompterFontSize)
        let slide = insights.slide
        let notes = slide.flatMap { insights.slideNotes[$0.key] }
        let covered = slide.flatMap { insights.slideCovered[$0.key] } ?? []
        let next = notes.flatMap { notes in notes.points.indices.first { !covered.contains($0) } }
        // A written script can run to dozens of sentences: the island shows the stretch being read.
        let lines = (notes?.points ?? []).enumerated().map { CueLine(id: $0.offset, kind: .point, text: $0.element) }
        let visible = Self.window(lines, next: next, limit: notes?.isScript == true ? 5 : 6)
        return VStack(alignment: .leading, spacing: 0) {
            if let slide {
                HStack(spacing: 6) {
                    Text(verbatim: String(localized: "第 \(slide.page) 页") + " · " + (notes?.isScript == true ? String(localized: "我的讲稿") : slide.documentName))
                        .font(.system(size: 12, weight: .medium)).foregroundStyle(.white.opacity(0.5)).lineLimit(1)
                    if insights.slideSource == .manual {
                        Text("手动").font(.system(size: 10, weight: .semibold)).foregroundStyle(tint)
                            .padding(.horizontal, 5).padding(.vertical, 1)
                            .background(tint.opacity(0.16), in: Capsule())
                            .help("这一页是手动翻到的；共享屏幕换到别的幻灯片后恢复自动跟随")
                    }
                    Spacer(minLength: 0)
                    if insights.generatingNotes.contains(slide.key) { ThinkingDots(color: tint) }
                    if model.hovering || model.keepsOpen {
                        slideStep("chevron.left", String(localized: "上一页 · \(state.shortcutLabel(.slidePrevious))"), slide.page > 1) { state.stepSlide(-1) }
                        slideStep("chevron.right", String(localized: "下一页 · \(state.shortcutLabel(.slideNext))"), slide.page < slide.pageCount) { state.stepSlide(1) }
                    }
                }
                .padding(.bottom, 8)
            }
            if let notes, !notes.points.isEmpty {
                VStack(alignment: .leading, spacing: size * 0.34) {
                    ForEach(visible) { line in
                        CueRow(line: line, size: size, tint: tint,
                               said: covered.contains(line.id), current: line.id == next, expanded: true)
                    }
                    if !notes.transition.isEmpty, visible.last?.id == lines.last?.id {
                        Text(verbatim: String(localized: "下一页") + "  " + notes.transition)
                            .font(.system(size: size * 0.66, weight: .medium)).foregroundStyle(tint.opacity(0.9))
                            .lineLimit(2).fixedSize(horizontal: false, vertical: true)
                            .padding(.top, 2)
                    }
                }
                .animation(Motion.expand, value: slide?.key)
                .animation(Motion.snappy, value: visible.first?.id)
            } else {
                Text(insights.slideNotesError ?? String(localized: "正在准备这一页的要点…"))
                    .font(.system(size: size * 0.72, weight: .medium)).foregroundStyle(.white.opacity(0.55))
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            if model.hovering { controls.padding(.top, 10).transition(.opacity.combined(with: .move(edge: .bottom))) }
        }
        .padding(.horizontal, 20).padding(.top, 4).padding(.bottom, 16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .transition(.opacity.combined(with: .scale(scale: 0.96, anchor: .top)))
        .id(slide?.key)
    }

    private func slideStep(_ symbol: String, _ help: String, _ enabled: Bool, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Image(systemName: symbol).font(.system(size: 11, weight: .bold))
                .foregroundStyle(.white.opacity(enabled ? 0.8 : 0.25))
                .frame(width: 22, height: 20)
                .background(.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 6, style: .continuous))
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain).disabled(!enabled).help(help)
    }

    private var cue: some View {
        let lines = lines
        let size = CGFloat(state.teleprompterFontSize)
        let next = lines.first { !model.covered.contains($0.id) }?.id
        // With the meaning under each sentence a line takes twice the room: show fewer.
        let glossed = lines.contains { !$0.gloss.isEmpty }
        // A new turn of theirs while an answer is on show (only when pinned open; otherwise
        // the island is listening instead).
        let theirs = state.partialTranscript.trimmingCharacters(in: .whitespacesAndNewlines).count
        let hearing = model.alwaysOpen && state.isRunning && !model.preview && !state.isAnswering && !lines.isEmpty && (theirs >= 12 || unansweredTurn)
        // Pinned open on a display nobody else sees, there is room for the whole answer.
        let limit = glossed ? (model.alwaysOpen ? (hearing ? 4 : 5) : 3) : (cueStyle == .script ? 4 : 6)
        let visible = Self.window(lines, next: next, limit: limit)
        let asked = BilingualAnswer.question(in: answer)
        return VStack(alignment: .leading, spacing: 0) {
            if hearing { heard.padding(.bottom, 12) }
            if !question.isEmpty {
                HStack(spacing: 6) {
                    // A question of several sentences ends with what is actually asked.
                    Text(verbatim: question)
                        .font(.system(size: 12, weight: .medium)).foregroundStyle(.white.opacity(0.5)).lineLimit(asked == nil ? 1 : 2)
                        .truncationMode(.head)
                    Spacer(minLength: 0)
                    if state.isAnswering { ThinkingDots(color: tint) }
                }
                .padding(.bottom, asked == nil ? 8 : 4)
            }
            if let asked {
                // What was asked, in the user's own language: understood before the answer is read.
                Text(verbatim: asked)
                    .font(.system(size: max(13, size * 0.62), weight: .medium)).foregroundStyle(tint)
                    .lineSpacing(2).lineLimit(3)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.bottom, 10)
            }
            if lines.isEmpty, state.isAnswering, case let meaning = theirTurn.translation, !meaning.isEmpty {
                // Until the answer starts, what they asked stays readable in my language.
                Text(verbatim: meaning)
                    .font(.system(size: size * 0.8, weight: .medium)).foregroundStyle(.white)
                    .lineSpacing(3).lineLimit(4).truncationMode(.head)
                    .frame(maxWidth: .infinity, alignment: .leading)
            } else if lines.isEmpty {
                Text(state.isAnswering ? "正在想…" : (model.presenting && PresenterMode.shared.showsSlideNotes ? "被问到时，回答会出现在这里；翻到已放进资料的幻灯片时，这里显示这一页的要点。" : "被问到时，回答会出现在这里。"))
                    .font(.system(size: size * 0.8, weight: .medium)).foregroundStyle(.white.opacity(0.55))
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            VStack(alignment: .leading, spacing: size * 0.34) {
                ForEach(visible) { line in
                    CueRow(line: line, size: size, tint: tint, said: model.covered.contains(line.id),
                           current: line.id == next && !state.isAnswering,
                           expanded: model.hovering || model.keepsOpen || cueStyle == .script || glossed)
                        .withGloss
                        .transition(.asymmetric(insertion: .opacity.combined(with: .offset(y: 10)),
                                                removal: .opacity.combined(with: .offset(y: -10))))
                }
            }
            .animation(Motion.expand, value: visible.map(\.id))
            if model.hovering { controls.padding(.top, 10).transition(.opacity.combined(with: .move(edge: .bottom))) }
        }
        .padding(.horizontal, 20).padding(.top, 4).padding(.bottom, 16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .transition(.opacity.combined(with: .scale(scale: 0.96, anchor: .top)))
    }

    /// Shown while the pointer rests on the island; everything here also has a shortcut.
    private var controls: some View {
        HStack(spacing: 2) {
            if let position = state.historyPositionText {
                IslandButton(symbol: "chevron.left", help: String(localized: "上一条回答")) { state.showPreviousAnswer() }
                Text(verbatim: position).font(.system(size: 11, weight: .medium).monospacedDigit()).foregroundStyle(.white.opacity(0.55))
                IslandButton(symbol: "chevron.right", help: String(localized: "下一条回答")) { state.showNextAnswer() }
            }
            IslandButton(title: String(localized: "更简短"), help: String(localized: "把回答改短 · \(state.shortcutLabel(.refineShorter))")) { state.refine(.shorter) }
                .disabled(state.displayedAnswer.isEmpty || state.isAnswering)
            IslandButton(title: String(localized: "举例"), help: String(localized: "加一个例子")) { state.refine(.example) }
                .disabled(state.displayedAnswer.isEmpty || state.isAnswering)
            Spacer()
            IslandButton(symbol: "textformat.size.smaller", help: String(localized: "缩小文字")) {
                state.teleprompterFontSize = max(14, state.teleprompterFontSize - 1); state.saveAnswerSettings()
            }
            IslandButton(symbol: "textformat.size.larger", help: String(localized: "放大文字")) {
                state.teleprompterFontSize = min(36, state.teleprompterFontSize + 1); state.saveAnswerSettings()
            }
            IslandButton(symbol: "macwindow", help: String(localized: "打开主窗口")) { TeleprompterController.shared.showWorkbench() }
            IslandButton(symbol: model.alwaysOpen ? "pin.fill" : "pin",
                         help: model.alwaysOpen ? String(localized: "不再一直展开 · \(state.shortcutLabel(.toggleCueOpen))")
                                                : String(localized: "一直展开 · \(state.shortcutLabel(.toggleCueOpen))")) {
                model.alwaysOpen.toggle()
            }
            if !model.keepsOpen {
                IslandButton(symbol: "chevron.up", help: String(localized: "收起 · \(state.shortcutLabel(.toggleTeleprompter)) 隐藏提词岛")) {
                    model.preview = false
                    model.collapsedKey = answerKey
                }
            }
        }
    }

    // MARK: Behaviour

    /// The lines around the next one to say: what was just said stays for a beat
    /// above it, what follows waits below.
    static func window(_ lines: [CueLine], next: Int?, limit: Int) -> [CueLine] {
        guard lines.count > limit else { return lines }
        let focus = next.flatMap { id in lines.firstIndex { $0.id == id } } ?? lines.count - 1
        let start = min(max(0, focus - 1), lines.count - limit)
        return Array(lines[start..<start + limit])
    }

    private func refreshCoverage() {
        guard state.insights.answerProgress, state.isRunning, !state.isAnswering, state.historyCursor == nil else { return }
        let since = state.insights.answerSince.addingTimeInterval(-2)
        let speech = state.entries.filter { $0.isMe && $0.suspectedEcho != true && $0.date >= since }.map(\.text).joined(separator: " ")
        guard speech.count >= 4 else { return }
        // A three-character lead-in matches incidental speech: only longer lines count.
        let said = Set(lines.filter { $0.spoken.count >= 5 && AnswerPoints.heuristicCovered($0.spoken, speech: speech) }.map(\.id))
        if !said.isSubset(of: model.covered) { model.covered.formUnion(said) }
    }

    /// Puts a finished answer away once it has been said, or when it has gone stale.
    private func settle() async {
        guard !state.isAnswering, !state.displayedAnswer.isEmpty, !model.preview else { return }
        let key = answerKey
        let shown = Date()
        while !Task.isCancelled {
            try? await Task.sleep(for: .seconds(2))
            guard key == answerKey else { return }
            let all = lines
            let finished = !all.isEmpty && all.allSatisfy { model.covered.contains($0.id) }
            let age = Date().timeIntervalSince(shown)
            if !model.hovering, (finished && age > 6) || age > 180 {
                model.collapsedKey = key
                return
            }
        }
    }

    static func clock(from start: Date, to now: Date) -> String {
        let seconds = max(0, Int(now.timeIntervalSince(start)))
        return seconds >= 3600
            ? String(format: "%d:%02d:%02d", seconds / 3600, seconds / 60 % 60, seconds % 60)
            : String(format: "%d:%02d", seconds / 60, seconds % 60)
    }

    static let sampleQuestion = String(localized: "能介绍一下你在这个项目里负责的部分吗？")
    static let sampleAnswer = String(localized: """
    我负责订单服务的**缓存层**，把核心查询从数据库迁到了 Redis。
    - **规模**：日均 2 亿次读取，P99 从 120 ms 降到 18 ms。
    - **一致性**：先更新数据库再删缓存，binlog 订阅兜底。
    - **我的角色**：方案设计、灰度上线和回滚预案都由我主导。
    """)
}

/// One cue line: the words to catch first in white, the rest behind them.
private struct CueRow: View {
    let line: CueLine
    let size: CGFloat
    let tint: Color
    let said: Bool
    let current: Bool
    /// At a glance a point is its lead-in and one line; resting the pointer on the
    /// island (or the sentence-by-sentence style) shows every line in full.
    let expanded: Bool

    var body: some View {
        Group {
            switch line.kind {
            case .lead:
                Text(verbatim: line.text)
                    .font(.system(size: size * 0.9, weight: .semibold))
                    .foregroundStyle(.white)
                    .lineSpacing(size * 0.14)
                    .lineLimit(expanded ? 5 : 2)
            case .point, .detail:
                HStack(alignment: .firstTextBaseline, spacing: 9) {
                    // The marker fills in on the line to say next.
                    Capsule().fill(current ? tint : .white.opacity(0.28))
                        .frame(width: 3, height: size * 0.56)
                        .alignmentGuide(.firstTextBaseline) { $0[.bottom] - size * 0.04 }
                    (Text(verbatim: line.head).fontWeight(.semibold).foregroundColor(.white)
                     + Text(verbatim: line.head.isEmpty || line.text.isEmpty ? "" : "  ")
                     + Text(verbatim: line.text).foregroundColor(.white.opacity(line.head.isEmpty ? 0.9 : 0.66)))
                        .font(.system(size: size * 0.76))
                        .lineSpacing(size * 0.12)
                        .lineLimit(expanded ? 4 : 1)
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .fixedSize(horizontal: false, vertical: true)
        .opacity(said ? 0.36 : 1)
        .animation(.easeOut(duration: 0.35), value: said)
        .animation(Motion.snappy, value: current)
    }

    /// The row with its meaning underneath: read to understand, not to say.
    @ViewBuilder var withGloss: some View {
        if line.gloss.isEmpty { self } else {
            VStack(alignment: .leading, spacing: size * 0.08) {
                self
                Text(verbatim: line.gloss)
                    .font(.system(size: max(11, size * 0.56)))
                    .foregroundStyle(.white.opacity(said ? 0.28 : 0.52))
                    .lineSpacing(2).lineLimit(expanded ? 3 : 1)
                    .padding(.leading, line.kind == .lead ? 0 : 12)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }
}

private struct IslandButton: View {
    var symbol: String?
    var title: String?
    let help: String
    let action: () -> Void
    @Environment(\.isEnabled) private var isEnabled
    @State private var hovering = false

    init(symbol: String, help: String, action: @escaping () -> Void) {
        self.symbol = symbol; self.title = nil; self.help = help; self.action = action
    }
    init(title: String, help: String, action: @escaping () -> Void) {
        self.symbol = nil; self.title = title; self.help = help; self.action = action
    }

    var body: some View {
        Button(action: action) {
            Group {
                if let symbol { Image(systemName: symbol).font(.system(size: 11, weight: .semibold)).frame(width: 26) }
                if let title { Text(verbatim: title).font(.system(size: 12, weight: .medium)).padding(.horizontal, 9) }
            }
            .frame(height: 26)
            .foregroundStyle(.white.opacity(isEnabled ? (hovering ? 1 : 0.72) : 0.3))
            .background(.white.opacity(hovering && isEnabled ? 0.16 : 0.07), in: Capsule())
            .contentShape(Capsule())
        }
        .buttonStyle(.plain)
        .onHover { hovering = $0 }
        .help(help)
        .accessibilityLabel(Text(verbatim: title ?? help))
        .accessibilityHint(Text(verbatim: title == nil ? "" : help))
    }
}

/// A labelled action on the deck.
private struct IslandAction: View {
    let title: String
    let symbol: String
    var prominent = false
    var destructive = false
    /// A small caution dot: the action works, but something it needs is missing.
    var warning = false
    let help: String
    let action: () -> Void
    @Environment(\.isEnabled) private var isEnabled
    @State private var hovering = false

    var body: some View {
        Button(action: action) {
            HStack(spacing: 5) {
                Image(systemName: symbol).font(.system(size: 10, weight: .semibold))
                Text(verbatim: title).font(.system(size: 12, weight: .medium)).lineLimit(1)
                if warning { Circle().fill(Ink.caution).frame(width: 5, height: 5) }
            }
            .padding(.horizontal, 11).frame(height: 28)
            .foregroundStyle(prominent ? Color.black : (destructive ? Ink.seal : Color.white.opacity(hovering ? 1 : 0.85)))
            .background(prominent ? Color.white.opacity(hovering ? 1 : 0.92) : Color.white.opacity(hovering ? 0.18 : 0.1), in: Capsule())
            .opacity(isEnabled ? 1 : 0.4)
            .contentShape(Capsule())
        }
        .buttonStyle(.plain)
        .onHover { hovering = $0 }
        .animation(Motion.hover, value: hovering)
        .help(help)
        .accessibilityLabel(Text(verbatim: title))
        .accessibilityHint(Text(verbatim: help))
    }
}

private struct IslandChipStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View { Chip(configuration: configuration) }
    private struct Chip: View {
        let configuration: ButtonStyleConfiguration
        @State private var hovering = false
        var body: some View {
            configuration.label
                .foregroundStyle(.white.opacity(hovering ? 1 : 0.85))
                .background(.white.opacity(hovering ? 0.2 : 0.1), in: Capsule())
                .contentShape(Capsule())
                .scaleEffect(configuration.isPressed ? 0.95 : 1)
                .onHover { hovering = $0 }
                .animation(Motion.hover, value: hovering)
                .animation(Motion.snappy, value: configuration.isPressed)
        }
    }
}

/// Three dots that take turns: the answer is on its way.
private struct ThinkingDots: View {
    let color: Color
    var body: some View {
        TimelineView(.animation(minimumInterval: 1 / 15, paused: Motion.reduce)) { context in
            let time = context.date.timeIntervalSinceReferenceDate
            HStack(spacing: 3) {
                ForEach(0..<3, id: \.self) { index in
                    Circle().fill(color)
                        .frame(width: 4, height: 4)
                        .opacity(0.35 + 0.65 * max(0, sin(time * 5 - Double(index) * 0.9)))
                }
            }
        }
        .accessibilityLabel("正在生成")
    }
}
