import CryptoKit
import Foundation

public enum SessionEvidenceDecision: String, Codable, Sendable, Equatable {
    case supported
    case unsupported
}

/// A person's explicit review, scoped to the exact claim and transcript revision they saw.
public struct SessionEvidenceReview: Codable, Sendable, Equatable {
    public let decision: SessionEvidenceDecision
    public let claimRevision: String
    public let transcriptSourceRevision: String
    public let lineSourceRevision: String
    public let reviewedAt: Date

    public init(decision: SessionEvidenceDecision, claimRevision: String, transcriptSourceRevision: String,
                lineSourceRevision: String, reviewedAt: Date = Date()) {
        self.decision = decision
        self.claimRevision = claimRevision
        self.transcriptSourceRevision = transcriptSourceRevision
        self.lineSourceRevision = lineSourceRevision
        self.reviewedAt = reviewedAt
    }
}

public enum SessionEvidenceSupport: String, Sendable, Equatable {
    /// The supplied quotation appears verbatim in a saved transcript version.
    case quoted
    /// A timestamp points to nearby speech, but no matching quotation was supplied.
    case timeOnly
    /// A quotation was supplied, but it could not be matched to the transcript.
    case quoteNotFound
    /// There is no usable quotation or timestamp to locate in the transcript.
    case unlocated

    public var label: String {
        switch self {
        case .quoted: String(localized: "引文在所选转写版本中找到")
        case .timeOnly: String(localized: "仅关联时间 · 内容待核对")
        case .quoteNotFound: String(localized: "未找到所引原话 · 内容待核对")
        case .unlocated: String(localized: "没有原话定位 · 内容待核对")
        }
    }
}

public enum SessionEvidenceOrigin: String, Sendable, Equatable {
    case textNotes
    case structuredNotes
}

/// Which saved transcript text actually matched a note's quotation.
public enum SessionEvidenceSourceVariant: String, Sendable, Equatable {
    case original
    case corrected
}

private func structuredPayloadRevision(_ payload: StructuredNotesPayload) -> String? {
    let encoder = JSONEncoder()
    encoder.outputFormatting = [.sortedKeys]
    guard let data = try? encoder.encode(payload) else { return nil }
    return SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
}

/// Stable locator for one editable text node in a generated structured-notes payload.
public struct SessionStructuredNotesTarget: Sendable, Equatable {
    public let format: NotesFormat
    public let path: String
    /// Prevents an old index path from editing a different node after regeneration.
    public let payloadRevision: String

    public init(format: NotesFormat, path: String, payloadRevision: String) {
        self.format = format
        self.path = path
        self.payloadRevision = payloadRevision
    }
}

/// A saved note or generated note node paired with the strongest transcript link available.
/// `support` describes the source match only; it never means that a claim is confirmed.
public struct SessionEvidenceReference: Sendable, Equatable, Identifiable {
    public let id: String
    public let title: String
    public let claim: String
    public let quote: String?
    public let location: String?
    public let lineID: UUID?
    public let sourceRevision: String?
    public let sourceVariant: SessionEvidenceSourceVariant?
    public let sourceText: String?
    public let support: SessionEvidenceSupport
    public let needsConfirmation: Bool
    public let origin: SessionEvidenceOrigin
    public let notesItemID: UUID?
    public let structuredTarget: SessionStructuredNotesTarget?
    public let kind: NotesSectionKind?
    public let owner: String?
    public let due: String?
    public let dueDate: Date?
    public let claimRevision: String?
    public let transcriptSourceRevision: String
    public let sourceStatus: SessionDerivedContentStatus
    public let reviewDecision: SessionEvidenceDecision?
    public let reviewIsStale: Bool

