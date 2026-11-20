using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Translated from Tests/NextThoughtCoreTests/MeetingTypeAndNotesTests.swift.</summary>
public sealed class MeetingTypeAndNotesTestsParityTests
{
    /// <summary>Monday 2026-10-26 10:00 UTC.</summary>
    private static readonly DateTimeOffset Monday = new(2026, 10, 26, 10, 0, 0, TimeSpan.Zero);

    private static int[] Ymd(DateTime? date) => date is { } value ? [value.Year, value.Month, value.Day] : [];

    // MARK: Meeting types

    [Fact(Skip = "Missing C# APIs: tolerant SessionMode decoding of unknown raw values ('fromTheFuture' → General), " +
                 "SessionMode.rawValue and SessionMode.builtIn.")]
    public void NewTypesRoundTripAndOldValuesStillDecode()
    {
    }

    [Fact]
    public void CustomTypeUsesItsBaseAndOldWorkspacesDecode()
    {
        var id = Guid.NewGuid().ToString("D");
        var old = JsonSerializer.Deserialize<WorkspaceSeries>(
            $$"""{"id":"{{id}}","name":"组会","mode":"labMeeting"}""", WorkspaceStore.Options)!;
        Assert.Null(old.CustomTypeName);
        Assert.Null(old.CustomBaseMode);
        Assert.Equal(MeetingMode.LabMeeting, old.EffectiveMode);

        var custom = new WorkspaceSeries(Guid.NewGuid().ToString("D"), "导师一对一", DateTimeOffset.UnixEpoch, [], [])
        {
            Mode = "custom"
        };
        Assert.Equal(MeetingMode.General, custom.EffectiveMode);
        custom = custom with { CustomTypeName = "导师一对一", CustomBaseMode = "interview" };
        Assert.Equal(MeetingMode.Interview, custom.EffectiveMode);
        Assert.Contains(custom.Profile.Fields, field => field.Id == "company");
        var again = JsonSerializer.Deserialize<WorkspaceSeries>(
            JsonSerializer.Serialize(custom, WorkspaceStore.Options), WorkspaceStore.Options)!;
        Assert.Equal("导师一对一", again.CustomTypeName);
        Assert.Equal("interview", again.CustomBaseMode);
        Assert.Equal("custom", again.Mode);
    }

    [Fact(Skip = "Missing C# APIs: MeetingTypeDefaults.For(mode) with LiveMode/Style/Template (C# only has TemplateFor), " +
                 "MeetingTypeDefaults.followsSlides and SessionMode.builtIn.")]
    public void TypeDefaultsFollowTheTable()
    {
    }

    [Fact]
    public void TypeIsGuessedFromTheTitle()
    {
        Assert.Equal(SessionMode.Interview, MeetingTypeGuesser.Guess("字节跳动 · 后端二面"));
        Assert.Equal(SessionMode.LabMeeting, MeetingTypeGuesser.Guess("周三组会"));
        Assert.Equal(SessionMode.Academic, MeetingTypeGuesser.Guess("毕业论文答辩"));
        Assert.Equal(SessionMode.Meeting, MeetingTypeGuesser.Guess("Weekly sync"));
        Assert.Equal(SessionMode.Lesson, MeetingTypeGuesser.Guess("机器学习讲座"));
        Assert.Equal(SessionMode.Client, MeetingTypeGuesser.Guess("Client kickoff call"));
        Assert.Equal(SessionMode.General, MeetingTypeGuesser.Guess("随便聊聊"));
        Assert.Equal(SessionMode.General, MeetingTypeGuesser.Guess(""));
    }

