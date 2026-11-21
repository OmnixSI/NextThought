using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NextThought.Core;

/// <summary>
/// A6/§4.3: Mac writes every UUID upper-case with hyphens (`152680E6-…`). Windows storage must
/// write the same spelling so a Mac import compares identifiers as data, not text.
/// </summary>
public sealed class UpperCaseGuidJsonConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && Guid.TryParse(reader.GetString(), out var value) ? value : Guid.Empty;

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("D").ToUpperInvariant());
}

/// <summary>
/// A6/§4.4: the Mac translation line is exactly <c>{id, text, inputRevision?}</c>. Windows keeps
/// its own extra fields (revision/sourceHash/direction/model/date) only when they are meaningful, so
/// rewriting a Mac line does not invent `revision: 0` / `date: 0001-01-01`.
/// </summary>
public sealed class SessionTranslationJsonConverter : JsonConverter<SessionTranslation>
{
    private static readonly DateTimeOffset MacEpoch = new(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override SessionTranslation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        Guid id = root.TryGetProperty("id", out var idNode) && idNode.ValueKind == JsonValueKind.String
            && Guid.TryParse(idNode.GetString(), out var parsed) ? parsed : Guid.Empty;
        var text = root.TryGetProperty("text", out var textNode) && textNode.ValueKind == JsonValueKind.String
            ? textNode.GetString() ?? "" : "";
        var inputRevision = root.TryGetProperty("inputRevision", out var inputNode) && inputNode.ValueKind == JsonValueKind.String
            ? inputNode.GetString() : null;
        var revision = root.TryGetProperty("revision", out var revisionNode) && revisionNode.ValueKind == JsonValueKind.Number
            ? revisionNode.GetInt32() : 0;
        var sourceHash = root.TryGetProperty("sourceHash", out var hashNode) && hashNode.ValueKind == JsonValueKind.String
            ? hashNode.GetString() ?? "" : "";
        var direction = root.TryGetProperty("direction", out var directionNode) && directionNode.ValueKind == JsonValueKind.String
            ? directionNode.GetString() ?? "" : "";
        var model = root.TryGetProperty("model", out var modelNode) && modelNode.ValueKind == JsonValueKind.String
            ? modelNode.GetString() ?? "" : "";
        var date = root.TryGetProperty("date", out var dateNode) ? ReadDate(dateNode) : default;
        return new SessionTranslation(id, revision, sourceHash, direction, text, model, date, inputRevision);
    }

    private static DateTimeOffset ReadDate(JsonElement node) => node.ValueKind switch
    {
        JsonValueKind.Number => MacEpoch.AddSeconds(node.GetDouble()),
        JsonValueKind.String when node.TryGetDateTimeOffset(out var value) => value,
        _ => default
    };

    public override void Write(Utf8JsonWriter writer, SessionTranslation value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id.ToString("D").ToUpperInvariant());
        writer.WriteString("text", value.Text);
        if (value.InputRevision is not null) writer.WriteString("inputRevision", value.InputRevision);
        if (value.Revision > 0) writer.WriteNumber("revision", value.Revision);
        if (value.SourceHash.Length > 0) writer.WriteString("sourceHash", value.SourceHash);
        if (value.Direction.Length > 0) writer.WriteString("direction", value.Direction);
        if (value.Model.Length > 0) writer.WriteString("model", value.Model);
        if (value.Date != default)
            writer.WriteNumber("date", (value.Date.ToUniversalTime() - MacEpoch).TotalSeconds);
        writer.WriteEndObject();
    }
}

/// <summary>
/// A6/§4.4: Mac writes `seriesItems` / `seriesChangeCandidates` only when the workspace has them.
/// The Windows record keeps non-null empty lists, so the serializer skips those two keys when empty;
/// everything else follows the normal record shape (computed properties are JsonIgnore'd).
/// </summary>
public sealed class WorkspaceSeriesJsonConverter : JsonConverter<WorkspaceSeries>
{
    private static readonly Lazy<JsonSerializerOptions> Inner =
        new(() => JsonDefaults.CreateArchiveOptions(includeWorkspaceSeriesConverter: false));

    public override WorkspaceSeries Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<WorkspaceSeries>(ref reader, Inner.Value)!;

    public override void Write(Utf8JsonWriter writer, WorkspaceSeries value, JsonSerializerOptions options)
    {
        var node = JsonSerializer.SerializeToNode(value, Inner.Value)!.AsObject();
        foreach (var key in new[] { "seriesItems", "seriesChangeCandidates" })
            if (node[key] is JsonArray { Count: 0 }) node.Remove(key);
        node.WriteTo(writer);
    }
}
