using System.Text.Json;

namespace NextThought.Core;

/// <summary>Reads/writes note-templates.json (Mac's custom notes templates).</summary>
public sealed class NotesTemplateStore
{
    public static readonly JsonSerializerOptions Options = new(JsonDefaults.ArchiveOptions)
    {
        WriteIndented = true
    };

    private readonly string path;

    public NotesTemplateStore(string dataDirectory) => path = Path.Combine(dataDirectory, "note-templates.json");

    public IReadOnlyList<NotesTemplate> Load()
    {
        try
        {
            if (!File.Exists(path)) return [];
            var templates = JsonSerializer.Deserialize<List<NotesTemplate>>(File.ReadAllText(path), Options) ?? [];
            return templates.Where(template => !string.IsNullOrWhiteSpace(template.Id) && !string.IsNullOrWhiteSpace(template.Name)
                && template.Sections.Count > 0).ToList();
        }
        catch (Exception) { return []; }
    }

    /// <summary>Editable form: "## template", "### section", following lines are the requirement.</summary>
    public static IReadOnlyList<NotesTemplate> ParseText(string text)
    {
        var templates = new List<NotesTemplate>();
        string? name = null;
        var sections = new List<NotesSectionSpec>();
        string? sectionTitle = null;
        var instructions = new List<string>();
        void FlushSection()
        {
            if (sectionTitle is null) return;
            var instruction = string.Join(" ", instructions).Trim();
            if (instruction.Length > 0)
                sections.Add(new NotesSectionSpec("section-" + (sections.Count + 1), sectionTitle, instruction));
            sectionTitle = null; instructions.Clear();
        }
        void FlushTemplate()
        {
            FlushSection();
            if (name is not null && sections.Count > 0)
                templates.Add(new NotesTemplate("custom-" + Guid.NewGuid().ToString("N"), name, sections.ToList()));
            name = null; sections.Clear();
        }
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("## ", StringComparison.Ordinal) && !line.StartsWith("### ", StringComparison.Ordinal))
            {
                FlushTemplate(); name = line[3..].Trim(); continue;
            }
            if (line.StartsWith("### ", StringComparison.Ordinal) && name is not null)
            {
                FlushSection(); sectionTitle = line[4..].Trim(); continue;
            }
            if (sectionTitle is not null && line.Trim().Length > 0) instructions.Add(line.Trim());
        }
        FlushTemplate();
        return templates;
    }

    public static string ToText(IReadOnlyList<NotesTemplate> templates) =>
        string.Join("\n\n", templates.Select(template =>
            "## " + template.Name + "\n" + string.Join("\n", template.Sections.Select(section =>
                "### " + section.Title + "\n" + section.Instruction))));

    /// <summary>A10: editing the Markdown keeps the template id/base and each section's extra fields.</summary>
    public static IReadOnlyList<NotesTemplate> PreserveIdentity(
        IReadOnlyList<NotesTemplate> edited, IReadOnlyList<NotesTemplate> existing)
    {
        var result = new List<NotesTemplate>();
        foreach (var template in edited)
        {
            var match = existing.FirstOrDefault(candidate =>
                candidate.Id == template.Id || string.Equals(candidate.Name, template.Name, StringComparison.Ordinal));
            if (match is null) { result.Add(template); continue; }
            var sections = template.Sections.Select((section, index) => index < match.Sections.Count
                ? section with
                {
                    Id = match.Sections[index].Id,
                    Kind = match.Sections[index].Kind,
                    IsGap = match.Sections[index].IsGap,
                    Carry = match.Sections[index].Carry,
                    CarryPrefix = match.Sections[index].CarryPrefix
                }
                : section).ToList();
            result.Add(template with { Id = match.Id, Base = match.Base, Sections = sections });
        }
        return result;
    }

    public void Save(IReadOnlyList<NotesTemplate> templates)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(templates, Options));
    }
}
