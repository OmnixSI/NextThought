using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from Tests/NextThoughtCoreTests/SceneAndGazeTests.swift: the scene presets and their
/// cue script, the speaking coach, and the gaze estimator. Same inputs and expected values as the
/// Swift source; the suits there are plain test methods here.
/// </summary>
public sealed class SceneAndGazeParityTests
{
    // MARK: ScenePresetTests

    [Fact]
    public void EverySceneOfferedHasDefaultsAndAProfile()
    {
        Assert.Contains(SessionMode.Interview, SessionModes.BuiltIn);
        Assert.Contains(SessionMode.GroupProject, SessionModes.BuiltIn);
        Assert.DoesNotContain(SessionMode.Custom, SessionModes.BuiltIn);
        Assert.Equal(SessionModes.BuiltIn.Count, SessionModes.BuiltIn.Distinct().Count());
        foreach (var mode in SessionModes.BuiltIn)
        {
            var meetingMode = mode.ToMeetingMode();
            Assert.NotEmpty(ModeProfile.For(meetingMode).TemplatePreferences);
            Assert.Equal(meetingMode, ScenePreset.For(meetingMode).Mode);
            var template = MeetingTypeDefaults.For(meetingMode).Template;
            Assert.NotEmpty(NotesTemplate.BuiltIn(template).Sections);
            Assert.NotEmpty(NotesTemplate.BuiltIn(template, english: true).Sections);
        }
    }

    [Fact]
    public void ScenesWhereTheUserIsQuestionedPromptBesideTheCamera()
    {
        Assert.True(ScenePreset.For(MeetingMode.Interview).EyeLine);
        Assert.True(ScenePreset.For(MeetingMode.Client).EyeLine);
        Assert.False(ScenePreset.For(MeetingMode.Lesson).EyeLine);
        Assert.Equal(CoachingProfile.Listening, ScenePreset.For(MeetingMode.Lesson).Coaching);
        Assert.Equal(LiveMode.Prompting, MeetingTypeDefaults.For(MeetingMode.Interview).LiveMode);
        Assert.Equal(NotesTemplateKind.GroupProject, MeetingTypeDefaults.For(MeetingMode.GroupProject).Template);
    }

    [Fact]
    public void TitlesPickTheScene()
    {
        Assert.Equal(SessionMode.Interview, MeetingTypeGuesser.Guess("字节跳动 · 后端二面"));
        Assert.Equal(SessionMode.GroupProject, MeetingTypeGuesser.Guess("数据库大作业 第二次碰头"));
        Assert.Equal(SessionMode.GroupProject, MeetingTypeGuesser.Guess("COMP5318 group project sync"));
        Assert.Equal(SessionMode.LabMeeting, MeetingTypeGuesser.Guess("周三组会"));
        Assert.Equal(SessionMode.Client, MeetingTypeGuesser.Guess("华东区客户沟通"));
    }

    [Fact]
    public void GroupProjectUsesLabMeetingQuestionThresholds()
    {
        Assert.Equal(QuestionIntentClassifier.Thresholds(MeetingMode.LabMeeting),
            QuestionIntentClassifier.Thresholds(MeetingMode.GroupProject));
    }

    // MARK: CueScriptTests

    private const string Answer = """
        我们采用**先更新数据库、再删除缓存**，并用 binlog 订阅兜底。
        - **Cache Aside**：写库成功后删除缓存，读请求未命中时回源并回填。
        - 删除失败兜底：失败的 key 进入重试队列。
        - 没有小标题的一条。
        ```
        DEL order:42
        ```
        2. **取舍：**这是最终一致方案。
        """;

