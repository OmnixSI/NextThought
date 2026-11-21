import Foundation
import NextThoughtCore

/// `--self-test backup-fixtures --output <dir>`: builds four small scenarios in scratch
/// storage through the app's own stores, then exports them with the code behind
/// 数据 → 备份 (`DataBackup.exportAll` / `exportWorkspace`) and takes three automatic
/// restore points. The files feed the Windows import check (Windows/MAC_FIXTURE_REQUEST.md).
/// Every meeting is made up; nothing is read from the user's own records.
@MainActor
enum BackupFixtureSelfTest {
    static func run(_ options: [String: String]) async throws -> String {
        guard AppEnvironment.values["NEXTTHOUGHT_DATA_DIR"] != nil else {
            throw SelfTest.Failure("backup-fixtures requires NEXTTHOUGHT_DATA_DIR scratch storage")
        }
        guard !DemoMode.isEnabled else { throw SelfTest.Failure("run backup-fixtures without NEXTTHOUGHT_DEMO") }
        guard let outputPath = options["output"] else { throw SelfTest.Failure("pass --output <dir>") }
        let output = URL(fileURLWithPath: outputPath, isDirectory: true)
        try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)

        let store = WorkspaceStore.shared, archive = SessionArchive.shared
        let glossary = GlossaryStore.shared, skills = SkillLibrary.shared
        // Adds to whatever the scratch folder already holds (the session-export and
        // session-backup-drain meetings), so the full export covers the whole folder.
        store.load(); archive.load(); glossary.load(); skills.load()
        var points: [URL] = []

        // A. Text only: two lines and notes, no translation, no voices.
        var textSeries = store.create(mode: .labMeeting, name: "夹具 · 纯文本组会")
        textSeries.lessons = [WorkspaceLesson(text: "先讲结论，再讲实验细节。")]
        store.update(textSeries)
        let textMeeting = try await meeting(in: textSeries, mode: .labMeeting, title: "周三组会", lines: [
            (.remote, nil, "这周的实验结果先讲一下。"),
            (.me, nil, "基线已经复现，误差在百分之二以内。")
        ], notes: "## 进展\n- 基线已复现，误差在 2% 以内\n\n## 下一步\n- 补消融实验", personal: "记得补消融实验的表格。")
        store.flush()
        points.append(try await DataBackup.automaticBackup(reason: "夹具 A"))

        // B. Several speakers: the user and two voices of the other side, one of them named.
        var voiceSeries = store.create(mode: .groupProject, name: "夹具 · 多人小组项目")
        voiceSeries.questionBank = [QuestionBankItem(question: "谁负责前端？", answer: "我负责前端，周五前给第一版。")]
        store.update(voiceSeries)
        let voiceMeeting = try await meeting(in: voiceSeries, mode: .groupProject, title: "分工会", lines: [
            (.remote, 1, "我们先把分工定下来，我来做后端接口。"),
            (.me, nil, "前端我来，周五前给第一版。"),
            (.remote, 2, "那测试和文档交给我。"),
            (.remote, 1, "好，下周一再碰一次。")
        ], notes: "## 分工\n- 后端接口：李工\n- 前端：我，周五前\n- 测试和文档：说话人 2", personal: nil)
        if var meta = archive.sessions.first(where: { $0.id == voiceMeeting }) {
            meta.voiceNames = ["1": "李工"]
            meta.meVoices = []
            archive.update(meta)
        }
        guard await archive.flush(sessionID: voiceMeeting) else { throw SelfTest.Failure("voice names were not saved") }
        store.flush()
        points.append(try await DataBackup.automaticBackup(reason: "夹具 B"))

        // C. Glossary: global terms and names, a series-only term, and a meeting that uses them.
        glossary.update(GlossaryEntry(canonical: "Zipformer", kind: .term, variants: ["Zip former"], note: "流式识别模型"))
        glossary.update(GlossaryEntry(canonical: "Sherpa-ONNX", kind: .term, variants: ["Sherpa"]))
        glossary.update(GlossaryEntry(canonical: "王老师", kind: .person, variants: ["汪老师"]))
        glossary.flush()
        var termSeries = store.create(mode: .meeting, name: "夹具 · 术语例会")
        termSeries.glossary = Glossary(entries: [GlossaryEntry(canonical: "X-ASR", kind: .term, variants: ["X ASR"])])
        var skill = skills.create(global: false)
        skill.name = "夹具 · 先给日期"
        skill.summary = "回答交付时间时先给日期"
        skill.content = "先说日期，再说依据。"
        skills.update(skill)
        termSeries.skillIDs = [skill.id]
        store.update(termSeries)
        let termMeeting = try await meeting(in: termSeries, mode: .meeting, title: "识别模型例会", lines: [
            (.remote, nil, "王老师说 Zipformer 的流式版本先接进来。"),
            (.me, nil, "好的，我用 Sherpa-ONNX 跑 X-ASR 的模型。")
        ], notes: "## 决定\n- Zipformer 流式版本先接入\n- 用 Sherpa-ONNX 跑 X-ASR 模型", personal: nil)
        store.flush()
        points.append(try await DataBackup.automaticBackup(reason: "夹具 C"))

