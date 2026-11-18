using System.Text;
using System.Text.Json;

namespace NextThought.Core;

/// <summary>One series-owned extracted document in the materials archive.</summary>
public sealed record WorkspaceArchivedDocument(string DocumentId, string MarkdownPath, string MetadataPath, bool Created);

/// <summary>
/// A series owns its extracted files; originals remain shared by content hash
/// (port of WorkspaceArchiveOwnership adapted to the Windows material model).
/// </summary>
public static class WorkspaceArchiveOwnership
{
    /// <summary>
    /// Ensures every document has <c>Materials/&lt;seriesId&gt;/&lt;documentId&gt;.md</c> and a matching
    /// <c>.json</c>, reusing existing files and never overwriting them.
    /// </summary>
    public static IReadOnlyList<WorkspaceArchivedDocument> Repair(WorkspaceSeries series,
        IReadOnlyList<MaterialDocument> documents, string dataRoot)
    {
        var owner = Path.Combine(Path.GetFullPath(dataRoot), "Materials", SafeName(series.Id));
        var archived = new List<WorkspaceArchivedDocument>();
        foreach (var document in documents)
        {
            var id = SafeName(document.Id);
            var markdown = Path.Combine(owner, id + ".md");
            var metadata = Path.Combine(owner, id + ".json");
            var created = false;
            Directory.CreateDirectory(owner);
            if (!File.Exists(markdown))
            {
                var text = string.Join("\n\n", document.Blocks.Select(block => block.Text?.Trim() ?? "")
                    .Where(text => text.Length > 0));
                WriteAtomic(markdown, Encoding.UTF8.GetBytes(text));
                created = true;
            }
            if (!File.Exists(metadata))
            {
                WriteAtomic(metadata, JsonSerializer.SerializeToUtf8Bytes(document, MaterialDocumentOptions));
                created = true;
            }
            archived.Add(new WorkspaceArchivedDocument(document.Id, markdown, metadata, created));
        }
        return archived;
    }

    public static readonly JsonSerializerOptions MaterialDocumentOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static string SafeName(string value)
    {
        var name = value.Trim();
        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.Contains('/') || name.Contains('\\') || name is "." or "..")
            throw new ArgumentException("文档或系列 id 不能用作文件名。", nameof(value));
        return name;
    }

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, overwrite: false);
    }
}
