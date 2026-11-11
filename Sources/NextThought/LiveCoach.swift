import AVFoundation
import Foundation
import NextThoughtCore
import Observation
import Vision

/// The live coach: how you are coming across, while it can still be changed. Pace,
/// monologue length and share of the talking come from transcript timestamps; eye
/// contact comes from face landmarks read off the camera. Everything stays on this Mac.
@MainActor
@Observable
final class LiveCoach {
    // MARK: Preferences
    var isEnabled = UserDefaults.standard.object(forKey: "coachEnabled") as? Bool ?? true {
        didSet {
            UserDefaults.standard.set(isEnabled, forKey: "coachEnabled")
            // Switching the coach off mid-meeting also lets go of the camera.
            if !isEnabled { camera.stop(); gaze = nil; nudge = nil; nudgeShownAt = nil; resetAway() }
        }
    }
    /// Watching the eyes needs the camera, so it is off until the user turns it on.
    var watchesGaze = UserDefaults.standard.object(forKey: "coachWatchesGaze") as? Bool ?? false {
        didSet {
            UserDefaults.standard.set(watchesGaze, forKey: "coachWatchesGaze")
            if !watchesGaze { camera.stop(); gaze = nil; resetAway() }
        }
    }
    var gazeSensitivity = GazeSensitivity(rawValue: UserDefaults.standard.string(forKey: "coachGazeSensitivity") ?? "") ?? .standard {
        didSet {
            UserDefaults.standard.set(gazeSensitivity.rawValue, forKey: "coachGazeSensitivity")
            camera.setTolerance(gazeSensitivity.tolerance)
        }
    }

    // MARK: Live readings
    private(set) var snapshot = CoachSnapshot()
    /// The nudge on screen right now; cleared after a few seconds.
    private(set) var nudge: CoachNudge?
    private(set) var gaze: GazeReading?
    /// How long the eyes have been off the lens while the user was speaking.
    private(set) var awayWhileSpeaking: TimeInterval = 0
    private(set) var contactRatio: Double?
    private(set) var cameraAccess: AVAuthorizationStatus = AVCaptureDevice.authorizationStatus(for: .video)
    private(set) var isCalibrating = false
    var isCalibrated: Bool { camera.isCalibrated }
    /// The camera is running. Mirrored from `GazeCamera`, whose own flag is not observable.
    private(set) var isWatching = false

    let camera = GazeCamera()
    @ObservationIgnored private var coach = SpeakingCoach(profile: ScenePreset.for(.general).coaching)
    @ObservationIgnored private var startedAt = Date()
    @ObservationIgnored private var lastNudgeAt: [CoachNudge: Date] = [:]
    @ObservationIgnored private var nudgeShownAt: Date?
    @ObservationIgnored private var lastSpokeAt: Date?
    @ObservationIgnored private var awaySince: Date?
    @ObservationIgnored private var tick: Task<Void, Never>?
    @ObservationIgnored private var accumulator = CoachSummaryAccumulator()
    @ObservationIgnored private var wasLookingUp = false

    init() {
        camera.onReading = { [weak self] reading, ratio in
            self?.receive(reading, ratio: ratio)
        }
        camera.onRunningChange = { [weak self] running in
            guard let self, self.isWatching != running else { return }
            self.isWatching = running
            if !running { self.isCalibrating = false }
        }
    }

    private func resetAway() {
        awaySince = nil
        if awayWhileSpeaking != 0 { awayWhileSpeaking = 0 }
        wasLookingUp = false
    }

    // MARK: Meeting lifecycle

