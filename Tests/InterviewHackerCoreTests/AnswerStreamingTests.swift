import Foundation
import Testing
@testable import InterviewHackerCore

struct AnswerStreamingTests {
    @Test func appServerRequestsAreSingleLineWithoutJSONRPCField() throws {
        let data = CodexAppServerProtocol.request(id: 3, method: "turn/start", params: CodexAppServerProtocol.turnStartParams(threadID: "T", text: "hi\nthere", imagePath: "/tmp/a.jpg", effort: "low"))
        let text = String(decoding: data, as: UTF8.self)
        #expect(text.hasSuffix("\n"))
        #expect(text.dropLast().contains("\n") == false)
        #expect(!text.contains("jsonrpc"))
        let object = try #require(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let params = try #require(object["params"] as? [String: Any])
        let input = try #require(params["input"] as? [[String: Any]])
        #expect(input.count == 2)
        #expect(input[1]["type"] as? String == "localImage")
    }

    @Test func threadStartIsReadOnlyEphemeralAndNeverAsks() {
        let params = CodexAppServerProtocol.threadStartParams(model: "m", effort: "low", cwd: "/tmp/x")
        #expect(params["sandbox"] as? String == "read-only")
        #expect(params["approvalPolicy"] as? String == "never")
        #expect(params["ephemeral"] as? Bool == true)
        #expect((params["config"] as? [String: Any])?["features.shell_tool"] as? Bool == false)
    }

    @Test func classifiesInboundMessages() {
        guard case let .response(id, result, error)? = CodexAppServerProtocol.parse(#"{"id":2,"result":{"thread":{"id":"T1"}}}"#) else {
            Issue.record("response"); return
        }
        #expect(id == 2 && error == nil)
        #expect(CodexAppServerProtocol.threadID(fromThreadStart: result ?? [:]) == "T1")
        guard case .serverRequest(_, let method, _)? = CodexAppServerProtocol.parse(#"{"id":9,"method":"item/commandExecution/requestApproval","params":{}}"#) else {
            Issue.record("server request"); return
        }
        #expect(method == "item/commandExecution/requestApproval")
        let reply = String(decoding: CodexAppServerProtocol.reply(toServerRequest: 9, method: method), as: UTF8.self)
        #expect(reply.contains("decline"))
        guard case .response(_, _, let failure)? = CodexAppServerProtocol.parse(#"{"id":4,"error":{"code":-32600,"message":"Not initialized"}}"#) else {
            Issue.record("error"); return
        }
        #expect(failure == "Not initialized")
    }

    @Test func accumulatesDeltasAndPrefersFinalAnswer() {
        var accumulator = AgentMessageAccumulator()
        let events: [(String, [String: Any])] = [
            ("item/agentMessage/delta", ["turnId": "U", "itemId": "c", "delta": "thinking"]),
            ("item/agentMessage/delta", ["turnId": "U", "itemId": "m", "delta": "Hel"]),
            ("item/agentMessage/delta", ["turnId": "U", "itemId": "m", "delta": "lo"]),
            ("item/agentMessage/delta", ["turnId": "OTHER", "itemId": "m", "delta": "XX"]),
        ]
        for (method, params) in events {
            if let event = CodexAppServerProtocol.turnEvent(method: method, params: params, turnID: "U") { accumulator.apply(event) }
        }
        #expect(accumulator.text == "Hello")
        let completed = CodexAppServerProtocol.turnEvent(method: "item/completed", params: ["turnId": "U", "item": ["type": "agentMessage", "id": "c", "text": "Final", "phase": "final_answer"]], turnID: "U")
        accumulator.apply(completed!)
        #expect(accumulator.text == "Final")
        #expect(CodexAppServerProtocol.turnEvent(method: "turn/completed", params: ["turn": ["id": "U", "status": "interrupted"]], turnID: "U") == .completed(status: "interrupted", error: nil))
        #expect(CodexAppServerProtocol.turnEvent(method: "error", params: ["error": ["message": "busy"], "willRetry": true], turnID: "U") == .retrying("busy"))
    }

