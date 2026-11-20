using NextThought.Core;

namespace NextThought.Platform;

/// <summary>
/// D1: the Mac demonstration data (DemoMode.swift <c>AppState.populateDemo</c>,
/// <c>populateDemoDraft</c>, <c>demoReviewNotes</c>, <c>demoWeeklyMeeting</c> and the
/// scripted island states) in the Windows data model. Every string below is copied
/// verbatim from the Swift source; the desktop shell adapts these records into its own
/// view state (Desktop/DemoContent.cs + AppState.LoadDemo). Nothing here is written to
/// the user's data directory, and the data lives in the support library so it can be
/// asserted without an Avalonia render pass.
/// </summary>
public sealed record DemoSession(
    SessionMeta Meta,
    IReadOnlyList<TranscriptLine> Lines,
    IReadOnlyList<AnswerRecord> Answers,
    IReadOnlyList<SessionQuestionEvent> Questions,
    string SummaryMarkdown,
    StructuredNotesDoc Notes,
    SessionSummary Summary);

/// <summary>The running meeting the demo shows on the live page (populateDemo's live state).</summary>
public sealed record DemoLiveSession(
    DateTimeOffset StartedAt,
    IReadOnlyList<TranscriptLine> Lines,
    IReadOnlyList<string> QuestionTexts,
    string PartialTranscript,
    string Question,
    string Answer,
    IReadOnlyList<AnswerRecord> AnswerHistory,
    int EchoDroppedCount,
    string PlannedMeetingTitle,
    string StatusText,
    string TranscriptionStatus,
    double LastRtf,
    double LastRefineSeconds,
    double LastFirstTokenSeconds,
    double LastAnswerSeconds,
    string LastAnswerModel,
    string RetrievalNote);

/// <summary>The preparation page of the next interview (populateDemoDraft).</summary>
public sealed record DemoDraft(
    string Title,
    string Mode,
    string SeriesName,
    IReadOnlyList<string> BaselineDocumentNames);

/// <summary>One scripted line of the first-run guide's sample interview (OnboardingSampleMeeting.swift).</summary>
public sealed record DemoOnboardingLine(int Id, bool FromMe, string Text, double At);

/// <summary>One takeaway the sample interview writes while it plays.</summary>
public sealed record DemoOnboardingTakeaway(int Id, bool IsTodo, string Text, double At);

/// <summary>The question the sample interviewer asks and the reference answer.</summary>
public sealed record DemoOnboardingSuggestion(string Question, string Answer, double At);

/// <summary>The whole first-run sample interview, in the current interface language.</summary>
public sealed record DemoOnboardingSample(
    string Title,
    double Duration,
    IReadOnlyList<DemoOnboardingLine> Lines,
    IReadOnlyList<DemoOnboardingTakeaway> Takeaways,
    DemoOnboardingSuggestion Suggestion);

/// <summary>Everything the demo mode seeds, resolved against one wall-clock instant.</summary>
public sealed record DemoDataset(
    IReadOnlyList<WorkspaceSeries> Series,
    IReadOnlyList<Skill> Skills,
    IReadOnlyList<MaterialDocument> Materials,
    DemoSession Review,
    DemoSession Weekly,
    DemoLiveSession Live,
    DemoDraft Draft)
{
    public WorkspaceSeries InterviewSeries =>
        Series.First(series => series.Name == DemoScript.InterviewSeriesName);

    public WorkspaceSeries AcademicSeries =>
        Series.First(series => series.Name == DemoScript.AcademicSeriesName);

    public WorkspaceSeries BusinessSeries =>
        Series.First(series => series.Name == DemoScript.BusinessSeriesName);
}

/// <summary>The Mac demo script as data (DemoMode.swift / DemoFilm.swift).</summary>
public static class DemoScript
{
    public const string InterviewSeriesName = "字节后端二面";
    public const string AcademicSeriesName = "ACL 2026 口头报告";
    public const string BusinessSeriesName = "周一客户例会 · 华东区";

