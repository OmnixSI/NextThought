import Foundation
import CoreGraphics

/// The small subset of input AppKit can use to move a scroll view without a
/// scroll-wheel or drag event. Kept pure so the event policy can be tested.
public enum ScrollFollowInputClassifier {
    /// ANSI key codes for arrows, Home/End, Page Up/Down, and Space.
    public static func isScrollNavigationKeyCode(_ keyCode: UInt16) -> Bool {
        switch keyCode {
        case 123...126, 115, 116, 119, 121, 49:
            true
        default:
            false
        }
    }

    public static func isPoint(_ point: CGPoint, inside frame: CGRect?) -> Bool {
        guard let frame, !frame.isNull, !frame.isEmpty else { return false }
        return frame.contains(point)
    }
}
