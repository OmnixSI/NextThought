import Foundation

/// The kind of archived text where a history search found a match.
public enum SessionSearchFieldKind: String, Codable, Sendable, Equatable {
    case transcript
    case correctedTranscript
    case translation
    case question
    case answer
    case notes
}

/// One searchable piece of a meeting. `location` is a meeting-relative time when one is
/// available, or nil for text such as a whole-meeting summary.
public struct SessionSearchField: Sendable, Equatable {
    public let kind: SessionSearchFieldKind
    public let text: String
    public let alternateText: String?
    public let location: String?
    public let id: String?
    public let lineID: UUID?
    public let sourceRevision: String?
    public let isStale: Bool?

    public init(kind: SessionSearchFieldKind, text: String, location: String? = nil, alternateText: String? = nil,
                id: String? = nil, lineID: UUID? = nil, sourceRevision: String? = nil, isStale: Bool? = nil) {
        self.kind = kind
        self.text = text
        self.alternateText = alternateText
        self.location = location
        self.id = id
        self.lineID = lineID
        self.sourceRevision = sourceRevision
        self.isStale = isStale
    }
}

/// A compact, actionable result suitable for showing beside a meeting in the history list.
public struct SessionSearchHit: Sendable, Equatable {
    public let kind: SessionSearchFieldKind
    public let excerpt: String
    public let location: String?
    public let matchedForm: String
    public let isMisheard: Bool
    public let id: String?
    public let lineID: UUID?
    public let sourceRevision: String?
    public let isStale: Bool?

    public init(kind: SessionSearchFieldKind, excerpt: String, location: String?, matchedForm: String, isMisheard: Bool,
                id: String? = nil, lineID: UUID? = nil, sourceRevision: String? = nil, isStale: Bool? = nil) {
        self.kind = kind
        self.excerpt = excerpt
        self.location = location
        self.matchedForm = matchedForm
        self.isMisheard = isMisheard
        self.id = id
        self.lineID = lineID
        self.sourceRevision = sourceRevision
        self.isStale = isStale
    }
}

public struct SessionSearchDocument: Sendable, Equatable {
    public let sessionID: UUID
    public let fields: [SessionSearchField]

    public init(sessionID: UUID, fields: [SessionSearchField]) {
        self.sessionID = sessionID
        self.fields = fields
    }
}

public struct SessionSearchResult: Sendable, Equatable {
    public let sessionID: UUID
    public let hit: SessionSearchHit

    public init(sessionID: UUID, hit: SessionSearchHit) {
        self.sessionID = sessionID
        self.hit = hit
    }
}

/// The archive portion of a history-search task identity. The active meeting is excluded because
/// its counts and transcript change while recording; it becomes searchable when it is finalized.
public struct SessionSearchArchiveStamp: Sendable, Equatable {
    private let searchableContentRevision: Int
    private let archivedSessions: [SessionMeta]

    public init(sessions: [SessionMeta], activeSessionID: UUID?,
                pendingTranslationSessionIDs: Set<UUID> = [], searchableContentRevision: Int) {
        self.searchableContentRevision = searchableContentRevision
        self.archivedSessions = sessions.filter {
            $0.id != activeSessionID && !pendingTranslationSessionIDs.contains($0.id)
        }
    }
}

/// Stateless lexical matching for archived meeting text. It deliberately does not impose a
/// corpus-size limit; the caller can load and present documents in cancellable batches.
public enum SessionContentSearch {
    /// Builds searchable text from the same saved sources the meeting page reads. Original and
    /// corrected transcript text stay independently searchable so the correction never hides
    /// what speech recognition originally wrote.
    public static func fields(meta: SessionMeta, lines: [SessionTranscriptLine], questions: [SessionQuestionEvent],
                              answers: [SessionAnswerEvent], summary: SessionSummary?, notes: StructuredNotes?) -> [SessionSearchField] {
        var fields: [SessionSearchField] = []
        func append(_ kind: SessionSearchFieldKind, _ text: String?, at location: String? = nil) {
            guard let text, !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }
            fields.append(SessionSearchField(kind: kind, text: text, location: location))
        }

