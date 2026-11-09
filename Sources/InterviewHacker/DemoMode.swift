import AppKit
import Foundation
import InterviewHackerCore
import SwiftUI

/// Screenshot / demo mode. Launch with `INTERVIEWHACKER_DEMO=1` to show a realistic
/// sample session without capturing audio or calling any model. Used by the
/// screenshots workflow to render README images on a real macOS runner.
///
/// Environment:
/// - `INTERVIEWHACKER_DEMO_SECTION`: home | prepare | live | live-notes | meetings | library |
///   settings (the older names assistant, materials and history still work)
/// - `INTERVIEWHACKER_DEMO_APPEARANCE`: light | dark
/// - `INTERVIEWHACKER_DEMO_OUT`: file that receives the window numbers as JSON
/// - `INTERVIEWHACKER_DEMO_FRAME`: x,y,width,height (screen coordinates): show the demo in
///   a window there, without activating the app (launch with `open -j`)
/// - `INTERVIEWHACKER_DEMO_LAYOUT`: an assistant layout preset (standard, presenting…)
/// - `INTERVIEWHACKER_DEMO_SNAPSHOT`: PNG the app renders its own main window into
///   (no Screen Recording permission needed)
enum DemoMode {
    static var isEnabled: Bool {
        if let override = ProcessInfo.processInfo.environment["INTERVIEWHACKER_DEMO"] { return override == "1" }
        return Bundle.main.object(forInfoDictionaryKey: "InterviewHackerReviewMode") as? Bool == true
            && Bundle.main.object(forInfoDictionaryKey: "InterviewHackerReviewDemo") as? Bool == true
    }

    static var speechReady: Bool { isEnabled && (ProcessInfo.processInfo.environment["INTERVIEWHACKER_DEMO_VOICE_READY"] == "1" || Bundle.main.object(forInfoDictionaryKey: "InterviewHackerReviewDemo") as? Bool == true) }

