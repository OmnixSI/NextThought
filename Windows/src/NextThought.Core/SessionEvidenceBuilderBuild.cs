using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NextThought.Core;

// B6: the full SessionEvidenceBuilder.build port (SessionEvidence.swift:146-345).
public static partial class SessionEvidenceBuilder
{
    public static IReadOnlyList<SessionEvidenceReference> Build(SessionMeta meta,
        IReadOnlyList<TranscriptLine> lines, StructuredNotesDoc? notes, SessionSummary? summary,
        IReadOnlyList<TranscriptCorrection>? corrections = null)
    {
        var projection = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, lines, [], corrections: corrections ?? notes?.Corrections);
        var transcript = projection.Transcript;
        var currentRevision = projection.TranscriptSourceRevision;
        var references = new List<SessionEvidenceReference>();

        SessionEvidenceReference Make(string id, string title, string claim, string? quote = null, int? seconds = null,
            bool needsConfirmation = false, SessionEvidenceOrigin origin = SessionEvidenceOrigin.TextNotes,
            Guid? notesItemId = null, SessionStructuredNotesTarget? structuredTarget = null,
            NotesSectionKind? kind = null, string? owner = null, string? due = null, DateTimeOffset? dueDate = null,
            SessionDerivedContentStatus sourceStatus = SessionDerivedContentStatus.Unknown)
        {
            var cleanQuote = quote?.Trim();
            SessionProjectedTranscriptLine? matched = null;
            SessionEvidenceSourceVariant? variant = null;
            string? matchedText = null;
            string? matchedRevision = null;
            SessionEvidenceSupport support;
            string? location = null;
            if (!string.IsNullOrEmpty(cleanQuote))
            {
                var matches = new List<(SessionProjectedTranscriptLine Line, SessionEvidenceSourceVariant Variant,
                    string Text, string Revision)>();
                foreach (var line in transcript)
                {
                    if (line.CorrectedText.Contains(cleanQuote, StringComparison.OrdinalIgnoreCase))
                        matches.Add((line, SessionEvidenceSourceVariant.Corrected, line.CorrectedText, line.SourceRevision));
                    else if (line.OriginalText.Contains(cleanQuote, StringComparison.OrdinalIgnoreCase))
                        matches.Add((line, SessionEvidenceSourceVariant.Original, line.OriginalText, line.OriginalRevision));
                }
                var closest = Closest(matches, seconds);
                if (closest is { } found)
                {
                    matched = found.Line; variant = found.Variant; matchedText = found.Text;
                    matchedRevision = found.Revision; location = NotesTime.Text(found.Line.Offset);
                    support = SessionEvidenceSupport.Quoted;
                }
                else support = SessionEvidenceSupport.QuoteNotFound;
            }
            else support = SessionEvidenceSupport.Unlocated;

            if (matched is null && seconds is { } target)
            {
                var nearest = transcript.OrderBy(line => Math.Abs(line.Offset - target)).FirstOrDefault();
                if (nearest is not null && Math.Abs(nearest.Offset - target) <= 45)
                {
                    matched = nearest; variant = SessionEvidenceSourceVariant.Corrected;
                    matchedText = nearest.CorrectedText; matchedRevision = nearest.SourceRevision;
                    if (support != SessionEvidenceSupport.QuoteNotFound) support = SessionEvidenceSupport.TimeOnly;
                }
            }
            location ??= seconds is { } rawSeconds ? NotesTime.Text(rawSeconds) : null;

            var claimRevision = ClaimRevision(id, title, claim, cleanQuote, location, kind, owner, due, dueDate);
            SessionEvidenceReview? saved = null;
            summary?.EvidenceReviews?.TryGetValue(id, out saved);
            var reviewIsCurrent = saved is not null
                && saved.ClaimRevision == claimRevision
                && saved.TranscriptSourceRevision == currentRevision
                && saved.LineSourceRevision == (matchedRevision ?? "");
            var reviewIsStale = saved is not null && !reviewIsCurrent;
            return new SessionEvidenceReference(id, title, claim, support, origin,
                SessionId: meta.Id, Quote: cleanQuote, Location: location, LineId: matched?.Id,
                SourceRevision: matchedRevision, SourceVariant: variant, SourceText: matchedText,
                NeedsConfirmation: needsConfirmation || support != SessionEvidenceSupport.Quoted,
                NotesItemId: notesItemId, StructuredTarget: structuredTarget, Kind: kind,
                Owner: owner, Due: due, DueDate: dueDate, ClaimRevision: claimRevision,
                TranscriptSourceRevision: currentRevision, SourceStatus: sourceStatus,
                ReviewDecision: reviewIsCurrent ? saved?.Decision : null,
                ReviewIsStale: reviewIsStale);
        }