    public const string ReviewTitle = "后端二面：缓存一致性与项目经历";
    public const string WeeklyTitle = "周一产品例会";
    public const string PlannedMeetingTitle = "字节跳动 · 后端二面";

    // Deterministic ids keep the demo stable between captures (Mac uses fresh UUIDs; nothing here
    // is ever persisted).
    private static readonly Guid InterviewSeriesId = Guid.Parse("0f8c1a52-0000-4000-8000-000000000001");
    private static readonly Guid AcademicSeriesId = Guid.Parse("0f8c1a52-0000-4000-8000-000000000002");
    private static readonly Guid BusinessSeriesId = Guid.Parse("0f8c1a52-0000-4000-8000-000000000003");
    private static readonly Guid ReviewSessionId = Guid.Parse("0f8c1a52-0000-4000-8000-000000000010");
    private static readonly Guid WeeklySessionId = Guid.Parse("0f8c1a52-0000-4000-8000-000000000011");

    /// <summary>The demo meeting title (DemoMode.swift <c>question</c>).</summary>
    public const string LiveQuestion = "那数据库和缓存同时更新的时候，你们是怎么保证一致性的？";

    /// <summary>The half sentence the demo hears while the answer is being prepared.</summary>
    public const string LivePartialTranscript = "如果删除缓存失败了怎么办";

    /// <summary>The demo answer (DemoMode.swift <c>reply</c>), Markdown kept as written.</summary>
    public const string LiveAnswer = """
        我们采用**先更新数据库、再删除缓存**，并用 binlog 订阅兜底，保证最终一致。

        - **Cache Aside**：写库成功后删除缓存，读请求未命中时回源并回填，避免并发写导致脏数据。
        - **删除失败兜底**：删除失败的 key 进入重试队列；同时用 **Canal 订阅 binlog** 异步再删一次。
        - **并发读写窗口**：对热点 key 做**延迟双删**（约 500 ms），把旧值被回填的概率降到很低。
        - **取舍**：这是最终一致方案，对强一致场景（如余额）直接读库或加分布式锁。
        """;

    /// <summary>The review meeting's saved summary Markdown (DemoMode.swift summaryMarkdown).</summary>
    public const string ReviewSummaryMarkdown = """
        # 标题
        后端二面：缓存一致性与项目经历
        ## 摘要
        面试官围绕订单服务缓存层展开，重点考察缓存与数据库的一致性、失败兜底和高并发下的取舍。整体回答完整，延迟双删与 binlog 兜底讲得清楚。
        ## 问题清单
        1. 简单介绍一下你负责的部分 —— 我的回答要点：订单服务缓存层，从数据库迁到 Redis
        2. 数据库和缓存同时更新时怎么保证一致性？ —— 我的回答要点：先更新数据库再删缓存，binlog 兜底
        3. 如果删除缓存失败了怎么办？ —— 我的回答要点：重试队列 + Canal 异步再删
        4. 热点 key 并发读写怎么处理？ —— 我的回答要点：延迟双删
        5. 什么场景下不能用最终一致？ —— 我的回答要点：余额等强一致场景直接读库
        6. 你还有什么问题想问我们？ —— 我的回答要点：问了团队的技术栈与值班制度
        ## 关键信息与结论
        - 团队使用 Redis Cluster + Canal
        - 下一轮是系统设计面
        ## 待办与跟进
        - 准备系统设计题：秒杀库存
        ## 复盘建议
        - 一致性问题可以先画时序，再讲方案
        - 补充一次线上缓存事故的真实数字
        ## 经验沉淀
        - 一致性问题先画读写时序，再说兜底方案
        ## 题库更新
        - 问：如果删除缓存失败了怎么办？
          答：重试队列 + binlog 异步兜底，监控删除失败率并告警。
        """;

    /// <summary>The bilingual answer of the pinned/bilingual island states (DemoMode snapshotIsland).</summary>
    public const string IslandBilingualQuestion =
        "In RQ2 the map is learned without labels. How do you know the learned prior is correct?";

