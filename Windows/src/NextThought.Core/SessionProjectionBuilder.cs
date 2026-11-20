using System.Security.Cryptography;
using System.Text;

namespace NextThought.Core;

/// <summary>
/// Builds a frozen, searchable view of a meeting (port of the portable part of
/// SessionProjectionBuilder). Transcript corrections, notes rendering and speaker labels are
/// still applied by the app's meeting page.
/// </summary>
public static class SessionProjectionBuilder
{
    public static SessionTextProjection Build(Guid sessionId, DateTimeOffset startedAt,
        IReadOnlyList<TranscriptLine> lines, IReadOnlyList<SessionProjectedAnswer> answers,
        SessionProjectedSummary? summary = null, IReadOnlyList<SessionQuestionEvent>? detected = null,
        SessionProjectionOptions? options = null, IReadOnlyList<TranscriptCorrection>? corrections = null,
        StructuredNotesPayload? notes = null, StructuredNotesDoc? textNotes = null)
    {
        options ??= new SessionProjectionOptions();
        corrections ??= [];
        var correctedLines = NotesCorrections.Apply(corrections, lines);
        var text = new List<SessionProjectedText>();
        var transcript = new List<SessionProjectedTranscriptLine>();
        var currentTranscriptRevision = TranscriptSourceRevision(sessionId, startedAt, lines, corrections);
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var applied = corrections.Where(correction => !correction.Reverted
                && (correction.LineId == line.Id || (correction.LineId is null && correction.Line == index))).ToList();
            var projectedLine = SessionProjectedTranscriptLine.From(line, correctedLines[index].Text, startedAt,
                options, appliedCorrections: applied);
            transcript.Add(projectedLine);
            var offset = projectedLine.Offset;
            var location = $"{offset / 60:00}:{offset % 60:00}";
            text.Add(new SessionProjectedText("line:" + line.Id + ":original", SessionProjectionTextKind.Transcript,
                line.Text, LineId: line.Id, Location: location,
                SourceRevision: TranscriptRevisions.Of(line.Id, line.Text)));
            if (projectedLine.CorrectedText != line.Text)
            {
                text.Add(new SessionProjectedText("line:" + line.Id + ":corrected", SessionProjectionTextKind.CorrectedTranscript,
                    projectedLine.CorrectedText, LineId: line.Id, Location: location,
                    SourceRevision: projectedLine.SourceRevision, AlternateText: line.Text));
            }
            if (line.Translation is { } translation && translation.Text.Trim().Length > 0)
            {
                text.Add(new SessionProjectedText("line:" + line.Id + ":translation", SessionProjectionTextKind.Translation,
                    translation.Text, LineId: line.Id, Location: location,
                    SourceRevision: "translation-" + translation.Revision));
            }
        }
        foreach (var question in detected ?? [])
        {
            text.Add(new SessionProjectedText("question:" + question.Id, SessionProjectionTextKind.Question,
                question.Text, SourceId: Guid.TryParse(question.Id, out var questionId) ? questionId : null));
        }
        foreach (var answer in answers)
        {
            text.Add(new SessionProjectedText("answer:" + answer.Id, SessionProjectionTextKind.Answer, answer.Answer,
                SourceId: answer.Id, SourceRevision: answer.SourceRevision, AlternateText: answer.Question));
        }
        var notesStatus = DerivedStatus(textNotes?.TranscriptSourceRevision ?? notes?.TranscriptSourceRevision, currentTranscriptRevision);
        var summaryStatus = SummaryStatus(summary, currentTranscriptRevision, notes);
        if (textNotes is not null)
        {
            void AddNote(string id, string text, SessionDerivedContentStatus status, string? revision = null)
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                text = text.Trim();
                // local function cannot capture text, so append below
            }
            var add = new List<SessionProjectedText>();
            void Append(string? value, string id, Guid? sourceId = null, string? location = null)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                add.Add(new SessionProjectedText(id, SessionProjectionTextKind.Notes, value,
                    SourceId: sourceId, Location: location,
                    SourceRevision: textNotes.TranscriptSourceRevision,
                    IsStale: notesStatus == SessionDerivedContentStatus.Stale,
                    SourceStatus: notesStatus));
            }
            Append(textNotes.Title, "notes:title");
            foreach (var section in textNotes.Sections)
            {
                Append(section.Title, "notes:section:" + section.Id);
                foreach (var item in section.Items)
                {
                    var itemId = Guid.TryParse(item.Id, out var parsed) ? parsed : (Guid?)null;
                    var location = item.Time is { } time && NotesTime.Seconds(time) is { } seconds ? NotesTime.Text(seconds) : null;
                    Append(item.Text, "notes:" + section.Id + ":" + item.Id + ":text", itemId, location);
                    Append(item.Owner, "notes:" + section.Id + ":" + item.Id + ":owner", itemId, location);
                    Append(item.Due, "notes:" + section.Id + ":" + item.Id + ":due", itemId, location);
                    Append(item.Answer, "notes:" + section.Id + ":" + item.Id + ":answer", itemId, location);
                    Append(item.Verdict, "notes:" + section.Id + ":" + item.Id + ":verdict", itemId, location);
                    Append(item.Better, "notes:" + section.Id + ":" + item.Id + ":better", itemId, location);
                    Append(item.Quote, "notes:" + section.Id + ":" + item.Id + ":quote", itemId, location);
                }
            }
            if (notes is not null) Append(StructuredNotesMarkdown.Render(notes), "notes:" + notes.Format.Raw());
            text.AddRange(add);
        }
        else if (notes is not null && StructuredNotesMarkdown.Render(notes) is { Length: > 0 } renderedNotes)
        {
            text.Add(new SessionProjectedText("notes:" + notes.Format.Raw(), SessionProjectionTextKind.Notes, renderedNotes,
                SourceRevision: notes.TranscriptSourceRevision, SourceStatus: notesStatus,
                IsStale: notesStatus == SessionDerivedContentStatus.Stale));
        }
        if (summary is not null)
        {
            var add = new List<SessionProjectedText>();
            void Append(string? value, string id)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                add.Add(new SessionProjectedText(id, SessionProjectionTextKind.Summary, value,
                    SourceRevision: summary.SourceTranscriptRevision,
                    IsStale: summaryStatus == SessionDerivedContentStatus.Stale,
                    SourceStatus: summaryStatus));
            }
            Append(summary.Title, "summary:" + sessionId + ":title");
            Append(summary.Markdown, "summary:" + sessionId + ":markdown");
            if (summary.Structured is { } latest)
                Append(StructuredNotesMarkdown.Render(latest), "summary:" + sessionId + ":structured:" + latest.Format.Raw());
            foreach (var payload in summary.StructuredAll ?? [])
                if (summary.Structured is null || payload.Format != summary.Structured.Format)
                    Append(StructuredNotesMarkdown.Render(payload), "summary:" + sessionId + ":structured:" + payload.Format.Raw());
            text.AddRange(add);
        }

        var revision = Revision(text);
        return new SessionTextProjection(sessionId, revision, currentTranscriptRevision, options, answers, text,
            summary is null ? null : summary with
            {
                SourceStatus = summaryStatus,
                SourceTranscriptRevision = summary.SourceTranscriptRevision
            })
        {
            Transcript = transcript,
            Notes = notes
        };
    }


    /// <summary>B7: fingerprint of the immutable transcript source (ids, times, speaker, text and corrections).</summary>
    public static string TranscriptSourceRevision(Guid sessionId, DateTimeOffset startedAt,
        IReadOnlyList<TranscriptLine> lines, IReadOnlyList<TranscriptCorrection> corrections)
    {
        var corrected = NotesCorrections.Apply(corrections, lines);
        var builder = new StringBuilder();
        builder.Append(sessionId.ToString("D")).Append('\u0000').Append("transcript-source-v1").Append('\u0001');
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var text = corrected[index].Text;
            builder.Append(line.Id).Append('\u0000').Append(line.Date.ToUnixTimeMilliseconds()).Append('\u0000')
                .Append(line.Speaker).Append('\u0000').Append(line.Voice?.ToString() ?? "").Append('\u0000')
                .Append(line.Text).Append('\u0000').Append(text).Append('\u0001');
            foreach (var correction in corrections.Where(correction => !correction.Reverted
                && (correction.LineId == line.Id || (correction.LineId is null && correction.Line == index))
                && text.Contains(correction.Corrected, StringComparison.Ordinal)))
                builder.Append(correction.Id).Append('\u0000').Append(correction.BaseRevision ?? "").Append('\u0000')
                    .Append(correction.Original).Append('\u0000').Append(correction.Corrected).Append('\u0001');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static SessionDerivedContentStatus DerivedStatus(string? saved, string current) =>
        saved is null ? SessionDerivedContentStatus.Unknown
            : saved == current ? SessionDerivedContentStatus.Current : SessionDerivedContentStatus.Stale;

    private static SessionDerivedContentStatus SummaryStatus(SessionProjectedSummary? summary, string current,
        StructuredNotesPayload? notes)
    {
        if (summary?.SourceTranscriptRevision is not { } saved) return SessionDerivedContentStatus.Unknown;
        var status = saved == current ? SessionDerivedContentStatus.Current : SessionDerivedContentStatus.Stale;
        if (notes is not null)
        {
            if (summary.StructuredNotesSourceRevision is { } notesSaved && SessionNotesEditor.PayloadRevision(notes) is { } notesCurrent)
                return notesSaved == notesCurrent ? status : SessionDerivedContentStatus.Stale;
            return status == SessionDerivedContentStatus.Stale ? status : SessionDerivedContentStatus.Unknown;
        }
        return summary.StructuredNotesSourceRevision is not null ? SessionDerivedContentStatus.Stale : status;
    }

    /// <summary>SHA-256 over ids and text in order, so the same sources yield the same revision.</summary>
    public static string Revision(IReadOnlyList<SessionProjectedText> text)
    {
        var builder = new StringBuilder();
        foreach (var item in text)
            builder.Append(item.Id).Append('\u0000').Append(item.Text).Append('\u0000')
                .Append(item.AlternateText ?? "").Append('\u0001');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }
}
