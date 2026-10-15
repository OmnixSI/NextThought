import AppKit
import Foundation
import InterviewHackerCore

/// Screenshot / demo mode. Launch with `INTERVIEWHACKER_DEMO=1` to show a realistic
/// sample session without capturing audio or calling any model. Used by the
/// screenshots workflow to render README images on a real macOS runner.
///
/// Environment:
/// - `INTERVIEWHACKER_DEMO_SECTION`: assistant | materials | settings | history
/// - `INTERVIEWHACKER_DEMO_APPEARANCE`: light | dark
/// - `INTERVIEWHACKER_DEMO_OUT`: file that receives the window numbers as JSON
enum DemoMode {
    static var isEnabled: Bool { ProcessInfo.processInfo.environment["INTERVIEWHACKER_DEMO"] == "1" }

    @MainActor
    static func apply() {
        guard isEnabled else { return }
        let environment = ProcessInfo.processInfo.environment
        let state = AppState.shared
        // Screenshots must be able to see the windows.
        state.capturePrivacy = false
        TeleprompterController.shared.applyPrivacy(false)
        state.appearance = environment["INTERVIEWHACKER_DEMO_APPEARANCE"] ?? "dark"
        state.applyAppearance()
        state.section = AppSection.allCases.first { "\($0)" == environment["INTERVIEWHACKER_DEMO_SECTION"] } ?? .assistant
        state.populateDemo()

        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
            NSApp.setActivationPolicy(.regular)
            NSApp.activate(ignoringOtherApps: true)
            if let main = NSApp.windows.first(where: { !($0 is NSPanel) }) {
                main.setFrame(NSRect(x: 60, y: 60, width: 1120, height: 820), display: true)
                main.makeKeyAndOrderFront(nil)
            }
            TeleprompterController.shared.show()
            if let panel = NSApp.windows.first(where: { $0 is NSPanel && $0.isVisible }) {
                panel.setFrame(NSRect(x: 900, y: 600, width: 680, height: 300), display: true)
            }
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 3) {
            guard let path = environment["INTERVIEWHACKER_DEMO_OUT"] else { return }
            let main = NSApp.windows.first(where: { !($0 is NSPanel) && $0.isVisible })?.windowNumber ?? 0
            let panel = NSApp.windows.first(where: { $0 is NSPanel && $0.isVisible })?.windowNumber ?? 0
            try? JSONSerialization.data(withJSONObject: ["main": main, "teleprompter": panel])
                .write(to: URL(fileURLWithPath: path))
        }
    }
}