    public const string IslandBilingualAnswer = """
        > 问：RQ2 没有真实信道作标签，怎么确认学到的先验是对的？

        The prior is not guaranteed to be exactly correct without ground-truth labels.
        > 译：没有真实标签，这个先验不能保证完全正确。

        I assess it through held-out service pilots and compare it with simpler baselines.
        > 译：我用留出的服务导频评估，并和更简单的基线比较。

        It stays matched when the installation changes, which a fixed mapping does not.
        > 译：安装条件变了它仍然匹配，固定映射做不到。
        """;

    /// <summary>The Chinese line under the bilingual island answer (DemoMode's 译 line).</summary>
    public const string IslandBilingualTranslation = "没有真实标签，这个先验不能保证完全正确。";

    // ---- Series (populateDemo's three workspaces) -------------------------------------

    private static IReadOnlyList<WorkspaceSeries> BuildSeries(DateTimeOffset now)
    {
        var lessons = new List<WorkspaceLesson>();
        foreach (var (text, pinned) in new[]
                 {
                     ("被问项目时先报规模和结果数字，再讲方案", true),
                     ("一致性问题先画读写时序，再说兜底方案", false),
                     ("反问环节准备 2 个关于团队技术栈的问题", false)
                 })
            lessons.Add(WorkspaceLesson.Create(text) with { Pinned = pinned });
        var questionBank = new List<QuestionBankItem>
        {
            new()
            {
                Question = "数据库和缓存同时更新时怎么保证一致性？",
                Answer = "先更新数据库再删除缓存；删除失败进入重试队列，并用 Canal 订阅 binlog 再删一次；热点 key 做延迟双删。",
                TimesAsked = 2,
                UpdatedAt = now.AddHours(-1)
            },
            new()
            {
                Question = "如果删除缓存失败了怎么办？",
                Answer = "重试队列 + binlog 异步兜底，监控删除失败率并告警。",
                TimesAsked = 1,
                UpdatedAt = now.AddHours(-2)
            },
            new()
            {
                Question = "为什么选 Kafka 而不是 RabbitMQ？",
                Answer = "吞吐和分区扩展性更好，订单事件需要按 key 有序且可回放。",
                TimesAsked = 0,
                UpdatedAt = now.AddHours(-3)
            }
        };
        var interview = new WorkspaceSeries(InterviewSeriesId.ToString("D"), InterviewSeriesName,
            now.AddDays(-10), [], lessons)
        {
            Mode = "interview",
            Notes = "上一轮一面问了 MySQL 索引和 Redis，反馈是项目讲得不够量化。",
            Fields = new Dictionary<string, string>
            {
                ["company"] = "字节跳动 · 抖音电商",
                ["position"] = "后端开发工程师（2-1）",
                ["interviewer"] = "二面面试官为订单中台负责人，偏重系统设计",
                ["highlights"] = "订单服务缓存层改造：P99 从 120 ms 降到 18 ms；大促 8 万 QPS 零故障",
                ["salary"] = "期望 45k × 15，两周内可到岗"
            },
            Documents = DocumentsOf(
                ("简历-2026.pdf", ["订单服务缓存层，Redis，延迟双删，Canal 订阅 binlog"], "简历"),
                ("岗位JD-后端开发.md", ["高并发、分布式缓存、消息队列"], "岗位 JD"),
                ("八股-缓存与数据库.md", ["缓存穿透、击穿、雪崩；一致性方案对比"], "八股 / 面经"),
                ("抖音电商-团队介绍.pdf", ["交易中台、订单履约"], "公司信息")),
            QuestionBank = questionBank,
            StageReview = """
                ## 总体进展
                已完成 2 次模拟和 1 次真实面试，缓存相关问题回答越来越完整，项目量化表达明显改善。
                ## 高频问题
                - 缓存一致性（3 次）：先更新库再删缓存 + binlog 兜底
                - 项目难点（2 次）：大促 8 万 QPS 的容量评估
                ## 下一步准备清单
                1. 系统设计：秒杀库存扣减
                2. 补充一次线上事故的复盘数字
                """,
            StageReviewAt = now.AddSeconds(-3_600),
            SkillIds = [SkillDesignId.ToString("D")]
        };
        var academic = new WorkspaceSeries(AcademicSeriesId.ToString("D"), AcademicSeriesName,
            now.AddDays(-1), [], [])
        {
            Mode = "academic",
            Fields = new Dictionary<string, string>
            {
                ["title"] = "Streaming Code-Switching ASR with Punctuation-Aware Decoding",
                ["venue"] = "ACL 2026 · Speech 分会场 · 15 分钟 + 5 分钟提问"
            },
            Documents = DocumentsOf(
                ("paper-final.pdf", ["method"], "论文"),
                ("slides-v3.pptx", ["slide"], "PPT / 幻灯片"))
        };
        var business = new WorkspaceSeries(BusinessSeriesId.ToString("D"), BusinessSeriesName,
            now.AddDays(-2), [], [])
        {
            Mode = "meeting",
            Fields = new Dictionary<string, string>
            {
                ["goal"] = "确认二期报价与上线时间",
                ["numbers"] = "二期报价 86 万；上线窗口 11 月 20 日"
            }
        };
        // The Windows shell lists series newest-first; this order matches the Mac home screenshot
        // (ACL 2026 口头报告, 周一客户例会 · 华东区, 字节后端二面).
        return [academic, business, interview];
    }

