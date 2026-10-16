import Foundation
import Testing
@testable import InterviewHackerCore

@Suite struct LiveInsightsTests {
    let deck = UUID()

    func pages(_ texts: [String]) -> [SlidePage] {
        texts.enumerated().map { SlidePage(documentID: deck, documentName: "talk.pptx", page: $0.offset + 1, pageCount: texts.count, text: $0.element) }
    }

    var talk: [SlidePage] {
        pages([
            "基于稀疏注意力的长文本建模 张三 清华大学 2026",
            "研究背景 长文本推理的显存瓶颈 注意力复杂度随长度平方增长 现有方法精度下降",
            "方法 动态稀疏注意力 局部窗口加全局令牌 可学习的路由选择关键块",
            "实验结果 在 LongBench 上平均提升 3.2 分 推理速度提升 2.1 倍 显存降低 40%",
            "总结与未来工作 稀疏注意力兼顾精度与效率 未来扩展到多模态",
        ])
    }

    @Test func findsTheSlideOnScreen() {
        let screen = "PowerPoint 幻灯片放映 实验结果 在 LongBench 上平均提升 3.2 分 推理速度提升 2.1 倍 显存降低 40% 4 / 5"
        let ranked = SlideMatcher.rank(screen: screen, pages: talk)
        #expect(ranked.first?.page.page == 4)
        #expect(SlideMatcher.decide(ranked) == .page(talk[3]))
    }

    @Test func presenterViewWithNextSlidePreviewPicksCurrent() {
        // Presenter view: the current slide (3) and the next one (4) are both on screen.
        let screen = "当前幻灯片 方法 动态稀疏注意力 局部窗口加全局令牌 可学习的路由选择关键块 下一张 实验结果 在 LongBench 上平均提升 3.2 分 推理速度提升 2.1 倍 显存降低 40% 备注 讲清楚路由"
        let outcome = SlideMatcher.decide(SlideMatcher.rank(screen: screen, pages: talk))
        #expect(outcome == .page(talk[2]))
    }

    @Test func unrelatedScreenMatchesNothing() {
        let screen = "微信 文件传输助手 今天下午三点开会 收到"
        #expect(SlideMatcher.decide(SlideMatcher.rank(screen: screen, pages: talk)) == .none)
    }

    @Test func decksAreRecognisedByName() {
        #expect(SlideMatcher.isDeck(name: "talk.pptx", pageCount: 1))
        #expect(SlideMatcher.isDeck(name: "slides.pdf", pageCount: 12))
        #expect(!SlideMatcher.isDeck(name: "resume.pdf", pageCount: 1))
        #expect(!SlideMatcher.isDeck(name: "notes.md", pageCount: 5))
    }

    @Test func sameScreenIsNotJudgedTwice() {
        let a = "def two_sum(nums, target):\n    seen = {}\n    for i, n in enumerate(nums):\n        if target - n in seen:\n            return [seen[target - n], i]\n        seen[n] = i"
        #expect(ScreenText.isSameScreen(a, a + " 10:42"))
        #expect(!ScreenText.isSameScreen(a, "class LRUCache: def get(self, key): def put(self, key, value):"))
    }

    @Test func heuristicRelevanceFollowsTheConversation() {
        let screen = "LeetCode 146. LRU 缓存 请你设计并实现一个满足 LRU (最近最少使用) 缓存约束的数据结构"
        #expect(ScreenText.heuristicRelevance(screen: screen, recentSpeech: "你看一下屏幕上这道 LRU 缓存的题，设计一个数据结构"))
        #expect(!ScreenText.heuristicRelevance(screen: screen, recentSpeech: "先简单做个自我介绍吧，讲讲你最近的项目"))
    }

