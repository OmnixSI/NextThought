import Foundation

/// What a follow-up meeting may use from its series and previous meeting.
///
/// The workspace projection is synchronous so recording can begin immediately.
/// If `transcript` is selected, the app loads and attaches that archived text
/// separately because reading the archive is asynchronous.
public struct MeetingCarry: Equatable, Sendable {
    /// Materials that were already in the series when preparation began.
    public var materials: Bool
    /// The reviewed question bank.
    public var review: Bool
    /// Lessons learned from previous meetings.
    public var lessons: Bool
    /// The previous meeting's full transcript (loaded asynchronously by the app).
    public var transcript: Bool

    public init(materials: Bool = true, review: Bool = true, lessons: Bool = true, transcript: Bool = false) {
        self.materials = materials
        self.review = review
        self.lessons = lessons
        self.transcript = transcript
    }

    public static let standard = MeetingCarry()

    /// Applies the immediately available series selection without building an index.
    /// Newly added documents are retained when baseline materials are excluded.
    public func workspaceSnapshot(from series: Workspace, baselineDocumentIDs: Set<UUID>) -> Workspace {
        var workspace = series
        if !materials {
            workspace.documents.removeAll { baselineDocumentIDs.contains($0.id) }
        }
        if !series.useKnowledge || !review { workspace.questionBank = [] }
        if !series.useKnowledge || !lessons { workspace.lessons = [] }
        return workspace
    }
}
