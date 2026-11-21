using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for VoiceClusterer and SpeakerLabels (SpeakerClustering.swift).</summary>
public sealed class SpeakerClusteringTests
{
    private const string LongText = "这是一句足够长的中文发言";

    private static TranscriptLine Line(string speaker, int? voice, Guid? id = null, string? text = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Date = DateTimeOffset.UtcNow,
        Speaker = speaker,
        Voice = voice,
        Text = text ?? LongText
    };

    private static float[] Unit(params float[] values)
    {
        var norm = Math.Sqrt(values.Sum(value => value * value));
        return values.Select(value => (float)(value / norm)).ToArray();
    }

    [Fact]
    public void ClustererJoinsSimilarVoicesAndStartsNewOnes()
    {
        var clusterer = new VoiceClusterer();
        Assert.Equal(0, clusterer.Label(Unit(1, 0, 0, 0), 3));
        Assert.Equal(0, clusterer.Label(Unit(0.9f, 0.1f, 0, 0), 3));
        Assert.Equal(1, clusterer.Label(Unit(0, 1, 0, 0), 3));
        Assert.Equal(2, clusterer.VoiceCount);
    }

    [Fact]
    public void ClustererLeavesShortLinesUnlabelled()
    {
        var clusterer = new VoiceClusterer();
        Assert.Equal(0, clusterer.Label(Unit(1, 0, 0, 0), 3));
        Assert.Null(clusterer.Label(Unit(0, 1, 0, 0), 1));
        Assert.Equal(1, clusterer.VoiceCount);
    }

    [Fact]
    public void MergeCombinesSumsAndZeroesTheSource()
    {
        var clusterer = new VoiceClusterer();
        clusterer.Label(Unit(1, 0, 0, 0), 3);
        clusterer.Label(Unit(0, 1, 0, 0), 3);
        clusterer.Merge(1, 0);
        Assert.Equal(2, clusterer.VoiceCount);
        Assert.Equal([1f, 1f, 0f, 0f], clusterer.Sums[0]);
        Assert.All(clusterer.Sums[1], value => Assert.Equal(0, value));
        Assert.Equal(0, VoiceClusterer.Similarity(Unit(0, 1, 0, 0), clusterer.Sums[1]));
    }

    [Fact]
    public void SimilarityHandlesZeroVectors()
    {
        Assert.Equal(0, VoiceClusterer.Similarity(Unit(1, 0), [0f, 0f]));
        Assert.Equal(1, VoiceClusterer.Similarity(Unit(1, 0), [1f, 0f]), 5);
    }

    [Fact]
    public void SubstantiveNeedsThreeWordsOrFourWideCharacters()
    {
        Assert.False(SpeakerLabels.Substantive("Yeah."));
        Assert.False(SpeakerLabels.Substantive("好"));
        Assert.True(SpeakerLabels.Substantive("I think so"));
        Assert.True(SpeakerLabels.Substantive("这是一个测试"));
    }

    [Fact]
    public void VoiceNumbersShowOnlyAfterFiveSubstantiveLinesEach()
    {
        var lines = new List<TranscriptLine>();
        for (var index = 0; index < 5; index++) lines.Add(Line("remote", 0));
        for (var index = 0; index < 4; index++) lines.Add(Line("remote", 1));
        Assert.Empty(SpeakerLabels.VoiceNumbers(lines));
        lines.Add(Line("remote", 1));
        var numbers = SpeakerLabels.VoiceNumbers(lines);
        Assert.Equal(1, numbers[0]);
        Assert.Equal(2, numbers[1]);
    }

    [Fact]
    public void MeLinesNeverCountAndKeepTheirOwnLabel()
    {
        var lines = new List<TranscriptLine>();
        for (var index = 0; index < 5; index++) lines.Add(Line("remote", 0));
        for (var index = 0; index < 5; index++) lines.Add(Line("remote", 1));
        var mine = Line("remote", 5);
        lines.Add(mine);
        lines.Add(Line("me", 0, text: "我自己的话也可以很长很长"));
        var numbers = SpeakerLabels.VoiceNumbers(lines, new HashSet<int> { 0, 5 });
        Assert.Empty(numbers);
        var labels = SpeakerLabels.PromptLabels(lines, null, new HashSet<int> { 0, 5 }, english: false);
        Assert.Equal("我", labels[mine.Id]);
    }