@MainActor
extension AppState {
    func populateDemo() {
        let now = Date()
        func entry(_ secondsAgo: Double, _ source: CaptureSource, _ text: String) -> TranscriptEntry {
            let date = now.addingTimeInterval(-secondsAgo)
            return TranscriptEntry(date: date, source: source.rawValue, text: text, isFinal: true, startedAt: date, endedAt: date.addingTimeInterval(3))
        }
        let started = now.addingTimeInterval(-86_400)
        var interview = Workspace(name: "字节后端二面", mode: .interview,
                                  notes: "上一轮一面问了 MySQL 索引和 Redis，反馈是项目讲得不够量化。",
                                  fields: ["company": "字节跳动 · 抖音电商", "position": "后端开发工程师（2-1）", "interviewer": "二面面试官为订单中台负责人，偏重系统设计",
                                           "highlights": "订单服务缓存层改造：P99 从 120 ms 降到 18 ms；大促 8 万 QPS 零故障", "salary": "期望 45k × 15，两周内可到岗"],
                                  documents: [ContextDocument(name: "简历-2026.pdf", pages: ["订单服务缓存层，Redis，延迟双删，Canal 订阅 binlog"], category: "简历"),
                                              ContextDocument(name: "岗位JD-后端开发.md", pages: ["高并发、分布式缓存、消息队列"], category: "岗位 JD"),
                                              ContextDocument(name: "八股-缓存与数据库.md", pages: ["缓存穿透、击穿、雪崩；一致性方案对比"], category: "八股 / 面经"),
                                              ContextDocument(name: "抖音电商-团队介绍.pdf", pages: ["交易中台、订单履约"], category: "公司信息")],
                                  lessons: [WorkspaceLesson(text: "被问项目时先报规模和结果数字，再讲方案", pinned: true),
                                            WorkspaceLesson(text: "一致性问题先画读写时序，再说兜底方案"),
                                            WorkspaceLesson(text: "反问环节准备 2 个关于团队技术栈的问题")],
                                  questionBank: [QuestionBankItem(question: "数据库和缓存同时更新时怎么保证一致性？", answer: "先更新数据库再删除缓存；删除失败进入重试队列，并用 Canal 订阅 binlog 再删一次；热点 key 做延迟双删。", timesAsked: 2),
                                                 QuestionBankItem(question: "如果删除缓存失败了怎么办？", answer: "重试队列 + binlog 异步兜底，监控删除失败率并告警。", timesAsked: 1),
                                                 QuestionBankItem(question: "为什么选 Kafka 而不是 RabbitMQ？", answer: "吞吐和分区扩展性更好，订单事件需要按 key 有序且可回放。", timesAsked: 0)],
                                  stageReview: """
                                  ## 总体进展
                                  已完成 2 次模拟和 1 次真实面试，缓存相关问题回答越来越完整，项目量化表达明显改善。
                                  ## 高频问题
                                  - 缓存一致性（3 次）：先更新库再删缓存 + binlog 兜底
                                  - 项目难点（2 次）：大促 8 万 QPS 的容量评估
                                  ## 下一步准备清单
                                  1. 系统设计：秒杀库存扣减
                                  2. 补充一次线上事故的复盘数字
                                  """, stageReviewAt: now.addingTimeInterval(-3_600))
        interview.updatedAt = now
        let academic = Workspace(name: "ACL 2026 口头报告", mode: .academic,
                                 fields: ["title": "Streaming Code-Switching ASR with Punctuation-Aware Decoding", "venue": "ACL 2026 · Speech 分会场 · 15 分钟 + 5 分钟提问"],
                                 documents: [ContextDocument(name: "paper-final.pdf", pages: ["method"], category: "论文"),
                                             ContextDocument(name: "slides-v3.pptx", pages: ["slide"], category: "PPT / 幻灯片")])
        let business = Workspace(name: "周一客户例会 · 华东区", mode: .meeting,
                                 fields: ["goal": "确认二期报价与上线时间", "numbers": "二期报价 86 万；上线窗口 11 月 20 日"])
        workspaces.installDemo([interview, academic, business])
        capturePermissionGranted = true
        captureTargets = [CaptureTarget(id: "window:1", title: "Zoom · 后端工程师二面"), CaptureTarget(id: "display:1", title: "整个屏幕 · 3024×1964")]
        selectedTargetID = "window:1"
        isRunning = true
        sessionStartedAt = now.addingTimeInterval(-754)
        statusText = "正在聆听 · 启动 0.4 s"
        transcriptionStatus = "X-ASR 本地识别 · RTF 0.08 · 精修 0.12 s"
        lastRTF = 0.08
        lastRefineSeconds = 0.12
        lastFirstTokenSeconds = 0.9
        lastAnswerSeconds = 4.2
        lastAnswerModel = "gpt-5.5-mini"
        entries = [
            entry(95, .system, "好，我们先聊聊你最近做的项目吧，简单介绍一下你负责的部分。"),
            entry(80, .microphone, "我主要负责订单服务的缓存层，把核心查询从数据库迁移到 Redis。"),
            entry(58, .system, "嗯，这个挺关键的。"),
            entry(52, .system, "那数据库和缓存同时更新的时候，你们是怎么保证一致性的？"),
            entry(20, .microphone, "我们用的是先更新数据库、再删除缓存的方式。"),
        ]
        questionTexts = ["好，我们先聊聊你最近做的项目吧，简单介绍一下你负责的部分。", "那数据库和缓存同时更新的时候，你们是怎么保证一致性的？"]
        partialTranscript = "如果删除缓存失败了怎么办"
        echoDroppedCount = 2
        var demoTracker = QuestionCandidateTracker(mode: .interview)
        if case let .updated(candidate) = demoTracker.ingest("如果删除缓存失败了怎么办", isFinal: false, utteranceKey: "demo") {
            liveCandidate = candidate
        }
        let question = "那数据库和缓存同时更新的时候，你们是怎么保证一致性的？"
        let reply = """
        我们采用**先更新数据库、再删除缓存**，并用 binlog 订阅兜底，保证最终一致。

        - **Cache Aside**：写库成功后删除缓存，读请求未命中时回源并回填，避免并发写导致脏数据。
        - **删除失败兜底**：删除失败的 key 进入重试队列；同时用 **Canal 订阅 binlog** 异步再删一次。
        - **并发读写窗口**：对热点 key 做**延迟双删**（约 500 ms），把旧值被回填的概率降到很低。
        - **取舍**：这是最终一致方案，对强一致场景（如余额）直接读库或加分布式锁。
        """
        currentQuestion = question
        answer = reply
        answerHistory = [
            AnswerRecord(question: "简单介绍一下你负责的部分", answer: "我负责订单服务的缓存层……", model: "gpt-5.5-mini", seconds: 3.1, firstTokenSeconds: 0.8, automatic: true),
            AnswerRecord(question: question, answer: reply, model: "gpt-5.5-mini", seconds: 4.2, firstTokenSeconds: 0.9, automatic: true),
        ]
        let meta = SessionMeta(startedAt: started, endedAt: started.addingTimeInterval(2_640), mode: .interview,
                               title: "后端二面：缓存一致性与项目经历", audioSource: "Zoom · 后端工程师二面",
                               recognizer: "X-ASR 中英 · 480 ms + 整句精修", answerModel: "gpt-5.5-mini",
                               transcriptCount: entries.count, questionCount: 6, answerCount: 2, summaryState: .ready,
                               workspaceID: interview.id, workspaceName: interview.name)
        let summaryMarkdown = """
        # 标题
        后端二面：缓存一致性与项目经历
        ## 摘要
        面试官围绕订单服务缓存层展开，重点考察缓存与数据库的一致性、失败兜底和高并发下的取舍。整体回答完整，延迟双删与 binlog 兜底讲得清楚。
        ## 问题清单
        1. 简单介绍一下你负责的部分 —— 我的回答要点：订单服务缓存层，从数据库迁到 Redis
        2. 数据库和缓存同时更新时怎么保证一致性？ —— 我的回答要点：先更新数据库再删缓存，binlog 兜底
        3. 如果删除缓存失败了怎么办？ —— 我的回答要点：重试队列 + Canal 异步再删
        4. 热点 key 并发读写怎么处理？ —— 我的回答要点：延迟双删
        5. 什么场景下不能用最终一致？ —— 我的回答要点：余额等强一致场景直接读库
        6. 你还有什么问题想问我们？ —— 我的回答要点：问了团队的技术栈与值班制度
        ## 关键信息与结论
        - 团队使用 Redis Cluster + Canal
        - 下一轮是系统设计面
        ## 待办与跟进
        - 准备系统设计题：秒杀库存
        ## 复盘建议
        - 一致性问题可以先画时序，再讲方案
        - 补充一次线上缓存事故的真实数字
        ## 经验沉淀
        - 一致性问题先画读写时序，再说兜底方案
        ## 题库更新
        - 问：如果删除缓存失败了怎么办？
          答：重试队列 + binlog 异步兜底，监控删除失败率并告警。
        """
        let parsed = SessionSummaryParser.parse(summaryMarkdown)
        let lines = entries.enumerated().map { index, entry in
            SessionTranscriptLine(date: started.addingTimeInterval(Double(index) * 40 + 15), speaker: entry.speaker, text: entry.text)
        }
        archive.installDemo([SessionDetail(
            meta: meta, lines: lines,
            answers: answerHistory.map { SessionAnswerEvent(date: started.addingTimeInterval(300), question: $0.question, answer: $0.answer, model: $0.model, firstTokenSeconds: $0.firstTokenSeconds, automatic: true) },
            questions: [SessionQuestionEvent(date: started.addingTimeInterval(120), text: question, intent: "question", score: 0.95, disposition: "auto")],
            context: nil,
            summary: SessionSummary(title: parsed.title ?? "", markdown: summaryMarkdown, questions: parsed.questions, generatedAt: started.addingTimeInterval(2_700), model: "gpt-5.5-mini",
                                    lessons: ["一致性问题先画读写时序，再说兜底方案"], bank: [], appliedToWorkspace: true))])
        historySelection = meta.id
    }
}
