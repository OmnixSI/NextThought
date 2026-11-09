import CryptoKit
import Foundation

public enum SessionTranscriptVariant: String, Codable, Sendable, Equatable {
    case original
    case corrected
}

public enum SessionLanguageView: String, Codable, Sendable, Equatable {
    case source
    case translated
    case bilingual
}

public struct SessionProjectionOptions: Sendable, Equatable {
    public var transcriptVariant: SessionTranscriptVariant
    public var languageView: SessionLanguageView

    public init(transcriptVariant: SessionTranscriptVariant = .corrected,
                languageView: SessionLanguageView = .bilingual) {
        self.transcriptVariant = transcriptVariant
        self.languageView = languageView
    }
}

public enum SessionProjectionTextKind: String, Codable, Sendable, Equatable {
    case transcript
    case correctedTranscript
    case translation
    case question
    case answer
    case summary
    case notes
}

public enum SessionDerivedContentStatus: String, Codable, Sendable, Equatable {
    case current
    case stale
    /// Older archives did not store a source revision, so freshness cannot be proven.
    case unknown
}

/// One stable unit of archived session text for search and navigation.
public struct SessionProjectedText: Sendable, Equatable, Identifiable {
    public let id: String
    public let kind: SessionProjectionTextKind
    public let sourceID: UUID?
    public let lineID: UUID?
    public let location: String?
    public let sourceRevision: String?
    public let isStale: Bool
    public let sourceStatus: SessionDerivedContentStatus?
    public let text: String
    /// Alternate spellings stay searchable without replacing what the user sees.
    public let alternateText: String?

    public init(id: String, kind: SessionProjectionTextKind, sourceID: UUID? = nil, lineID: UUID? = nil,
                location: String? = nil, sourceRevision: String? = nil, isStale: Bool = false,
                sourceStatus: SessionDerivedContentStatus? = nil,
                text: String, alternateText: String? = nil) {
        self.id = id
        self.kind = kind
        self.sourceID = sourceID
        self.lineID = lineID
        self.location = location
        self.sourceRevision = sourceRevision
        self.isStale = isStale
        self.sourceStatus = sourceStatus
        self.text = text
        self.alternateText = alternateText
    }
}

public struct SessionProjectedTranscriptLine: Sendable, Equatable, Identifiable {
    public let id: UUID
    public let date: Date
    public let startedAt: Date?
    public let endedAt: Date?
    public let speaker: SessionSpeaker
    public let voice: Int?
    public let speakerLabel: String
    public let transcriptVariant: SessionTranscriptVariant
    public let languageView: SessionLanguageView
    public let originalText: String
    public let correctedText: String
    public let translationText: String?
    public let translationInputRevision: String?
    public let sourceRevision: String
    public let originalRevision: String
    private let availableCorrections: [TranscriptCorrection]
    public let translationIsStale: Bool
    public let offset: Int

    init(id: UUID, date: Date, startedAt: Date?, endedAt: Date?, speaker: SessionSpeaker, voice: Int?,
         speakerLabel: String, transcriptVariant: SessionTranscriptVariant, languageView: SessionLanguageView,
         originalText: String, correctedText: String, translationText: String?, translationInputRevision: String?,
         sourceRevision: String, originalRevision: String, availableCorrections: [TranscriptCorrection],
         translationIsStale: Bool, offset: Int) {
        self.id = id
        self.date = date
        self.startedAt = startedAt
        self.endedAt = endedAt
        self.speaker = speaker
        self.voice = voice
        self.speakerLabel = speakerLabel
        self.transcriptVariant = transcriptVariant
        self.languageView = languageView
        self.originalText = originalText
        self.correctedText = correctedText
        self.translationText = translationText
        self.translationInputRevision = translationInputRevision
        self.sourceRevision = sourceRevision
        self.originalRevision = originalRevision
        self.availableCorrections = availableCorrections
        self.translationIsStale = translationIsStale
        self.offset = offset
    }

