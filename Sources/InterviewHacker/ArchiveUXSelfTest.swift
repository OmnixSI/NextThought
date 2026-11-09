import AppKit
import Foundation
import InterviewHackerCore

enum ArchiveUXSelfTest {
    @MainActor
    static func run() async throws -> String {
        guard ProcessInfo.processInfo.environment["INTERVIEWHACKER_DATA_DIR"] != nil else { throw SelfTest.Failure("isolated data directory required") }
        _ = NSApplication.shared
        let state = AppState.shared, store = WorkspaceStore.shared, archive = SessionArchive.shared
        let defaults = UserDefaults.standard
        let keys = ["customNotesTemplates", "notesTemplateByType", "notesModelID", "notesReasoning", "notesLanguage"]
        let old = keys.map { defaults.object(forKey: $0) }
        let oldTemplates = state.customNotesTemplates
        defer {
            for (key, value) in zip(keys, old) { if let value { defaults.set(value, forKey: key) } else { defaults.removeObject(forKey: key) } }
            state.customNotesTemplates = oldTemplates
        }
        let workspace = store.create(mode: .labMeeting, name: "归档体验自检")
        let meta = SessionMeta(startedAt: .now, mode: .labMeeting, title: "可恢复的研究讨论", workspaceID: workspace.id, workspaceName: workspace.name)
        archive.begin(meta, context: .init(mode: .labMeeting, style: .concise, preferences: "", profile: "", documents: []))
        let line = SessionTranscriptLine(date: .now, speaker: .remote, text: "下周补做消融实验")
        archive.append(line); archive.finish(); await archive.flush()
        var user = SessionUserContent(); user.personalNotes = "备份里的原始笔记"
        user.highlights = [.init(lineID: line.id, offsetSeconds: 0, quote: line.text)]
        _ = try await archive.saveUserContent(user, for: meta.id)
        let template = NotesTemplate(id: "archive-ux-template", name: "备份中的研究模板", sections: [.init(id: "evidence", title: "证据", instruction: "附原话")])
        defaults.set(try JSONEncoder().encode([template]), forKey: "customNotesTemplates")
        state.customNotesTemplates = [template]
        store.flush(); await archive.flush()
        let exported = AppPaths.root.appendingPathComponent("archive-ux-roundtrip.ihbackup")
        try DataBackup.exportAll(to: exported, includeSecrets: false, workspaces: store.workspaces)

        user = try await archive.userContent(meta.id); user.personalNotes = "本机后来修改的笔记"
        _ = try await archive.saveUserContent(user, for: meta.id)
        var localTemplate = template; localTemplate.name = "本机后来修改的模板"
        defaults.set(try JSONEncoder().encode([localTemplate]), forKey: "customNotesTemplates")
        state.customNotesTemplates = [localTemplate]
        let keep = try await DataBackup.prepareImport(exported)
        guard keep.sessions.first(where: { $0.id == meta.id })?.status == .conflict,
              keep.templates.first(where: { $0.id == template.id })?.status == .conflict else { throw SelfTest.Failure("modified notes/template were not identified as conflicts") }
        let kept = try await DataBackup.applyImport(keep, useBackupSessions: [], useBackupTemplates: [], applySettings: false)
        guard kept.sessionsKept >= 1, kept.templatesKept == 1,
              try SessionUserContentStore.load(from: archive.folder(meta.id)).personalNotes == "本机后来修改的笔记" else { throw SelfTest.Failure("default restore did not retain local notes") }
        let replace = try await DataBackup.prepareImport(exported)
        let restored = try await DataBackup.applyImport(replace, useBackupSessions: [meta.id], useBackupTemplates: [template.id], applySettings: false)
        let content = try SessionUserContentStore.load(from: archive.folder(meta.id))
        guard restored.sessionsReplaced == 1, restored.templatesReplaced == 1,
              content.personalNotes == "备份里的原始笔记", content.highlights.count == 1,
              state.customNotesTemplates.first?.name == template.name else { throw SelfTest.Failure("selected backup content or independent templates did not restore") }
        let same = try await DataBackup.prepareImport(exported)
        guard same.sessions.first(where: { $0.id == meta.id })?.status == .identical else { throw SelfTest.Failure("re-import is not content-identical") }
        let malformed = AppPaths.root.appendingPathComponent("archive-ux-malformed.ihbackup")
        try Data("{broken metadata".utf8).write(to: same.root.appendingPathComponent("Sessions/\(meta.id.uuidString)/meta.json"))
        _ = try Shell.check("/usr/bin/ditto", ["-c", "-k", "--keepParent", same.root.path, malformed.path], "package malformed test archive")
        var rejected = false
        do { _ = try await DataBackup.prepareImport(malformed) } catch { rejected = true }
        guard rejected, try SessionUserContentStore.load(from: archive.folder(meta.id)).personalNotes == content.personalNotes else { throw SelfTest.Failure("bad metadata was not rejected before changing live content") }
        guard !DataBackup.listBackups().isEmpty else { throw SelfTest.Failure("no recovery point before applying restore") }
        return "portable backup: personal notes + highlights + templates; preview detects conflicts; default keeps local; explicit replace is atomic; identical re-import; recovery point present"
    }
}
