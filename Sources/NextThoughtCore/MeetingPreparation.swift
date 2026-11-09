import Foundation

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
        var result = carry.workspaceSnapshot(from: series, baselineDocumentIDs: baselineDocumentIDs)
        if result.effectiveMode != effectiveMode {
            if ModeProfile.for(result.effectiveMode).matchesDefaultTemplatePreferences(result.preferences) {
                result.preferences = ModeProfile.for(effectiveMode).templatePreferences
            }
            result.style = MeetingTypeDefaults.for(effectiveMode).style
        }
        result.mode = mode
        result.customTypeName = customTypeName
        result.customBaseMode = mode == .custom ? customBaseMode : nil
        if carry.transcript, let previousTranscript { result.documents.append(previousTranscript) }
        return result
    }
}
