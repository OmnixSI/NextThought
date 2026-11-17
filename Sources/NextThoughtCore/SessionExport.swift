import Foundation

public enum SessionExportFormat: String, CaseIterable, Sendable, Identifiable {
    case markdown
    case docx
    case pdf
    case srt
    case vtt

    public var id: String { rawValue }
    /// The extension files are saved with. Markdown is `.md`, the one editors and
    /// previewers open; the case name is not an extension.
    public var fileExtension: String { self == .markdown ? "md" : rawValue }
    public var title: String {
        switch self {
        case .markdown: String(localized: "Markdown")
        case .docx: String(localized: "Word 文档")
        case .pdf: String(localized: "PDF")
        case .srt: String(localized: "SRT 字幕")
        case .vtt: String(localized: "WebVTT 字幕")
        }
    }
}

public enum SessionSubtitleFormat: String, Sendable, Equatable {
    case srt
    case vtt
}

public struct SessionSubtitleExport: Sendable, Equatable {
    public let text: String
    /// Cues whose end times were estimated because the archive has no end time.
    public let estimatedTimingCount: Int
    public let staleTranslationCount: Int

    public init(text: String, estimatedTimingCount: Int, staleTranslationCount: Int) {
        self.text = text
        self.estimatedTimingCount = estimatedTimingCount
        self.staleTranslationCount = staleTranslationCount
    }
}

/// Deterministic subtitles generated from the same frozen text projection as notes and Markdown.
/// Missing utterance end times are estimated and reported to the preview instead of described as
/// precise word-level alignment.
public enum SessionSubtitleExporter {
    private struct Cue {
        var start: TimeInterval
        var end: TimeInterval
        var text: String
    }

    public static func export(meta: SessionMeta, projection: SessionTextProjection,
                              format: SessionSubtitleFormat) -> SessionSubtitleExport {
        let lines = projection.transcript
        var cues: [Cue] = []
        var estimated = 0
        var staleTranslations = 0

        for index in lines.indices {
            let line = lines[index]
            let startDate = line.startedAt ?? line.date
            let start = max(0, startDate.timeIntervalSince(meta.startedAt))
            let nextStart: TimeInterval? = lines.indices.contains(index + 1)
                ? max(0, (lines[index + 1].startedAt ?? lines[index + 1].date).timeIntervalSince(meta.startedAt))
                : nil

            let end: TimeInterval
            if let endedAt = line.endedAt, endedAt.timeIntervalSince(meta.startedAt) > start {
                end = endedAt.timeIntervalSince(meta.startedAt)
            } else {
                estimated += 1
                let textLength = max(1, line.displayText.filter { !$0.isWhitespace }.count)
                let estimatedDuration = min(12, max(1.2, Double(textLength) / 5.0))
                if let nextStart, nextStart > start {
                    end = max(start + 0.25, min(start + estimatedDuration, nextStart))
                } else {
                    end = start + estimatedDuration
                }
            }

            let sourceText = line.transcriptVariant == .original ? line.originalText : line.correctedText
            let translation = line.translationText?.trimmingCharacters(in: .whitespacesAndNewlines)
            let hasTranslation = translation?.isEmpty == false
            let text: String
            switch line.languageView {
            case .source:
                text = "\(line.speakerLabel)：\(sourceText)"
            case .translated:
                if hasTranslation, let translation {
                    let label = line.translationIsStale ? "译文（可能已过期）" : "译文"
                    text = "\(line.speakerLabel) · \(label)：\(translation)"
                    if line.translationIsStale { staleTranslations += 1 }
                } else {
                    text = "\(line.speakerLabel) · 无译文，显示原文：\(sourceText)"
                }
            case .bilingual:
                var bilingual = "\(line.speakerLabel)：\(sourceText)"
                if hasTranslation, let translation {
                    let label = line.translationIsStale ? "译文（可能已过期）" : "译文"
                    bilingual += "\n\(label)：\(translation)"
                    if line.translationIsStale { staleTranslations += 1 }
                }
                text = bilingual
            }
            cues.append(Cue(start: start, end: end, text: text))
        }

        let body = cues.enumerated().map { index, cue in
            let separator = format == .vtt ? " --> " : " --> "
            let start = timestamp(cue.start, separator: format == .srt ? "," : ".")
            let end = timestamp(cue.end, separator: format == .srt ? "," : ".")
            let header = "\(start)\(separator)\(end)"
            return format == .srt ? "\(index + 1)\n\(header)\n\(cue.text)" : "\(header)\n\(cue.text)"
        }.joined(separator: "\n\n")
        let text = format == .vtt ? "WEBVTT\n\n" + body + (body.isEmpty ? "" : "\n") : body + (body.isEmpty ? "" : "\n")
        return SessionSubtitleExport(text: text, estimatedTimingCount: estimated,
                                     staleTranslationCount: staleTranslations)
    }

    private static func timestamp(_ seconds: TimeInterval, separator: String) -> String {
        let milliseconds = max(0, Int((seconds * 1_000).rounded()))
        let hours = milliseconds / 3_600_000
        let minutes = (milliseconds / 60_000) % 60
        let wholeSeconds = (milliseconds / 1_000) % 60
        let fraction = milliseconds % 1_000
        return String(format: "%02d:%02d:%02d%@%03d", hours, minutes, wholeSeconds, separator, fraction)
    }
}
