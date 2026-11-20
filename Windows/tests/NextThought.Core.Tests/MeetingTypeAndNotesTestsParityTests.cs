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

    [Fact]
    public void NewTypesRoundTripAndOldValuesStillDecode()
    {
        var encoded = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(SessionModes.BuiltIn, JsonDefaults.Options);
        var decoded = System.Text.Json.JsonSerializer.Deserialize<List<SessionMode>>(encoded, JsonDefaults.Options)!;
        Assert.Equal(SessionModes.BuiltIn, decoded);
        Assert.Equal(SessionMode.Interview, System.Text.Json.JsonSerializer.Deserialize<SessionMode>("\"interview\"", JsonDefaults.Options));
        var tolerant = System.Text.Json.JsonSerializer.Deserialize<List<SessionMode>>("[\"labMeeting\",\"client\",\"discussion\",\"custom\",\"academic\",\"meeting\",\"lesson\",\"fromTheFuture\"]", JsonDefaults.Options)!;
        Assert.Equal([SessionMode.LabMeeting, SessionMode.Client, SessionMode.Discussion, SessionMode.Custom, SessionMode.Academic, SessionMode.Meeting, SessionMode.Lesson, SessionMode.General], tolerant);
        Assert.Equal("academic", SessionMode.Academic.Raw());
        Assert.Equal("\u6c47\u62a5 / \u7b54\u8fa9", SessionMode.Academic.Title());
        Assert.Equal("meeting", SessionMode.Meeting.Raw());
        Assert.Equal("\u5de5\u4f5c\u4f1a\u8bae", SessionMode.Meeting.Title());
        Assert.Equal("lesson", SessionMode.Lesson.Raw());
        Assert.Equal("\u8bb2\u5ea7 / \u8bfe\u7a0b", SessionMode.Lesson.Title());
        Assert.Equal(9, SessionModes.BuiltIn.Count);
        Assert.DoesNotContain(SessionMode.Custom, SessionModes.BuiltIn);
        Assert.Contains(SessionMode.Interview, SessionModes.BuiltIn);
        Assert.Contains(NotesTemplateKind.Interview, NotesTemplateKinds.Selectable);
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

    [Fact]
    public void TypeDefaultsFollowTheTable()
    {
        void Check(SessionMode mode, LiveMode live, AnswerStyle style, NotesTemplateKind template)
        {
            var defaults = MeetingTypeDefaults.For(mode.ToMeetingMode());
            Assert.Equal(live, defaults.LiveMode);
            Assert.Equal(style, defaults.Style);
            Assert.Equal(template, defaults.Template);
        }
        Check(SessionMode.General, LiveMode.NotesOnly, AnswerStyle.Concise, NotesTemplateKind.General);
        Check(SessionMode.Interview, LiveMode.Prompting, AnswerStyle.Concise, NotesTemplateKind.Interview);
        Check(SessionMode.Academic, LiveMode.Prompting, AnswerStyle.Concise, NotesTemplateKind.Talk);
        Check(SessionMode.LabMeeting, LiveMode.NotesOnly, AnswerStyle.Detailed, NotesTemplateKind.Academic);
        Check(SessionMode.Meeting, LiveMode.NotesOnly, AnswerStyle.Concise, NotesTemplateKind.Work);
        Check(SessionMode.Client, LiveMode.Prompting, AnswerStyle.Concise, NotesTemplateKind.Client);
        Check(SessionMode.Lesson, LiveMode.NotesOnly, AnswerStyle.Detailed, NotesTemplateKind.Lecture);
        Check(SessionMode.Discussion, LiveMode.NotesOnly, AnswerStyle.Concise, NotesTemplateKind.Discussion);
        Check(SessionMode.Custom, LiveMode.NotesOnly, AnswerStyle.Concise, NotesTemplateKind.General);
        Assert.True(MeetingTypeDefaults.For(MeetingMode.Academic).FollowsSlides);
        Assert.Equal(new HashSet<NotesTemplateKind>(NotesTemplateKinds.Selectable), SessionModes.BuiltIn.Select(mode => MeetingTypeDefaults.For(mode.ToMeetingMode()).Template).ToHashSet());
        foreach (var mode in Enum.GetValues<SessionMode>()) Assert.NotEmpty(ModeProfile.For(mode.ToMeetingMode()).Fields);
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

    [Fact]
    public void BuiltInTemplatesHaveAGapSection()
    {
        foreach (var kind in NotesTemplateKinds.All)
        {
            foreach (var english in new[] { false, true })
            {
                var template = NotesTemplate.BuiltIn(kind, english);
                Assert.Equal(kind.Raw(), template.Id);
                Assert.True(template.IsBuiltIn);
                Assert.Contains(template.Sections, section => section.IsGap);
                Assert.Equal(template.Sections.Select(section => section.Id).Distinct().Count(), template.Sections.Count);
            }
        }
        var copy = NotesTemplate.BuiltIn(NotesTemplateKind.Work).CustomCopy("\u5468\u4f1a");
        Assert.False(copy.IsBuiltIn);
        Assert.Equal(NotesTemplateKind.Work, copy.Base);
        Assert.Equal(NotesTemplate.BuiltIn(NotesTemplateKind.Work).Sections.Select(section => section.Id), copy.Sections.Select(section => section.Id));
        var encoded = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(copy, JsonDefaults.Options);
        var decoded = System.Text.Json.JsonSerializer.Deserialize<NotesTemplate>(encoded, JsonDefaults.Options)!;
        Assert.Equal(copy.Base, decoded.Base);
        Assert.Equal(copy.Sections.Select(section => section.Id), decoded.Sections.Select(section => section.Id));
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

    [Fact]
    public void ModelReplyIsParsedAndChecked()
    {
        const string reply = "\u597d\u7684\uff0c\u7eaa\u8981\u5982\u4e0b\uff1a\n```json\n"
            + "{\"title\": \"\u5468\u4e00\u4f8b\u4f1a\", \"sections\": ["
            + "{\"id\": \"summary\", \"items\": [{\"text\": \"\u4e0b\u5468\u4e09\u524d\u4e0a\u7ebf\u65b0\u7248\u672c\"}]}, "
            + "{\"id\": \"actions\", \"items\": ["
            + "{\"text\": \"\u56de\u5f52\u6d4b\u8bd5\u5e76\u7ed9\u51fa\u7ed3\u679c\", \"time\": \"05:40\", \"owner\": \"\u6211\", \"due\": \"\u5468\u4e8c\u4e0b\u5348\"}, "
            + "{\"text\": \"\u8bbe\u8ba1\u7a3f\u590d\u5ba1\", \"time\": \"12:30\", \"due\": \"\u5468\u56db\u6216\u8005\u5468\u4e94\", \"uncertain\": \"true\"}, "
            + "{\"text\": \"\u6ca1\u6709\u4f9d\u636e\u7684\u5f85\u529e\", \"time\": \"30:00\"}"
            + "]}, {\"title\": \"\u98ce\u9669\u548c\u672a\u51b3\u95ee\u9898\", \"items\": [{\"text\": \"\u5bfc\u51fa\u529f\u80fd\u6ca1\u4eba\u8ba4\u9886\", \"time\": 1268}]}"
            + "]}\n```";
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.Work);
        var notes = NotesJson.Parse(reply, template, "zh")!;
        Assert.Equal("\u5468\u4e00\u4f8b\u4f1a", notes.Title);
        Assert.Equal(template.Sections.Select(section => section.Id), notes.Sections.Select(section => section.Id));
        Assert.Empty(notes.Sections.First(section => section.Id == "decisions").Items);
        var actions = notes.Sections.First(section => section.Id == "actions");
        Assert.Equal(3, actions.Items.Count);
        Assert.Equal("\u6211", actions.Items[0].Owner);
        Assert.True(actions.Items[1].Uncertain);
        Assert.Equal("1268", notes.Sections.First(section => section.Id == "risks").Items.First().Time);
        var monday = new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.FromHours(8));
        notes = NotesVerifier.Verify(notes, [192, 340, 750, 1_268], 1_800, monday);
        var checkedActions = notes.Sections.First(section => section.Id == "actions");
        Assert.Equal(["\u56de\u5f52\u6d4b\u8bd5\u5e76\u7ed9\u51fa\u7ed3\u679c", "\u8bbe\u8ba1\u7a3f\u590d\u5ba1"], checkedActions.Items.Select(item => item.Text));
        Assert.Equal(2, notes.DroppedCount);
        Assert.Empty(notes.Sections.First(section => section.Id == "risks").Items);
        Assert.Equal("10 \u6708 27 \u65e5\uff08\u5468\u4e8c\uff09", checkedActions.Items[0].Due);
        Assert.Equal(new DateTime(2026, 10, 27), checkedActions.Items[0].DueDate);
        Assert.Single(notes.Sections.First(section => section.Id == "summary").Items);
        Assert.Equal(2, notes.ActionItemCount());
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
