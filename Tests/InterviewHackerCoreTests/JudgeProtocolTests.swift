import Foundation
import Testing
@testable import InterviewHackerCore

struct JudgeProtocolTests {
    @Test func requestIsVersionedJSON() throws {
        let parts = JudgeProtocol.request(task: .webSearchNeed, text: "现在比特币多少钱", mode: "interview", ruleScore: 0.456, today: "2026-10-16 星期五")
        let object = try #require(JSONSerialization.jsonObject(with: Data(parts.user.utf8)) as? [String: Any])
        #expect(object["task"] as? String == "web_search_need")
        #expect(object["version"] as? Int == 1)
        #expect(object["rule_score"] as? Double == 0.46)
        #expect(parts.system.contains("web_search_need"))
    }

    @Test func parsesReplies() {
        #expect(JudgeProtocol.parse(#"{"answer": true, "confidence": 0.92, "query": "BTC price today"}"#) == .init(yes: true, confidence: 0.92, query: "BTC price today"))
        #expect(JudgeProtocol.parse("好的：\n```json\n{\"answer\": false, \"confidence\": 0.8}\n```")?.yes == false)
        #expect(JudgeProtocol.parse(#"{"is_question": "yes"}"#)?.yes == true)
        #expect(JudgeProtocol.parse("yes")?.yes == true)
        #expect(JudgeProtocol.parse("No.")?.yes == false)
        #expect(JudgeProtocol.parse("maybe") == nil)
        #expect(JudgeProtocol.parse(#"{"answer": 2}"#)?.yes == true)
    }
}
