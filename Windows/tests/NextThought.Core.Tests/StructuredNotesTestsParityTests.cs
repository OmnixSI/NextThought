using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from StructuredNotesTests.swift: notes formats (fishbone, outline, viewpoint
/// matrix), lenient parsing, the check step, Markdown, prompts and storage.
/// </summary>
public sealed class StructuredNotesTestsParityTests
{
    private static readonly DateTimeOffset When = DateTimeOffset.FromUnixTimeSeconds(1_790_000_000);

    private static StructuredNotesPayload? Clean(StructuredNotesPayload payload, int[]? offsets = null,
        double duration = 600) => StructuredNotesCleaner.Clean(payload, offsets ?? [], duration);

    [Fact(Skip = "missing API: NotesFormats.Resolve (explicit standard override)")]
    public void EveryTypeHasARecommendedFormat()
    {
    }

    [Fact]
    public void UnknownFormatReadsAsStandard()
    {
        Assert.Equal(NotesFormat.Outline, JsonSerializer.Deserialize<NotesFormat>("\"outline\""));
        Assert.Equal(NotesFormat.Standard, JsonSerializer.Deserialize<NotesFormat>("\"mindmap\""));
        var cases = Enum.GetValues<NotesFormat>();
        var all = JsonSerializer.Deserialize<NotesFormat[]>(JsonSerializer.Serialize(cases));
        Assert.Equal(cases, all);
    }

    [Fact]
    public void ParsesFishboneInsideFencesAndTalk()
    {
        var reply = """
            好的，下面是鱼骨图：
            ```json
            {"head": "上线延期", "categories": [
              {"name": "人员", "causes": [{"text": "后端缺人", "time": "02:10"}, "测试排期冲突 [05:30]"]},
              {"name": "流程", "causes": [{"text": "评审太晚", "time": 400}]},
            ]}
            ```
            以上，供参考。
            """;

        var payload = StructuredNotesParser.Parse(reply, NotesFormat.Fishbone);

        Assert.NotNull(payload);
        Assert.Equal(NotesFormat.Fishbone, payload.Format);
        Assert.Equal("zh", payload.Language);
        var fishbone = payload.Fishbone!;
        Assert.Equal("上线延期", fishbone.Head);
        Assert.Equal(["人员", "流程"], fishbone.Categories.Select(category => category.Name));
        Assert.Equal([new TimedText("后端缺人", 130), new TimedText("测试排期冲突", 330)],
            fishbone.Categories[0].Causes.ToArray());
        Assert.Equal([new TimedText("评审太晚", 400)], fishbone.Categories[1].Causes.ToArray());
    }

    [Fact]
    public void TimesComeInManyShapes()
    {
        // StructuredNotesParser.seconds is private on the Windows port; the same shapes reach it
        // through the public parser, which stores what it read on each cause.
        var reply = """
            {"head": "h", "categories": [{"name": "甲", "causes": [
              {"text": "a", "time": "12:41"},
              {"text": "b", "time": "[12:41]"},
              {"text": "c", "time": "1:02:05"},
              {"text": "d", "time": "12:41-13:05"},
              {"text": "e", "time": "761"},
              {"text": "f", "time": 761},
              {"text": "g", "time": "稍后"},
              {"text": "h", "time": null}
            ]}]}
            """;

        var causes = StructuredNotesParser.Parse(reply, NotesFormat.Fishbone)!.Fishbone!.Categories[0].Causes;

        Assert.Equal(new int?[] { 761, 761, 3725, 761, 761, 761, null, null },
            causes.Select(cause => cause.At).ToArray());
    }

    [Fact]
    public void MissingPiecesDegradeInsteadOfFailing()
    {
        // No head: the fallback title. No category name: "其他" / "Other". Plain strings have no time.
        var reply = """
            {"categories": [{"causes": ["资源不足", {"text": "沟通少", "at": "03:00"}]}, {"name": "空的", "causes": []}, "oops"]}
            """;
        var zh = StructuredNotesParser.Parse(reply, NotesFormat.Fishbone, fallbackTitle: "项目复盘");

        Assert.NotNull(zh);
        Assert.Equal("项目复盘", zh.Fishbone!.Head);
        Assert.Single(zh.Fishbone.Categories);
        Assert.Equal("其他", zh.Fishbone.Categories[0].Name);
        Assert.Equal([new TimedText("资源不足", null), new TimedText("沟通少", 180)],
            zh.Fishbone.Categories[0].Causes.ToArray());

        var en = StructuredNotesParser.Parse(reply, NotesFormat.Fishbone, english: true);
        Assert.NotNull(en);
        Assert.Equal("en", en.Language);
        Assert.Equal("Other", en.Fishbone!.Categories[0].Name);

        // A branch written flat (label + detail) becomes a one-cause category.
        var flat = StructuredNotesParser.Parse(
            """{"question": "为什么慢", "branches": [{"label": "数据库", "detail": "缺索引", "t": 90}]}""",
            NotesFormat.Fishbone);
        Assert.NotNull(flat);
        Assert.Equal("为什么慢", flat.Fishbone!.Head);
        Assert.Single(flat.Fishbone.Categories);
        Assert.Equal("数据库", flat.Fishbone.Categories[0].Name);
        Assert.Equal([new TimedText("缺索引", 90)], flat.Fishbone.Categories[0].Causes.ToArray());
    }

    [Fact]
    public void UnreadableRepliesGiveNil()
    {
        Assert.Null(StructuredNotesParser.Parse("抱歉，我无法完成。", NotesFormat.Fishbone));
        Assert.Null(StructuredNotesParser.Parse("{}", NotesFormat.Fishbone));
        Assert.Null(StructuredNotesParser.Parse("""{"head": "x", "categories": []}""", NotesFormat.Fishbone));
        Assert.Null(StructuredNotesParser.Parse("{ 这不是 JSON ", NotesFormat.Fishbone));
        Assert.Null(StructuredNotesParser.Parse("""{"head": "x"}""", NotesFormat.Standard));

        // Braces inside strings do not confuse the scan.
        var tricky = """
            前言 {"head": "含}括号", "categories": [{"name": "甲", "causes": [{"text": "a{b", "time": "00:30"}]}]} 后记 {x}
            """;
        Assert.Equal("含}括号", StructuredNotesParser.Parse(tricky, NotesFormat.Fishbone)?.Fishbone?.Head);
    }

    [Fact]
    public void AWrapperKeyIsUnwrapped()
    {
        var reply = """
            {"fishbone": {"head": "H", "categories": [{"name": "甲", "causes": [{"text": "a", "time": "00:30"}]}]}}
            """;

        var payload = StructuredNotesParser.Parse(reply, NotesFormat.Fishbone);

        Assert.NotNull(payload);
        Assert.Equal("H", payload.Fishbone?.Head);
    }

    [Fact]
    public void ParsesOutlineAndViewpoints()
    {
        var outlinePayload = StructuredNotesParser.Parse("""
            {"sections": [{"title": "缓存", "time": "01:00", "points": [
                {"text": "击穿与穿透", "time": "01:15", "children": [{"text": "击穿是热点失效", "time": "01:30"}, "雪崩是批量失效"]},
                "TTL 的设置 [02:00]"]}],
             "concepts": [{"term": "TTL", "meaning": "过期时间", "time": "02:00"}, {"term": "无含义"}],
             "review": [{"text": "复习布隆过滤器", "time": "30:05"}]}
            """, NotesFormat.Outline);

        Assert.NotNull(outlinePayload);
        var outline = outlinePayload.Outline!;
        var section = outline.Sections[0];
        Assert.Equal("缓存", section.Title);
        Assert.Equal(60, section.At!.Value);
        Assert.Equal(["击穿与穿透", "TTL 的设置"], section.Points.Select(point => point.Text));
        Assert.Equal([new TimedText("击穿是热点失效", 90), new TimedText("雪崩是批量失效", null)],
            section.Points[0].Children.ToArray());
        Assert.Equal(120, section.Points[1].At!.Value);
        Assert.Equal([new OutlineConcept("TTL", "过期时间", 120)], outline.Concepts.ToArray());
        Assert.Equal([new TimedText("复习布隆过滤器", 1805)], outline.Review.ToArray());

        var matrixPayload = StructuredNotesParser.Parse("""
            {"topic": "是否上线", "parties": ["小王", {"name": "小李"}],
             "issues": [{"title": "时间", "positions": [{"party": "小王", "text": "下周上线", "time": "01:40"}, {"party": "小李", "text": "", "time": "02:00"}]}],
             "consensus": [{"text": "先灰度", "time": "03:20"}], "disagreements": ["发布时间 [03:50]"],
             "owners": [{"who": "小王", "task": "写方案", "due": "周四", "time": "03:40"}, {"who": "小李"}]}
            """, NotesFormat.Viewpoints);

        Assert.NotNull(matrixPayload);
        var matrix = matrixPayload.Viewpoints!;
        Assert.Equal("是否上线", matrix.Topic);
        Assert.Equal(["小王", "小李"], matrix.Parties);
        Assert.Single(matrix.Issues);
        Assert.Equal("时间", matrix.Issues[0].Title);
        Assert.Equal([new ViewpointPosition("小王", "下周上线", 100)], matrix.Issues[0].Positions.ToArray());
        Assert.Equal([new TimedText("先灰度", 200)], matrix.Consensus.ToArray());
        Assert.Equal([new TimedText("发布时间", 230)], matrix.Disagreements.ToArray());
        Assert.Equal([new ViewpointOwner("小王", "写方案", "周四", 220)], matrix.Owners.ToArray());
    }

    [Fact]
    public void TheSchemaInThePromptIsReadableByTheParser()
    {
        foreach (var format in new[] { NotesFormat.Fishbone, NotesFormat.Outline, NotesFormat.Viewpoints })
        {
            var payload = StructuredNotesParser.Parse(StructuredNotesPrompt.Schema(format), format);
            Assert.NotNull(payload);
            Assert.True(payload.HasContent);
        }
        Assert.Equal(761,
            StructuredNotesParser.Parse(StructuredNotesPrompt.Schema(NotesFormat.Fishbone), NotesFormat.Fishbone)!
                .Fishbone!.Categories[0].Causes[0].At!.Value);
    }

    [Fact]
    public void UnsupportedNodesAreDropped()
    {
        var raw = new StructuredNotesPayload(NotesFormat.Fishbone, fishbone: new FishboneNotes("上线延期", 5_000,
            [
                new FishboneCategory("人员",
                [
                    new TimedText("后端缺人", 130),
                    new TimedText("后端缺人！", 140), // the same cause again: kept once, not counted as dropped
                    new TimedText("没有时间点的说法", null), // dropped
                    new TimedText("太远的时间点", 5_000) // dropped: after the meeting
                ]),
                new FishboneCategory("人员", [new TimedText("测试排期冲突", 330)]), // same category: merged
                new FishboneCategory("流程", [new TimedText("评审太晚", 400)]),
                new FishboneCategory("空类别", [new TimedText("无依据", 10_000)]) // dropped, and the category with it
            ]), model: "m");

        var cleaned = Clean(raw, [130, 330, 400]);

        Assert.NotNull(cleaned);
        var fishbone = cleaned.Fishbone!;
        Assert.Equal("上线延期", fishbone.Head);
        Assert.Null(fishbone.HeadAt);
        Assert.Equal(["人员", "流程"], fishbone.Categories.Select(category => category.Name));
        Assert.Equal([new TimedText("后端缺人", 130), new TimedText("测试排期冲突", 330)],
            fishbone.Categories[0].Causes.ToArray());
        Assert.Equal([new TimedText("评审太晚", 400)], fishbone.Categories[1].Causes.ToArray());
        Assert.Equal(3, cleaned.DroppedCount);
        Assert.Equal("m", cleaned.Model);
        Assert.Null(cleaned.Failed);
    }

    [Fact]
    public void ATimeFarFromEveryLineHasNoBacking()
    {
        // Same rule as the template notes: within 45 s of a transcript line.
        Assert.True(NotesVerifier.IsSupported(100, [], 50)); // inside duration + 90
        Assert.False(NotesVerifier.IsSupported(200, [], 50));
        Assert.False(NotesVerifier.IsSupported(100, [160], 600)); // 60 s away
        Assert.True(NotesVerifier.IsSupported(100, [160], 600, tolerance: 60));

        var raw = new StructuredNotesPayload(NotesFormat.Fishbone, fishbone: new FishboneNotes("h",
            Categories:
            [
                new FishboneCategory("甲", [new TimedText("a", 130), new TimedText("b", 200), new TimedText("c", 400)])
            ]));
        var cleaned = Clean(raw, [130, 330, 400]);

        Assert.NotNull(cleaned);
        Assert.Equal(["a", "c"], cleaned.Fishbone!.Categories[0].Causes.Select(cause => cause.Text));
        Assert.Equal(1, cleaned.DroppedCount);
    }

    [Fact]
    public void TooLittleLeftMeansNoDiagram()
    {
        var one = new StructuredNotesPayload(NotesFormat.Fishbone, fishbone: new FishboneNotes("h",
            Categories: [new FishboneCategory("甲", [new TimedText("a", 10)])]));
        Assert.Null(Clean(one));

        var none = new StructuredNotesPayload(NotesFormat.Fishbone, fishbone: new FishboneNotes("h",
            Categories: [new FishboneCategory("甲", [new TimedText("a", null), new TimedText("b", null)])]));
        Assert.Null(Clean(none));
        Assert.Null(Clean(new StructuredNotesPayload(NotesFormat.Fishbone)));
        Assert.Null(Clean(new StructuredNotesPayload(NotesFormat.Standard)));
    }

    [Fact]
    public void SizesAreCapped()
    {
        var many = Enumerable.Range(0, 10).Select(index => new TimedText($"原因{index}", index * 10)).ToList();
        var one = new StructuredNotesPayload(NotesFormat.Fishbone, fishbone: new FishboneNotes(new string('长', 100),
            Categories:
            [
                new FishboneCategory("A", many),
                new FishboneCategory("B", [new TimedText("另一个", 50)])
            ]));
        var cleaned = Clean(one);

        Assert.NotNull(cleaned);
        Assert.Equal(StructuredNotesLimits.CausesPerCategory, cleaned.Fishbone!.Categories[0].Causes.Count);
        Assert.Equal(StructuredNotesLimits.HeadCharacters, cleaned.Fishbone.Head.Length);
        Assert.True(cleaned.Fishbone.Head.EndsWith("…", StringComparison.Ordinal));

        var wide = new StructuredNotesPayload(NotesFormat.Fishbone, fishbone: new FishboneNotes("h",
            Categories: Enumerable.Range(0, 9).Select(index => new FishboneCategory($"类{index}",
                [new TimedText($"甲{index}", 10), new TimedText($"乙{index}", 20)])).ToList()));
        var narrowed = Clean(wide);

        Assert.NotNull(narrowed);
        Assert.Equal(StructuredNotesLimits.Categories, narrowed.Fishbone!.Categories.Count);
    }

    [Fact]
    public void OutlineIsCheckedAndBounded()
    {
        var raw = new StructuredNotesPayload(NotesFormat.Outline, outline: new OutlineNotes(
            Sections:
            [
                new OutlineSection("缓存", 99_999,
                [
                    new OutlinePoint("击穿与穿透", 75,
                        [new TimedText("击穿是热点失效", 90), new TimedText("没依据", null)]),
                    new OutlinePoint("没有时间", null)
                ]),
                new OutlineSection("", null, [new OutlinePoint("TTL", 120)]),
                new OutlineSection("全被丢掉", 10, [new OutlinePoint("无依据", 50_000)])
            ],
            Concepts:
            [
                new OutlineConcept("TTL", "过期时间", 120),
                new OutlineConcept("ttl", "重复", 121),
                new OutlineConcept("X", "无依据", null)
            ],
            Review: [new TimedText("复习布隆过滤器", 300)]));

        var cleaned = Clean(raw);

        Assert.NotNull(cleaned);
        var outline = cleaned.Outline!;
        Assert.Equal(2, outline.Sections.Count);
        Assert.Equal("缓存", outline.Sections[0].Title);
        Assert.Equal(75, outline.Sections[0].At!.Value); // the section's own time was unsupported: first point's time
        Assert.Equal(["击穿与穿透"], outline.Sections[0].Points.Select(point => point.Text));
        Assert.Equal([new TimedText("击穿是热点失效", 90)], outline.Sections[0].Points[0].Children.ToArray());
        Assert.Equal("第 2 部分", outline.Sections[1].Title);
        Assert.Equal([new OutlineConcept("TTL", "过期时间", 120)], outline.Concepts.ToArray());
        Assert.Equal([new TimedText("复习布隆过滤器", 300)], outline.Review.ToArray());
        // dropped: 没依据 child, 没有时间 point, 无依据 point, X concept = 4
        Assert.Equal(4, cleaned.DroppedCount);
        Assert.Null(cleaned.Fishbone);
        Assert.Null(cleaned.Viewpoints);

        var big = new OutlineNotes(Sections: Enumerable.Range(0, 14).Select(section =>
            new OutlineSection($"章{section}", null,
                Enumerable.Range(0, 9).Select(point => new OutlinePoint($"点{section}-{point}", 10)).ToList())).ToList());
        var boundedPayload = Clean(new StructuredNotesPayload(NotesFormat.Outline, outline: big));

        Assert.NotNull(boundedPayload);
        var bounded = boundedPayload.Outline!;
        Assert.Equal(StructuredNotesLimits.Sections, bounded.Sections.Count);
        Assert.True(bounded.Sections.All(section => section.Points.Count <= StructuredNotesLimits.PointsPerSection));
        var nodes = bounded.Sections.Sum(section => section.Points.Count);
        Assert.True(nodes <= StructuredNotesLimits.OutlineNodes);
    }

    [Fact]
    public void ViewpointMatrixKeepsOnlySidesThatSpoke()
    {
        var raw = new StructuredNotesPayload(NotesFormat.Viewpoints, viewpoints: new ViewpointMatrix(
            "是否上线",
            Parties: ["小王", "小李", "老张"],
            Issues:
            [
                new ViewpointIssue("时间",
                [
                    new ViewpointPosition("小王", "下周上线", 100),
                    new ViewpointPosition("小李", "再等两周", 160),
                    new ViewpointPosition("小赵", "无所谓", 170) // not in parties: added
                ]),
                new ViewpointIssue("范围", [new ViewpointPosition("小王", "先做核心", null)]) // dropped
            ],
            Consensus: [new TimedText("先灰度", 200)],
            Owners:
            [
                new ViewpointOwner("小王", "写方案", "周四", 220),
                new ViewpointOwner("小李", "没依据", null, null)
            ]));

        var cleaned = Clean(raw);

        Assert.NotNull(cleaned);
        var matrix = cleaned.Viewpoints!;
        Assert.Equal(["小王", "小李", "小赵"], matrix.Parties); // 老张 never spoke: no column
        Assert.Equal(["时间"], matrix.Issues.Select(issue => issue.Title));
        Assert.Equal([new ViewpointPosition("小李", "再等两周", 160)],
            matrix.PositionsOf(matrix.Issues[0], "小李").ToArray());
        Assert.Equal([new TimedText("先灰度", 200)], matrix.Consensus.ToArray());
        Assert.Equal([new ViewpointOwner("小王", "写方案", "周四", 220)], matrix.Owners.ToArray());
        Assert.Equal(2, cleaned.DroppedCount);

        var crowd = new ViewpointMatrix("t",
            Parties: Enumerable.Range(0, 7).Select(index => $"人{index}").ToList(),
            Issues:
            [
                new ViewpointIssue("议题", Enumerable.Range(0, 7)
                    .Select(index => new ViewpointPosition($"人{index}", $"观点{index}", 10)).ToList())
            ]);
        var limitedPayload = Clean(new StructuredNotesPayload(NotesFormat.Viewpoints, viewpoints: crowd));

        Assert.NotNull(limitedPayload);
        var limited = limitedPayload.Viewpoints!;
        Assert.Equal(StructuredNotesLimits.Parties, limited.Parties.Count);
        Assert.Equal(StructuredNotesLimits.Parties, limited.Issues[0].Positions.Count);
    }

    [Fact]
    public void RendersFishboneMarkdown()
    {
        var fishbone = new FishboneNotes("上线延期",
            Categories:
            [
                new FishboneCategory("人员",
                    [new TimedText("后端缺人", 130), new TimedText("测试排期冲突", 330)]),
                new FishboneCategory("流程", [new TimedText("评审太晚", 400)])
            ]);
        var zh = StructuredNotesMarkdown.Render(new StructuredNotesPayload(NotesFormat.Fishbone, fishbone: fishbone));

        Assert.Equal("## 因果鱼骨图：上线延期\n\n### 人员\n- 后端缺人 [02:10]\n- 测试排期冲突 [05:30]\n\n### 流程\n- 评审太晚 [06:40]\n", zh);

        var en = StructuredNotesMarkdown.Render(
            new StructuredNotesPayload(NotesFormat.Fishbone, fishbone: fishbone, language: "en"));
        Assert.True(en.StartsWith("## Cause-and-effect fishbone: 上线延期\n\n### 人员\n- 后端缺人 [02:10]",
            StringComparison.Ordinal));
    }

    [Fact]
    public void RendersOutlineMarkdown()
    {
        var outline = new OutlineNotes(
            Sections:
            [
                new OutlineSection("缓存", 60,
                    [new OutlinePoint("击穿与穿透的区别", 75, [new TimedText("击穿是热点失效", 90)])])
            ],
            Concepts: [new OutlineConcept("TTL", "过期时间", 120)],
            Review: [new TimedText("复习布隆过滤器", 3_725)]);
        var text = StructuredNotesMarkdown.Render(new StructuredNotesPayload(NotesFormat.Outline, outline: outline));

        Assert.Equal("## 要点大纲\n\n### 1. 缓存 [01:00]\n- 击穿与穿透的区别 [01:15]\n  - 击穿是热点失效 [01:30]\n\n### 关键概念\n- **TTL**：过期时间 [02:00]\n\n### 待复习\n- 复习布隆过滤器 [1:02:05]\n", text);
    }

    [Fact]
    public void RendersViewpointMarkdown()
    {
        var matrix = new ViewpointMatrix(
            "是否上线",
            Parties: ["小王", "小李"],
            Issues:
            [
                new ViewpointIssue("时间",
                [
                    new ViewpointPosition("小李", "再等两周", 160),
                    new ViewpointPosition("小王", "下周上线", 100)
                ])
            ],
            Consensus: [new TimedText("先灰度", 200)],
            Disagreements: [new TimedText("发布时间", 230)],
            Owners: [new ViewpointOwner("小王", "写方案", "周四", 220)]);
        var text = StructuredNotesMarkdown.Render(new StructuredNotesPayload(NotesFormat.Viewpoints, viewpoints: matrix));

        // Columns follow the order of `parties`, not the order the sides spoke in.
        Assert.Equal("## 观点对照：是否上线\n\n### 时间\n- **小王**：下周上线 [01:40]\n- **小李**：再等两周 [02:40]\n\n### 共识\n- 先灰度 [03:20]\n\n### 分歧\n- 发布时间 [03:50]\n\n### 分工\n- 小王 · 写方案 · 周四 [03:40]\n", text);
    }

    [Fact]
    public void EmptyOrFailedPayloadsRenderNothing()
    {
        Assert.Equal("", StructuredNotesMarkdown.Render(StructuredNotesPayload.Plain(english: false)));
        Assert.Equal("", StructuredNotesMarkdown.Render(StructuredNotesPayload.Failure(NotesFormat.Fishbone, english: false)));
        Assert.False(StructuredNotesPayload.Failure(NotesFormat.Outline, english: true).HasContent);
        Assert.True(StructuredNotesPayload.Failure(NotesFormat.Outline, english: true).Failed == true);
    }

    [Fact]
    public void PromptsNameTheSchemaInBothLanguages()
    {
        foreach (var format in new[] { NotesFormat.Fishbone, NotesFormat.Outline, NotesFormat.Viewpoints })
        {
            var zh = StructuredNotesPrompt.System(format, english: false);
            var en = StructuredNotesPrompt.System(format, english: true);
            Assert.Contains(StructuredNotesPrompt.Schema(format), zh);
            Assert.Contains(StructuredNotesPrompt.Schema(format), en);
            Assert.Contains("简体中文", zh);
            Assert.Contains("time", zh);
            Assert.Contains(format.Title(false), zh);
            Assert.Contains("English", en);
            Assert.Contains("time", en);
            Assert.Contains(format.Title(true), en);
            Assert.DoesNotContain("简体中文", en);
        }
        Assert.Contains("categories", StructuredNotesPrompt.System(NotesFormat.Fishbone, english: false));
        Assert.Contains("sections", StructuredNotesPrompt.System(NotesFormat.Outline, english: true));
        Assert.Contains("positions", StructuredNotesPrompt.System(NotesFormat.Viewpoints, english: false));
        Assert.Equal("", StructuredNotesPrompt.System(NotesFormat.Standard, english: false));
        Assert.Equal("", StructuredNotesPrompt.Schema(NotesFormat.Standard));
    }

    [Fact]
    public void UserPromptsCarryTranscriptAndParts()
    {
        var user = StructuredNotesPrompt.User("场景：组会", "[00:10] 我：你好");

        Assert.Equal("场景：组会\n\n## 完整转写\n[00:10] 我：你好", user);
        Assert.Equal("## 完整转写\nx", StructuredNotesPrompt.User("", "x"));

        var part = StructuredNotesPrompt.PartialUser("H", "[00:10] 我：你好", 1, 3);
        Assert.Contains("第 2/3 部分", part);
        Assert.Contains("[00:10] 我：你好", part);

        var merge = StructuredNotesPrompt.MergeUser("H", ["{a}", "{b}"], NotesFormat.Outline);
        Assert.Contains("### 第 1 部分\n{a}", merge);
        Assert.Contains("### 第 2 部分\n{b}", merge);
        Assert.Contains("要点大纲", merge);
    }

    [Fact]
    public void OldSummariesWithoutStructuredFieldsStillDecode()
    {
        const string old = """{"title":"周会","markdown":"# 标题\n周会","questions":[],"generatedAt":"2026-10-26T10:00:00Z","model":"m"}""";

        var summary = JsonSerializer.Deserialize<SessionSummary>(old, SessionUserContentStore.Options)!;

        Assert.Equal("周会", summary.Title);
        Assert.Null(summary.Structured);
        Assert.Null(summary.Lessons);
    }

    [Fact]
    public void StructuredPayloadRoundTripsInASummary()
    {
        var payload = new StructuredNotesPayload(NotesFormat.Fishbone,
            fishbone: new FishboneNotes("上线延期", 12,
                [new FishboneCategory("人员", [new TimedText("后端缺人", 130)])]),
            language: "zh", droppedCount: 2, generatedAt: When, model: "m");
        var summary = new SessionSummary
        {
            Title = "组会", Markdown = "# 组会", Questions = [], GeneratedAt = When, Structured = payload
        };
        var data = JsonSerializer.Serialize(summary, SessionUserContentStore.Options);

        var decoded = JsonSerializer.Deserialize<SessionSummary>(data, SessionUserContentStore.Options)!;

        Assert.Equal(summary.Title, decoded.Title);
        Assert.Equal(summary.Markdown, decoded.Markdown);
        Assert.Equal(summary.GeneratedAt, decoded.GeneratedAt);
        Assert.NotNull(decoded.Structured);
        var structured = decoded.Structured!;
        Assert.Equal(NotesFormat.Fishbone, structured.Format);
        Assert.Equal("上线延期", structured.Fishbone!.Head);
        Assert.Equal(12, structured.Fishbone.HeadAt!.Value);
        Assert.Equal("后端缺人", structured.Fishbone.Categories[0].Causes[0].Text);
        Assert.Equal(130, structured.Fishbone.Categories[0].Causes[0].At!.Value);
        Assert.Equal(2, structured.DroppedCount);
        Assert.Equal("m", structured.Model);

        // A summary without it encodes without the key, so older versions read the file unchanged.
        var plain = JsonSerializer.Serialize(
            new SessionSummary { Title = "t", Markdown = "m", Questions = [], GeneratedAt = When },
            SessionUserContentStore.Options);
        Assert.DoesNotContain("structured", plain);
    }

    [Fact(Skip = "missing API: SessionSummary tolerant structured-notes decoding")]
    public void DamagedOrNewerStructuredDataDoesNotBreakTheSummary()
    {
    }

    [Fact(Skip = "missing API: SessionSummary.preserveStructuredViews(from:)")]
    public void TextRewriteKeepsDiagramsCompletedAfterItsSnapshot()
    {
    }

    [Fact(Skip = "missing API: SessionSummary.preserveStructuredViews(from:)")]
    public void TextRewritePreservesLegacySingleView()
    {
    }

    [Fact(Skip = "missing API: SessionSummary.payload(for:) / storedFormats")]
    public void OldSummaryWithOnlyStructuredStillAnswersForItsFormat()
    {
    }

    [Fact(Skip = "missing API: SessionSummary.store(_:) / payload(for:) / storedFormats")]
    public void StoringKeepsOneViewPerFormatAndNewerReplacesOlder()
    {
    }

    [Fact(Skip = "missing API: SessionSummary.store(_:) / payload(for:)")]
    public void ASingleViewFromAnOlderVersionIsKeptWhenAnotherIsStored()
    {
    }

    [Fact(Skip = "missing API: SessionSummary.store(_:) / payload(for:) / storedFormats")]
    public void FailuresAndThePlainFormatAreNeverStored()
    {
    }

    [Fact]
    public void SeveralViewsRoundTripAndOlderReadersIgnoreThem()
    {
        var fishbone = new StructuredNotesPayload(NotesFormat.Fishbone,
            fishbone: new FishboneNotes("上线延期",
                Categories: [new FishboneCategory("人员", [new TimedText("后端缺人", 130)])]),
            generatedAt: When, model: "m");
        var outline = new StructuredNotesPayload(NotesFormat.Outline,
            outline: new OutlineNotes(Sections: [new OutlineSection("导数", 5, [new OutlinePoint("定义", 6)])]),
            generatedAt: When, model: "m");
        var summary = new SessionSummary
        {
            Title = "t", Markdown = "m", Questions = [], GeneratedAt = When,
            // What SessionSummary.store keeps for two views: one entry per format, in save order.
            StructuredAll = [fishbone, outline]
        };
        var data = JsonSerializer.Serialize(summary, SessionUserContentStore.Options);

        var decoded = JsonSerializer.Deserialize<SessionSummary>(data, SessionUserContentStore.Options)!;

        Assert.Equal(summary.Title, decoded.Title);
        Assert.Equal(summary.Markdown, decoded.Markdown);
        Assert.Equal(summary.GeneratedAt, decoded.GeneratedAt);
        var stored = decoded.StructuredAll!;
        Assert.Equal([NotesFormat.Fishbone, NotesFormat.Outline],
            stored.Select(payload => payload.Format));
        Assert.Equal("上线延期", stored[0].Fishbone!.Head);
        Assert.Equal("导数", stored[1].Outline!.Sections[0].Title);

        // Nothing saved: the key is not written, so files stay as they were.
        var plain = JsonSerializer.Serialize(
            new SessionSummary { Title = "t", Markdown = "m", Questions = [], GeneratedAt = When },
            SessionUserContentStore.Options);
        Assert.DoesNotContain("structuredAll", plain);
    }
}
