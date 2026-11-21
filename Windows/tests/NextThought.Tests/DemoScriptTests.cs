using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// D1: the demo mode's copy is the Mac demo's copy, verbatim. The expectations below are copied
/// from Sources/NextThought/DemoMode.swift (populateDemo / populateDemoDraft / demoReviewNotes /
/// demoWeeklyMeeting / snapshotIsland), DemoFilm.swift and OnboardingSampleMeeting.swift; the
/// dataset itself lives in NextThought.Platform.DemoScript so no Avalonia render pass is needed.
/// </summary>
public sealed class DemoScriptTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 20, 15, 32, 0, TimeSpan.FromHours(8));

    private static DemoDataset Demo() => DemoScript.Create(Now);

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "DemoContent.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    // ---- series / workspaces ----------------------------------------------------------

    [Fact]
    public void DemoHasTheThreeMacWorkspaces()
    {
        var demo = Demo();
        Assert.Equal(3, demo.Series.Count);
        Assert.Equal(new[] { DemoScript.AcademicSeriesName, DemoScript.BusinessSeriesName, DemoScript.InterviewSeriesName },
            demo.Series.Select(series => series.Name).ToArray());
        var interview = demo.InterviewSeries;
        Assert.Equal("interview", interview.Mode);
        Assert.Equal("上一轮一面问了 MySQL 索引和 Redis，反馈是项目讲得不够量化。", interview.Notes);

        var fields = interview.Fields!;
        Assert.Equal("字节跳动 · 抖音电商", fields["company"]);
        Assert.Equal("后端开发工程师（2-1）", fields["position"]);
        Assert.Equal("二面面试官为订单中台负责人，偏重系统设计", fields["interviewer"]);
        Assert.Equal("订单服务缓存层改造：P99 从 120 ms 降到 18 ms；大促 8 万 QPS 零故障", fields["highlights"]);
        Assert.Equal("期望 45k × 15，两周内可到岗", fields["salary"]);

        var academic = demo.AcademicSeries;
        Assert.Equal("academic", academic.Mode);
        Assert.Equal("Streaming Code-Switching ASR with Punctuation-Aware Decoding", academic.Fields!["title"]);
        Assert.Equal("ACL 2026 · Speech 分会场 · 15 分钟 + 5 分钟提问", academic.Fields["venue"]);

        var business = demo.BusinessSeries;
        Assert.Equal("meeting", business.Mode);
        Assert.Equal("确认二期报价与上线时间", business.Fields!["goal"]);
        Assert.Equal("二期报价 86 万；上线窗口 11 月 20 日", business.Fields["numbers"]);
    }

    [Fact]
    public void DemoDocumentsAndKnowledgeMatchTheMacSeries()
    {
        var demo = Demo();
        var interview = demo.InterviewSeries;
        Assert.Equal(new[] { "简历-2026.pdf", "岗位JD-后端开发.md", "八股-缓存与数据库.md", "抖音电商-团队介绍.pdf" },
            interview.Documents.Select(document => document.Name).ToArray());
        Assert.Equal(new[] { "简历", "岗位 JD", "八股 / 面经", "公司信息" },
            interview.Documents.Select(document => document.Category).ToArray());
        Assert.Equal("订单服务缓存层，Redis，延迟双删，Canal 订阅 binlog", interview.Documents[0].Pages[0]);

        Assert.Equal(new[] { "被问项目时先报规模和结果数字，再讲方案", "一致性问题先画读写时序，再说兜底方案", "反问环节准备 2 个关于团队技术栈的问题" },
            interview.Lessons.Select(lesson => lesson.Text).ToArray());
        Assert.True(interview.Lessons[0].Pinned);
        Assert.False(interview.Lessons[1].Pinned);

        Assert.Equal(3, interview.QuestionBank.Count);
        Assert.Equal("数据库和缓存同时更新时怎么保证一致性？", interview.QuestionBank[0].Question);
        Assert.Equal("先更新数据库再删除缓存；删除失败进入重试队列，并用 Canal 订阅 binlog 再删一次；热点 key 做延迟双删。",
            interview.QuestionBank[0].Answer);
        Assert.Equal(2, interview.QuestionBank[0].TimesAsked);
        Assert.Equal("如果删除缓存失败了怎么办？", interview.QuestionBank[1].Question);
        Assert.Equal(1, interview.QuestionBank[1].TimesAsked);
        Assert.Equal("为什么选 Kafka 而不是 RabbitMQ？", interview.QuestionBank[2].Question);
        Assert.Equal(0, interview.QuestionBank[2].TimesAsked);

        Assert.Contains("## 总体进展", interview.StageReview);
        Assert.Contains("已完成 2 次模拟和 1 次真实面试，缓存相关问题回答越来越完整，项目量化表达明显改善。", interview.StageReview);
        Assert.Contains("1. 系统设计：秒杀库存扣减", interview.StageReview);
        Assert.NotNull(interview.StageReviewAt);
        Assert.Equal(new[] { demo.Skills[1].Id }, interview.SkillIds!.ToArray());
    }

    [Fact]
    public void DemoSessionsAreAssignedToTheirMacSeries()
    {
        var demo = Demo();
        Assert.Equal(new[] { demo.Review.Meta.Id.ToString("D") }, demo.InterviewSeries.SessionIds.ToArray());
        Assert.Equal(new[] { demo.Weekly.Meta.Id.ToString("D") }, demo.BusinessSeries.SessionIds.ToArray());
        Assert.Empty(demo.AcademicSeries.SessionIds);
    }

    // ---- skills / materials -----------------------------------------------------------

    [Fact]
    public void DemoSkillsMatchTheMacSkillLibrary()
    {
        var demo = Demo();
        Assert.Equal(new[] { "面试口语化", "系统设计答题框架", "学术答辩应对" }, demo.Skills.Select(skill => skill.Name).ToArray());
        Assert.Equal("回答像真人说话，先结论后例子", demo.Skills[0].Summary);
        Assert.Equal("先用一句话回答，再讲一个我做过的具体例子；不用“首先其次最后”。", demo.Skills[0].Content);
        Assert.Equal("需求 → 估算 → 架构 → 取舍", demo.Skills[1].Summary);
        Assert.Equal("系统设计题按 需求澄清、容量估算、核心架构、关键取舍 四步回答。", demo.Skills[1].Content);
        Assert.Equal("承认局限、给出补充实验", demo.Skills[2].Summary);
        Assert.Equal("被质疑时先承认问题，再说明已有证据和计划中的补充实验。", demo.Skills[2].Content);
        // E28: the Mac demo library installs 面试口语化 as a global skill; the other two are
        // per-series (SkillLibrary.installDemo in Sources/NextThought/SkillLibrary.swift).
        Assert.Equal(new[] { true, false, false }, demo.Skills.Select(skill => skill.Global).ToArray());
    }

    [Fact]
    public void DemoLibraryHoldsTheWorkspaceMaterials()
    {
        var demo = Demo();
        Assert.Equal(6, demo.Materials.Count);
        Assert.Equal(new[] { "简历-2026.pdf", "岗位JD-后端开发.md", "八股-缓存与数据库.md", "抖音电商-团队介绍.pdf", "paper-final.pdf", "slides-v3.pptx" },
            demo.Materials.Select(material => material.Name).ToArray());
        Assert.Equal(".pdf", demo.Materials[0].Extension);
        Assert.Equal(".md", demo.Materials[1].Extension);
        Assert.Equal("订单服务缓存层，Redis，延迟双删，Canal 订阅 binlog", demo.Materials[0].Blocks[0].Text);
        Assert.Equal("交易中台、订单履约", demo.Materials[3].Blocks[0].Text);
    }

    // ---- the archived review meeting --------------------------------------------------

    [Fact]
    public void ArchivedReviewMetaMatchesDemoMode()
    {
        var review = Demo().Review;
        Assert.Equal("后端二面：缓存一致性与项目经历", review.Meta.Title);
        Assert.Equal("interview", review.Meta.Mode);
        Assert.Equal("Zoom · 后端工程师二面", review.Meta.AudioSource);
        Assert.Equal("X-ASR 中英 · 480 ms + 整句精修", review.Meta.Recognizer);
        Assert.Equal("gpt-5.5-mini", review.Meta.AnswerModel);
        Assert.Equal(6, review.Meta.TranscriptCount);
        Assert.Equal(6, review.Meta.QuestionCount);
        Assert.Equal(2, review.Meta.AnswerCount);
        Assert.Equal("ready", review.Meta.SummaryState);
        Assert.Equal(DemoScript.InterviewSeriesName, review.Meta.WorkspaceName);
        Assert.Equal(Now.AddDays(-1), review.Meta.StartedAt);
        Assert.Equal(Now.AddDays(-1).AddSeconds(2_640), review.Meta.EndedAt!.Value);
    }

    [Fact]
    public void ReviewTranscriptAndAnswersAreVerbatim()
    {
        var review = Demo().Review;
        Assert.Equal(new[]
        {
            "好，我们先聊聊你最近做的项目吧，简单介绍一下你负责的部分。",
            "我主要负责订单服务的缓存层，把核心查询从数据库迁移到 Redis。",
            "嗯，这个挺关键的。",
            "那数据库和缓存同时更新的时候，你们是怎么保证一致性的？",
            "数据库和缓存同时更新的时候你们是怎么保证一致性的",
            "我们用的是先更新数据库、再删除缓存的方式。"
        }, review.Lines.Select(line => line.Text).ToArray());
        Assert.Equal(new[] { "remote", "me", "remote", "remote", "me", "me" },
            review.Lines.Select(line => line.Speaker).ToArray());
        Assert.All(review.Lines.Select(line => line.SuspectedEcho), echo => Assert.Null(echo));
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 15, 32, 15, TimeSpan.FromHours(8)), review.Lines[0].Date);

        Assert.Equal(2, review.Answers.Count);
        Assert.Equal("简单介绍一下你负责的部分", review.Answers[0].Question);
        Assert.Equal("我负责订单服务的缓存层……", review.Answers[0].Answer);
        Assert.Equal("gpt-5.5-mini", review.Answers[0].Model);
        Assert.Equal(3.1, review.Answers[0].Seconds!.Value, 10);
        Assert.Equal(0.8, review.Answers[0].FirstTokenSeconds!.Value, 10);
        Assert.Equal(DemoScript.LiveQuestion, review.Answers[1].Question);
        Assert.Equal(DemoScript.LiveAnswer, review.Answers[1].Answer);
        Assert.Equal("演示：关键词查找，未调用模型", review.Answers[1].Evidence!.Retrieval);
        Assert.All(review.Answers, answer => Assert.True(answer.Automatic));

        var question = Assert.Single(review.Questions);
        Assert.Equal(DemoScript.LiveQuestion, question.Text);
        Assert.Equal("question", question.Intent);
        Assert.Equal(0.95, question.Score);
        Assert.Equal("auto", question.Disposition);
    }

    [Fact]
    public void ReviewSummaryMarkdownIsVerbatim()
    {
        var review = Demo().Review;
        Assert.Equal(DemoScript.ReviewSummaryMarkdown, review.SummaryMarkdown);
        var lines = Norm(DemoScript.ReviewSummaryMarkdown).Split('\n');
        Assert.Equal("# 标题", lines[0]);
        Assert.Equal("后端二面：缓存一致性与项目经历", lines[1]);
        Assert.Contains("面试官围绕订单服务缓存层展开，重点考察缓存与数据库的一致性、失败兜底和高并发下的取舍。整体回答完整，延迟双删与 binlog 兜底讲得清楚。",
            DemoScript.ReviewSummaryMarkdown);
        Assert.Contains("## 问题清单", DemoScript.ReviewSummaryMarkdown);
        Assert.Contains("6. 你还有什么问题想问我们？ —— 我的回答要点：问了团队的技术栈与值班制度", DemoScript.ReviewSummaryMarkdown);
        Assert.Contains("## 题库更新", DemoScript.ReviewSummaryMarkdown);

        Assert.Equal("后端二面：缓存一致性与项目经历", review.Summary.Title);
        Assert.Equal(6, review.Summary.Questions!.Count);
        Assert.Equal(new[] { "一致性问题先画读写时序，再说兜底方案" }, review.Summary.Lessons!.ToArray());
        Assert.Equal("热点 key 过期的一瞬间大量请求打到数据库，怎么办？", review.Summary.Bank![0].Question);
    }

    [Fact]
    public void ReviewStructuredNotesMatchDemoReviewNotes()
    {
        var notes = Demo().Review.Notes;
        Assert.Equal(NotesTemplateKind.Interview.Raw(), notes.TemplateId);
        Assert.Equal("字节跳动 · 后端二面", notes.Title);
        Assert.Equal("zh", notes.Language);
        Assert.Equal("gpt-5.5", notes.Model);
        Assert.Equal(new[] { "impression", "questions", "missed", "improve" }, notes.Sections.Select(section => section.Id).ToArray());
        Assert.Equal("一句话印象", notes.Sections[0].Title);
        Assert.Equal("项目部分讲得扎实、有数字；系统设计追问时结论来得太慢，缓存击穿的方案仍然没讲完整。",
            Assert.Single(notes.Sections[0].Items).Text);

        var questions = notes.Sections[1].Items;
        Assert.Equal("如果删除缓存失败了怎么办？", questions[0].Text);
        Assert.Equal("18:22", questions[0].Time);
        Assert.Equal("重试队列 + binlog 异步删除，两道保险。", questions[0].Answer);
        Assert.Equal("good", questions[0].Verdict);
        Assert.Equal("热点 key 过期的一瞬间大量请求打到数据库，怎么办？", questions[1].Text);
        Assert.Equal("27:05", questions[1].Time);
        Assert.Equal("加锁……（停顿 8 秒）……可能用布隆过滤器。", questions[1].Answer);
        Assert.Equal("improve", questions[1].Verdict);
        Assert.Equal("先给结论：互斥锁重建 + 逻辑过期，二选一看一致性要求。布隆过滤器解决的是穿透，不是击穿，别混用。",
            questions[1].Better);

        Assert.Equal(new[] { "简历里的“大促 8 万 QPS 零故障”一次也没提到", "职位描述里的“抖音电商订单链路”没有主动联系起来" },
            notes.Sections[2].Items.Select(item => item.Text).ToArray());
        Assert.Equal(new[] { "系统设计题先说结论，再展开", "准备好击穿、穿透、雪崩三者的区别", "主动带出大促数据" },
            notes.Sections[3].Items.Select(item => item.Text).ToArray());

        var correction = Assert.Single(notes.Corrections!);
        Assert.Equal(3, correction.Line);
        Assert.Equal("缓存鸡穿", correction.Original);
        Assert.Equal("缓存击穿", correction.Corrected);
        Assert.Equal(Now.AddDays(-1).AddSeconds(2_700), notes.GeneratedAt!.Value);
    }

    // ---- the archived work meeting ----------------------------------------------------

    [Fact]
    public void ArchivedWeeklyMeetingMatchesDemoMode()
    {
        var weekly = Demo().Weekly;
        Assert.Equal("周一产品例会", weekly.Meta.Title);
        Assert.Equal("meeting", weekly.Meta.Mode);
        Assert.Equal("电脑声音", weekly.Meta.AudioSource);
        Assert.Equal("X-ASR 中英 · 480 ms + 整句精修", weekly.Meta.Recognizer);
        Assert.Equal("gpt-5.5", weekly.Meta.AnswerModel);
        Assert.Equal(4, weekly.Meta.TranscriptCount);
        Assert.Equal(0, weekly.Meta.QuestionCount);
        Assert.Equal(0, weekly.Meta.AnswerCount);
        Assert.Equal("ready", weekly.Meta.SummaryState);
        Assert.Equal(DemoScript.BusinessSeriesName, weekly.Meta.WorkspaceName);
        Assert.Equal(10, weekly.Meta.StartedAt.Hour);
        Assert.Equal(38, (int)(weekly.Meta.EndedAt!.Value - weekly.Meta.StartedAt).TotalMinutes);

        Assert.Equal(new[]
        {
            "这个版本下周三之前必须上线，客户那边已经在催了。",
            "好，那就这么定。支付那两个 bug 今天修掉。",
            "回归测试我来排，周二下午给结果。",
            "设计稿周四之前，或者周五也行，到时候再看。",
            "客户要的导出功能，目前还没人认领。"
        }, weekly.Lines.Select(line => line.Text).ToArray());

        var notes = weekly.Notes;
        Assert.Equal(NotesTemplateKind.Work.Raw(), notes.TemplateId);
        Assert.Equal("周一产品例会", notes.Title);
        Assert.Equal(new[] { "summary", "decisions", "actions", "risks" }, notes.Sections.Select(section => section.Id).ToArray());
        Assert.Equal("新版本下周三前上线；支付 bug 今天修，回归测试周二出结果。",
            Assert.Single(notes.Sections[0].Items).Text);
        Assert.Equal("新版本下周三前上线", Assert.Single(notes.Sections[1].Items).Text);
        Assert.Equal("03:12", notes.Sections[1].Items[0].Time);

        var actions = notes.Sections[2].Items;
        Assert.Equal(3, actions.Count);
        Assert.Equal(new[] { "回归测试并给出结果", "修复支付相关的 2 个 bug", "设计稿复审" }, actions.Select(item => item.Text).ToArray());
        Assert.Equal(new[] { "我", "对方", (string?)null }, actions.Select(item => item.Owner).ToArray());
        Assert.Equal(new[] { "05:40", "04:55", "12:30" }, actions.Select(item => item.Time).ToArray());
        // NotesVerifier.verify resolves due dates relative to the meeting date (Mac screenshot: 10 月 27 日 (周二)).
        Assert.Equal(new DateTime(2026, 11, 24), actions[0].DueDate!.Value);
        Assert.Equal(new DateTime(2026, 11, 20), actions[1].DueDate!.Value);
        Assert.Equal(new DateTime(2026, 11, 26), actions[2].DueDate!.Value);
        Assert.True(actions[2].Uncertain);

        var risks = notes.Sections[3].Items;
        Assert.Equal(new[] { "客户要求的导出功能没人认领", "设计稿复审是周四还是周五，两种说法" }, risks.Select(item => item.Text).ToArray());
        Assert.True(risks[1].Uncertain);
        Assert.Equal("21:08", risks[0].Time);

        Assert.Equal("周一产品例会", weekly.Summary.Title);
        Assert.Equal(weekly.Meta.EndedAt!.Value.AddSeconds(2 * 60), weekly.Summary.GeneratedAt);
        Assert.Contains("## 决定", weekly.SummaryMarkdown);
        Assert.Contains("## 待办 · 谁 · 哪天交付", weekly.SummaryMarkdown);
        Assert.Contains("## 风险和未决问题", weekly.SummaryMarkdown);
        Assert.Contains("回归测试并给出结果", weekly.SummaryMarkdown);
        Assert.Contains("（待确认）", weekly.SummaryMarkdown);
        Assert.Empty(weekly.Summary.Questions!);
        Assert.Empty(weekly.Summary.Lessons!);
        Assert.Empty(weekly.Summary.Bank!);
    }

    // ---- the live page (populateDemo / DemoFilm) --------------------------------------

    [Fact]
    public void LiveSessionMatchesPopulateDemoAndDemoFilm()
    {
        var live = Demo().Live;
        Assert.Equal(Now.AddSeconds(-754), live.StartedAt);
        Assert.Equal(6, live.Lines.Count);
        Assert.Equal("好，我们先聊聊你最近做的项目吧，简单介绍一下你负责的部分。", live.Lines[0].Text);
        Assert.Equal("remote", live.Lines[0].Speaker);
        Assert.Equal("我主要负责订单服务的缓存层，把核心查询从数据库迁移到 Redis。", live.Lines[1].Text);
        Assert.Equal("me", live.Lines[1].Speaker);
        Assert.Equal("数据库和缓存同时更新的时候你们是怎么保证一致性的", live.Lines[4].Text);
        Assert.True(live.Lines[4].SuspectedEcho);

        Assert.Equal(new[] { "好，我们先聊聊你最近做的项目吧，简单介绍一下你负责的部分。", DemoScript.LiveQuestion },
            live.QuestionTexts.ToArray());
        Assert.Equal("如果删除缓存失败了怎么办", live.PartialTranscript);
        Assert.Equal(1, live.EchoDroppedCount);
        Assert.Equal(DemoScript.LiveQuestion, live.Question);
        Assert.Equal(DemoScript.LiveAnswer, live.Answer);
        Assert.Equal("字节跳动 · 后端二面", live.PlannedMeetingTitle);
        Assert.Equal("正在聆听 · 启动 0.4 s", live.StatusText);
        Assert.Equal("X-ASR 本地识别 · RTF 0.08 · 精修 0.12 s", live.TranscriptionStatus);
        Assert.Equal(0.08, live.LastRtf);
        Assert.Equal(0.12, live.LastRefineSeconds);
        Assert.Equal(0.9, live.LastFirstTokenSeconds);
        Assert.Equal(4.2, live.LastAnswerSeconds);
        Assert.Equal("gpt-5.5", live.LastAnswerModel);
        Assert.Equal("演示：关键词查找，未调用模型", live.RetrievalNote);

        Assert.Equal(2, live.AnswerHistory.Count);
        Assert.Equal("gpt-5.5-mini", live.AnswerHistory[0].Model);
        Assert.Equal(4.2, live.AnswerHistory[1].Seconds!.Value, 10);
        Assert.Equal(0.9, live.AnswerHistory[1].FirstTokenSeconds!.Value, 10);
        Assert.All(live.AnswerHistory, answer => Assert.True(answer.Automatic));
    }

    [Fact]
    public void LiveAnswerIsTheMacMarkdownReply()
    {
        Assert.Equal(Norm("""
            我们采用**先更新数据库、再删除缓存**，并用 binlog 订阅兜底，保证最终一致。

            - **Cache Aside**：写库成功后删除缓存，读请求未命中时回源并回填，避免并发写导致脏数据。
            - **删除失败兜底**：删除失败的 key 进入重试队列；同时用 **Canal 订阅 binlog** 异步再删一次。
            - **并发读写窗口**：对热点 key 做**延迟双删**（约 500 ms），把旧值被回填的概率降到很低。
            - **取舍**：这是最终一致方案，对强一致场景（如余额）直接读库或加分布式锁。
            """), Norm(DemoScript.LiveAnswer));
    }

    /// <summary>The demo copy is compared as content: the checkout's line endings (CRLF on Windows)
    /// are a platform artifact, the Mac source strings use LF.</summary>
    private static string Norm(string value) => value.Replace("\r\n", "\n");

    // ---- prepare draft ----------------------------------------------------------------

    [Fact]
    public void PrepareDraftMatchesPopulateDemoDraft()
    {
        var draft = Demo().Draft;
        Assert.Equal("字节跳动 · 后端二面", draft.Title);
        Assert.Equal("interview", draft.Mode);
        Assert.Equal(DemoScript.InterviewSeriesName, draft.SeriesName);
        Assert.Equal(new[] { "简历-2026.pdf", "岗位JD-后端开发.md", "八股-缓存与数据库.md", "抖音电商-团队介绍.pdf" },
            draft.BaselineDocumentNames.ToArray());
    }

    // ---- island / bilingual demo copy -------------------------------------------------

    [Fact]
    public void IslandBilingualCopyMatchesSnapshotIsland()
    {
        Assert.Equal("In RQ2 the map is learned without labels. How do you know the learned prior is correct?",
            DemoScript.IslandBilingualQuestion);
        Assert.Contains("> 问：RQ2 没有真实信道作标签，怎么确认学到的先验是对的？", DemoScript.IslandBilingualAnswer);
        Assert.Contains("The prior is not guaranteed to be exactly correct without ground-truth labels.", DemoScript.IslandBilingualAnswer);
        Assert.Contains("> 译：没有真实标签，这个先验不能保证完全正确。", DemoScript.IslandBilingualAnswer);
        Assert.Contains("It stays matched when the installation changes, which a fixed mapping does not.", DemoScript.IslandBilingualAnswer);
        Assert.Equal("没有真实标签，这个先验不能保证完全正确。", DemoScript.IslandBilingualTranslation);
    }

    // ---- language-dependent demo content (Mac String(localized:)) ---------------------

    [Fact]
    public void OnboardingSampleFollowsTheMacInterfaceLanguage()
    {
        var chinese = DemoScript.Onboarding(english: false);
        Assert.Equal("面试示例", chinese.Title);
        Assert.Equal(34d, chinese.Duration);
        Assert.Equal(7, chinese.Lines.Count);
        Assert.Equal(6, chinese.Takeaways.Count);
        Assert.Equal("先聊聊你简历里的检索项目。上周加入重排之后，结果怎么样？", chinese.Lines[0].Text);
        Assert.Equal("已答：Recall@10 从 72% 到 78%，候选数同时变化", chinese.Takeaways[0].Text);
        Assert.Equal("提升来自重排，还是更多候选？", chinese.Suggestion.Question);
        Assert.True(chinese.Lines[1].FromMe);
        Assert.True(chinese.Takeaways[2].IsTodo);

        var english = DemoScript.Onboarding(english: true);
        Assert.Equal("Sample interview", english.Title);
        Assert.Equal("Let's talk about the retrieval project on your resume. How did the results look after you added reranking last week?",
            english.Lines[0].Text);
        Assert.Equal("On this validation set, Recall@10 rose from 72% to 78%, but we also increased the number of candidates.",
            english.Lines[1].Text);
        Assert.Equal("Answered: Recall@10 rose from 72% to 78%, with the candidate count changing too", english.Takeaways[0].Text);
        Assert.Equal("Did reranking or more candidates drive the improvement?", english.Suggestion.Question);
    }

    // ---- no invented demo copy --------------------------------------------------------

    [Fact]
    public void DemoDataDoesNotKeepTheInventedStructuralSampleCopy()
    {
        var root = Root();
        var script = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Platform", "DemoScript.cs"));
        var adapter = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "DemoContent.cs"));
        foreach (var invented in new[] { "结构组会", "连续刚构", "温度补偿", "监测方案 v3", "支座刚度", "鱼骨" })
        {
            Assert.DoesNotContain(invented, script);
            Assert.DoesNotContain(invented, adapter);
        }
        Assert.Contains("字节后端二面", script);
        var state = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "AppState.Demo.cs"));
        Assert.Contains("DemoContent.Create()", state);
        Assert.Contains("LoadDemo", state);
        var legacy = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "AppState.StructuredNotes.cs"));
        Assert.DoesNotContain("StartDemoFeeder", legacy);
        Assert.DoesNotContain("FishboneJson", legacy);
        Assert.DoesNotContain("结构组会", legacy);
        var screenshots = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "SelfTest", "RunScreenshotsAsync.cs"));
        Assert.Contains("DemoContent.Question", screenshots);
    }
}
