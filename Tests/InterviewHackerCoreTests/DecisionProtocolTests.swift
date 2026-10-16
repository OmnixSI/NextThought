import Foundation
import Testing
@testable import InterviewHackerCore

struct DecisionProtocolTests {
    @Test func requestUsesTheSystemOneFormat() throws {
        let task = DecisionTasks.utterance("那你们是怎么保证一致性的", context: ["[对方] 我们聊聊缓存"], previousQuestion: "你们用的什么缓存", mode: .interview)
        let object = try #require(JSONSerialization.jsonObject(with: DecisionProtocol.body(state: task.state, questions: task.questions)) as? [String: Any])
        #expect(object["think_mode"] as? String == "never")
        #expect((object["state"] as? String)?.contains("那你们是怎么保证一致性的") == true)
        let questions = try #require(object["questions"] as? [String: [String: Any]])
        #expect(questions["intent"]?["type"] as? String == "choice")
        #expect((questions["intent"]?["criteria"] as? [String: Any])?.keys.contains("follow_up") == true)
        #expect(questions["answer_now"]?["type"] as? String == "noul")
        #expect(questions["continues_previous"] != nil)
        // No previous question, no continuation question.
        #expect(DecisionTasks.utterance("hi", context: [], previousQuestion: nil, mode: .general).questions["continues_previous"] == nil)
    }

    @Test func parsesTheDocumentedResponse() throws {
        let json = #"""
        {"answers": {
          "intent": {"type": "choice", "choice": "follow_up", "confidence": 0.81, "probabilities": {"follow_up": 0.86, "statement": 0.1, "question": 0.04}},
          "answer_now": {"type": "noul", "noul": 0.92},
          "continues_previous": {"type": "noul", "noul": 0.12}
        }, "usage": {"input_tokens": 290, "output_tokens": 0}, "model": "StartLux-Decision-4B"}
        """#
        let answers = try #require(DecisionProtocol.parse(Data(json.utf8)))
        #expect(answers["intent"]?.probabilities["follow_up"] == 0.86)
        let verdict = try #require(DecisionTasks.utteranceVerdict(answers))
        #expect(verdict.intent == .followUp)
        #expect(verdict.answerProbability == 0.92)
        #expect(verdict.continuesPrevious == 0.12)
        #expect(verdict.shouldAnswer)
    }

    @Test func logisticsAreNeverAnswered() {
        let answers: [String: DecisionAnswer] = ["intent": .init(choice: "logistics"), "answer_now": .init(yes: 0.9)]
        let verdict = DecisionTasks.utteranceVerdict(answers)
        #expect(verdict?.isLogistics == true)
        #expect(verdict?.shouldAnswer == false)
        #expect(DecisionTasks.utteranceVerdict(["intent": .init(choice: "question")]) == nil)
    }

    @Test func otherTasksAreSingleYesNoQuestions() throws {
        for task in [DecisionTasks.webSearch("现在比特币多少钱", today: "2026-10-16"),
                     DecisionTasks.echo(microphone: "数据库和缓存", otherSide: ["数据库和缓存怎么保证一致性"]),
                     DecisionTasks.sameQuestion("怎么保证一致性", "如何保证数据一致")] {
            #expect(task.questions.count == 1)
            let object = try #require(JSONSerialization.jsonObject(with: DecisionProtocol.body(state: task.state, questions: task.questions)) as? [String: Any])
            #expect(((object["questions"] as? [String: [String: Any]])?.values.first?["type"] as? String) == "noul")
        }
    }

    @Test func readsServerErrors() {
        #expect(DecisionProtocol.errorMessage(Data(#"{"error": "gate must be a number"}"#.utf8)) == "gate must be a number")
        #expect(DecisionProtocol.parse(Data("{}".utf8)) == nil)
    }

    @Test func evaluationCorpusIsShared() {
        #expect(QuestionEvalCorpus.all.count == QuestionEvalCorpus.extended.count + QuestionEvalCorpus.heldOut.count)
        #expect(QuestionEvalCorpus.all.contains { $0.isQuestion } && QuestionEvalCorpus.all.contains { !$0.isQuestion })
    }
}
