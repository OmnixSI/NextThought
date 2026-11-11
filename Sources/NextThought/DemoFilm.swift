import AppKit
import NextThoughtCore

/// Frame-by-frame recordings of scripted demo scenes, for the README's animations.
///
/// Stage mode only (`scripts/dev/preview.sh walk`): `NEXTTHOUGHT_DEMO_FILM=island|home`
/// plays a short script with demo data and writes every frame the app draws into
/// `NEXTTHOUGHT_DEMO_FILM_DIR` as PNG files, then quits. The app renders its own
/// windows, so no Screen Recording permission is involved.
@MainActor
enum DemoFilm {
    private static var timer: Timer?
    private static var started = Date()

    static func begin(_ environment: [String: String]) {
        guard let kind = environment["NEXTTHOUGHT_DEMO_FILM"], let folder = environment["NEXTTHOUGHT_DEMO_FILM_DIR"] else { return }
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        switch kind {
        case "island": island(folder)
        case "home": home(folder)
        default: break
        }
    }

    /// Runs `step` at `time` seconds into the film.
    private static func at(_ time: Double, _ step: @escaping @MainActor () -> Void) {
        DispatchQueue.main.asyncAfter(deadline: .now() + time) { MainActor.assumeIsolated { step() } }
    }

    private static var frames: [(rep: NSBitmapImageRep, time: Double)] = []

    /// Draws the window into memory at 1x on every tick (encoding PNGs while recording
    /// would halve the frame rate), then writes the frames and their times and quits.
    private static func record(_ window: @escaping @MainActor () -> NSWindow?, to folder: String, fps: Double, seconds: Double) {
        started = Date(); frames = []
        let timer = Timer(timeInterval: 1 / fps, repeats: true) { _ in
            MainActor.assumeIsolated {
                let elapsed = Date().timeIntervalSince(started)
                if let view = window()?.contentView?.superview ?? window()?.contentView {
                    let size = view.bounds.size
                    if let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(size.width), pixelsHigh: Int(size.height),
                                                  bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                                                  colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0) {
                        rep.size = size
                        view.cacheDisplay(in: view.bounds, to: rep)
                        frames.append((rep, elapsed))
                    }
                }
                guard elapsed >= seconds else { return }
                self.timer?.invalidate()
                for (index, frame) in frames.enumerated() {
                    try? frame.rep.representation(using: .png, properties: [:])?
                        .write(to: URL(fileURLWithPath: folder + String(format: "/f%04d.png", index)))
                }
                try? frames.map { String(format: "%.3f", $0.time) }.joined(separator: "\n")
                    .write(toFile: folder + "/times.txt", atomically: true, encoding: .utf8)
                try? "\(frames.count)".write(toFile: folder + "/done", atomically: true, encoding: .utf8)
                exit(0)
            }
        }
        RunLoop.main.add(timer, forMode: .common)
        self.timer = timer
    }

    /// Listening, a question heard, the answer streaming in, a closer look, the deck.
    private static func island(_ folder: String) {
        let state = AppState.shared
        let controller = CueIslandController.shared
        let answer = state.displayedAnswer, question = state.displayedQuestion, candidate = state.liveCandidate
        state.answer = ""; state.answerHistory = []; state.currentAnswerID = nil; state.historyCursor = nil
        state.currentQuestion = ""; state.liveCandidate = nil
        controller.model.backdrop = true
        controller.model.demoHover = false
        for window in NSApp.windows where !(window is NSPanel) { window.orderOut(nil) }
        controller.show()
        let panel = { NSApp.windows.first { $0.title == String(localized: "NextThought 提词岛") } }
        record(panel, to: folder, fps: 20, seconds: 12.6)

        at(1.0) { state.liveCandidate = candidate }
        at(2.6) {
            state.liveCandidate = nil
            state.pendingQuestion = question; state.answerDraft = ""; state.isAnswering = true
        }
        // The answer arrives a few characters at a time, as it does from a model.
        let characters = Array(answer)
        let steps = 30
        for step in 1...steps {
            at(2.7 + Double(step) * 0.075) { state.answerDraft = String(characters.prefix(characters.count * step / steps)) }
        }
        at(5.2) {
            state.isAnswering = false; state.answer = answer; state.currentQuestion = question
            state.pendingQuestion = ""; state.answerDraft = ""
        }
        at(6.6) { controller.model.demoHover = true }
        at(8.4) { controller.model.demoHover = false }
        at(9.2) { controller.model.collapsedKey = CueIslandView.answerKey(state) }
        at(10.0) { controller.model.pinned = true }
        at(11.8) { controller.model.pinned = false }
    }

    /// The home page taking on one scene after another.
    private static func home(_ folder: String) {
        let state = AppState.shared
        record({ NSApp.windows.first(where: MainWindowLayout.isMainWindow) }, to: folder, fps: 15, seconds: 7.6)
        let scenes: [SessionMode] = [.labMeeting, .academic, .groupProject, .client, .lesson, .interview]
        for (index, scene) in scenes.enumerated() {
            at(1.0 + Double(index) * 1.05) { state.lastScene = scene }
        }
    }
}