    public var displayText: String {
        if languageView == .translated, let translationText,
           !translationText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { return translationText }
        return transcriptVariant == .original ? originalText : correctedText
    }
    public var appliedCorrections: [TranscriptCorrection] { transcriptVariant == .original ? [] : availableCorrections }
}

public struct SessionProjectedAnswer: Sendable, Equatable, Identifiable {
    public let id: UUID
    public let date: Date
    public let question: String
    public let answer: String
    public let sourceRevision: String

    public init(id: UUID, date: Date, question: String, answer: String, sourceRevision: String) {
        self.id = id
        self.date = date
        self.question = question
        self.answer = answer
        self.sourceRevision = sourceRevision
    }
}

public struct SessionProjectedSummary: Sendable, Equatable {
    public let title: String
    public let markdown: String
    public let sourceTranscriptRevision: String?
    public let sourceStatus: SessionDerivedContentStatus

    public init(title: String, markdown: String, sourceTranscriptRevision: String? = nil,
                sourceStatus: SessionDerivedContentStatus = .unknown) {
        self.title = title
        self.markdown = markdown
        self.sourceTranscriptRevision = sourceTranscriptRevision
        self.sourceStatus = sourceStatus
    }
}

/// A frozen view of the session's transcript, translations, answers and notes.
/// Search and export should consume this value instead of independently reapplying corrections.
public struct SessionTextProjection: Sendable, Equatable {
    public let sessionID: UUID
    /// SHA-256 fingerprint of the exact sources and options represented by this projection.
    public let revision: String
    public let transcriptSourceRevision: String
    public let options: SessionProjectionOptions
    public let transcript: [SessionProjectedTranscriptLine]
    public let answers: [SessionProjectedAnswer]
    public let summary: SessionProjectedSummary?
    /// The current structured notes (including user edits) represented by this projection.
    public let notes: StructuredNotes?
    public let detectedQuestions: [SessionQuestionEvent]
    public let searchableText: [SessionProjectedText]

    /// Overall freshness of generated notes and summaries included with this projection.
    /// Original transcript lines alone are current because they are the source of record.
    public var derivedContentStatus: SessionDerivedContentStatus {
        let statuses = searchableText.compactMap(\.sourceStatus) + [summary?.sourceStatus].compactMap { $0 }
        if statuses.contains(.stale) { return .stale }
        if statuses.contains(.unknown) { return .unknown }
        return .current
    }

    public init(sessionID: UUID, revision: String, transcriptSourceRevision: String = "", options: SessionProjectionOptions,
                transcript: [SessionProjectedTranscriptLine], answers: [SessionProjectedAnswer],
                detectedQuestions: [SessionQuestionEvent], searchableText: [SessionProjectedText],
                summary: SessionProjectedSummary? = nil, notes: StructuredNotes? = nil) {
        self.sessionID = sessionID
        self.revision = revision
        self.transcriptSourceRevision = transcriptSourceRevision
        self.options = options
        self.transcript = transcript
        self.answers = answers
        self.summary = summary
        self.notes = notes
        self.detectedQuestions = detectedQuestions
        self.searchableText = searchableText
    }
}

