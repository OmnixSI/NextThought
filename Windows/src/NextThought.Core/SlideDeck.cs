namespace NextThought.Core;

/// <summary>One navigable slide from an imported PPTX (DocumentReader stores "幻灯片 N" pages).</summary>
public sealed record SlidePageInfo(int Number, string Text, string DocumentName,
    string DocumentId = "", string Notes = "")
{
    /// <summary>Pages in the deck; filled in by SlideDecks (0 when unknown).</summary>
    public int PageCount { get; init; }

    /// <summary>Same as the document id + page (SlidePage.Key).</summary>
    public string Key => (DocumentId.Length > 0 ? DocumentId : DocumentName) + "#" + Number;

    /// <summary>SlidePage.Page name used by the follow rules.</summary>
    public int Page => Number;

    /// <summary>The slide's title: its first line with words in it (mirror of SlidePage.Title).</summary>
    public string Title
    {
        get
        {
            var line = Text.Split('\n')
                .Select(item => item.Trim().Trim('#', '*', '-', ' ', '\t'))
                .FirstOrDefault(item => item.Any(char.IsLetter)) ?? "";
            return line.Length > 48 ? line[..48] : line;
        }
    }
}

/// <summary>Turns the first imported deck into a page list for slide previous/next.</summary>
public static class SlideDecks
{
    public static IReadOnlyList<SlidePageInfo> FromDocuments(IEnumerable<MaterialDocument> documents)
    {
        var deck = documents.FirstOrDefault(document =>
            document.Extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase)
            && document.Blocks.Any(block => block.Page is > 0 || block.Location.StartsWith("幻灯片", StringComparison.Ordinal)));
        if (deck is null) return [];
        // E9: one slide per page; blocks marked as speaker notes stay with their page (Mac
        // SlidePage.Split keeps 演讲备注 apart from what the audience sees).
        var heading = SlidePage.NotesHeading;
        return deck.Blocks
            .GroupBy(block => block.Page ?? 0)
            .Select((group, index) =>
            {
                var shown = group.Where(block => !block.Location.Contains(heading, StringComparison.Ordinal)
                                                 && !block.Text.Contains(heading, StringComparison.Ordinal))
                    .Select(block => block.Text).ToList();
                var notes = group.Where(block => block.Location.Contains(heading, StringComparison.Ordinal)
                                                 || block.Text.Contains(heading, StringComparison.Ordinal))
                    .Select(block => block.Text).ToList();
                return new SlidePageInfo(
                    group.Key > 0 ? group.Key : index + 1,
                    string.Join("\n", shown),
                    deck.Name,
                    deck.Id,
                    string.Join("\n", notes));
            })
            .OrderBy(page => page.Number)
            .ToList() is { Count: > 0 } pages
            ? pages.Select(page => page with { PageCount = pages.Count }).ToList()
            : [];
    }
}
