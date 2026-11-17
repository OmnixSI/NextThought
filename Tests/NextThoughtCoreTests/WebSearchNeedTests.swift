import Foundation
import Testing
@testable import NextThoughtCore

struct WebSearchNeedTests {
    let now = ISO8601DateFormatter().date(from: "2026-10-16T08:00:00Z")!

    @Test(arguments: WebSearchNeed.evalSamples.filter(\.needsSearch).map(\.question))
    func timeSensitiveQuestionsNeedSearch(_ question: String) {
        let result = WebSearchNeed.assess(question, now: now)
        #expect(result.needsSearch, "\(question) → \(result)")
    }

    @Test(arguments: WebSearchNeed.evalSamples.filter { !$0.needsSearch }.map(\.question))
    func everydayQuestionsStayOffline(_ question: String) {
        let result = WebSearchNeed.assess(question, now: now)
        #expect(!result.needsSearch, "\(question) → \(result)")
    }

    @Test func reasonNamesTheCue() {
        let result = WebSearchNeed.assess("最近的油价是多少", now: now)
        #expect(result.reason.contains("最近"))
        #expect(result.reason.contains("油价"))
    }

    @Test func aWordInsideAnotherIsNotATopic() {
        #expect(!WebSearchNeed.mentions("won", in: "i was wondering whether the site map needs to be updated"))
        #expect(WebSearchNeed.mentions("won", in: "who won the election"))
        #expect(WebSearchNeed.mentions("launch", in: "when it launches"))
        #expect(WebSearchNeed.mentions("股价", in: "苹果的股价是多少"))
        #expect(WebSearchNeed.assess("I was wondering whether the site map in your third research question needs to be updated when the environment changes.").score < 0.5)
    }
}
