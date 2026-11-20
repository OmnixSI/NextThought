using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from Tests/NextThoughtCoreTests/VoiceProfileTests.swift: building the saved
/// voiceprint from several recordings, deciding whether a meeting voice is the user, and how "我"
/// shows in the labels. Small hand-made vectors, so every similarity can be worked out by hand.
/// </summary>
public sealed class VoiceProfileParityTests
{
    // MARK: Enrollment

    [Fact]
    public void AveragesConsistentRecordings()
    {
        var result = VoiceEnrollment.Make(
        [
            new float[] { 1, 0, 0, 0 },
            new float[] { 0.8f, 0.6f, 0, 0 },
            new float[] { 0.8f, -0.6f, 0, 0 }
        ]);
        var profile = result.Profile;
        Assert.NotNull(profile);
        Assert.Equal(3, profile!.Clips);
        Assert.Equal(VoiceProfile.ModelId, profile.Model);
        // The y parts cancel: the average points along the first axis.
        Assert.True(Math.Abs(profile.Embedding[0] - 1) < 0.0001);
        Assert.True(Math.Abs(profile.Embedding[1]) < 0.0001);
        Assert.True(profile.IsUsable);
    }

    [Fact]
    public void NeedsAtLeastThreeRecordings()
    {
        var result = VoiceEnrollment.Make([new float[] { 1, 0, 0, 0 }, new float[] { 1, 0, 0, 0 }]);
        Assert.Null(result.Profile);
        Assert.Equal(VoiceEnrollmentFailure.TooFewClips, result.Failure);
        Assert.Equal(2, result.UsableClips);
        Assert.Equal(3, VoiceProfile.MinClips);
    }

    [Fact]
    public void ZeroAndMismatchedRecordingsDoNotCount()
    {
        var result = VoiceEnrollment.Make(
        [
            new float[] { 1, 0, 0, 0 },
            new float[] { 0, 0, 0, 0 },
            new float[] { 1, 0 },
            new float[] { 1, 0, 0, 0 }
        ]);
        Assert.Null(result.Profile);
        Assert.Equal(VoiceEnrollmentFailure.TooFewClips, result.Failure);
        Assert.Equal(2, result.UsableClips);
        Assert.Equal(3, VoiceProfile.MinClips);
    }

    [Fact]
    public void ARecordingOfAnotherVoiceIsDropped()
    {
        // Against the sum (2.6, 0, 1, 0) the stray one scores 1 / 2.786 = 0.36 < 0.5 and goes;
        // the three that stay still make a profile.
        var profile = VoiceEnrollment.Make(
        [
            new float[] { 1, 0, 0, 0 },
            new float[] { 0.8f, 0.6f, 0, 0 },
            new float[] { 0.8f, -0.6f, 0, 0 },
            new float[] { 0, 0, 1, 0 }
        ]).Profile;
        Assert.NotNull(profile);
        Assert.Equal(3, profile!.Clips);
        Assert.True(Math.Abs(profile.Embedding[2]) < 0.0001);
    }

    [Fact]
    public void RecordingsThatDoNotSoundAlikeFail()
    {
        // Five different directions: each is 1 / sqrt(5) = 0.447 < 0.5 to the sum, so none is kept.
        var clips = Enumerable.Range(0, 5)
            .Select(index => Enumerable.Range(0, 5).Select(unit => unit == index ? 1f : 0f).ToArray())
            .ToList();
        var result = VoiceEnrollment.Make(clips);
        Assert.Null(result.Profile);
        Assert.Equal(VoiceEnrollmentFailure.Inconsistent, result.Failure);
    }

    // MARK: Recognising

    [Fact]
    public void RecognisesAVoiceCloseToTheProfile()
    {
        var profile = new VoiceProfile
        {
            Embedding = [1f, 0, 0, 0],
            Clips = 3,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(0)
        };
        // Only the direction of the cluster sum matters, not its length.
        Assert.True(profile.Recognises([3f, 0, 0, 0]));
        Assert.True(profile.Recognises([0.6f, 0.8f, 0, 0]));     // cosine 0.6
        Assert.False(profile.Recognises([0.5f, 0.866f, 0, 0]));  // cosine 0.5 < 0.55
        Assert.False(profile.Recognises([0f, 0, 0, 0]));         // a merged-away voice
        Assert.False(profile.Recognises([1f, 0, 0]));            // another model's size
    }

    [Fact]
    public void AProfileOfAnotherModelIsNotUsed()
    {
        var profile = new VoiceProfile
        {
            Embedding = [1f, 0, 0, 0],
            Clips = 3,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(0)
        } with { Model = "older-model" };
        Assert.False(profile.IsUsable);
        Assert.False(profile.Recognises([1f, 0, 0, 0]));
        Assert.False(new VoiceProfile
        {
            Embedding = [0f, 0, 0, 0],
            Clips = 3,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(0)
        }.IsUsable);
    }

