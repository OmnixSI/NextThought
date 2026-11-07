namespace NextThought.Core;

public sealed record DocumentBlock(string Location, string Text, int? Page = null, string Kind = "text");
public sealed record MaterialDocument
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public string Extension { get; init; } = "";
    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<DocumentBlock> Blocks { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public override string ToString() => $"{Name} · {Blocks.Count} 段";
}

public sealed record SourceCitation(string Label, string DocumentId, string DocumentName, string Sha256,
    string Location, string Quote, int? Page = null);
