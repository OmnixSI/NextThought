using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextThought.Core;

/// <summary>A generated meeting summary; user edits keep their own copy (port of SessionSummary fields used here).</summary>
public sealed record SessionSummary
{
    public string Title { get; init; } = "";
    public string Markdown { get; init; } = "";
    public string Model { get; init; } = "";
    public string? TranscriptSourceRevision { get; init; }
    public string? StructuredNotesSourceRevision { get; init; }
    public StructuredNotesPayload? Structured { get; init; }
    public IReadOnlyList<SummaryQuestion>? Questions { get; init; }
    public IReadOnlyList<string>? Lessons { get; init; }
    public IReadOnlyList<BankEntry>? Bank { get; init; }

    /// <summary>Mac summary.notes: the generated template document the notes editor shows.</summary>
    public StructuredNotesDoc? Notes { get; init; }
    public DateTimeOffset? GeneratedAt { get; init; }

    /// <summary>All structured views saved for this meeting (structuredAll).</summary>
    public IReadOnlyList<StructuredNotesPayload>? StructuredAll { get; init; }

    /// <summary>User reviews keyed by evidence id (evidenceReviews).</summary>
    public IReadOnlyDictionary<string, SessionEvidenceReview>? EvidenceReviews { get; init; }

    /// <summary>A text rewrite keeps the latest saved diagrams, including results completed while it ran.</summary>
    public SessionSummary PreserveStructuredViews(SessionSummary? latest) => this with
    {
        Structured = latest?.Structured,
        StructuredAll = latest?.StructuredAll,
        EvidenceReviews = latest?.EvidenceReviews ?? EvidenceReviews
    };

    /// <summary>The saved view of a format that has something to draw (plain/failure never count).</summary>
    public StructuredNotesPayload? Payload(NotesFormat format)
    {
        if (!format.IsStructured()) return null;
        var found = StructuredAll?.FirstOrDefault(payload => payload.Format == format && payload.HasContent);
        if (found is not null) return found;
        return Structured is { } single && single.Format == format && single.HasContent ? single : null;
    }

    /// <summary>The formats that have a saved view, in the order of the notes formats.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<NotesFormat> StoredFormats =>
        new[] { NotesFormat.Fishbone, NotesFormat.Outline, NotesFormat.Viewpoints, NotesFormat.Standard }
            .Where(format => Payload(format) is not null).ToList();

    /// <summary>Saves a view: newer same-format replaces in place, older single joins first; empty is ignored.</summary>
    public SessionSummary Store(StructuredNotesPayload payload)
    {
        if (!payload.Format.IsStructured() || !payload.HasContent) return this;
        var all = (StructuredAll ?? []).ToList();
        if (Structured is { } single && single.Format.IsStructured() && single.HasContent
            && !all.Any(existing => existing.Format == single.Format))
            all.Add(single);
        var index = all.FindIndex(existing => existing.Format == payload.Format);
        if (index >= 0) all[index] = payload; else all.Add(payload);
        return this with { StructuredAll = all, Structured = payload };
    }

    /// <summary>
    /// Mac's SessionSummary also carries title/questions/model. Keep every unknown field so a
    /// Mac-authored editedSummary survives a Windows read/write cycle.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Missing status in an old answer means it finished; unknown future statuses stay incomplete.</summary>
public enum AnswerCompletion
{
    Complete,
    Interrupted,
    Failed
}
    /// <summary>Old missing status means complete; unknown future statuses stay interrupted.</summary>
public sealed class AnswerCompletionJsonConverter : System.Text.Json.Serialization.JsonConverter<AnswerCompletion>
{
    public override AnswerCompletion Read(ref System.Text.Json.Utf8JsonReader reader, Type type, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType != System.Text.Json.JsonTokenType.String) return AnswerCompletion.Interrupted;
        return reader.GetString() switch
        {
            "complete" or null => AnswerCompletion.Complete,
            "interrupted" => AnswerCompletion.Interrupted,
            "failed" => AnswerCompletion.Failed,
            _ => AnswerCompletion.Interrupted
        };
    }
    public override void Write(System.Text.Json.Utf8JsonWriter writer, AnswerCompletion value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            AnswerCompletion.Complete => "complete",
            AnswerCompletion.Failed => "failed",
            _ => "interrupted"
        });
}


public sealed record SessionHighlight
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");
    [System.Text.Json.Serialization.JsonPropertyName("lineID")]
    public Guid LineId { get; init; }
    public int OffsetSeconds { get; init; }
    public string Quote { get; init; } = "";
}

/// <summary>One explicit user decision about a model correction.</summary>
public sealed record SessionCorrectionDecision
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");
    [System.Text.Json.Serialization.JsonPropertyName("lineID")]
    public Guid LineId { get; init; }
    public string Original { get; init; } = "";
    public string Corrected { get; init; } = "";
    public bool Reverted { get; init; }

    public SessionCorrectionDecision() { }

    [JsonConstructor]
    public SessionCorrectionDecision(string? id, Guid lineId, string original, string corrected, bool reverted)
    {
        Id = id ?? LegacyId(lineId, original, corrected);
        LineId = lineId;
        Original = original;
        Corrected = corrected;
        Reverted = reverted;
    }

    /// <summary>
    /// Schema-1 decisions had no id. A stable fallback keeps source/search revisions steady across reads.
    /// </summary>
    private static string LegacyId(Guid lineId, string original, string corrected)
    {
        // Mac: transcriptRevision(lineID, "\(utf8.count):\(original)\(corrected)"), prefix 32 as a UUID.
        var hash = TranscriptRevisions.Of(lineId, $"{Encoding.UTF8.GetByteCount(original)}:{original}{corrected}");
        var compact = hash[..32];
        return $"{compact[..8]}-{compact[8..12]}-{compact[12..16]}-{compact[16..20]}-{compact[20..]}";
    }
}