    [Fact]
    public void KeyPointsKeepTheLeadAndTheHeadOfEachPoint()
    {
        var lines = CueScript.Lines(Answer, CueStyle.KeyPoints);
        Assert.Equal(new[] { CueLine.Kind.Lead, CueLine.Kind.Point, CueLine.Kind.Point, CueLine.Kind.Point, CueLine.Kind.Point },
            lines.Select(line => line.Type));
        Assert.Equal("我们采用先更新数据库、再删除缓存，并用 binlog 订阅兜底。", lines[0].Text);
        Assert.Equal("Cache Aside", lines[1].Head);
        Assert.Equal("写库成功后删除缓存，读请求未命中时回源并回填。", lines[1].Text);
        Assert.Equal("删除失败兜底", lines[2].Head);
        Assert.Equal("", lines[3].Head);
        Assert.Equal("取舍", lines[4].Head);
        Assert.Equal("这是最终一致方案。", lines[4].Text);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, lines.Select(line => line.Id));
        Assert.DoesNotContain(lines, line => line.Text.Contains("DEL"));
    }

    [Fact]
    public void ColonsInsideValuesAreNotLeadIns()
    {
        Assert.Equal("", CueScript.SplitHead("会议 10:30 开始").Head);
        Assert.Equal("", CueScript.SplitHead("见 https://example.com").Head);
        Assert.Equal("", CueScript.SplitHead("这是一个超过十四个字的很长很长的前半句：后半句").Head);
    }

    [Fact]
    public void ScriptStyleIsSentenceBySentence()
    {
        var lines = CueScript.Lines("先说结论：这个方案可行。再说第一个理由，展开一点。最后收尾，把话交回去！", CueStyle.Script);
        Assert.Equal(3, lines.Count);
        Assert.Equal(CueLine.Kind.Lead, lines[0].Type);
        Assert.Equal("最后收尾，把话交回去！", lines[^1].Text);
    }

    [Fact]
    public void EmptyAnswersGiveNoLines()
    {
        Assert.Empty(CueScript.Lines("", CueStyle.KeyPoints));
        Assert.Empty(CueScript.Lines("\n\n", CueStyle.Script));
    }

    // MARK: SpeakingCoachTests

    [Fact]
    public void UnitsCompareChineseAndEnglish()
    {
        Assert.Equal(8.0, SpeechUnits.Count("我们先更新数据库"));
        Assert.Equal(6.0, SpeechUnits.Count("hello brave new world"));
        Assert.Equal(5.5, SpeechUnits.Count("用 Redis 做缓存"));
        Assert.Equal(0.0, SpeechUnits.Count("，。！"));
    }

    [Fact]
    public void PaceNeedsEnoughSpeechAndFlagsRushing()
    {
        var coach = new SpeakingCoach(new CoachingProfile(120));
        coach.Ingest(new SpokenSegment(isMe: true, start: 0, end: 3, text: new string('字', 12)));
        Assert.Null(coach.Snapshot(3).Pace);
        // 60 characters in 10 seconds = 360 per minute: too fast.
        coach.Ingest(new SpokenSegment(isMe: true, start: 4, end: 14, text: new string('字', 60)));
        var snapshot = coach.Snapshot(14);
        Assert.True((snapshot.Pace ?? 0) > 310);
        Assert.Equal(CoachNudge.SlowDown, snapshot.Nudge);
    }

    [Fact]
    public void MonologueRunsUntilTheOtherSideSpeaksOrAPause()
    {
        var coach = new SpeakingCoach(new CoachingProfile(60));
        coach.Ingest(new SpokenSegment(isMe: false, start: 0, end: 5, text: "你介绍一下项目"));
        for (var index = 0; index < 8; index++)
        {
            var start = 6 + index * 10.0;
            coach.Ingest(new SpokenSegment(isMe: true, start: start, end: start + 9, text: new string('字', 36)));
        }
        var talking = coach.Snapshot(85);
        Assert.Equal(79, talking.Monologue, 6);
        Assert.Equal(CoachNudge.WrapUp, talking.Nudge);
        // Silence ends it.
        Assert.Equal(0, coach.Snapshot(95).Monologue, 6);
        coach.Ingest(new SpokenSegment(isMe: false, start: 96, end: 99, text: "好的明白"));
        Assert.Equal(0, coach.Snapshot(99).Monologue, 6);
    }

    [Fact]
    public void TalkShareNudgesBothWays()
    {
        var listener = new SpeakingCoach(new CoachingProfile(null, new ClosedRange(0.25, 0.6)));
        listener.Ingest(new SpokenSegment(isMe: true, start: 0, end: 50, text: new string('字', 190)));
        listener.Ingest(new SpokenSegment(isMe: false, start: 51, end: 61, text: new string('字', 40)));
        var dominating = listener.Snapshot(70);
        Assert.True((dominating.TalkShare ?? 0) > 0.8);
        Assert.Equal(CoachNudge.LetThemTalk, dominating.Nudge);

        var quiet = new SpeakingCoach(new CoachingProfile(null, new ClosedRange(0.25, 0.6)));
        quiet.Ingest(new SpokenSegment(isMe: false, start: 0, end: 60, text: new string('字', 200)));
        quiet.Ingest(new SpokenSegment(isMe: true, start: 61, end: 73, text: new string('字', 44)));
        // Not in the first three minutes.
        Assert.NotNull(quiet.Snapshot(80).TalkShare);
        Assert.Null(quiet.Snapshot(80).Nudge);
        Assert.Equal(CoachNudge.SpeakUp, quiet.Snapshot(200).Nudge);
    }

    [Fact]
    public void NoShareUntilBothSidesSpokeAndTheUserSaidEnough()
    {
        var profile = new CoachingProfile(null, new ClosedRange(0.25, 0.6));
        // An interviewer still on the first question: the user has not spoken at all.
        var onlyThem = new SpeakingCoach(profile);
        onlyThem.Ingest(new SpokenSegment(isMe: false, start: 0, end: 60, text: new string('字', 200)));
        Assert.Null(onlyThem.Snapshot(200).TalkShare);
        Assert.Null(onlyThem.Snapshot(200).Nudge);
        // Only the user so far.
        var onlyMe = new SpeakingCoach(profile);
        onlyMe.Ingest(new SpokenSegment(isMe: true, start: 0, end: 60, text: new string('字', 200)));
        Assert.Null(onlyMe.Snapshot(200).TalkShare);
        // A few seconds from the user is not enough to judge.
        var brief = new SpeakingCoach(profile);
        brief.Ingest(new SpokenSegment(isMe: false, start: 0, end: 60, text: new string('字', 200)));
        brief.Ingest(new SpokenSegment(isMe: true, start: 61, end: 65, text: new string('字', 14)));
        Assert.Null(brief.Snapshot(200).TalkShare);
    }

    [Fact]
    public void AListeningSceneNeverNudges()
    {
        var coach = new SpeakingCoach(CoachingProfile.Listening);
        coach.Ingest(new SpokenSegment(isMe: false, start: 0, end: 5, text: "同学来回答一下"));
        coach.Ingest(new SpokenSegment(isMe: true, start: 6, end: 16, text: new string('字', 80)));
        var snapshot = coach.Snapshot(16);
        Assert.True((snapshot.Pace ?? 0) > 310);
        Assert.Null(snapshot.Nudge);
        coach.Ingest(new SpokenSegment(isMe: true, start: 17, end: 400, text: new string('字', 900)));
        Assert.Null(coach.Snapshot(400).Nudge);
    }

    // MARK: GazeEstimatorTests

    /// <summary>An almond-shaped eye 30 wide and 12 tall at <paramref name="center"/>, rotated by <paramref name="roll"/>.</summary>
    private static EyeGeometry Eye(GazePoint center, GazePoint pupil, double openness = 0.4, double roll = 0)
    {
        const double half = 15.0;
        var height = 30.0 * openness / 2;
        GazePoint[] raw =
        [
            new GazePoint(-half, 0), new GazePoint(-half / 2, height), new GazePoint(0, height * 1.1),
            new GazePoint(half / 2, height), new GazePoint(half, 0), new GazePoint(half / 2, -height),
            new GazePoint(0, -height * 1.1), new GazePoint(-half / 2, -height)
        ];
        GazePoint Place(GazePoint point) => new(
            center.X + point.X * Math.Cos(roll) - point.Y * Math.Sin(roll),
            center.Y + point.X * Math.Sin(roll) + point.Y * Math.Cos(roll));
        return new EyeGeometry(raw.Select(Place).ToList(), Place(pupil));
    }

    private static GazeFrame Frame(double time, GazePoint? pupil = null, double yaw = 0, double pitch = 0,
        double openness = 0.4, double roll = 0)
    {
        var relative = pupil ?? new GazePoint(0, 0);
        return new GazeFrame(
            Eye(new GazePoint(100, 100), relative, openness, roll),
            Eye(new GazePoint(160, 100), relative, openness, roll),
            yaw, pitch, time);
    }

    [Fact]
    public void CentredPupilsReadAsNeutral()
    {
        var signature = GazeEstimator.Signature(Frame(time: 0));
        Assert.NotNull(signature);
        Assert.True(Math.Abs(signature!.X) < 1e-9);
        Assert.True(Math.Abs(signature.Y) < 1e-9);
    }

    [Fact]
    public void ATiltedHeadDoesNotLookLikeAGlance()
    {
        var signature = GazeEstimator.Signature(Frame(time: 0, roll: 0.3));
        Assert.NotNull(signature);
        Assert.True(Math.Abs(signature!.X) < 1e-9);
        Assert.True(Math.Abs(signature.Y) < 1e-9);
    }

    [Fact]
    public void ClosedEyesGiveNoSignature()
    {
        Assert.Null(GazeEstimator.Signature(Frame(time: 0, openness: 0.05)));
    }

    [Fact]
    public void LookingDownAtTheScreenBreaksContactAndComingBackRestoresIt()
    {
        var estimator = new GazeEstimator();
        for (var index = 0; index < 10; index++)
            estimator.AddCalibration(Frame(time: index * 0.1, pupil: new GazePoint(0.4, -0.3)));
        Assert.True(estimator.FinishCalibration());
        var time = 1.0;
        GazeReading Feed(GazePoint pupil, int frames, double yaw = 0)
        {
            var reading = new GazeReading(GazeReading.StateValue.NoFace);
            for (var index = 0; index < frames; index++)
            {
                time += 0.1;
                reading = estimator.Ingest(Frame(time: time, pupil: pupil, yaw: yaw));
            }
            return reading;
        }
        Assert.Equal(GazeReading.StateValue.OnCamera, Feed(new GazePoint(0.4, -0.3), 40).State);
        // Pupils drop a fifth of the eye width: reading something lower on the screen.
        var reading = Feed(new GazePoint(0.4, -6), 40);
        Assert.Equal(GazeReading.StateValue.Away, reading.State);
        Assert.True(reading.OffsetValue.Y < 0);
        Assert.Equal(GazeReading.StateValue.OnCamera, Feed(new GazePoint(0.4, -0.3), 40).State);
        // Turning the head to a second display, eyes still centred in the head.
        Assert.Equal(GazeReading.StateValue.Away, Feed(new GazePoint(0.4, -0.3), 40, yaw: 0.45).State);
        var ratio = estimator.ContactRatio ?? 0;
        Assert.True(ratio > 0.35 && ratio < 0.75);
    }

    [Fact]
    public void ABlinkKeepsTheVerdict()
    {
        var estimator = new GazeEstimator();
        for (var index = 0; index < 10; index++) _ = estimator.Ingest(Frame(time: index * 0.1));
        var blink = estimator.Ingest(Frame(time: 1.1, openness: 0.04));
        Assert.Equal(GazeReading.StateValue.OnCamera, blink.State);
        var missing = estimator.Ingest(null);
        Assert.Equal(GazeReading.StateValue.NoFace, missing.State);
    }

    [Fact]
    public void CalibrationNeedsEnoughOpenEyedFrames()
    {
        var estimator = new GazeEstimator();
        for (var index = 0; index < 3; index++) estimator.AddCalibration(Frame(time: index));
        Assert.False(estimator.FinishCalibration());
        Assert.False(estimator.IsCalibrated);
    }
}
