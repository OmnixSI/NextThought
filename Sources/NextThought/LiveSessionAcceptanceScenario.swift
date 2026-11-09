import SwiftUI

enum LiveSessionAcceptanceLayout {
    static let coordinateSpace = "live-session-acceptance-layout"
}

struct LiveSessionAcceptanceFramePreferenceKey: PreferenceKey {
    static var defaultValue: [String: CGRect] = [:]

    static func reduce(value: inout [String: CGRect], nextValue: () -> [String: CGRect]) {
        value.merge(nextValue(), uniquingKeysWith: { _, next in next })
    }
}

@MainActor
final class LiveSessionAcceptanceFrameCollector {
    private(set) var frames: [String: CGRect] = [:]

    func record(_ frames: [String: CGRect]) {
        self.frames = frames
    }
}

extension View {
    func liveSessionAcceptanceFrame(_ identifier: String) -> some View {
        background {
            GeometryReader { proxy in
                Color.clear.preference(
                    key: LiveSessionAcceptanceFramePreferenceKey.self,
                    value: [identifier: proxy.frame(in: .named(LiveSessionAcceptanceLayout.coordinateSpace))]
                )
            }
        }
    }
}

/// Deterministic UI states used only by the live-session acceptance renderer.
/// They are never enabled in the normal application flow.
enum LiveSessionAcceptanceScenario: String, CaseIterable, Identifiable {
    case normal
    case streaming
    case busy
    case busyFocus = "focus-busy"
    case success
    case saveWarning = "save-warning"
    case saveRecovery = "save-recovery"
    case unavailable
    case stale
    case staleManualEdit = "stale-manual-edit"
    case empty
    case readingHistory = "reading-history"
    case streamingHistory = "streaming-reading-history"
    case selection
    case selectionFocus = "selected-focus"
    case cancelLateCallback = "cancel-late-callback"
    case focus
    case overlay
    case largeText = "large-text"

    var id: String { rawValue }

    var accessibilitySize: DynamicTypeSize {
        self == .largeText ? .accessibility3 : .large
    }

    var description: String {
        switch self {
        case .normal: "Normal transcript and completed answer"
        case .streaming: "Partial transcript text while recognition is in progress"
        case .busy: "Answer request is active and can be stopped"
        case .busyFocus: "Keyboard focus remains on Stop while answer generation is busy"
        case .success: "Completed answer and saved archive status"
        case .saveWarning: "Archive write is blocked with retry available"
        case .saveRecovery: "Archive has recovered and shows its saved receipt"
        case .unavailable: "Answer service unavailable while meeting capture continues"
        case .stale: "Synthetic stale-derived-content notice"
        case .staleManualEdit: "Synthetic stale source notice beside a preserved manual transcript correction"
        case .empty: "No transcript or answer content yet"
        case .readingHistory: "User is reading older transcript lines while new lines arrive"
        case .streamingHistory: "Partial transcript streams while the reader stays pinned to history"
        case .selection: "Selection-pinned scroll state while new lines arrive"
        case .selectionFocus: "Selection-pinned history with keyboard focus on return-to-live"
        case .cancelLateCallback: "Cancelled answer preserves its partial result and rejects a late completion"
        case .focus: "Keyboard focus on the pause control"
        case .overlay: "Evidence details popover is open"
        case .largeText: "Accessibility3 environment override; macOS does not resize text from this value"
        }
    }
}

enum LiveSessionAcceptanceButtonInteraction: String {
    case normal
    case hover
    case pressed
    case focused
}

private struct LiveSessionAcceptanceButtonInteractionKey: EnvironmentKey {
    static let defaultValue = LiveSessionAcceptanceButtonInteraction.normal
}

extension EnvironmentValues {
    var liveSessionAcceptanceButtonInteraction: LiveSessionAcceptanceButtonInteraction {
        get { self[LiveSessionAcceptanceButtonInteractionKey.self] }
        set { self[LiveSessionAcceptanceButtonInteractionKey.self] = newValue }
    }
}

private struct LiveSessionAcceptanceScenarioKey: EnvironmentKey {
    static let defaultValue = LiveSessionAcceptanceScenario.normal
}

extension EnvironmentValues {
    var liveSessionAcceptanceScenario: LiveSessionAcceptanceScenario {
        get { self[LiveSessionAcceptanceScenarioKey.self] }
        set { self[LiveSessionAcceptanceScenarioKey.self] = newValue }
    }
}
