namespace NextThought.Core;

/// <summary>E24: limits retrieval to the current series' enabled documents.</summary>
public static class RetrievalScope
{
    public static IReadOnlyList<MaterialDocument> ForSeries(
        IReadOnlyList<MaterialDocument> documents,
        IReadOnlyList<string> seriesDocumentIds)
    {
        if (seriesDocumentIds.Count == 0) return documents;
        var allowed = seriesDocumentIds.ToHashSet(StringComparer.Ordinal);
        return documents.Where(document => allowed.Contains(document.Id)).ToList();
    }
}
