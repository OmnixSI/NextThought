using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>E21: one pending series change proposal.</summary>
public sealed record SeriesChangeRow(WorkspaceSeriesChangeCandidate Candidate)
{
    public string Id => Candidate.Id;
    public string Text => Candidate.Text;
    public string Kind => Candidate.Kind.ToString();
    public string Owner => Candidate.Owner ?? "";
    public string Due => Candidate.Due ?? "";
    public string SourceQuote => Candidate.SourceQuote ?? "";
}