    [Theory]
    [InlineData("字节跳动 · 后端二面", SessionMode.Interview)]
    [InlineData("Google phone screen", SessionMode.Interview)]
    [InlineData("Final interview round", SessionMode.Interview)]
    [InlineData("周三组会", SessionMode.LabMeeting)]
    [InlineData("Lab meeting", SessionMode.LabMeeting)]
    [InlineData("课题组会 第 3 次", SessionMode.LabMeeting)]
    [InlineData("小组会议", SessionMode.Meeting)]
    [InlineData("项目组会议", SessionMode.Meeting)]
    [InlineData("Weekly sync", SessionMode.Meeting)]
    [InlineData("季度 review", SessionMode.Meeting)]
    [InlineData("周会", SessionMode.Meeting)]
    [InlineData("数据库课程设计", SessionMode.GroupProject)]
    [InlineData("COMP5318 group project sync", SessionMode.GroupProject)]
    [InlineData("大作业讨论", SessionMode.GroupProject)]
    [InlineData("Capstone check-in", SessionMode.GroupProject)]
    [InlineData("客户 talk", SessionMode.Client)]
    [InlineData("Client talk", SessionMode.Client)]
    [InlineData("华东区客户沟通", SessionMode.Client)]
    [InlineData("Customer onboarding call", SessionMode.Client)]
    [InlineData("毕业论文答辩", SessionMode.Academic)]
    [InlineData("ACL 2026 oral presentation", SessionMode.Academic)]
    [InlineData("Thesis defense", SessionMode.Academic)]
    [InlineData("Tech talk", SessionMode.Academic)]
    [InlineData("项目汇报", SessionMode.Academic)]
    [InlineData("机器学习讲座", SessionMode.Lesson)]
    [InlineData("Lecture 4: Transformers", SessionMode.Lesson)]
    [InlineData("线性代数课程", SessionMode.Lesson)]
    [InlineData("CS229 class", SessionMode.Lesson)]
    [InlineData("小组讨论", SessionMode.Discussion)]
    [InlineData("Brainstorm", SessionMode.Discussion)]
    [InlineData("随便聊聊", SessionMode.General)]
    [InlineData("Coffee with Sam", SessionMode.General)]
    [InlineData("Classic rock night", SessionMode.General)]
    [InlineData("Subclass design notes", SessionMode.General)]
    [InlineData("", SessionMode.General)]
    public void TitlesPickTheirScene(string title, SessionMode mode)
    {
        Assert.Equal(mode, MeetingTypeGuesser.Guess(title));
    }

    [Fact]
    public void PreparationPromptFollowsTheScene()
    {
        foreach (var mode in Enum.GetValues<SessionMode>())
        {
            var meetingMode = mode.ToMeetingMode();
            var system = PreparationPrompt.System(meetingMode);
            var output = PreparationPrompt.Output(meetingMode);
            Assert.True((!system.Contains("导师意见") && !system.Contains("学术讨论"))
                        || mode == SessionMode.LabMeeting, mode.ToString());
            Assert.True(!output.Contains("研究问题") && !output.Contains("讨论重点"), mode.ToString());
            Assert.Contains(ModeProfile.For(meetingMode).Role, system);
        }

        Assert.Contains("## 资料里的亮点", PreparationPrompt.Output(MeetingMode.Interview));
        Assert.Contains("## 客户关心什么", PreparationPrompt.Output(MeetingMode.Client));
        Assert.Contains("## 我可能不懂的地方", PreparationPrompt.Output(MeetingMode.Lesson));
        Assert.Contains("## 资料重点\n", PreparationPrompt.Output(MeetingMode.General));
        Assert.Equal(PreparationPrompt.Output(MeetingMode.General), PreparationPrompt.Output(MeetingMode.Custom));
    }

    [Fact]
    public void EverySceneHasPromptGuidance()
    {
        var instructions = AnswerPromptBuilder.Instructions(webSearch: false);
        foreach (var mode in Enum.GetValues<SessionMode>().Where(mode => mode != SessionMode.Custom))
        {
            var name = mode.ToString();
            var raw = char.ToLowerInvariant(name[0]) + name[1..];
            Assert.True(instructions.Contains("(" + raw + ")") || instructions.Contains("（" + raw + "）"),
                mode.ToString());
        }
    }

    // MARK: Series

