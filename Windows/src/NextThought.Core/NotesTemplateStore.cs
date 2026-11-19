using System.Text.Json;

namespace NextThought.Core;

/// <summary>Reads/writes note-templates.json (Mac's custom notes templates).</summary>
public sealed class NotesTemplateStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
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

    public void Save(IReadOnlyList<NotesTemplate> templates)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(templates, Options));
    }
}
