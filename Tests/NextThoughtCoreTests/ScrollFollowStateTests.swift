import Testing
@testable import NextThoughtCore

struct ScrollFollowStateTests {
    @Test func newTranscriptItemsDoNotMoveTheReadingPosition() {
        var state = ScrollFollowState()
        state.userScrolledAway()
        state.receivedNewContent(count: 3)

        #expect(state.mode == .readingHistory)
        #expect(!state.isFollowing)
        #expect(state.pendingItemCount == 3)
        #expect(state.hasNewContent)
    }

    @Test func textSelectionPinsTheViewportUntilExplicitReturn() {
        var state = ScrollFollowState()
        state.userSelectedText()
        state.receivedNewContent()
        state.userScrolledAway()

        #expect(state.mode == .selectingText)
        #expect(state.pendingItemCount == 1)

        state.returnToLatest()
        #expect(state.mode == .following)
        #expect(state.isFollowing)
        #expect(state.pendingItemCount == 0)
        #expect(!state.hasNewContent)
    }

    @Test func followStateIsIndependentForEachPane() {
        var transcript = ScrollFollowState()
        let answer = ScrollFollowState()

        transcript.userScrolledAway()
        transcript.receivedNewContent(count: 2)

        #expect(transcript.mode == .readingHistory)
        #expect(transcript.pendingItemCount == 2)
        #expect(answer.isFollowing)
        #expect(answer.pendingItemCount == 0)
    }

    @Test func ignoresContentCountsThatCannotRepresentNewItems() {
        var state = ScrollFollowState()
        state.userScrolledAway()
        state.receivedNewContent(count: 0)
        state.receivedNewContent(count: -4)

        #expect(state.pendingItemCount == 0)
        #expect(!state.hasNewContent)
    }
}
