import Foundation
import NextThoughtCore

// A15: macOS fixture generator. Every file is built from the real NextThoughtCore
// types and written with the encoder the app uses (`JSONLines.encoder()`: ISO-8601
// dates, sorted keys, unescaped slashes). Every value is decoded back with
// `JSONLines.decoder()` and compared before it is written. Revision values come
// from the Swift functions the app calls; the replaced series item comes from
// `WorkspaceSeriesChangeMerger.applyCandidate(.replace)`. No app data is read, and
// nothing is built, installed or signed.

enum EmitError: Error, CustomStringConvertible {
    case roundTrip(String), missing(String)
    var description: String {
        switch self {
        case let .roundTrip(name): "\(name): decoding the written bytes gave a different value"
        case let .missing(what): "missing: \(what)"
        }
    }
}

let encoder = JSONLines.encoder()
let decoder = JSONLines.decoder()
/// Whole seconds so the ISO-8601 round trip is exact.
let base = Date(timeIntervalSince1970: 1_764_000_000)
func at(_ seconds: Int) -> Date { base.addingTimeInterval(TimeInterval(seconds)) }
func id(_ text: String) -> UUID { UUID(uuidString: text)! }

let seriesID = id("33333333-3333-4333-8333-333333333333")
let firstID = id("11111111-1111-4111-8111-111111111111")
let secondID = id("22222222-2222-4222-8222-222222222222")

struct Meeting {
    var meta: SessionMeta
    var lines: [SessionTranscriptLine]
    var notes: StructuredNotes
    var summary: SessionSummary
}

func meeting(id sessionID: UUID, start: Int, title: String, lines raw: [(UUID, Int, SessionSpeaker, Int?, String)],
             decision: String, quote: String, correction: (line: Int, original: String, corrected: String)?) -> Meeting {
    let lines = raw.map { SessionTranscriptLine(id: $0.0, date: at(start + $0.1), speaker: $0.2, text: $0.4,
                                                startedAt: at(start + $0.1 - 3), endedAt: at(start + $0.1), voice: $0.3) }
    var meta = SessionMeta(id: sessionID, startedAt: at(start), endedAt: at(start + 600), mode: .meeting, title: title,
                           audioSource: "system+mic", recognizer: "zipformer-xasr-480", answerModel: "fixture-model",
                           transcriptCount: lines.count, questionCount: 1, answerCount: 1, summaryState: .ready,
                           workspaceID: seriesID, workspaceName: "A15 交付系列")
    meta.voiceNames = ["1": "王老师"]
    meta.meVoices = []
    meta.actionItemCount = 1
    meta.improveCount = 0
    let template = NotesTemplate.builtIn(.work)
    let corrections = correction.map {
        [TranscriptCorrection(id: id("dddddddd-dddd-4ddd-8ddd-\(String(format: "%012d", start))"), line: $0.line,
                              lineID: lines[$0.line].id, original: $0.original, corrected: $0.corrected)]
    } ?? []
    var notes = StructuredNotes(templateID: template.id, templateName: template.name, language: "zh", title: title,
                                sections: [
                                    NotesSectionResult(id: "summary", title: "一句话", kind: .oneLine,
                                                       items: [NotesItem(id: id("eeeeeeee-eeee-4eee-8eee-\(String(format: "%012d", start + 1))"),
                                                                         text: "确认了 A15 提交时间。")]),
                                    NotesSectionResult(id: "decisions", title: "决定", kind: .items,
                                                       items: [NotesItem(id: id("eeeeeeee-eeee-4eee-8eee-\(String(format: "%012d", start + 2))"),
                                                                         text: decision, time: "00:20", quote: quote)])
                                ],
                                corrections: corrections, generatedAt: at(start + 660), model: "fixture-model")
    notes.transcriptSourceRevision = SessionProjectionBuilder.transcriptSourceRevision(meta: meta, lines: lines, notes: notes)
    let summary = SessionSummary(title: title, markdown: "# \(title)\n\n## 决定\n- \(decision)", questions: [
        SummaryQuestion(id: id("ffffffff-ffff-4fff-8fff-\(String(format: "%012d", start))"), text: "提交时间定了吗？",
                        answerGist: decision)
    ], generatedAt: at(start + 660), model: "fixture-model",
       transcriptSourceRevision: notes.transcriptSourceRevision,
       structuredNotesSourceRevision: SessionProjectionBuilder.structuredNotesRevision(notes))
    return Meeting(meta: meta, lines: lines, notes: notes, summary: summary)
}