    public init(id: String, title: String, claim: String, quote: String? = nil, location: String? = nil,
                lineID: UUID? = nil, sourceRevision: String? = nil,
                sourceVariant: SessionEvidenceSourceVariant? = nil, sourceText: String? = nil,
                support: SessionEvidenceSupport, needsConfirmation: Bool = false,
                origin: SessionEvidenceOrigin, notesItemID: UUID? = nil,
                structuredTarget: SessionStructuredNotesTarget? = nil,
                kind: NotesSectionKind? = nil, owner: String? = nil, due: String? = nil,
                dueDate: Date? = nil, claimRevision: String? = nil,
                transcriptSourceRevision: String = "", sourceStatus: SessionDerivedContentStatus = .unknown,
                reviewDecision: SessionEvidenceDecision? = nil, reviewIsStale: Bool = false) {
        self.id = id
        self.title = title
        self.claim = claim
        self.quote = quote
        self.location = location
        self.lineID = lineID
        self.sourceRevision = sourceRevision
        self.sourceVariant = sourceVariant
        self.sourceText = sourceText
        self.support = support
        self.needsConfirmation = needsConfirmation
        self.origin = origin
        self.notesItemID = notesItemID
        self.structuredTarget = structuredTarget
        self.kind = kind
        self.owner = owner
        self.due = due
        self.dueDate = dueDate
        self.claimRevision = claimRevision
        self.transcriptSourceRevision = transcriptSourceRevision
        self.sourceStatus = sourceStatus
        self.reviewDecision = reviewDecision
        self.reviewIsStale = reviewIsStale
    }
}

