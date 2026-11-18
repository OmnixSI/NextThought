namespace NextThought.Core;

/// <summary>
/// A single meeting's choices (port of MeetingPlan): applying them makes a value copy and never
/// changes the reusable series. Materials are filtered to the baseline documents, and the previous
/// meeting's transcript is attached when the carry selection asks for it.
/// </summary>
public sealed record MeetingPlan
{
    public string? Title { get; init; }
    public MeetingMode Mode { get; init; } = MeetingMode.General;
    public string? CustomTypeName { get; init; }
    public MeetingMode CustomBaseMode { get; init; } = MeetingMode.General;
    public MeetingCarry Carry { get; init; } = MeetingCarry.Standard;
    public IReadOnlySet<string> BaselineDocumentIds { get; init; } = new HashSet<string>();
    public string? PreviousSessionId { get; init; }

    public MeetingMode EffectiveMode => Mode == MeetingMode.Custom ? CustomBaseMode : Mode;

    /// <summary>The series as this meeting may use it; the original series is untouched.</summary>
    public WorkspaceSeries PreparedWorkspace(WorkspaceSeries series, ContextDocument? previousTranscript = null)
    {
        var prepared = Carry.WorkspaceSnapshot(series, BaselineDocumentIds);
        if (!Carry.Transcript || previousTranscript is null) return prepared;
        return prepared with
        {
            Documents = [.. prepared.Documents, previousTranscript],
            DocumentIds = [.. prepared.DocumentIds, previousTranscript.Id]
        };
    }
}
