using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>
/// The Mac app's settings.plist (a backup's exported preferences). G5: the keys, types and
/// defaults come from <see cref="MacPreferences"/>; mapped keys are converted to the Windows
/// settings, unmapped Mac keys and unknown keys are kept as raw JSON so nothing is dropped (G6).
/// </summary>
public static class MacSettingsPlist
{
    /// <summary>Writes every Mac-exported key plus the raw values of keys this build does not know.</summary>
    public static byte[] Write(AppSettings settings)
    {
        var macValues = MacPreferenceMap.ReadAll(settings);
        var elements = new SortedDictionary<string, XElement>(StringComparer.Ordinal);

        // G6: keys Windows does not know at all survive a Mac -> Windows -> Mac round trip.
        foreach (var (key, value) in settings.AdditionalProperties ?? new Dictionary<string, JsonElement>())
        {
            if (MacPreferences.IsKnown(key) || SettingsSnapshot.ExportedKeys.Contains(key)) continue;
            elements[key] = JsonToPlist(value);
        }

        foreach (var key in SettingsSnapshot.ExportedKeys)
        {
            elements[key] = key switch
            {
                "subtitleBarX" => new XElement("real", (settings.SubtitleBarX ?? 0).ToString("R", CultureInfo.InvariantCulture)),
                "subtitleBarY" => new XElement("real", (settings.SubtitleBarY ?? 0).ToString("R", CultureInfo.InvariantCulture)),
                _ => MacPreferences.IsKnown(key)
                    ? ObjectToPlist(macValues[key])
                    : new XElement("string", "")
            };
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
        return System.Text.Encoding.UTF8.GetBytes(document.ToString());
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
        _ => element.Value
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

    /// <summary>G6: a plist value to JSON, used for keys this build does not understand.</summary>
    private static System.Text.Json.Nodes.JsonNode? ElementToNode(XElement element) => element.Name.LocalName switch
    {
        "true" => System.Text.Json.Nodes.JsonValue.Create(true),
        "false" => System.Text.Json.Nodes.JsonValue.Create(false),
        "integer" => long.TryParse(element.Value, out var integer) ? System.Text.Json.Nodes.JsonValue.Create(integer) : System.Text.Json.Nodes.JsonValue.Create(0),
        "real" => double.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) ? System.Text.Json.Nodes.JsonValue.Create(real) : System.Text.Json.Nodes.JsonValue.Create(0),
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
        JsonValueKind.Object => new XElement("dict", value.EnumerateObject().SelectMany(property => new object[]
        {
            new XElement("key", property.Name), JsonToPlist(property.Value)
        })),
        _ => new XElement("string", value.GetString() ?? "")
    };
}
