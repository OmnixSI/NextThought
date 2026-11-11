import Foundation

// Eye contact, from face landmarks alone.
//
// The camera sits above the screen, so "looking at the lens" has a fixed signature
// for one person in one seat: where the pupils sit inside the eye openings, and how
// the head is turned. `GazeEstimator` reduces a frame of landmarks to that signature,
// learns the user's own on-camera signature from a short calibration, and reports how
// far the current frame is from it. No image leaves this code path: it sees points.

public struct GazePoint: Sendable, Equatable {
    public var x: Double
    public var y: Double
    public init(_ x: Double, _ y: Double) { self.x = x; self.y = y }
}

/// One eye in image coordinates (any consistent unit; only ratios are used).
public struct EyeGeometry: Sendable, Equatable {
    public var contour: [GazePoint]
    public var pupil: GazePoint
    public init(contour: [GazePoint], pupil: GazePoint) { self.contour = contour; self.pupil = pupil }

    /// Pupil position inside the opening: x along the eye (−1 inner … +1 outer edge
    /// of the image-left/right axis), y across it, both in half eye-widths; plus how
    /// open the eye is (height ÷ width). Nil for a degenerate contour.
    func reading() -> (x: Double, y: Double, openness: Double)? {
        guard contour.count >= 4,
              let left = contour.min(by: { $0.x < $1.x }), let right = contour.max(by: { $0.x < $1.x }) else { return nil }
        let axisX = right.x - left.x, axisY = right.y - left.y
        let width = (axisX * axisX + axisY * axisY).squareRoot()
        guard width > 1e-6 else { return nil }
        let ux = axisX / width, uy = axisY / width
        let centerX = (left.x + right.x) / 2, centerY = (left.y + right.y) / 2
        // Across the eye, measured on the axis perpendicular to the corners so a
        // tilted head does not read as looking up or down.
        var low = Double.infinity, high = -Double.infinity
        for point in contour {
            let across = -(point.x - centerX) * uy + (point.y - centerY) * ux
            low = min(low, across); high = max(high, across)
        }
        let dx = pupil.x - centerX, dy = pupil.y - centerY
        let along = dx * ux + dy * uy
        let across = -dx * uy + dy * ux - (low + high) / 2
        return (along / (width / 2), across / (width / 2), (high - low) / width)
    }
}

/// A frame's worth of landmarks. Angles are radians; nil when the detector has none.
public struct GazeFrame: Sendable, Equatable {
    public var leftEye: EyeGeometry
    public var rightEye: EyeGeometry
    public var yaw: Double?
    public var pitch: Double?
    public var time: TimeInterval

    public init(leftEye: EyeGeometry, rightEye: EyeGeometry, yaw: Double? = nil, pitch: Double? = nil, time: TimeInterval) {
        self.leftEye = leftEye; self.rightEye = rightEye; self.yaw = yaw; self.pitch = pitch; self.time = time
    }
}

/// The gaze signature of one frame: where the pupils sit in the eye openings (half
/// eye-widths) and how the head is turned (radians). The two are compared with the
/// calibrated pose separately, so no sign convention between them matters.
public struct GazeSignature: Sendable, Equatable {
    public var x: Double
    public var y: Double
    public var yaw: Double
    public var pitch: Double
    public init(x: Double, y: Double, yaw: Double = 0, pitch: Double = 0) {
        self.x = x; self.y = y; self.yaw = yaw; self.pitch = pitch
    }
}

public struct GazeReading: Sendable, Equatable {
    public enum State: String, Sendable { case onCamera, away, noFace }
    public var state: State
    /// Distance from the on-camera signature; 1 is the edge of "looking at the lens".
    public var deviation: Double
    /// Which way the eyes have wandered since calibration (x along the eye, y up).
    public var offset: GazeSignature
    public init(state: State, deviation: Double = 0, offset: GazeSignature = GazeSignature(x: 0, y: 0)) {
        self.state = state; self.deviation = deviation; self.offset = offset
    }
}

public struct GazeEstimator: Sendable {
    /// An eye less open than this is blinking or squinting; its pupil cannot be trusted.
    static let minimumOpenness = 0.14
    /// Head turn that still faces the lens (≈ 14°); a second display is well beyond it.
    static let headTolerance = 0.25
    /// Pupil travel, in half eye-widths, that still reads as eye contact on a webcam.
    public var tolerance: Double
    /// The on-camera signature; the neutral pose until `calibrate` has run.
    public private(set) var baseline = GazeSignature(x: 0, y: 0)
    public private(set) var isCalibrated = false