    private static readonly Guid SkillSpokenId = Guid.Parse("0f8c1a52-0000-4000-8000-000000000201");
    private static readonly Guid SkillDesignId = Guid.Parse("0f8c1a52-0000-4000-8000-000000000202");
    private static readonly Guid SkillRebuttalId = Guid.Parse("0f8c1a52-0000-4000-8000-000000000203");
    private static readonly Guid[] DocumentIds =
    [
        Guid.Parse("0f8c1a52-0000-4000-8000-000000000101"), Guid.Parse("0f8c1a52-0000-4000-8000-000000000102"),
        Guid.Parse("0f8c1a52-0000-4000-8000-000000000103"), Guid.Parse("0f8c1a52-0000-4000-8000-000000000104"),
        Guid.Parse("0f8c1a52-0000-4000-8000-000000000105"), Guid.Parse("0f8c1a52-0000-4000-8000-000000000106")
    ];
    private static int nextDocument;

    private static IReadOnlyList<ContextDocument> DocumentsOf(
        params (string Name, string[] Pages, string Category)[] documents) =>
        documents.Select(document => new ContextDocument
        {
            Id = DocumentIds[nextDocument++ % DocumentIds.Length].ToString("D").ToUpperInvariant(),
            Name = document.Name,
            Pages = document.Pages,
            Category = document.Category
        }).ToList();

    // ---- Skills (populateDemo) ---------------------------------------------------------

    private static IReadOnlyList<Skill> BuildSkills() =>
    [
        new()
        {
            Id = SkillSpokenId.ToString("D"),
            Name = "面试口语化",
            Summary = "回答像真人说话，先结论后例子",
            Content = "先用一句话回答，再讲一个我做过的具体例子；不用“首先其次最后”。",
            Global = true,
            Enabled = true
        },
        new()
        {
            Id = SkillDesignId.ToString("D"),
            Name = "系统设计答题框架",
            Summary = "需求 → 估算 → 架构 → 取舍",
            Content = "系统设计题按 需求澄清、容量估算、核心架构、关键取舍 四步回答。",
            Global = true,
            Enabled = true
        },
        new()
        {
            Id = SkillRebuttalId.ToString("D"),
            Name = "学术答辩应对",
            Summary = "承认局限、给出补充实验",
            Content = "被质疑时先承认问题，再说明已有证据和计划中的补充实验。",
            Global = true,
            Enabled = true
        }
    ];

    // ---- Sessions (populateDemo + demoReviewNotes + demoWeeklyMeeting) -----------------

