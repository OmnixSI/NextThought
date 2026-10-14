import Testing
@testable import InterviewHackerCore

struct CodexEventParserTests {
    @Test func readsCompleteAgentMessage() {
        let line = #"{"type":"item.completed","item":{"type":"agent_message","text":"Ready"}}"#
        #expect(CodexOutputParser.parse(line) == .message("Ready"))
    }

    @Test func readsCodexFailureDetails() {
        let line = #"{"type":"turn.failed","error":{"message":"Account unavailable"}}"#
        #expect(CodexOutputParser.parse(line) == .failure("Account unavailable"))
    }

    @Test func ignoresProgressAndUnknownDeltaEvents() {
        let progress = #"{"type":"item.started","item":{"type":"agent_message","text":"Draft"}}"#
        let delta = #"{"type":"item.agent_message.delta","delta":"Draft"}"#
        #expect(CodexOutputParser.parse(progress) == nil)
        #expect(CodexOutputParser.parse(delta) == nil)
    }

    @Test func ignoresMalformedJSON() {
        #expect(CodexOutputParser.parse("not json") == nil)
    }
}
