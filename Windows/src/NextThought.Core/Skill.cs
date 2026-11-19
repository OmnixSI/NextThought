using System.Text;

namespace NextThought.Core;

/// <summary>
/// A user skill: Markdown describing how to answer (port of Skill.swift). Optional front matter
/// gives the name and description.
/// </summary>
public sealed record Skill
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Content { get; init; } = "";
    /// <summary>Applies to every scenario.</summary>
    public bool Global { get; init; }
    public bool Enabled { get; init; } = true;
    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Parses a Markdown file; the name falls back to the first heading, then the file name.</summary>
    public static Skill Parse(string text, string fileName)
    {
        var body = text.Replace("\r\n", "\n");
        var name = "";
        var summary = "";
        if (body.StartsWith("---\n", StringComparison.Ordinal))
        {
            var end = body.IndexOf("\n---", 4, StringComparison.Ordinal);
            if (end >= 0)
            {
                var header = body[4..end];
                foreach (var line in header.Split('\n'))
                {
                    var parts = line.Split(':', 2).Select(part => part.Trim()).ToArray();
                    if (parts.Length != 2) continue;
                    var value = parts[1].Trim('"', '\'');
                    switch (parts[0].ToLowerInvariant())
                    {
                        case "name" or "title":
                            name = value;
                            break;
                        case "description" or "summary":
                            summary = value;
                            break;
                    }
                }
                body = body[(end + 4)..].Trim('\n');
            }
        }
        if (name.Length == 0)
        {
            var heading = body.Split('\n').FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal));
            if (heading is not null) name = heading[2..].Trim();
        }
        if (name.Length == 0)
        {
            name = Path.GetFileNameWithoutExtension(fileName);
            if (name.Length == 0 || name.Equals("SKILL", StringComparison.OrdinalIgnoreCase)) name = "未命名技能";
        }
        return new Skill { Name = name, Summary = summary, Content = body.Trim() };
    }

    /// <summary>Skills that apply to a scenario: enabled, and global or attached to it.</summary>
    public static IReadOnlyList<Skill> Active(IReadOnlyList<Skill> library, IReadOnlyCollection<Guid> attached) =>
        library.Where(skill => skill.Enabled && (skill.Global || attached.Contains(skill.Id))).ToList();

    public const string StarterTemplate = """
        ---
        name: 新技能
        description: 一句话说明这个技能做什么
        ---
        # 新技能

        在这里写给模型的要求，例如：
        - 回答像我平时说话：先说结论，再讲一个具体例子。
        - 被问到项目时，按“背景 → 我做了什么 → 结果数字”讲。
        - 不知道的就说“这块我了解得不深，我的理解是……”，不要硬编。
        """;
}
