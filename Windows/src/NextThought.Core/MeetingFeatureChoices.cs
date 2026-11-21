using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextThought.Core;

/// <summary>
/// J2 (§7): the switches of 这场用什么, remembered per series. The Mac keeps them in
/// <c>UserDefaults</c> under <c>"meetingFeatures." + seriesID.uuidString</c>, as the JSON of
/// <see cref="MeetingFeatures"/> (Sources/NextThought/LiveNotesEngine.swift:151-178); it never
/// writes them into the Workspace/series file and UserDefaults are not part of a backup either.
///
/// The Windows store is a sidecar (<see cref="FileName"/>) whose root keys are exactly those Mac
/// keys — so the names never drift — and it is not written into any series file or backup. The
/// 实时转写 row of the Mac picker is always on (<c>isOn: nil</c>) and has no persisted field; only
/// the four <see cref="MeetingFeatures"/> switches exist.
/// </summary>
public sealed class MeetingFeatureChoices
{
    /// <summary>File name of the Windows sidecar under the data root.</summary>
    public const string FileName = "meeting-features.json";

    private const string KeyPrefix = "meetingFeatures.";

    /// <summary>
    /// The Mac key of one series: <c>"meetingFeatures." + series id</c>. UUIDs are normalized to the
    /// upper-case form of <c>UUID.uuidString</c>, so the key is the same whichever case reaches us (A6).
    /// </summary>
    public static string KeyFor(string seriesID)
    {
        var trimmed = seriesID?.Trim() ?? "";
        return KeyPrefix + (Guid.TryParse(trimmed, out var id) ? id.ToString("D").ToUpperInvariant() : trimmed);
    }

    /// <summary>Mac key → the four switches (case-insensitive: UUID strings may differ in case).</summary>
    [JsonIgnore]
    public Dictionary<string, MeetingFeatures> Keys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What the series used last time, or null when nothing was chosen for it.</summary>
    public MeetingFeatures? Get(string seriesID)
    {
        if (string.IsNullOrEmpty(seriesID)) return null;
        return Keys.TryGetValue(KeyFor(seriesID), out var features) ? features : null;
    }

    /// <summary>
    /// Mac <c>meetingFeatures</c> getter: the series' saved choice, else what the scene starts with
    /// (<see cref="MeetingFeatures.Standard"/>). This is what the next meeting of the series uses.
    /// </summary>
    public MeetingFeatures ForSeries(string seriesID, bool prompting, bool speakers) =>
        Get(seriesID) ?? MeetingFeatures.Standard(prompting, speakers);

    /// <summary>Remember a choice for one series (Mac <c>meetingFeatures = …</c> in UserDefaults).</summary>
    public void Set(string seriesID, MeetingFeatures features)
    {
        if (string.IsNullOrEmpty(seriesID)) return;
        Keys[KeyFor(seriesID)] = features;
    }

    /// <summary>Forget one series' choice; the scene default returns next time (Mac resetMeetingFeatures).</summary>
    public bool Remove(string seriesID)
    {
        if (string.IsNullOrEmpty(seriesID)) return false;
        return Keys.Remove(KeyFor(seriesID));
    }

    /// <summary>Mac <c>resetMeetingFeatures</c> by its Swift name; same as <see cref="Remove"/>.</summary>
    public bool Reset(string seriesID) => Remove(seriesID);

    /// <summary>Read the sidecar; a missing, empty or damaged file is an empty store, never an error.</summary>
    public static MeetingFeatureChoices Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new MeetingFeatureChoices();
            var text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return new MeetingFeatureChoices();
            using var document = JsonDocument.Parse(text);
            var store = new MeetingFeatureChoices();
            if (document.RootElement.ValueKind != JsonValueKind.Object) return store;
            var root = document.RootElement;
            // The first Windows draft wrote {"keys": {…}}; accept it so an existing sidecar keeps its choices.
            if (root.TryGetProperty("keys", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object)
                root = wrapped;
            foreach (var property in root.EnumerateObject())
            {
                if (!property.Name.StartsWith(KeyPrefix, StringComparison.Ordinal)) continue;
                if (property.Value.ValueKind != JsonValueKind.Object) continue;
                var features = property.Value.Deserialize<MeetingFeatures>(JsonDefaults.Options);
                if (features is not null) store.Keys[property.Name] = features;
            }
            return store;
        }
        catch
        {
            return new MeetingFeatureChoices();
        }
    }

    /// <summary>Write the sidecar with the Mac keys at the root; a failed write is the caller's to report.</summary>
    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
               {
                   Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
               }))
        {
            writer.WriteStartObject();
            foreach (var (key, features) in Keys
                         .Where(pair => pair.Key.StartsWith(KeyPrefix, StringComparison.Ordinal))
                         .OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(key);
                writer.WriteRawValue(JsonSerializer.Serialize(features, JsonDefaults.Options));
            }
            writer.WriteEndObject();
        }
        File.WriteAllBytes(path, stream.ToArray());
    }
}
