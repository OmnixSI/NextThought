using System.Text;

namespace NextThought.Core;

/// <summary>What a meeting can be exported as (port of SessionExportFormat).</summary>
public enum SessionExportFormat
{
    Markdown,
    Docx,
    Pdf,
    Srt,
    Vtt
}

public static class SessionExportFormats
{
    /// <summary>Markdown is saved as .md; the other names are the extensions.</summary>
    public static string FileExtension(this SessionExportFormat format) =>
        format == SessionExportFormat.Markdown ? "md" : format.ToString().ToLowerInvariant();

    public static string Title(this SessionExportFormat format) => format switch
    {
        SessionExportFormat.Markdown => "Markdown",
        SessionExportFormat.Docx => "Word 文档",
        SessionExportFormat.Pdf => "PDF",
        SessionExportFormat.Srt => "SRT 字幕",
        _ => "WebVTT 字幕"
    };
}

public enum SessionSubtitleFormat
{
    Srt,
    Vtt
}

/// <summary>Which language goes into the subtitle cues (Mac SessionExport subtitle mode).</summary>
public enum SessionSubtitleContent
{
    Original,
    Translation,
    Bilingual
}

/// <summary>Deterministic subtitles plus how many cues had to estimate their end time.</summary>
public sealed record SessionSubtitleExport(string Text, int EstimatedTimingCount, int StaleTranslationCount);

/// <summary>
/// Deterministic subtitles from the archived transcript (port of SessionSubtitleExporter in
/// SessionExport.swift). Missing utterance end times are estimated from text length and capped
/// by the next line, and the caller is told how many were estimated.
/// </summary>
public static class SessionSubtitleExporter
{
    public static SessionSubtitleExport Export(SessionMeta meta, IReadOnlyList<TranscriptLine> lines, SessionSubtitleFormat format,
        SessionSubtitleContent content = SessionSubtitleContent.Bilingual)
    {
        var cues = new List<(double Start, double End, string Text)>();
        var estimated = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var start = Math.Max(0, ((line.StartedAt ?? line.Date) - meta.StartedAt).TotalSeconds);
            double? nextStart = index + 1 < lines.Count
                ? Math.Max(0, ((lines[index + 1].StartedAt ?? lines[index + 1].Date) - meta.StartedAt).TotalSeconds)
                : null;

            double end;
            if (line.EndedAt is { } ended && (ended - meta.StartedAt).TotalSeconds > start)
            {
                end = (ended - meta.StartedAt).TotalSeconds;
            }
            else
            {
                estimated++;
                var length = Math.Max(1, line.Text.Count(character => !char.IsWhiteSpace(character)));
                var duration = Math.Min(12, Math.Max(1.2, length / 5.0));
                end = nextStart is { } next && next > start
                    ? Math.Max(start + 0.25, Math.Min(start + duration, next))
                    : start + duration;
            }

            var source = $"{SpeakerLabel(line.Speaker)}：{line.Text}";
            var translated = line.Translation is { } translation && !string.IsNullOrWhiteSpace(translation.Text)
                ? $"译文：{translation.Text.Trim()}" : "";
            var text = content switch
            {
                SessionSubtitleContent.Original => source,
                SessionSubtitleContent.Translation => translated.Length > 0 ? translated : source,
                _ => translated.Length > 0 ? source + "\n" + translated : source
            };
            cues.Add((start, end, text));
        }

