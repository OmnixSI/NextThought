namespace NextThought.Core;

public enum SessionEvidenceDecision
{
    Supported,
    Unsupported
}

/// <summary>A person's explicit review, scoped to the exact claim and transcript revision they saw.</summary>
public sealed record SessionEvidenceReview(
    SessionEvidenceDecision Decision,
    string ClaimRevision,
    string TranscriptSourceRevision,
    string LineSourceRevision,
    DateTimeOffset ReviewedAt);

public enum SessionEvidenceSupport
{
    /// <summary>The supplied quotation appears verbatim in a saved transcript version.</summary>
    Quoted,

    /// <summary>A timestamp points to nearby speech, but no matching quotation was supplied.</summary>
    TimeOnly,

    /// <summary>A quotation was supplied, but it could not be matched to the transcript.</summary>
    QuoteNotFound,

    /// <summary>There is no usable quotation or timestamp to locate in the transcript.</summary>
    Unlocated
}

public static class SessionEvidenceSupports
{
    public static string Label(this SessionEvidenceSupport support) => support switch
    {
        SessionEvidenceSupport.Quoted => "引文在所选转写版本中找到",
        SessionEvidenceSupport.TimeOnly => "仅关联时间 · 内容待核对",
        SessionEvidenceSupport.QuoteNotFound => "未找到所引原话 · 内容待核对",
        _ => "没有原话定位 · 内容待核对"
    };
}

public enum SessionEvidenceOrigin
{
    TextNotes,
    StructuredNotes
}

/// <summary>Which saved transcript text actually matched a note's quotation.</summary>
public enum SessionEvidenceSourceVariant
{
    Original,
    Corrected
}

/// <summary>Stable locator for one editable text node in a generated structured-notes payload.</summary>
public sealed record SessionStructuredNotesTarget(NotesFormat Format, string Path, string PayloadRevision);

/// <summary>A saved note paired with the strongest transcript link available (port of SessionEvidenceReference).</summary>
public sealed record SessionEvidenceReference(
    string Id,
    string Title,
    string Claim,
    SessionEvidenceSupport Support,
    SessionEvidenceOrigin Origin,
    Guid? SessionId = null,
    string? Quote = null,
    string? Location = null,
    Guid? LineId = null,
    string? SourceRevision = null,
    SessionEvidenceSourceVariant? SourceVariant = null,
    string? SourceText = null,
    bool NeedsConfirmation = false,
    Guid? NotesItemId = null,
    SessionStructuredNotesTarget? StructuredTarget = null,
    NotesSectionKind? Kind = null,
    string? Owner = null,
    string? Due = null,
    DateTimeOffset? DueDate = null,
    string? ClaimRevision = null,
    string TranscriptSourceRevision = "",
    SessionDerivedContentStatus SourceStatus = SessionDerivedContentStatus.Unknown,
    SessionEvidenceDecision? ReviewDecision = null,
    bool ReviewIsStale = false);
