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
    public let sourceStatus: SessionDerivedContentStatus?
    public let alternateID: String?
    public let alternateKind: SessionSearchFieldKind?
    public let alternateLineID: UUID?
    public let alternateSourceRevision: String?

    public init(kind: SessionSearchFieldKind, text: String, location: String? = nil, alternateText: String? = nil,
                id: String? = nil, lineID: UUID? = nil, sourceRevision: String? = nil, isStale: Bool? = nil,
                sourceStatus: SessionDerivedContentStatus? = nil, alternateID: String? = nil,
                alternateKind: SessionSearchFieldKind? = nil, alternateLineID: UUID? = nil,
                alternateSourceRevision: String? = nil) {
        self.kind = kind
        self.text = text
        self.alternateText = alternateText
        self.location = location
        self.id = id
        self.lineID = lineID
        self.sourceRevision = sourceRevision
        self.isStale = isStale
        self.sourceStatus = sourceStatus
        self.alternateID = alternateID
        self.alternateKind = alternateKind
        self.alternateLineID = alternateLineID
        self.alternateSourceRevision = alternateSourceRevision
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
    public let sourceStatus: SessionDerivedContentStatus?

    public init(kind: SessionSearchFieldKind, excerpt: String, location: String?, matchedForm: String, isMisheard: Bool,
                id: String? = nil, lineID: UUID? = nil, sourceRevision: String? = nil, isStale: Bool? = nil,
                sourceStatus: SessionDerivedContentStatus? = nil) {
        self.kind = kind
        self.excerpt = excerpt
        self.location = location
        self.matchedForm = matchedForm
        self.isMisheard = isMisheard
        self.id = id
        self.lineID = lineID
        self.sourceRevision = sourceRevision
        self.isStale = isStale
        self.sourceStatus = sourceStatus
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
        let projection = SessionProjectionBuilder.build(meta: meta, lines: lines, answers: answers, detected: questions,
                                                        summary: summary, notes: notes)
        let structuredPayloads = structuredPayloads(in: summary)
        let structuredIDs = Set(structuredPayloads.map { "summary:\(meta.id.uuidString):structured:\($0.format.rawValue)" })
        for item in projection.searchableText where !structuredIDs.contains(item.id) {
            let kind: SessionSearchFieldKind
            switch item.kind {
            case .transcript: kind = .transcript
            case .correctedTranscript: kind = .correctedTranscript
            case .translation: kind = .translation
            case .question: kind = .question
            case .answer: kind = .answer
            case .summary, .notes: kind = .notes
            }
            let lineID = item.lineID ?? nearestTranscriptLineID(at: item.location, in: projection.transcript)
            fields.append(SessionSearchField(kind: kind, text: item.text, location: item.location,
                                             alternateText: item.alternateText, id: item.id, lineID: lineID,
                                             sourceRevision: item.sourceRevision, isStale: item.isStale,
                                             sourceStatus: item.sourceStatus,
                                             alternateID: item.kind == .correctedTranscript
                                                ? "line:\(item.lineID?.uuidString ?? ""):original" : nil,
                                             alternateKind: item.kind == .correctedTranscript ? .transcript : nil,
                                             alternateLineID: item.kind == .correctedTranscript ? item.lineID : nil,
                                             alternateSourceRevision: item.kind == .correctedTranscript
                                                ? projection.transcript.first(where: { $0.id == item.lineID })?.originalRevision : nil))
        }
        for payload in structuredPayloads where payload.hasContent {
            let status = payload.transcriptSourceRevision.map {
                $0 == projection.transcriptSourceRevision ? SessionDerivedContentStatus.current : .stale
            } ?? .unknown
            fields.append(contentsOf: structuredFields(payload, sourceStatus: status,
                                                       transcript: projection.transcript))
        }
        return fields
    }

    public static func firstHit(in fields: [SessionSearchField], terms: GlossarySearch) -> SessionSearchHit? {
        hits(in: fields, terms: terms).first
    }

    /// Returns one hit for every matching source field, in document order. The history UI uses
    /// this list for a per-meeting result counter and next/previous navigation.
    public static func hits(in fields: [SessionSearchField], terms: GlossarySearch) -> [SessionSearchHit] {
        var seenIDs = Set<String>()
        return fields.compactMap { field in
            let matchedText: String
            let match: GlossarySearchHit
            let usesAlternate: Bool
            if let directMatch = terms.firstMatch(in: field.text) {
                matchedText = field.text
                match = directMatch
                usesAlternate = false
            } else if let alternateText = field.alternateText, let alternateMatch = terms.firstMatch(in: alternateText) {
                matchedText = alternateText
                match = alternateMatch
                usesAlternate = true
            } else {
                return nil
            }
            let range = matchedText.range(of: match.form, options: [.caseInsensitive, .widthInsensitive])
            let hit = SessionSearchHit(kind: usesAlternate ? (field.alternateKind ?? field.kind) : field.kind,
                                       excerpt: excerpt(matchedText, around: range),
                                       location: field.location,
                                       matchedForm: match.form,
                                       isMisheard: match.isMisheard,
                                       id: usesAlternate ? (field.alternateID ?? field.id) : field.id,
                                       lineID: usesAlternate ? (field.alternateLineID ?? field.lineID) : field.lineID,
                                       sourceRevision: usesAlternate ? (field.alternateSourceRevision ?? field.sourceRevision) : field.sourceRevision,
                                       isStale: usesAlternate ? false : field.isStale,
                                       sourceStatus: usesAlternate ? nil : field.sourceStatus)
            guard let id = hit.id else { return hit }
            return seenIDs.insert(id).inserted ? hit : nil
        }
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

    private static func structuredPayloads(in summary: SessionSummary?) -> [StructuredNotesPayload] {
        guard let summary else { return [] }
        var payloads = summary.structuredAll ?? []
        if let latest = summary.structured, !payloads.contains(where: { $0.format == latest.format }) {
            payloads.append(latest)
        }
        return payloads
    }

    /// Keep precise timestamps for structured formats while carrying the same freshness
    /// contract as the projection's combined Markdown field.
    private static func structuredFields(_ payload: StructuredNotesPayload,
                                         sourceStatus: SessionDerivedContentStatus,
                                         transcript: [SessionProjectedTranscriptLine]) -> [SessionSearchField] {
        var fields: [SessionSearchField] = []
        var ordinal = 0
        func append(_ kind: SessionSearchFieldKind = .notes, _ text: String?, at location: String? = nil) {
            guard let text, !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }
            ordinal += 1
            fields.append(SessionSearchField(kind: kind, text: text, location: location,
                                             id: "summary:structured:\(payload.format.rawValue):\(ordinal)",
                                             lineID: nearestTranscriptLineID(at: location, in: transcript),
                                             sourceRevision: payload.transcriptSourceRevision,
                                             isStale: sourceStatus == .stale, sourceStatus: sourceStatus))
        }
        switch payload.format {
        case .standard:
            break
        case .fishbone:
            guard let notes = payload.fishbone else { return [] }
            append(.notes, notes.head, at: notes.headAt.map(NotesTime.text))
            for category in notes.categories {
                append(.notes, category.name)
                for cause in category.causes { append(.notes, cause.text, at: cause.at.map(NotesTime.text)) }
            }
        case .outline:
            guard let notes = payload.outline else { return [] }
            for section in notes.sections {
                append(.notes, section.title, at: section.at.map(NotesTime.text))
                for point in section.points {
                    append(.notes, point.text, at: point.at.map(NotesTime.text))
                    for child in point.children { append(.notes, child.text, at: child.at.map(NotesTime.text)) }
                }
            }
            for concept in notes.concepts {
                append(.notes, concept.term, at: concept.at.map(NotesTime.text))
                append(.notes, concept.meaning, at: concept.at.map(NotesTime.text))
            }
            for item in notes.review { append(.notes, item.text, at: item.at.map(NotesTime.text)) }
        case .viewpoints:
            guard let notes = payload.viewpoints else { return [] }
            append(.notes, notes.topic, at: notes.topicAt.map(NotesTime.text))
            for issue in notes.issues {
                append(.notes, issue.title)
                for position in issue.positions {
                    append(.notes, position.party + "：" + position.text, at: position.at.map(NotesTime.text))
                }
            }
            for item in notes.consensus { append(.notes, item.text, at: item.at.map(NotesTime.text)) }
            for item in notes.disagreements { append(.notes, item.text, at: item.at.map(NotesTime.text)) }
            for owner in notes.owners {
                append(.notes, [owner.who, owner.task, owner.due].compactMap { $0 }.joined(separator: " · "),
                       at: owner.at.map(NotesTime.text))
            }
        }
        return fields
    }

    private static func nearestTranscriptLineID(at location: String?,
                                                in transcript: [SessionProjectedTranscriptLine]) -> UUID? {
        guard let seconds = location.flatMap(NotesTime.seconds), !transcript.isEmpty,
              let nearest = transcript.min(by: { abs($0.offset - seconds) < abs($1.offset - seconds) }),
              abs(nearest.offset - seconds) <= 45 else { return nil }
        return nearest.id
    }

}
