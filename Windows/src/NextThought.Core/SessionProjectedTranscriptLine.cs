namespace NextThought.Core;

/// <summary>
/// One transcript line as the projection shows it: original and corrected text, translation and
/// the options applied (port of SessionProjectedTranscriptLine).
/// </summary>
public sealed record SessionProjectedTranscriptLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Date { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public string Speaker { get; init; } = "remote";
    public int? Voice { get; init; }
    public string SpeakerLabel { get; init; } = "";
    public SessionTranscriptVariant TranscriptVariant { get; init; } = SessionTranscriptVariant.Corrected;
    public SessionLanguageView LanguageView { get; init; } = SessionLanguageView.Bilingual;
    public string OriginalText { get; init; } = "";
    public string CorrectedText { get; init; } = "";
    public string? TranslationText { get; init; }
    public string? TranslationInputRevision { get; init; }
    public string SourceRevision { get; init; } = "";
    public string OriginalRevision { get; init; } = "";
    public IReadOnlyList<TranscriptCorrection> AppliedCorrections { get; init; } = [];
    public bool TranslationIsStale { get; init; }
    public int Offset { get; init; }

    /// <summary>Translated view prefers a non-empty translation; otherwise the selected transcript variant.</summary>
    public string DisplayText
    {
        get
        {
            if (LanguageView == SessionLanguageView.Translated
                && TranslationText is { } translation && translation.Trim().Length > 0)
                return translation;
            return TranscriptVariant == SessionTranscriptVariant.Original ? OriginalText : CorrectedText;
        }
    }

    public static SessionProjectedTranscriptLine From(TranscriptLine line, string correctedText,
        DateTimeOffset startedAt, SessionProjectionOptions options, string? speakerLabel = null,
        IReadOnlyList<TranscriptCorrection>? appliedCorrections = null)
    {
        var offset = Math.Max(0, (int)((line.StartedAt ?? line.Date) - startedAt).TotalSeconds);
        var originalRevision = TranscriptRevisions.Of(line.Id, line.Text);
        var sourceRevision = TranscriptRevisions.Of(line.Id,
            options.TranscriptVariant == SessionTranscriptVariant.Original ? line.Text : correctedText);
        var inputRevision = line.TranslationInputRevision
            ?? (line.Translation is { } translation ? "translation-" + translation.Revision
                : line.TranslatedText is not null ? originalRevision : null);
        var translationIsStale = (line.Translation is not null || !string.IsNullOrWhiteSpace(line.TranslatedText))
            && inputRevision is not null && inputRevision != sourceRevision;
        var label = speakerLabel ?? (line.Speaker == "me" ? "我" : "对方");
        return new SessionProjectedTranscriptLine
        {
            Id = line.Id,
            Date = line.Date,
            StartedAt = line.StartedAt,
            EndedAt = line.EndedAt,
            Speaker = line.Speaker,
            Voice = line.Voice,
            SpeakerLabel = label,
            TranscriptVariant = options.TranscriptVariant,
            LanguageView = options.LanguageView,
            OriginalText = line.Text,
            CorrectedText = correctedText,
            TranslationText = line.Translation?.Text ?? line.TranslatedText,
            TranslationInputRevision = inputRevision,
            TranslationIsStale = translationIsStale,
            SourceRevision = sourceRevision,
            OriginalRevision = originalRevision,
            AppliedCorrections = options.TranscriptVariant == SessionTranscriptVariant.Original ? [] : appliedCorrections ?? [],
            Offset = offset
        };
    }
}