    [Fact]
    public void NextRoundMatchesItsSeries()
    {
        Assert.Equal(SeriesMatcher.BaseTitle("字节跳动 · 后端"), SeriesMatcher.BaseTitle("字节跳动 · 后端二面"));
        Assert.True(SeriesMatcher.Similarity("字节跳动 · 后端二面", "字节跳动 · 后端") > 0.9);
        Assert.True(SeriesMatcher.Similarity("Google SWE round 2", "Google SWE") > 0.9);
        var bytedance = Guid.NewGuid();
        var tencent = Guid.NewGuid();
        var weekly = Guid.NewGuid();
        var candidates = new List<SeriesMatcher.Candidate>
        {
            new(tencent, "腾讯 · 前端", SessionMode.Interview, Monday),
            new(bytedance, "字节跳动 · 后端", SessionMode.Interview, Monday.AddDays(-1)),
            new(weekly, "周组会", SessionMode.LabMeeting, Monday)
        };
        Assert.Equal(bytedance,
            SeriesMatcher.BestMatch("字节跳动 · 后端三面", SessionMode.Interview, candidates));
        Assert.Equal(weekly, SeriesMatcher.BestMatch("周组会 第 3 周", null, candidates));
        Assert.Null(SeriesMatcher.BestMatch("阿里 · 算法一面", null, candidates));
        Assert.Null(SeriesMatcher.BestMatch("", null, candidates));
    }

    [Fact]
    public void RecurringSeriesOutsideInterviewsMatch()
    {
        foreach (var (title, baseTitle) in new[]
                 {
                     ("机器学习 第 3 讲", "机器学习"), ("机器学习 第三讲", "机器学习"),
                     ("CS229 Lecture 4", "CS229 Lecture"), ("ML reading group Week 5", "ML reading group"),
                     ("第 2 次组会", "组会"), ("季度复盘 Q3", "季度复盘"), ("季度复盘 第 3 季度", "季度复盘")
                 })
        {
            Assert.Equal(SeriesMatcher.BaseTitle(baseTitle), SeriesMatcher.BaseTitle(title));
        }

        var lecture = Guid.NewGuid();
        var standup = Guid.NewGuid();
        var review = Guid.NewGuid();
        var lab = Guid.NewGuid();
        var candidates = new List<SeriesMatcher.Candidate>
        {
            new(lecture, "机器学习", SessionMode.Lesson, Monday),
            new(standup, "数据库大作业 碰头", SessionMode.GroupProject, Monday),
            new(review, "季度复盘", SessionMode.Meeting, Monday),
            new(lab, "组会", SessionMode.LabMeeting, Monday)
        };
        Assert.Equal(lecture, SeriesMatcher.BestMatch("机器学习 第 4 讲", null, candidates));
        Assert.Equal(standup, SeriesMatcher.BestMatch("数据库大作业 第二次碰头", null, candidates));
        Assert.Equal(review, SeriesMatcher.BestMatch("季度复盘 Q3", null, candidates));
        Assert.Equal(lab, SeriesMatcher.BestMatch("第 2 次组会", null, candidates));
        Assert.Null(SeriesMatcher.BestMatch("操作系统 第 4 讲", null, candidates));
        Assert.Null(SeriesMatcher.BestMatch("Lecture 4", null, candidates));
        Assert.Null(SeriesMatcher.BestMatch("Q3 offsite planning", null, candidates));
    }

    // MARK: Dates