    private static IReadOnlyList<TranscriptLine> LiveLines(DateTimeOffset now)
    {
        TranscriptLine Line(double secondsAgo, string source, string text, bool echo = false) => new()
        {
            Id = Guid.NewGuid(),
            Date = now.AddSeconds(-secondsAgo),
            StartedAt = now.AddSeconds(-secondsAgo),
            EndedAt = now.AddSeconds(-secondsAgo + 3),
            Speaker = source == "system" ? "remote" : "me",
            Text = text,
            SuspectedEcho = echo ? true : null
        };
        return
        [
            Line(95, "system", "好，我们先聊聊你最近做的项目吧，简单介绍一下你负责的部分。"),
            Line(80, "microphone", "我主要负责订单服务的缓存层，把核心查询从数据库迁移到 Redis。"),
            Line(58, "system", "嗯，这个挺关键的。"),
            Line(52, "system", "那数据库和缓存同时更新的时候，你们是怎么保证一致性的？"),
            Line(51, "microphone", "数据库和缓存同时更新的时候你们是怎么保证一致性的", echo: true),
            Line(20, "microphone", "我们用的是先更新数据库、再删除缓存的方式。")
        ];
    }

    private static (IReadOnlyList<AnswerRecord> Answers, IReadOnlyList<SessionQuestionEvent> Questions) BuildReviewEvents(
        DateTimeOffset started)
    {
        var early = new AnswerRecord
        {
            Id = Guid.NewGuid(),
            Date = started.AddSeconds(300),
            Question = "简单介绍一下你负责的部分",
            Answer = "我负责订单服务的缓存层……",
            Model = "gpt-5.5-mini",
            Seconds = 3.1,
            FirstTokenSeconds = 0.8,
            Automatic = true
        };
        var answer = new AnswerRecord
        {
            Id = Guid.NewGuid(),
            Date = started.AddSeconds(300),
            Question = LiveQuestion,
            Answer = LiveAnswer,
            Model = "gpt-5.5-mini",
            Seconds = 4.2,
            FirstTokenSeconds = 0.9,
            Automatic = true,
            Evidence = new EvidenceBundle(Guid.NewGuid())
            {
                Retrieval = "演示：关键词查找，未调用模型",
                Materials =
                [
                    new ContextExcerpt("简历-2026.pdf", 1, "订单服务缓存层，Redis，延迟双删，Canal 订阅 binlog"),
                    new ContextExcerpt("八股-缓存与数据库.md", 2, "缓存穿透、击穿、雪崩；一致性方案对比")
                ],
                Screen = new ScreenContext { Source = "屏幕 · 面试题页面", CapturedAt = started.AddSeconds(290) },
                WebSources = [new WebResult("Redis 缓存一致性最佳实践", "https://redis.io/docs/latest/operate/oss_and_stack/", "延迟双删与 binlog 兜底的取舍")],
                Notes = ["联网来源为演示数据，未实际检索"]
            }
        };
        SessionQuestionEvent question = new()
        {
            Id = Guid.NewGuid().ToString("D"),
            Date = started.AddSeconds(120),
            Text = LiveQuestion,
            Intent = "question",
            Score = 0.95,
            Disposition = "auto"
        };
        return ([early, answer], [question]);
    }

