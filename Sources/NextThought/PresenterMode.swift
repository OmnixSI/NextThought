import AppKit
import Observation

/// Dual-screen presenting: the slides are on the external display (the one that is
/// shared), and the MacBook, with the camera, carries the speaker notes and NextThought.
///
/// While a meeting runs with this on:
/// - the cue island stays open on the camera's display, so the answers and the current
///   slide's talking points are read while looking at the lens;
/// - the main window steps aside (it would cover the speaker notes); ⌃⌥S brings it back;
/// - the slides display is read every couple of seconds, so the island follows the deck
///   that was added to the series and answers can refer to "this slide";
/// - nothing of NextThought is drawn on the slides display.
/// Everything it changed is put back when the meeting ends or the mode is turned off.
@MainActor
@Observable
final class PresenterMode {
    static let shared = PresenterMode()

    var isEnabled = UserDefaults.standard.bool(forKey: "presenterMode") {
        didSet {
            guard isEnabled != oldValue else { return }
            UserDefaults.standard.set(isEnabled, forKey: "presenterMode")
            if AppState.shared.isRunning { isEnabled ? begin() : end() }
        }
    }

    /// Name of the display the slides are on; empty picks the first display that is not
    /// the camera's.
    var slidesScreenName = UserDefaults.standard.string(forKey: "presenterSlidesScreen") ?? "" {
        didSet {
            UserDefaults.standard.set(slidesScreenName, forKey: "presenterSlidesScreen")
            if isActive { followSlides() }
        }
    }

    /// The mode is shaping the running meeting.
    private(set) var isActive = false

    /// What the mode changed, to put back afterwards.
    @ObservationIgnored private var saved: (screenWatch: Bool, target: String, region: [Double]?, follow: Bool)?

    /// The display the camera is on (the MacBook's own).
    var cameraScreen: NSScreen? {
        CueIslandController.cameraScreen(named: CueIslandController.shared.model.screenName)
    }

    /// Displays the slides could be on.
    var otherScreens: [NSScreen] {
        let camera = cameraScreen
        return NSScreen.screens.filter { $0 != camera }
    }

    var slidesScreen: NSScreen? {
        let others = otherScreens
        if !slidesScreenName.isEmpty, let chosen = others.first(where: { $0.localizedName == slidesScreenName }) { return chosen }
        return others.first
    }

    /// One short line for menus and settings: where the slides go.
    var summary: String {
        if let screen = slidesScreen { return String(localized: "幻灯片在 \(screen.localizedName)，提示在 MacBook") }
        return String(localized: "接上外接屏后可用")
    }

    // MARK: Meeting

    /// Called when a meeting starts (and when the mode is turned on during one).
    func begin() {
        // One display only: nothing to present on, and hiding the page would hide the meeting.
        guard isEnabled, AppState.shared.isRunning, !isActive, slidesScreen != nil else { return }
        isActive = true
        let island = CueIslandController.shared
        island.model.presenting = true
        island.show()
        // The page would sit on top of the speaker notes; the island carries the meeting.
        AppState.shared.liveWindow.set(.hidden)
        followSlides()
        Diagnostics.record("presenter.begin", ["screens": NSScreen.screens.count])
    }

    /// The external display came or went during a meeting.
    func displaysChanged() {
        guard isEnabled, AppState.shared.isRunning else { return }
        if slidesScreen == nil { end() } else if !isActive { begin() } else { followSlides() }
    }

    /// Called when the meeting ends (and when the mode is turned off during one).
    func end() {
        guard isActive else { return }
        isActive = false
        CueIslandController.shared.model.presenting = false
        let state = AppState.shared
        if let saved {
            state.insights.screenWatch = saved.screenWatch
            state.insights.screenTarget = saved.target
            state.insights.screenRegion = saved.region
            state.insights.followSlides = saved.follow
            self.saved = nil
            state.startScreenWatch()
        }
        if state.isRunning { state.liveWindow.set(.notes) }
        CueIslandController.shared.refreshPresence()
    }

    /// Reads the slides display for the meeting, without changing the saved preference.
    private func followSlides() {
        let state = AppState.shared
        guard let screen = slidesScreen,
              let number = screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber else { return }
        if saved == nil {
            saved = (state.insights.screenWatch, state.insights.screenTarget, state.insights.screenRegion, state.insights.followSlides)
        }
        state.insights.screenWatch = true
        state.insights.screenTarget = "display:\(number.uint32Value)"
        state.insights.screenRegion = nil
        state.insights.followSlides = true
        state.startScreenWatch()
    }
}