    [Fact]
    public void RelativeDatesBecomeDates()
    {
        DateTime? Resolve(string phrase) => NotesDates.Resolve(phrase, Monday, TimeZoneInfo.Utc);

        Assert.Equal([2026, 10, 26], Ymd(Resolve("今天")));
        Assert.Equal([2026, 10, 27], Ymd(Resolve("明天上午")));
        Assert.Equal([2026, 10, 28], Ymd(Resolve("后天")));
        Assert.Equal([2026, 10, 29], Ymd(Resolve("周四交付")));
        Assert.Equal([2026, 10, 27], Ymd(Resolve("周二下午")));
        Assert.Equal([2026, 10, 30], Ymd(Resolve("本周五")));
        Assert.Equal([2026, 11, 2], Ymd(Resolve("下周一前")));
        Assert.Equal([2026, 11, 1], Ymd(Resolve("星期天")));
        Assert.Equal([2026, 10, 30], Ymd(Resolve("10月30日")));
        Assert.Equal([2026, 10, 30], Ymd(Resolve("10 月 30 号")));
        Assert.Equal([2026, 10, 31], Ymd(Resolve("月底")));
        Assert.Equal([2026, 11, 2], Ymd(Resolve("next Monday")));
        Assert.Equal([2026, 10, 30], Ymd(Resolve("by Friday")));
        Assert.Equal([2026, 10, 27], Ymd(Resolve("tomorrow")));
        Assert.Empty(Ymd(Resolve("尽快")));
        Assert.Empty(Ymd(Resolve("下周")));
        var friday = new DateTime(2026, 10, 30);
        Assert.Equal("10 月 30 日（周五）", NotesDates.Format(friday, english: false));
        Assert.Equal("Fri, Oct 30", NotesDates.Format(friday, english: true));
    }

    [Fact]
    public void TimesAreRead()
    {
        Assert.Equal(761, NotesTime.Seconds("12:41"));
        Assert.Equal(761, NotesTime.Seconds("[12:41]"));
        Assert.Equal(761, NotesTime.Seconds("12：41"));
        Assert.Equal(3_725, NotesTime.Seconds("1:02:05"));
        Assert.Null(NotesTime.Seconds("12:75"));
        Assert.Null(NotesTime.Seconds("1268"));
        Assert.Null(NotesTime.Seconds(null));
        Assert.Equal("12:41", NotesTime.Text(761));
        Assert.Equal("1:02:05", NotesTime.Text(3_725));
    }

    // MARK: Templates and notes

    [Fact(Skip = "Missing C# API: NotesTemplate.base (the custom copy's source template). " +
                 "C# NotesTemplate only keeps Id/Name/Sections and IsBuiltIn.")]
    public void BuiltInTemplatesHaveAGapSection()
    {
    }

    [Fact]
    public void TalkTemplateIsBuiltForThePresenter()
    {
        Assert.Equal(NotesTemplateKind.Talk, MeetingTypeDefaults.TemplateFor(MeetingMode.Academic));
        Assert.Equal(NotesTemplateKind.Academic, MeetingTypeDefaults.TemplateFor(MeetingMode.LabMeeting));
        Assert.Contains(NotesTemplateKind.Talk, NotesTemplateKinds.Selectable);
        Assert.Equal(NotesTemplateKind.Academic, NotesTemplateKinds.Parse("academic"));
        foreach (var english in new[] { false, true })
        {
            var talk = NotesTemplate.BuiltIn(NotesTemplateKind.Talk, english);
            var ids = talk.Sections.Select(section => section.Id).ToList();
            Assert.Contains("questions", ids);
            Assert.DoesNotContain("feedback", ids);
            Assert.DoesNotContain("experiments", ids);
            Assert.DoesNotContain("paper", ids);
            var questions = talk.Sections.First(section => section.Id == "questions");
            Assert.Equal(NotesSectionKind.Questions, questions.Kind);
            Assert.Equal(NotesCarry.Bank, questions.Carry);
            Assert.Equal(NotesSectionKind.OneLine, talk.Sections[0].Kind);
        }

        Assert.Equal("汇报 / 答辩", NotesTemplate.BuiltIn(NotesTemplateKind.Talk).Name);
        Assert.Equal("Talk / Defense", NotesTemplate.BuiltIn(NotesTemplateKind.Talk, english: true).Name);
        Assert.Equal("组会 / 研究讨论", NotesTemplate.BuiltIn(NotesTemplateKind.Academic).Name);
        Assert.Equal("academic", NotesTemplate.BuiltIn(NotesTemplateKind.Academic).Id);
    }

