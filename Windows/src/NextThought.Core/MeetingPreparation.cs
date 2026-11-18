namespace NextThought.Core;

/// <summary>
/// A single meeting's choices (port of MeetingPlan): applying them makes a value copy and never
/// changes the reusable series. The Windows series does not carry its own document list or style,
/// so only the carry selection and the effective mode are applied here yet.
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
    public WorkspaceSeries PreparedWorkspace(WorkspaceSeries series) =>
        Carry.WorkspaceSnapshot(series, BaselineDocumentIds);
}
