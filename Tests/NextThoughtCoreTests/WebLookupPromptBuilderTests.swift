import Foundation
import Testing
@testable import NextThoughtCore

struct WebLookupPromptBuilderTests {
    @Test func lookupDropsSkillsAndBoundsContextWithoutLosingTheSubject() throws {
        let input: [String: Any] = ["question": "它刚发布的版本是什么？", "today": "2026-10-17",
            "recent_transcript": String(repeating: "旧上下文", count: 5_000) + "正在讨论 Swift",
            "recent_answers": (0..<6).map { ["question": "第\($0)题", "answer": String(repeating: "回答", count: 2_000)] },
            "skills": [["content": String(repeating: "技能", count: 6_000)]]]
        let data = try JSONSerialization.data(withJSONObject: input)
        let prompt = WebLookupPromptBuilder.makeParts(userJSON: String(decoding: data, as: UTF8.self))
        let output = try JSONSerialization.jsonObject(with: Data(prompt.user.utf8)) as! [String: Any]
        #expect(output["question"] as? String == "它刚发布的版本是什么？")
        #expect((output["recent_transcript"] as? String)?.hasSuffix("正在讨论 Swift") == true)
        #expect((output["recent_transcript"] as? String)?.count == 1_200)
        #expect((output["recent_answers"] as? [[String: String]])?.count == 2)
        #expect(output["skills"] == nil)
        #expect(prompt.user.count < 4_000)
        #expect(prompt.system.contains("同一轮检索中并行查询"))
        #expect(prompt.system.contains("最多一轮检索"))
    }

    @Test func malformedCallerRetainsABoundedQuestion() {
        let prompt = WebLookupPromptBuilder.makeParts(userJSON: String(repeating: "车型", count: 3_000))
        #expect(prompt.user.count < 2_100)
    }

    @Test func parsesBatchedSearchProgressAndIgnoresOtherTurns() {
        let params: [String: Any] = ["turnId": "T", "item": ["id": "S", "type": "webSearch",
            "action": ["type": "search", "queries": ["query one", "query two"]]]]
        #expect(CodexAppServerProtocol.turnEvent(method: "item/started", params: params, turnID: "T") == .webSearch(itemID: "S", completed: false, queryCount: 2))
        #expect(CodexAppServerProtocol.turnEvent(method: "item/completed", params: params, turnID: "T") == .webSearch(itemID: "S", completed: true, queryCount: 2))
        #expect(CodexAppServerProtocol.turnEvent(method: "item/started", params: params, turnID: "OTHER") == nil)
    }
}