    @MainActor
    static func apply() {
        guard isEnabled else { return }
        var environment = ProcessInfo.processInfo.environment
        if Bundle.main.object(forInfoDictionaryKey: "InterviewHackerReviewDemo") as? Bool == true {
            environment["INTERVIEWHACKER_DEMO_SECTION"] = environment["INTERVIEWHACKER_DEMO_SECTION"] ?? "home"
            environment["INTERVIEWHACKER_DEMO_VOICE_READY"] = environment["INTERVIEWHACKER_DEMO_VOICE_READY"] ?? "1"
        }
        // Each capture starts from the default tabs; earlier captures must not leak in.
        for key in ["settingsGroup", "workspaceEditorTab", "demoOnboarding", "onboardingStep", "quickStartStep", "demoNotes"] {
            UserDefaults.standard.removeObject(forKey: key)
        }
        // INTERVIEWHACKER_DEMO_DEFAULTS="key=value;key=value" opens specific tabs.
        for pair in (environment["INTERVIEWHACKER_DEMO_DEFAULTS"] ?? "").split(separator: ";") {
            let parts = pair.split(separator: "=", maxSplits: 1).map(String.init)
            guard parts.count == 2 else { continue }
            if let number = Int(parts[1]) { UserDefaults.standard.set(number, forKey: parts[0]) } else { UserDefaults.standard.set(parts[1], forKey: parts[0]) }
        }
        let state = AppState.shared
        if let path = environment["INTERVIEWHACKER_DEMO_SCREENSHOT_PREVIEW"], let image = NSImage(contentsOfFile: path) {
            state.screenshotPreview = image
            if let screenshotPath = environment["INTERVIEWHACKER_DEMO_SCREENSHOT_URL"] {
                state.screenshotURL = URL(fileURLWithPath: screenshotPath)
            }
        }
        // Screenshots must be able to see the windows.
        state.capturePrivacy = false
        TeleprompterController.shared.applyPrivacy(false)
        state.appearance = environment["INTERVIEWHACKER_DEMO_APPEARANCE"] ?? "dark"
        state.applyAppearance()
        state.section = AppSection(demoName: environment["INTERVIEWHACKER_DEMO_SECTION"] ?? "") ?? .home
        state.populateDemo()
        if environment["INTERVIEWHACKER_DEMO_API"] == "1" {
            state.answerBackend = .openAICompatible; state.apiBaseURL = "https://example.invalid/v1"
            state.apiModel = "example-model"; state.apiKey = ""
        }
        // Only the live page shows a running meeting; every other section is the
        // normal window with the sidebar.
        let demoSection = environment["INTERVIEWHACKER_DEMO_SECTION"] ?? "assistant"
        if environment["INTERVIEWHACKER_DEMO_IDLE"] == "1" || !["assistant", "live", "live-notes"].contains(demoSection) { state.isRunning = false }
        if demoSection == "live-notes" { state.automaticAnswers = false }
        if demoSection == "prepare" { state.populateDemoDraft() }
        if let advanced = environment["INTERVIEWHACKER_DEMO_ADVANCED"] { state.simplifiedInterface = advanced != "1" }
        if environment["INTERVIEWHACKER_DEMO_VOICE_READY"] == "1" {
            state.transcriptionBackend = "zipformer"; state.microphoneAccess = .granted
        }
        state.documentModelsChecked = true
        if environment["INTERVIEWHACKER_DEMO_DOCUMENTS_READY"] == "1" {
            state.installedDocumentModels = Set(ModelCatalog.all.map(\.id)); state.runtimeInstalled = true
        }
        if let provider = environment["INTERVIEWHACKER_DEMO_DOCUMENT_PROVIDER"].flatMap(DocumentOCRProvider.init(rawValue:)) {
            state.documentOCRProvider = provider
        }
        if let semantic = environment["INTERVIEWHACKER_DEMO_SEMANTIC"] { state.semanticRetrievalEnabled = semantic == "1" }
        if let preset = AssistantLayout.Preset(rawValue: environment["INTERVIEWHACKER_DEMO_LAYOUT"] ?? "") { state.assistantLayout = preset.layout }
        // INTERVIEWHACKER_DEMO_ONBOARDING=1 shows the guide (step via quickStartStep).
        if environment["INTERVIEWHACKER_DEMO_ONBOARDING"] == "1" || UserDefaults.standard.integer(forKey: "demoOnboarding") == 1 {
            state.isOnboardingPresented = true
        }

        // INTERVIEWHACKER_DEMO_FRAME: the demo on one display while the others stay in use.
        let frame = (environment["INTERVIEWHACKER_DEMO_FRAME"] ?? "").split(separator: ",").compactMap { Double($0) }
        if frame.count == 4 {
            // A window of its own: a hidden launch does not always create the SwiftUI one.
            let window = NSWindow(contentRect: NSRect(x: frame[0], y: frame[1], width: frame[2], height: frame[3]),
                                  styleMask: [.titled, .closable, .resizable, .fullSizeContentView], backing: .buffered, defer: false)
            window.titlebarAppearsTransparent = true
            window.titleVisibility = .hidden
            window.isReleasedWhenClosed = false
            window.contentView = NSHostingView(rootView: ContentView().environment(state))
            window.setFrame(NSRect(x: frame[0], y: frame[1], width: frame[2], height: frame[3]), display: true)
            if environment["INTERVIEWHACKER_DEMO_HIDDEN"] != "1" { window.orderFront(nil) }
            demoWindow = window
            DispatchQueue.main.asyncAfter(deadline: .now() + 2.5) { snapshot(window, to: environment["INTERVIEWHACKER_DEMO_SNAPSHOT"]) }
            return
        }
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
            snapshot(NSApp.windows.first(where: MainWindowLayout.isMainWindow), to: environment["INTERVIEWHACKER_DEMO_SNAPSHOT"])
            guard let path = environment["INTERVIEWHACKER_DEMO_OUT"] else { return }
            let main = NSApp.windows.first(where: { !($0 is NSPanel) && $0.isVisible })?.windowNumber ?? 0
            let panel = NSApp.windows.first(where: { $0 is NSPanel && $0.isVisible })?.windowNumber ?? 0
            try? JSONSerialization.data(withJSONObject: ["main": main, "teleprompter": panel])
                .write(to: URL(fileURLWithPath: path))
        }
    }
}

