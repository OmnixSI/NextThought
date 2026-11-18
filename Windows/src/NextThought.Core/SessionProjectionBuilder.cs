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
        SessionProjectionOptions? options = null, IReadOnlyList<TranscriptCorrection>? corrections = null)
    {
        options ??= new SessionProjectionOptions();
        corrections ??= [];
        var correctedLines = NotesCorrections.Apply(corrections, lines);
        var text = new List<SessionProjectedText>();
        var transcript = new List<SessionProjectedTranscriptLine>();
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
            text.Add(new SessionProjectedText("line:" + line.Id, SessionProjectionTextKind.Transcript,
                projectedLine.DisplayText, LineId: line.Id, Location: location,
                SourceRevision: TranscriptRevisions.Of(line.Id, projectedLine.DisplayText),
                AlternateText: options.TranscriptVariant == SessionTranscriptVariant.Original ? null : projectedLine.OriginalText));
            if (line.Translation is { } translation && translation.Text.Trim().Length > 0
                && options.LanguageView != SessionLanguageView.Source)
            {
                text.Add(new SessionProjectedText("translation:" + line.Id, SessionProjectionTextKind.Translation,
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
        if (summary is { Markdown.Length: > 0 } projected)
        {
            text.Add(new SessionProjectedText("summary", SessionProjectionTextKind.Summary, projected.Markdown,
                SourceRevision: projected.SourceTranscriptRevision, IsStale: projected.SourceStatus == SessionDerivedContentStatus.Stale,
                SourceStatus: projected.SourceStatus));
        }

        var revision = Revision(text);
        return new SessionTextProjection(sessionId, revision, "", options, answers, text, summary)
        {
            Transcript = transcript
        };
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
