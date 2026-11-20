using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from ScrollFollowStateTests.swift: a pane follows the live edge or pins the reading
/// position, and only the newest-content rules are counted while it is pinned.
/// </summary>
public sealed class ScrollFollowStateParityTests
{
    [Fact]
    public void NewTranscriptItemsDoNotMoveTheReadingPosition()
    {
        var state = new ScrollFollowState();
        state.UserScrolledAway();
        state.ReceivedNewContent(count: 3);

        Assert.Equal(ScrollFollowState.Modes.ReadingHistory, state.Mode);
        Assert.False(state.IsFollowing);
        Assert.Equal(3, state.PendingItemCount);
        Assert.True(state.HasNewContent);
    }

    [Fact]
    public void TextSelectionPinsTheViewportUntilExplicitReturn()
    {
        var state = new ScrollFollowState();
        state.UserScrolledAway();
        state.UserSelectedText();
        state.ReceivedNewContent();
        state.UserScrolledAway();
        state.ReachedLatest();

        Assert.Equal(ScrollFollowState.Modes.SelectingText, state.Mode);
        Assert.Equal(1, state.PendingItemCount);

        state.ReturnToLatest();
        Assert.Equal(ScrollFollowState.Modes.Following, state.Mode);
        Assert.True(state.IsFollowing);
        Assert.Equal(0, state.PendingItemCount);
        Assert.False(state.HasNewContent);
    }

    [Fact]
    public void ReadingHistoryResumesFollowingAtTheLiveEdge()
    {
        var state = new ScrollFollowState();
        state.UserScrolledAway();
        state.ReceivedNewContent(count: 1);
        state.ReachedLatest();

        Assert.True(state.IsFollowing);
        Assert.Equal(0, state.PendingItemCount);
    }

    [Fact]
    public void FollowStateIsIndependentForEachPane()
    {
        var transcript = new ScrollFollowState();
        var answer = new ScrollFollowState();

        transcript.UserScrolledAway();
        transcript.ReceivedNewContent(count: 2);

        Assert.Equal(ScrollFollowState.Modes.ReadingHistory, transcript.Mode);
        Assert.Equal(2, transcript.PendingItemCount);
        Assert.True(answer.IsFollowing);
        Assert.Equal(0, answer.PendingItemCount);
    }

    [Fact]
    public void IgnoresContentCountsThatCannotRepresentNewItems()
    {
        var state = new ScrollFollowState();
        state.UserScrolledAway();
        state.ReceivedNewContent(count: 0);
        state.ReceivedNewContent(count: -4);

        Assert.Equal(0, state.PendingItemCount);
        Assert.False(state.HasNewContent);
    }

    [Fact]
    public void KeyboardNavigationPausesFollowingBeforeNewContentArrives()
    {
        var state = new ScrollFollowState();
        const ushort pageUpKeyCode = 116;

        if (ScrollFollowInputClassifier.IsScrollNavigationKeyCode(pageUpKeyCode)) state.UserScrolledAway();
        state.ReceivedNewContent(count: 2);

        Assert.False(state.IsFollowing);
        Assert.Equal(ScrollFollowState.Modes.ReadingHistory, state.Mode);
        Assert.Equal(2, state.PendingItemCount);
    }

    [Fact]
    public void ClassifiesNavigationKeysButNotOrdinaryTypingKeys()
    {
        ushort[] navigationKeys = [123, 124, 125, 126, 115, 116, 119, 121, 49];
        ushort[] ordinaryKeys = [0, 36, 48, 53, 55];

        Assert.All(navigationKeys, keyCode => Assert.True(ScrollFollowInputClassifier.IsScrollNavigationKeyCode(keyCode)));
        Assert.All(ordinaryKeys, keyCode => Assert.False(ScrollFollowInputClassifier.IsScrollNavigationKeyCode(keyCode)));
    }

    [Fact]
    public void RecognizesClicksInsideTheVerticalScrollerFrame()
    {
        var scrollerFrame = new ScrollFrame(90, 0, 10, 100);

        Assert.True(ScrollFollowInputClassifier.IsPoint(95, 50, scrollerFrame));
        Assert.False(ScrollFollowInputClassifier.IsPoint(89, 50, scrollerFrame));
        Assert.False(ScrollFollowInputClassifier.IsPoint(95, 50, null));
        Assert.False(ScrollFollowInputClassifier.IsPoint(0, 0, new ScrollFrame(0, 0, 0, 0)));
    }
}