    [Fact]
    public void ProfileSurvivesEncoding()
    {
        var profile = new VoiceProfile
        {
            Embedding = [0.5f, 0.5f, 0.5f, 0.5f],
            Clips = 4,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000)
        };
        var data = JsonSerializer.SerializeToUtf8Bytes(profile, JsonDefaults.Options);
        var decoded = JsonSerializer.Deserialize<VoiceProfile>(data, JsonDefaults.Options);
        Assert.NotNull(decoded);
        Assert.Equal(profile.Model, decoded!.Model);
        Assert.Equal(profile.Clips, decoded.Clips);
        Assert.Equal(profile.CreatedAt, decoded.CreatedAt);
        Assert.Equal(profile.Embedding, decoded.Embedding);
        Assert.True(decoded.IsUsable);
    }

    // MARK: Labels with "我"

    private static TranscriptLine Line(string speaker, int? voice, string text = "这是一句比较长的话") => new()
    {
        Date = DateTimeOffset.FromUnixTimeSeconds(0),
        Speaker = speaker,
        Text = text,
        Voice = voice
    };

    [Fact]
    public void MyVoiceIsLabelledEvenWithFewLines()
    {
        var lines = new[] { Line("room", 0), Line("room", 1) };
        var labels = SpeakerLabels.PromptLabels(lines, null, new HashSet<int> { 0 }, english: false);
        Assert.Equal("我", labels[lines[0].Id]);
        Assert.False(labels.ContainsKey(lines[1].Id));
        Assert.Equal("Me", SpeakerLabels.PromptLabels(lines, null, new HashSet<int> { 0 }, english: true)[lines[0].Id]);
    }

    [Fact]
    public void MyVoiceDoesNotCountAsAnotherPerson()
    {
        // Voice 0 is the user, voice 1 the only other one: a one-to-one stays 对方 / 现场, no "说话人 1".
        var lines = Enumerable.Range(0, 6).Select(_ => Line("room", 0))
            .Concat(Enumerable.Range(0, 6).Select(_ => Line("room", 1))).ToList();
        Assert.Empty(SpeakerLabels.VoiceNumbers(lines, new HashSet<int> { 0 }));
        // Without the mark, both are numbered as before.
        Assert.Equal(2, SpeakerLabels.VoiceNumbers(lines).Count);
    }

    [Fact]
    public void OthersAreStillNumberedAroundMe()
    {
        var lines = Enumerable.Range(0, 6).Select(_ => Line("room", 0))
            .Concat(Enumerable.Range(0, 5).Select(_ => Line("room", 1)))
            .Concat(Enumerable.Range(0, 5).Select(_ => Line("room", 2))).ToList();
        var numbers = SpeakerLabels.VoiceNumbers(lines, new HashSet<int> { 0 });
        Assert.Equal(2, numbers.Count);
        Assert.Equal(1, numbers[1]);
        Assert.Equal(2, numbers[2]);
        var labels = SpeakerLabels.PromptLabels(lines, new Dictionary<string, string> { ["2"] = "小王" },
            new HashSet<int> { 0 }, english: false);
        Assert.Equal("我", labels[lines[0].Id]);
        Assert.Equal("说话人 1", labels[lines[7].Id]);
        Assert.Equal("小王", labels[lines[12].Id]);
    }

    [Fact]
    public void MeLinesLeaveTheUsersOwnLinesAlone()
    {
        var mine = Line("me", null);
        var recognised = Line("room", 3);
        Assert.Equal(new[] { recognised.Id }, SpeakerLabels.MeLines([mine, recognised], new HashSet<int> { 3 }));
        Assert.Empty(SpeakerLabels.MeLines([mine, recognised], new HashSet<int>()));
    }

    [Fact]
    public void RunStartsTreatRecognisedLinesAsMe()
    {
        var first = Line("me", null);
        var second = Line("room", 3);
        var third = Line("room", 4);
        var starts = SpeakerLabels.RunStarts([first, second, third], new Dictionary<Guid, int>(),
            new HashSet<Guid> { second.Id });
        // me, me (same speaker: no new tag), then somebody else.
        Assert.Equal(new[] { first.Id, third.Id }, starts);
    }

    [Fact]
    public void MergingKeepsTheMark()
    {
        Assert.Equal(new[] { 5 }, SpeakerLabels.MergedMe([2], 2, 5));
        Assert.Equal(new[] { 5 }, SpeakerLabels.MergedMe([5], 2, 5));
        Assert.Equal(new[] { 1 }, SpeakerLabels.MergedMe([1], 2, 5));
        Assert.Null(SpeakerLabels.MergedMe(null, 2, 5));
    }

    [Fact]
    public void NotesPromptMentionsMe()
    {
        var lines = Enumerable.Range(0, 6).Select(_ => Line("room", 0)).ToList();
        var alone = SpeakerLabels.PromptNote(lines, null, new HashSet<int> { 0 }, english: false, inPerson: true);
        Assert.True(alone?.Contains("“我”") == true);
        Assert.Null(SpeakerLabels.PromptNote(lines, null, new HashSet<int>(), english: false, inPerson: true));
    }

    [Fact]
    public void OldMetaWithoutTheFieldStillDecodes()
    {
        var meta = new SessionMeta { StartedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), Mode = "interview" };
        Assert.Null(meta.MeVoices);
    }
}
