using System.Text;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from Tests/NextThoughtCoreTests/SpeakerClusteringTests.swift: the voice grouping
/// rules, the labels the transcript shows, and the backward-compatible fields they add to the
/// session records. Vectors are small hand-made ones, so every similarity can be worked out by hand.
/// </summary>
public sealed class SpeakerClusteringParityTests
{
    // Unit-length axes standing in for voiceprints of three different people.
    private static readonly float[] A = [1, 0, 0, 0];
    private static readonly float[] B = [0, 1, 0, 0];
    private static readonly float[] C = [0, 0, 1, 0];

    // MARK: VoiceClusterer

    [Fact]
    public void ThresholdsAreCicadasTunedValues()
    {
        Assert.Equal(0.5f, VoiceClusterer.Join);
        Assert.Equal(0.5f, VoiceClusterer.StartNew);
        Assert.Equal(2.0, VoiceClusterer.NewSeconds);
    }

    [Fact]
    public void SameVoiceJoinsEvenWhenShort()
    {
        var clusterer = new VoiceClusterer();
        Assert.Equal(0, clusterer.Label(A, 3));
        // cosine to [1,0,0,0] is 0.99 / 1 = 0.99 >= 0.5; a short line may join, it just may not start a voice.
        Assert.Equal(0, clusterer.Label([0.99f, 0.1f, 0, 0], 0.6));
        Assert.Equal(1, clusterer.VoiceCount);
        Assert.True(Math.Abs(clusterer.Sums[0][0] - 1.99) < 0.0001);
    }

    [Fact]
    public void DifferentVoiceStartsANewOne()
    {
        var clusterer = new VoiceClusterer();
        Assert.Equal(0, clusterer.Label(A, 3));
        // b is orthogonal to a: similarity 0 < 0.5 and the line lasts 3 s >= 2 s.
        Assert.Equal(1, clusterer.Label(B, 3));
        // Later lines go to the closest of the two.
        Assert.Equal(1, clusterer.Label([0.1f, 0.99f, 0, 0], 1));
        Assert.Equal(0, clusterer.Label([0.99f, 0.1f, 0, 0], 1));
        Assert.Equal(2, clusterer.VoiceCount);
    }

    [Fact]
    public void AShortLineNeverStartsAVoice()
    {
        var clusterer = new VoiceClusterer();
        // Nothing to join and under 2 s: stays unlabelled, no voice is created.
        Assert.Null(clusterer.Label(A, 1.0));
        Assert.Equal(0, clusterer.VoiceCount);
        Assert.Equal(0, clusterer.Label(A, 2.0));
        // 1.9 s of an unlike voice: unlabelled again.
        Assert.Null(clusterer.Label(B, 1.9));
        Assert.Equal(1, clusterer.VoiceCount);
    }

    [Fact]
    public void SimilarityAroundTheThreshold()
    {
        var clusterer = new VoiceClusterer();
        _ = clusterer.Label(A, 3);
        // [0.6, 0.8, 0, 0] has cosine 0.6 to a: joins. [0.4, 0.9165, 0, 0] has 0.4: starts a new voice.
        Assert.Equal(0, clusterer.Label([0.6f, 0.8f, 0, 0], 3));
        var fresh = new VoiceClusterer();
        _ = fresh.Label(A, 3);
        Assert.Equal(1, fresh.Label([0.4f, 0.9165f, 0, 0], 3));
    }

