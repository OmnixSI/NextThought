import Foundation
import Testing
@testable import InterviewHackerCore

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
}