public enum SessionEvidenceBuilder {
    /// Builds references for standard notes and every saved structured notes view.
    /// Time proximity is deliberately reported as a navigation hint, never as claim support.
    public static func build(meta: SessionMeta, lines: [SessionTranscriptLine], notes: StructuredNotes?,
                             summary: SessionSummary?) -> [SessionEvidenceReference] {
        let projection = SessionProjectionBuilder.build(meta: meta, lines: lines, summary: summary, notes: notes)
        let transcript = projection.transcript
        let currentTranscriptRevision = projection.transcriptSourceRevision
        var result: [SessionEvidenceReference] = []

        func makeReference(id: String, title: String, claim: String, quote: String? = nil,
                           seconds: Int? = nil, needsConfirmation: Bool = false,
                           origin: SessionEvidenceOrigin, notesItemID: UUID? = nil,
                           structuredTarget: SessionStructuredNotesTarget? = nil,
                           kind: NotesSectionKind? = nil, owner: String? = nil, due: String? = nil,
                           dueDate: Date? = nil, sourceStatus: SessionDerivedContentStatus) -> SessionEvidenceReference {
            let cleanQuote = quote?.trimmingCharacters(in: .whitespacesAndNewlines)
            var matchedLine: SessionProjectedTranscriptLine?
            var sourceVariant: SessionEvidenceSourceVariant?
            var matchedText: String?
            var matchedRevision: String?
            var support: SessionEvidenceSupport
            var location: String?

            if let cleanQuote, !cleanQuote.isEmpty {
                let comparisonOptions: String.CompareOptions = [.caseInsensitive, .widthInsensitive]
                // Each line contributes one match, preferring its corrected text. A timestamp
                // then chooses the nearest occurrence across both saved transcript versions.
                let matches = transcript.compactMap { line -> (line: SessionProjectedTranscriptLine,
                                                               variant: SessionEvidenceSourceVariant,
                                                               text: String, revision: String)? in
                    if line.correctedText.range(of: cleanQuote, options: comparisonOptions) != nil {
                        return (line, .corrected, line.correctedText, line.sourceRevision)
                    }
                    if line.originalText.range(of: cleanQuote, options: comparisonOptions) != nil {
                        return (line, .original, line.originalText, line.originalRevision)
                    }
                    return nil
                }
                let matched = closest(matches, to: seconds)
                if let matched {
                    matchedLine = matched.line
                    sourceVariant = matched.variant
                    matchedText = matched.text
                    matchedRevision = matched.revision
                    location = NotesTime.text(matched.line.offset)
                    support = .quoted
                } else {
                    support = .quoteNotFound
                }
            } else {
                support = .unlocated
            }

            if matchedLine == nil, let target = seconds {
                matchedLine = transcript.min { lhs, rhs in abs(lhs.offset - target) < abs(rhs.offset - target) }
                if let closest = matchedLine, abs(closest.offset - target) > 45 { matchedLine = nil }
                if let nearest = matchedLine {
                    sourceVariant = .corrected
                    matchedText = nearest.correctedText
                    matchedRevision = nearest.sourceRevision
                    if support != .quoteNotFound { support = .timeOnly }
                }
            }
            if matchedLine == nil, support == .unlocated { support = .unlocated }
            location = location ?? seconds.map(NotesTime.text)

            let claimRevision = claimFingerprint(id: id, title: title, claim: claim, quote: cleanQuote,
                                                 location: location, kind: kind, owner: owner,
                                                 due: due, dueDate: dueDate)
            let savedReview = summary?.evidenceReviews?[id]
            let reviewIsCurrent = savedReview.map {
                $0.claimRevision == claimRevision
                    && $0.transcriptSourceRevision == currentTranscriptRevision
                    && $0.lineSourceRevision == matchedRevision
            } ?? false
            let reviewIsStale = savedReview != nil && !reviewIsCurrent

            return SessionEvidenceReference(id: id, title: title, claim: claim, quote: cleanQuote,
                                            location: location, lineID: matchedLine?.id,
                                            sourceRevision: matchedRevision, sourceVariant: sourceVariant,
                                            sourceText: matchedText, support: support,
                                            needsConfirmation: needsConfirmation || support != .quoted, origin: origin,
                                            notesItemID: notesItemID, structuredTarget: structuredTarget,
                                            kind: kind, owner: owner, due: due,
                                            dueDate: dueDate, claimRevision: claimRevision,
                                            transcriptSourceRevision: currentTranscriptRevision,
                                            sourceStatus: sourceStatus,
                                            reviewDecision: reviewIsCurrent ? savedReview?.decision : nil,
                                            reviewIsStale: reviewIsStale)
        }

        let notesStatus = status(savedRevision: notes?.transcriptSourceRevision,
                                 currentRevision: currentTranscriptRevision)
        for section in notes?.sections ?? [] {
            for item in section.items {
                result.append(makeReference(id: "notes:\(section.id):\(item.id.uuidString)", title: section.title,
                                            claim: item.text, quote: item.quote,
                                            seconds: item.time.flatMap(NotesTime.seconds),
                                            needsConfirmation: item.uncertain, origin: .textNotes,
                                            notesItemID: item.id, kind: section.kind, owner: item.owner,
                                            due: item.due, dueDate: item.dueDate, sourceStatus: notesStatus))
            }
        }

        var payloads = summary?.structuredAll ?? []
        if let latest = summary?.structured, !payloads.contains(where: { $0.format == latest.format }) {
            payloads.append(latest)
        }
        let referenceFactory: (String, String, String, Int?, SessionDerivedContentStatus, SessionStructuredNotesTarget?) -> SessionEvidenceReference = {
            id, title, claim, seconds, sourceStatus, structuredTarget in
            makeReference(id: id, title: title, claim: claim, seconds: seconds,
                          origin: .structuredNotes, structuredTarget: structuredTarget,
                          sourceStatus: sourceStatus)
        }
        for payload in payloads where payload.hasContent {
            let payloadStatus = status(savedRevision: payload.transcriptSourceRevision,
                                       currentRevision: currentTranscriptRevision)
            appendStructured(payload, sourceStatus: payloadStatus,
                             makeReference: referenceFactory, into: &result)
        }
        return result
    }

    private static func status(savedRevision: String?, currentRevision: String) -> SessionDerivedContentStatus {
        guard let savedRevision else { return .unknown }
        return savedRevision == currentRevision ? .current : .stale
    }

    private static func closest(_ matches: [(line: SessionProjectedTranscriptLine,
                                             variant: SessionEvidenceSourceVariant,
                                             text: String, revision: String)],
                                to seconds: Int?) -> (line: SessionProjectedTranscriptLine,
                                                       variant: SessionEvidenceSourceVariant,
                                                       text: String, revision: String)? {
        guard let seconds else { return matches.first }
        return matches.min { lhs, rhs in
            let leftDistance = abs(lhs.line.offset - seconds)
            let rightDistance = abs(rhs.line.offset - seconds)
            if leftDistance != rightDistance { return leftDistance < rightDistance }
            if lhs.variant != rhs.variant { return lhs.variant == .corrected }
            return lhs.line.offset < rhs.line.offset
        }
    }