    [Fact]
    public void MergeSendsLaterLinesToTheKeptVoice()
    {
        var clusterer = new VoiceClusterer();
        Assert.Equal(0, clusterer.Label(A, 3));
        Assert.Equal(1, clusterer.Label(B, 3));
        clusterer.Merge(1, 0);
        Assert.Equal(new float[] { 0, 0, 0, 0 }, clusterer.Sums[1]);
        Assert.Equal(new float[] { 1, 1, 0, 0 }, clusterer.Sums[0]);
        // b against [1,1,0,0]: dot 1 / sqrt(2) = 0.707 >= 0.5 -> voice 0, never the emptied voice 1.
        Assert.Equal(0, clusterer.Label(B, 1));
        Assert.Equal(new float[] { 1, 2, 0, 0 }, clusterer.Sums[0]);
        // a against [1,2,0,0] is only 0.447 (< 0.5): too weak to join, too short to start a voice.
        Assert.Null(clusterer.Label(A, 1));
        // [0.8, 0.6, 0, 0] against [1,2,0,0]: (0.8 + 1.2) / sqrt(5) = 0.894 -> joins voice 0.
        Assert.Equal(0, clusterer.Label([0.8f, 0.6f, 0, 0], 1));
        // A third person gets the next id: ids are never reused.
        Assert.Equal(2, clusterer.Label(C, 3));
    }

    [Fact]
    public void MergeIgnoresNonsense()
    {
        var clusterer = new VoiceClusterer();
        _ = clusterer.Label(A, 3);
        _ = clusterer.Label(B, 3);
        var before = clusterer.Sums.Select(sum => sum.ToArray()).ToList();
        clusterer.Merge(0, 0);
        clusterer.Merge(5, 0);
        clusterer.Merge(0, -1);
        Assert.Equal(before.SelectMany(sum => sum), clusterer.Sums.SelectMany(sum => sum));
    }

    // MARK: SpeakerLabels

    private static TranscriptLine Line(string speaker, int? voice, string text = "这是一句比较长的话") => new()
    {
        Date = DateTimeOffset.FromUnixTimeSeconds(0),
        Speaker = speaker,
        Text = text,
        Voice = voice
    };

    [Fact]
    public void SubstantiveMeansWordsOrCharacters()
    {
        Assert.True(SpeakerLabels.Substantive("I think so"));
        Assert.True(SpeakerLabels.Substantive("我觉得可以"));
        Assert.False(SpeakerLabels.Substantive("Yeah."));
        Assert.False(SpeakerLabels.Substantive("ok sure"));
        Assert.False(SpeakerLabels.Substantive("好的"));
        Assert.False(SpeakerLabels.Substantive("..."));
        Assert.False(SpeakerLabels.Substantive("1 2 3"));   // no letters: not words
    }

    [Fact]
    public void NumbersFollowTheOrderVoicesReachFiveLines()
    {
        // Voice 3 speaks first but voice 7 reaches five lines first (index 6 vs index 9).
        var lines = new List<TranscriptLine>();
        for (var index = 0; index < 2; index++) lines.Add(Line("remote", 3));
        for (var index = 0; index < 5; index++) lines.Add(Line("remote", 7));
        for (var index = 0; index < 3; index++) lines.Add(Line("remote", 3));
        var numbers = SpeakerLabels.VoiceNumbers(lines);
        Assert.Equal(2, numbers.Count);
        Assert.Equal(1, numbers[7]);
        Assert.Equal(2, numbers[3]);
        // A later line never renumbers anyone.
        lines.Add(Line("remote", 9));
        for (var index = 0; index < 5; index++) lines.Add(Line("remote", 9));
        var later = SpeakerLabels.VoiceNumbers(lines);
        Assert.Equal(3, later.Count);
        Assert.Equal(1, later[7]);
        Assert.Equal(2, later[3]);
        Assert.Equal(3, later[9]);
        Assert.Equal(3, SpeakerLabels.ShownCount(lines));
    }

    [Fact]
    public void ASingleVoiceShowsNoNumbers()
    {
        var lines = Enumerable.Range(0, 8).Select(_ => Line("remote", 0)).ToList();
        Assert.Empty(SpeakerLabels.VoiceNumbers(lines));
        Assert.Empty(SpeakerLabels.Numbers(lines));
        // Two voices, but the second has only four lines: still one on show, so nothing shows.
        var other = lines.Concat(Enumerable.Range(0, 4).Select(_ => Line("remote", 1))).ToList();
        Assert.Empty(SpeakerLabels.VoiceNumbers(other));
    }

