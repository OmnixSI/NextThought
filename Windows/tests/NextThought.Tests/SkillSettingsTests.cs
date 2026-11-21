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
                     "{loc:Tr 技能}", "{loc:Tr 技能可用于所有系列，或在系列中按系列选择。}",
                     "{loc:Tr 技能是一份 Markdown 说明（例如 SKILL.md）：你希望的说话方式、答题套路、领域知识。打开“所有系列”的技能每次回答都会用；其他技能在系列的“技能”页按系列勾选。}",
                     "{loc:Tr 还没有技能。可以导入现成的 SKILL.md，或点“新建技能”从模板开始。}",
                     "{loc:Tr 导入 .md 文件…}", "{loc:Tr 新建技能}"
                 })
            Assert.Contains(key, xaml);
        Assert.Contains("IsVisible=\"{Binding !HasSkills}\"", xaml);
        // §3.5: the Mac rows are built by the page code; the demo-install entry is Windows-only and gone.
        Assert.Contains("x:Name=\"SkillListPanel\"", xaml);
        Assert.DoesNotContain("{loc:Tr 安装示例技能}", xaml);
        Assert.Contains("Classes=\"primary\" Content=\"{loc:Tr 导入 .md 文件…}\"", xaml);
    }

    [Fact]
    public void TheSkillEditorIsTheMacSheet()
    {
        var window = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SkillEditorWindow.axaml"));
        foreach (var key in new[] { "{loc:Tr 编辑技能}", "{loc:Tr 名称}", "{loc:Tr 一句话说明（可选）}",
                                    "{loc:Tr 所有系列都使用这个技能}", "{loc:Tr 取消}", "{loc:Tr 保存}" })
            Assert.Contains(key, window);
        var windowCode = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SkillEditorWindow.axaml.cs"));
        Assert.Contains("EditAsync", windowCode);
        Assert.Contains("\"%lld 字 · 单个技能最多使用约 6000 字\"", windowCode);
        Assert.Contains("Close(null)", windowCode);
        var code = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs"));
        // Mac SkillSettings rows: icon, summary/N 字, N 个系列在用, toggles saved immediately, edit sheet, delete confirm.
        foreach (var key in new[] { "SkillEditorWindow.EditAsync", "SeriesUsingSkill", "SaveSkillEdit",
                                    "UiText.T(\"编辑\")", "UiText.T(\"删除\")", "UiText.T(\"所有系列\")",
                                    "UiText.Format(\"%lld 个系列在用\"", "UiText.T(\"还没有系列使用\")",
                                    "UiText.Format(\"删除技能“%@”？\", skill.Name)" })
            Assert.Contains(key, code);
        Assert.Contains("ConfirmDialog", code);
        Assert.DoesNotContain("SkillEditorPanel", code);
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
