import Foundation
import Testing
@testable import NextThoughtCore

@Suite struct LiveNotesTests {
    private func lines(_ count: Int) -> [LiveNotesPrompt.Line] {
        (0..<count).map { LiveNotesPrompt.Line(seconds: $0 * 20, speaker: $0 % 2 == 0 ? "老师" : "我", text: "第 \($0) 句的内容") }
    }

    @Test func theFirstNotesComeEarlyAndLaterOnesOnTheInterval() {
        var notes = LiveNotes()
        #expect(!LiveNotesSchedule.isDue(notes: notes, lines: 0, sinceLastAttempt: 100))
        #expect(LiveNotesSchedule.isDue(notes: notes, lines: 3, sinceLastAttempt: 1))
        #expect(!LiveNotesSchedule.isDue(notes: notes, lines: 2, sinceLastAttempt: 5))
        #expect(LiveNotesSchedule.isDue(notes: notes, lines: 1, sinceLastAttempt: 21))
        notes.summary = "讨论上线计划"; notes.covered = 3
        #expect(!LiveNotesSchedule.isDue(notes: notes, lines: 3, sinceLastAttempt: 300))
        #expect(!LiveNotesSchedule.isDue(notes: notes, lines: 8, sinceLastAttempt: 30))
        #expect(LiveNotesSchedule.isDue(notes: notes, lines: 8, sinceLastAttempt: 46))
    }

    @Test func aReplyBecomesNotesThatCoverTheLinesSent() throws {
        let reply = """
        好的，这是更新后的纪要：
        {"summary": "讨论新版本的上线范围。", "topics": [{"title": "上线计划", "points": ["下周三前上线", "支付问题今天修"]}],
         "decisions": ["周三上线范围不变"], "todos": [{"text": "回归测试", "owner": "我"}, {"text": "起草说明", "owner": null}]}
        """
        let notes = try #require(LiveNotesPrompt.parse(reply, previous: LiveNotes(), lines: 6))
        #expect(notes.summary == "讨论新版本的上线范围。")
        #expect(notes.topics.map(\.title) == ["上线计划"])
        #expect(notes.todos == [LiveNotes.Todo(text: "回归测试", owner: "我"), LiveNotes.Todo(text: "起草说明")])
        #expect(notes.covered == 6)
        #expect(notes.statements.contains("周三上线范围不变"))
    }

    @Test func settledTopicsStayAndAreNotSentBackForRewriting() throws {
        var previous = LiveNotes()
        previous.summary = "三个议题"
        previous.topics = [.init(title: "上线计划", points: ["周三上线"]), .init(title: "导出功能", points: ["下个版本做"]), .init(title: "客户说明", points: ["周五前给"])]
        previous.covered = 9
        let prompt = LiveNotesPrompt.user(notes: previous, lines: lines(12), language: "简体中文")
        #expect(prompt.contains("较早的议题（已定稿"))
        #expect(prompt.contains("- 上线计划"))
        #expect(!prompt.contains("\"周三上线\""))
        #expect(prompt.contains("第 9 句的内容") && !prompt.contains("第 6 句的内容"))
        // The reply holds the last two topics and a new one; the first is laid back in front.
        let reply = #"{"summary": "四个议题", "topics": [{"title": "导出功能", "points": ["下个版本做", "小王接手"]}, {"title": "客户说明", "points": ["周五前给"]}, {"title": "回归测试", "points": ["周二出结果"]}], "decisions": [], "todos": []}"#
        let notes = try #require(LiveNotesPrompt.parse(reply, previous: previous, lines: 12))
        #expect(notes.topics.map(\.title) == ["上线计划", "导出功能", "客户说明", "回归测试"])
        #expect(notes.topics[1].points.count == 2)
    }

    @Test func anUnreadableOrEmptyReplyNeverReplacesTheNotes() {
        var previous = LiveNotes()
        previous.summary = "已有内容"; previous.topics = [.init(title: "上线计划", points: ["周三上线"])]
        #expect(LiveNotesPrompt.parse("抱歉，我无法处理。", previous: previous, lines: 5) == nil)
        #expect(LiveNotesPrompt.parse(#"{"summary": "", "topics": []}"#, previous: previous, lines: 5) == nil)
        #expect(LiveNotesPrompt.parse(#"{"summary": "", "topics": [{"title": "别的", "points": ["一点"]}]}"#, previous: previous, lines: 5) == nil)
    }

    @Test func recordingTimeAndQuestionsAddUpByMonthAndBySeries() {
        let calendar = Calendar(identifier: .gregorian)
        func meeting(_ day: Int, month: Int = 11, minutes: Double, questions: Int, answers: Int, series: UUID? = nil, ended: Bool = true) -> SessionMeta {
            let start = calendar.date(from: DateComponents(year: 2026, month: month, day: day, hour: 10))!
            var meta = SessionMeta(startedAt: start, endedAt: ended ? start.addingTimeInterval(minutes * 60) : nil, mode: .labMeeting,
                                   audioSource: "", recognizer: "", answerModel: "", questionCount: questions, answerCount: answers)
            meta.workspaceID = series
            return meta
        }
        let lab = UUID()
        let all = [meeting(3, minutes: 62, questions: 4, answers: 2, series: lab), meeting(18, minutes: 30.7, questions: 9, answers: 9),
                   meeting(20, month: 10, minutes: 45, questions: 1, answers: 0, series: lab), meeting(19, minutes: 99, questions: 0, answers: 0, ended: false)]
        let month = MeetingStats.month(of: calendar.date(from: DateComponents(year: 2026, month: 11, day: 20))!, calendar: calendar)
        let november = MeetingStats.of(all, in: month)
        #expect(november.meetings == 3 && november.questions == 13 && november.answers == 11)
        #expect(november.hoursAndMinutes.hours == 1 && november.hoursAndMinutes.minutes == 32)
        #expect(MeetingStats.of(all).meetings == 4)
        #expect(MeetingStats.of(all, series: lab) == MeetingStats(meetings: 2, seconds: 107 * 60, questions: 5, answers: 2))
        #expect(MeetingFeatures.standard(prompting: false, speakers: true).liveNotes)
        #expect(!MeetingFeatures.standard(prompting: true, speakers: true).liveNotes)
    }
}
