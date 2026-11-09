import Foundation
import Testing
@testable import InterviewHackerCore

@Suite struct ProductArchiveUXTests {
    private func temporaryRoot() throws -> URL {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("archive-ux-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        return root
    }

    @Test func searchCoversMoreThanOneThousandAndEvictionDoesNotLimitCoverage() async throws {
        let root = try temporaryRoot(); defer { try? FileManager.default.removeItem(at: root) }
        var ids: [UUID] = []
        for index in 0..<1005 {
            let id = UUID(); ids.append(id)
            let folder = root.appendingPathComponent(id.uuidString)
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            let line = SessionTranscriptLine(date: .now, speaker: .remote, text: index == 1004 ? "组会讨论稀疏注意力" : "第 \(index) 场普通记录")
            try JSONLines.line(line).write(to: folder.appendingPathComponent("transcript.jsonl"))
            // Search must not load or decode the materials snapshot.
            try Data("not a valid context".utf8).write(to: folder.appendingPathComponent("context.json"))
        }
        let search = SessionSearch(root: root, cacheLimit: 1024)
        let result = try await search.search(ids: ids, terms: Glossary().search(for: "稀疏注意力"), revision: 0)
        #expect(result.checked == 1005)
        #expect(result.unreadable == 0)
        #expect(result.hits[ids.last!] != nil)
        let bytes = await search.cachedByteCount
        #expect(bytes <= 1024)
    }

    @Test func savedUserNotesRefreshAndCarryExactTargets() async throws {
        let root = try temporaryRoot(); defer { try? FileManager.default.removeItem(at: root) }
        let id = UUID(), lineID = UUID()
        let folder = root.appendingPathComponent(id.uuidString)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        try JSONLines.line(SessionTranscriptLine(id: lineID, date: .now, speaker: .remote, text: "模型对比实验"))
            .write(to: folder.appendingPathComponent("transcript.jsonl"))
        let search = SessionSearch(root: root)
        let first = try await search.search(ids: [id], terms: Glossary().search(for: "模型对比"), revision: 0)
        #expect(first.hits[id]?.target == .transcript(lineID))
        var user = SessionUserContent(); user.personalNotes = "下周补做消融实验"; user.revision = 1
        try JSONLines.encoder().encode(user).write(to: folder.appendingPathComponent(SessionUserContent.filename), options: .atomic)
        let next = try await search.search(ids: [id], terms: Glossary().search(for: "消融实验"), revision: 1)
        #expect(next.hits[id]?.target == .personalNotes)
        user.personalNotes = "下周补做稳健性实验"; user.revision = 2
        try JSONLines.encoder().encode(user).write(to: folder.appendingPathComponent(SessionUserContent.filename), options: .atomic)
        let removed = try await search.search(ids: [id], terms: Glossary().search(for: "消融实验"), revision: 2)
        #expect(removed.hits.isEmpty)
        try Data("broken".utf8).write(to: folder.appendingPathComponent(SessionUserContent.filename))
        let corrupted = try await search.search(ids: [id], terms: Glossary().search(for: "模型"), revision: 3)
        #expect(corrupted.unreadable == 1)
    }

    @Test func oldCopiedProjectOwnsArchivesBeforeOriginalIsDeleted() throws {
        let root = try temporaryRoot(); defer { try? FileManager.default.removeItem(at: root) }
        let originalID = UUID()
        let folder = root.appendingPathComponent("Materials").appendingPathComponent(originalID.uuidString)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        try Data("手工核对后的提取内容".utf8).write(to: folder.appendingPathComponent("paper.md"))
        let document = ContextDocument(name: "paper.pdf", pages: ["旧的内联内容"], markdownPath: "Materials/\(originalID.uuidString)/paper.md")
        let copy = Workspace(name: "研究副本", mode: .labMeeting, documents: [document])
        let repaired = try WorkspaceArchiveOwnership.repair(copy, dataRoot: root)
        #expect(repaired.updatedAt == copy.updatedAt)
        #expect(repaired.documents[0].markdownPath?.contains(copy.id.uuidString) == true)
        try FileManager.default.removeItem(at: folder)
        let text = try String(contentsOf: root.appendingPathComponent(repaired.documents[0].markdownPath!), encoding: .utf8)
        #expect(text == "手工核对后的提取内容")
        #expect(try WorkspaceArchiveOwnership.repair(repaired, dataRoot: root) == repaired)
        let missing = Workspace(name: "旧版资料", mode: .labMeeting, documents: [document])
        let rebuilt = try WorkspaceArchiveOwnership.repair(missing, dataRoot: root)
        #expect(try String(contentsOf: root.appendingPathComponent(rebuilt.documents[0].markdownPath!), encoding: .utf8) == "旧的内联内容")
    }

    @Test func templateMergeDefaultsToLocalAndPreferencesIncludeNotes() {
        let local = NotesTemplate(id: "custom-test", name: "本机模板", sections: [])
        let incoming = NotesTemplate(id: "custom-test", name: "备份模板", sections: [])
        let kept = BackupMerge.notesTemplates(existing: [local], incoming: [incoming])
        #expect(kept.templates == [local]); #expect(kept.kept == 1)
        let replaced = BackupMerge.notesTemplates(existing: [local], incoming: [incoming], useIncoming: [incoming.id])
        #expect(replaced.templates == [incoming]); #expect(replaced.replaced == 1)
        let keys = Set(SettingsSnapshot.exportedKeys)
        #expect(keys.isSuperset(of: ["customNotesTemplates", "notesTemplateByType", "notesLanguage", "notesModelID", "notesReasoning"]))
    }

    @Test func restoreConflictUsesContentRatherThanFileDates() throws {
        let root = try temporaryRoot(); defer { try? FileManager.default.removeItem(at: root) }
        let file = root.appendingPathComponent(SessionUserContent.filename)
        try Data("my notes".utf8).write(to: file)
        let first = try BackupContent.fingerprint(root)
        try FileManager.default.setAttributes([.modificationDate: Date(timeIntervalSince1970: 1)], ofItemAtPath: file.path)
        #expect(try BackupContent.fingerprint(root) == first)
        try Data("revised notes".utf8).write(to: file)
        let second = try BackupContent.fingerprint(root)
        #expect(BackupContent.status(local: first, incoming: second) == .conflict)
        #expect(BackupContent.status(local: first, incoming: first) == .identical)
        #expect(BackupContent.status(local: nil, incoming: second) == .new)
    }
}
