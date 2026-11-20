using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextThought.Core;

/// <summary>One user-loaded skill (port of Skill.swift): a name, a one-line summary and Markdown.</summary>
public sealed record Skill
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");
    public string Name { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Content { get; init; } = "";
    /// <summary>Mac Skill default: a new/imported skill is per-scenario until 所有系列 is switched on.</summary>
    public bool Global { get; init; }
    public bool Enabled { get; init; } = true;
    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Mac Skill.starterTemplate: the front-matter name/summary and the editable body.</summary>
    public static string StarterName => "新技能";
    public static string StarterSummary => "一句话说明这个技能做什么";
    public static string StarterContent =>
        "# 新技能\n\n在这里写给模型的要求，例如：\n- 回答像我平时说话：先说结论，再讲一个具体例子。\n- 被问到项目时，按“背景 → 我做了什么 → 结果数字”讲。\n- 不知道的就说“这块我了解得不深，我的理解是……”，不要硬编。";

    /// <summary>Mac DemoMode demo skills (installDemo list), verbatim.</summary>
    public static IReadOnlyList<Skill> DemoSkills =>
    [
        new() { Name = "面试口语化", Summary = "回答像真人说话，先结论后例子", Content = "先用一句话回答，再讲一个我做过的具体例子；不用“首先其次最后”。", Global = true, Enabled = true },
        new() { Name = "系统设计答题框架", Summary = "需求 → 估算 → 架构 → 取舍", Content = "系统设计题按 需求澄清、容量估算、核心架构、关键取舍 四步回答。", Enabled = true },
        new() { Name = "学术答辩应对", Summary = "承认局限、给出补充实验", Content = "被质疑时先承认问题，再说明已有证据和计划中的补充实验。", Enabled = true }
    ];

    /// <summary>Parses a Markdown skill with optional YAML front matter and a # heading.</summary>
    public static Skill Parse(string text, string fileName)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var body = normalized;
        string? name = null, summary = null;
        if (normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            var end = normalized.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (end > 0)
            {
                foreach (var line in normalized[4..end].Split('\n'))
                {
                    var separator = line.IndexOf(':');
                    if (separator <= 0) continue;
                    var key = line[..separator].Trim().ToLowerInvariant();
                    var value = line[(separator + 1)..].Trim().Trim('"', '\'');
                    if (key == "name") name = value;
                    else if (key is "description" or "summary") summary = value;
                }
                body = normalized[(end + 4)..].TrimStart('\n');
            }
        }
        var heading = body.Split('\n').FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal));
        if (heading is not null) name ??= heading[2..].Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = Path.GetFileNameWithoutExtension(fileName) is { Length: > 0 } stem && !stem.Equals("skill", StringComparison.OrdinalIgnoreCase)
                ? stem : "未命名技能";
        return new Skill { Name = name, Summary = summary ?? "", Content = body.Trim() };
    }

    /// <summary>Enabled skills that are global or attached to this meeting.</summary>
    public static IReadOnlyList<Skill> Active(IReadOnlyList<Skill> skills, IReadOnlyCollection<string> attachedIDs) =>
        skills.Where(skill => skill.Enabled && (skill.Global || attachedIDs.Contains(skill.Id))).ToList();
}

/// <summary>Reads and writes skills.json (a Mac-compatible array of Skill).</summary>
public sealed class SkillStore
{
    public static readonly JsonSerializerOptions Options = new(JsonDefaults.ArchiveOptions)
    {
        WriteIndented = true
    };

    private readonly string path;

    public SkillStore(string dataDirectory) => path = Path.Combine(dataDirectory, "skills.json");

    public string FilePath => path;

    public IReadOnlyList<Skill> Load()
    {
        try
        {
            if (!File.Exists(path)) return [];
            var skills = JsonSerializer.Deserialize<List<Skill>>(File.ReadAllText(path), Options) ?? [];
            return skills.Where(skill => !string.IsNullOrWhiteSpace(skill.Name)).ToList();
        }
        catch (Exception) { return []; }
    }

    public void Save(IReadOnlyList<Skill> skills)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(skills.Where(skill => !string.IsNullOrWhiteSpace(skill.Name)), Options));
    }

    /// <summary>Human-editable form: "## name", optional "摘要：…", then Markdown until the next "##".</summary>
    /// <summary>A10: a text edit keeps the old id, import time and switches for the same skill.</summary>
    public static IReadOnlyList<Skill> PreserveIdentity(IReadOnlyList<Skill> edited, IReadOnlyList<Skill> existing)
    {
        var result = new List<Skill>();
        var pool = existing.ToList();
        foreach (var skill in edited)
        {
            var match = pool.FirstOrDefault(candidate => string.Equals(candidate.Name, skill.Name, StringComparison.Ordinal))
                        ?? (result.Count < existing.Count ? existing[result.Count] : null);
            if (match is not null)
            {
                pool.Remove(match);
                result.Add(skill with { Id = match.Id, ImportedAt = match.ImportedAt, Global = match.Global, Enabled = match.Enabled });
            }
            else result.Add(skill);
        }
        return result;
    }

    public static IReadOnlyList<Skill> ParseText(string text)
    {
        var skills = new List<Skill>();
        Skill? current = null;
        var content = new List<string>();
        void Flush()
        {
            if (current is null) return;
            var body = string.Join("\n", content).Trim();
            if (body.Length > 0 || current.Summary.Length > 0)
                skills.Add(current with { Content = body });
        }
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush(); current = new Skill { Name = line[3..].Trim() }; content.Clear();
                continue;
            }
            if (current is null) continue;
            if (line.StartsWith("摘要：", StringComparison.Ordinal)) { current = current with { Summary = line[3..].Trim() }; continue; }
            if (line.StartsWith("Summary:", StringComparison.OrdinalIgnoreCase)) { current = current with { Summary = line[8..].Trim() }; continue; }
            content.Add(line);
        }
        Flush();
        return skills;
    }

    public static string ToText(IReadOnlyList<Skill> skills) =>
        string.Join("\n\n", skills.Select(skill =>
            "## " + skill.Name + (skill.Summary.Length > 0 ? "\n摘要：" + skill.Summary : "") +
            (skill.Content.Length > 0 ? "\n" + skill.Content : "")));
}