    [Fact(Skip = "Missing C# API: NotesVerifier.Verify(notes:lineOffsets:duration:meetingDate:calendar:) " +
                 "(C# only exposes NotesVerifier.IsSupported), so unsupported-time dropping and due-date rewriting cannot be tested.")]
    public void ModelReplyIsParsedAndChecked()
    {
    }

    [Fact]
    public void ReviewFeedsTheQuestionBank()
    {
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.Interview);
        const string reply = """
            {"sections": [{"id": "questions", "items": [
              {"text": "如果删除缓存失败了怎么办？", "time": "18:22", "answer": "重试队列 + binlog", "verdict": "good"},
              {"text": "热点 key 过期怎么办？", "time": "27:05", "answer": "加锁", "verdict": "improve", "better": "互斥锁重建 + 逻辑过期。"}
            ]}, {"id": "improve", "items": [{"text": "先说结论", "time": "27:05"}]}]}
            """;
        var notes = NotesJson.Parse(reply, template, "zh")!;
        Assert.Equal(2, notes.QuestionCount());
        Assert.Equal(1, notes.ImproveCount());
        Assert.Equal(new[] { new BankEntry { Question = "热点 key 过期怎么办？", Answer = "互斥锁重建 + 逻辑过期。" } },
            notes.BankEntries());
        Assert.Equal(new[] { "先说结论" }, notes.Lessons(template));
    }

    [Fact]
    public void CorrectionsNeverChangeTheTranscript()
    {
        var start = Monday;
        var lines = new List<TranscriptLine>
        {
            new()
            {
                Date = start, StartedAt = start, Speaker = "remote", Text = "缓存鸡穿怎么处理？"
            },
            new()
            {
                Date = start.AddSeconds(5), StartedAt = start.AddSeconds(5), Speaker = "me", Text = "用互斥锁。"
            }
        };
        const string reply = """
            {"corrections": [{"line": 7, "from": "缓存鸡穿", "to": "缓存击穿"}, {"line": 1, "from": "不存在", "to": "x"}, {"line": 0, "from": "一样", "to": "一样"}]}
            """;
        var corrections = NotesCorrections.Valid(NotesCorrectionsJson.Parse(reply), lines);
        // A misnumbered fix is moved to the line it belongs to; fixes that match nothing are dropped.
        var correction = Assert.Single(corrections);
        Assert.Equal(0, correction.Line);
        var fixedLines = NotesCorrections.Apply(corrections, lines);
        Assert.Equal("缓存击穿怎么处理？", fixedLines[0].Text);
        Assert.Equal("缓存鸡穿怎么处理？", lines[0].Text);
        var undone = new List<TranscriptCorrection> { correction with { Reverted = true } };
        Assert.Equal("缓存鸡穿怎么处理？", NotesCorrections.Apply(undone, lines)[0].Text);
        // Stored notes keep corrections across a save.
        var notes = new StructuredNotesDoc("general", "通用", "zh", "", [], corrections);
        var decoded = JsonSerializer.Deserialize<StructuredNotesDoc>(
            JsonSerializer.Serialize(notes, JsonDefaults.Options), JsonDefaults.Options)!;
        Assert.Equal(corrections.ToArray(), decoded.Corrections!.ToArray());
    }

    [Fact]
    public void RoomSpeakerIsLabelled()
    {
        Assert.Equal("现场", ProofreadPrompt.SpeakerLabel("room"));
        var line = new TranscriptLine
        {
            Date = Monday, StartedAt = Monday, Speaker = "room", Text = "小王你负责周四交付"
        };
        var decoded = JsonSerializer.Deserialize<TranscriptLine>(
            JsonSerializer.Serialize(line, JsonDefaults.Options), JsonDefaults.Options)!;
        Assert.Equal("room", decoded.Speaker);
        Assert.EndsWith("现场：小王你负责周四交付", ProofreadPrompt.Transcript([line], Monday));
    }
}
