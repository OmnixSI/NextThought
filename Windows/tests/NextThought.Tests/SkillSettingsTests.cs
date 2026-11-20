using Xunit;

namespace NextThought.Tests;

/// <summary>E28: the settings skill card follows Mac SkillSettings/SkillEditor (copy, starter
/// template, demo skills, delete confirmation).</summary>
public sealed class SkillSettingsTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative));

    [Fact]
    public void TheSkillCardUsesTheMacCopyAndActions()
    {
        var xaml = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        foreach (var key in new[]
                 {
                     "{loc:Tr 技能}", "{loc:Tr 技能可用于所有系列，或在资料夹中按系列选择。}",
                     "{loc:Tr 技能是一份 Markdown 说明（例如 SKILL.md）：你希望的说话方式、答题套路、领域知识。打开“所有系列”的技能每次回答都会用；其他技能在资料夹的“技能”页按系列勾选。}",
                     "{loc:Tr 还没有技能。可以导入现成的 SKILL.md，或点“新建技能”从模板开始。}",
                     "{loc:Tr 导入 .md 文件…}", "{loc:Tr 新建技能}"
                 })
            Assert.Contains(key, xaml);
        Assert.Contains("IsVisible=\"{Binding !HasSkills}\"", xaml);
    }

    [Fact]
    public void TheEditorUsesTheMacLabelsAndConfirmsDelete()
    {
        var code = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs"));
        foreach (var key in new[]
                 {
                     "UiText.T(\"名称\")", "UiText.T(\"一句话说明（可选）\")", "UiText.T(\"所有系列都使用这个技能\")",
                     "UiText.T(\"启用\")", "UiText.T(\"保存\")", "UiText.T(\"删除\")",
                     "\"%lld 字 · 单个技能最多使用约 6000 字\""
                 })
            Assert.Contains(key, code);
        Assert.Contains("UiText.Format(\"删除技能“%@”？\", skill.Name)", code);
        Assert.Contains("ConfirmDialog", code);
    }

    [Fact]
    public void TheStarterTemplateAndDemoSkillsComeFromCore()
    {
        var skills = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.Skills.cs"));
        Assert.Contains("Name = Skill.StarterName", skills);
        Assert.Contains("Summary = Skill.StarterSummary", skills);
        Assert.Contains("Content = Skill.StarterContent", skills);
        Assert.Contains("Skill.DemoSkills", skills);
        Assert.Contains("demo with { Id = Guid.NewGuid()", skills);
        Assert.Contains("HasSkills", skills);
    }

    [Fact]
    public void TheDemoModeSkillsKeepTheMacGlobalFlags()
    {
        var demo = Read(Path.Combine("Windows", "src", "NextThought.Platform", "DemoScript.cs"));
        var design = demo.IndexOf("系统设计答题框架", StringComparison.Ordinal);
        var rebuttal = demo.IndexOf("学术答辩应对", StringComparison.Ordinal);
        Assert.True(design > 0 && rebuttal > design);
        var spokenGlobal = demo.IndexOf("Global = true", StringComparison.Ordinal);
        var designGlobal = demo.IndexOf("Global = false", demo.IndexOf("Name = \"系统设计答题框架\"", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.True(spokenGlobal > 0);
        Assert.True(designGlobal > design);
    }
}
