using System.Text.Json;

namespace NextThought.Core;

/// <summary>
/// E21: material-review.json — one review note per library material, kept beside the data so the
/// review sheet remembers what was checked (ProjectLibrarySupport.swift MaterialReviewSheet).
/// </summary>
public static class MaterialReviewNotes
{
    public const string FileName = "material-review.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string PathOf(string dataRoot) => Path.Combine(dataRoot, FileName);

    public static IReadOnlyDictionary<string, string> Load(string dataRoot)
    {
        var path = PathOf(dataRoot);
        if (!File.Exists(path)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), Options)
                   ?? new Dictionary<string, string>();
        }
        catch (JsonException) { return new Dictionary<string, string>(); }
    }

    public static string NoteFor(string dataRoot, string materialId) =>
        Load(dataRoot).TryGetValue(materialId, out var note) ? note : "";

    public static void Save(string dataRoot, string materialId, string note)
    {
        var map = Load(dataRoot).ToDictionary(pair => pair.Key, pair => pair.Value);
        if (string.IsNullOrWhiteSpace(note)) map.Remove(materialId);
        else map[materialId] = note.Trim();
        Directory.CreateDirectory(dataRoot);
        File.WriteAllText(PathOf(dataRoot), JsonSerializer.Serialize(map, Options));
    }
}

/// <summary>
/// E21: the extracted text shown beside the original file. Only the first 80,000 characters are
/// inlined; the full extraction stays on disk and the sheet says so.
/// </summary>
public static class MaterialReviewPreview
{
    public const int PreviewLimit = 80_000;

    public static (string Text, bool Truncated) Build(IReadOnlyList<DocumentBlock> blocks, int limit = PreviewLimit)
    {
        var text = string.Join("\n\n", blocks.Select(block =>
            block.Location.Length > 0 ? "[" + block.Location + "]\n" + block.Text : block.Text));
        return text.Length <= limit ? (text, false) : (text[..limit], true);
    }
}
