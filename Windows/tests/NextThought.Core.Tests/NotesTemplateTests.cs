using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the ports of NotesTemplates.swift / MeetingType.swift (templates, parse, markdown, prompt).</summary>
public sealed class NotesTemplateTests
{
    [Fact]
    public void EverySceneHasATemplateAndTheBuiltinsHaveSections()
    {
        foreach (var mode in Enum.GetValues<MeetingMode>())
        {
            var kind = MeetingTypeDefaults.TemplateFor(mode);
            var template = NotesTemplate.BuiltIn(kind);
            Assert.NotEmpty(template.Sections);
            Assert.True(template.IsBuiltIn);
        }
        Assert.Equal(NotesTemplateKind.Interview, MeetingTypeDefaults.TemplateFor(MeetingMode.Interview));
        Assert.Equal(NotesTemplateKind.Talk, MeetingTypeDefaults.TemplateFor(MeetingMode.Academic));
        Assert.Equal(NotesTemplateKind.Academic, MeetingTypeDefaults.TemplateFor(MeetingMode.LabMeeting));
        Assert.Equal(NotesTemplateKind.Lecture, MeetingTypeDefaults.TemplateFor(MeetingMode.Lesson));
    }

    [Fact]
    public void InterviewTemplateCarriesQuestionsAndLessons()
    {
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.Interview);
        Assert.Equal(6, template.Sections.Count);
        Assert.Equal(NotesSectionKind.OneLine, template.Sections[0].Kind);
        var questions = template.Sections.First(section => section.Id == "questions");
        Assert.Equal(NotesSectionKind.Questions, questions.Kind);
        Assert.Equal(NotesCarry.Bank, questions.Carry);
        Assert.Contains(template.Sections, section => section.IsGap);
        Assert.Contains(template.Sections, section => section.Carry == NotesCarry.Lesson);
    }

    [Fact]
    public void ParsesSectionsByIdTitleAndPosition()
    {
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.Work);
        var reply = """
            Here you go:
            ```json
            {"title": "周会","sections": [
              {"id": "summary", "items": [{"text": "定了上线顺序", "time": "00:40"}]},
              {"title": "决定", "items": ["先上灰度"]},
              {"items": [{"text": "小王写回滚脚本", "owner": "小王", "due": "周四", "time": "12:41"}]}
            ]}
            ```
            """;
        var doc = NotesJson.Parse(reply, template)!;
        Assert.NotNull(doc);
        Assert.Equal("周会", doc.Title);
        Assert.Equal("定了上线顺序", doc.Sections[0].Items[0].Text);
        Assert.Equal("先上灰度", doc.Sections[1].Items[0].Text);
        var actions = doc.Sections.First(section => section.Id == "actions");
        Assert.Equal("小王", actions.Items[0].Owner);
        Assert.Equal("周四", actions.Items[0].Due);
        Assert.Equal("12:41", actions.Items[0].Time);
    }

    [Fact]
    public void TidyCapsDropsRepeatsAndOneLineKeepsOne()
    {
        var items = new[]
        {
            new NotesItem { Text = "缓存过期！" },
            new NotesItem { Text = "缓存过期" },
            new NotesItem { Text = "   " }
        };
        Assert.Single(NotesJson.Tidy(items, NotesSectionKind.Items, false));
        var many = Enumerable.Range(0, 20).Select(index => new NotesItem { Text = "第 " + index + " 条" });
        Assert.Equal(12, NotesJson.Tidy(many, NotesSectionKind.Items, false).Count);
        Assert.Equal(5, NotesJson.Tidy(many, NotesSectionKind.Items, true).Count);
        Assert.Single(NotesJson.Tidy(many, NotesSectionKind.OneLine, false));
    }

    [Fact]
    public void VerdictsRenderWithTheBetterAnswer()
    {
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.Interview);
        var reply = """
            {"title":"一面","sections":[{"id":"questions","items":[
              {"text":"为什么选这个岗位","answer":"因为项目经验","verdict":"improve","better":"先说岗位要求，再说自己对应的经历","time":"03:10"}]}]}
            """;
        var doc = NotesJson.Parse(reply, template)!;
        var markdown = NotesMarkdown.Render(doc);
        Assert.Contains("### 逐题回放", markdown);
        Assert.Contains("- 为什么选这个岗位 [03:10]", markdown);
        Assert.Contains("更好的答法：先说岗位要求", markdown);
    }

    [Fact]
    public void MarkdownSkipsEmptySectionsAndRendersOwners()
    {
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.GroupProject);
        var doc = NotesJson.Parse("""
            {"sections":[{"id":"tasks","items":[{"text":"写综述","owner":"我","due":"下周一"}]}]}
            """, template)!;
        var markdown = NotesMarkdown.Render(doc);
        Assert.Contains("### 分工 · 谁 · 哪天交", markdown);
        Assert.Contains("- 写综述 · 我 · 下周一", markdown);
        Assert.DoesNotContain("### 定下来的方案", markdown);
    }

    [Fact]
    public void EmptyOrBrokenRepliesReturnNull()
    {
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.General);
        Assert.Null(NotesJson.Parse("没有 JSON", template));
        // Nothing usable came back: the caller keeps the plain summary instead.
        Assert.Null(NotesJson.Parse("""{"title":"x"}""", template));
    }

    [Fact]
    public void OrganizePromptListsSectionsAndSchema()
    {
        var prompt = NotesPrompt.OrganizeSystem(NotesTemplate.BuiltIn(NotesTemplateKind.Client));
        Assert.Contains("客户沟通", prompt);
        Assert.Contains("id \"promises\"", prompt);
        Assert.Contains("owner", prompt);
        Assert.Contains("\"sections\"", prompt);
        Assert.Contains("只输出一个 JSON 对象", prompt);
        var inPerson = NotesPrompt.OrganizeSystem(NotesTemplate.BuiltIn(NotesTemplateKind.Work), inPerson: true);
        Assert.Contains("线下会议", inPerson);
    }


    [Fact]
    public void CustomTemplateTextRoundTripsSections()
    {
        var parsed = NotesTemplateStore.ParseText("## 客户会\n### 决定\n只写已经拍板的事。\n### 待办\n写清负责人和日期。");
        var template = Assert.Single(parsed);
        Assert.Equal("客户会", template.Name);
        Assert.Equal(2, template.Sections.Count);
        Assert.Equal("决定", template.Sections[0].Title);
        Assert.Equal("写清负责人和日期。", template.Sections[1].Instruction);
        Assert.Contains("## 客户会", NotesTemplateStore.ToText(parsed));
        Assert.Contains("### 待办", NotesTemplateStore.ToText(parsed));
    }

    [Fact]
    public void CustomTemplateStoreRoundTrips()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nt-template-" + Guid.NewGuid().ToString("N"));
        try
        {
            var template = new NotesTemplate("custom-client", "客户会", [
                new NotesSectionSpec("decisions", "决定", "只写已经拍板的事。"),
                new NotesSectionSpec("actions", "待办", "写清负责人和日期。", NotesSectionKind.Actions, Carry: NotesCarry.Lesson)
            ]);
            var store = new NotesTemplateStore(directory);
            store.Save([template]);
            var loaded = Assert.Single(store.Load());
            Assert.Equal("客户会", loaded.Name);
            Assert.Equal(2, loaded.Sections.Count);
            Assert.Equal(NotesCarry.Lesson, loaded.Sections[1].Carry);
            Assert.False(loaded.IsBuiltIn);
        }
        finally { try { Directory.Delete(directory, true); } catch { } }
    }

    [Fact]
    public void EnglishBuiltInsMirrorTheChineseStructure()
    {
        foreach (var kind in Enum.GetValues<NotesTemplateKind>())
        {
            var chinese = NotesTemplate.BuiltIn(kind);
            var english = NotesTemplate.BuiltIn(kind, english: true);
            Assert.Equal(chinese.Sections.Select(section => section.Id), english.Sections.Select(section => section.Id));
            Assert.Equal(chinese.Sections.Select(section => section.Kind), english.Sections.Select(section => section.Kind));
            Assert.Equal(chinese.Sections.Select(section => section.Carry), english.Sections.Select(section => section.Carry));
            Assert.All(english.Sections, section =>
            {
                Assert.DoesNotContain("。", section.Instruction);
            });
        }
        Assert.Equal("Interview review", NotesTemplate.BuiltIn(NotesTemplateKind.Interview, english: true).Name);
        Assert.Equal("Next round", NotesTemplate.BuiltIn(NotesTemplateKind.Interview, english: true).Sections[4].CarryPrefix);
        Assert.Equal("To do", NotesTemplate.BuiltIn(NotesTemplateKind.Work, english: true).Sections[2].CarryPrefix);
    }
}