    private static StructuredNotesDoc BuildReviewNotes(DateTimeOffset started)
    {
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.Interview);
        NotesSectionResult Section(string id, params NotesItem[] items)
        {
            var spec = template.Sections.First(section => section.Id == id);
            return new NotesSectionResult(id, spec.Title, spec.Kind, spec.IsGap, items);
        }
        return new StructuredNotesDoc(
            NotesTemplateKind.Interview.Raw(), template.Name, "zh", PlannedMeetingTitle,
            [
                Section("impression",
                    new NotesItem { Text = "项目部分讲得扎实、有数字；系统设计追问时结论来得太慢，缓存击穿的方案仍然没讲完整。" }),
                Section("questions",
                    new NotesItem
                    {
                        Text = "如果删除缓存失败了怎么办？", Time = "18:22",
                        Answer = "重试队列 + binlog 异步删除，两道保险。", Verdict = "good"
                    },
                    new NotesItem
                    {
                        Text = "热点 key 过期的一瞬间大量请求打到数据库，怎么办？", Time = "27:05",
                        Answer = "加锁……（停顿 8 秒）……可能用布隆过滤器。", Verdict = "improve",
                        Better = "先给结论：互斥锁重建 + 逻辑过期，二选一看一致性要求。布隆过滤器解决的是穿透，不是击穿，别混用。"
                    }),
                Section("missed",
                    new NotesItem { Text = "简历里的“大促 8 万 QPS 零故障”一次也没提到", Time = "31:40" },
                    new NotesItem { Text = "职位描述里的“抖音电商订单链路”没有主动联系起来", Time = "40:12" }),
                Section("improve",
                    new NotesItem { Text = "系统设计题先说结论，再展开", Time = "27:05" },
                    new NotesItem { Text = "准备好击穿、穿透、雪崩三者的区别", Time = "27:30" },
                    new NotesItem { Text = "主动带出大促数据", Time = "31:40" })
            ],
            [new TranscriptCorrection { Line = 3, Original = "缓存鸡穿", Corrected = "缓存击穿" }],
            0,
            started.AddSeconds(2_700),
            "gpt-5.5");
    }

    private static DemoSession BuildReview(DateTimeOffset started, WorkspaceSeries interview)
    {
        var lines = LiveLines(started).Select((line, index) => (line with
        {
            Date = started.AddSeconds(index * 40 + 15),
            StartedAt = null,
            EndedAt = null,
            // Mac's saved SessionTranscriptLine keeps only date, speaker and text.
            SuspectedEcho = null
        })).ToList();
        var meta = new SessionMeta
        {
            Id = ReviewSessionId,
            Title = ReviewTitle,
            Mode = "interview",
            StartedAt = started,
            EndedAt = started.AddSeconds(2_640),
            AudioSource = "Zoom · 后端工程师二面",
            Recognizer = "X-ASR 中英 · 480 ms + 整句精修",
            AnswerModel = "gpt-5.5-mini",
            TranscriptCount = 6,
            QuestionCount = 6,
            AnswerCount = 2,
            SummaryState = "ready",
            WorkspaceId = interview.Id,
            WorkspaceName = interview.Name
        };
        var (answers, questions) = BuildReviewEvents(started);
        var parsed = SessionSummaryParser.Parse(ReviewSummaryMarkdown);
        var summary = new SessionSummary
        {
            Title = parsed.Title ?? "",
            Markdown = ReviewSummaryMarkdown,
            Questions = parsed.Questions,
            GeneratedAt = started.AddSeconds(2_700),
            Model = "gpt-5.5",
            Lessons = ["一致性问题先画读写时序，再说兜底方案"],
            Bank =
            [
                new BankEntry
                {
                    Question = "热点 key 过期的一瞬间大量请求打到数据库，怎么办？",
                    Answer = "先给结论：互斥锁重建 + 逻辑过期，二选一看一致性要求。布隆过滤器解决的是穿透，不是击穿。"
                }
            ]
        };
        return new DemoSession(meta, lines, answers, questions, ReviewSummaryMarkdown,
            BuildReviewNotes(started), summary);
    }

    private static DemoSession BuildWeekly(DateTimeOffset now, WorkspaceSeries business)
    {
        var localToday = now.ToLocalTime().Date;
        var started = new DateTimeOffset(DateTime.SpecifyKind(localToday.AddHours(10), DateTimeKind.Unspecified),
            TimeZoneInfo.Local.GetUtcOffset(localToday.AddHours(10)));
        TranscriptLine Line(int offsetSeconds, string speaker, string text) => new()
        {
            Id = Guid.NewGuid(),
            Date = started.AddSeconds(offsetSeconds),
            StartedAt = started.AddSeconds(offsetSeconds),
            EndedAt = started.AddSeconds(offsetSeconds + 4),
            Speaker = speaker,
            Text = text
        };
        var lines = new List<TranscriptLine>
        {
            Line(192, "remote", "这个版本下周三之前必须上线，客户那边已经在催了。"),
            Line(295, "remote", "好，那就这么定。支付那两个 bug 今天修掉。"),
            Line(340, "me", "回归测试我来排，周二下午给结果。"),
            Line(750, "remote", "设计稿周四之前，或者周五也行，到时候再看。"),
            Line(1_268, "remote", "客户要的导出功能，目前还没人认领。")
        };
        var meta = new SessionMeta
        {
            Id = WeeklySessionId,
            Title = WeeklyTitle,
            Mode = "meeting",
            StartedAt = started,
            EndedAt = started.AddSeconds(38 * 60),
            AudioSource = "电脑声音",
            Recognizer = "X-ASR 中英 · 480 ms + 整句精修",
            AnswerModel = "gpt-5.5",
            TranscriptCount = 4,
            QuestionCount = 0,
            AnswerCount = 0,
            SummaryState = "ready",
            WorkspaceId = business.Id,
            WorkspaceName = business.Name
        };
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.Work);
        NotesSectionResult Section(string id, params NotesItem[] items)
        {
            var spec = template.Sections.First(section => section.Id == id);
            return new NotesSectionResult(id, spec.Title, spec.Kind, spec.IsGap, items);
        }
        var notes = new StructuredNotesDoc(
            NotesTemplateKind.Work.Raw(), template.Name, "zh", WeeklyTitle,
            [
                Section("summary", new NotesItem { Text = "新版本下周三前上线；支付 bug 今天修，回归测试周二出结果。" }),
                Section("decisions", new NotesItem { Text = "新版本下周三前上线", Time = "03:12" }),
                Section("actions",
                    new NotesItem { Text = "回归测试并给出结果", Time = "05:40", Owner = "我", Due = "周二下午" },
                    new NotesItem { Text = "修复支付相关的 2 个 bug", Time = "04:55", Owner = "对方", Due = "今天" },
                    new NotesItem { Text = "设计稿复审", Time = "12:30", Due = "周四", Uncertain = true }),
                Section("risks",
                    new NotesItem { Text = "客户要求的导出功能没人认领", Time = "21:08" },
                    new NotesItem { Text = "设计稿复审是周四还是周五，两种说法", Time = "12:30", Uncertain = true })
            ],
            [],
            0,
            started.AddSeconds(40 * 60),
            "gpt-5.5");
        var lineOffsets = lines.Select(line => (int)(line.Date - started).TotalSeconds).ToList();
        notes = NotesVerifier.Verify(notes, lineOffsets, (meta.EndedAt!.Value - meta.StartedAt).TotalSeconds, started);
        var summary = new SessionSummary
        {
            Title = WeeklyTitle,
            Markdown = NotesMarkdown.Render(notes, WeeklyTitle, ""),
            Questions = [],
            GeneratedAt = notes.GeneratedAt,
            Model = "gpt-5.5",
            Lessons = [],
            Bank = []
        };
        return new DemoSession(meta, lines, [], [], summary.Markdown, notes, summary);
    }

    // ---- Materials (workspace documents as the library sees them) ----------------------

    private static IReadOnlyList<MaterialDocument> BuildMaterials(IReadOnlyList<WorkspaceSeries> series, DateTimeOffset now)
    {
        var materials = new List<MaterialDocument>();
        // The interview series' four materials come first (the prepare page uses them), then the
        // talk's two; this matches the order the Mac demo's workspaces were installed in.
        var ordered = new[] { InterviewSeriesName, AcademicSeriesName, BusinessSeriesName }
            .Select(name => series.FirstOrDefault(item => item.Name == name))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();
        foreach (var document in ordered.SelectMany(item => item.Documents))
        {
            var extension = Path.GetExtension(document.Name).ToLowerInvariant();
            if (extension.Length == 0) extension = ".pdf";
            materials.Add(new MaterialDocument
            {
                Id = document.Id.Replace("-", "").ToLowerInvariant(),
                Name = document.Name,
                Sha256 = "demo",
                Extension = extension,
                ImportedAt = now,
                Reader = "演示",
                Blocks = document.Pages
                    .Select((page, index) => new DocumentBlock("第 " + (index + 1) + " 页", page, index + 1))
                    .ToList(),
                Warnings = []
            });
        }
        return materials;
    }

    /// <summary>Builds the whole demo data set for one instant (Mac: <c>Date()</c> inside populateDemo).</summary>
    public static DemoDataset Create(DateTimeOffset now)
    {
        var reviewStarted = now.AddSeconds(-86_400);
        var series = BuildSeries(now);
        var interview = series.First(item => item.Id == InterviewSeriesId.ToString("D"));
        var business = series.First(item => item.Id == BusinessSeriesId.ToString("D"));
        var review = BuildReview(reviewStarted, interview);
        var weekly = BuildWeekly(now, business);
        var linkedSeries = series
            .Select(item => item.Id == interview.Id ? item.WithSession(review.Meta.Id.ToString("D"))
                : item.Id == business.Id ? item.WithSession(weekly.Meta.Id.ToString("D"))
                : item)
            .ToList();
        var linkedInterview = linkedSeries.First(item => item.Id == interview.Id);
        var draft = new DemoDraft(PlannedMeetingTitle, "interview", InterviewSeriesName,
            linkedInterview.Documents.Select(document => document.Name).ToList());
        var live = new DemoLiveSession(
            now.AddSeconds(-754),
            LiveLines(now),
            ["好，我们先聊聊你最近做的项目吧，简单介绍一下你负责的部分。", LiveQuestion],
            LivePartialTranscript,
            LiveQuestion,
            LiveAnswer,
            review.Answers,
            1,
            PlannedMeetingTitle,
            "正在聆听 · 启动 0.4 s",
            "X-ASR 本地识别 · RTF 0.08 · 精修 0.12 s",
            0.08,
            0.12,
            0.9,
            4.2,
            "gpt-5.5",
            "演示：关键词查找，未调用模型");
        return new DemoDataset(linkedSeries, BuildSkills(), BuildMaterials(linkedSeries, now), review, weekly, live, draft);
    }

    /// <summary>
    /// The first-run guide's sample interview (OnboardingSampleMeeting.swift). Its lines are
    /// sent through <see cref="UiText"/> because on Mac they are <c>String(localized:)</c>
    /// literals: the meeting itself is Chinese, the guide's copy follows the interface language.
    /// </summary>
    public static DemoOnboardingSample Onboarding(bool english = false)
    {
        string T(string text) => UiText.Translate(text, english);
        return new(
            T("面试示例"),
            34,
            [
                new(0, false, T("先聊聊你简历里的检索项目。上周加入重排之后，结果怎么样？"), 0.8),
                new(1, true, T("这组验证集上，Recall@10 从 72% 到了 78%，但候选数量也同时增加了。"), 5.0),
                new(2, false, T("那提升来自重排，还是更多候选？需要怎样的对照才能判断？"), 10.5),
                new(3, true, T("现在还不能确定。我会固定候选数量，再做一组去掉重排的消融。"), 16.0),
                new(4, false, T("好，还要看延迟分布。平均值可能会掩盖长尾，别只报一个数字。"), 21.0),
                new(5, true, T("我会同时报告 P95 延迟，并把失败样例一起整理出来。"), 26.5),
                new(6, false, T("最后一个问题：结论换一组数据还成立吗？"), 29.5)
            ],
            [
                new(0, false, T("已答：Recall@10 从 72% 到 78%，候选数同时变化"), 8.5),
                new(1, false, T("追问：提升来自重排，还是更多候选？"), 14.0),
                new(2, true, T("复盘：固定候选数，补充去掉重排的消融"), 19.0),
                new(3, false, T("追问：报告延迟分布，关注长尾"), 24.0),
                new(4, true, T("复盘：补充 P95 延迟与失败样例"), 28.0),
                new(5, true, T("复盘：准备换一组数据的验证"), 32.0)
            ],
            new(T("提升来自重排，还是更多候选？"),
                T("目前不能区分。需要固定候选数量，对比有无重排；同时记录检索质量和延迟，再判断提升来源。"),
                13.0));
    }
}
