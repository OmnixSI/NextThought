namespace NextThought.Core;

/// <summary>
/// Turns a generated structured-notes payload into evidence references whose support is checked
/// against the saved transcript (port of the structured half of SessionEvidenceBuilder).
/// </summary>
public static class SessionNotesEvidence
{
    public static IReadOnlyList<SessionEvidenceReference> References(StructuredNotesPayload payload,
        IReadOnlyList<TranscriptLine> lines, string transcriptSourceRevision, Guid? sessionId = null)
    {
        var revision = SessionNotesEditor.PayloadRevision(payload);
        var references = new List<SessionEvidenceReference>();
        switch (payload.Format)
        {
            case NotesFormat.Fishbone when payload.Fishbone is { } fishbone:
                Add(references, "head", fishbone.Head, fishbone.HeadAt, null, "因果鱼骨图", lines,
                    transcriptSourceRevision, sessionId, revision, payload.Format);
                for (var category = 0; category < fishbone.Categories.Count; category++)
                {
                    var group = fishbone.Categories[category];
                    for (var cause = 0; cause < group.Causes.Count; cause++)
                        Add(references, $"category:{category}:cause:{cause}", group.Causes[cause].Text,
                            group.Causes[cause].At, null, group.Name, lines, transcriptSourceRevision,
                            sessionId, revision, payload.Format);
                }
                break;

            case NotesFormat.Outline when payload.Outline is { } outline:
                for (var section = 0; section < outline.Sections.Count; section++)
                {
                    var group = outline.Sections[section];
                    for (var point = 0; point < group.Points.Count; point++)
                    {
                        var item = group.Points[point];
                        Add(references, $"section:{section}:point:{point}", item.Text, item.At, null, group.Title,
                            lines, transcriptSourceRevision, sessionId, revision, payload.Format);
                        for (var child = 0; child < item.Children.Count; child++)
                            Add(references, $"section:{section}:point:{point}:child:{child}", item.Children[child].Text,
                                item.Children[child].At, null, group.Title, lines, transcriptSourceRevision,
                                sessionId, revision, payload.Format);
                    }
                }
                for (var concept = 0; concept < outline.Concepts.Count; concept++)
                {
                    var item = outline.Concepts[concept];
                    Add(references, $"concept:{concept}", item.Meaning, item.At, null, item.Term, lines,
                        transcriptSourceRevision, sessionId, revision, payload.Format);
                }
                for (var review = 0; review < outline.Review.Count; review++)
                    Add(references, $"review:{review}", outline.Review[review].Text, outline.Review[review].At,
                        null, "待复习", lines, transcriptSourceRevision, sessionId, revision, payload.Format);
                break;

            case NotesFormat.Viewpoints when payload.Viewpoints is { } matrix:
                for (var issue = 0; issue < matrix.Issues.Count; issue++)
                {
                    var item = matrix.Issues[issue];
                    for (var position = 0; position < item.Positions.Count; position++)
                    {
                        var side = item.Positions[position];
                        Add(references, $"issue:{issue}:position:{position}:text", side.Text, side.At, null,
                            item.Title + " · " + side.Party, lines, transcriptSourceRevision, sessionId,
                            revision, payload.Format);
                    }
                }
                for (var consensus = 0; consensus < matrix.Consensus.Count; consensus++)
                    Add(references, $"consensus:{consensus}", matrix.Consensus[consensus].Text,
                        matrix.Consensus[consensus].At, null, "共识", lines, transcriptSourceRevision, sessionId,
                        revision, payload.Format);
                for (var disagreement = 0; disagreement < matrix.Disagreements.Count; disagreement++)
                    Add(references, $"disagreement:{disagreement}", matrix.Disagreements[disagreement].Text,
                        matrix.Disagreements[disagreement].At, null, "分歧", lines, transcriptSourceRevision,
                        sessionId, revision, payload.Format);
                for (var owner = 0; owner < matrix.Owners.Count; owner++)
                {
                    var item = matrix.Owners[owner];
                    Add(references, $"owner:{owner}:task", item.Task, item.At, null, item.Who, lines,
                        transcriptSourceRevision, sessionId, revision, payload.Format);
                }
                break;
        }
        return references;
    }

    private static void Add(List<SessionEvidenceReference> references, string path, string claim, int? at,
        NotesSectionKind? kind, string title, IReadOnlyList<TranscriptLine> lines, string transcriptSourceRevision,
        Guid? sessionId, string payloadRevision, NotesFormat format)
    {
        if (claim.Trim().Length == 0) return;
        var location = at is { } seconds ? $"{seconds / 60:00}:{seconds % 60:00}" : null;
        var support = SessionEvidenceBuilder.Support(claim, location, lines);
        references.Add(new SessionEvidenceReference(
            "notes:" + path, title, claim, support, SessionEvidenceOrigin.StructuredNotes,
            SessionId: sessionId, Quote: claim, Location: location,
            NeedsConfirmation: support != SessionEvidenceSupport.Quoted, Kind: kind,
            StructuredTarget: new SessionStructuredNotesTarget(format, path, payloadRevision),
            TranscriptSourceRevision: transcriptSourceRevision,
            SourceStatus: SessionDerivedContentStatus.Current));
    }
}
