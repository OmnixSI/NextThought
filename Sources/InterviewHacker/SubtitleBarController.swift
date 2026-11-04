import AppKit
import Observation
import SwiftUI
import InterviewHackerCore

/// What the subtitle bar draws right now. Written by `SubtitleBarController`, read by
/// `SubtitleBarView`.
@MainActor
@Observable
final class SubtitleBarModel {
    var display: SubtitleDisplay?
    var phase: SubtitleBarTiming.Phase = .hidden
    /// The pointer is over the bar: it turns almost transparent and clicks pass through.
    var isDimmed = false
    /// The pointer has rested on the bar: it can be dragged.
    var isEditing = false
    /// Width of the card itself (the panel adds room for the shadow).
    var cardWidth: CGFloat = 800
}

/// A panel that can never become key or main window: the meeting app keeps the keyboard.
private final class SubtitlePanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

/// The floating subtitle bar (the "字幕条" mode of the live window).
///
/// - A non-activating panel above other windows, on every Space, never key.
/// - Appears when somebody speaks, fades out a few seconds after the last word.
/// - Click-through. While the pointer is over it, it turns almost transparent so
///   what is underneath stays visible; rest the pointer on it for a moment and it
///   becomes solid and can be dragged. The position is remembered.
/// - Excluded from screen sharing like the other windows (`capturePrivacy`), and
///   shows a badge when it is not.
///
/// It only follows the transcript while the mode is on: a 10 Hz timer reads the
/// partial and the last finished line from `AppState`. Outside the mode nothing runs.
@MainActor
final class SubtitleBarController {
    static let shared = SubtitleBarController()

    let model = SubtitleBarModel()

    /// Room around the card for the shadow and the badge.
    static let insets = NSEdgeInsets(top: 16, left: 20, bottom: 28, right: 20)
    private static let positionKey = "subtitleBar.position"
    private static let pollInterval: TimeInterval = 0.1
    private static let maxCardWidth: CGFloat = 900
    /// Horizontal padding inside the card (see `SubtitleBarView`).
    static let cardPadding = Space.s6

    private var panel: NSPanel?
    private var hostingView: NSView?
    private var moveObserver: NSObjectProtocol?
    private var timer: Timer?
    private var feed = SubtitleFeed()
    private let timing = SubtitleBarTiming.standard
    /// Whether this controller has the panel on screen. Another piece of code that
    /// hides every window for a moment (a screenshot) does not change it, so the bar
    /// is not put back while they are hidden.
    private var isOrderedIn = false
    /// Frame changes made here are not the user dragging.
    private var isAdjustingFrame = false
    private var lastMouse = NSPoint.zero
    private var stillSeconds: TimeInterval = 0

    // MARK: Start and stop