/// The decision of a meeting as reviewed evidence: build references, mark the decision
/// supported, build again so the review is current.
func reviewedDecision(_ meeting: inout Meeting) throws -> SessionEvidenceReference {
    let first = SessionEvidenceBuilder.build(meta: meeting.meta, lines: meeting.lines, notes: meeting.notes, summary: meeting.summary)
    guard let decision = first.first(where: { $0.origin == .textNotes && $0.quote != nil && $0.lineID != nil }),
          let claim = decision.claimRevision, let line = decision.sourceRevision else {
        throw EmitError.missing("quoted decision evidence in \(meeting.meta.title ?? "")")
    }
    meeting.summary.evidenceReviews = [decision.id: SessionEvidenceReview(
        decision: .supported, claimRevision: claim, transcriptSourceRevision: decision.transcriptSourceRevision,
        lineSourceRevision: line, reviewedAt: meeting.summary.generatedAt.addingTimeInterval(30))]
    let second = SessionEvidenceBuilder.build(meta: meeting.meta, lines: meeting.lines, notes: meeting.notes, summary: meeting.summary)
    guard let reviewed = second.first(where: { $0.id == decision.id }), reviewed.reviewDecision == .supported else {
        throw EmitError.missing("supported review for \(decision.id)")
    }
    return reviewed
}

func data<T: Codable & Equatable>(_ value: T, _ name: String) throws -> Data {
    let bytes = try encoder.encode(value)
    guard try decoder.decode(T.self, from: bytes) == value else { throw EmitError.roundTrip(name) }
    return bytes
}

func lines<T: Codable & Equatable>(_ values: [T], _ name: String) throws -> Data {
    var out = Data()
    for value in values {
        let bytes = try data(value, name)
        out.append(bytes); out.append(0x0A)
    }
    let report = JSONLines.decodeReport(T.self, from: out)
    guard report.records == values else { throw EmitError.roundTrip(name) }
    return out
}

func write(_ bytes: Data, _ path: String, in output: URL) throws {
    let url = output.appendingPathComponent(path)
    try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
    try bytes.write(to: url, options: .atomic)
}

