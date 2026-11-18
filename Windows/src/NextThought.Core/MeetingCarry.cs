namespace NextThought.Core;

/// <summary>
/// What a follow-up meeting may use from its series and previous meeting (port of MeetingCarry).
/// The Windows series carries lessons and the question bank but not per-series documents, so the
/// materials flag has no effect here yet.
/// </summary>
public sealed record MeetingCarry
{
    /// <summary>Materials that were already in the series when preparation began.</summary>
    public bool Materials { get; init; } = true;

    /// <summary>The reviewed question bank.</summary>
    public bool Review { get; init; } = true;

    /// <summary>Lessons learned from previous meetings.</summary>
    public bool Lessons { get; init; } = true;

    /// <summary>The previous meeting's full transcript.</summary>
    public bool Transcript { get; init; }

    public static readonly MeetingCarry Standard = new();

    /// <summary>Applies the immediately available series selection without building an index.</summary>
    public WorkspaceSeries WorkspaceSnapshot(WorkspaceSeries series, IReadOnlySet<string> baselineDocumentIds)
    {
        var workspace = series;
        if (!series.UseKnowledge || !Review) workspace = workspace with { QuestionBank = [] };
        if (!series.UseKnowledge || !Lessons) workspace = workspace with { Lessons = [] };
        return workspace;
    }
}