    func begin(mode: SessionMode, startedAt: Date) {
        self.startedAt = startedAt
        coach = SpeakingCoach(profile: ScenePreset.for(mode).coaching)
        snapshot = CoachSnapshot(); nudge = nil; nudgeShownAt = nil; lastNudgeAt = [:]
        resetAway(); contactRatio = nil; lastSpokeAt = nil
        accumulator = CoachSummaryAccumulator()
        if isEnabled, watchesGaze { startWatching() }
        // Monologues grow between transcript lines, so the coach looks once a second,
        // whether or not the island is on screen.
        tick?.cancel()
        tick = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(for: .seconds(1))
                guard !Task.isCancelled else { return }
                self?.refresh()
            }
        }
    }

    /// Stops the coach and returns what it measured (nil when there was nothing to say).
    @discardableResult
    func end() -> CoachSummary? {
        tick?.cancel(); tick = nil
        accumulator.observeEyeContact(contactRatio)
        let summary = isEnabled ? accumulator.summary() : nil
        camera.stop()
        gaze = nil; nudge = nil; nudgeShownAt = nil
        resetAway()
        return summary.flatMap { $0.isEmpty && $0.nudges.isEmpty ? nil : $0 }
    }

    /// A finished transcript line from either side.
    func ingest(_ entry: TranscriptEntry) {
        guard isEnabled else { return }
        let end = (entry.endedAt ?? entry.date).timeIntervalSince(startedAt)
        // Lines without timestamps get a plausible length from their text.
        let start = entry.startedAt?.timeIntervalSince(startedAt) ?? end - max(1, SpeechUnits.count(entry.text) / 4.2)
        let segment = SpokenSegment(isMe: entry.isMe, start: start, end: end, text: entry.text)
        coach.ingest(segment)
        accumulator.ingest(segment)
        if entry.isMe { lastSpokeAt = entry.endedAt ?? entry.date }
        refresh()
    }

    /// Called about once a second while a meeting runs: monologues grow between lines.
    func refresh(now: Date = Date()) {
        guard isEnabled else { return }
        let next = coach.snapshot(at: now.timeIntervalSince(startedAt))
        if next != snapshot { snapshot = next }
        accumulator.observe(next)
        accumulator.observeEyeContact(contactRatio)
        if let shown = nudgeShownAt, now.timeIntervalSince(shown) > 7 { nudge = nil; nudgeShownAt = nil }
        guard nudge == nil, let candidate = next.nudge else { return }
        // The same advice at most once a minute and a half: a coach, not a metronome.
        if let last = lastNudgeAt[candidate], now.timeIntervalSince(last) < 90 { return }
        lastNudgeAt[candidate] = now
        nudge = candidate; nudgeShownAt = now
        accumulator.record(candidate)
    }

    // MARK: Gaze

    func requestCameraAndWatch() {
        AVCaptureDevice.requestAccess(for: .video) { granted in
            Task { @MainActor in
                self.cameraAccess = AVCaptureDevice.authorizationStatus(for: .video)
                if granted { self.startWatching() } else { self.watchesGaze = false }
            }
        }
    }

    func startWatching() {
        cameraAccess = AVCaptureDevice.authorizationStatus(for: .video)
        guard cameraAccess == .authorized else {
            if cameraAccess == .notDetermined { requestCameraAndWatch() }
            return
        }
        camera.setTolerance(gazeSensitivity.tolerance)
        camera.start()
    }

    func stopWatching() {
        camera.stop()
        gaze = nil
        resetAway()
    }

    /// Two seconds of looking into the lens become the user's on-camera pose.
    func calibrate() async -> Bool {
        guard camera.isRunning else { return false }
        isCalibrating = true
        defer { isCalibrating = false }
        return await camera.calibrate(seconds: 2)
    }

    private func receive(_ reading: GazeReading, ratio: Double?) {
        gaze = reading
        contactRatio = ratio
        if !isWatching { isWatching = true }
        let now = Date()
        let speaking = lastSpokeAt.map { now.timeIntervalSince($0) < 4 } ?? false
        if reading.state == .away, speaking {
            if awaySince == nil { awaySince = now }
            awayWhileSpeaking = now.timeIntervalSince(awaySince ?? now)
        } else {
            awaySince = nil
            if awayWhileSpeaking != 0 { awayWhileSpeaking = 0 }
        }
        let lookingUp = shouldLookUp
        if lookingUp, !wasLookingUp { accumulator.recordLookUp() }
        wasLookingUp = lookingUp
    }

    /// The eyes have been off the lens long enough, mid-sentence, to be worth a cue.
    var shouldLookUp: Bool { awayWhileSpeaking >= 4 }
}

