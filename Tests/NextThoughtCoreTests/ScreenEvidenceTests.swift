import Foundation
import Testing
@testable import NextThoughtCore

struct ScreenEvidenceTests {
    @Test func deicticQuestionDoesNotRequireTextOverlap() {
        let screen = ScreenContext(frameID: "page", targetID: "window:7", source: "PPT", regions: [.init(text: "Throughput 92.7%", bounds: [0.1,0.2,0.3,0.1], confidence: 0.95)])
        #expect(ScreenReference.isRelated("你这一页的这个地方为什么这样画？", context: screen))
        #expect(ScreenReference.needsImage("右边这个图说明了什么？"))
        #expect(!ScreenReference.isRelated("介绍一下你最近负责的项目", context: screen))
    }
    @Test func oldOrFutureFrameCannotMasqueradeAsCurrent() {
        let now = Date(), old = ScreenContext(frameID: "old", targetID: "display:1", source: "screen", capturedAt: Date().addingTimeInterval(-3), regions: [])
        #expect(!old.isFresh(now: now))
        let future = ScreenContext(frameID: "future", targetID: "display:1", source: "screen", capturedAt: now.addingTimeInterval(3), regions: [])
        #expect(!future.isFresh(now: now))
    }
    @Test func searchDecisionRequiresStructuredVerdictAndKeepsSubject() {
        let parsed = SearchDecision.parse(#"{"needsSearch":true,"reason":"当前规格","query":"Claude context window"}"#, question: "它支持多长？")
        #expect(parsed?.search == true)
        #expect(parsed?.query == "Claude context window")
        #expect(SearchDecision.parse("yes", question: "q") == nil)
        #expect(!AnswerGrounding.pureRewrite("补充这个方法与最新 SOTA 的差异"))
        #expect(AnswerGrounding.pureRewrite("把上一条回答压缩成两句"))
    }
    @Test func lowSimilarityCanReturnNoMaterialsAndPhysicalLocatorWins() {
        let doc = ContextDocument(name: "paper.pdf", pages: [], content: .init(pipeline: "test", blocks: [.init(markdown: "cache consistency", locator: .init(page: 7))]))
        let chunks = ContextChunker.chunks(documents: [doc])
        var semantic = SemanticIndex(); semantic.setVector([1,0], fingerprint: chunks[0].fingerprint, for: 0)
        let index = HybridContextIndex(documents: [doc], semantic: semantic)
        #expect(index.retrieve(question: "unrelated astronomy", queryVector: [0,1]).isEmpty)
        #expect(index.retrieve(question: "paper.pdf 第7页", queryVector: [0,1]).first?.locator?.page == 7)
    }
    @Test func promptAllowsGeneralScienceWithoutInventingResearchResults() {
        let prompt = AnswerPromptBuilder.makeParts(question: "什么是近场波束成形？", context: "", profile: "", turns: [], hasScreenshot: false, mode: .academic)
        #expect(prompt.system.contains("资料不是回答主题的范围上限"))
        #expect(prompt.system.contains("不编造第一人称经历"))
    }
}
