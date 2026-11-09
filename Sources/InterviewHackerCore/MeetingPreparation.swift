import Foundation

public struct MeetingCarry: Equatable, Sendable {
    public var materials = true
    public var review = true
    public var lessons = true
    public var transcript = false
    public init(materials: Bool = true, review: Bool = true, lessons: Bool = true, transcript: Bool = false) {
        self.materials = materials; self.review = review; self.lessons = lessons; self.transcript = transcript
    }
    public static let standard = MeetingCarry()
}

/// A single meeting's choices. Applying them makes a value copy and never changes
/// the reusable series or its defaults.
public struct MeetingPlan: Equatable, Sendable {
    public var title: String?
    public var mode: SessionMode
    public var customTypeName: String?
    public var customBaseMode: SessionMode
    public var carry: MeetingCarry
    public var baselineDocumentIDs: Set<UUID>
    public var previousSessionID: UUID?
    public init(title: String? = nil, mode: SessionMode = .general, customTypeName: String? = nil,
                customBaseMode: SessionMode = .general, carry: MeetingCarry = .standard,
                baselineDocumentIDs: Set<UUID> = [], previousSessionID: UUID? = nil) {
        self.title = title; self.mode = mode; self.customTypeName = customTypeName
        self.customBaseMode = customBaseMode; self.carry = carry
        self.baselineDocumentIDs = baselineDocumentIDs; self.previousSessionID = previousSessionID
    }
    public var effectiveMode: SessionMode { mode == .custom ? customBaseMode : mode }

    public func preparedWorkspace(from series: Workspace, previousTranscript: ContextDocument? = nil) -> Workspace {
        var result = series
        if result.effectiveMode != effectiveMode {
            if ModeProfile.for(result.effectiveMode).matchesDefaultTemplatePreferences(result.preferences) {
                result.preferences = ModeProfile.for(effectiveMode).templatePreferences
            }
            result.style = MeetingTypeDefaults.for(effectiveMode).style
        }
        result.mode = mode
        result.customTypeName = customTypeName
        result.customBaseMode = mode == .custom ? customBaseMode : nil
        if !carry.materials { result.documents.removeAll { baselineDocumentIDs.contains($0.id) } }
        if !carry.review { result.questionBank = [] }
        if !carry.lessons { result.lessons = [] }
        if carry.transcript, let previousTranscript { result.documents.append(previousTranscript) }
        return result
    }
}
