using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextThought.Core;

/// <summary>A generated meeting summary; user edits keep their own copy (port of SessionSummary fields used here).</summary>
public sealed record SessionSummary
{
    public string Markdown { get; init; } = "";
    public StructuredNotesPayload? Structured { get; init; }
    public DateTimeOffset? GeneratedAt { get; init; }
}

/// <summary>Missing status in an old answer means it finished; unknown future statuses stay incomplete.</summary>
public enum AnswerCompletion
{
    Complete,
    Interrupted,
    Failed
}

public sealed record SessionHighlight
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");
    public Guid LineId { get; init; }
    public int OffsetSeconds { get; init; }
    public string Quote { get; init; } = "";
}

/// <summary>One explicit user decision about a model correction.</summary>
public sealed record SessionCorrectionDecision
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");
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
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            lineId.ToString("D") + "\u0000" + original.Length + ":" + original + corrected)));
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
    public StructuredNotesPayload? EditedNotes { get; init; }
    public SessionSummary? EditedSummary { get; init; }
    public string? EditedMarkdown { get; init; }
    public DateTimeOffset? BasedOnGeneratedAt { get; init; }
    public IReadOnlyList<SessionCorrectionDecision> CorrectionDecisions { get; init; } = [];

    public bool HasContent =>
        PersonalNotes.Trim().Length > 0 || Highlights.Count > 0 || EditedNotes is not null
        || EditedSummary is not null || EditedMarkdown is not null || CorrectionDecisions.Count > 0;

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

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new NotesFormatConverter() }
    };

    public static SessionUserContent Load(string directory)
    {
        var path = Path.Combine(directory, SessionUserContent.FileName);
        if (!File.Exists(path)) return new SessionUserContent();
        var content = JsonSerializer.Deserialize<SessionUserContent>(File.ReadAllText(path), Options)
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