    @Test func loginDetection() {
        #expect(CodexAppServerProtocol.isLoggedIn(accountRead: ["account": ["type": "chatgpt"], "requiresOpenaiAuth": true]))
        #expect(!CodexAppServerProtocol.isLoggedIn(accountRead: ["account": NSNull(), "requiresOpenaiAuth": true]))
    }

    @Test func parsesChatCompletionSSE() {
        let parser = ChatCompletionStreamParser()
        #expect(parser.parse(line: #"data: {"choices":[{"delta":{"content":"你好"}}]}"#) == .delta("你好"))
        #expect(parser.parse(line: "data: [DONE]") == .done)
        #expect(parser.parse(line: ": keep-alive") == nil)
        #expect(parser.parse(line: #"data: {"choices":[{"delta":{"role":"assistant"}}]}"#) == nil)
        #expect(parser.parse(line: #"{"error":{"message":"bad key"}}"#) == .failure("bad key"))
    }

    @Test func endpointRulesRefuseCleartextRemoteHosts() {
        #expect(ChatCompletionStreamParser.endpoint(from: "https://api.deepseek.com/v1")?.absoluteString == "https://api.deepseek.com/v1/chat/completions")
        #expect(ChatCompletionStreamParser.endpoint(from: "http://localhost:11434/v1/")?.absoluteString == "http://localhost:11434/v1/chat/completions")
        #expect(ChatCompletionStreamParser.endpoint(from: "http://example.com/v1") == nil)
        #expect(ChatCompletionStreamParser.endpoint(from: "") == nil)
    }

    @Test func promptPartsMatchSinglePromptAndRefinementsKeepQuestion() {
        let parts = AnswerPromptBuilder.makeParts(question: "q", context: "c", profile: "", turns: [], hasScreenshot: false)
        #expect(parts.system == AnswerPromptBuilder.instructions)
        #expect(parts.system + "\n\n" + parts.user == AnswerPromptBuilder.make(question: "q", context: "c", profile: "", turns: [], hasScreenshot: false))
        #expect(AnswerRefinement.shorter.question(for: "什么是索引").contains("什么是索引"))
    }
}

struct OfficeImportTests {
    @Test func extractsSlideParagraphs() {
        let xml = #"<p:sld><a:p><a:r><a:t>Redis &amp; 缓存</a:t></a:r><a:r><a:t> 设计</a:t></a:r></a:p><a:p><a:r><a:t>第二段</a:t></a:r></a:p><a:p></a:p></p:sld>"#
        #expect(OfficeXMLText.extract(xml) == "Redis & 缓存 设计\n第二段")
    }
    @Test func extractsWordParagraphsWithAttributes() {
        let xml = #"<w:body><w:p><w:r><w:t xml:space="preserve">Hello </w:t></w:r><w:r><w:t>world</w:t></w:r></w:p></w:body>"#
        #expect(OfficeXMLText.extract(xml) == "Hello world")
    }
    @Test func ordersSlidesNumerically() {
        #expect(OfficeXMLText.numericOrder(["ppt/slides/slide10.xml", "ppt/slides/slide2.xml", "ppt/slides/slide1.xml"]) == ["ppt/slides/slide1.xml", "ppt/slides/slide2.xml", "ppt/slides/slide10.xml"])
    }
    @Test func presetRoundTrips() throws {
        let preset = SessionPreset(name: "后端面试", mode: .interview, style: .concise, preferences: SessionPreset.templatePreferences(for: .interview), profile: "p", documents: [ContextDocument(name: "cv.md", pages: ["Swift"])])
        let decoded = try JSONDecoder().decode(SessionPreset.self, from: JSONEncoder().encode(preset))
        #expect(decoded == preset)
    }
}

struct RefinementTests {
    @Test func chainedRefinementsDoNotNest() {
        let once = AnswerRefinement.shorter.question(for: "什么是索引")
        let twice = AnswerRefinement.english.question(for: once)
        #expect(twice.components(separatedBy: "原问题：").count == 2)
        #expect(twice.hasSuffix("什么是索引"))
        #expect(AnswerRefinement.originalQuestion(of: twice) == "什么是索引")
    }
}
