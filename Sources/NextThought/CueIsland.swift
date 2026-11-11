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
        didSet { UserDefaults.standard.set(surface.rawValue, forKey: "cueSurface") }
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
}

@MainActor
final class CueIslandController {
    static let shared = CueIslandController()
    let model = CueIslandModel()
    private var panel: NSPanel?
    private var tracker: Timer?
    private var observers: [NSObjectProtocol] = []
    private static let canvas = CGSize(width: 760, height: 460)

    var isVisible: Bool { panel?.isVisible == true }

    func toggle() { isVisible ? hide() : show() }

    func show() {
        if panel == nil { panel = makePanel() }
        reposition()
        panel?.sharingType = AppState.shared.capturePrivacy ? .none : .readOnly
        panel?.orderFrontRegardless()
        startTracking()
    }

    func hide() {
        panel?.orderOut(nil)
        tracker?.invalidate(); tracker = nil
        model.hovering = false
        model.preview = false
    }

    /// Follows the meeting: the island appears with a scene that prompts beside the
    /// camera and leaves when the meeting ends.
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
            state.coach.begin(mode: mode, startedAt: state.sessionStartedAt ?? Date())
            if mode != .custom {
                state.lastScene = mode
                UserDefaults.standard.set(mode.rawValue, forKey: "lastScene")
            }
            model.collapsedKey = nil; model.covered = []
            if model.surface == .island, ScenePreset.for(mode).eyeLine { show() }
        } else {
            state.coach.end()
            if !model.preview { hide() }
        }
    }

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
            metrics.notchWidth = demoNotch && notch == 0 ? 186 : max(120, screen.frame.width - left - right)
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
            MainActor.assumeIsolated { self?.reposition() }
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

    private func track() {
        guard let panel, panel.isVisible else { return }
        let size = model.islandSize
        let rect = NSRect(x: panel.frame.midX - size.width / 2,
                          y: panel.frame.maxY - model.metrics.topOffset - size.height,
                          width: size.width, height: size.height)
        // A little slack below, so the pointer does not fall off between rows.
        let inside = size.width > 0 && rect.insetBy(dx: -6, dy: -6).contains(NSEvent.mouseLocation)
        if inside != model.hovering {
            model.hovering = inside
            panel.ignoresMouseEvents = !inside
        }
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

    enum Phase: Equatable { case hidden, idle, hearing(String), cue }

    private var mode: SessionMode { (state.preparedWorkspace ?? state.activeWorkspace).effectiveMode }
    private var tint: Color { mode.theme.tint }
    private var answerKey: String {
        "\(state.currentAnswerID.map { "\($0)" } ?? "-")|\(state.historyCursor ?? -1)|\(state.displayedQuestion.count)"
    }
    private var answer: String { model.preview && state.displayedAnswer.isEmpty ? Self.sampleAnswer : state.displayedAnswer }
    private var question: String {
        if model.preview && state.displayedQuestion.isEmpty && state.pendingQuestion.isEmpty { return Self.sampleQuestion }
        let text = state.displayedQuestion.isEmpty ? state.pendingQuestion : state.displayedQuestion
        return AnswerRefinement.originalQuestion(of: text)
    }
    private var cueStyle: CueStyle {
        switch model.style {
        case .scene: ScenePreset.for(mode).cueStyle
        case .keyPoints: .keyPoints
        case .script: .script
        }
    }
    private var lines: [CueLine] { CueScript.lines(from: answer, style: cueStyle) }

    private var phase: Phase {
        if model.preview { return .cue }
        let hasAnswer = !state.displayedAnswer.isEmpty || state.isAnswering
        if hasAnswer, model.collapsedKey != answerKey { return .cue }
        if state.isRunning, let candidate = state.liveCandidate, candidate.state != .triggered,
           !candidate.text.isEmpty { return .hearing(candidate.text) }
        return state.isRunning || hasAnswer ? .idle : .hidden
    }

    var body: some View {
        let phase = phase
        ZStack(alignment: .top) {
            if model.backdrop {
                LinearGradient(colors: [Color(nsColor: .hex(0x9db4d6)), Color(nsColor: .hex(0xd9c7b8))], startPoint: .topLeading, endPoint: .bottomTrailing)
            }
            if phase != .hidden {
                island(phase)
                    .transition(.opacity.combined(with: .scale(scale: 0.7, anchor: .top)))
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        .environment(\.colorScheme, .dark)
        .animation(Motion.morph, value: phase)
        .animation(Motion.expand, value: model.hovering)
        .task(id: state.isRunning) {
            // Monologues grow between transcript lines; the coach looks once a second.
            while state.isRunning, !Task.isCancelled {
                state.coach.refresh()
                try? await Task.sleep(for: .seconds(1))
            }
        }
        .task(id: "\(answerKey)|\(state.isAnswering)") { await settle() }
        .onChange(of: state.entries.count) { _, _ in refreshCoverage() }
        .onChange(of: answerKey) { _, _ in model.covered = [] }
    }

    // MARK: Island

    private func island(_ phase: Phase) -> some View {
        let metrics = model.metrics
        let flare: CGFloat = metrics.hasNotch ? 8 : 0
        let idle = phase == .idle
        let shape = IslandShape(flare: flare, radius: idle ? (metrics.hasNotch ? 12 : 15) : 24)
        let edge: Color = state.coach.shouldLookUp ? Ink.caution : tint
        return islandContent(phase)
            .frame(width: width(phase))
            .padding(.horizontal, flare)
            .modifier(IslandChrome(shape: shape, edge: edge, edgeOpacity: idle ? 0 : 0.85,
                                   edgeWidth: state.coach.shouldLookUp ? 2 : 1.2,
                                   floating: !metrics.hasNotch, lifted: !idle))
            .onTapGesture {
                // A collapsed island opens again on a click.
                if idle, model.collapsedKey != nil { model.collapsedKey = nil }
            }
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
        }
    }

    private func width(_ phase: Phase) -> CGFloat {
        let metrics = model.metrics
        let ears: CGFloat = metrics.hasNotch ? metrics.notchWidth + 2 * (state.coach.nudge != nil || state.coach.shouldLookUp ? 132 : 76) : 164
        switch phase {
        case .hidden, .idle: return ears
        case .hearing: return max(ears, min(model.width.points, 440))
        case .cue: return max(ears, model.width.points)
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
            Spacer(minLength: metrics.hasNotch ? metrics.notchWidth : 8)
            coachEar
        }
        .padding(.horizontal, 14)
        .frame(height: metrics.barHeight)
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
        .symbolEffect(.bounce, value: text)
    }

    /// A question is being asked: its words, as they are heard.
    private func hearing(_ text: String) -> some View {
        HStack(spacing: 8) {
            Image(systemName: "waveform").font(.system(size: 11, weight: .semibold)).foregroundStyle(tint)
                .symbolEffect(.variableColor.iterative, options: .repeating)
            Text(verbatim: text)
                .font(.system(size: 13, weight: .medium)).foregroundStyle(.white.opacity(0.82))
                .lineLimit(1).truncationMode(.head)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(.horizontal, 18).padding(.top, 2).padding(.bottom, 12)
        .transition(.opacity.combined(with: .move(edge: .top)))
    }

    // MARK: Cue

    private var cue: some View {
        let lines = lines
        let size = CGFloat(state.teleprompterFontSize)
        let next = lines.first { !model.covered.contains($0.id) }?.id
        let visible = Self.window(lines, next: next, limit: cueStyle == .script ? 4 : 6)
        return VStack(alignment: .leading, spacing: 0) {
            if !question.isEmpty {
                HStack(spacing: 6) {
                    Text(verbatim: question)
                        .font(.system(size: 12, weight: .medium)).foregroundStyle(.white.opacity(0.5)).lineLimit(1)
                    Spacer(minLength: 0)
                    if state.isAnswering { ThinkingDots(color: tint) }
                }
                .padding(.bottom, 8)
            }
            if lines.isEmpty {
                Text(state.isAnswering ? "正在想…" : "被问到时，回答会出现在这里。")
                    .font(.system(size: size * 0.8, weight: .medium)).foregroundStyle(.white.opacity(0.55))
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            VStack(alignment: .leading, spacing: size * 0.34) {
                ForEach(visible) { line in
                    CueRow(line: line, size: size, tint: tint, said: model.covered.contains(line.id),
                           current: line.id == next && !state.isAnswering,
                           expanded: model.hovering || cueStyle == .script)
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
            IslandButton(symbol: "chevron.up", help: String(localized: "收起 · \(state.shortcutLabel(.toggleTeleprompter)) 隐藏提词岛")) {
                model.preview = false
                model.collapsedKey = answerKey
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
        let said = Set(lines.filter { AnswerPoints.heuristicCovered($0.spoken, speech: speech) }.map(\.id))
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