        let projection = SessionProjectionBuilder.build(meta: meta, lines: lines, answers: answers, detected: questions,
                                                        summary: summary, notes: notes)
        for item in projection.searchableText where item.lineID != nil {
            let kind: SessionSearchFieldKind
            switch item.kind {
            case .transcript: kind = .transcript
            case .correctedTranscript: kind = .correctedTranscript
            case .translation: kind = .translation
            case .question: kind = .question
            case .answer: kind = .answer
            case .summary, .notes: kind = .notes
            }
            fields.append(SessionSearchField(kind: kind, text: item.text, location: item.location,
                                             alternateText: item.alternateText, id: item.id, lineID: item.lineID,
                                             sourceRevision: item.sourceRevision, isStale: item.isStale))
        }
        if let notes {
            append(.notes, notes.title)
            for section in notes.sections {
                append(.notes, section.title)
                for item in section.items {
                    let location = item.time.flatMap { NotesTime.seconds($0) }.map(NotesTime.text)
                    append(.notes, item.text, at: location)
                    append(.notes, item.owner, at: location)
                    append(.notes, item.due, at: location)
                    append(.notes, item.answer, at: location)
                    append(.notes, item.verdict, at: location)
                    append(.notes, item.better, at: location)
                    append(.notes, item.quote, at: location)
                }
            }
        }
        for question in questions {
            let location = NotesTime.text(max(0, Int(question.date.timeIntervalSince(meta.startedAt))))
            append(.question, question.text, at: location)
            append(.question, question.leadIn, at: location)
        }
        for answer in answers {
            let location = NotesTime.text(max(0, Int(answer.date.timeIntervalSince(meta.startedAt))))
            append(.question, answer.question, at: location)
            append(.answer, answer.answer, at: location)
            append(.answer, answer.webAnswer, at: location)
        }
        if let summary {
            append(.notes, summary.title)
            append(.notes, summary.markdown)
            for question in summary.questions {
                append(.question, question.text)
                append(.answer, question.answerGist)
            }
            for lesson in summary.lessons ?? [] { append(.notes, lesson) }
            for entry in summary.bank ?? [] {
                append(.question, entry.question)
                append(.answer, entry.answer)
            }
            var structured = summary.structuredAll ?? []
            if let latest = summary.structured, !structured.contains(where: { $0.format == latest.format }) {
                structured.append(latest)
            }
            for payload in structured { appendStructured(payload, append: append) }
        }
        return fields
    }

    public static func firstHit(in fields: [SessionSearchField], terms: GlossarySearch) -> SessionSearchHit? {
        for field in fields {
            let matchedText: String
            let match: GlossarySearchHit
            if let directMatch = terms.firstMatch(in: field.text) {
                matchedText = field.text
                match = directMatch
            } else if let alternateText = field.alternateText, let alternateMatch = terms.firstMatch(in: alternateText) {
                matchedText = alternateText
                match = alternateMatch
            } else {
                continue
            }
            let range = matchedText.range(of: match.form, options: [.caseInsensitive, .widthInsensitive])
            return SessionSearchHit(kind: field.kind,
                                    excerpt: excerpt(matchedText, around: range),
                                    location: field.location,
                                    matchedForm: match.form,
                                    isMisheard: match.isMisheard,
                                    id: field.id,
                                    lineID: field.lineID,
                                    sourceRevision: field.sourceRevision,
                                    isStale: field.isStale)
        }
        return nil
    }

    /// Searches every supplied document and returns one first hit per meeting, preserving the
    /// archive order. This helper is also used to exercise large, synthetic history fixtures.
    public static func search(_ documents: [SessionSearchDocument], terms: GlossarySearch) -> [SessionSearchResult] {
        documents.compactMap { document in
            firstHit(in: document.fields, terms: terms).map { SessionSearchResult(sessionID: document.sessionID, hit: $0) }
        }
    }

    private static func excerpt(_ text: String, around range: Range<String.Index>?) -> String {
        guard let range else { return String(text.prefix(116)) + (text.count > 116 ? "…" : "") }
        let lower = text.index(range.lowerBound, offsetBy: -32, limitedBy: text.startIndex) ?? text.startIndex
        let upper = text.index(range.upperBound, offsetBy: 64, limitedBy: text.endIndex) ?? text.endIndex
        let before = lower == text.startIndex ? "" : "…"
        let after = upper == text.endIndex ? "" : "…"
        let context = Range<String.Index>(uncheckedBounds: (lower, upper))
        return before + String(text[context]) + after
    }

    private static func appendStructured(_ payload: StructuredNotesPayload,
                                         append: (SessionSearchFieldKind, String?, String?) -> Void) {
        switch payload.format {
        case .standard: break
        case .fishbone:
            guard let notes = payload.fishbone else { return }
            append(.notes, notes.head, notes.headAt.map(NotesTime.text))
            for category in notes.categories {
                append(.notes, category.name, nil)
                for cause in category.causes { append(.notes, cause.text, cause.at.map(NotesTime.text)) }
            }
        case .outline:
            guard let notes = payload.outline else { return }
            for section in notes.sections {
                append(.notes, section.title, section.at.map(NotesTime.text))
                for point in section.points {
                    append(.notes, point.text, point.at.map(NotesTime.text))
                    for child in point.children { append(.notes, child.text, child.at.map(NotesTime.text)) }
                }
            }
            for concept in notes.concepts {
                append(.notes, concept.term, concept.at.map(NotesTime.text))
                append(.notes, concept.meaning, concept.at.map(NotesTime.text))
            }
            for item in notes.review { append(.notes, item.text, item.at.map(NotesTime.text)) }
        case .viewpoints:
            guard let notes = payload.viewpoints else { return }
            append(.notes, notes.topic, notes.topicAt.map(NotesTime.text))
            for issue in notes.issues {
                append(.notes, issue.title, nil)
                for position in issue.positions {
                    append(.notes, position.party + "：" + position.text, position.at.map(NotesTime.text))
                }
            }
            for item in notes.consensus { append(.notes, item.text, item.at.map(NotesTime.text)) }
            for item in notes.disagreements { append(.notes, item.text, item.at.map(NotesTime.text)) }
            for owner in notes.owners {
                append(.notes, [owner.who, owner.task, owner.due].compactMap { $0 }.joined(separator: " · "), owner.at.map(NotesTime.text))
            }
        }
    }
}