    private static func claimFingerprint(id: String, title: String, claim: String, quote: String?,
                                         location: String?, kind: NotesSectionKind?, owner: String?, due: String?,
                                         dueDate: Date?) -> String {
        let parts = [id, title, claim, quote ?? "", location ?? "", kind?.rawValue ?? "",
                     owner ?? "", due ?? "", dueDate.map { String($0.timeIntervalSince1970.bitPattern) } ?? ""]
        var bytes = Data()
        for part in parts {
            let encoded = Data(part.utf8)
            var length = UInt64(encoded.count).bigEndian
            withUnsafeBytes(of: &length) { bytes.append(contentsOf: $0) }
            bytes.append(encoded)
        }
        return SHA256.hash(data: bytes).map { String(format: "%02x", $0) }.joined()
    }

    private static func appendStructured(
        _ payload: StructuredNotesPayload,
        sourceStatus: SessionDerivedContentStatus,
        makeReference: (String, String, String, Int?, SessionDerivedContentStatus, SessionStructuredNotesTarget?) -> SessionEvidenceReference,
        into result: inout [SessionEvidenceReference]
    ) {
        let payloadRevision = structuredPayloadRevision(payload)
        func add(_ path: String, _ title: String, _ text: String?, at seconds: Int?) {
            guard let text, !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                  let payloadRevision else { return }
            let target = SessionStructuredNotesTarget(format: payload.format, path: path,
                                                      payloadRevision: payloadRevision)
            result.append(makeReference("structured:\(payload.format.rawValue):\(path)", title, text,
                                        seconds, sourceStatus, target))
        }
        switch payload.format {
        case .standard: break
        case .fishbone:
            guard let notes = payload.fishbone else { return }
            add("head", "议题", notes.head, at: notes.headAt)
            for (categoryIndex, category) in notes.categories.enumerated() {
                for (causeIndex, cause) in category.causes.enumerated() {
                    add("category:\(categoryIndex):cause:\(causeIndex)", category.name, cause.text, at: cause.at)
                }
            }
        case .outline:
            guard let notes = payload.outline else { return }
            for (sectionIndex, section) in notes.sections.enumerated() {
                add("section:\(sectionIndex)", "章节", section.title, at: section.at)
                for (pointIndex, point) in section.points.enumerated() {
                    let path = "section:\(sectionIndex):point:\(pointIndex)"
                    add(path, section.title, point.text, at: point.at)
                    for (childIndex, child) in point.children.enumerated() {
                        add("\(path):child:\(childIndex)", section.title, child.text, at: child.at)
                    }
                }
            }
            for (index, concept) in notes.concepts.enumerated() {
                add("concept:\(index):term", "关键概念", concept.term, at: concept.at)
                add("concept:\(index):meaning", "关键概念", concept.meaning, at: concept.at)
            }
            for (index, item) in notes.review.enumerated() { add("review:\(index)", "待复习", item.text, at: item.at) }
        case .viewpoints:
            guard let notes = payload.viewpoints else { return }
            add("topic", "议题", notes.topic, at: notes.topicAt)
            for (issueIndex, issue) in notes.issues.enumerated() {
                for (positionIndex, position) in issue.positions.enumerated() {
                    add("issue:\(issueIndex):position:\(positionIndex):party", issue.title,
                        position.party, at: position.at)
                    add("issue:\(issueIndex):position:\(positionIndex):text", issue.title,
                        position.text, at: position.at)
                }
            }
            for (index, item) in notes.consensus.enumerated() { add("consensus:\(index)", "共识", item.text, at: item.at) }
            for (index, item) in notes.disagreements.enumerated() { add("disagreement:\(index)", "分歧", item.text, at: item.at) }
            for (index, owner) in notes.owners.enumerated() {
                add("owner:\(index):who", "分工", owner.who, at: owner.at)
                add("owner:\(index):task", "分工", owner.task, at: owner.at)
            }
        }
    }
}

