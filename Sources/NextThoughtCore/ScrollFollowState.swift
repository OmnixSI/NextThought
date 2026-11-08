/// Tracks whether a live pane should follow incoming content or preserve the
/// position the user is reading. Each pane owns its own value.
public struct ScrollFollowState: Equatable, Sendable {
    public enum Mode: Equatable, Sendable {
        case following
        case readingHistory
        case selectingText
    }

    public private(set) var mode: Mode = .following
    public private(set) var pendingItemCount = 0

    public init() {}

    public var isFollowing: Bool { mode == .following }
    public var hasNewContent: Bool { pendingItemCount > 0 }

    /// A user scroll away from the latest content pauses automatic following.
    public mutating func userScrolledAway() {
        guard isFollowing else { return }
        mode = .readingHistory
    }

    /// Selecting text also pins the viewport so streaming updates cannot move
    /// the text under the selection.
    public mutating func userSelectedText() {
        guard isFollowing else { return }
        mode = .selectingText
    }

    /// Counts completed transcript items or coarse answer updates while paused.
    public mutating func receivedNewContent(count: Int = 1) {
        guard !isFollowing, count > 0 else { return }
        pendingItemCount += count
    }

    /// Explicitly restore the live edge and clear the unread count.
    public mutating func returnToLatest() {
        mode = .following
        pendingItemCount = 0
    }
}