enum GazeSensitivity: String, CaseIterable, Identifiable {
    case relaxed, standard, strict
    var id: String { rawValue }
    var tolerance: Double {
        switch self {
        case .relaxed: 0.17
        case .standard: 0.12
        case .strict: 0.09
        }
    }
    var title: String {
        switch self {
        case .relaxed: String(localized: "宽松")
        case .standard: String(localized: "标准")
        case .strict: String(localized: "严格")
        }
    }
}

extension CoachNudge {
    var message: String {
        switch self {
        case .wrapUp: String(localized: "说了一阵了，收个尾")
        case .slowDown: String(localized: "慢一点")
        case .letThemTalk: String(localized: "停一下，听对方说")
        case .speakUp: String(localized: "该你说两句了")
        }
    }
    var symbol: String {
        switch self {
        case .wrapUp: "hourglass"
        case .slowDown: "tortoise.fill"
        case .letThemTalk: "ear.fill"
        case .speakUp: "bubble.left.fill"
        }
    }
}

// MARK: - Camera

/// Reads face landmarks off the camera at a few frames a second and reduces them to
/// a gaze reading. Frames are analysed in memory and dropped; nothing is recorded.
final class GazeCamera: NSObject, AVCaptureVideoDataOutputSampleBufferDelegate, @unchecked Sendable {
    /// Called on the main actor, at most a few times a second.
    @MainActor var onReading: ((GazeReading, Double?) -> Void)?
    /// Called on the main actor when the session starts or stops running.
    @MainActor var onRunningChange: ((Bool) -> Void)?

    private let session = AVCaptureSession()
    private let queue = DispatchQueue(label: "nextthought.gaze", qos: .utility)
    private let request = VNDetectFaceLandmarksRequest()
    // Touched on `queue` only.
    private var estimator = GazeEstimator()
    private var configured = false
    private var lastAnalysis: CFTimeInterval = 0
    private var calibrationEnd: CFTimeInterval?
    private var calibrationDone: ((Bool) -> Void)?
    private let lock = NSLock()
    private var running = false
    private var calibrated = false

    var isRunning: Bool { lock.withLock { running } }
    var isCalibrated: Bool { lock.withLock { calibrated } }

    private static let baselineKey = "coachGazeBaseline"

    override init() {
        super.init()
        // The pose learned last time still describes this seat and this camera.
        if let saved = UserDefaults.standard.array(forKey: Self.baselineKey) as? [Double], saved.count == 4 {
            estimator.restore(baseline: GazeSignature(x: saved[0], y: saved[1], yaw: saved[2], pitch: saved[3]))
            calibrated = true
        }
    }

    func setTolerance(_ tolerance: Double) {
        queue.async { self.estimator.tolerance = tolerance }
    }

    func start() {
        queue.async {
            guard !self.session.isRunning else { return }
            if !self.configured { self.configured = self.configure() }
            guard self.configured else { return }
            self.estimator.resetSession()
            self.session.startRunning()
            let isRunning = self.session.isRunning
            self.lock.withLock { self.running = isRunning }
            Task { @MainActor in self.onRunningChange?(isRunning) }
        }
    }

    func stop() {
        queue.async {
            if self.session.isRunning { self.session.stopRunning() }
            self.lock.withLock { self.running = false }
            // A calibration cannot finish without frames: release its waiter, and keep
            // the frames it had collected from leaking into the next one.
            self.calibrationEnd = nil
            self.estimator.cancelCalibration()
            self.calibrationDone?(false)
            self.calibrationDone = nil
            Task { @MainActor in self.onRunningChange?(false) }
        }
    }

