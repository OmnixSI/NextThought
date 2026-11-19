using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for screen delta, document classifier, skills, voice profile and the BLE protocol.</summary>
public sealed class SmallPortsBatch4Tests
{
    [Fact]
    public void ScreenDeltaIgnoresIdenticalFrames()
    {
        var frame = new byte[20 * 20 * 4];
        Assert.Null(ScreenDelta.Region(frame, frame, 20, 20));
        Assert.Equal([0d, 0d, 1d, 1d], ScreenDelta.Region(null, frame, 20, 20)!);
        Assert.Null(ScreenDelta.Region(null, new byte[10], 20, 20));
    }

    [Fact]
    public void ScreenDeltaBoundsMeaningfulChanges()
    {
        var width = 100;
        var height = 100;
        var previous = new byte[width * height * 4];
        var current = (byte[])previous.Clone();
        for (var y = 48; y <= 49; y++)
            for (var x = 48; x <= 50; x++)
            {
                var offset = (y * width + x) * 4;
                current[offset] = 40;
                current[offset + 1] = 40;
                current[offset + 2] = 40;
            }
        var region = ScreenDelta.Region(previous, current, width, height);
        Assert.NotNull(region);
        Assert.Equal(0.45, region![0], 3);
        Assert.Equal(0.45, region[1], 3);
        Assert.Equal(0.09, region[2], 3);
        Assert.Equal(0.08, region[3], 3);

        var almostAll = (byte[])previous.Clone();
        for (var index = 0; index < almostAll.Length; index++) almostAll[index] = 200;
        Assert.Equal([0d, 0d, 1d, 1d], ScreenDelta.Region(previous, almostAll, width, height)!);
    }

    [Fact]
    public void ClassifierUsesTheFileNameFirst()
    {
        Assert.Equal("简历", DocumentClassifier.Classify("张三_简历.pdf", "内容", ["简历", "论文", "其他"]));
        Assert.Equal("论文", DocumentClassifier.Classify("paper_v2.pdf", "", ["简历", "论文"]));
        Assert.Equal("岗位 JD", DocumentClassifier.Classify("岗位JD.txt", "", ["岗位 JD", "其他"]));
    }

    [Fact]
    public void ClassifierRefusesWeakOrUnknownMatches()
    {
        Assert.Null(DocumentClassifier.Classify("notes.txt", "本论文讨论方法", ["论文"]));
        Assert.Null(DocumentClassifier.Classify("x.txt", "无关内容", []));
        Assert.Equal("其他", DocumentClassifier.Classify("简历.pdf", "", ["其他"]));
    }

    [Fact]
    public void SkillParsesFrontMatterAndHeading()
    {
        var skill = Skill.Parse("""
            ---
            name: "技术讨论表达"
            description: 结论清楚，依据充分
            ---
            # 技术讨论表达

            正文内容。
            """, "skill.md");
        Assert.Equal("技术讨论表达", skill.Name);
        Assert.Equal("结论清楚，依据充分", skill.Summary);
        Assert.StartsWith("# 技术讨论表达", skill.Content);

        var heading = Skill.Parse("# 我的风格\n先说结论。", "SKILL.md");
        Assert.Equal("我的风格", heading.Name);
        Assert.StartsWith("# 我的风格", heading.Content);
        Assert.Contains("先说结论。", heading.Content);

        var unnamed = Skill.Parse("没有标题的正文", "SKILL.md");
        Assert.Equal("未命名技能", unnamed.Name);
    }

    [Fact]
    public void ActiveSkillsAreEnabledAndAttachedOrGlobal()
    {
        var a = new Skill { Name = "a", Global = true };
        var b = new Skill { Name = "b" };
        var c = new Skill { Name = "c", Enabled = false };
        var active = Skill.Active([a, b, c], [b.Id]);
        Assert.Equal(["a", "b"], active.Select(skill => skill.Name));
    }

    [Fact]
    public void VoiceEnrollmentAveragesConsistentClips()
    {
        var result = VoiceEnrollment.Make([[1f, 0f], [2f, 0f], [3f, 0f]]);
        Assert.Null(result.Failure);
        Assert.NotNull(result.Profile);
        Assert.Equal(3, result.Profile!.Clips);
        Assert.True(result.Profile.IsUsable);
        Assert.True(result.Profile.Recognises([1f, 0f]));
        Assert.False(result.Profile.Recognises([0f, 1f]));
    }

