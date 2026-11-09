import Foundation
import Testing
@testable import NextThoughtCore

struct SessionRecordTests {
    let start = Date(timeIntervalSince1970: 1_800_000_000)

    @Test func jsonLinesSurviveATornLastLine() throws {
        var data = Data()
        data.append(try JSONLines.line(SessionTranscriptLine(date: start, speaker: .remote, text: "你好")))
        data.append(try JSONLines.line(SessionTranscriptLine(date: start.addingTimeInterval(2), speaker: .me, text: "你好，我是候选人")))
        data.append(Data("{\"id\":\"broken".utf8))
        let lines = JSONLines.decode(SessionTranscriptLine.self, from: data)
        #expect(lines.count == 2)
        #expect(lines[1].speaker == .me)
        #expect(lines[1].date == start.addingTimeInterval(2))
    }

    @Test func interruptedSessionReconcilesTranscriptCountBeforeSummaryRecovery() throws {
        var transcript = Data()
        transcript.append(try JSONLines.line(SessionTranscriptLine(date: start, speaker: .remote, text: "先确认范围")))
        transcript.append(try JSONLines.line(SessionTranscriptLine(date: start.addingTimeInterval(2), speaker: .me, text: "我来跟进")))
        let lastWrite = start.addingTimeInterval(3)
        var meta = SessionMeta(startedAt: start, mode: .interview, transcriptCount: 0, summaryState: .pending,
                               summaryError: "旧错误")

        let recoveredTranscript = meta.recoverInterruptedRecording(transcriptData: transcript, lastTranscriptWrite: lastWrite)
        #expect(recoveredTranscript)
        #expect(meta.transcriptCount == 2)
        #expect(meta.endedAt == lastWrite)
        #expect(meta.summaryState == .none)
        #expect(meta.summaryError == nil)

        var noTranscript = SessionMeta(startedAt: start, mode: .interview, summaryState: .pending)
        let recoveredEmptyTranscript = noTranscript.recoverInterruptedRecording(transcriptData: nil, lastTranscriptWrite: nil)
        #expect(!recoveredEmptyTranscript)
        #expect(noTranscript.transcriptCount == 0)
        #expect(noTranscript.summaryState == .none)
    }

    @Test func parsesTitleAndQuestionList() {
        let markdown = """
        # 标题
        **字节后端二面：缓存一致性**
        ## 摘要
        面试官主要考察缓存。
        ## 问题清单
        1. 你们怎么保证数据库和缓存一致？ —— 我的回答要点：先更新数据库再删缓存
        2. 如果删除缓存失败怎么办 —— 我的回答要点：未回答
        3) 介绍一下你自己
        ## 关键信息与结论
        - Redis
        """
        let parsed = SessionSummaryParser.parse(markdown)
        #expect(parsed.title == "字节后端二面：缓存一致性")
        #expect(parsed.questions.count == 3)
        #expect(parsed.questions[0].text == "你们怎么保证数据库和缓存一致？")
        #expect(parsed.questions[0].answerGist == "先更新数据库再删缓存")
        #expect(parsed.questions[2].text == "介绍一下你自己")
        #expect(parsed.questions[2].answerGist == nil)
    }

    @Test func longTranscriptsAreChunkedOnLineBoundaries() {
        let text = (0..<100).map { "[00:\($0)] 对方：" + String(repeating: "字", count: 50) }.joined(separator: "\n")
        let chunks = SessionSummaryPrompt.chunks(text, limit: 1_000)
        #expect(chunks.count > 1)
        #expect(chunks.allSatisfy { $0.count <= 1_000 })
        #expect(chunks.joined(separator: "\n") == text)
    }

    @Test func promptLabelsSpeakersAndOffsets() {
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(600), mode: .interview)
        let lines = [SessionTranscriptLine(date: start.addingTimeInterval(65), speaker: .remote, text: "介绍一下你自己"),
                     SessionTranscriptLine(date: start.addingTimeInterval(70), speaker: .me, text: "我叫小王")]
        let prompt = SessionSummaryPrompt.full(.init(meta: meta, lines: lines, answers: [], detected: [], context: nil))
        #expect(prompt.contains("[01:05] 对方：介绍一下你自己"))
        #expect(prompt.contains("[01:10] 我：我叫小王"))
        #expect(prompt.contains("时长：10 分钟"))
    }

    @Test func markdownExportContainsSummaryAnswersAndTranscript() {
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(120), mode: .meeting, audioSource: "Zoom")
        let summary = SessionSummary(title: "周会", markdown: "# 标题\n周会\n## 摘要\n讨论上线。", questions: [], generatedAt: start, model: "m")
        let text = SessionMarkdownExporter.export(meta: meta, summary: summary,
                                                  lines: [SessionTranscriptLine(date: start.addingTimeInterval(5), speaker: .remote, text: "什么时候上线")],
                                                  answers: [SessionAnswerEvent(date: start, question: "什么时候上线", answer: "下周三", model: "m", automatic: true)],
                                                  detected: [])
        #expect(text.hasPrefix("# 周会\n"))
        #expect(text.contains("## 摘要\n讨论上线。"))
        #expect(!text.contains("# 标题"))
        #expect(text.contains("### 什么时候上线"))
        #expect(text.contains("- `00:05` **对方**：什么时候上线"))
    }
}