    [Fact]
    public void ShortLinesAndMyLinesDoNotCount()
    {
        var lines = Enumerable.Range(0, 5).Select(_ => Line("remote", 0)).ToList();
        lines.AddRange(Enumerable.Range(0, 9).Select(_ => Line("remote", 1, "嗯")));   // not substantive
        lines.AddRange(Enumerable.Range(0, 9).Select(_ => Line("me", 2)));            // the user is never told apart
        Assert.Empty(SpeakerLabels.VoiceNumbers(lines));
        lines.AddRange(Enumerable.Range(0, 5).Select(_ => Line("remote", 1)));
        var numbers = SpeakerLabels.VoiceNumbers(lines);
        Assert.Equal(2, numbers.Count);
        Assert.Equal(1, numbers[0]);
        Assert.Equal(2, numbers[1]);
    }

    [Fact]
    public void TheRoomIsToldApartToo()
    {
        var lines = Enumerable.Range(0, 5).Select(_ => Line("room", 4)).ToList();
        lines.AddRange(Enumerable.Range(0, 5).Select(_ => Line("room", 1)));
        var numbers = SpeakerLabels.VoiceNumbers(lines);
        Assert.Equal(2, numbers.Count);
        Assert.Equal(1, numbers[4]);
        Assert.Equal(2, numbers[1]);
    }

    [Fact]
    public void NamesAndLabels()
    {
        var lines = Enumerable.Range(0, 5).Select(_ => Line("remote", 7)).ToList();
        lines.AddRange(Enumerable.Range(0, 5).Select(_ => Line("remote", 3)));
        var names = SpeakerLabels.Names(new Dictionary<string, string> { ["7"] = "小王", ["3"] = "" }, lines);
        Assert.Single(names);
        Assert.Equal("小王", names[1]);
        Assert.Empty(SpeakerLabels.Names(null, lines));
        Assert.Equal("小王", SpeakerLabels.Label(1, new Dictionary<int, string> { [1] = "小王" }));
        Assert.Equal("说话人 2", SpeakerLabels.Label(2, new Dictionary<int, string> { [1] = "小王" }));
        Assert.Equal("Speaker 2", SpeakerLabels.Label(2, new Dictionary<int, string>(), english: true));
    }

    [Fact]
    public void RunStartsMarkWhereTheSpeakerChanges()
    {
        var mine = Line("me", null);
        var lines = Enumerable.Range(0, 5).Select(_ => Line("remote", 7)).ToList();
        lines.AddRange(Enumerable.Range(0, 5).Select(_ => Line("remote", 3)));
        lines.Insert(2, mine);
        var numbers = SpeakerLabels.Numbers(lines);
        var starts = SpeakerLabels.RunStarts(lines, numbers);
        // Indices: 0,1 speaker 1 | 2 me | 3,4,5 speaker 1 | 6...10 speaker 2.
        Assert.Equal(4, starts.Count);
        Assert.Contains(lines[0].Id, starts);
        Assert.Contains(lines[2].Id, starts);
        Assert.Contains(lines[3].Id, starts);
        Assert.Contains(lines[6].Id, starts);
    }

    [Fact]
    public void NotesLabelsUseNamesThenNumbers()
    {
        var lines = Enumerable.Range(0, 5).Select(_ => Line("remote", 7)).ToList();
        lines.AddRange(Enumerable.Range(0, 5).Select(_ => Line("remote", 3)));
        lines.Add(Line("remote", null));
        lines.Add(Line("me", null));
        var labels = SpeakerLabels.PromptLabels(lines, new Dictionary<string, string> { ["7"] = "  小王\n" },
            null, english: false);
        Assert.Equal("小王", labels[lines[0].Id]);
        Assert.Equal("说话人 2", labels[lines[5].Id]);
        Assert.False(labels.ContainsKey(lines[10].Id));    // not on show: keeps 对方
        Assert.False(labels.ContainsKey(lines[11].Id));    // me
        Assert.Equal("Speaker 2", SpeakerLabels.PromptLabels(lines, null, null, english: true)[lines[5].Id]);

        var note = SpeakerLabels.PromptNote(lines, new Dictionary<string, string> { ["7"] = "小王" },
            null, english: false, inPerson: false);
        Assert.True(note?.Contains("小王、说话人 2") == true);
        Assert.True(note?.Contains("“对方”") == true);
        var room = SpeakerLabels.PromptNote(lines, null, null, english: false, inPerson: true);
        Assert.True(room?.Contains("“现场”") == true);
        Assert.Null(SpeakerLabels.PromptNote([Line("remote", 0)], null, null, english: false, inPerson: false));
    }

