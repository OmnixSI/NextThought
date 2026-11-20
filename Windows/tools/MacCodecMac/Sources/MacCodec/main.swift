import Foundation

// A15: macOS fixture generator. It writes schema-compatible samples for every
// file family the Windows backup/import path must decode. No app data is read,
// no app is built, installed or signed.

let iso = ISO8601DateFormatter()
let now = Date(timeIntervalSince1970: 1_764_000_000)
let stamp = iso.string(from: now)

func write(_ name: String, _ value: Any, to output: URL) throws {
    let data = try JSONSerialization.data(withJSONObject: value, options: [.prettyPrinted, .sortedKeys])
    try data.write(to: output.appendingPathComponent(name), options: .atomic)
}

let sessionID = "11111111-1111-4111-8111-111111111111"
let secondID = "22222222-2222-4222-8222-222222222222"
let seriesID = "33333333-3333-4333-8333-333333333333"

func emit(_ output: URL) throws {
    try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)
    let meta: [String: Any] = [
        "id": sessionID, "title": "A15 fixture meeting", "startedAt": stamp, "endedAt": stamp,
        "mode": "meeting", "platform": "mac", "inPerson": false,
        "workspaceID": seriesID, "workspaceName": "A15 series",
        "questionCount": 1, "answerCount": 1
    ]
    try write("session-meta.json", meta, to: output)
    let lines: [[String: Any]] = [
        ["id": secondID, "date": stamp, "speaker": "remote", "text": "A15 夹具里的转写句子。"],
        ["id": "44444444-4444-4444-8444-444444444444", "date": stamp, "speaker": "me", "text": "A15 夹具里的第二句。"]
    ]
    for line in lines {
        let data = try JSONSerialization.data(withJSONObject: line, options: [.sortedKeys])
        let path = output.appendingPathComponent("transcript.jsonl")
        if !FileManager.default.fileExists(atPath: path.path) { FileManager.default.createFile(atPath: path.path, contents: nil) }
        let handle = try FileHandle(forWritingTo: path); try handle.seekToEnd(); try handle.write(contentsOf: data + Data([0x0A])); try handle.close()
    }
    try write("translations.jsonl", ["id": secondID, "inputRevision": "rev-a15-1", "text": "A15 fixture sentence."], to: output)
    try write("questions.jsonl", ["id": "55555555-5555-4555-8555-555555555555", "date": stamp, "text": "A15 问题？", "intent": "detected", "disposition": "candidate"], to: output)
    try write("summary.json", [
        "title": "A15 fixture meeting", "markdown": "# A15\n夹具纪要。", "generatedAt": stamp,
        "questions": [], "model": "fixture"
    ], to: output)
    try write("user-content.json", [
        "schemaVersion": 1, "revision": 1, "updatedAt": stamp,
        "personalNotes": "A15 用户笔记。", "highlights": [],
        "editedMarkdown": "# A15\n编辑过的纪要。"
    ], to: output)
    let replacedItem: [String: Any] = [
        "id": "66666666-6666-4666-8666-666666666666", "kind": "decision", "text": "夹具系列条目",
        "revision": 2, "createdAt": stamp, "updatedAt": stamp,
        "history": [
            ["revision": 1, "text": "旧文案", "recordedAt": stamp],
            ["revision": 2, "text": "替换后的文案", "recordedAt": stamp]
        ]
    ]
    try write("workspace.json", [
        "id": seriesID, "name": "A15 series", "createdAt": stamp,
        "sessionIds": [sessionID], "lessons": [["id": "77777777-7777-4777-8777-777777777777", "text": "一条经验"]],
        "seriesItems": [replacedItem], "questionBank": [], "documents": []
    ], to: output)
    try write("skills.json", [["id": "88888888-8888-4888-8888-888888888888", "name": "A15 skill", "prompt": "A15 提示词"]], to: output)
    try write("templates.json", [["id": "99999999-9999-4999-8999-999999999999", "name": "A15 template", "sections": [["title": "要点", "prompt": "列出要点"]]]], to: output)
    try write("glossary.json", ["entries": [["id": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "canonical": "A15", "variants": ["A一五"], "kind": "term"]]], to: output)
    try write("backup-manifest.json", ["format": "nextthought-backup", "version": 1, "createdAt": stamp, "includesSecrets": false, "sessions": [sessionID], "workspaces": [seriesID]], to: output)
    try write("revisions.json", [
        "transcriptSourceRevision": "rev-transcript-a15",
        "claimSourceRevision": "rev-claim-a15",
        "sourceRevision": "rev-source-a15",
        "expected": [
            "transcriptSourceRevision=rev-transcript-a15",
            "claimSourceRevision=rev-claim-a15",
            "sourceRevision=rev-source-a15"
        ]
    ], to: output)
    print("MacCodec emit: wrote A15 fixtures to \(output.path)")
}

func check(_ output: URL) throws {
    let required = ["session-meta.json", "transcript.jsonl", "translations.jsonl", "questions.jsonl",
                    "summary.json", "user-content.json", "workspace.json", "skills.json", "templates.json",
                    "glossary.json", "backup-manifest.json", "revisions.json"]
    var missing: [String] = []
    for name in required where !FileManager.default.fileExists(atPath: output.appendingPathComponent(name).path) { missing.append(name) }
    if !missing.isEmpty {
        print("MacCodec check: missing: \(missing.joined(separator: ", "))")
        exit(1)
    }
    print("MacCodec check: all \(required.count) fixture files present")
}

let args = Array(CommandLine.arguments.dropFirst())
guard let command = args.first else { print("usage: MacCodec emit|check --output <dir>"); exit(2) }
var output = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
if let index = args.firstIndex(of: "--output"), index + 1 < args.count { output = URL(fileURLWithPath: args[index + 1]) }
do {
    switch command {
    case "emit": try emit(output)
    case "check": try check(output)
    default: print("usage: MacCodec emit|check --output <dir>"); exit(2)
    }
} catch {
    FileHandle.standardError.write(Data("MacCodec error: \(error)\n".utf8)); exit(1)
}