    func start() {
        guard timer == nil else { return }
        feed = SubtitleFeed()
        model.display = nil
        model.phase = .hidden
        model.isDimmed = false
        model.isEditing = false
        stillSeconds = 0
        ensurePanel()
        let next = Timer(timeInterval: Self.pollInterval, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.tick() }
        }
        RunLoop.main.add(next, forMode: .common)
        timer = next
    }

    func stop() {
        timer?.invalidate()
        timer = nil
        model.phase = .hidden
        model.isDimmed = false
        model.isEditing = false
        panel?.ignoresMouseEvents = true
        panel?.orderOut(nil)
        isOrderedIn = false
    }

    /// Screen-share exclusion follows the same setting as every other window.
    func applyPrivacy(_ enabled: Bool) {
        panel?.sharingType = enabled ? .none : .readOnly
    }

    /// Back to the default place: bottom centre of the screen.
    func resetPosition() {
        UserDefaults.standard.removeObject(forKey: Self.positionKey)
        if isOrderedIn { place() }
    }

    // MARK: Panel

    private func ensurePanel() {
        guard panel == nil else { return }
        let hosting = NSHostingView(rootView: SubtitleBarView(model: model).environment(AppState.shared))
        let next = SubtitlePanel(contentRect: NSRect(x: 0, y: 0, width: 840, height: 120),
                                 styleMask: [.borderless, .nonactivatingPanel],
                                 backing: .buffered, defer: false)
        next.title = String(localized: "InterviewHacker 字幕条")
        next.isFloatingPanel = true
        next.level = .floating
        next.hidesOnDeactivate = false
        next.isReleasedWhenClosed = false
        next.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
        next.isOpaque = false
        next.backgroundColor = .clear
        // The card draws its own shadow (floatingSurface).
        next.hasShadow = false
        next.animationBehavior = .none
        next.isMovableByWindowBackground = true
        // Click-through until the pointer rests on the bar.
        next.ignoresMouseEvents = true
        next.sharingType = AppState.shared.capturePrivacy ? .none : .readOnly
        next.contentView = hosting
        moveObserver = NotificationCenter.default.addObserver(forName: NSWindow.didMoveNotification, object: next, queue: .main) { [weak self] _ in
            MainActor.assumeIsolated { self?.panelMoved() }
        }
        panel = next
        hostingView = hosting
    }

    /// Width from the screen, bottom centre or the remembered spot; height is fitted
    /// to the text by `fitHeight`.
    private func place() {
        guard let panel else { return }
        var center: CGFloat?
        var bottom: CGFloat?
        var screen: NSScreen?
        if let saved = UserDefaults.standard.array(forKey: Self.positionKey) as? [Double], saved.count == 2 {
            let point = NSPoint(x: saved[0], y: saved[1] + 20)
            screen = NSScreen.screens.first { $0.frame.contains(point) }
            if screen != nil { center = CGFloat(saved[0]); bottom = CGFloat(saved[1]) }
        }
        let target = screen
            ?? NSScreen.screens.first { NSMouseInRect(NSEvent.mouseLocation, $0.frame, false) }
            ?? NSScreen.main
        guard let target else { return }
        let visible = target.visibleFrame
        let cardWidth = min(Self.maxCardWidth, visible.width - 160)
        if abs(model.cardWidth - cardWidth) > 0.5 { model.cardWidth = cardWidth }
        let width = cardWidth + Self.insets.left + Self.insets.right
        let x = (center ?? visible.midX) - width / 2
        let y = bottom ?? (visible.minY + 24)
        setFrame(NSRect(x: x, y: y, width: width, height: max(panel.frame.height, 60)))
    }

    private func setFrame(_ frame: NSRect) {
        isAdjustingFrame = true
        panel?.setFrame(frame, display: true)
        isAdjustingFrame = false
    }

    /// The bar grows upwards: the bottom edge stays where the user put it.
    private func fitHeight() {
        guard let panel, let hostingView else { return }
        let height = ceil(hostingView.fittingSize.height)
        guard height > 0, abs(height - panel.frame.height) > 1 else { return }
        var frame = panel.frame
        frame.size.height = height
        setFrame(frame)
    }

    private func panelMoved() {
        guard !isAdjustingFrame, let panel else { return }
        UserDefaults.standard.set([Double(panel.frame.midX), Double(panel.frame.minY)], forKey: Self.positionKey)
    }

    /// The visible card inside the panel (without the shadow room).
    private func cardRect(of frame: NSRect) -> NSRect {
        NSRect(x: frame.minX + Self.insets.left, y: frame.minY + Self.insets.bottom,
               width: frame.width - Self.insets.left - Self.insets.right,
               height: frame.height - Self.insets.top - Self.insets.bottom)
    }

    // MARK: Following the transcript

    private func tick() {
        guard let panel else { return }
        refresh(now: Date())
        if model.phase == .hidden {
            if isOrderedIn {
                setEditing(false)
                model.isDimmed = false
                panel.orderOut(nil)
                isOrderedIn = false
            }
            return
        }
        if !isOrderedIn {
            // The emergency "hide everything" shortcut is on: stay away until it is off.
            guard !TeleprompterController.shared.isPanicHidden else { return }
            place()
            panel.orderFrontRegardless()
            isOrderedIn = true
        }
        fitHeight()
        updatePointer()
    }

    private func refresh(now: Date) {
        let state = AppState.shared
        let meSpeaker: SessionSpeaker = state.isOfflineMeeting ? .room : .me
        let channels = [
            SubtitleChannel(speaker: .remote, partial: state.partialTranscript,
                            partialTranslation: state.liveTranslation.systemPartialTranslation),
            SubtitleChannel(speaker: meSpeaker, partial: state.microphonePartialTranscript,
                            partialTranslation: state.liveTranslation.microphonePartialTranslation)
        ]
        let lastFinal = state.entries.last(where: { $0.suspectedEcho != true }).map {
            SubtitleLine(speaker: $0.speaker, text: $0.text, translation: $0.translatedText ?? "")
        }
        feed.update(channels: channels, lastFinal: lastFinal, now: now)

        // A bar the user is holding stays up.
        let phase = model.isEditing ? SubtitleBarTiming.Phase.visible : timing.phase(now: now, lastActivity: feed.lastActivity)
        if model.phase != phase { model.phase = phase }

        guard let line = feed.line else { return }
        let fontSize = CGFloat(state.liveWindow.subtitleFontSize)
        let inner = max(200, model.cardWidth - 2 * Self.cardPadding)
        // Half-em units per line, with some room for words that wrap early. The main
        // line gets two lines, the original beneath it one line at 60 % size.
        let tag: CGFloat = line.speaker == .remote ? 0 : 8
        let primaryUnits = Int(2 * inner / (fontSize * 0.5) * 0.8 - tag)
        let secondaryUnits = Int(inner / (fontSize * 0.6 * 0.5) * 0.8)
        let display = SubtitleText.display(for: line, primaryUnits: primaryUnits, secondaryUnits: secondaryUnits)
        if model.display != display { model.display = display }
    }

    // MARK: Pointer

    private func updatePointer() {
        guard let panel else { return }
        let mouse = NSEvent.mouseLocation
        let moved = hypot(mouse.x - lastMouse.x, mouse.y - lastMouse.y) > 2
        lastMouse = mouse
        let over = cardRect(of: panel.frame).insetBy(dx: -4, dy: -4).contains(mouse)

        if model.isEditing {
            // Stay solid while the pointer is on the bar or a button is held (a drag).
            if !over, NSEvent.pressedMouseButtons == 0 { setEditing(false) }
            return
        }
        guard model.phase == .visible else {
            stillSeconds = 0
            setDimmed(false)
            return
        }
        if over {
            setDimmed(true)
            stillSeconds = moved ? 0 : stillSeconds + Self.pollInterval
            if stillSeconds >= 0.6 {
                setDimmed(false)
                setEditing(true)
            }
        } else {
            stillSeconds = 0
            setDimmed(false)
        }
    }

    private func setDimmed(_ on: Bool) {
        if model.isDimmed != on { model.isDimmed = on }
    }

    private func setEditing(_ on: Bool) {
        if model.isEditing != on { model.isEditing = on }
        panel?.ignoresMouseEvents = !on
        if !on { stillSeconds = 0 }
    }
}
