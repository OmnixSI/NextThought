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
    /// <summary>
    /// Searchable fields for a projection: every indexed text becomes one field, keeping its
    /// location, line id, revisions and freshness (port of the projection-backed fields builder).
    /// </summary>
    public static IReadOnlyList<SessionSearchField> Fields(SessionTextProjection projection)
    {
        var fields = new List<SessionSearchField>();
        foreach (var text in projection.SearchableText)
        {
            var kind = text.Kind switch
            {
                SessionProjectionTextKind.Transcript => SessionSearchFieldKind.Transcript,
                SessionProjectionTextKind.CorrectedTranscript => SessionSearchFieldKind.CorrectedTranscript,
                SessionProjectionTextKind.Translation => SessionSearchFieldKind.Translation,
                SessionProjectionTextKind.Question => SessionSearchFieldKind.Question,
                SessionProjectionTextKind.Answer => SessionSearchFieldKind.Answer,
                _ => SessionSearchFieldKind.Notes
            };
            fields.Add(new SessionSearchField(kind, text.Text, text.Location, text.AlternateText, text.Id,
                text.LineId, text.SourceRevision, text.IsStale, text.SourceStatus));
        }
        return fields;
    }


    /// <summary>
    /// Adds the structured-notes nodes as searchable note fields, keeping their evidence ids and
    /// locations so a hit can open the exact node (port of the structured fields builder).
    /// </summary>
    public static IReadOnlyList<SessionSearchField> Fields(SessionTextProjection projection,
        StructuredNotesPayload? notes, IReadOnlyList<TranscriptLine> lines, string transcriptSourceRevision)
    {
        var fields = Fields(projection).ToList();
        if (notes is null) return fields;
        foreach (var reference in SessionNotesEvidence.References(notes, lines, transcriptSourceRevision))
        {
            fields.Add(new SessionSearchField(SessionSearchFieldKind.Notes, reference.Claim, reference.Location,
                Id: reference.Id, SourceRevision: reference.StructuredTarget?.PayloadRevision,
                IsStale: reference.ReviewIsStale, SourceStatus: reference.SourceStatus,
                AlternateText: reference.Title));
        }
        return fields;
    }

    /// <summary>B22: the archived-meta form of the fields builder used by the history UI.</summary>
    public static IReadOnlyList<SessionSearchField> Fields(SessionMeta meta,
        IReadOnlyList<TranscriptLine> lines, IReadOnlyList<SessionQuestionEvent> questions,
        IReadOnlyList<SessionAnswerEvent> answers, SessionSummary? summary, StructuredNotesDoc? notes)
    {
        var projectedAnswers = answers.Where(answer => answer.IsComplete).Select(answer =>
            new SessionProjectedAnswer(Guid.TryParse(answer.Id, out var id) ? id : Guid.NewGuid(), answer.Date,
                answer.Question, answer.Answer, TranscriptRevisions.Of(Guid.NewGuid(), answer.Answer))).ToList();
        var projectedSummary = summary is null ? null : new SessionProjectedSummary(
            summary.Title, summary.Markdown, summary.TranscriptSourceRevision,
            SessionDerivedContentStatus.Unknown, summary.StructuredNotesSourceRevision,
            summary.Structured, summary.StructuredAll, summary.Questions, summary.Lessons, summary.Bank);
        var projection = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, lines, projectedAnswers,
            projectedSummary, questions, textNotes: notes, meta: meta);
        var fields = Fields(projection)
            .Where(field => field.Id is null || !field.Id.Contains(":structured:", StringComparison.Ordinal))
            .ToList();
        var transcript = projection.Transcript;
        Guid? Nearest(string? location)
        {
            if (location is null || NotesTime.Seconds(location) is not { } seconds) return null;
            var nearest = transcript.Select(line => (line, distance: Math.Abs(line.Offset - seconds)))
                .OrderBy(pair => pair.distance).FirstOrDefault();
            return nearest.line is not null && nearest.distance <= 45 ? nearest.line.Id : null;
        }
        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            fields[index] = field with { LineId = field.LineId ?? Nearest(field.Location) };
        }
        if (summary is not null && notes is not null
            && summary.StructuredNotesSourceRevision is { } revision
            && revision != SessionProjectionBuilder.StructuredNotesRevision(notes))
        {
            for (var index = 0; index < fields.Count; index++)
                if (fields[index].Id is { } id && id.StartsWith("summary:", StringComparison.Ordinal))
                    fields[index] = fields[index] with
                    {
                        IsStale = true,
                        SourceStatus = SessionDerivedContentStatus.Stale
                    };
        }
        foreach (var payload in (summary?.StructuredAll ?? []).Concat(summary?.Structured is { } single
                     && !(summary.StructuredAll ?? []).Any(existing => existing.Format == single.Format) ? [single] : []))
        {
            if (!payload.HasContent) continue;
            var status = payload.TranscriptSourceRevision is { } payloadRevision && payloadRevision == projection.TranscriptSourceRevision
                ? SessionDerivedContentStatus.Current : SessionDerivedContentStatus.Unknown;
            foreach (var (text, location) in StructuredFieldTexts(payload))
            {
                if (string.IsNullOrWhiteSpace(text)) continue;
                fields.Add(new SessionSearchField(SessionSearchFieldKind.Notes, text, location,
                    Id: "summary:structured:" + payload.Format.Raw() + ":" + fields.Count,
                    LineId: Nearest(location), SourceRevision: payload.TranscriptSourceRevision,
                    IsStale: status == SessionDerivedContentStatus.Stale, SourceStatus: status));
            }
        }
        return fields;
    }

    private static IEnumerable<(string Text, string? Location)> StructuredFieldTexts(StructuredNotesPayload payload) =>
        payload.Format switch
        {
            NotesFormat.Fishbone when payload.Fishbone is { } fishbone =>
                new[] { (fishbone.Head, fishbone.HeadAt is { } headAt ? NotesTime.Text(headAt) : null) }
                    .Concat(fishbone.Categories.SelectMany(category => new[] { (category.Name, (string?)null) }
                        .Concat(category.Causes.Select(cause => (cause.Text, cause.At is { } at ? NotesTime.Text(at) : null))))),
            NotesFormat.Outline when payload.Outline is { } outline =>
                outline.Sections.SelectMany(section => new[] { (section.Title, section.At is { } at ? NotesTime.Text(at) : null) }
                    .Concat(section.Points.SelectMany(point => new[] { (point.Text, point.At is { } pointAt ? NotesTime.Text(pointAt) : null) }
                        .Concat(point.Children.Select(child => (child.Text, child.At is { } childAt ? NotesTime.Text(childAt) : null)))))),
            _ => []
        };

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

