using System.Text.Json.Serialization;

namespace NextThought.Core;

/// <summary>
/// One accepted decision or action carried by a series (port of WorkspaceSeriesItem). It keeps
/// the evidence anchor of its current revision so a later brief can tell whether it is still current.
/// </summary>
public sealed record WorkspaceSeriesItem
{
    public enum KindValue
    {
        Decision,
        Action
    }

    public enum ActionStateValue
    {
        Open,
        InProgress,
        Done,
        Cancelled,
        Superseded
    }

    public string Id { get; init; } = Guid.NewGuid().ToString("D");
    public KindValue Kind { get; init; }
    public string Text { get; init; } = "";

    [JsonPropertyName("owner")]
    public string? Owner { get; init; }

    [JsonPropertyName("due")]
    public string? Due { get; init; }

    [JsonPropertyName("dueDate")]
    public DateTimeOffset? DueDate { get; init; }

    /// <summary>Nil for decisions; actions begin open and can be updated independently of evidence review.</summary>
    [JsonPropertyName("actionState")]
    public ActionStateValue? ActionState { get; init; }

    [JsonPropertyName("sourceSessionID")]
    public string SourceSessionId { get; init; } = "";

    [JsonPropertyName("evidenceID")]
    public string EvidenceId { get; init; } = "";

    public string ClaimRevision { get; init; } = "";
    public string SourceRevision { get; init; } = "";
    public string? SourceQuote { get; init; }
    public string? Location { get; init; }

    /// <summary>The full transcript snapshot used to verify this accepted revision.</summary>
    public string? TranscriptSourceRevision { get; init; }

    /// <summary>The anchoring meeting was deleted; the text stays for re-confirmation, quote scrubbed.</summary>
    public bool? SourceMissing { get; init; }

    /// <summary>The user explicitly kept this text as independent context.</summary>
    public bool? IndependentlyRetained { get; init; }

    public int Revision { get; init; } = 1;
    public IReadOnlyList<WorkspaceSeriesItemRevision> History { get; init; } = [];
    public DateTimeOffset ConfirmedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public DateTimeOffset EffectiveUpdatedAt => UpdatedAt == default ? ConfirmedAt : UpdatedAt;
}

public sealed record WorkspaceSeriesItemRevision
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
    public int Revision { get; init; } = 1;
    public WorkspaceSeriesItem.KindValue Kind { get; init; }
    public string Text { get; init; } = "";
    public string? Owner { get; init; }
    public string? Due { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public WorkspaceSeriesItem.ActionStateValue? ActionState { get; init; }

    [JsonPropertyName("sourceSessionID")]
    public string SourceSessionId { get; init; } = "";
    [JsonPropertyName("evidenceID")]
    public string EvidenceId { get; init; } = "";
    public string ClaimRevision { get; init; } = "";
    public string SourceRevision { get; init; } = "";
    public string? SourceQuote { get; init; }
    public string? Location { get; init; }
    public string? TranscriptSourceRevision { get; init; }
    public bool? SourceMissing { get; init; }

    [JsonPropertyName("recordedAt")]
    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Unknown Mac revision fields survive round-trips.</summary>
    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalProperties { get; init; }
}