    [Fact]
    public void TranscriptTextCarriesTheLabels()
    {
        // C# port of NotesPrompt.transcript (labels included) is SessionSummaryPrompt.TranscriptText.
        var start = DateTimeOffset.FromUnixTimeSeconds(0);
        var first = new TranscriptLine
        {
            Date = start.AddSeconds(65),
            Speaker = "remote",
            Text = "周四交",
            Voice = 1
        };
        var mine = new TranscriptLine { Date = start.AddSeconds(70), Speaker = "me", Text = "好" };
        var text = SessionSummaryPrompt.TranscriptText([first, mine], start,
            new Dictionary<Guid, string> { [first.Id] = "小王" });
        Assert.Equal("[01:05] 小王：周四交\n[01:10] 我：好", text);
        // Without labels the old format is unchanged.
        Assert.Equal("[01:05] 对方：周四交\n[01:10] 我：好", SessionSummaryPrompt.TranscriptText([first, mine], start));
    }

    [Fact]
    public void CleanNameAndMerge()
    {
        Assert.Equal("小王", SpeakerLabels.CleanName("  小王 "));
        Assert.Equal("a b", SpeakerLabels.CleanName("a\nb"));
        Assert.Null(SpeakerLabels.CleanName("   "));
        Assert.Equal(24, SpeakerLabels.CleanName(new string('x', 40))!.Length);
        // Merging 3 into 7: 3's name moves over only when 7 has none.
        var moved = SpeakerLabels.Merged(new Dictionary<string, string> { ["3"] = "小李" }, 3, 7);
        Assert.NotNull(moved);
        Assert.Equal(new[] { "7" }, moved!.Keys);
        Assert.Equal("小李", moved["7"]);
        var kept = SpeakerLabels.Merged(new Dictionary<string, string> { ["3"] = "小李", ["7"] = "小王" }, 3, 7);
        Assert.NotNull(kept);
        Assert.Equal(new[] { "7" }, kept!.Keys);
        Assert.Equal("小王", kept["7"]);
        Assert.Null(SpeakerLabels.Merged(null, 3, 7));
    }

    // MARK: Records stay readable

    [Fact]
    public void OldRecordsDecodeWithoutVoice()
    {
        const string oldLine =
            """{"id":"6F9619FF-8B86-D011-B42D-00C04FC964FF","date":"2026-10-26T10:00:00Z","speaker":"remote","text":"你好"}""";
        var decoded = JSONLines.Decode<TranscriptLine>(Encoding.UTF8.GetBytes(oldLine));
        Assert.Single(decoded);
        Assert.Null(decoded[0].Voice);
        Assert.Equal("你好", decoded[0].Text);

        var withVoice = new TranscriptLine
        {
            Date = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
            Speaker = "room",
            Text = "好",
            Voice = 2
        };
        var data = JSONLines.Line(withVoice);
        Assert.Equal(new[] { withVoice }, JSONLines.Decode<TranscriptLine>(data));
        // A line without a voice writes no key at all, so old readers see what they always saw.
        var plain = JSONLines.Line(new TranscriptLine
        {
            Date = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
            Speaker = "me",
            Text = "x"
        });
        Assert.DoesNotContain("voice", Encoding.UTF8.GetString(plain));
    }