extension DemoMode {
    @MainActor static var demoWindow: NSWindow?

    /// Renders a window's own content into a PNG (no Screen Recording permission needed).
    @MainActor
    static func snapshot(_ window: NSWindow?, to path: String?) {
        guard let path, let view = window?.contentView?.superview,
              let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { return }
        view.cacheDisplay(in: view.bounds, to: bitmap)
        try? bitmap.representation(using: .png, properties: [:])?.write(to: URL(fileURLWithPath: path))
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
        let spoken = Skill(name: "面试口语化", summary: "回答像真人说话，先结论后例子", content: "先用一句话回答，再讲一个我做过的具体例子；不用“首先其次最后”。", global: true)
        let design = Skill(name: "系统设计答题框架", summary: "需求 → 估算 → 架构 → 取舍", content: "系统设计题按 需求澄清、容量估算、核心架构、关键取舍 四步回答。")
        let rebuttal = Skill(name: "学术答辩应对", summary: "承认局限、给出补充实验", content: "被质疑时先承认问题，再说明已有证据和计划中的补充实验。")
        SkillLibrary.shared.installDemo([spoken, design, rebuttal])
        interview.skillIDs = [design.id]
        workspaces.installDemo([interview, academic, business])
        capturePermissionGranted = true
        codexLoginStatus = "Logged in using ChatGPT"
        codexVersion = "codex-cli 0.160.0"
        codexModels = [CodexModelOption(id: "gpt-5.5", name: "GPT-5.5", efforts: ["low", "medium", "high"], defaultEffort: "low", fastTier: "1.5x speed, increased usage"),
                       CodexModelOption(id: "gpt-6-luna", name: "GPT-6 Luna", efforts: ["low", "medium", "high"], defaultEffort: "low", fastTier: "1.5x speed", defaultsToFast: true)]
        codexModelID = "gpt-5.5"
        codexReasoning = "low"
        codexFastSetting = true
        captureTargets = [CaptureTarget(id: "window:1", title: "Zoom · 后端工程师二面"), CaptureTarget(id: "display:1", title: "整个屏幕 · 3024×1964")]
        selectedTargetID = "window:1"
        isRunning = true
        automaticAnswers = true
        plannedMeeting = MeetingPlan(title: "字节跳动 · 后端二面")
        sessionStartedAt = now.addingTimeInterval(-754)
        statusText = "正在聆听 · 启动 0.4 s"
        transcriptionStatus = "X-ASR 本地识别 · RTF 0.08 · 精修 0.12 s"
        lastRTF = 0.08
        lastRefineSeconds = 0.12
        lastFirstTokenSeconds = 0.9
        lastAnswerSeconds = 4.2
        lastAnswerModel = "gpt-5.5"
        entries = [
            entry(95, .system, "好，我们先聊聊你最近做的项目吧，简单介绍一下你负责的部分。"),
            entry(80, .microphone, "我主要负责订单服务的缓存层，把核心查询从数据库迁移到 Redis。"),
            entry(58, .system, "嗯，这个挺关键的。"),
            entry(52, .system, "那数据库和缓存同时更新的时候，你们是怎么保证一致性的？"),
            { var echo = entry(51, .microphone, "数据库和缓存同时更新的时候你们是怎么保证一致性的"); echo.suspectedEcho = true; return echo }(),
            entry(20, .microphone, "我们用的是先更新数据库、再删除缓存的方式。"),
        ]
        questionTexts = ["好，我们先聊聊你最近做的项目吧，简单介绍一下你负责的部分。", "那数据库和缓存同时更新的时候，你们是怎么保证一致性的？"]
        partialTranscript = "如果删除缓存失败了怎么办"
        echoDroppedCount = 1
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
        currentAnswerID = answerHistory.last?.id
        if let id = currentAnswerID {
            answerCoordinator.begin(id: id, question: question)
            answerCoordinator.update(id: id) { evidence in
                evidence.evidence.retrieval = "演示：关键词查找，未调用模型"
                evidence.evidence.materials = selectedExcerpts
            }
            answerCoordinator.finish(id: id, text: reply)
        }
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
        var reviewMeta = meta
        reviewMeta.improveCount = 1
        let review = SessionDetail(
            meta: reviewMeta, lines: lines,
            answers: answerHistory.map { SessionAnswerEvent(date: started.addingTimeInterval(300), question: $0.question, answer: $0.answer, model: $0.model, firstTokenSeconds: $0.firstTokenSeconds, automatic: true) },
            questions: [SessionQuestionEvent(date: started.addingTimeInterval(120), text: question, intent: "question", score: 0.95, disposition: "auto")],
            context: nil,
            summary: SessionSummary(title: parsed.title ?? "", markdown: summaryMarkdown, questions: parsed.questions, generatedAt: started.addingTimeInterval(2_700), model: "gpt-5.5",
                                    lessons: ["一致性问题先画读写时序，再说兜底方案"], bank: [BankEntry(question: "热点 key 过期的一瞬间大量请求打到数据库，怎么办？", answer: "先给结论：互斥锁重建 + 逻辑过期，二选一看一致性要求。布隆过滤器解决的是穿透，不是击穿。")], appliedToWorkspace: false),
            notes: Self.demoReviewNotes(started: started))
        let weekly = Self.demoWeeklyMeeting(now: now, workspace: business)
        archive.installDemo([weekly, review])
        let showsWork = ProcessInfo.processInfo.environment["INTERVIEWHACKER_DEMO_NOTES"] == "work"
            || UserDefaults.standard.string(forKey: "demoNotes") == "work"
        historySelection = showsWork ? weekly.meta.id : meta.id
    }