/// Applies a user edit to one generated structured-note node without replacing other views.
public enum SessionEvidenceEditor {
    @discardableResult
    public static func updateStructuredNote(in summary: inout SessionSummary,
                                            target: SessionStructuredNotesTarget,
                                            text: String) -> Bool {
        guard var payload = summary.payload(for: target.format),
              structuredPayloadRevision(payload) == target.payloadRevision,
              update(&payload, path: target.path, text: text) else { return false }
        summary.store(payload)
        return true
    }

    private static func update(_ payload: inout StructuredNotesPayload, path: String, text: String) -> Bool {
        let parts = path.split(separator: ":").map(String.init)
        switch payload.format {
        case .standard:
            return false
        case .fishbone:
            guard var notes = payload.fishbone else { return false }
            if parts == ["head"] {
                notes.head = text
            } else if parts.count == 4, parts[0] == "category", let category = Int(parts[1]),
                      parts[2] == "cause", let cause = Int(parts[3]),
                      notes.categories.indices.contains(category), notes.categories[category].causes.indices.contains(cause) {
                notes.categories[category].causes[cause].text = text
            } else {
                return false
            }
            payload.fishbone = notes
            return true
        case .outline:
            guard var notes = payload.outline else { return false }
            if parts.count == 2, parts[0] == "section", let section = Int(parts[1]), notes.sections.indices.contains(section) {
                notes.sections[section].title = text
            } else if parts.count == 4, parts[0] == "section", let section = Int(parts[1]),
                      parts[2] == "point", let point = Int(parts[3]), notes.sections.indices.contains(section),
                      notes.sections[section].points.indices.contains(point) {
                notes.sections[section].points[point].text = text
            } else if parts.count == 6, parts[0] == "section", let section = Int(parts[1]),
                      parts[2] == "point", let point = Int(parts[3]), parts[4] == "child", let child = Int(parts[5]),
                      notes.sections.indices.contains(section), notes.sections[section].points.indices.contains(point),
                      notes.sections[section].points[point].children.indices.contains(child) {
                notes.sections[section].points[point].children[child].text = text
            } else if parts.count == 3, parts[0] == "concept", let index = Int(parts[1]), notes.concepts.indices.contains(index) {
                switch parts[2] {
                case "term": notes.concepts[index].term = text
                case "meaning": notes.concepts[index].meaning = text
                default: return false
                }
            } else if parts.count == 2, parts[0] == "review", let index = Int(parts[1]), notes.review.indices.contains(index) {
                notes.review[index].text = text
            } else {
                return false
            }
            payload.outline = notes
            return true
        case .viewpoints:
            guard var notes = payload.viewpoints else { return false }
            if parts == ["topic"] {
                notes.topic = text
            } else if parts.count == 5, parts[0] == "issue", let issue = Int(parts[1]),
                      parts[2] == "position", let position = Int(parts[3]), notes.issues.indices.contains(issue),
                      notes.issues[issue].positions.indices.contains(position) {
                switch parts[4] {
                case "party": notes.issues[issue].positions[position].party = text
                case "text": notes.issues[issue].positions[position].text = text
                default: return false
                }
            } else if parts.count == 2, parts[0] == "consensus", let index = Int(parts[1]), notes.consensus.indices.contains(index) {
                notes.consensus[index].text = text
            } else if parts.count == 2, parts[0] == "disagreement", let index = Int(parts[1]), notes.disagreements.indices.contains(index) {
                notes.disagreements[index].text = text
            } else if parts.count == 3, parts[0] == "owner", let index = Int(parts[1]), notes.owners.indices.contains(index) {
                switch parts[2] {
                case "who": notes.owners[index].who = text
                case "task": notes.owners[index].task = text
                default: return false
                }
            } else {
                return false
            }
            payload.viewpoints = notes
            return true
        }
    }
}
