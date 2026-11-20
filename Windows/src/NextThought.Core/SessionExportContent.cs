namespace NextThought.Core;

/// <summary>
/// E19: the content one export-sheet selection produces (port of SessionExportPreviewContent.make
/// in SessionExportUI.swift). Document formats render the same Markdown projection the notes page
/// shows; subtitle formats take the same projected lines so the two can never disagree.
/// </summary>
public static class SessionExportContent
{
    public static bool UsesPageLayout(SessionExportFormat format) =>
        format is SessionExportFormat.Markdown or SessionExportFormat.Docx or SessionExportFormat.Pdf;

    /// <summary>SessionExportUI.swift subtitle language mapping (原文/译文/原文与译文).</summary>
    public static SessionSubtitleContent SubtitleContent(SessionLanguageView view) => view switch
    {
        SessionLanguageView.Source => SessionSubtitleContent.Original,
        SessionLanguageView.Translated => SessionSubtitleContent.Translation,
        _ => SessionSubtitleContent.Bilingual
    };

    public static string Build(
        SessionExportFormat format,
        SessionTextProjection projection,
        SessionMeta meta,
        IReadOnlyList<TranscriptLine> lines,
        string modeTitle,
        double durationSeconds,
        string audioSource,
        StructuredNotesPayload? notes = null,
        IReadOnlyList<SessionQuestionEvent>? detectedQuestions = null,
        string userContentMarkdown = "")
    {
        switch (format)
        {
            case SessionExportFormat.Srt:
                return SessionSubtitleExporter.ExportProjected(meta, projection.Transcript, SessionSubtitleFormat.Srt,
                    SubtitleContent(projection.Options.LanguageView)).Text;
            case SessionExportFormat.Vtt:
                return SessionSubtitleExporter.ExportProjected(meta, projection.Transcript, SessionSubtitleFormat.Vtt,
                    SubtitleContent(projection.Options.LanguageView)).Text;
            default:
                var markdown = SessionMarkdownExporter.Export(meta.Title, modeTitle, meta.StartedAt, durationSeconds,
                    audioSource, projection, lines, detectedQuestions, notes);
                if (userContentMarkdown.Length == 0) return markdown;
                return markdown.TrimEnd() + "\n\n" + userContentMarkdown.Trim() + "\n";
        }
    }

    /// <summary>SessionExportUI.swift baseName: the title with path separators made harmless.</summary>
    public static string SuggestedFileName(string title, SessionExportFormat format)
    {
        var name = (string.IsNullOrWhiteSpace(title) ? "会议" : title)
            .Replace("/", "-", StringComparison.Ordinal)
            .Replace(":", "-", StringComparison.Ordinal)
            .Replace("\\", "-", StringComparison.Ordinal);
        return name + "." + format.FileExtension();
    }
}
