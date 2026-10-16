import Foundation
import Testing
@testable import InterviewHackerCore

struct WebSearchNeedTests {
    let now = ISO8601DateFormatter().date(from: "2026-10-16T08:00:00Z")!

    @Test(arguments: [
        "现在比特币多少钱",
        "最近 OpenAI 发布了什么新模型",
        "今天北京天气怎么样",
        "英伟达最新一季的财报营收是多少",
        "苹果现任 CEO 是谁",
        "2026 年诺贝尔物理学奖颁给了谁",
        "What's the latest version of Swift?",
        "What is the current exchange rate of USD to CNY",
        "这周 A 股的行情怎么样，股价跌了吗",
    ])
    func timeSensitiveQuestionsNeedSearch(_ question: String) {
        let result = WebSearchNeed.assess(question, now: now)
        #expect(result.needsSearch, "\(question) → \(result)")
    }

    @Test(arguments: [
        "你最近在做什么项目",
        "介绍一下你目前负责的工作",
        "Redis 和 Memcached 有什么区别",
        "数据库和缓存怎么保证一致性",
        "为什么选择 Kafka 而不是 RabbitMQ",
        "讲一下 TCP 三次握手的原理",
        "Can you walk me through your current project?",
        "目前我们团队用的是微服务架构，你怎么看",
    ])
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