public enum SessionProjectionBuilder {
    public static func build(meta: SessionMeta, lines: [SessionTranscriptLine], answers: [SessionAnswerEvent] = [],
                             detected: [SessionQuestionEvent] = [], summary: SessionSummary? = nil,
                             notes: StructuredNotes? = nil, options: SessionProjectionOptions = .init()) -> SessionTextProjection {
        let corrections = notes?.activeCorrections ?? []
        let correctedLines = NotesCorrections.apply(corrections, to: lines)
        let labels = SpeakerLabels.promptLabels(lines, voiceNames: meta.voiceNames, meVoices: Set(meta.meVoices ?? []), english: false)
        var projectedLines: [SessionProjectedTranscriptLine] = []
        projectedLines.reserveCapacity(lines.count)
        var searchable: [SessionProjectedText] = []

        for (index, raw) in lines.enumerated() {
            let corrected = correctedLines.indices.contains(index) ? correctedLines[index].text : raw.text
            let applied = correctionsForLine(corrections, index: index, line: raw, correctedText: corrected)
            let rawRevision = transcriptRevision(id: raw.id, text: raw.text)
            let correctedRevision = transcriptRevision(id: raw.id, text: corrected)
            let selectedSourceRevision = options.transcriptVariant == .original ? rawRevision : correctedRevision
            let inputRevision = raw.translationInputRevision ?? (raw.translatedText == nil ? nil : rawRevision)
            let translationIsStale = raw.translatedText.map {
                !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            } == true
                && inputRevision != nil
                && inputRevision != selectedSourceRevision
            let offset = max(0, Int(raw.date.timeIntervalSince(meta.startedAt)))
            let location = NotesTime.text(offset)
            let line = SessionProjectedTranscriptLine(
                id: raw.id,
                date: raw.date,
                startedAt: raw.startedAt,
                endedAt: raw.endedAt,
                speaker: raw.speaker,
                voice: raw.voice,
                speakerLabel: labels[raw.id] ?? raw.speaker.label,
                transcriptVariant: options.transcriptVariant,
                languageView: options.languageView,
                originalText: raw.text,
                correctedText: corrected,
                translationText: raw.translatedText,
                translationInputRevision: inputRevision,
                sourceRevision: selectedSourceRevision,
                originalRevision: rawRevision,
                availableCorrections: applied,
                translationIsStale: translationIsStale,
                offset: offset
            )
            projectedLines.append(line)

            searchable.append(SessionProjectedText(id: "line:\(raw.id.uuidString):original", kind: .transcript,
                                                   sourceID: raw.id, lineID: raw.id, location: location,
                                                   sourceRevision: rawRevision, text: raw.text))
            if corrected != raw.text {
                searchable.append(SessionProjectedText(id: "line:\(raw.id.uuidString):corrected", kind: .correctedTranscript,
                                                       sourceID: raw.id, lineID: raw.id, location: location,
                                                       sourceRevision: correctedRevision, text: corrected,
                                                       alternateText: raw.text))
            }
            if let translation = raw.translatedText, !translation.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                let translationRevision = digest([raw.id.uuidString, "translation", inputRevision ?? "", translation])
                searchable.append(SessionProjectedText(id: "line:\(raw.id.uuidString):translation", kind: .translation,
                                                       sourceID: raw.id, lineID: raw.id, location: location,
                                                       sourceRevision: translationRevision, isStale: translationIsStale,
                                                       text: translation))
            }
        }

        let currentTranscriptSourceRevision = transcriptSourceRevision(meta: meta, lines: lines, notes: notes)
        let notesStatus = derivedStatus(savedRevision: notes?.transcriptSourceRevision,
                                        currentRevision: currentTranscriptSourceRevision)
        var summaryStatus: SessionDerivedContentStatus
        if let savedRevision = summary?.transcriptSourceRevision {
            summaryStatus = savedRevision == currentTranscriptSourceRevision ? .current : .stale
        } else {
            summaryStatus = .unknown
        }
        if let notes {
            if let savedNotesRevision = summary?.structuredNotesSourceRevision,
               let currentNotesRevision = structuredNotesRevision(notes) {
                if savedNotesRevision != currentNotesRevision { summaryStatus = .stale }
            } else if summaryStatus != .stale {
                // Older archives cannot prove that rendered summary text still matches
                // notes.json after a manual note edit.
                summaryStatus = .unknown
            }
        } else if summary?.structuredNotesSourceRevision != nil {
            // The summary was generated from structured notes that are no longer available.
            summaryStatus = .stale
        }