    [Fact]
    public void VoiceEnrollmentRejectsTooFewAndInconsistentClips()
    {
        var few = VoiceEnrollment.Make([[1f, 0f], [1f, 0f]]);
        Assert.Equal(VoiceEnrollmentFailure.TooFewClips, few.Failure);
        Assert.Equal(2, few.UsableClips);

        var inconsistent = VoiceEnrollment.Make([[1f, 0f], [0f, 1f], [1f, 0f]]);
        Assert.Equal(VoiceEnrollmentFailure.Inconsistent, inconsistent.Failure);

        var zeros = VoiceEnrollment.Make([[0f, 0f], [0f, 0f], [0f, 0f]]);
        Assert.Equal(VoiceEnrollmentFailure.TooFewClips, zeros.Failure);
    }

    [Fact]
    public void VoiceProfileRequiresTheSameModel()
    {
        var profile = new VoiceProfile { Embedding = [1f, 0f], Clips = 3, Model = "other" };
        Assert.False(profile.IsUsable);
        Assert.False(profile.Recognises([1f, 0f]));
    }

    [Fact]
    public void FragmentsCarryTheHeaderAndRoundTrip()
    {
        var data = Enumerable.Range(0, 100).Select(index => (byte)index).ToArray();
        var fragments = CompanionBLEProtocol.Fragments(data, 7, 20);
        Assert.Equal(9, fragments.Count);
        Assert.All(fragments.Take(8), packet => Assert.Equal(20, packet.Length));
        Assert.Equal(12, fragments[^1].Length);
        Assert.Equal(7u, (uint)(fragments[0][0] << 24 | fragments[0][1] << 16 | fragments[0][2] << 8 | fragments[0][3]));
        Assert.Equal(9, fragments[0][6] << 8 | fragments[0][7]);

        var reassembler = new CompanionBLEProtocol.Reassembler();
        (uint Id, byte[] Data)? whole = null;
        foreach (var packet in fragments.Reverse()) whole = reassembler.Receive(packet);
        Assert.NotNull(whole);
        Assert.Equal(7u, whole!.Value.Id);
        Assert.Equal(data, whole.Value.Data);
    }

    [Fact]
    public void FragmentsRejectSmallMtuAndOversizedData()
    {
        Assert.Empty(CompanionBLEProtocol.Fragments([1, 2, 3], 1, 10));
        Assert.Empty(CompanionBLEProtocol.Fragments(new byte[CompanionBLEProtocol.MaximumBytes + 1], 1, 100));
    }

    [Fact]
    public void OutboxSendsStateFragmentsAndWaitsForTheAck()
    {
        var outbox = new CompanionBLEOutbox(41);
        outbox.EnqueueState(Enumerable.Repeat((byte)1, 30).ToArray());
        var first = outbox.Peek(20);
        Assert.NotNull(first);
        Assert.Equal(42u, outbox.ActiveMessageId);
        outbox.DidSend();
        var second = outbox.Peek(20);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
        outbox.DidSend();
        outbox.DidSend();
        Assert.Null(outbox.Peek(20));
        outbox.Acknowledge(42);
        Assert.Null(outbox.ActiveMessageId);
        Assert.Null(outbox.Peek(20));
    }

    [Fact]
    public void OutboxReplaysTheSameMessageAfterATimeout()
    {
        var outbox = new CompanionBLEOutbox(1);
        outbox.EnqueueState([1, 2, 3, 4]);
        var start = DateTimeOffset.UtcNow;
        var first = outbox.Peek(20, start);
        Assert.NotNull(first);
        outbox.DidSend(start);
        for (var round = 0; round < 4; round++)
        {
            var replay = outbox.Peek(20, start.AddSeconds(3 * (round + 1)));
            if (round < 3) { Assert.Equal(first, replay); outbox.DidSend(start.AddSeconds(3 * (round + 1))); }
        }
        Assert.Equal(1, outbox.FailureCount);
        Assert.Null(outbox.ActiveMessageId);
    }

    [Fact]
    public void OutboxKeepsAtMostEightAcknowledgements()
    {
        var outbox = new CompanionBLEOutbox(0);
        for (var index = 0; index < 10; index++) outbox.EnqueueAcknowledgement([(byte)index]);
        for (var index = 0; index < 8; index++)
        {
            Assert.NotNull(outbox.Peek(20));
            outbox.DidSend();
            outbox.Acknowledge(outbox.ActiveMessageId!.Value);
        }
        Assert.Null(outbox.Peek(20));
    }
}