/// <summary>B22: a revision key for the archived-search cache; live sessions do not churn it.</summary>
public sealed class SessionSearchArchiveStamp : IEquatable<SessionSearchArchiveStamp>
{
    private readonly string canonical;

    public SessionSearchArchiveStamp(IReadOnlyList<SessionMeta> sessions, string? activeSessionId,
        IReadOnlyList<string>? pendingTranslationSessionIds = null, long searchableContentRevision = 0)
    {
        var pending = (pendingTranslationSessionIds ?? []).ToHashSet(StringComparer.Ordinal);
        var parts = new List<string> { "v1" };
        foreach (var session in sessions.OrderBy(session => session.Id))
        {
            var live = session.Id.ToString("D") == activeSessionId || pending.Contains(session.Id.ToString("D"));
            parts.Add(string.Join("\u0000", new[]
            {
                session.Id.ToString("D"), session.StartedAt.ToUnixTimeSeconds().ToString(),
                live ? "" : session.EndedAt?.ToUnixTimeSeconds().ToString() ?? "",
                session.Mode, live ? "" : session.Title,
                live ? "" : session.TranscriptCount.ToString(),
                live ? "" : session.QuestionCount.ToString(),
                live ? "" : session.AnswerCount.ToString(),
                live ? "" : session.SummaryState
            }));
        }
        parts.Add("live:" + string.Join(",", pending.Select(id => id).Append(activeSessionId ?? "").Where(id => id.Length > 0).OrderBy(id => id, StringComparer.Ordinal)));
        parts.Add("revision:" + searchableContentRevision);
        canonical = string.Join("\n", parts);
    }

    public bool Equals(SessionSearchArchiveStamp? other) => other is not null && canonical == other.canonical;
    public override bool Equals(object? obj) => obj is SessionSearchArchiveStamp other && Equals(other);
    public override int GetHashCode() => canonical.GetHashCode(StringComparison.Ordinal);
}
