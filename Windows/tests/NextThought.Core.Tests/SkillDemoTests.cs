using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// E28: the skill editor's starter template and the three demo skills are the Mac Skill.swift /
/// DemoMode.swift texts, and imported/new skills are per-scenario by default (Mac global = false).
/// </summary>
public sealed class SkillDemoTests
{
    [Fact]
    public void ANewSkillIsPerScenarioLikeTheMacDefault()
    {
        Assert.False(new Skill().Global);
        var parsed = Skill.Parse("# 标题\n\n正文", "标题.md");
        Assert.False(parsed.Global);
        // Front matter keeps the Mac name/description mapping.
        var withHeader = Skill.Parse("---\nname: 技术讨论表达\ndescription: 结论清楚，依据充分\n---\n正文", "skill.md");
        Assert.Equal("技术讨论表达", withHeader.Name);
        Assert.Equal("结论清楚，依据充分", withHeader.Summary);
        Assert.False(withHeader.Global);
    }

    [Fact]
    public void TheStarterTemplateIsTheMacText()
    {
        Assert.Equal("新技能", Skill.StarterName);
        Assert.Equal("一句话说明这个技能做什么", Skill.StarterSummary);
        Assert.Contains("# 新技能", Skill.StarterContent);
        Assert.Contains("- 回答像我平时说话：先说结论，再讲一个具体例子。", Skill.StarterContent);
        Assert.Contains("- 被问到项目时，按“背景 → 我做了什么 → 结果数字”讲。", Skill.StarterContent);
        Assert.Contains("- 不知道的就说“这块我了解得不深，我的理解是……”，不要硬编。", Skill.StarterContent);
    }

    [Fact]
    public void TheThreeDemoSkillsAreVerbatim()
    {
        var demo = Skill.DemoSkills;
        Assert.Equal(3, demo.Count);
        Assert.Equal(("面试口语化", "回答像真人说话，先结论后例子", "先用一句话回答，再讲一个我做过的具体例子；不用“首先其次最后”。", true),
            (demo[0].Name, demo[0].Summary, demo[0].Content, demo[0].Global));
        Assert.Equal(("系统设计答题框架", "需求 → 估算 → 架构 → 取舍", "系统设计题按 需求澄清、容量估算、核心架构、关键取舍 四步回答。", false),
            (demo[1].Name, demo[1].Summary, demo[1].Content, demo[1].Global));
        Assert.Equal(("学术答辩应对", "承认局限、给出补充实验", "被质疑时先承认问题，再说明已有证据和计划中的补充实验。", false),
            (demo[2].Name, demo[2].Summary, demo[2].Content, demo[2].Global));
        Assert.All(demo, skill => Assert.True(skill.Enabled));
    }
}
