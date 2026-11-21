using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Port of SkillTests.swift. The Mac <c>Workspace</c> type maps to <see cref="WorkspaceSeries"/> on
/// Windows (it is the record that carries <c>skillIDs</c>); everything else is the same API.
/// </summary>
public sealed class SkillParityTests
{
    [Fact]
    public void ReadsFrontMatter()
    {
        const string text = "---\nname: 面试口语化\ndescription: \"回答像真人说话\"\n---\n\n# 标题\n先说结论。\n";
        var skill = Skill.Parse(text, "SKILL.md");
        Assert.Equal("面试口语化", skill.Name);
        Assert.Equal("回答像真人说话", skill.Summary);
        Assert.Equal("# 标题\n先说结论。", skill.Content);
    }

    [Fact]
    public void FallsBackToHeadingThenFileName()
    {
        Assert.Equal("学术答辩", Skill.Parse("# 学术答辩\n承认局限。", "x.md").Name);
        Assert.Equal("商务谈判", Skill.Parse("只有正文", "商务谈判.md").Name);
        Assert.Equal("未命名技能", Skill.Parse("只有正文", "SKILL.md").Name);
    }

    [Fact]
    public void ActiveSkillsAreGlobalOrAttached()
    {
        // The Swift initializer defaults `global` to false; the C# record defaults Global to true,
        // so the same inputs have to say it explicitly.
        var global = new Skill { Name = "g", Content = "a", Global = true };
        var attached = new Skill { Name = "a", Content = "b", Global = false };
        var other = new Skill { Name = "o", Content = "c", Global = false };
        var disabled = new Skill { Name = "d", Content = "d", Global = true, Enabled = false };
        var active = Skill.Active([global, attached, other, disabled], [attached.Id]);
        Assert.Equal(["g", "a"], active.Select(skill => skill.Name));
    }

    [Fact]
    public void WorkspaceKeepsSkillsAndOldFilesStillLoad()
    {
        var id = Guid.NewGuid().ToString("D");
        var workspace = new WorkspaceSeries(Guid.NewGuid().ToString("D"), "w", DateTimeOffset.UnixEpoch, [], [])
        {
            Mode = "interview",
            SkillIds = [id]
        };
        var data = JsonSerializer.Serialize(workspace, WorkspaceStore.Options);
        var decoded = JsonSerializer.Deserialize<WorkspaceSeries>(data, WorkspaceStore.Options)!;
        Assert.Equal(id, Assert.Single(decoded.SkillIds!));

        var old = $$"""{"id":"{{Guid.NewGuid():D}}","name":"旧场景","mode":"interview"}""";
        var loaded = JsonSerializer.Deserialize<WorkspaceSeries>(old, WorkspaceStore.Options)!;
        Assert.Equal("旧场景", loaded.Name);
        Assert.Empty(loaded.SkillIds ?? []);
    }
}
