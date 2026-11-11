import Foundation
import Testing
@testable import NextThoughtCore

/// Meeting types, series matching and the after-meeting notes (docs/redesign, sections 3 and 7).
struct MeetingTypeAndNotesTests {
    let calendar: Calendar = {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC")!
        return calendar
    }()

    /// Monday 2026-10-26 10:00 UTC.
    var monday: Date { calendar.date(from: DateComponents(year: 2026, month: 10, day: 26, hour: 10))! }

    private func ymd(_ date: Date?) -> [Int] {
        guard let date else { return [] }
        let c = calendar.dateComponents([.year, .month, .day], from: date)
        return [c.year ?? 0, c.month ?? 0, c.day ?? 0]
    }

    // MARK: Meeting types

    @Test func newTypesRoundTripAndOldValuesStillDecode() throws {
        let encoded = try JSONEncoder().encode(SessionMode.allCases)
        #expect(try JSONDecoder().decode([SessionMode].self, from: encoded) == SessionMode.allCases)
        #expect(try JSONDecoder().decode(SessionMode.self, from: Data(#""interview""#.utf8)) == .interview)
        let decoded = try JSONDecoder().decode([SessionMode].self,
                                               from: Data(#"["labMeeting","client","discussion","custom","academic","meeting","lesson","fromTheFuture"]"#.utf8))
        #expect(decoded == [.labMeeting, .client, .discussion, .custom, .academic, .meeting, .lesson, .general])
        // Raw values never change; only the names shown do.
        #expect(SessionMode.academic.rawValue == "academic" && SessionMode.academic.title == "汇报 / 答辩")
        #expect(SessionMode.meeting.rawValue == "meeting" && SessionMode.meeting.title == "工作会议")
        #expect(SessionMode.lesson.rawValue == "lesson" && SessionMode.lesson.title == "讲座 / 课程")
        #expect(!SessionMode.builtIn.contains(.custom) && SessionMode.builtIn.contains(.interview) && SessionMode.builtIn.count == 9)
        #expect(NotesTemplateKind.selectable.contains(.interview))
    }

    @Test func customTypeUsesItsBaseAndOldWorkspacesDecode() throws {
        let id = UUID()
        let old = try JSONDecoder().decode(Workspace.self, from: Data(#"{"id":"\#(id.uuidString)","name":"组会","mode":"labMeeting"}"#.utf8))
        #expect(old.customTypeName == nil && old.customBaseMode == nil && old.effectiveMode == .labMeeting)

        var custom = Workspace(name: "导师一对一", mode: .custom)
        #expect(custom.effectiveMode == .general)
        custom.customTypeName = "导师一对一"
        custom.customBaseMode = .interview
        #expect(custom.effectiveMode == .interview)
        #expect(custom.profile.fields.contains { $0.id == "company" })
        let again = try JSONDecoder().decode(Workspace.self, from: JSONEncoder().encode(custom))
        #expect(again.customTypeName == "导师一对一" && again.customBaseMode == .interview && again.mode == .custom)
    }

    @Test func typeDefaultsFollowTheTable() {
        func check(_ mode: SessionMode, _ live: LiveMode, _ style: AnswerStyle, _ template: NotesTemplateKind) {
            let defaults = MeetingTypeDefaults.for(mode)
            #expect(defaults.liveMode == live, "\(mode)")
            #expect(defaults.style == style, "\(mode)")
            #expect(defaults.template == template, "\(mode)")
        }
        check(.general, .notesOnly, .concise, .general)
        check(.interview, .prompting, .concise, .interview)
        check(.academic, .prompting, .concise, .academic)
        check(.labMeeting, .notesOnly, .detailed, .academic)
        check(.meeting, .notesOnly, .concise, .work)
        check(.client, .prompting, .concise, .client)
        check(.lesson, .notesOnly, .detailed, .lecture)
        check(.discussion, .notesOnly, .concise, .discussion)
        check(.custom, .notesOnly, .concise, .general)
        #expect(MeetingTypeDefaults.for(.academic).followsSlides)
        // Every built-in template is the default of some type.
        #expect(Set(SessionMode.builtIn.map { MeetingTypeDefaults.for($0).template }) == Set(NotesTemplateKind.selectable))
        // Every type has a profile with fields and material slots.
        for mode in SessionMode.allCases { #expect(!ModeProfile.for(mode).fields.isEmpty) }
    }

    @Test func typeIsGuessedFromTheTitle() {
        #expect(MeetingTypeGuesser.guess("字节跳动 · 后端二面") == .interview)
        #expect(MeetingTypeGuesser.guess("周三组会") == .labMeeting)
        #expect(MeetingTypeGuesser.guess("毕业论文答辩") == .academic)
        #expect(MeetingTypeGuesser.guess("Weekly sync") == .meeting)
        #expect(MeetingTypeGuesser.guess("机器学习讲座") == .lesson)
        #expect(MeetingTypeGuesser.guess("Client kickoff call") == .client)
        #expect(MeetingTypeGuesser.guess("随便聊聊") == .general)
        #expect(MeetingTypeGuesser.guess("") == .general)
    }

    // MARK: Series

    @Test func nextRoundMatchesItsSeries() {
        #expect(SeriesMatcher.baseTitle("字节跳动 · 后端二面") == SeriesMatcher.baseTitle("字节跳动 · 后端"))
        #expect(SeriesMatcher.similarity("字节跳动 · 后端二面", "字节跳动 · 后端") > 0.9)
        #expect(SeriesMatcher.similarity("Google SWE round 2", "Google SWE") > 0.9)
        let bytedance = UUID(), tencent = UUID(), weekly = UUID()
        let candidates = [
            SeriesMatcher.Candidate(id: tencent, name: "腾讯 · 前端", mode: .interview, lastUsed: monday),
            SeriesMatcher.Candidate(id: bytedance, name: "字节跳动 · 后端", mode: .interview, lastUsed: monday.addingTimeInterval(-86_400)),
            SeriesMatcher.Candidate(id: weekly, name: "周组会", mode: .labMeeting, lastUsed: monday),
        ]
        #expect(SeriesMatcher.bestMatch(title: "字节跳动 · 后端三面", mode: .interview, candidates: candidates) == bytedance)
        #expect(SeriesMatcher.bestMatch(title: "周组会 第 3 周", candidates: candidates) == weekly)
        #expect(SeriesMatcher.bestMatch(title: "阿里 · 算法一面", candidates: candidates) == nil)
        #expect(SeriesMatcher.bestMatch(title: "", candidates: candidates) == nil)
    }

    // MARK: Dates

    @Test func relativeDatesBecomeDates() {
        func resolve(_ phrase: String) -> [Int] { ymd(NotesDates.resolve(phrase, from: monday, calendar: calendar)) }
        #expect(resolve("今天") == [2026, 10, 26])
        #expect(resolve("明天上午") == [2026, 10, 27])
        #expect(resolve("后天") == [2026, 10, 28])
        #expect(resolve("周四交付") == [2026, 10, 29])
        #expect(resolve("周二下午") == [2026, 10, 27])
        #expect(resolve("本周五") == [2026, 10, 30])
        #expect(resolve("下周一前") == [2026, 11, 2])
        #expect(resolve("星期天") == [2026, 11, 1])
        #expect(resolve("10月30日") == [2026, 10, 30])
        #expect(resolve("10 月 30 号") == [2026, 10, 30])
        #expect(resolve("月底") == [2026, 10, 31])
        #expect(resolve("next Monday") == [2026, 11, 2])
        #expect(resolve("by Friday") == [2026, 10, 30])
        #expect(resolve("tomorrow") == [2026, 10, 27])
        #expect(resolve("尽快").isEmpty)
        #expect(resolve("下周").isEmpty)
        let friday = calendar.date(from: DateComponents(year: 2026, month: 10, day: 30))!
        #expect(NotesDates.format(friday, english: false, calendar: calendar) == "10 月 30 日（周五）")
        #expect(NotesDates.format(friday, english: true, calendar: calendar) == "Fri, Oct 30")
    }

    @Test func timesAreRead() {
        #expect(NotesTime.seconds("12:41") == 761)
        #expect(NotesTime.seconds("[12:41]") == 761)
        #expect(NotesTime.seconds("12：41") == 761)
        #expect(NotesTime.seconds("1:02:05") == 3_725)
        #expect(NotesTime.seconds("12:75") == nil)
        #expect(NotesTime.seconds("1268") == nil)
        #expect(NotesTime.seconds(nil) == nil)
        #expect(NotesTime.text(761) == "12:41")
        #expect(NotesTime.text(3_725) == "1:02:05")
    }

    // MARK: Templates and notes

    @Test func builtInTemplatesHaveAGapSection() {
        for kind in NotesTemplateKind.allCases {
            for english in [false, true] {
                let template = NotesTemplate.builtIn(kind, english: english)
                #expect(template.id == kind.rawValue && template.isBuiltIn)
                #expect(template.sections.contains { $0.isGap }, "\(kind) needs a gaps section")
                #expect(Set(template.sections.map(\.id)).count == template.sections.count)
            }
        }
        let copy = NotesTemplate.builtIn(.work).customCopy(named: "周会")
        #expect(!copy.isBuiltIn && copy.base == .work && copy.sections == NotesTemplate.builtIn(.work).sections)
        let decoded = try? JSONDecoder().decode(NotesTemplate.self, from: JSONEncoder().encode(copy))
        #expect(decoded == copy)
    }

    @Test func modelReplyIsParsedAndChecked() throws {
        let reply = """
        好的，纪要如下：
        ```json
        {"title": "周一例会", "sections": [
          {"id": "summary", "items": [{"text": "下周三前上线新版本"}]},
          {"id": "actions", "items": [
            {"text": "回归测试并给出结果", "time": "05:40", "owner": "我", "due": "周二下午"},
            {"text": "设计稿复审", "time": "12:30", "due": "周四或者周五", "uncertain": "true"},
            {"text": "没有依据的待办", "time": "30:00"}
          ]},
          {"title": "风险和未决问题", "items": [{"text": "导出功能没人认领", "time": 1268}]}
        ]}
        ```
        """
        let template = NotesTemplate.builtIn(.work)
        var notes = try #require(NotesJSON.parse(reply, template: template, language: "zh"))
        #expect(notes.title == "周一例会")
        #expect(notes.sections.map(\.id) == template.sections.map(\.id))
        #expect(notes.sections.first { $0.id == "decisions" }?.items.isEmpty == true)
        let actions = try #require(notes.sections.first { $0.id == "actions" })
        #expect(actions.items.count == 3 && actions.items[0].owner == "我" && actions.items[1].uncertain)
        #expect(notes.sections.first { $0.id == "risks" }?.items.first?.time == "1268")

        NotesVerifier.verify(&notes, lineOffsets: [192, 340, 750, 1_268], duration: 1_800, meetingDate: monday, calendar: calendar)
        let checked = try #require(notes.sections.first { $0.id == "actions" })
        // "30:00" has no transcript line near it; "1268" is not a time.
        #expect(checked.items.map(\.text) == ["回归测试并给出结果", "设计稿复审"])
        #expect(notes.droppedCount == 2)
        #expect(notes.sections.first { $0.id == "risks" }?.items.isEmpty == true)
        #expect(checked.items[0].due == "10 月 27 日（周二）")
        #expect(ymd(checked.items[0].dueDate) == [2026, 10, 27])
        // One-line summaries need no time.
        #expect(notes.sections.first { $0.id == "summary" }?.items.count == 1)
        #expect(notes.actionItemCount == 2)

        let lessons = notes.lessons(template: template)
        #expect(lessons.contains("[待办] 回归测试并给出结果 · 我 · 10 月 27 日（周二）"))
        #expect(NotesJSON.parse("没有 JSON", template: template, language: "zh") == nil)
        let markdown = NotesMarkdown.render(notes, title: "周一例会", facts: "10 月 26 日")
        #expect(markdown.hasPrefix("# 周一例会"))
        #expect(markdown.contains("- 回归测试并给出结果 · 我 · 10 月 27 日（周二） [05:40]"))
        #expect(markdown.contains("（待确认）"))
    }

    @Test func reviewFeedsTheQuestionBank() throws {
        let template = NotesTemplate.builtIn(.interview)
        let reply = """
        {"sections": [{"id": "questions", "items": [
          {"text": "如果删除缓存失败了怎么办？", "time": "18:22", "answer": "重试队列 + binlog", "verdict": "good"},
          {"text": "热点 key 过期怎么办？", "time": "27:05", "answer": "加锁", "verdict": "improve", "better": "互斥锁重建 + 逻辑过期。"}
        ]}, {"id": "improve", "items": [{"text": "先说结论", "time": "27:05"}]}]}
        """
        let notes = try #require(NotesJSON.parse(reply, template: template, language: "zh"))
        #expect(notes.questionCount == 2 && notes.improveCount == 1)
        #expect(notes.bankEntries == [BankEntry(question: "热点 key 过期怎么办？", answer: "互斥锁重建 + 逻辑过期。")])
        #expect(notes.lessons(template: template) == ["先说结论"])
    }

    @Test func correctionsNeverChangeTheTranscript() {
        let start = monday
        let lines = [SessionTranscriptLine(date: start, speaker: .remote, text: "缓存鸡穿怎么处理？"),
                     SessionTranscriptLine(date: start.addingTimeInterval(5), speaker: .me, text: "用互斥锁。")]
        let reply = #"{"corrections": [{"line": 7, "from": "缓存鸡穿", "to": "缓存击穿"}, {"line": 1, "from": "不存在", "to": "x"}, {"line": 0, "from": "一样", "to": "一样"}]}"#
        let corrections = NotesCorrections.valid(NotesJSON.corrections(reply), lines: lines)
        // A misnumbered fix is moved to the line it belongs to; fixes that match nothing are dropped.
        #expect(corrections.count == 1 && corrections[0].line == 0)
        let fixed = NotesCorrections.apply(corrections, to: lines)
        #expect(fixed[0].text == "缓存击穿怎么处理？" && lines[0].text == "缓存鸡穿怎么处理？")
        var undone = corrections
        undone[0].reverted = true
        #expect(NotesCorrections.apply(undone, to: lines)[0].text == "缓存鸡穿怎么处理？")
        // Stored notes keep corrections across a save.
        let notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "", sections: [], corrections: corrections)
        let decoded = try? JSONLines.decoder().decode(StructuredNotes.self, from: JSONLines.encoder().encode(notes))
        #expect(decoded?.corrections == corrections)
    }

    @Test func roomSpeakerIsLabelled() throws {
        #expect(SessionSpeaker.room.label == "现场")
        let line = SessionTranscriptLine(date: monday, speaker: .room, text: "小王你负责周四交付")
        let decoded = try JSONLines.decoder().decode(SessionTranscriptLine.self, from: JSONLines.encoder().encode(line))
        #expect(decoded.speaker == .room)
        #expect(NotesPrompt.transcript([line], start: monday).hasSuffix("现场：小王你负责周四交付"))
    }
}
