namespace NextThought.Core;

/// <summary>
/// E20: attach library materials to a series (port of WorkspaceDocumentsTab's import/remove).
/// A series keeps both the embedded <see cref="ContextDocument"/> and the library document id, so
/// an answer can resolve the snippet back to the original library source.
/// </summary>
public static class SeriesMaterials
{
    /// <summary>WorkspaceDocumentsTab files an unrecognised category under 其他.</summary>
    public const string OtherCategory = "其他";

    public static ContextDocument DocumentOf(MaterialDocument material) => new()
    {
        Id = material.Id,
        Name = material.Name,
        Pages = material.Blocks.Select(block => block.Text).ToList(),
        Enabled = true,
        SourceFormat = material.Extension.TrimStart('.').ToUpperInvariant()
    };

    public static bool IsAttached(WorkspaceSeries series, string materialId) =>
        series.DocumentIds.Contains(materialId, StringComparer.OrdinalIgnoreCase)
        || series.Documents.Any(document => string.Equals(document.Id, materialId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Idempotent: importing the same library document twice does not duplicate it.</summary>
    public static WorkspaceSeries Attach(WorkspaceSeries series, MaterialDocument material)
    {
        if (IsAttached(series, material.Id)) return series;
        return series with
        {
            Documents = [.. series.Documents, DocumentOf(material)],
            DocumentIds = [.. series.DocumentIds, material.Id]
        };
    }

    /// <summary>Removes the embedded copy and the library id together (no dangling DocumentIds).</summary>
    public static WorkspaceSeries Detach(WorkspaceSeries series, string documentId) => series with
    {
        Documents = series.Documents
            .Where(document => !string.Equals(document.Id, documentId, StringComparison.OrdinalIgnoreCase)).ToList(),
        DocumentIds = series.DocumentIds
            .Where(id => !string.Equals(id, documentId, StringComparison.OrdinalIgnoreCase)).ToList()
    };

    /// <summary>Documents are grouped by their material slot; a missing slot means 其他.</summary>
    public static string CategoryOf(ContextDocument document) =>
        string.IsNullOrWhiteSpace(document.Category) ? OtherCategory : document.Category;

    /// <summary>Used by the 复盘 tab: how many series materials are actually usable in answers.</summary>
    public static int EnabledCount(WorkspaceSeries series) => series.Documents.Count(document => document.Enabled);
}
