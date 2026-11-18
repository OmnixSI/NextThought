namespace NextThought.Core;

public enum SessionSearchFieldKind
{
    Transcript,
    CorrectedTranscript,
    Translation,
    Question,
    Answer,
    Notes
}

public enum SessionDerivedContentStatus
{
    Unknown,
    Current,
    Stale
}

/// <summary>One searchable piece of a meeting (port of SessionSearchField).</summary>
public sealed record SessionSearchField(
    SessionSearchFieldKind Kind,
    string Text,
    string? Location = null,
    string? AlternateText = null,
    string? Id = null,
    Guid? LineId = null,
    string? SourceRevision = null,
    bool? IsStale = null,
    SessionDerivedContentStatus? SourceStatus = null,
    string? AlternateId = null,
    SessionSearchFieldKind? AlternateKind = null,
    Guid? AlternateLineId = null,
    string? AlternateSourceRevision = null);

public sealed record SessionSearchHit(
    SessionSearchFieldKind Kind,
    string Excerpt,
    string? Location,
    string MatchedForm,
    bool IsMisheard,
    string? Id = null,
    Guid? LineId = null,
    string? SourceRevision = null,
    bool? IsStale = null,
    SessionDerivedContentStatus? SourceStatus = null);

public sealed record SessionSearchDocument(Guid SessionId, IReadOnlyList<SessionSearchField> Fields);

public sealed record SessionSearchResult(Guid SessionId, SessionSearchHit Hit);

/// <summary>
/// Stateless lexical matching for archived meeting text (port of SessionContentSearch).
/// It deliberately imposes no corpus-size limit; callers load documents in batches.
/// </summary>
public static class SessionContentSearch
{
    public static SessionSearchHit? FirstHit(IReadOnlyList<SessionSearchField> fields, GlossarySearch terms) =>
        Hits(fields, terms).FirstOrDefault();

    /// <summary>One hit per matching source field, in document order, deduplicated by id.</summary>
    public static IReadOnlyList<SessionSearchHit> Hits(IReadOnlyList<SessionSearchField> fields, GlossarySearch terms)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var hits = new List<SessionSearchHit>();
        foreach (var field in fields)
        {
            string matched;
            GlossarySearchHit match;
            bool alternate;
            if (terms.FirstMatch(field.Text) is { } direct)
            {
                matched = field.Text;
                match = direct;
                alternate = false;
            }
            else if (field.AlternateText is { } alternateText && terms.FirstMatch(alternateText) is { } alternateMatch)
            {
                matched = alternateText;
                match = alternateMatch;
                alternate = true;
            }
            else
            {
                continue;
            }
            var hit = new SessionSearchHit(
                alternate ? field.AlternateKind ?? field.Kind : field.Kind,
                Excerpt(matched, match.Form),
                field.Location,
                match.Form,
                match.IsMisheard,
                alternate ? field.AlternateId ?? field.Id : field.Id,
                alternate ? field.AlternateLineId ?? field.LineId : field.LineId,
                alternate ? field.AlternateSourceRevision ?? field.SourceRevision : field.SourceRevision,
                alternate ? false : field.IsStale,
                alternate ? null : field.SourceStatus);
            if (hit.Id is null || seen.Add(hit.Id)) hits.Add(hit);
        }
        return hits;
    }

    /// <summary>One first hit per meeting, preserving the archive order.</summary>
    public static IReadOnlyList<SessionSearchResult> Search(IReadOnlyList<SessionSearchDocument> documents, GlossarySearch terms)
    {
        var results = new List<SessionSearchResult>();
        foreach (var document in documents)
            if (FirstHit(document.Fields, terms) is { } hit)
                results.Add(new SessionSearchResult(document.SessionId, hit));
        return results;
    }

    private static string Excerpt(string text, string form)
    {
        var index = IndexOfFold(text, form);
        if (index < 0) return text.Length > 116 ? text[..116] + "…" : text;
        var start = Math.Max(0, index - 32);
        var end = Math.Min(text.Length, index + form.Length + 64);
        return (start > 0 ? "…" : "") + text[start..end] + (end < text.Length ? "…" : "");
    }

    private static int IndexOfFold(string text, string form)
    {
        if (form.Length == 0 || text.Length < form.Length) return -1;
        var haystack = text.Select(character => Fold(character)).ToArray();
        var needle = form.Select(character => Fold(character)).ToArray();
        for (var index = 0; index <= haystack.Length - needle.Length; index++)
            if (haystack.AsSpan(index, needle.Length).SequenceEqual(needle)) return index;
        return -1;
    }

    private static char Fold(char character)
    {
        var value = character;
        if (value is >= '\uFF01' and <= '\uFF5E') value = (char)(value - 0xFEE0);
        else if (value == '\u3000') value = ' ';
        return char.ToLowerInvariant(value);
    }
}