    @Test func slideNotesParse() {
        let text = """
        【要点】
        - 这一页我想讲清楚 **动态稀疏注意力** 的核心思路。
        - 我们用局部窗口加全局令牌。
        2. 路由是可学习的，会挑出关键块。
        【过渡】
        那这个方法效果怎么样？我们看实验。
        【可能被问】
        - 路由的开销有多大？
        - 和 Longformer 比有什么不同？
        """
        let notes = SlideNotes.parse(text)
        #expect(notes?.points.count == 3)
        #expect(notes?.points.first == "这一页我想讲清楚 动态稀疏注意力 的核心思路。")
        #expect(notes?.transition == "那这个方法效果怎么样？我们看实验。")
        #expect(notes?.questions.count == 2)
        #expect(SlideNotes.parse("") == nil)
    }

    @Test func slideNotesPromptCarriesTheSlide() {
        let prompt = SlideNotes.prompt(page: talk[2], previous: talk[1].text, next: talk[3].text, excerpts: [], mode: .academic, preferences: "")
        #expect(prompt.user.contains("当前第 3 / 5 页"))
        #expect(prompt.user.contains("下一页"))
        #expect(prompt.system.contains("【要点】"))
    }

    @Test func answerSplitsIntoSpokenSentences() {
        let answer = """
        我觉得核心是**一致性和可用性的取舍**。比如说：
        - 写多读少的场景，我会先更新数据库再删缓存；
        - 读多写少的时候，可以加一个短的过期时间。
        ```python
        cache.delete(key)
        ```
        最后要监控缓存命中率！
        """
        let points = AnswerPoints.split(answer)
        #expect(points.first == "我觉得核心是一致性和可用性的取舍。")
        #expect(points.contains { $0.hasPrefix("比如说：写多读少") })
        #expect(!points.contains { $0.contains("cache.delete") })
        #expect(points.last == "最后要监控缓存命中率！")
    }

    @Test func heuristicCoverage() {
        #expect(AnswerPoints.heuristicCovered("写多读少的场景，我会先更新数据库再删缓存", speech: "嗯我觉得写多读少的场景的话我会先更新数据库然后再删缓存"))
        #expect(!AnswerPoints.heuristicCovered("最后要监控缓存命中率", speech: "我会先更新数据库再删缓存"))
        #expect(AnswerPoints.next(after: [0, 1], count: 4) == 2)
        #expect(AnswerPoints.next(after: [0, 1], count: 2) == nil)
    }

    // MARK: Decision tasks

    @Test func screenTaskAndVerdict() throws {
        let task = DecisionTasks.screen("LeetCode 146 LRU 缓存", recent: ["[对方] 看一下屏幕上的题"], mode: .interview)
        #expect(task.state.contains("LeetCode 146"))
        #expect(task.questions.count == 2)
        let verdict = DecisionTasks.screenVerdict([
            DecisionTasks.InsightKey.screenKind: DecisionAnswer(choice: "question", confidence: 0.9),
            DecisionTasks.InsightKey.screenRelevant: DecisionAnswer(yes: 0.82),
        ])
        #expect(verdict?.kind == .question)
        #expect(verdict?.isRelevant == true)
        #expect(DecisionTasks.screenVerdict([:]) == nil)
    }

    @Test func slideTaskRoundTrip() {
        let candidates = Array(talk[2...3])
        let task = DecisionTasks.slide("方法 动态稀疏注意力", candidates: candidates)
        guard case let .choice(_, options) = task.questions[DecisionTasks.InsightKey.slide] else { Issue.record("not a choice"); return }
        #expect(options.count == 3)
        #expect(options.last?.id == "none")
        let key = DecisionTasks.slideKey(candidates[0])
        let pick = DecisionTasks.slideChoice([DecisionTasks.InsightKey.slide: DecisionAnswer(choice: key, probabilities: [key: 0.8])], candidates: candidates)
        #expect(pick == candidates[0])
        #expect(DecisionTasks.slideChoice([DecisionTasks.InsightKey.slide: DecisionAnswer(choice: "none")], candidates: candidates) == nil)
        #expect(DecisionTasks.slideChoice([DecisionTasks.InsightKey.slide: DecisionAnswer(choice: key, probabilities: [key: 0.3])], candidates: candidates) == nil)
    }

