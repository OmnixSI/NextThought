namespace NextThought.Core;

public enum SessionTranscriptVariant
{
    Original,
    Corrected
}

public enum SessionLanguageView
{
    Source,
    Translated,
    Bilingual
}

public sealed record SessionProjectionOptions
{
    public SessionTranscriptVariant TranscriptVariant { get; init; } = SessionTranscriptVariant.Corrected;
    public SessionLanguageView LanguageView { get; init; } = SessionLanguageView.Bilingual;
}

public enum SessionProjectionTextKind
{
    Transcript,
    CorrectedTranscript,
    Translation,
    Question,
    Answer,
    Summary,
    Notes
}

/// <summary>One stable unit of archived session text for search and navigation.</summary>
public sealed record SessionProjectedText(
    string Id,
    SessionProjectionTextKind Kind,
    string Text,
    Guid? SourceId = null,
    Guid? LineId = null,
    string? Location = null,
    string? SourceRevision = null,
    bool IsStale = false,
    SessionDerivedContentStatus? SourceStatus = null,
    string? AlternateText = null);

public sealed record SessionProjectedAnswer(
    Guid Id,
    DateTimeOffset Date,
    string Question,
    string Answer,
    string SourceRevision,
    AnswerCompletion? Completion = null)
{
    public bool IsComplete => Completion is null or AnswerCompletion.Complete;
}

public sealed record SessionProjectedSummary(
    string Title,
    string Markdown,
    string? SourceTranscriptRevision = null,
    SessionDerivedContentStatus SourceStatus = SessionDerivedContentStatus.Unknown,
    string? StructuredNotesSourceRevision = null,
    StructuredNotesPayload? Structured = null,
    IReadOnlyList<StructuredNotesPayload>? StructuredAll = null);

/// <summary>
/// A frozen view of the session's answers, summary and searchable text (port of
/// SessionTextProjection; transcript lines, notes and detected questions stay app-side yet).
/// </summary>
public sealed record SessionTextProjection(
    Guid SessionId,
    string Revision,
    string TranscriptSourceRevision,
    SessionProjectionOptions Options,
    IReadOnlyList<SessionProjectedAnswer> Answers,
    IReadOnlyList<SessionProjectedText> SearchableText,
    SessionProjectedSummary? Summary = null)
{
    /// <summary>
    /// Overall freshness of generated notes and summaries; original transcript lines alone are
    /// current because they are the source of record.
    /// </summary>
    /// <summary>Projected transcript lines; populated by the projection builder when available.</summary>
    public IReadOnlyList<SessionProjectedTranscriptLine> Transcript { get; init; } = [];

    /// <summary>The structured notes represented by this projection (null when no notes exist).</summary>
    public StructuredNotesPayload? Notes { get; init; }

    public SessionDerivedContentStatus DerivedContentStatus
    {
        get
        {
            var statuses = SearchableText.Where(text => text.SourceStatus is not null)
                .Select(text => text.SourceStatus!.Value)
                .ToList();
            if (Summary is { } summary) statuses.Add(summary.SourceStatus);
            if (statuses.Contains(SessionDerivedContentStatus.Stale)) return SessionDerivedContentStatus.Stale;
            if (statuses.Contains(SessionDerivedContentStatus.Unknown)) return SessionDerivedContentStatus.Unknown;
            return SessionDerivedContentStatus.Current;
        }
    }
}
