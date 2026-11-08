using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Tests;

public sealed class CoreRegressionTests
{
    private sealed record Sample(string Text, bool Expected);
    [Fact]
    public void OriginalBilingualCorpusMeetsUpstreamRecallAndFalseTriggerBudget()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Fixtures", "question-corpus.json");
        var samples = JsonSerializer.Deserialize<Sample[]>(File.ReadAllText(file), JsonDefaults.Options)!;
        var positives = 0; var hits = 0; var falseTriggers = new List<string>();
        foreach (var sample in samples)
        {
            var result = QuestionIntentClassifier.Assess(sample.Text);
            var trigger = result.IsActionable && result.Score >= QuestionIntentClassifier.Thresholds(MeetingMode.Meeting).Actionable;
            if (sample.Expected) { positives++; if (trigger) hits++; }
            else if (trigger) falseTriggers.Add(sample.Text);
        }
        Assert.Equal(60, samples.Length);
        Assert.True((double)hits/positives >= .9, $"Recall {hits}/{positives}");
        Assert.True(falseTriggers.Count <= 2, string.Join("\n", falseTriggers));
    }
    [Theory]
    [InlineData("介绍一下你自己", UtteranceIntent.ExplanationRequest)]
    [InlineData("Why did you choose Swift?", UtteranceIntent.Question)]
    [InlineData("那缺点呢", UtteranceIntent.FollowUp)]
    [InlineData("Can you", UtteranceIntent.Incomplete)]
    [InlineData("如果流量突然翻倍，", UtteranceIntent.Incomplete)]
    [InlineData("我来介绍一下今天的议程", UtteranceIntent.Statement)]
    public void PreservesOriginalIntentCases(string text, UtteranceIntent expected) => Assert.Equal(expected, QuestionIntentClassifier.Assess(text).Intent);
    [Fact]
    public void StablePartialAndFinalDoNotTriggerDuplicateAnswers()
    {
        var tracker = new QuestionCandidateTracker(); var now = DateTimeOffset.UtcNow; var id = Guid.NewGuid();
        var first = tracker.Ingest("How does", false, id, now);
        var second = tracker.Ingest("How does a database index work?", false, id, now.AddSeconds(.3));
        Assert.Equal(first.Candidate!.Id, second.Candidate!.Id); Assert.Equal(1, second.Candidate.Revision);
        Assert.Equal(CandidateKind.None, tracker.Tick(now.AddSeconds(.5)).Kind);
        Assert.Equal(CandidateKind.Trigger, tracker.Tick(now.AddSeconds(1)).Kind);
        Assert.Equal(CandidateKind.None, tracker.Ingest("How does a database index work?", true, id, now.AddSeconds(2)).Kind);
        Assert.Equal(CandidateKind.None, tracker.Ingest("How does a database index work?", true, Guid.NewGuid(), now.AddSeconds(3)).Kind);
    }
    [Fact]
    public void SplitQuestionJoinsButStaleFragmentDoesNot()
    {
        var tracker = new QuestionCandidateTracker(); var now = DateTimeOffset.UtcNow;
        Assert.Equal(CandidateKind.Updated, tracker.Ingest("Can you", true, Guid.NewGuid(), now).Kind);
        var joined = tracker.Ingest("explain this design", true, Guid.NewGuid(), now.AddSeconds(2));
        Assert.Equal(CandidateKind.Trigger, joined.Kind); Assert.Equal("Can you explain this design", joined.Candidate!.Text);
        tracker = new(); tracker.Ingest("Can you", true, Guid.NewGuid(), now);
        Assert.Equal(CandidateKind.None, tracker.Ingest("The meeting has ended", true, Guid.NewGuid(), now.AddSeconds(9)).Kind);
    }
    [Fact]
    public void RevisedLongQuestionReplacesTheEarlyTrigger()
    {
        var tracker = new QuestionCandidateTracker(); var now = DateTimeOffset.UtcNow; var id = Guid.NewGuid();
        tracker.Ingest("你为什么选择这个方案？", false, id, now);
        var early = tracker.Tick(now.AddSeconds(1)); Assert.Equal(CandidateKind.Trigger, early.Kind);
        var final = tracker.Ingest("你为什么选择这个方案，而不是采用分布式消息队列呢？", true, id, now.AddSeconds(3));
        Assert.Equal(CandidateKind.Trigger, final.Kind); Assert.Equal(early.Candidate!.Id, final.Candidate!.Supersedes);
    }
    [Fact]
    public void VadClosesOneUtteranceAndNeverDuplicatesAnOverlapTail()
    {
        var vad = new VoiceActivitySegmenter(); var voice = Enumerable.Range(0, 1600).Select(i => (short)(Math.Sin(i*.15)*4000)).ToArray();
        for (var i = 0; i < 8; i++) Assert.Null(vad.Append(voice));
        SpeechSegment? final = null;
        for (var i = 0; i < 8; i++) final ??= vad.Append(new short[1600]);
        Assert.NotNull(final); Assert.True(final!.Samples.Length >= 12800); Assert.Null(vad.Flush());
        vad = new(); var events = new List<SpeechSegment>();
        for (var i = 0; i < 120; i++) if (vad.Append(voice) is { } segment) events.Add(segment);
        Assert.Single(events); Assert.Null(vad.Flush());
    }
}