        var notesStatus = Status(notes?.TranscriptSourceRevision, currentRevision);
        foreach (var section in notes?.Sections ?? [])
        foreach (var item in section.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Text)) continue;
            if (!Guid.TryParse(item.Id, out var itemId)) itemId = Guid.Empty;
            references.Add(Make("notes:" + section.Id + ":" + item.Id, section.Title, item.Text,
                quote: item.Quote, seconds: NotesTime.Seconds(item.Time), needsConfirmation: item.Uncertain,
                notesItemId: itemId == Guid.Empty ? null : itemId, kind: section.Kind,
                owner: item.Owner, due: item.Due,
                dueDate: item.DueDate is { } date ? new DateTimeOffset(date) : null,
                sourceStatus: notesStatus));
        }

        var payloads = (summary?.StructuredAll ?? []).ToList();
        if (summary?.Structured is { } latest && !payloads.Any(payload => payload.Format == latest.Format)) payloads.Add(latest);
        foreach (var payload in payloads.Where(payload => payload.HasContent))
        {
            var payloadStatus = Status(payload.TranscriptSourceRevision, currentRevision);
            var payloadRevision = SessionNotesEditor.PayloadRevision(payload);
            void Add(string path, string title, string? text, int? at)
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                references.Add(Make("structured:" + payload.Format.Raw() + ":" + path, title, text,
                    seconds: at, origin: SessionEvidenceOrigin.StructuredNotes,
                    structuredTarget: new SessionStructuredNotesTarget(payload.Format, path, payloadRevision),
                    sourceStatus: payloadStatus));
            }
            switch (payload.Format)
            {
                case NotesFormat.Fishbone when payload.Fishbone is { } fishbone:
                    Add("head", "议题", fishbone.Head, fishbone.HeadAt);
                    for (var category = 0; category < fishbone.Categories.Count; category++)
                    {
                        var group = fishbone.Categories[category];
                        for (var cause = 0; cause < group.Causes.Count; cause++)
                            Add($"category:{category}:cause:{cause}", group.Name, group.Causes[cause].Text, group.Causes[cause].At);
                    }
                    break;
                case NotesFormat.Outline when payload.Outline is { } outline:
                    for (var section = 0; section < outline.Sections.Count; section++)
                    {
                        var group = outline.Sections[section];
                        Add($"section:{section}", "章节", group.Title, group.At);
                        for (var point = 0; point < group.Points.Count; point++)
                        {
                            var item = group.Points[point];
                            var path = $"section:{section}:point:{point}";
                            Add(path, group.Title, item.Text, item.At);
                            for (var child = 0; child < item.Children.Count; child++)
                                Add(path + $":child:{child}", group.Title, item.Children[child].Text, item.Children[child].At);
                        }
                    }
                    for (var concept = 0; concept < outline.Concepts.Count; concept++)
                    {
                        var item = outline.Concepts[concept];
                        Add($"concept:{concept}:term", "关键概念", item.Term, item.At);
                        Add($"concept:{concept}:meaning", "关键概念", item.Meaning, item.At);
                    }
                    for (var review = 0; review < outline.Review.Count; review++)
                        Add($"review:{review}", "待复习", outline.Review[review].Text, outline.Review[review].At);
                    break;
                case NotesFormat.Viewpoints when payload.Viewpoints is { } matrix:
                    Add("topic", "议题", matrix.Topic, matrix.TopicAt);
                    for (var issue = 0; issue < matrix.Issues.Count; issue++)
                    {
                        var item = matrix.Issues[issue];
                        for (var position = 0; position < item.Positions.Count; position++)
                        {
                            var side = item.Positions[position];
                            Add($"issue:{issue}:position:{position}", item.Title, side.Text, side.At);
                        }
                    }
                    for (var consensus = 0; consensus < matrix.Consensus.Count; consensus++)
                        Add($"consensus:{consensus}", "共识", matrix.Consensus[consensus].Text, matrix.Consensus[consensus].At);
                    for (var disagreement = 0; disagreement < matrix.Disagreements.Count; disagreement++)
                        Add($"disagreement:{disagreement}", "分歧", matrix.Disagreements[disagreement].Text, matrix.Disagreements[disagreement].At);
                    for (var owner = 0; owner < matrix.Owners.Count; owner++)
                        Add($"owner:{owner}", "负责人", matrix.Owners[owner].Task, matrix.Owners[owner].At);
                    break;
            }
        }
        return references;
    }

    private static (SessionProjectedTranscriptLine Line, SessionEvidenceSourceVariant Variant,
        string Text, string Revision)? Closest(
        IReadOnlyList<(SessionProjectedTranscriptLine Line, SessionEvidenceSourceVariant Variant,
            string Text, string Revision)> matches, int? seconds)
    {
        if (matches.Count == 0) return null;
        if (seconds is null) return matches[0];
        return matches.OrderBy(match => Math.Abs(match.Line.Offset - seconds.Value))
            .ThenBy(match => match.Variant == SessionEvidenceSourceVariant.Corrected ? 0 : 1)
            .ThenBy(match => match.Line.Offset).First();
    }

    private static SessionDerivedContentStatus Status(string? saved, string current)
    {
        if (saved is null) return SessionDerivedContentStatus.Unknown;
        return saved == current ? SessionDerivedContentStatus.Current : SessionDerivedContentStatus.Stale;
    }

    private static string ClaimRevision(string id, string title, string claim, string? quote, string? location,
        NotesSectionKind? kind, string? owner, string? due, DateTimeOffset? dueDate) =>
        SwiftDigest.Of(id, title, claim, quote ?? "", location ?? "",
            kind is { } value ? System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(value.ToString()) : "",
            owner ?? "", due ?? "", dueDate is { } date ? SwiftDigest.DateBitPattern(date) : "");
}
