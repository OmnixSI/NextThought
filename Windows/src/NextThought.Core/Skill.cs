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
    public bool Global { get; init; } = true;
    public bool Enabled { get; init; } = true;
    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.UtcNow;

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