    /// The preparation page of the next interview (`INTERVIEWHACKER_DEMO_SECTION=prepare`).
    func populateDemoDraft() {
        guard let series = workspaces.visible.first(where: { $0.mode == .interview }) else { return }
        var draft = MeetingDraft()
        draft.title = "字节跳动 · 后端二面"
        draft.mode = .interview
        draft.suggestedSeriesID = series.id
        draft.seriesID = series.id
        draft.baselineDocumentIDs = Set(series.documents.map(\.id))
        meetingDraft = draft
        section = .home
    }

    /// Interview review notes matching docs/redesign/screenshots/ReviewPage-zh-light.png.
    static func demoReviewNotes(started: Date) -> StructuredNotes {
        let template = NotesTemplate.builtIn(.interview)
        func section(_ id: String, _ items: [NotesItem]) -> NotesSectionResult {
            let spec = template.sections.first { $0.id == id }!
            return NotesSectionResult(id: id, title: spec.title, kind: spec.kind, isGap: spec.isGap, items: items)
        }
        return StructuredNotes(templateID: template.id, templateName: template.name, language: "zh", title: "字节跳动 · 后端二面", sections: [
            section("impression", [NotesItem(text: "项目部分讲得扎实、有数字；系统设计追问时结论来得太慢，缓存击穿的方案仍然没讲完整。")]),
            section("questions", [
                NotesItem(text: "如果删除缓存失败了怎么办？", time: "18:22", answer: "重试队列 + binlog 异步删除，两道保险。", verdict: "good"),
                NotesItem(text: "热点 key 过期的一瞬间大量请求打到数据库，怎么办？", time: "27:05", answer: "加锁……（停顿 8 秒）……可能用布隆过滤器。", verdict: "improve",
                          better: "先给结论：互斥锁重建 + 逻辑过期，二选一看一致性要求。布隆过滤器解决的是穿透，不是击穿，别混用。"),
            ]),
            section("missed", [NotesItem(text: "简历里的“大促 8 万 QPS 零故障”一次也没提到", time: "31:40"),
                               NotesItem(text: "职位描述里的“抖音电商订单链路”没有主动联系起来", time: "40:12")]),
            section("improve", [NotesItem(text: "系统设计题先说结论，再展开", time: "27:05"),
                                NotesItem(text: "准备好击穿、穿透、雪崩三者的区别", time: "27:30"),
                                NotesItem(text: "主动带出大促数据", time: "31:40")]),
        ], corrections: [TranscriptCorrection(line: 3, original: "缓存鸡穿", corrected: "缓存击穿")], generatedAt: started.addingTimeInterval(2_700), model: "gpt-5.5")
    }

