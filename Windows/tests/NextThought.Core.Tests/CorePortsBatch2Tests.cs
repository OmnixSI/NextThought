using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the second self-contained Core batch (web search, scroll follow, readiness).</summary>
public sealed class CorePortsBatch2Tests
{
    [Fact]
    public void WebSearchEvalSamplesMatchTheirLabels()
    {
        var now = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.FromHours(8));
        foreach (var (question, expected) in WebSearchNeed.EvalSamples)
        {
            var result = WebSearchNeed.Assess(question, now);
            Assert.Equal(expected, result.NeedsSearch);
        }
    }

    [Fact]
    public void WebSearchBoundariesKeepWordsWhole()
    {
        Assert.False(WebSearchNeed.Mentions("won", "I was wondering about it"));
        Assert.True(WebSearchNeed.Mentions("won", "they won the match"));
        // "recent" is not "recently"; only "", s, es, d, ed endings belong to the word.
        Assert.False(WebSearchNeed.Mentions("recent", "recently released"));
        Assert.True(WebSearchNeed.Mentions("recently", "recently released"));
        Assert.True(WebSearchNeed.Mentions("price", "stock prices fell"));
        Assert.True(WebSearchNeed.Mentions("价格", "这个价格合适吗"));
    }

    [Fact]
    public void WeakTimeWordsAloneStayOffline()
    {
        var result = WebSearchNeed.Assess("目前我们团队用的是微服务架构", new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.False(result.NeedsSearch);
        Assert.Contains("时间词", result.Reason);
    }

    [Fact]
    public void ResearchAndProductQuestionsScoreHigh()
    {
        Assert.True(WebSearchNeed.Assess("What is the context window of Claude?").NeedsSearch);
        Assert.True(WebSearchNeed.Assess("现在的主流大模型 SOTA 是哪个").NeedsSearch);
        Assert.True(WebSearchNeed.Assess("最新的签证政策是什么").NeedsSearch);
    }

    [Fact]
    public void AssessmentBandsMatchTheThresholds()
    {
        Assert.True(new WebSearchAssessment(0.61, "").NeedsSearch);
        Assert.False(new WebSearchAssessment(0.59, "").NeedsSearch);
        Assert.True(new WebSearchAssessment(0.4, "").Uncertain);
        Assert.False(new WebSearchAssessment(0.2, "").Uncertain);
        Assert.Equal("每次都查", WebSearchMode.Always.Title());
    }

    [Fact]
    public void ScrollFollowTracksReadingAndUnreadCount()
    {
        var state = new ScrollFollowState();
        Assert.True(state.IsFollowing);
        state.ReceivedNewContent();
        Assert.False(state.HasNewContent);

        state.UserScrolledAway();
        Assert.Equal(ScrollFollowState.Modes.ReadingHistory, state.Mode);
        state.ReceivedNewContent();
        state.ReceivedNewContent(2);
        Assert.Equal(3, state.PendingItemCount);
        Assert.True(state.HasNewContent);

        state.ReachedLatest();
        Assert.True(state.IsFollowing);
        Assert.False(state.HasNewContent);
    }

    [Fact]
    public void SelectingTextStaysPinnedUntilExplicitlyReturned()
    {
        var state = new ScrollFollowState();
        state.UserSelectedText();
        Assert.Equal(ScrollFollowState.Modes.SelectingText, state.Mode);
        state.ReachedLatest();
        Assert.Equal(ScrollFollowState.Modes.SelectingText, state.Mode);
        state.ReturnToLatest();
        Assert.True(state.IsFollowing);
    }

    [Theory]
    [InlineData(123, true)]
    [InlineData(126, true)]
    [InlineData(115, true)]
    [InlineData(49, true)]
    [InlineData(0, false)]
    [InlineData(36, false)]
    public void ScrollKeysAreRecognised(ushort keyCode, bool expected)
    {
        Assert.Equal(expected, ScrollFollowInputClassifier.IsScrollNavigationKeyCode(keyCode));
    }

    [Fact]
    public void PointInsideFrameIsScrolling()
    {
        var frame = new ScrollFrame(10, 20, 100, 50);
        Assert.True(ScrollFollowInputClassifier.IsPoint(50, 30, frame));
        Assert.False(ScrollFollowInputClassifier.IsPoint(5, 30, frame));
        Assert.False(ScrollFollowInputClassifier.IsPoint(50, 80, frame));
        Assert.False(ScrollFollowInputClassifier.IsPoint(50, 30, null));
        Assert.False(ScrollFollowInputClassifier.IsPoint(50, 30, new ScrollFrame(10, 20, 0, 0)));
    }

    [Fact]
    public void RecordingReadinessListsEveryMissingCapability()
    {
        var ready = new RecordingReadiness(true, true, true, true, true, RecordingPermission.Granted);
        Assert.Empty(ready.Issues);
        Assert.True(ready.CanStartRecording);
        Assert.True(ready.CanRequestStart);

        var noSpeech = new RecordingReadiness(false, true, true, true, true, RecordingPermission.Granted);
        Assert.Contains(RecordingIssue.Recognition, noSpeech.Issues);
        Assert.False(noSpeech.CanRequestStart);

        var noCapture = new RecordingReadiness(true, false, true, true, true, RecordingPermission.Granted);
        Assert.Contains(RecordingIssue.SystemAudioPermission, noCapture.Issues);
        Assert.True(noCapture.CanRequestStart);
        Assert.False(noCapture.CanStartRecording);

        var noAudio = new RecordingReadiness(true, true, false, true, true, RecordingPermission.Granted);
        Assert.Contains(RecordingIssue.AudioSource, noAudio.Issues);

        var denied = new RecordingReadiness(true, true, true, true, true, RecordingPermission.Denied);
        Assert.Contains(RecordingIssue.MicrophonePermission, denied.Issues);
        Assert.False(denied.CanRequestStart);

        var undetermined = new RecordingReadiness(true, true, true, true, true, RecordingPermission.Undetermined);
        Assert.True(undetermined.CanRequestStart);
        Assert.False(undetermined.CanStartRecording);
    }

    [Fact]
    public void InPersonMeetingsSkipTheSystemAudioChecks()
    {
        var readiness = new RealtimeSetupReadiness(true, false, true, true, false, false, SystemAudioRequired: false);
        Assert.True(readiness.SystemAudioReady);
        Assert.True(readiness.VoiceReady);
        Assert.True(readiness.CanRecord);
        Assert.False(readiness.CanAnswer);

        var online = new RealtimeSetupReadiness(true, true, false, false, true, false);
        Assert.False(online.CanRecord);
        Assert.False(online.SystemAudioReady);
    }

    [Fact]
    public void ReadinessHelpersMirrorTheMacRules()
    {
        Assert.True(RealtimeSetupReadiness.MicrophonePermissionAllowsStart(isGranted: false, isUndetermined: true));
        Assert.False(RealtimeSetupReadiness.MicrophonePermissionAllowsStart(isGranted: false, isUndetermined: false));
        Assert.True(RealtimeSetupReadiness.RequiresMicrophone(microphoneEnabled: false, isOfflineMeeting: true));
        Assert.False(RealtimeSetupReadiness.RequiresMicrophone(microphoneEnabled: false, isOfflineMeeting: false));

        var speaking = new RealtimeSetupReadiness(true, true, false, false, true, true);
        Assert.True(speaking.CanStart);
        Assert.True(speaking.CanUseAI);
        Assert.True(speaking.CoreReady);
    }

    [Fact]
    public void DocumentFeaturesNeedEveryDependency()
    {
        var checking = new DocumentFeatureAvailability(true, true, true, true, true, true);
        Assert.False(checking.Ready(DocumentFeatureAvailability.Feature.Paddle));

        var runtimeOnly = new DocumentFeatureAvailability(false, true, false, false, false, false);
        Assert.False(runtimeOnly.Ready(DocumentFeatureAvailability.Feature.Paddle));
        Assert.True(runtimeOnly.Ready(DocumentFeatureAvailability.Feature.Office));
        Assert.False(runtimeOnly.Ready(DocumentFeatureAvailability.Feature.Codex));

        var full = new DocumentFeatureAvailability(false, true, true, true, true, true);
        Assert.True(full.Ready(DocumentFeatureAvailability.Feature.Paddle));
        Assert.True(full.Ready(DocumentFeatureAvailability.Feature.Codex));
        Assert.True(full.Ready(DocumentFeatureAvailability.Feature.Semantic));

        var noRuntime = new DocumentFeatureAvailability(false, false, true, true, true, true);
        Assert.False(noRuntime.Ready(DocumentFeatureAvailability.Feature.Office));
    }
}
