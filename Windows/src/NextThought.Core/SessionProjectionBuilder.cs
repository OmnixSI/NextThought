using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NextThought.Core;

/// <summary>A11: the Mac digest is SHA-256 over length-prefixed (big-endian) UTF-8 parts, full lowercase hex.</summary>
public static class SwiftDigest
{
    public static string Of(params string[] parts)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var part in parts)
        {
            var bytes = Encoding.UTF8.GetBytes(part);
            var length = new byte[8];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(length, (ulong)bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>Swift's <c>String(date.timeIntervalSince1970.bitPattern)</c>.</summary>
    public static string DateBitPattern(DateTimeOffset date) =>
        BitConverter.DoubleToUInt64Bits((date.UtcDateTime.Ticks - DateTimeOffset.UnixEpoch.UtcDateTime.Ticks) / 10_000_000.0)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
}
/// <summary>
/// Builds a frozen, searchable view of a meeting (port of the portable part of
/// SessionProjectionBuilder). Transcript corrections, notes rendering and speaker labels are
/// still applied by the app's meeting page.
/// </summary>
public static class SessionProjectionBuilder
{
    /// <summary>A11: Mac's structured-notes snapshot fingerprint: sorted-key JSON, ISO-8601 dates, base64, digest.</summary>
    public static string StructuredNotesRevision(StructuredNotesDoc notes)
    {
        var json = JsonSerializer.Serialize(notes, JsonDefaults.ArchiveOptions);
        var node = System.Text.Json.Nodes.JsonNode.Parse(json);
        var sorted = SortKeys(node);
        var options = new JsonSerializerOptions(JsonDefaults.ArchiveOptions)
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var bytes = Encoding.UTF8.GetBytes(sorted?.ToJsonString(options) ?? "null");
        return SwiftDigest.Of("structured-notes-v1", Convert.ToBase64String(bytes));
    }

    private static System.Text.Json.Nodes.JsonNode? Clone(System.Text.Json.Nodes.JsonNode? node) =>
        node is null ? null : System.Text.Json.Nodes.JsonNode.Parse(node.ToJsonString());

    private static System.Text.Json.Nodes.JsonNode? SortKeys(System.Text.Json.Nodes.JsonNode? node) => node switch
    {
        System.Text.Json.Nodes.JsonObject obj => new System.Text.Json.Nodes.JsonObject(
            obj.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => KeyValuePair.Create(pair.Key, Clone(SortKeys(pair.Value))))),
        System.Text.Json.Nodes.JsonArray array => new System.Text.Json.Nodes.JsonArray(
            array.Select(value => Clone(SortKeys(value))).ToArray()),
        _ => node
    };

    public static SessionTextProjection Build(Guid sessionId, DateTimeOffset startedAt,
        IReadOnlyList<TranscriptLine> lines, IReadOnlyList<SessionProjectedAnswer> answers,
        SessionProjectedSummary? summary = null, IReadOnlyList<SessionQuestionEvent>? detected = null,
        SessionProjectionOptions? options = null, IReadOnlyList<TranscriptCorrection>? corrections = null,
        StructuredNotesPayload? notes = null, StructuredNotesDoc? textNotes = null)
    {
        options ??= new SessionProjectionOptions();
        corrections ??= [];
        var correctedLines = NotesCorrections.Apply(corrections, lines);
        var speakerLabels = SpeakerLabels.PromptLabels(lines, null, null, false);
        var text = new List<SessionProjectedText>();
        var transcript = new List<SessionProjectedTranscriptLine>();
        var currentTranscriptRevision = TranscriptSourceRevision(sessionId, startedAt, lines, corrections);
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var applied = corrections.Where(correction => !correction.Reverted
                && (correction.LineId == line.Id || (correction.LineId is null && correction.Line == index))).ToList();
            var projectedLine = SessionProjectedTranscriptLine.From(line, correctedLines[index].Text, startedAt,
                options, speakerLabels.TryGetValue(line.Id, out var label) ? label : null, appliedCorrections: applied);
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
                    SourceRevision: "translation-" + translation.Revision,
                    IsStale: projectedLine.TranslationIsStale));
            }
        }
        foreach (var question in detected ?? [])
        {
            text.Add(new SessionProjectedText("question:" + question.Id + ":text", SessionProjectionTextKind.Question,
                question.Text, SourceId: Guid.TryParse(question.Id, out var questionId) ? questionId : null,
                Location: NotesTime.Text(Math.Max(0, (int)(question.Date - startedAt).TotalSeconds))));
            if (!string.IsNullOrWhiteSpace(question.LeadIn))
                text.Add(new SessionProjectedText("question:" + question.Id + ":lead-in", SessionProjectionTextKind.Question,
                    question.LeadIn, SourceId: Guid.TryParse(question.Id, out var leadId) ? leadId : null,
                    Location: NotesTime.Text(Math.Max(0, (int)(question.Date - startedAt).TotalSeconds))));
        }
        foreach (var answer in answers)
        {
            var answerLocation = NotesTime.Text(Math.Max(0, (int)(answer.Date - startedAt).TotalSeconds));
            text.Add(new SessionProjectedText("answer:" + answer.Id + ":question", SessionProjectionTextKind.Question,
                answer.Question, SourceId: answer.Id, Location: answerLocation));
            text.Add(new SessionProjectedText("answer:" + answer.Id + ":body", SessionProjectionTextKind.Answer, answer.Answer,
                SourceId: answer.Id, Location: answerLocation, SourceRevision: answer.SourceRevision,
                AlternateText: answer.Question));
        }
        var notesStatus = DerivedStatus(textNotes?.TranscriptSourceRevision ?? notes?.TranscriptSourceRevision, currentTranscriptRevision);
        var summaryStatus = SummaryStatus(summary, currentTranscriptRevision, notes);
        if (textNotes is not null)
        {
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
            void Append(string? value, string id, SessionProjectionTextKind kind = SessionProjectionTextKind.Summary)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                add.Add(new SessionProjectedText(id, kind, value,
                    SourceRevision: summary.SourceTranscriptRevision,
                    IsStale: summaryStatus == SessionDerivedContentStatus.Stale,
                    SourceStatus: summaryStatus));
            }
            Append(summary.Title, "summary:" + sessionId + ":title");
            Append(summary.Markdown, "summary:" + sessionId + ":markdown");
            foreach (var question in summary.Questions ?? [])
            {
                Append(question.Text, "summary:" + question.Id + ":question", SessionProjectionTextKind.Question);
                Append(question.AnswerGist, "summary:" + question.Id + ":answer", SessionProjectionTextKind.Answer);
            }
            for (var index = 0; index < (summary.Lessons ?? []).Count; index++)
                Append((summary.Lessons ?? [])[index], "summary:" + sessionId + ":lesson:" + index, SessionProjectionTextKind.Notes);
            for (var index = 0; index < (summary.Bank ?? []).Count; index++)
            {
                var entry = (summary.Bank ?? [])[index];
                Append(entry.Question, "summary:" + sessionId + ":bank:" + index + ":question", SessionProjectionTextKind.Question);
                Append(entry.Answer, "summary:" + sessionId + ":bank:" + index + ":answer", SessionProjectionTextKind.Answer);
            }
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


    /// <summary>A11: fingerprint of the immutable transcript source, byte-for-byte as the Mac computes it.</summary>
    public static string TranscriptSourceRevision(Guid sessionId, DateTimeOffset startedAt,
        IReadOnlyList<TranscriptLine> lines, IReadOnlyList<TranscriptCorrection> corrections)
    {
        var corrected = NotesCorrections.Apply(corrections, lines);
        var labels = SpeakerLabels.PromptLabels(lines, null, null, false);
        var parts = new List<string> { sessionId.ToString("D").ToUpperInvariant(), "transcript-source-v1" };
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var text = corrected[index].Text;
            parts.Add(line.Id.ToString("D").ToUpperInvariant());
            parts.Add(SwiftDigest.DateBitPattern(line.Date));
            parts.Add(line.Speaker);
            parts.Add(line.Voice?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "");
            parts.Add(labels.TryGetValue(line.Id, out var label) ? label : line.Speaker);
            parts.Add(line.Text);
            parts.Add(text);
            if (text == line.Text) continue;
            foreach (var correction in corrections.Where(correction => !correction.Reverted
                && (correction.LineId == line.Id || (correction.LineId is null && correction.Line == index))
                && text.Contains(correction.Corrected, StringComparison.Ordinal)))
            {
                parts.Add(correction.Id.ToUpperInvariant());
                parts.Add(correction.BaseRevision ?? "");
                parts.Add(JsonNamingPolicy.CamelCase.ConvertName(correction.Source.ToString()));
                parts.Add(correction.Original);
                parts.Add(correction.Corrected);
            }
        }
        return SwiftDigest.Of([.. parts]);
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