    /// A work meeting with notes matching docs/redesign/screenshots/SummaryPage-zh-light.png.
    static func demoWeeklyMeeting(now: Date, workspace: Workspace) -> SessionDetail {
        let started = Calendar.current.date(bySettingHour: 10, minute: 0, second: 0, of: now) ?? now.addingTimeInterval(-7_200)
        var meta = SessionMeta(startedAt: started, endedAt: started.addingTimeInterval(38 * 60), mode: .meeting,
                               title: "周一产品例会", audioSource: "电脑声音", recognizer: "X-ASR 中英 · 480 ms + 整句精修", answerModel: "gpt-5.5",
                               transcriptCount: 4, questionCount: 0, answerCount: 0, summaryState: .ready,
                               workspaceID: workspace.id, workspaceName: workspace.name)
        meta.actionItemCount = 3
        let lines = [
            SessionTranscriptLine(date: started.addingTimeInterval(192), speaker: .remote, text: "这个版本下周三之前必须上线，客户那边已经在催了。"),
            SessionTranscriptLine(date: started.addingTimeInterval(295), speaker: .remote, text: "好，那就这么定。支付那两个 bug 今天修掉。"),
            SessionTranscriptLine(date: started.addingTimeInterval(340), speaker: .me, text: "回归测试我来排，周二下午给结果。"),
            SessionTranscriptLine(date: started.addingTimeInterval(750), speaker: .remote, text: "设计稿周四之前，或者周五也行，到时候再看。"),
            SessionTranscriptLine(date: started.addingTimeInterval(1_268), speaker: .remote, text: "客户要的导出功能，目前还没人认领。"),
        ]
        let template = NotesTemplate.builtIn(.work)
        func section(_ id: String, _ items: [NotesItem]) -> NotesSectionResult {
            let spec = template.sections.first { $0.id == id }!
            return NotesSectionResult(id: id, title: spec.title, kind: spec.kind, isGap: spec.isGap, items: items)
        }
        var notes = StructuredNotes(templateID: template.id, templateName: template.name, language: "zh", title: "周一产品例会", sections: [
            section("summary", [NotesItem(text: "新版本下周三前上线；支付 bug 今天修，回归测试周二出结果。")]),
            section("decisions", [NotesItem(text: "新版本下周三前上线", time: "03:12")]),
            section("actions", [NotesItem(text: "回归测试并给出结果", time: "05:40", owner: "我", due: "周二下午"),
                                NotesItem(text: "修复支付相关的 2 个 bug", time: "04:55", owner: "对方", due: "今天"),
                                NotesItem(text: "设计稿复审", time: "12:30", due: "周四", uncertain: true)]),
            section("risks", [NotesItem(text: "客户要求的导出功能没人认领", time: "21:08"),
                              NotesItem(text: "设计稿复审是周四还是周五，两种说法", time: "12:30", uncertain: true)]),
        ], corrections: [], generatedAt: started.addingTimeInterval(40 * 60), model: "gpt-5.5")
        NotesVerifier.verify(&notes, lineOffsets: lines.map { Int($0.date.timeIntervalSince(started)) }, duration: meta.duration, meetingDate: started)
        let summary = SessionSummary(title: "周一产品例会", markdown: NotesMarkdown.render(notes, title: "周一产品例会", facts: ""),
                                     questions: [], generatedAt: notes.generatedAt, model: "gpt-5.5", lessons: [], bank: [], appliedToWorkspace: true)
        return SessionDetail(meta: meta, lines: lines, answers: [], questions: [], context: nil, summary: summary, notes: notes)
    }
}
