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

    public void Save(IReadOnlyList<NotesTemplate> templates)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(templates, Options));
    }
}