func emit(_ output: URL) throws {
    try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)

    // Three speakers: the user, and two voices of the other side (voice 1 is named).
    let l1 = id("44444444-4444-4444-8444-000000000001"), l2 = id("44444444-4444-4444-8444-000000000002")
    let l3 = id("44444444-4444-4444-8444-000000000003"), l4 = id("44444444-4444-4444-8444-000000000004")
    var first = meeting(id: firstID, start: 0, title: "A15 第一次交付会", lines: [
        (l1, 10, .remote, 1, "我们先按周五之前提交，Zipformer 的结果一起附上。"),
        (l2, 20, .me, nil, "好的，周五之前提交没问题。"),
        (l3, 30, .remote, 2, "Sherpa 那边的模型我来准备。"),
        (l4, 40, .remote, 1, "那就这么定，下周再看评测。")
    ], decision: "先按周五提交", quote: "周五之前提交", correction: (line: 2, original: "Sherpa", corrected: "Sherpa-ONNX"))
    let m1 = id("44444444-4444-4444-8444-000000000011"), m2 = id("44444444-4444-4444-8444-000000000012")
    var second = meeting(id: secondID, start: 7 * 86_400, title: "A15 第二次交付会", lines: [
        (m1, 10, .remote, 1, "评测还没跑完，正式改为周一之前提交。"),
        (m2, 20, .me, nil, "明白，周一之前提交。")
    ], decision: "正式改为周一提交", quote: "周一之前提交", correction: nil)
    let firstDecision = try reviewedDecision(&first)
    let secondDecision = try reviewedDecision(&second)

    // Series: lesson, question bank, glossary, and one decision replaced through the merger.
    let term = GlossaryEntry(id: id("aaaaaaaa-aaaa-4aaa-8aaa-000000000001"), canonical: "Zipformer", kind: .term,
                             variants: ["Zip former", "z 坡 former"], note: "流式识别模型")
    let person = GlossaryEntry(id: id("aaaaaaaa-aaaa-4aaa-8aaa-000000000002"), canonical: "王老师", kind: .person,
                               variants: ["汪老师"])
    let seriesTerm = GlossaryEntry(id: id("aaaaaaaa-aaaa-4aaa-8aaa-000000000003"), canonical: "Sherpa-ONNX", kind: .term,
                                   variants: ["Sherpa"])
    var workspace = Workspace(id: seriesID, name: "A15 交付系列", mode: .meeting, notes: "每周一次的交付例会。",
                              lessons: [WorkspaceLesson(id: id("77777777-7777-4777-8777-000000000001"),
                                                        text: "先给结论再给评测数字。", sourceSessionID: firstID,
                                                        createdAt: at(700), updatedAt: at(700))],
                              questionBank: [QuestionBankItem(id: id("77777777-7777-4777-8777-000000000002"),
                                                              question: "提交时间定了吗？", answer: "先按周五提交。",
                                                              sourceSessionID: firstID, updatedAt: at(700))])
    workspace.glossary = Glossary(entries: [seriesTerm])
    workspace.createdAt = at(-86_400); workspace.updatedAt = at(700)
    _ = WorkspaceSeriesChangeMerger.collectCandidates(from: [firstDecision], sessionID: firstID, into: &workspace, now: at(720))
    guard let addID = workspace.seriesChangeCandidates?.last?.id,
          let added = WorkspaceSeriesChangeMerger.applyCandidate(addID, decision: .add, operationID: id("bbbbbbbb-bbbb-4bbb-8bbb-000000000001"),
                                                                 into: &workspace, now: at(730)),
          let randomItemID = added.resultItemID else { throw EmitError.missing("added series item") }
    // The merger gives a new item a random id. Pin it before the replace (which derives the
    // history entry's id from it) by rewriting the encoded workspace and decoding it again.
    let itemID = id("99999999-9999-4999-8999-000000000001")
    let pinned = String(decoding: try encoder.encode(workspace), as: UTF8.self)
        .replacingOccurrences(of: randomItemID.uuidString, with: itemID.uuidString)
    workspace = try decoder.decode(Workspace.self, from: Data(pinned.utf8))
    _ = WorkspaceSeriesChangeMerger.collectCandidates(from: [secondDecision], sessionID: secondID, into: &workspace,
                                                      now: at(7 * 86_400 + 720))
    guard let replaceID = workspace.seriesChangeCandidates?.last?.id,
          WorkspaceSeriesChangeMerger.applyCandidate(replaceID, decision: .replace, operationID: id("bbbbbbbb-bbbb-4bbb-8bbb-000000000002"),
                                                     targetItemID: itemID, expectedRevision: 1, into: &workspace,
                                                     now: at(7 * 86_400 + 730)) != nil,
          let replaced = workspace.seriesItems?.first(where: { $0.id == itemID }),
          replaced.revision == 2, replaced.history.count == 1 else { throw EmitError.missing("replaced series item") }

    // The first meeting, as the flat files the Windows check reads.
    let translations = first.lines.filter { $0.speaker == .remote }.map {
        SessionTranscriptTranslation(id: $0.id, text: "EN: " + $0.text,
                                     inputRevision: SessionProjectionBuilder.transcriptRevision(id: $0.id, text: $0.text))
    }
    let voices = first.lines.compactMap { line in line.voice.map { SessionVoiceTag(id: line.id, voice: $0) } }
    let questions = [SessionQuestionEvent(id: id("55555555-5555-4555-8555-000000000001"), date: at(15),
                                          text: "提交时间定了吗？", leadIn: "那我问一下", intent: "confirm", score: 0.82,
                                          disposition: "auto")]
    let answers = [SessionAnswerEvent(id: id("55555555-5555-4555-8555-000000000002"), date: at(16),
                                      question: "提交时间定了吗？", answer: "先按周五提交。", model: "fixture-model",
                                      seconds: 2.5, firstTokenSeconds: 0.75, automatic: true, completion: .complete)]
    var user = SessionUserContent()
    user.revision = 3
    user.updatedAt = at(800)
    user.personalNotes = "记得把评测表发给王老师。"
    user.highlights = [SessionHighlight(id: id("cccccccc-cccc-4ccc-8ccc-000000000001"), lineID: l2, offsetSeconds: 20,
                                        quote: "周五之前提交没问题")]
    user.editedMarkdown = "# A15 第一次交付会\n\n## 决定\n- 先按周五提交（已核对）"
    user.basedOnGeneratedAt = first.summary.generatedAt
    user.correctionDecisions = [SessionCorrectionDecision(id: id("cccccccc-cccc-4ccc-8ccc-000000000002"), lineID: l3,
                                                          original: "Sherpa", corrected: "Sherpa-ONNX", reverted: false)]
    let skills = [Skill(id: id("88888888-8888-4888-8888-000000000001"), name: "A15 交付口径",
                        summary: "回答交付时间时先给日期", content: "先说日期，再说依据。", global: false,
                        enabled: true, importedAt: at(-3_600))]
    var custom = NotesTemplate.builtIn(.work).customCopy(named: "A15 交付纪要")
    custom.id = "99999999-9999-4999-8999-000000000002"  // customCopy draws a random UUID string
    let glossary = Glossary(entries: [term, person])
    var manifest = BackupManifest(kind: .full, createdAt: at(900), appVersion: "fixture", appBuild: "0",
                                  includesSecrets: false, workspaceCount: 1, sessionCount: 2)
    manifest.createdAt = at(900)

    try write(try data(first.meta, "session-meta.json"), "session-meta.json", in: output)
    try write(try lines(first.lines, "transcript.jsonl"), "transcript.jsonl", in: output)
    try write(try lines(translations, "translations.jsonl"), "translations.jsonl", in: output)
    try write(try lines(voices, "voices.jsonl"), "voices.jsonl", in: output)
    try write(try lines(questions, "questions.jsonl"), "questions.jsonl", in: output)
    try write(try lines(answers, "answers.jsonl"), "answers.jsonl", in: output)
    try write(try data(first.summary, "summary.json"), "summary.json", in: output)
    try write(try data(first.notes, "structured-notes.json"), "structured-notes.json", in: output)
    try write(try data(user, "user-content.json"), "user-content.json", in: output)
    try write(try data(workspace, "workspace.json"), "workspace.json", in: output)
    try write(try data(skills, "skills.json"), "skills.json", in: output)
    try write(try data([custom], "templates.json"), "templates.json", in: output)
    try write(try data(glossary, "glossary.json"), "glossary.json", in: output)
    try write(try data(manifest, "backup-manifest.json"), "backup-manifest.json", in: output)

    // Both meetings in the Mac folder layout (`Sessions/<id>/…`), for the cross-meeting item.
    for meeting in [first, second] {
        let folder = "sessions/\(meeting.meta.id.uuidString)"
        try write(try data(meeting.meta, "meta.json"), "\(folder)/meta.json", in: output)
        try write(try lines(meeting.lines, "transcript.jsonl"), "\(folder)/transcript.jsonl", in: output)
        try write(try data(meeting.summary, "summary.json"), "\(folder)/summary.json", in: output)
        try write(try data(meeting.notes, "structured-notes.json"), "\(folder)/structured-notes.json", in: output)
    }

    // A11: every revision value with the Swift result next to its inputs.
    func lineRevisions(_ meeting: Meeting) -> [[String: String]] {
        meeting.lines.map { ["lineID": $0.id.uuidString, "text": $0.text,
                             "transcriptRevision": SessionProjectionBuilder.transcriptRevision(id: $0.id, text: $0.text)] }
    }
    func evidence(_ reference: SessionEvidenceReference) -> [String: String] {
        ["evidenceID": reference.id, "claim": reference.claim, "quote": reference.quote ?? "",
         "location": reference.location ?? "", "lineID": reference.lineID?.uuidString ?? "",
         "claimRevision": reference.claimRevision ?? "", "sourceRevision": reference.sourceRevision ?? "",
         "transcriptSourceRevision": reference.transcriptSourceRevision]
    }
    func meetingRevisions(_ meeting: Meeting, _ reference: SessionEvidenceReference) -> [String: Any] {
        ["sessionID": meeting.meta.id.uuidString,
         "folder": "sessions/\(meeting.meta.id.uuidString)",
         "transcriptSourceRevision": SessionProjectionBuilder.transcriptSourceRevision(meta: meeting.meta, lines: meeting.lines, notes: meeting.notes),
         "transcriptSourceRevisionWithoutNotes": SessionProjectionBuilder.transcriptSourceRevision(meta: meeting.meta, lines: meeting.lines),
         "structuredNotesRevision": SessionProjectionBuilder.structuredNotesRevision(meeting.notes) ?? "",
         "lines": lineRevisions(meeting), "decisionEvidence": evidence(reference)]
    }
    let transcriptSource = SessionProjectionBuilder.transcriptSourceRevision(meta: first.meta, lines: first.lines, notes: first.notes)
    let claim = firstDecision.claimRevision ?? ""
    let source = firstDecision.sourceRevision ?? ""
    let revisions: [String: Any] = [
        "note": "Values computed by NextThoughtCore (SessionProjectionBuilder, SessionEvidenceBuilder). claimSourceRevision is the Swift claimRevision of the first meeting's decision; sourceRevision is the revision of the transcript line it quotes.",
        "transcriptSourceRevision": transcriptSource,
        "claimSourceRevision": claim,
        "sourceRevision": source,
        "expected": ["transcriptSourceRevision=\(transcriptSource)", "claimSourceRevision=\(claim)", "sourceRevision=\(source)"],
        "meetings": [meetingRevisions(first, firstDecision), meetingRevisions(second, secondDecision)],
        "seriesItem": ["id": replaced.id.uuidString, "revision": replaced.revision,
                       "claimRevision": replaced.claimRevision, "sourceRevision": replaced.sourceRevision,
                       "historyClaimRevision": replaced.history.first?.claimRevision ?? "",
                       "historyRevisionID": replaced.history.first?.id.uuidString ?? ""]
    ]
    try write(try JSONSerialization.data(withJSONObject: revisions, options: [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]),
              "revisions.json", in: output)
    print("MacCodec emit: wrote A15 fixtures to \(output.path)")
}

