import CoreGraphics
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
        state.userScrolledAway()
        state.userSelectedText()
        state.receivedNewContent()
        state.userScrolledAway()
        state.reachedLatest()

        #expect(state.mode == .selectingText)
        #expect(state.pendingItemCount == 1)

        state.returnToLatest()
        #expect(state.mode == .following)
        #expect(state.isFollowing)
        #expect(state.pendingItemCount == 0)
        #expect(!state.hasNewContent)
    }

    @Test func readingHistoryResumesFollowingAtTheLiveEdge() {
        var state = ScrollFollowState()
        state.userScrolledAway()
        state.receivedNewContent(count: 1)
        state.reachedLatest()

        #expect(state.isFollowing)
        #expect(state.pendingItemCount == 0)
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

    @Test func keyboardNavigationPausesFollowingBeforeNewContentArrives() {
        var state = ScrollFollowState()
        let pageUpKeyCode: UInt16 = 116

        if ScrollFollowInputClassifier.isScrollNavigationKeyCode(pageUpKeyCode) {
            state.userScrolledAway()
        }
        state.receivedNewContent(count: 2)

        #expect(!state.isFollowing)
        #expect(state.mode == .readingHistory)
        #expect(state.pendingItemCount == 2)
    }

    @Test func classifiesNavigationKeysButNotOrdinaryTypingKeys() {
        let navigationKeys: [UInt16] = [123, 124, 125, 126, 115, 116, 119, 121, 49]
        let ordinaryKeys: [UInt16] = [0, 36, 48, 53, 55]

        #expect(navigationKeys.allSatisfy(ScrollFollowInputClassifier.isScrollNavigationKeyCode))
        #expect(ordinaryKeys.allSatisfy { !ScrollFollowInputClassifier.isScrollNavigationKeyCode($0) })
    }

    @Test func recognizesClicksInsideTheVerticalScrollerFrame() {
        let scrollerFrame = CGRect(x: 90, y: 0, width: 10, height: 100)

        #expect(ScrollFollowInputClassifier.isPoint(CGPoint(x: 95, y: 50), inside: scrollerFrame))
        #expect(!ScrollFollowInputClassifier.isPoint(CGPoint(x: 89, y: 50), inside: scrollerFrame))
        #expect(!ScrollFollowInputClassifier.isPoint(CGPoint(x: 95, y: 50), inside: nil))
        #expect(!ScrollFollowInputClassifier.isPoint(CGPoint(x: 0, y: 0), inside: .zero))
    }
}