        // Exports, exactly as 数据 → 备份 writes them (no API keys).
        let all = output.appendingPathComponent("all-scenarios.ntbackup")
        try DataBackup.exportAll(to: all, includeSecrets: false, workspaces: store.workspaces,
                                 skills: skills.skills, glossary: glossary.glossary)
        let singles: [(String, Workspace, UUID)] = [
            ("01-text-only.ntbackup", store.workspace(textSeries.id) ?? textSeries, textMeeting),
            ("02-voiceprints.ntbackup", store.workspace(voiceSeries.id) ?? voiceSeries, voiceMeeting),
            ("03-glossary.ntbackup", store.workspace(termSeries.id) ?? termSeries, termMeeting)
        ]
        for (name, workspace, session) in singles {
            try DataBackup.exportWorkspace(workspace, sessions: [session], to: output.appendingPathComponent(name), skills: skills.skills)
        }

        // D. The restore-point chain is a folder (`Backups/`), so it travels as a ZIP of it.
        let restorePoints = DataBackup.listBackups().filter { $0.url.pathExtension == "ntrestore" }
        try expect(restorePoints.count >= 3, "restore points: \(restorePoints.count)")
        try expect(points.allSatisfy { FileManager.default.fileExists(atPath: $0.path) }, "a new restore point is missing")
        let chain = output.appendingPathComponent("04-incremental-chain-Backups.zip")
        try? FileManager.default.removeItem(at: chain)
        try ditto(["-c", "-k", "--keepParent", AppPaths.backups.path, chain.path])

        // Read every export back the way an import does, and look inside for secrets.
        var report: [String] = []
        for file in [all] + singles.map({ output.appendingPathComponent($0.0) }) {
            let prepared = try await DataBackup.prepareImport(file)
            defer { prepared.discard() }
            try expect(!prepared.manifest.includesSecrets, "\(file.lastPathComponent) says it includes secrets")
            let secrets = FileManager.default.enumerator(at: prepared.root, includingPropertiesForKeys: nil)?
                .compactMap { $0 as? URL }.filter { $0.lastPathComponent == "secrets.plist" } ?? []
            try expect(secrets.isEmpty, "\(file.lastPathComponent) carries secrets.plist")
            report.append("\(file.lastPathComponent) \(prepared.manifest.kind.rawValue) \(prepared.manifest.workspaceCount)ws/\(prepared.manifest.sessionCount)s")
        }
        let voices = archive.folder(voiceMeeting).appendingPathComponent("voices.jsonl")
        try expect(FileManager.default.fileExists(atPath: voices.path), "voices.jsonl missing")
        try expect(!glossary.glossary.isEmpty, "glossary empty")
        report.append("\(restorePoints.count) restore points")
        return report.joined(separator: "; ")
    }

    /// One finished meeting written through `SessionArchive`, with notes and optional personal notes.
    private static func meeting(in workspace: Workspace, mode: SessionMode, title: String,
                                lines: [(SessionSpeaker, Int?, String)], notes: String, personal: String?) async throws -> UUID {
        let archive = SessionArchive.shared
        let start = Date().addingTimeInterval(-600)
        var meta = SessionMeta(startedAt: start, mode: mode, title: title, audioSource: "system+mic",
                               recognizer: "zipformer", answerModel: "fixture", workspaceID: workspace.id,
                               workspaceName: workspace.name)
        meta.inPerson = false
        archive.begin(meta, context: SessionContextSnapshot(mode: mode, style: workspace.style, preferences: workspace.preferences,
                                                            profile: "", documents: workspace.documents,
                                                            glossary: workspace.glossary))
        for (index, line) in lines.enumerated() {
            let date = start.addingTimeInterval(TimeInterval(15 * (index + 1)))
            let record = SessionTranscriptLine(date: date, speaker: line.0, text: line.2,
                                               startedAt: date.addingTimeInterval(-4), endedAt: date)
            archive.append(record)
            if let voice = line.1 { archive.appendVoice(sessionID: meta.id, lineID: record.id, voice: voice) }
        }
        archive.append(SessionQuestionEvent(date: start.addingTimeInterval(20), text: lines[0].2, intent: "explanationRequest",
                                            score: 0.8, disposition: "suggested"))
        guard let finished = archive.finish(at: start.addingTimeInterval(TimeInterval(15 * (lines.count + 1)))) else {
            throw SelfTest.Failure("meeting \(title) was discarded")
        }
        meta = finished
        guard archive.saveSummary(SessionSummary(title: title, markdown: "# \(title)\n\n" + notes, questions: [],
                                                 generatedAt: Date(), model: "fixture"), for: meta.id) else {
            throw SelfTest.Failure("notes of \(title) not saved")
        }
        if let personal {
            var content = SessionUserContent()
            content.personalNotes = personal
            _ = try await archive.saveUserContent(content, for: meta.id)
        }
        guard await archive.flush(sessionID: meta.id) else { throw SelfTest.Failure("meeting \(title) not saved") }
        guard let detail = await archive.detail(meta.id), detail.lines.count == lines.count else {
            throw SelfTest.Failure("meeting \(title) not readable after writing")
        }
        return meta.id
    }

    private static func ditto(_ arguments: [String]) throws {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/ditto")
        process.arguments = arguments
        try process.run()
        process.waitUntilExit()
        try expect(process.terminationStatus == 0, "ditto \(arguments.first ?? "") exited \(process.terminationStatus)")
    }

    private static func expect(_ condition: Bool, _ message: @autoclosure () -> String) throws {
        if !condition { throw SelfTest.Failure(message()) }
    }
}