    private var smoothed: GazeSignature?
    private var looking = true
    private var calibration: [GazeSignature] = []

    // Session totals, for "眼神接触 82%".
    public private(set) var trackedTime: TimeInterval = 0
    public private(set) var contactTime: TimeInterval = 0
    private var lastTime: TimeInterval?

    public init(tolerance: Double = 0.12) { self.tolerance = tolerance }

    /// The signature of a frame, or nil while the eyes are closed.
    public static func signature(of frame: GazeFrame) -> GazeSignature? {
        guard let left = frame.leftEye.reading(), let right = frame.rightEye.reading() else { return nil }
        guard left.openness >= minimumOpenness || right.openness >= minimumOpenness else { return nil }
        // A half-closed eye is ignored; when both are open they are averaged.
        let eyes = [left, right].filter { $0.openness >= minimumOpenness }
        let x = eyes.reduce(0) { $0 + $1.x } / Double(eyes.count)
        let y = eyes.reduce(0) { $0 + $1.y } / Double(eyes.count)
        return GazeSignature(x: x, y: y, yaw: frame.yaw ?? 0, pitch: frame.pitch ?? 0)
    }

    /// Feeds frames recorded while the user looks into the lens; `finishCalibration`
    /// takes their median, which shrugs off a blink or a glance.
    public mutating func addCalibration(_ frame: GazeFrame) {
        if let signature = Self.signature(of: frame) { calibration.append(signature) }
    }

    @discardableResult
    public mutating func finishCalibration(minimumFrames: Int = 8) -> Bool {
        defer { calibration.removeAll() }
        guard calibration.count >= minimumFrames else { return false }
        func median(_ values: [Double]) -> Double {
            let sorted = values.sorted()
            return sorted.count % 2 == 1 ? sorted[sorted.count / 2] : (sorted[sorted.count / 2 - 1] + sorted[sorted.count / 2]) / 2
        }
        baseline = GazeSignature(x: median(calibration.map(\.x)), y: median(calibration.map(\.y)),
                                 yaw: median(calibration.map(\.yaw)), pitch: median(calibration.map(\.pitch)))
        isCalibrated = true
        smoothed = nil
        return true
    }

    public mutating func restore(baseline: GazeSignature) {
        self.baseline = baseline; isCalibrated = true; smoothed = nil
    }

    /// Reads one frame. `nil` means no face was found.
    public mutating func ingest(_ frame: GazeFrame?) -> GazeReading {
        guard let frame else {
            lastTime = nil
            return GazeReading(state: .noFace)
        }
        let elapsed = lastTime.map { min(1, max(0, frame.time - $0)) } ?? 0
        lastTime = frame.time
        // A blink keeps the previous verdict: nobody breaks eye contact by blinking.
        if let signature = Self.signature(of: frame) {
            if let previous = smoothed {
                func ease(_ from: Double, _ to: Double) -> Double { from + (to - from) * 0.35 }
                smoothed = GazeSignature(x: ease(previous.x, signature.x), y: ease(previous.y, signature.y),
                                         yaw: ease(previous.yaw, signature.yaw), pitch: ease(previous.pitch, signature.pitch))
            } else {
                smoothed = signature
            }
        }
        guard let current = smoothed else { return GazeReading(state: .noFace) }
        let offset = GazeSignature(x: current.x - baseline.x, y: current.y - baseline.y,
                                   yaw: current.yaw - baseline.yaw, pitch: current.pitch - baseline.pitch)
        // Eyelids follow the gaze up and down, so the vertical reading is the
        // noisier one; weigh it a little less.
        let eyes = ((offset.x * offset.x) + (offset.y * 0.8) * (offset.y * 0.8)).squareRoot() / tolerance
        let head = ((offset.yaw * offset.yaw) + (offset.pitch * offset.pitch)).squareRoot() / Self.headTolerance
        let deviation = max(eyes, head)
        // Hysteresis: leaving takes a clear move, coming back a clear return.
        if looking, deviation > 1.25 { looking = false } else if !looking, deviation < 0.85 { looking = true }
        trackedTime += elapsed
        if looking { contactTime += elapsed }
        return GazeReading(state: looking ? .onCamera : .away, deviation: deviation, offset: offset)
    }

    /// Share of the tracked time spent looking at the lens; nil before ten seconds.
    public var contactRatio: Double? {
        trackedTime >= 10 ? contactTime / trackedTime : nil
    }

    public mutating func resetSession() {
        trackedTime = 0; contactTime = 0; lastTime = nil; smoothed = nil; looking = true
    }
}