    func calibrate(seconds: Double) async -> Bool {
        await withCheckedContinuation { continuation in
            queue.async {
                self.calibrationDone?(false)
                self.calibrationEnd = CACurrentMediaTime() + seconds
                self.calibrationDone = { continuation.resume(returning: $0) }
            }
        }
    }

    private func configure() -> Bool {
        // The camera above the screen the cues sit on: the built-in one when present.
        let discovery = AVCaptureDevice.DiscoverySession(deviceTypes: [.builtInWideAngleCamera, .external],
                                                         mediaType: .video, position: .unspecified)
        guard let device = discovery.devices.first(where: { $0.deviceType == .builtInWideAngleCamera }) ?? discovery.devices.first,
              let input = try? AVCaptureDeviceInput(device: device) else { return false }
        session.beginConfiguration()
        defer { session.commitConfiguration() }
        session.sessionPreset = .vga640x480
        guard session.canAddInput(input) else { return false }
        session.addInput(input)
        let output = AVCaptureVideoDataOutput()
        output.alwaysDiscardsLateVideoFrames = true
        output.setSampleBufferDelegate(self, queue: queue)
        guard session.canAddOutput(output) else { return false }
        session.addOutput(output)
        return true
    }

    func captureOutput(_ output: AVCaptureOutput, didOutput sampleBuffer: CMSampleBuffer, from connection: AVCaptureConnection) {
        // Six readings a second are plenty for where someone is looking.
        let now = CACurrentMediaTime()
        guard now - lastAnalysis >= 1.0 / 6.0, let pixels = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }
        lastAnalysis = now
        let size = CGSize(width: CVPixelBufferGetWidth(pixels), height: CVPixelBufferGetHeight(pixels))
        let handler = VNImageRequestHandler(cvPixelBuffer: pixels, orientation: .up)
        try? handler.perform([request])
        // The largest face is the person at the keyboard.
        let face = request.results?.max { $0.boundingBox.width * $0.boundingBox.height < $1.boundingBox.width * $1.boundingBox.height }
        let frame = face.flatMap { Self.frame(from: $0, imageSize: size, time: now) }

        if let end = calibrationEnd {
            if let frame { estimator.addCalibration(frame) }
            if now >= end {
                let success = estimator.finishCalibration()
                if success {
                    let baseline = estimator.baseline
                    UserDefaults.standard.set([baseline.x, baseline.y, baseline.yaw, baseline.pitch], forKey: Self.baselineKey)
                    lock.withLock { calibrated = true }
                }
                calibrationEnd = nil
                calibrationDone?(success)
                calibrationDone = nil
            }
        }
        let reading = estimator.ingest(frame)
        let ratio = estimator.contactRatio
        Task { @MainActor in self.onReading?(reading, ratio) }
    }

    static func frame(from face: VNFaceObservation, imageSize: CGSize, time: TimeInterval) -> GazeFrame? {
        guard let landmarks = face.landmarks,
              let left = eye(landmarks.leftEye, pupil: landmarks.leftPupil, imageSize: imageSize),
              let right = eye(landmarks.rightEye, pupil: landmarks.rightPupil, imageSize: imageSize) else { return nil }
        return GazeFrame(leftEye: left, rightEye: right, yaw: face.yaw?.doubleValue, pitch: face.pitch?.doubleValue, time: time)
    }

    private static func eye(_ region: VNFaceLandmarkRegion2D?, pupil: VNFaceLandmarkRegion2D?, imageSize: CGSize) -> EyeGeometry? {
        guard let region, let pupil, let centre = pupil.pointsInImage(imageSize: imageSize).first else { return nil }
        let contour = region.pointsInImage(imageSize: imageSize).map { GazePoint(Double($0.x), Double($0.y)) }
        guard contour.count >= 4 else { return nil }
        return EyeGeometry(contour: contour, pupil: GazePoint(Double(centre.x), Double(centre.y)))
    }
}
