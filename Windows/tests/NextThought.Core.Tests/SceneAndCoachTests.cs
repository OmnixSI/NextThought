using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Ported from the Mac Core tests for Scene.swift and CoachSummary.swift.</summary>
public sealed class SceneAndCoachTests
{
    [Fact]
    public void ScenePresetsCoverEveryMode()
    {
        foreach (var mode in Enum.GetValues<MeetingMode>())
        {
            var preset = ScenePreset.For(mode);
            Assert.Equal(mode, preset.Mode);
        }
    }

    [Fact]
    public void ScenesChooseCueStyleAndEyeLine()
    {
        var interview = ScenePreset.For(MeetingMode.Interview);
        Assert.Equal(CueStyle.KeyPoints, interview.CueStyle);
        Assert.True(interview.EyeLine);
        Assert.Equal(120, interview.Coaching.MonologueLimit);
        Assert.Equal(new ClosedRange(0.4, 0.78), interview.Coaching.TalkShare);

        var academic = ScenePreset.For(MeetingMode.Academic);
        Assert.Equal(CueStyle.Script, academic.CueStyle);
        Assert.Equal(new ClosedRange(150, 290), academic.Coaching.PaceBounds);

        // A lecture is a listening scene: no nudges at all.
        var lesson = ScenePreset.For(MeetingMode.Lesson);
        Assert.Null(lesson.Coaching.MonologueLimit);
        Assert.False(lesson.Coaching.NudgesPace);
    }

    [Fact]
    public void KeyPointsLinesPullHeadsAndBullets()
    {
        var answer = """
            ## 缓存怎么写

            先写数据库，再删缓存，这是最常用的做法。

            - **Cache Aside**：写库成功后删除缓存
            1. 先更新数据库
            ```csharp
            cache.Remove(key);
            ```
            """;
        var lines = CueScript.Lines(answer, CueStyle.KeyPoints);
        Assert.Contains(lines, line => line.Type == CueLine.Kind.Point && line.Head == "缓存怎么写");
        Assert.Contains(lines, line => line.Head == "Cache Aside" && line.Text == "写库成功后删除缓存");
        Assert.Contains(lines, line => line.Text == "先更新数据库");
        Assert.DoesNotContain(lines, line => line.Text.Contains("cache.Remove"));
    }

    [Fact]
    public void ScriptStyleSplitsSentences()
    {
        // Pieces shorter than eight characters merge with the next one, as in Swift.
        var lines = CueScript.Lines("先给结论。然后解释原因！最后补充一个例子？", CueStyle.Script);
        Assert.Equal(2, lines.Count);
        Assert.Equal(CueLine.Kind.Lead, lines[0].Type);
        Assert.Equal("先给结论。然后解释原因！", lines[0].Text);
        Assert.Equal(CueLine.Kind.Detail, lines[1].Type);
    }

    [Fact]
    public void BilingualAnswersSplitGlossesFromWhatIsSpoken()
    {
        var answer = """
            > 问：你们怎么处理缓存一致性？
            先写数据库，再删缓存。
            > 译：Write the database, then drop the cache.
            这样读请求永远能命中旧数据。
            > 译：Reads may serve stale data briefly.
            """;
        Assert.True(BilingualAnswer.IsBilingual(answer));
        Assert.Equal("你们怎么处理缓存一致性？", BilingualAnswer.Question(answer));
        var spoken = BilingualAnswer.Spoken(answer);
        Assert.DoesNotContain("译：", spoken);
        Assert.Contains("先写数据库", spoken);

        var lines = CueScript.Lines(answer, CueStyle.KeyPoints);
        Assert.Contains(lines, line => line.Text.Contains("先写数据库") && line.Gloss.Contains("Write the database"));
    }

    [Fact]
    public void SourceNotesAreNotSpoken()
    {
        Assert.Equal("残差约为百分之十二", BilingualAnswer.WithoutSource("残差约为百分之十二（报告第 5 页）"));
        Assert.Equal("the residual is twelve percent", BilingualAnswer.WithoutSource("the residual is twelve percent (report, p. 5)"));
        Assert.Equal("这是一个例子", BilingualAnswer.WithoutSource("这是一个例子"));
    }

    [Fact]
    public void SpeechUnitsCountCjkOnceAndLatinWordsOneAndAHalf()
    {
        Assert.Equal(4, SpeechUnits.Count("今天讨论"));
        Assert.Equal(3.0, SpeechUnits.Count("hello world"));
        Assert.Equal(3.5, SpeechUnits.Count("今天 hello"));
    }

    [Fact]
    public void CoachWithoutNudgesYetWhenThereIsNoSpeech()
    {
        var coach = new SpeakingCoach(ScenePreset.For(MeetingMode.Interview).Coaching);
        var snapshot = coach.Snapshot(30);
        Assert.Null(snapshot.Pace);
        Assert.Null(snapshot.TalkShare);
        Assert.Equal(0, snapshot.Monologue);
        Assert.Null(snapshot.Nudge);
    }

    [Fact]
    public void PaceAndShareComeFromRecentSegments()
    {
        var coach = new SpeakingCoach(ScenePreset.For(MeetingMode.Interview).Coaching);
        // 10 s of my speech, 10 CJK units -> 60 units/min; 40 s of theirs (share needs 45 s total).
        coach.Ingest(new SpokenSegment(true, 0, 10, "一二三四五六七八九十"));
        coach.Ingest(new SpokenSegment(false, 10, 50, "对方的发言内容在这里"));
        var snapshot = coach.Snapshot(50);
        Assert.Equal(60, snapshot.Pace!.Value, 3);
        Assert.Equal(0.2, snapshot.TalkShare!.Value, 3);
    }

    [Fact]
    public void MonologueTriggersWrapUp()
    {
        var coach = new SpeakingCoach(ScenePreset.For(MeetingMode.Interview).Coaching);
        // 130 s of uninterrupted speech is past the 120 s interview limit.
        coach.Ingest(new SpokenSegment(true, 0, 130, "这是我的回答，讲了很多内容但没有停下来"));
        var snapshot = coach.Snapshot(131);
        Assert.Equal(CoachNudge.WrapUp, snapshot.Nudge);
        Assert.Equal(130, snapshot.Monologue, 3);
    }

    [Fact]
    public void CoachSummaryFoldsSegmentsAndNudges()
    {
        var accumulator = new CoachSummaryAccumulator();
        accumulator.Ingest(new SpokenSegment(true, 0, 30, "一二三四五六七八九十一二三四五六七八九十"));
        accumulator.Ingest(new SpokenSegment(false, 30, 50, "对方说了二十秒"));
        accumulator.Observe(new CoachSnapshot(Monologue: 45));
        accumulator.Record(CoachNudge.WrapUp);
        accumulator.Record(CoachNudge.WrapUp);
        accumulator.ObserveEyeContact(0.6);
        var summary = accumulator.Summary();
        Assert.Equal(40, summary.Pace!.Value, 3);
        Assert.Equal(0.6, summary.TalkShare!.Value, 3);
        Assert.Equal(45, summary.LongestMonologue);
        Assert.Equal(2, summary.Nudges!["wrapup"]);
        Assert.Equal(0.6, summary.EyeContact);
        Assert.False(summary.IsEmpty);
        Assert.True(new CoachSummary().IsEmpty);
    }
}