    [Fact]
    public void NamesMapVoicesToTheNumbersOnShow()
    {
        var lines = new List<TranscriptLine>();
        for (var index = 0; index < 5; index++) lines.Add(Line("remote", 0));
        for (var index = 0; index < 5; index++) lines.Add(Line("remote", 1));
        var names = SpeakerLabels.Names(new Dictionary<string, string> { ["0"] = "小明", ["1"] = "小红" }, lines);
        Assert.Equal("小明", names[1]);
        Assert.Equal("小红", names[2]);
        Assert.Equal("小明", SpeakerLabels.Label(1, names));
        Assert.Equal("Speaker 2", SpeakerLabels.Label(2, new Dictionary<int, string>(), english: true));
        Assert.Equal(2, SpeakerLabels.ShownCount(lines));
    }

    [Fact]
    public void RunStartsMarksEachSpeakerChange()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var lines = new[]
        {
            Line("me", null, ids[0], "我先说"),
            Line("remote", 0, ids[1]),
            Line("remote", 0, ids[2]),
            Line("remote", 1, ids[3])
        };
        var numbers = new Dictionary<Guid, int> { [ids[1]] = 1, [ids[2]] = 1, [ids[3]] = 2 };
        var starts = SpeakerLabels.RunStarts(lines, numbers);
        Assert.Contains(ids[0], starts);
        Assert.Contains(ids[1], starts);
        Assert.DoesNotContain(ids[2], starts);
        Assert.Contains(ids[3], starts);
    }

    [Fact]
    public void NamingRewritesLabelsWithoutTouchingLongerNumbers()
    {
        var names = new Dictionary<int, string> { [1] = "甲", [2] = "乙", [12] = "丙" };
        var result = SpeakerLabels.Naming("说话人 2 说，说话人 12 说，说话人 1 说。", names);
        Assert.Equal("乙 说，丙 说，甲 说。", result);
        Assert.Equal("Kai Wu", SpeakerLabels.ShortName("Kai Wu（导师）"));
        Assert.Equal("Kai Wu", SpeakerLabels.ShortName("Kai Wu"));
        Assert.Null(SpeakerLabels.CleanName("  \n "));
        Assert.Equal("小明", SpeakerLabels.CleanName("  小明  "));
        Assert.Equal(24, SpeakerLabels.CleanName(new string('字', 30))!.Length);
    }

    [Fact]
    public void MergedNamesKeepTheTargetAndMoveOnlyWhenEmpty()
    {
        var merged = SpeakerLabels.Merged(new Dictionary<string, string> { ["2"] = "乙" }, 2, 5);
        Assert.Equal("乙", merged!["5"]);
        var kept = SpeakerLabels.Merged(new Dictionary<string, string> { ["2"] = "乙", ["5"] = "戊" }, 2, 5);
        Assert.Equal("戊", kept!["5"]);
        Assert.Equal([5], SpeakerLabels.MergedMe([2], 2, 5)!);
        Assert.Null(SpeakerLabels.MergedMe([], 2, 5));
    }

    [Fact]
    public void PromptNoteExplainsVoiceLabelsWhenTwoAreOnShow()
    {
        var lines = new List<TranscriptLine>();
        for (var index = 0; index < 5; index++) lines.Add(Line("remote", 0));
        for (var index = 0; index < 5; index++) lines.Add(Line("remote", 1));
        var note = SpeakerLabels.PromptNote(lines, null, null, english: false, inPerson: false);
        Assert.NotNull(note);
        Assert.Contains("说话人区分", note!);
        Assert.Contains("说话人 1", note);
        var english = SpeakerLabels.PromptNote(lines, null, null, english: true, inPerson: true);
        Assert.Contains("Speakers", english!);
        Assert.Contains("Room", english);

        var one = new[] { Line("remote", 0) };
        Assert.Null(SpeakerLabels.PromptNote(one, null, null, english: false, inPerson: false));
    }
}