        var body = string.Join("\n\n", cues.Select((cue, index) => format == SessionSubtitleFormat.Srt
            ? $"{index + 1}\n{Stamp(cue.Start, ',')} --> {Stamp(cue.End, ',')}\n{cue.Text}"
            : $"{Stamp(cue.Start, '.')} --> {Stamp(cue.End, '.')}\n{cue.Text}"));
        var output = format == SessionSubtitleFormat.Vtt
            ? "WEBVTT\n\n" + body + (body.Length == 0 ? "" : "\n")
            : body + (body.Length == 0 ? "" : "\n");
        return new SessionSubtitleExport(output, estimated, 0);
    }

    /// <summary>B16: exported subtitles from the same projected lines the notes page uses.</summary>
    public static SessionSubtitleExport ExportProjected(SessionMeta meta, IReadOnlyList<SessionProjectedTranscriptLine> lines,
        SessionSubtitleFormat format, SessionSubtitleContent content = SessionSubtitleContent.Bilingual)
    {
        var cues = new List<(double Start, double End, string Text)>();
        var estimated = 0;
        var staleTranslations = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var start = Math.Max(0, line.Offset);
            double? nextStart = index + 1 < lines.Count ? Math.Max(0, lines[index + 1].Offset) : null;
            double end;
            if (line.EndedAt is { } ended && (ended - meta.StartedAt).TotalSeconds > start)
                end = (ended - meta.StartedAt).TotalSeconds;
            else
            {
                estimated++;
                var length = Math.Max(1, line.DisplayText.Count(character => !char.IsWhiteSpace(character)));
                var duration = Math.Min(12, Math.Max(1.2, length / 5.0));
                end = nextStart is { } next && next > start
                    ? Math.Max(start + 0.25, Math.Min(start + duration, next)) : start + duration;
            }

            var sourceText = line.TranscriptVariant == SessionTranscriptVariant.Original
                ? line.OriginalText : line.CorrectedText;
            var translation = line.TranslationText?.Trim();
            var hasTranslation = !string.IsNullOrEmpty(translation);
            var label = line.TranslationIsStale ? "\u8BD1\u6587\uFF08\u53EF\u80FD\u5DF2\u8FC7\u671F\uFF09" : "\u8BD1\u6587";
            var text = content switch
            {
                SessionSubtitleContent.Original => line.SpeakerLabel + "\uFF1A" + sourceText,
                SessionSubtitleContent.Translation => hasTranslation
                    ? line.SpeakerLabel + " \u00B7 " + label + "\uFF1A" + translation
                    : line.SpeakerLabel + " \u00B7 \u65E0\u8BD1\u6587\uFF0C\u663E\u793A\u539F\u6587\uFF1A" + sourceText,
                _ => line.SpeakerLabel + "\uFF1A" + sourceText
                     + (hasTranslation ? "\n" + label + "\uFF1A" + translation : "")
            };
            if (hasTranslation && line.TranslationIsStale) staleTranslations++;
            cues.Add((start, end, text));
        }

        var body = string.Join("\n\n", cues.Select((cue, index) => format == SessionSubtitleFormat.Srt
            ? $"{index + 1}\n{Stamp(cue.Start, ',')} --> {Stamp(cue.End, ',')}\n{cue.Text}"
            : $"{Stamp(cue.Start, '.')} --> {Stamp(cue.End, '.')}\n{cue.Text}"));
        var output = format == SessionSubtitleFormat.Vtt
            ? "WEBVTT\n\n" + body + (body.Length == 0 ? "" : "\n")
            : body + (body.Length == 0 ? "" : "\n");
        return new SessionSubtitleExport(output, estimated, staleTranslations);
    }

    public static string SpeakerLabel(string speaker) => speaker switch
    {
        "remote" => "对方",
        "room" => "现场",
        "me" => "我",
        _ => speaker
    };

    /// <summary>HH:MM:SS,mmm for SRT and HH:MM:SS.mmm for WebVTT.</summary>
    public static string Stamp(double seconds, char separator)
    {
        var milliseconds = Math.Max(0, (long)Math.Round(seconds * 1000));
        var hours = milliseconds / 3_600_000;
        var minutes = milliseconds / 60_000 % 60;
        var wholeSeconds = milliseconds / 1_000 % 60;
        var fraction = milliseconds % 1_000;
        return $"{hours:00}:{minutes:00}:{wholeSeconds:00}{separator}{fraction:000}";
    }
}
