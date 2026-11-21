using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>
/// The Mac app's settings.plist (a backup's exported preferences). G5: the keys, types and
/// defaults come from <see cref="MacPreferences"/>; mapped keys are converted to the Windows
/// settings, unmapped Mac keys and unknown keys are kept as raw JSON so nothing is dropped (G6).
///
/// A12: the real Mac backup fixture stores <c>apiPresets</c> as a base64 <c>&lt;data&gt;</c>
/// (JSON in UserDefaults), so reading accepts both the data form and the legacy array form and
/// writing uses the Mac data form; known-but-unmapped keys are exported with their preserved raw
/// value (or the Mac default) instead of an empty string, and key material is never exported.
/// </summary>
public static class MacSettingsPlist
{
    /// <summary>Keys that look like credentials must never be written to the exported settings.</summary>
    private static bool IsSecretKey(string key)
    {
        var lower = key.ToLowerInvariant();
        return lower.Contains("apikey", StringComparison.Ordinal)
               || lower.Contains("api_key", StringComparison.Ordinal)
               || lower.Contains("secret", StringComparison.Ordinal)
               || lower.Contains("password", StringComparison.Ordinal)
               || lower.Contains("token", StringComparison.Ordinal);
    }

    /// <summary>Writes every Mac-exported key plus the raw values of keys this build does not know.</summary>
    public static byte[] Write(AppSettings settings)
    {
        var macValues = MacPreferenceMap.ReadAll(settings);
        var elements = new SortedDictionary<string, XElement>(StringComparer.Ordinal);
        var raw = settings.AdditionalProperties ?? new Dictionary<string, JsonElement>();

        // G6: keys Windows does not know at all survive a Mac -> Windows -> Mac round trip.
        foreach (var (key, value) in raw)
        {
            if (IsSecretKey(key)) continue;
            if (MacPreferences.IsKnown(key) || SettingsSnapshot.ExportedKeys.Contains(key)) continue;
            elements[key] = JsonToPlist(value);
        }

        foreach (var key in SettingsSnapshot.ExportedKeys)
        {
            switch (key)
            {
                case "subtitleBarX":
                    elements[key] = new XElement("real", (settings.SubtitleBarX ?? 0).ToString("R", CultureInfo.InvariantCulture));
                    continue;
                case "subtitleBarY":
                    elements[key] = new XElement("real", (settings.SubtitleBarY ?? 0).ToString("R", CultureInfo.InvariantCulture));
                    continue;
            }
            if (MacPreferenceMap.IsMapped(key))
            {
                // Mac UserDefaults stores apiPresets as JSON data; the plist carries it as <data>.
                elements[key] = key == "apiPresets"
                    ? DataElement(JsonSerializer.Serialize(macValues[key], JsonDefaults.Options))
                    : ObjectToPlist(macValues[key]);
                continue;
            }
            // A known Mac-only key: the imported raw value wins, otherwise its Mac default keeps the
            // exported type valid (Mac writes the value it has in UserDefaults).
            if (raw.TryGetValue(key, out var preserved))
                elements[key] = JsonToPlist(preserved);
            else
                elements[key] = ObjectToPlist(MacPreferences.Default(key));
        }

        var xml = new XElement("plist", new XAttribute("version", "1.0"),
            new XElement("dict", elements.SelectMany(pair => new object[]
            {
                new XElement("key", pair.Key),
                pair.Value
            })));
        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
            xml);
        // XDocument.ToString() omits the XML declaration, but Mac writes one; keep the export
        // byte-compatible in shape so the plist round-trip is easier to compare.
        var declaration = document.Declaration is { } head ? head + "\n" : "";
        return System.Text.Encoding.UTF8.GetBytes(declaration + document);
    }

    /// <summary>Reads the Mac settings.plist into the Windows settings, keeping unknown keys (G6).</summary>
    public static AppSettings Apply(byte[] plist, AppSettings current)
    {
        try
        {
            var document = XDocument.Parse(System.Text.Encoding.UTF8.GetString(plist));
            var dictionary = document.Root?.Element("dict");
            if (dictionary is null) return current;
            var children = dictionary.Elements().ToList();
            var macValues = new Dictionary<string, object?>(StringComparer.Ordinal);
            var unknown = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            for (var index = 0; index + 1 < children.Count; index += 2)
            {
                if (children[index].Name != "key") continue;
                var key = children[index].Value;
                // A12: credentials never enter the settings model, even if an archive has them.
                if (IsSecretKey(key)) continue;
                var element = children[index + 1];
                if (MacPreferences.IsKnown(key))
                {
                    var preference = MacPreferences.Find(key)!;
                    var value = ElementToValue(element);
                    // An empty <string/> is how an absent value (Mac default nil) is exported.
                    if (preference.Default is null && value is string { Length: 0 }) value = null;
                    macValues[key] = value;
                }
                else if (key is "subtitleBarX" or "subtitleBarY")
                {
                    var real = ParseReal(element);
                    if (key == "subtitleBarX") current.SubtitleBarX = real;
                    else current.SubtitleBarY = real;
                }
                else
                {
                    unknown[key] = JsonSerializer.SerializeToElement(ElementToNode(element), JsonDefaults.Options);
                }
            }
            MacPreferenceMap.ApplyValues(current, macValues);
            var merged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var (key, value) in current.AdditionalProperties ?? new Dictionary<string, JsonElement>())
                merged[key] = value.Clone();
            foreach (var (key, value) in unknown) merged[key] = value;
            current.AdditionalProperties = merged;
            return current;
        }
        catch (System.Xml.XmlException) { return current; }
    }

    private static object? ElementToValue(XElement element) => element.Name.LocalName switch
    {
        "true" => true,
        "false" => false,
        "integer" => long.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) ? integer : 0L,
        "real" => ParseReal(element),
        "array" => element.Elements().Select(ElementToValue).ToList(),
        "dict" => DictToValue(element),
        "data" => DataToValue(element.Value),
        _ => element.Value
    };

    /// <summary>
    /// A12: the real Mac backup writes apiPresets as a base64 &lt;data&gt; holding JSON. Decode it
    /// into the same object shape an &lt;array&gt; of dictionaries produces so the mapping applies;
    /// data that is not JSON stays as base64 text.
    /// </summary>
    private static object? DataToValue(string base64)
    {
        var compact = string.Concat(base64.Where(character => !char.IsWhiteSpace(character)));
        try
        {
            var bytes = Convert.FromBase64String(compact);
            using var document = JsonDocument.Parse(bytes);
            return JsonElementToValue(document.RootElement);
        }
        catch (FormatException) { return compact; }
        catch (JsonException) { return compact; }
    }

    private static object? JsonElementToValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(property => property.Name, property => JsonElementToValue(property.Value), StringComparer.Ordinal),
        JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToValue).ToList(),
        JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),
        _ => null
    };

    private static Dictionary<string, object?> DictToValue(XElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        var children = element.Elements().ToList();
        for (var index = 0; index + 1 < children.Count; index += 2)
            if (children[index].Name == "key")
                result[children[index].Value] = ElementToValue(children[index + 1]);
        return result;
    }

    private static double ParseReal(XElement element) =>
        double.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) ? real : 0.0;

    private static XElement ObjectToPlist(object? value) => value switch
    {
        null => new XElement("string", ""),
        bool flag => new XElement(flag ? "true" : "false"),
        string text => new XElement("string", text),
        long integer => new XElement("integer", integer.ToString(CultureInfo.InvariantCulture)),
        int integer => new XElement("integer", integer.ToString(CultureInfo.InvariantCulture)),
        double real => new XElement("real", real.ToString("R", CultureInfo.InvariantCulture)),
        float real => new XElement("real", ((double)real).ToString("R", CultureInfo.InvariantCulture)),
        IReadOnlyDictionary<string, object?> map => new XElement("dict", map
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .SelectMany(pair => new object[] { new XElement("key", pair.Key), ObjectToPlist(pair.Value) })),
        System.Collections.IEnumerable items when value is not string => new XElement("array",
            items.Cast<object?>().Select(ObjectToPlist)),
        JsonElement element => JsonToPlist(element),
        _ => new XElement("string", Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")
    };

    private static XElement DataElement(string json) =>
        new("data", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)));

    /// <summary>G6: a plist value to JSON, used for keys this build does not understand.</summary>
    private static System.Text.Json.Nodes.JsonNode? ElementToNode(XElement element) => element.Name.LocalName switch
    {
        "true" => System.Text.Json.Nodes.JsonValue.Create(true),
        "false" => System.Text.Json.Nodes.JsonValue.Create(false),
        "integer" => long.TryParse(element.Value, out var integer) ? System.Text.Json.Nodes.JsonValue.Create(integer) : System.Text.Json.Nodes.JsonValue.Create(0),
        "real" => double.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) ? System.Text.Json.Nodes.JsonValue.Create(real) : System.Text.Json.Nodes.JsonValue.Create(0),
        "data" => new System.Text.Json.Nodes.JsonObject
        {
            ["$data"] = string.Concat(element.Value.Where(character => !char.IsWhiteSpace(character)))
        },
        "array" => new System.Text.Json.Nodes.JsonArray(element.Elements().Select(ElementToNode).ToArray()),
        "dict" => DictToNode(element),
        _ => System.Text.Json.Nodes.JsonValue.Create(element.Value)
    };

    private static System.Text.Json.Nodes.JsonObject DictToNode(XElement element)
    {
        var result = new System.Text.Json.Nodes.JsonObject();
        var children = element.Elements().ToList();
        for (var index = 0; index + 1 < children.Count; index += 2)
            if (children[index].Name == "key") result[children[index].Value] = ElementToNode(children[index + 1]);
        return result;
    }

    private static XElement JsonToPlist(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => new XElement("true"),
        JsonValueKind.False => new XElement("false"),
        JsonValueKind.Number => value.TryGetInt64(out _) ? new XElement("integer", value.GetRawText()) : new XElement("real", value.GetRawText()),
        JsonValueKind.Array => new XElement("array", value.EnumerateArray().Select(JsonToPlist)),
        JsonValueKind.Object when IsDataNode(value, out var data) => new XElement("data", data),
        JsonValueKind.Object => new XElement("dict", value.EnumerateObject().SelectMany(property => new object[]
        {
            new XElement("key", property.Name), JsonToPlist(property.Value)
        })),
        _ => new XElement("string", value.GetString() ?? "")
    };

    /// <summary>G6: an unknown &lt;data&gt; value is kept as {"$data": base64} and written back as data.</summary>
    private static bool IsDataNode(JsonElement value, out string data)
    {
        data = "";
        var properties = value.EnumerateObject().ToList();
        if (properties.Count != 1 || properties[0].Name != "$data"
            || properties[0].Value.ValueKind != JsonValueKind.String) return false;
        data = properties[0].Value.GetString() ?? "";
        return true;
    }
}