/// <summary>
/// User-authored meeting content lives beside, never inside, the replaceable AI output
/// (port of SessionUserContent).
/// </summary>
public sealed record SessionUserContent
{
    public const string FileName = "user-content.json";

    public int SchemaVersion { get; init; } = 1;
    public int Revision { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.MinValue;
    public string PersonalNotes { get; init; } = "";
    public IReadOnlyList<SessionHighlight> Highlights { get; init; } = [];
    /// <summary>Mac editedNotes: the user's section/item version of the template notes.</summary>
    [JsonPropertyName("editedNotes")]
    public StructuredNotesDoc? EditedNotes { get; init; }

    /// <summary>Windows-only: the user's version of one structured view (fishbone/outline/viewpoints).</summary>
    [JsonPropertyName("editedStructuredViews")]
    public StructuredNotesPayload? EditedStructuredViews { get; init; }
    public SessionSummary? EditedSummary { get; init; }
    public string? EditedMarkdown { get; init; }
    public DateTimeOffset? BasedOnGeneratedAt { get; init; }
    public IReadOnlyList<SessionCorrectionDecision> CorrectionDecisions { get; init; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasContent =>
        PersonalNotes.Trim().Length > 0 || Highlights.Count > 0 || EditedNotes is not null
        || EditedStructuredViews is not null || EditedSummary is not null || EditedMarkdown is not null
        || CorrectionDecisions.Count > 0;

    /// <summary>Model corrections can change with a template; explicit user decisions cannot.</summary>
    public IReadOnlyList<TranscriptCorrection> Corrections(
        IReadOnlyList<TranscriptCorrection> generated, IReadOnlyList<TranscriptLine> lines)
    {
        var result = generated.ToList();
        var indices = new Dictionary<Guid, int>();
        for (var index = 0; index < lines.Count; index++) indices.TryAdd(lines[index].Id, index);
        foreach (var decision in CorrectionDecisions)
        {
            if (!indices.TryGetValue(decision.LineId, out var index)) continue;
            if (!lines[index].Text.Contains(decision.Original, StringComparison.Ordinal)) continue;
            var existing = result.FindIndex(correction => correction.Line == index && correction.Original == decision.Original);
            if (existing >= 0)
            {
                result[existing] = result[existing] with { Corrected = decision.Corrected, Reverted = decision.Reverted };
            }
            else
            {
                result.Add(new TranscriptCorrection
                {
                    Id = decision.Id,
                    Line = index,
                    LineId = decision.LineId,
                    Original = decision.Original,
                    Corrected = decision.Corrected,
                    Reverted = decision.Reverted
                });
            }
        }
        return result;
    }
}

/// <summary>Reads/writes user-content.json with an optimistic revision check (port of SessionUserContentStore).</summary>
public sealed class SessionUserContentStore
{
    public sealed class ChangedElsewhereException()
        : Exception("这场记录已更新，请载入最新内容后重试。你的草稿仍然保留。");

    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonDefaults.ArchiveOptions)
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new NotesFormatConverter());
        return options;
    }

    public static SessionUserContent Load(string directory)
    {
        var path = Path.Combine(directory, SessionUserContent.FileName);
        if (!File.Exists(path)) return new SessionUserContent();
        var json = File.ReadAllText(path);
        // Old Windows files put a StructuredNotesPayload under editedNotes; Mac's editedNotes is the
        // template document. Move the legacy shape to its own field before deserializing.
        if (System.Text.Json.Nodes.JsonNode.Parse(json) is System.Text.Json.Nodes.JsonObject node
            && node["editedNotes"] is System.Text.Json.Nodes.JsonObject legacy
            && legacy.ContainsKey("format") && !legacy.ContainsKey("templateID"))
        {
            node["editedStructuredViews"] = legacy.DeepClone();
            node.Remove("editedNotes");
            json = node.ToJsonString();
        }
        var content = JsonSerializer.Deserialize<SessionUserContent>(json, Options)
                      ?? throw new InvalidDataException("无法读取用户内容。");
        if (content.SchemaVersion > 1) throw new InvalidDataException("用户内容格式比当前应用新。");
        return content;
    }

    /// <summary>Writes the next revision atomically; a stale draft is refused with its text preserved.</summary>
    public static SessionUserContent Save(SessionUserContent content, string directory)
    {
        var latest = Load(directory);
        if (content.Revision != latest.Revision) throw new ChangedElsewhereException();
        var committed = content with { Revision = latest.Revision + 1, UpdatedAt = DateTimeOffset.UtcNow };
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, SessionUserContent.FileName);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, JsonSerializer.Serialize(committed, Options));
        File.Move(temporary, path, overwrite: true);
        return committed;
    }
}
