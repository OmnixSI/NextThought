using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>User-loaded skills (Skill.swift): Markdown guidance injected into answers.</summary>
public partial class AppState
{
    private SkillStore? skillStore;
    private IReadOnlyList<Skill> skills = [];

    public ObservableCollection<SkillRow> SkillRows { get; } = [];

    [ObservableProperty] private string skillsText = "";
    [ObservableProperty] private int skillCount;
    [ObservableProperty] private string skillsStatus = "";

    private void LoadSkills()
    {
        skillStore = new SkillStore(Paths.Root);
        skills = skillStore.Load();
        SkillsText = SkillStore.ToText(skills);
        SkillCount = skills.Count;
        SkillsStatus = skills.Count == 0 ? UiText.T("尚未加载技能。") : UiText.Format("%lld 个技能；回答时按需遵守。", skills.Count);
        RefreshSkillRows();
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
        RefreshSkillRows();
        RefreshSeriesSkills();
        SkillsStatus = UiText.Format("已导入，共 %lld 个技能。", merged.Count);
        SetStatus(SkillsStatus);
    }

    [RelayCommand]
    private void SaveSkills()
    {
        try
        {
            var parsed = SkillStore.ParseText(SkillsText);
            skillStore ??= new SkillStore(Paths.Root);
            parsed = SkillStore.PreserveIdentity(parsed, skills);
            skillStore.Save(parsed);
            skills = parsed;
            RefreshSkillRows();
            SkillCount = parsed.Count;
            SkillsStatus = parsed.Count == 0 ? UiText.T("技能已清空。") : UiText.Format("已保存 %lld 个技能，下一场回答生效。", parsed.Count);
            SetStatus(SkillsStatus);
        }
        catch (Exception ex) { SkillsStatus = "技能保存失败：" + ex.Message; SetStatus(SkillsStatus, true); }
    }

    /// <summary>E28: the Mac SkillSettings empty state.</summary>
    public bool HasSkills => skills.Count > 0;

    private void RefreshSkillRows()
    {
        OnPropertyChanged(nameof(HasSkills));
        SkillRows.Clear();
        foreach (var skill in skills) SkillRows.Add(new SkillRow(skill, PersistSkillRows));
    }

    private void PersistSkillRows()
    {
        skills = SkillRows.Select(row => row.Skill).ToList();
        skillStore ??= new SkillStore(Paths.Root);
        skillStore.Save(skills);
        SkillCount = skills.Count;
        SkillsText = SkillStore.ToText(skills);
        SkillsStatus = UiText.Format("%lld 个技能；已保存启停状态。", skills.Count);
    }

    /// <summary>E28: skills as rows the settings page edits one by one.</summary>
    public IReadOnlyList<Skill> EditableSkills => skills;

    public void SaveSkillEdit(Skill edited)
    {
        var list = skills.ToList();
        var index = list.FindIndex(skill => skill.Id == edited.Id);
        if (index >= 0) list[index] = edited with { ImportedAt = list[index].ImportedAt };
        else list.Add(edited);
        PersistSkills(list, "技能已保存。");
    }

    public void AddSkill()
    {
        var list = skills.ToList();
        // Mac SkillLibrary.create: Skill.parse(Skill.starterTemplate, fileName: "新技能.md"), per-scenario.
        list.Add(new Skill
        {
            Id = Guid.NewGuid().ToString("D"),
            Name = Skill.StarterName,
            Summary = Skill.StarterSummary,
            Content = Skill.StarterContent,
            Enabled = true
        });
        PersistSkills(list, "已新建技能，改完内容后保存。");
    }

    public void DeleteSkill(string id)
    {
        PersistSkills(skills.Where(skill => skill.Id != id).ToList(), "技能已删除。");
    }

    /// <summary>Mac DemoMode installDemo list: the three sample skills, verbatim (E28).</summary>
    /// <summary>§3.6 E28: Mac SkillSettings "N 个系列在用" — how many series attach this skill.</summary>
    public int SeriesUsingSkill(string skillId) =>
        workspace.Series.Count(series => (series.SkillIds ?? []).Any(id =>
            string.Equals(id, skillId, StringComparison.OrdinalIgnoreCase)));

    public void InstallDemoSkill()
    {
        var list = skills.ToList();
        var added = 0;
        foreach (var demo in Skill.DemoSkills)
        {
            if (list.Any(skill => skill.Name == demo.Name)) continue;
            list.Add(demo with { Id = Guid.NewGuid().ToString("D") });
            added++;
        }
        if (added == 0) { SetStatus("示例技能已经在列表里。"); return; }
        PersistSkills(list, "已安装示例技能。");
    }

    private void PersistSkills(IReadOnlyList<Skill> list, string status)
    {
        try
        {
            skillStore ??= new SkillStore(Paths.Root);
            skillStore.Save(list);
            skills = SkillStore.PreserveIdentity(list, skills);
            SkillCount = skills.Count;
            SkillsText = SkillStore.ToText(skills);
            SkillsStatus = status;
            RefreshSkillRows();
            RefreshSeriesSkills();
            SetStatus(status);
        }
        catch (Exception ex) { SkillsStatus = "技能保存失败：" + ex.Message; SetStatus(SkillsStatus, true); }
    }

    /// <summary>Enabled skill instructions for the answer prompt (capped so a huge file cannot crowd out the meeting).</summary>
    public string SkillPrompt(SessionMeta? meta = null)
    {
        var attached = (meta is null ? null : workspace.SeriesOfSession(meta.Id.ToString()))?.SkillIds ?? [];
        var enabled = skills.Where(skill => skill.Enabled
            && (skill.Global || attached.Any(id => Guid.TryParse(id, out var parsed) && parsed == Guid.Parse(skill.Id)))).ToList();
        if (enabled.Count == 0) return "";
        var text = string.Join("\n\n", enabled.Select(skill =>
            "### " + skill.Name + (skill.Summary.Length > 0 ? " — " + skill.Summary : "") + "\n" + skill.Content));
        return text.Length <= 6000 ? text : text[..6000];
    }
}


/// <summary>One editable skill row: checking it enables the skill for answers.</summary>
public sealed partial class SkillRow : ObservableObject
{
    private readonly Action changed;

    public SkillRow(Skill skill, Action changed)
    {
        Skill = skill;
        this.changed = changed;
        isEnabled = skill.Enabled;
    }

    public Skill Skill { get; private set; }
    public string Name => Skill.Name;
    public string Summary => Skill.Summary;

    [ObservableProperty] private bool isEnabled;

    partial void OnIsEnabledChanged(bool value)
    {
        Skill = Skill with { Enabled = value };
        changed();
    }
}