        let projectedAnswers = answers.map { answer in
            SessionProjectedAnswer(id: answer.id, date: answer.date, question: answer.question, answer: answer.answer,
                                   sourceRevision: digest([answer.id.uuidString, answer.question, answer.answer]))
        }
        for question in detected {
            let location = NotesTime.text(max(0, Int(question.date.timeIntervalSince(meta.startedAt))))
            append(question.text, id: "question:\(question.id.uuidString):text", kind: .question, sourceID: question.id, location: location, into: &searchable)
            append(question.leadIn, id: "question:\(question.id.uuidString):lead-in", kind: .question, sourceID: question.id, location: location, into: &searchable)
        }
        for answer in projectedAnswers {
            let location = NotesTime.text(max(0, Int(answer.date.timeIntervalSince(meta.startedAt))))
            append(answer.question, id: "answer:\(answer.id.uuidString):question", kind: .question, sourceID: answer.id, location: location, into: &searchable)
            append(answer.answer, id: "answer:\(answer.id.uuidString):body", kind: .answer, sourceID: answer.id, location: location,
                   revision: answer.sourceRevision, into: &searchable)
            if let event = answers.first(where: { $0.id == answer.id }) {
                append(event.webAnswer, id: "answer:\(answer.id.uuidString):web", kind: .answer, sourceID: answer.id,
                       location: location, revision: answer.sourceRevision, into: &searchable)
            }
        }
        appendNotes(notes, sourceStatus: notesStatus, into: &searchable)
        appendSummary(summary, sessionID: meta.id, sourceStatus: summaryStatus,
                      currentTranscriptSourceRevision: currentTranscriptSourceRevision, into: &searchable)

