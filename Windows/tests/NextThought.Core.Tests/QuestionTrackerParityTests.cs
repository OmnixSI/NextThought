using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B15: translated from QuestionIntentTests.swift tracker/threshold rules.</summary>
public sealed class QuestionTrackerParityTests
{
    private static DateTimeOffset At(int seconds) => DateTimeOffset.FromUnixTimeSeconds(1_800_000_000).AddSeconds(seconds);

    [Fact]
    public void GroupProjectUsesTheLabMeetingThresholds()
    {
        Assert.Equal((.55, .3), QuestionIntentClassifier.Thresholds(MeetingMode.GroupProject));
        Assert.Equal(QuestionIntentClassifier.Thresholds(MeetingMode.LabMeeting),
            QuestionIntentClassifier.Thresholds(MeetingMode.GroupProject));
        Assert.Equal((.45, .25), QuestionIntentClassifier.Thresholds(MeetingMode.Interview));
        Assert.Equal((.6, .35), QuestionIntentClassifier.Thresholds(MeetingMode.Lesson));
    }

    [Fact]
    public void StreamingRevisionsUpdateOneCandidateAndTriggerOnce()
    {
        var tracker = new QuestionCandidateTracker(MeetingMode.Meeting);
        var id = Guid.NewGuid();
        var first = tracker.Ingest("为什么选这个岗位", false, id, At(0));
        Assert.Equal(CandidateKind.Updated, first.Kind);
        var revised = tracker.Ingest("为什么选这个岗位？", false, id, At(1));
        Assert.Equal(CandidateKind.Updated, revised.Kind);
        Assert.True(tracker.Tick(At(3)).Kind == CandidateKind.Trigger);
        Assert.True(tracker.Current?.Triggered);
        Assert.Equal(CandidateKind.None, tracker.Ingest("为什么选这个岗位？", true, id, At(4)).Kind);
        Assert.Equal(CandidateKind.None, tracker.Tick(At(6)).Kind);
    }

    [Fact]
    public void ResetAndConfirmFollowTheMacContract()
    {
        var tracker = new QuestionCandidateTracker(MeetingMode.Meeting);
        var decision = tracker.Ingest("你们怎么保证缓存一致性？", true, Guid.NewGuid(), At(0));
        if (decision.Kind != CandidateKind.Trigger)
            Assert.True(tracker.Confirm(decision.Candidate!, At(1)));
        tracker.NoteAnswered();
        Assert.True(tracker.HasPriorAnswer);
        tracker.Reset();
        Assert.False(tracker.HasPriorAnswer);
        Assert.Null(tracker.Current);
        Assert.Equal(CandidateKind.None, tracker.Tick(At(2)).Kind);
    }

    [Fact]
    public void BareFollowUpNeedsAPriorAnswer()
    {
        Assert.Equal(UtteranceIntent.FollowUp, QuestionIntentClassifier.Assess("为什么", MeetingMode.Meeting, true).Intent);
        Assert.Equal(UtteranceIntent.FollowUp, QuestionIntentClassifier.Assess("为什么", MeetingMode.Meeting, false).Intent);
        Assert.True(QuestionIntentClassifier.Assess("为什么", MeetingMode.Meeting, true).Score >
                    QuestionIntentClassifier.Assess("为什么", MeetingMode.Meeting, false).Score);
    }
}
