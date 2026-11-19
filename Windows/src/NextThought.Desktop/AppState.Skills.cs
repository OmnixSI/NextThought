using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>User-loaded skills (Skill.swift): Markdown guidance injected into answers.</summary>
public partial class AppState
{
    private SkillStore? skillStore;
    private IReadOnlyList<Skill> skills = [];

    [ObservableProperty] private string skillsText = "";
    [ObservableProperty] private int skillCount;
    [ObservableProperty] private string skillsStatus = "";

    private void LoadSkills()
    {
        skillStore = new SkillStore(Paths.Root);
        skills = skillStore.Load();
        SkillsText = SkillStore.ToText(skills);
        SkillCount = skills.Count;
        SkillsStatus = skills.Count == 0 ? "尚未加载技能。" : $"{skills.Count} 个技能；回答时按需遵守。";
    }

    /// <summary>Adds imported .md skills, replacing a skill with the same name.</summary>
    public void AddSkills(IEnumerable<Skill> imported)
    {
        var merged = skills.ToList();
        foreach (var skill in imported)
        {
            var index = merged.FindIndex(item => item.Name.Equals(skill.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) merged[index] = skill with { Id = merged[index].Id };
            else merged.Add(skill);
        }
        skillStore ??= new SkillStore(Paths.Root);
        skillStore.Save(merged);
        skills = merged;
        SkillsText = SkillStore.ToText(merged);
        SkillCount = merged.Count;
        SkillsStatus = $"已导入，共 {merged.Count} 个技能。";
        SetStatus(SkillsStatus);
    }

    [RelayCommand]
    private void SaveSkills()
    {
        try
        {
            var parsed = SkillStore.ParseText(SkillsText);
            skillStore ??= new SkillStore(Paths.Root);
            skillStore.Save(parsed);
            skills = parsed;
            SkillCount = parsed.Count;
            SkillsStatus = parsed.Count == 0 ? "技能已清空。" : $"已保存 {parsed.Count} 个技能，下一场回答生效。";
            SetStatus(SkillsStatus);
        }
        catch (Exception ex) { SkillsStatus = "技能保存失败：" + ex.Message; SetStatus(SkillsStatus, true); }
    }

    /// <summary>Enabled skill instructions for the answer prompt (capped so a huge file cannot crowd out the meeting).</summary>
    public string SkillPrompt()
    {
        var enabled = skills.Where(skill => skill.Enabled).ToList();
        if (enabled.Count == 0) return "";
        var text = string.Join("\n\n", enabled.Select(skill =>
            "### " + skill.Name + (skill.Summary.Length > 0 ? " — " + skill.Summary : "") + "\n" + skill.Content));
        return text.Length <= 6000 ? text : text[..6000];
    }
}