        var revisionParts = [meta.id.uuidString, options.transcriptVariant.rawValue, options.languageView.rawValue]
        for line in projectedLines {
            revisionParts.append(contentsOf: [line.id.uuidString, dateRevision(line.date), dateRevision(line.startedAt),
                                               dateRevision(line.endedAt), line.speaker.rawValue,
                                               line.voice.map { String($0) } ?? "", line.speakerLabel])
            revisionParts.append(contentsOf: [line.originalText, line.correctedText, line.translationText ?? ""])
            revisionParts.append(contentsOf: [line.translationInputRevision ?? "", line.sourceRevision, line.originalRevision,
                                               line.translationIsStale ? "translation-stale" : "translation-current",
                                               String(line.offset)])
            revisionParts.append("corrections:\(line.appliedCorrections.count)")
            for correction in line.appliedCorrections {
                revisionParts.append(contentsOf: [correction.id.uuidString, String(correction.line),
                                                   correction.lineID?.uuidString ?? "", correction.baseRevision ?? ""])
                revisionParts.append(contentsOf: [correction.source.rawValue, correction.original, correction.corrected,
                                                   correction.reverted ? "reverted" : "active"])
            }
        }
        for answer in projectedAnswers {
            revisionParts.append(contentsOf: [answer.id.uuidString, dateRevision(answer.date), answer.question,
                                               answer.answer, answer.sourceRevision])
        }
        for question in detected {
            revisionParts.append(contentsOf: [question.id.uuidString, dateRevision(question.date),
                                               question.text, question.leadIn ?? "", question.intent,
                                               String(question.score.bitPattern), question.disposition])
        }
        if let summary {
            revisionParts.append(contentsOf: ["summary-present", summary.title, summary.markdown])
        } else {
            revisionParts.append("summary-absent")
        }
        for item in searchable {
            revisionParts.append(contentsOf: [item.id, item.kind.rawValue, item.sourceID?.uuidString ?? "",
                                               item.lineID?.uuidString ?? "", item.location ?? "", item.sourceRevision ?? "",
                                               item.sourceStatus?.rawValue ?? "",
                                               item.isStale ? "stale" : "current", item.text, item.alternateText ?? ""])
        }
        let revision = digest(revisionParts)
        let projectedSummary = summary.map {
            SessionProjectedSummary(title: $0.title, markdown: $0.markdown,
                                    sourceTranscriptRevision: $0.transcriptSourceRevision,
                                    sourceStatus: summaryStatus)
        }
        return SessionTextProjection(sessionID: meta.id, revision: revision,
                                     transcriptSourceRevision: currentTranscriptSourceRevision, options: options,
                                     transcript: projectedLines, answers: projectedAnswers,
                                     detectedQuestions: detected, searchableText: searchable,
                                     summary: projectedSummary, notes: notes)
    }

    /// Stable fingerprint for the immutable source phrase behind a transcript line.
    public static func transcriptRevision(id: UUID, text: String) -> String {
        digest([id.uuidString, text])
    }

    /// Stable fingerprint of the exact structured-notes snapshot used to render a summary.
    public static func structuredNotesRevision(_ notes: StructuredNotes) -> String? {
        guard let data = try? JSONLines.encoder().encode(notes) else { return nil }
        return digest(["structured-notes-v1", data.base64EncodedString()])
    }

    public static func transcriptSourceRevision(meta: SessionMeta, lines: [SessionTranscriptLine],
                                                notes: StructuredNotes? = nil) -> String {
        let corrections = notes?.activeCorrections ?? []
        let correctedLines = NotesCorrections.apply(corrections, to: lines)
        let labels = SpeakerLabels.promptLabels(lines, voiceNames: meta.voiceNames,
                                                meVoices: Set(meta.meVoices ?? []), english: false)
        var parts = [meta.id.uuidString, "transcript-source-v1"]
        for (index, line) in lines.enumerated() {
            let corrected = correctedLines.indices.contains(index) ? correctedLines[index].text : line.text
            parts.append(contentsOf: [line.id.uuidString, dateRevision(line.date), line.speaker.rawValue,
                                      line.voice.map { String($0) } ?? "", labels[line.id] ?? line.speaker.label,
                                      line.text, corrected])
            for correction in correctionsForLine(corrections, index: index, line: line, correctedText: corrected) {
                parts.append(contentsOf: [correction.id.uuidString, correction.baseRevision ?? "",
                                          correction.source.rawValue, correction.original, correction.corrected])
            }
        }
        return digest(parts)
    }

    private static func dateRevision(_ date: Date?) -> String {
        guard let date else { return "" }
        return String(date.timeIntervalSince1970.bitPattern)
    }

    private static func correctionsForLine(_ corrections: [TranscriptCorrection], index: Int,
                                           line: SessionTranscriptLine, correctedText: String) -> [TranscriptCorrection] {
        guard correctedText != line.text else { return [] }
        return corrections.filter { correction in
            let appliesToLine = correction.lineID.map { $0 == line.id } ?? (correction.line == index)
            return appliesToLine && !correction.reverted && correctedText.contains(correction.corrected)
        }
    }

    private static func derivedStatus(savedRevision: String?, currentRevision: String) -> SessionDerivedContentStatus {
        guard let savedRevision else { return .unknown }
        return savedRevision == currentRevision ? .current : .stale
    }

    private static func appendNotes(_ notes: StructuredNotes?, sourceStatus: SessionDerivedContentStatus,
                                    into output: inout [SessionProjectedText]) {
        guard let notes else { return }
        let isStale = sourceStatus == .stale
        append(notes.title, id: "notes:title", kind: .notes, revision: notes.transcriptSourceRevision,
               isStale: isStale, sourceStatus: sourceStatus, into: &output)
        for section in notes.sections {
            append(section.title, id: "notes:section:\(section.id)", kind: .notes,
                   revision: notes.transcriptSourceRevision, isStale: isStale,
                   sourceStatus: sourceStatus, into: &output)
            for item in section.items {
                let location = item.time.flatMap(NotesTime.seconds).map(NotesTime.text)
                let fields: [(String, String?)] = [
                    ("text", item.text), ("owner", item.owner), ("due", item.due), ("answer", item.answer),
                    ("verdict", item.verdict), ("better", item.better), ("quote", item.quote)
                ]
                for (field, value) in fields {
                    append(value, id: "notes:\(section.id):\(item.id.uuidString):\(field)", kind: .notes,
                           sourceID: item.id, location: location, revision: notes.transcriptSourceRevision,
                           isStale: isStale, sourceStatus: sourceStatus, into: &output)
                }
            }
        }
    }

    private static func appendSummary(_ summary: SessionSummary?, sessionID: UUID,
                                      sourceStatus: SessionDerivedContentStatus,
                                      currentTranscriptSourceRevision: String,
                                      into output: inout [SessionProjectedText]) {
        guard let summary else { return }
        let summaryIsStale = sourceStatus == .stale
        append(summary.title, id: "summary:\(sessionID.uuidString):title", kind: .summary, sourceID: sessionID,
               revision: summary.transcriptSourceRevision, isStale: summaryIsStale, sourceStatus: sourceStatus, into: &output)
        append(summary.markdown, id: "summary:\(sessionID.uuidString):markdown", kind: .summary, sourceID: sessionID,
               revision: summary.transcriptSourceRevision, isStale: summaryIsStale, sourceStatus: sourceStatus, into: &output)
        for question in summary.questions {
            append(question.text, id: "summary:\(question.id.uuidString):question", kind: .question, sourceID: question.id,
                   revision: summary.transcriptSourceRevision, isStale: summaryIsStale, sourceStatus: sourceStatus, into: &output)
            append(question.answerGist, id: "summary:\(question.id.uuidString):answer", kind: .answer, sourceID: question.id,
                   revision: summary.transcriptSourceRevision, isStale: summaryIsStale, sourceStatus: sourceStatus, into: &output)
        }
        for (index, lesson) in (summary.lessons ?? []).enumerated() {
            append(lesson, id: "summary:\(sessionID.uuidString):lesson:\(index)", kind: .notes, sourceID: sessionID,
                   revision: summary.transcriptSourceRevision, isStale: summaryIsStale, sourceStatus: sourceStatus, into: &output)
        }
        for (index, entry) in (summary.bank ?? []).enumerated() {
            append(entry.question, id: "summary:\(sessionID.uuidString):bank:\(index):question", kind: .question, sourceID: sessionID,
                   revision: summary.transcriptSourceRevision, isStale: summaryIsStale, sourceStatus: sourceStatus, into: &output)
            append(entry.answer, id: "summary:\(sessionID.uuidString):bank:\(index):answer", kind: .answer, sourceID: sessionID,
                   revision: summary.transcriptSourceRevision, isStale: summaryIsStale, sourceStatus: sourceStatus, into: &output)
        }
        var payloads = summary.structuredAll ?? []
        if let single = summary.structured, !payloads.contains(where: { $0.format == single.format }) { payloads.append(single) }
        for payload in payloads where payload.hasContent {
            let payloadStatus = payload.transcriptSourceRevision.map {
                $0 == currentTranscriptSourceRevision ? SessionDerivedContentStatus.current : .stale
            } ?? .unknown
            append(StructuredNotesMarkdown.render(payload), id: "summary:\(sessionID.uuidString):structured:\(payload.format.rawValue)",
                   kind: .notes, sourceID: sessionID, revision: payload.transcriptSourceRevision,
                   isStale: payloadStatus == .stale, sourceStatus: payloadStatus, into: &output)
        }
    }

    private static func append(_ value: String?, id: String, kind: SessionProjectionTextKind, sourceID: UUID? = nil,
                               location: String? = nil, revision: String? = nil, isStale: Bool = false,
                               sourceStatus: SessionDerivedContentStatus? = nil,
                               into output: inout [SessionProjectedText]) {
        guard let value, !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }
        output.append(SessionProjectedText(id: id, kind: kind, sourceID: sourceID, location: location,
                                           sourceRevision: revision, isStale: isStale, sourceStatus: sourceStatus, text: value))
    }

    private static func digest(_ components: [String]) -> String {
        var bytes = Data()
        for component in components {
            let encoded = Data(component.utf8)
            var length = UInt64(encoded.count).bigEndian
            withUnsafeBytes(of: &length) { bytes.append(contentsOf: $0) }
            bytes.append(encoded)
        }
        return SHA256.hash(data: bytes).map { String(format: "%02x", $0) }.joined()
    }
}