    @Test func coverageTask() {
        let task = DecisionTasks.coverage(speech: "我会先更新数据库再删缓存", points: [(0, "先更新数据库再删缓存"), (3, "监控命中率")])
        #expect(task.questions.keys.sorted() == ["point_0", "point_3"])
        let covered = DecisionTasks.covered(["point_0": DecisionAnswer(yes: 0.9), "point_3": DecisionAnswer(yes: 0.2)])
        #expect(covered == [0])
    }

    @Test func questionKindAndSkills() {
        let task = DecisionTasks.questionKind("讲一个你和同事意见不合的经历", mode: .interview)
        guard case let .choice(_, options) = task.questions[DecisionTasks.InsightKey.questionKind] else { Issue.record("not a choice"); return }
        #expect(options.count == QuestionKind.allCases.count)
        #expect(DecisionTasks.questionKindChoice([DecisionTasks.InsightKey.questionKind: DecisionAnswer(choice: "behavioral", probabilities: ["behavioral": 0.7])]) == .behavioral)
        #expect(DecisionTasks.questionKindChoice([DecisionTasks.InsightKey.questionKind: DecisionAnswer(choice: "behavioral", probabilities: ["behavioral": 0.2])]) == nil)
        #expect(!QuestionKind.behavioral.hint.isEmpty)

        let skills = DecisionTasks.skills("实现 LRU", skills: [("算法题模板", "思路-代码-复杂度"), ("STAR 行为题", "")])
        #expect(skills.questions.count == 2)
        let drop = DecisionTasks.unrelatedSkills(["skill_0": DecisionAnswer(yes: 0.9), "skill_1": DecisionAnswer(yes: 0.1)], count: 3)
        #expect(drop == [1])
    }

    @Test func liveLineAndWebCheck() {
        let task = DecisionTasks.liveLine("那这个需求下周三之前你整理一份方案发给我", isMine: false, mode: .meeting)
        #expect(task.questions.count == 2)
        let verdict = DecisionTasks.lineVerdict([
            DecisionTasks.InsightKey.noteKind: DecisionAnswer(choice: "action_item", probabilities: ["action_item": 0.8]),
            DecisionTasks.InsightKey.wrapUp: DecisionAnswer(yes: 0.1),
        ])
        #expect(verdict?.note == .actionItem)
        #expect(verdict?.noteProbability == 0.8)
        let none = DecisionTasks.lineVerdict([
            DecisionTasks.InsightKey.noteKind: DecisionAnswer(choice: "none", probabilities: ["none": 0.9]),
            DecisionTasks.InsightKey.wrapUp: DecisionAnswer(yes: 0.95),
        ])
        #expect(none?.note == nil)
        #expect(none?.wrapUp == 0.95)
        let differ = DecisionTasks.answersDiffer(quick: "A", web: "B")
        #expect(differ.questions[DecisionTasks.InsightKey.differs] != nil)
    }

    @Test func promptCarriesScreenSlideAndHint() {
        let parts = AnswerPromptBuilder.makeParts(question: "路由开销有多大？", context: "", profile: "", turns: [], hasScreenshot: false, mode: .academic,
                                                  screen: "方法 动态稀疏注意力", currentSlide: ContextExcerpt(source: "talk.pptx", page: 3, text: "方法"),
                                                  answerHint: QuestionKind.numbers.hint)
        #expect(parts.user.contains("\"screen_text\""))
        #expect(parts.user.contains("\"current_slide\""))
        #expect(parts.user.contains("\"answer_hint\""))
        let plain = AnswerPromptBuilder.makeParts(question: "q", context: "", profile: "", turns: [], hasScreenshot: false)
        #expect(!plain.user.contains("screen_text"))
    }
}
