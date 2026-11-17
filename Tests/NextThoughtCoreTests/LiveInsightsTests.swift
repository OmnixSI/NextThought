import Foundation
import Testing
@testable import NextThoughtCore

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

    @Test func promptCarriesScreenAndSlide() {
        let parts = AnswerPromptBuilder.makeParts(question: "路由开销有多大？", context: "", profile: "", turns: [], hasScreenshot: false, mode: .academic,
                                                  screen: "方法 动态稀疏注意力", currentSlide: ContextExcerpt(source: "talk.pptx", page: 3, text: "方法"))
        #expect(parts.user.contains("\"screen_text\""))
        #expect(parts.user.contains("\"current_slide\""))
        let plain = AnswerPromptBuilder.makeParts(question: "q", context: "", profile: "", turns: [], hasScreenshot: false)
        #expect(!plain.user.contains("screen_text"))
    }
    // MARK: Following a real talk

    @Test func aReportIsNotADeck() {
        #expect(SlideMatcher.isDeck(name: "slides.pdf", pageCount: 30, averageCharacters: 420))
        #expect(!SlideMatcher.isDeck(name: "report.pdf", pageCount: 27, averageCharacters: 3_000))
        #expect(SlideMatcher.isDeck(name: "talk.pptx", pageCount: 40, averageCharacters: 3_000))
    }

    @Test func speakerNotesAreKeptApartFromTheSlide() {
        let slide = DocumentLocator(slide: 3)
        let blocks = DocumentStructure.parse("Scenario\nTwo co-sited arrays", locator: slide)
            + DocumentStructure.parse("### 演讲备注\n这一页是我们的通信场景。灯杆上装了两块阵列。", locator: slide)
        let parts = SlidePage.split(blocks)
        #expect(parts.text == "Scenario\nTwo co-sited arrays")
        #expect(parts.notes == "这一页是我们的通信场景。灯杆上装了两块阵列。")
    }

    @Test func aScriptIsTheSpeakersOwnWords() {
        let next = SlidePage(documentID: deck, documentName: "talk.pptx", page: 4, pageCount: 9, text: "# Signals the base station keeps\nSRS, probes, pilots")
        let page = SlidePage(documentID: deck, documentName: "talk.pptx", page: 3, pageCount: 9, text: "Scenario",
                             notes: "这一页是我们的通信场景。灯杆上装了两块阵列。右上角的小图是俯视图。")
        let script = SlideNotes.script(for: page, next: next)
        #expect(script?.isScript == true)
        #expect(script?.points == ["这一页是我们的通信场景。", "灯杆上装了两块阵列。", "右上角的小图是俯视图。"])
        #expect(script?.transition == "Signals the base station keeps")
        // A stub ("TODO") is not a script.
        #expect(SlideNotes.script(for: SlidePage(documentID: deck, documentName: "talk.pptx", page: 1, pageCount: 9, text: "Title", notes: "TODO"), next: nil) == nil)
        // Notes written before `isScript` existed still decode.
        let old = try? JSONDecoder().decode(SlideNotes.self, from: Data(#"{"points":["a"],"transition":"","questions":[]}"#.utf8))
        #expect(old?.isScript == false)
    }

    @Test func aFooterOnEverySlideDoesNotDecideThePage() {
        let footer = "Adaptive Beam Prediction Across Bands · A. Speaker · Example University 2026"
        let bodies = ["Scenario: a sub-6 GHz aided mmWave site with two co-sited arrays",
                      "Signals the base station keeps: fingerprints, probing beams and received pilots",
                      "Label-free adaptive prior learns the cross-band map from service pilots",
                      "Result: the harder the site, the larger the gain over the analytic map"]
        let pages = pages(bodies.map { $0 + "\n" + footer })
        // The screen shows slide 3 and, like every slide, the footer.
        let ranked = SlideMatcher.rank(screen: bodies[2] + " " + footer, pages: pages)
        #expect(ranked.first?.page.page == 3)
        #expect(SlideMatcher.decide(ranked) == .page(pages[2]))
        // Only the footer is readable (a slide that is one picture): no page is claimed.
        #expect(SlideMatcher.decide(SlideMatcher.rank(screen: footer, pages: pages)) != .page(pages[0]))
    }

    @Test func theNextSlideNeedsLessProofThanAJump() {
        let pages = pages(["alpha beta gamma delta epsilon zeta eta theta", "iota kappa lambda mu nu xi omicron pi rho", "sigma tau upsilon phi chi psi omega end"])
        let weak = [SlideMatch(page: pages[1], score: 0.38), SlideMatch(page: pages[2], score: 0.2)]
        #expect(SlideMatcher.decide(weak, previous: pages[0]) == .page(pages[1]))
        #expect(SlideMatcher.decide(weak, previous: nil) != .page(pages[1]))
    }
    @Test func theScriptBeingSpokenFindsItsSlide() {
        let scripts = ["First, the background. Wireless systems increasingly rely on learned models for channel estimation.",
                       "This slide shows the scenario. Two arrays are installed on one lamp post, and their normals differ by a yaw angle.",
                       "Now the results. The harder the deployment condition, the larger the gain over the analytic mapping."]
        let pages = scripts.enumerated().map { SlidePage(documentID: deck, documentName: "talk.pptx", page: $0.offset + 1, pageCount: 3, text: "Slide \($0.offset + 1)", notes: $0.element) }
        // Reading slide 2's script, as recognised (a word lost here and there), moves to slide 2 and not beyond.
        let heard = "this slide shows the scenario two arrays are installed on lamp post and their normals differ by a yaw angle"
        #expect(SlideMatcher.spokenPage(among: Array(pages[1...]), speech: heard)?.page == 2)
        #expect(SlideMatcher.spokenPage(among: [pages[2]], speech: heard) == nil)
        // English is told by its words: sharing letters with the script is not saying it.
        #expect(!AnswerPoints.heuristicCovered("The harder the deployment condition, the larger the gain", speech: "there is a garden behind the old theatre and children play in it together"))
        #expect(AnswerPoints.heuristicCovered("The harder the deployment condition, the larger the gain", speech: "so the harder the deployment gets the larger our gain"))
    }
    // MARK: Answers in one language, understood in another

    @Test func anEnglishAnswerCarriesItsMeaning() {
        // As the answer model writes it: the question first, a meaning under each sentence,
        // with or without the "译：" mark.
        let answer = """
        > 问：没有真实信道作标签，怎么确认学到的先验是对的？

        The prior is not guaranteed to be exactly correct without ground-truth labels.
        > 这个先验在没有真实标签时不能保证完全正确。

        I assess it through held-out service pilots and compare it with simpler baselines.
        > 译：我用留出的服务导频评估，并和更简单的基线比较。
        """
        #expect(BilingualAnswer.question(in: answer) == "没有真实信道作标签，怎么确认学到的先验是对的？")
        let lines = CueScript.lines(from: answer, style: .keyPoints)
        #expect(lines.map(\.text) == ["The prior is not guaranteed to be exactly correct without ground-truth labels.",
                                      "I assess it through held-out service pilots and compare it with simpler baselines."])
        #expect(lines.map(\.gloss) == ["这个先验在没有真实标签时不能保证完全正确。", "我用留出的服务导频评估，并和更简单的基线比较。"])
        // Only the English is to be said: coverage and the read-aloud script leave the Chinese out.
        #expect(AnswerPoints.split(answer).count == 2)
        #expect(!BilingualAnswer.spoken(answer).contains("先验"))
        // While it streams, the question's meaning is there before the first sentence.
        #expect(CueScript.lines(from: "> 问：怎么确认先验是对的？\n", style: .script).isEmpty)
    }

    @Test func anOrdinaryQuoteIsNotAGloss() {
        let answer = "这句话的出处是：\n> Premature optimization is the root of all evil.\n它提醒我们先度量。"
        #expect(!BilingualAnswer.isBilingual(answer))
        #expect(CueScript.lines(from: answer, style: .keyPoints).count == 3)
        #expect(BilingualAnswer.spoken(answer) == answer)
    }
    // MARK: Terms and people

    @Test func termsComeFromTheMaterials() {
        let pages = ["RQ1 studies beam management for XL-MIMO. TSIR reuses forecasts. In RQ1 the XL-MIMO array is hybrid.",
                     "DeepMIMO O1 at mmWave: TSIR gains 9 pp. DeepMIMO gives the channels, and mmWave pilots are scarce.",
                     "González-Prelcic and Nimier-David report 3.5 bit/s/Hz in 2026. THE RESULTS ARE PRELIMINARY AND UNPUBLISHED."]
        let terms = MaterialTerms.extract(from: pages)
        #expect(Set(terms) == ["RQ1", "XL-MIMO", "TSIR", "DeepMIMO", "mmWave"])
        // Ordinary words, years, units, surnames and shouting are not terms.
        for word in ["Results", "2026", "bit/s/Hz", "Nimier-David", "lez-Prelcic", "UNPUBLISHED", "PDF"] { #expect(!MaterialTerms.isTerm(word)) }
    }

    @Test func notesReadWithTheNamesGivenLater() {
        let names = [1: "Kai Wu（主导师）", 2: "Ren Ping Liu", 12: "Wei Ni"]
        #expect(SpeakerLabels.naming("说话人 1 要求补充基线，说话人2 同意；说话人 12 没有发言。", names: names)
                == "Kai Wu 要求补充基线，Ren Ping Liu 同意；Wei Ni 没有发言。")
        #expect(SpeakerLabels.naming("Speaker 2 asked about RQ2.", names: names) == "Ren Ping Liu asked about RQ2.")
        #expect(SpeakerLabels.naming("说话人 3 没有名字。", names: names) == "说话人 3 没有名字。")
        #expect(SpeakerLabels.shortName("Kai Wu (advisor)") == "Kai Wu")
        var notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "与说话人 1 的讨论",
                                    sections: [NotesSectionResult(id: "actions", title: "待办", kind: .actions,
                                                                  items: [NotesItem(text: "补充基线对比", owner: "说话人 1", quote: "说话人 1：补一下基线")])])
        notes = notes.named(names)
        #expect(notes.title == "与Kai Wu 的讨论" || notes.title == "与Kai Wu的讨论")
        #expect(notes.sections[0].items[0].owner == "Kai Wu")
        // The quote is what was said: untouched.
        #expect(notes.sections[0].items[0].quote == "说话人 1：补一下基线")
    }

    @Test func questionRepeatedInEnglishGivesItsMeaning() {
        let answer = "> 问：How does reusing the prediction reduce pilots?\n> 译：复用上一次的预测，如何降低导频开销？\n\nIt improves the beam at the same pilot cost.\n> 译：它在同样的导频开销下改善波束。"
        #expect(BilingualAnswer.question(in: answer) == "复用上一次的预测，如何降低导频开销？")
        let lines = CueScript.lines(from: answer, style: .script)
        #expect(lines.map(\.text) == ["It improves the beam at the same pilot cost."])
        #expect(lines.first?.gloss == "它在同样的导频开销下改善波束。")
    }

    @Test func meaningsGivenTogetherAtTheEndGoToTheirSentences() {
        let answer = "> 问：信道模型是否考虑近场？\n\nYes. My model includes the near-field effect.\n\nI use ray-traced channels at 3.5 and 28 GHz. (报告第 15 页)\n\n> 译：是的，模型考虑了近场效应。\n> 译：我用 3.5 和 28 GHz 的射线追踪信道。（报告第 15 页）"
        let lines = CueScript.lines(from: answer, style: .script)
        #expect(lines.map(\.text) == ["Yes. My model includes the near-field effect.", "I use ray-traced channels at 3.5 and 28 GHz."])
        #expect(lines.map(\.gloss) == ["是的，模型考虑了近场效应。", "我用 3.5 和 28 GHz 的射线追踪信道。（报告第 15 页）"])
    }

    @Test func sourceNotesAreNotSpoken() {
        #expect(BilingualAnswer.withoutSource("It raised the mean by about ten percent. (第 1 页，报告第 5.1 节)") == "It raised the mean by about ten percent.")
        #expect(BilingualAnswer.withoutSource("The array response depends on angle and range (near field).") == "The array response depends on angle and range (near field).")
    }
}

