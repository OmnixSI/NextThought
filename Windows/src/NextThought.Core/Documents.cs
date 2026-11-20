namespace NextThought.Core;

public sealed record DocumentContent
{
    public int SchemaVersion { get; init; } = 1;
    public string Pipeline { get; init; } = "system";
    public IReadOnlyList<DocumentBlock> Blocks { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string Status { get; init; } = "complete";
    public Dictionary<string, System.Text.Json.JsonElement>? Components { get; init; }
    /// <summary>Raw page text kept by older writers; absent in newer content files.</summary>
    public IReadOnlyList<string>? RawPages { get; init; }

    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalProperties { get; init; }
}

public sealed record DocumentBlock(string Location, string Text, int? Page = null, string Kind = "text", IReadOnlyList<int>? Box = null);
public sealed record MaterialDocument
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public string Extension { get; init; } = "";
    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Reader { get; init; } = "windows-basic-v1";
    public List<DocumentBlock> Blocks { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    /// <summary>E21: warnings that need the user to look (the quiet page-image note does not).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool NeedsReview => DocumentNotice.Warnings(Warnings).Count > 0;
    public override string ToString() => $"{Name} · {Blocks.Count} 段";
}

public sealed record SourceCitation(string Label, string DocumentId, string DocumentName, string Sha256,
    string Location, string Quote, int? Page = null, string? ChunkId = null)
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalProperties { get; init; }
}