    [Fact]
    public void MetaVoiceNamesAreOptional()
    {
        var meta = new SessionMeta { StartedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), Mode = "meeting" };
        var encoded = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(meta, JsonDefaults.Options);
        Assert.DoesNotContain("voiceNames", Encoding.UTF8.GetString(encoded));
        var decoded = System.Text.Json.JsonSerializer.Deserialize<SessionMeta>(encoded, JsonDefaults.Options)!;
        Assert.Null(decoded.VoiceNames);
        meta.VoiceNames = new Dictionary<string, string> { ["1"] = "\u5c0f\u738b" };
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<SessionMeta>(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(meta, JsonDefaults.Options), JsonDefaults.Options)!;
        Assert.Equal("\u5c0f\u738b", roundTrip.VoiceNames?["1"]);
    }

    [Fact]
    public void VoiceTagsRoundTripAsJSONLines()
    {
        var tags = new[] { new SessionVoiceTag(Guid.NewGuid(), 0), new SessionVoiceTag(Guid.NewGuid(), 4) };
        var data = tags.SelectMany(tag => JSONLines.Line(tag)).ToArray();
        Assert.Equal(tags, JSONLines.Decode<SessionVoiceTag>(data));
    }

    // MARK: 3.4.0 hand-named people (J5)

    [Fact]
    public void APersonNamedByHandIsShownHoweverLittleTheySaid()
    {
        TranscriptLine Line(int voice, string text) => new()
        {
            Id = Guid.NewGuid(),
            Date = DateTimeOffset.UtcNow,
            Speaker = "room",
            Text = text,
            Voice = voice
        };
        // One voice does all the talking; two others say a sentence each.
        var lines = Enumerable.Range(0, 8).Select(_ => Line(1, "这一页讲的是缓存一致性的方案")).ToList();
        lines.Insert(3, Line(4, "我有一个问题想问一下"));
        lines.Add(Line(7, "好的谢谢老师的解释"));
        Assert.Empty(SpeakerLabels.VoiceNumbers(lines));
        // Naming the quiet ones puts them on show, after the one who earned a number.
        var named = SpeakerLabels.VoiceNumbers(lines, named: new HashSet<int> { 7, 4 });
        Assert.Equal(3, named.Count);
        Assert.Equal(1, named[1]);
        Assert.Equal(2, named[4]);
        Assert.Equal(3, named[7]);
        var names = SpeakerLabels.Names(new Dictionary<string, string> { ["4"] = "小王" }, lines);
        Assert.Single(names);
        Assert.Equal("小王", names[2]);
        Assert.Equal(9, SpeakerLabels.Numbers(lines, named: new HashSet<int> { 4 }).Count);
        Assert.Equal(13, SpeakerLabels.NewVoice(
            lines, new Dictionary<string, string> { ["9"] = "x" }, new HashSet<int> { 12 }));
    }

    [Fact]
    public void NumbersEarnedByTalkingDoNotMoveWhenSomeoneIsNamed()
    {
        TranscriptLine Line(int voice) => new()
        {
            Id = Guid.NewGuid(),
            Date = DateTimeOffset.UtcNow,
            Speaker = "remote",
            Text = "这是一句足够长的话用来计数",
            Voice = voice
        };
        var lines = new List<TranscriptLine> { Line(9) };
        lines.AddRange(Enumerable.Range(0, 5).Select(_ => Line(3)));
        lines.AddRange(Enumerable.Range(0, 5).Select(_ => Line(5)));
        var numbers = SpeakerLabels.VoiceNumbers(lines);
        Assert.Equal(2, numbers.Count);
        Assert.Equal(1, numbers[3]);
        Assert.Equal(2, numbers[5]);
        var renumbered = SpeakerLabels.VoiceNumbers(lines, named: new HashSet<int> { 9, 5 });
        Assert.Equal(3, renumbered.Count);
        Assert.Equal(1, renumbered[3]);
        Assert.Equal(2, renumbered[5]);
        Assert.Equal(3, renumbered[9]);
    }
}
