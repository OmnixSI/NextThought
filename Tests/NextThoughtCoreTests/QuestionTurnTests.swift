import Foundation
import Testing
@testable import NextThoughtCore

@Suite struct QuestionTurnTests {
    private let start = Date(timeIntervalSince1970: 1_000)

    @Test func aDoubtPutAsAStatementIsAnsweredWhenTheyStop() {
        var turn = QuestionTurn()
        turn.heard("I am not fully convinced by the comparison in your results section.", at: start)
        #expect(!turn.isDue(now: start.addingTimeInterval(1), partial: "", replying: false))
        #expect(turn.isDue(now: start.addingTimeInterval(2.1), partial: "", replying: false))
        turn.asked()
        #expect(!turn.isDue(now: start.addingTimeInterval(4), partial: "", replying: false))
    }

    @Test func nothingIsAskedWhileTheyAreStillTalking() {
        var turn = QuestionTurn()
        turn.heard("Thank you for the presentation, I have two questions for you.", at: start)
        #expect(!turn.isDue(now: start.addingTimeInterval(3), partial: "The first one is about", replying: false))
    }

    @Test func aSecondQuestionInTheSameTurnAsksAgainForBoth() {
        var turn = QuestionTurn()
        turn.heard("First, how do you estimate the channel?", at: start)
        #expect(turn.worthAsking())
        turn.asked()
        turn.heard("Okay.", at: start.addingTimeInterval(2))
        #expect(!turn.worthAsking())
        turn.heard("And second, why did you choose this baseline instead of a stronger one?", at: start.addingTimeInterval(6))
        #expect(turn.worthAsking())
        #expect(turn.text().hasPrefix("First, how do you estimate the channel?"))
        #expect(turn.text().hasSuffix("stronger one?"))
        #expect(turn.isDue(now: start.addingTimeInterval(8.5), partial: "", replying: false))
    }

    @Test func myReplyEndsTheirTurn() {
        var turn = QuestionTurn()
        turn.heard("Could you explain the loss function on slide twelve in more detail please?", at: start)
        turn.spoke("Mm.")
        #expect(!turn.isEmpty)
        turn.spoke("Sure, the loss has two terms.")
        #expect(turn.isEmpty)
        turn.heard("And what about the cost?", at: start.addingTimeInterval(20))
        #expect(turn.text() == "And what about the cost?")
    }

    @Test func startingToReplyAsksAtOnce() {
        var turn = QuestionTurn()
        turn.heard("It seems to me the gain mostly comes from the larger dataset.", at: start)
        #expect(turn.isDue(now: start.addingTimeInterval(0.4), partial: "", replying: true))
    }

    @Test func shortRemarksAndOldTurnsAreLeftAlone() {
        var turn = QuestionTurn()
        turn.heard("Okay, thank you.", at: start)
        #expect(!turn.isDue(now: start.addingTimeInterval(3), partial: "", replying: false))
        turn.heard("That was a very clear talk and I enjoyed it a lot.", at: start.addingTimeInterval(1))
        #expect(!turn.isDue(now: start.addingTimeInterval(40), partial: "", replying: false))
    }

    @Test func aLongPauseStartsANewTurn() {
        var turn = QuestionTurn()
        turn.heard("How large is the array in your simulation?", at: start)
        turn.asked()
        turn.heard("Why is the near field model needed here?", at: start.addingTimeInterval(60))
        #expect(turn.requests == 0)
        #expect(turn.text() == "Why is the near field model needed here?")
    }

    @Test func wordsCountLatinWordsAndChineseCharacters() {
        #expect(QuestionTurn.words("near-field XL-MIMO isn't new") == 4)
        #expect(QuestionTurn.words("为什么用这个基线") == 8)
    }
}
