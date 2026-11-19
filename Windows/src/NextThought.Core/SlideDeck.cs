namespace NextThought.Core;

/// <summary>One navigable slide from an imported PPTX (DocumentReader stores "幻灯片 N" pages).</summary>
public sealed record SlidePageInfo(int Number, string Text, string DocumentName);

/// <summary>Turns the first imported deck into a page list for slide previous/next.</summary>
public static class SlideDecks
{
    public static IReadOnlyList<SlidePageInfo> FromDocuments(IEnumerable<MaterialDocument> documents)
    {
        var deck = documents.FirstOrDefault(document =>
            document.Extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase)
            && document.Blocks.Any(block => block.Page is > 0 || block.Location.StartsWith("幻灯片", StringComparison.Ordinal)));
        if (deck is null) return [];
        return deck.Blocks
            .Select((block, index) => new SlidePageInfo(
                block.Page ?? index + 1,
                block.Text,
                deck.Name))
            .OrderBy(page => page.Number)
            .ToList();
    }
}
