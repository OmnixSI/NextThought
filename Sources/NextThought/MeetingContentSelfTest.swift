import Foundation
import NextThoughtCore

/// Exercises the real archive without microphones, models or external services.
@MainActor
enum MeetingContentSelfTest {
    static func run() async throws -> String {
        guard (ProcessInfo.processInfo.environment["NEXTTHOUGHT_DATA_DIR"] != nil || ProcessInfo.processInfo.environment["INTERVIEWHACKER_DATA_DIR"] != nil), !DemoMode.isEnabled else {
            throw SelfTest.Failure("meeting-content requires an isolated data directory and no demo mode")
        }
        let archive = SessionArchive.shared
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let context = SessionContextSnapshot(mode: .labMeeting, style: .detailed, preferences: "", profile: "", documents: [], effectiveMode: .labMeeting)
        let noteOnly = SessionMeta(startedAt: start, mode: .labMeeting, title: "仅有个人笔记")
        archive.begin(noteOnly, context: context)
        _ = try await archive.updateUserContent(for: noteOnly.id) { $0.personalNotes = "下次验证消融实验" }
        guard archive.finish()?.id == noteOnly.id else { throw SelfTest.Failure("note-only meeting was discarded") }
        await archive.flush()
        guard let reopened = await archive.detail(noteOnly.id), reopened.userContent.personalNotes == "下次验证消融实验" else {
            throw SelfTest.Failure("note-only content did not survive disk reload")
        }

        let meta = SessionMeta(startedAt: start, mode: .labMeeting, title: "研究讨论")
        let line = SessionTranscriptLine(date: start.addingTimeInterval(5), speaker: .room, text: "Mamba 结构需要消融实验")
        archive.begin(meta, context: context)
        archive.append(line)
        _ = archive.finish()
        await archive.flush()
        let item = NotesItem(text: "原始AI结论", time: "00:05", uncertain: true)
        let original = StructuredNotes(templateID: "lab", templateName: "组会", language: "zh", title: "研究讨论",
                                       sections: [.init(id: "results", title: "研究进展", kind: .items, items: [item])],
                                       corrections: [.init(line: 0, original: "Mamba", corrected: "曼巴")], generatedAt: start)
        let summary = SessionSummary(title: "AI题目", markdown: "## 研究进展\n原始AI结论", questions: [], generatedAt: start, model: "test")
        try await archive.saveGeneratedContent(summary, notes: original, for: meta.id)
        var edited = original; edited.sections[0].items[0].text = "用户核实后的实验结论"
        let snapshot = edited
        _ = try await archive.updateUserContent(for: meta.id) { content in
            content.personalNotes = "我的疑问：基线是否一致"
            content.editedNotes = snapshot
            content.highlights = [.init(lineID: line.id, offsetSeconds: 5, quote: line.text)]
            content.correctionDecisions = [.init(lineID: line.id, original: "Mamba", corrected: "曼巴", reverted: true)]
        }
        var regenerated = original; regenerated.sections[0].items[0].text = "新AI结论"; regenerated.generatedAt = start.addingTimeInterval(1)
        try await archive.saveGeneratedContent(summary, notes: regenerated, for: meta.id)
        guard let effective = await archive.detail(meta.id),
              effective.effectiveNotes?.sections[0].items[0].text == snapshot.sections[0].items[0].text,
              effective.effectiveLines.first?.text == line.text,
              effective.userContent.highlights.first?.lineID == line.id,
              effective.fullMarkdown.contains("用户核实后的实验结论"),
              effective.fullMarkdown.contains("我的疑问：基线是否一致") else {
            throw SelfTest.Failure("regeneration replaced edits, corrections, highlights or effective export")
        }
        guard SessionTranscriptLocator.index(offsetSeconds: 18 * 60 + 22, startedAt: effective.meta.startedAt, lines: effective.lines) == nil,
              SessionTranscriptLocator.index(lineID: line.id, offsetSeconds: 18 * 60 + 22, startedAt: effective.meta.startedAt, lines: effective.lines) == 0,
              effective.effectiveNotes?.sections[0].items[0].text == snapshot.sections[0].items[0].text else {
            throw SelfTest.Failure("distant citation borrowed unrelated evidence or changed edited notes")
        }

        let checkpoint = SessionMeta(startedAt: start, mode: .labMeeting, title: "回答检查点")
        archive.begin(checkpoint, context: context)
        let request = UUID()
        archive.append(SessionAnswerEvent(id: request, date: start, question: "为什么需要消融", answer: "为了验证", model: "test", automatic: false, completion: .interrupted))
        archive.append(SessionAnswerEvent(id: request, date: start, question: "为什么需要消融", answer: "为了验证每个模块的贡献", model: "test", automatic: false, completion: .interrupted))
        _ = archive.finish()
        await archive.flush()
        guard let partial = await archive.detail(checkpoint.id), partial.answers.count == 1,
              partial.answers[0].answer == "为了验证每个模块的贡献", !partial.answers[0].isComplete else {
            throw SelfTest.Failure("same-ID checkpoints duplicated, regressed or lost their incomplete status")
        }

        let corruptURL = archive.folder(noteOnly.id).appendingPathComponent(SessionUserContent.filename)
        let intact = try Data(contentsOf: corruptURL)
        try Data("{damaged".utf8).write(to: corruptURL, options: .atomic)
        archive.editPersonalNotes("失败后仍保留的草稿", for: noteOnly.id)
        await archive.persistPersonalNotes(for: noteOnly.id)
        guard archive.userContentDrafts[noteOnly.id] == "失败后仍保留的草稿", archive.userSaveErrors[noteOnly.id] != nil else {
            throw SelfTest.Failure("failed save lost its draft or claimed success")
        }
        try intact.write(to: corruptURL, options: .atomic)
        await archive.persistPersonalNotes(for: noteOnly.id)
        guard archive.userContentDrafts[noteOnly.id] == nil, archive.userSaveErrors[noteOnly.id] == nil else { throw SelfTest.Failure("save retry did not recover") }
        await archive.flushUserContent()
        return "note-only retention, disk reload, human-edit protection, bounded/stable-ID evidence lookup, correction decisions, effective export, partial-answer checkpoints and failed-save retry passed; no model calls"
    }
}