let required = ["session-meta.json", "transcript.jsonl", "translations.jsonl", "questions.jsonl",
                "summary.json", "user-content.json", "workspace.json", "skills.json", "templates.json",
                "glossary.json", "backup-manifest.json", "revisions.json"]

/// Decodes every file with the app's own types, so a fixture edited by hand fails here.
func check(_ output: URL) throws {
    func read(_ name: String) throws -> Data { try Data(contentsOf: output.appendingPathComponent(name)) }
    func each<T: Decodable>(_ type: T.Type, _ name: String) throws -> Int {
        let report = JSONLines.decodeReport(T.self, from: try read(name))
        guard report.malformedLineNumbers.isEmpty, !report.truncatedFinalLine else {
            throw EmitError.roundTrip("\(name): unreadable line(s) \(report.malformedLineNumbers)")
        }
        return report.records.count
    }
    var missing: [String] = []
    for name in required where !FileManager.default.fileExists(atPath: output.appendingPathComponent(name).path) { missing.append(name) }
    guard missing.isEmpty else { print("MacCodec check: missing: \(missing.joined(separator: ", "))"); exit(1) }
    _ = try decoder.decode(SessionMeta.self, from: read("session-meta.json"))
    _ = try each(SessionTranscriptLine.self, "transcript.jsonl")
    _ = try each(SessionTranscriptTranslation.self, "translations.jsonl")
    _ = try each(SessionQuestionEvent.self, "questions.jsonl")
    _ = try decoder.decode(SessionSummary.self, from: read("summary.json"))
    _ = try decoder.decode(SessionUserContent.self, from: read("user-content.json"))
    let workspace = try decoder.decode(Workspace.self, from: read("workspace.json"))
    guard workspace.seriesItems?.contains(where: { $0.revision > 1 && !$0.history.isEmpty }) == true else {
        throw EmitError.missing("a replaced series item in workspace.json")
    }
    _ = try decoder.decode([Skill].self, from: read("skills.json"))
    _ = try decoder.decode([NotesTemplate].self, from: read("templates.json"))
    _ = try decoder.decode(Glossary.self, from: read("glossary.json"))
    try decoder.decode(BackupManifest.self, from: read("backup-manifest.json")).validate()
    _ = try JSONSerialization.jsonObject(with: read("revisions.json"))
    print("MacCodec check: all \(required.count) fixture files decode with NextThoughtCore")
}

let args = Array(CommandLine.arguments.dropFirst())
guard let command = args.first else { print("usage: MacCodec emit|check --output <dir>"); exit(2) }
var output = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
if let index = args.firstIndex(of: "--output"), index + 1 < args.count { output = URL(fileURLWithPath: args[index + 1]) }
do {
    switch command {
    case "emit": try emit(output); try check(output)
    case "check": try check(output)
    default: print("usage: MacCodec emit|check --output <dir>"); exit(2)
    }
} catch {
    FileHandle.standardError.write(Data("MacCodec error: \(error)\n".utf8)); exit(1)
}
