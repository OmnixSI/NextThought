using System.Security;
using System.Xml.Linq;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>
/// The small, documented subset of the Mac settings.plist that maps onto AppSettings.
/// macOS UserDefaults stores these keys; unknown keys are ignored on both sides.
/// </summary>
public static class MacSettingsPlist
{
    private static readonly HashSet<string> BoolKeys =
    [
        "apiVisionEnabled", "verifyUncertainWithModel", "automaticAnswers", "recordSessions", "autoSummarize",
        "autoCollectKnowledgeCandidates", "autoApplyLessons", "secondPassEnabled", "cloudConsent", "echoSuppression",
        "microphoneVoiceProcessing", "microphoneEnabled", "capturePrivacy", "keepOnTop", "reducedEffects",
        "automaticUpdateChecks", "autoBackup", "advancedSettingsEnabled", "insightScreenWatch", "insightFollowSlides",
        "insightAnswerProgress", "documentProviderWasSelected", "ocrFallbackToVision", "semanticRetrievalEnabled",
        "coachEnabled", "coachWatchesGaze", "cueAlwaysOpen", "cueLauncher", "cueAvoidsSharing", "keepRecognizerWarm"
    ];

    private static readonly HashSet<string> RealKeys =
        ["teleprompterOpacity", "teleprompterFontSize", "insightScreenInterval", "coachGazeSensitivity", "codexOCRTimeout"];

    private static readonly HashSet<string> ArrayKeys =
        ["apiPresets", "globalSkillIDs", "pinnedWorkspaceIDs", "customNotesTemplates", "customShortcuts",
         "notesTemplateByType", "sceneOverrides", "projectLibrarySort", "assistantLayout"];

    /// <summary>A12: every SettingsSnapshot.exportedKeys entry, in the Mac settings.plist shape.</summary>
    public static byte[] Write(AppSettings settings)
    {
        var values = new Dictionary<string, (string Type, string Value)>(StringComparer.Ordinal)
        {
            ["apiBaseURL"] = ("string", settings.BaseUrl),
            ["apiModel"] = ("string", settings.Model),
            ["answerBackend"] = ("string", settings.UseCodex ? "codex" : "api"),
            ["codexModelID"] = ("string", settings.CodexModel),
            ["codexReasoning"] = ("string", settings.ReasoningEffort),
            ["automaticAnswers"] = ("bool", settings.AutomaticAnswers ? "true" : "false"),
            ["recordSessions"] = ("bool", "true"),
            ["autoSummarize"] = ("bool", settings.AutoSummarize ? "true" : "false"),
            ["autoCollectKnowledgeCandidates"] = ("bool", "true"),
            ["autoApplyLessons"] = ("bool", "true"),
            ["transcriptionBackend"] = ("string", "local"),
            ["localStreamingModel"] = ("string", settings.LocalModel),
            ["secondPassEnabled"] = ("bool", settings.SecondPass ? "true" : "false"),
            ["recognitionLanguage"] = ("string", settings.Language),
            ["recognitionVocabulary"] = ("string", settings.Vocabulary),
            ["cloudProvider"] = ("string", ""),
            ["cloudModel"] = ("string", ""),
            ["cloudConsent"] = ("bool", "false"),
            ["echoSuppression"] = ("bool", "false"),
            ["microphoneVoiceProcessing"] = ("bool", "false"),
            ["microphoneEnabled"] = ("bool", settings.CaptureMicrophone ? "true" : "false"),
            ["teleprompterOpacity"] = ("real", "0.92"),
            ["teleprompterFontSize"] = ("real", "26"),
            ["capturePrivacy"] = ("bool", "true"),
            ["keepOnTop"] = ("bool", "true"),
            ["appearance"] = ("string", settings.Appearance),
            ["reducedEffects"] = ("bool", "false"),
            ["customShortcuts"] = ("dict", ""),
            ["companionPort"] = ("integer", "0"),
            ["companionRelayURL"] = ("string", ""),
            ["keepRecognizerWarm"] = ("bool", "false"),
            ["updateChannel"] = ("string", "stable"),
            ["automaticUpdateChecks"] = ("bool", "true"),
            ["updateRepository"] = ("string", ""),
            ["activeWorkspaceID"] = ("string", ""),
            ["autoBackup"] = ("bool", "false"),
            ["advancedSettingsEnabled"] = ("bool", "true"),
            ["apiPresets"] = ("array", ""),
            ["activeAPIPresetID"] = ("string", ""),
            ["webSearchMode"] = ("string", settings.WebSearch ? "auto" : "off"),
            ["webSearchProvider"] = ("string", ""),
            ["insightScreenWatch"] = ("bool", settings.InsightScreenWatch ? "true" : "false"),
            ["insightScreenInterval"] = ("real", settings.InsightScreenInterval.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ["insightFollowSlides"] = ("bool", settings.InsightFollowSlides ? "true" : "false"),
            ["insightAnswerProgress"] = ("bool", "false"),
            ["globalSkillIDs"] = ("array", ""),
            ["assistantLayout"] = ("dict", ""),
            ["documentOCRProvider"] = ("string", ""),
            ["documentProviderWasSelected"] = ("bool", "false"),
            ["ocrFallbackToVision"] = ("bool", "false"),
            ["semanticRetrievalEnabled"] = ("bool", settings.UseSemanticRetrieval ? "true" : "false"),
            ["localOCRModelID"] = ("string", ""),
            ["codexOCRModel"] = ("string", ""),
            ["codexOCREffort"] = ("string", ""),
            ["codexOCRTimeout"] = ("real", "60"),
            ["notesModelID"] = ("string", settings.NotesModel),
            ["notesReasoning"] = ("string", settings.ReasoningEffort),
            ["notesLanguage"] = ("string", ""),
            ["notesTemplateByType"] = ("dict", ""),
            ["customNotesTemplates"] = ("array", ""),
            ["pinnedWorkspaceIDs"] = ("array", ""),
            ["projectLibrarySort"] = ("dict", ""),
            ["cueSurface"] = ("string", "island"),
            ["cueWidth"] = ("string", settings.CueWidth),
            ["cueStyle"] = ("string", "keyPoints"),
            ["cueScreen"] = ("string", settings.CueScreen),
            ["cueLauncher"] = ("bool", "true"),
            ["cueAvoidsSharing"] = ("bool", "false"),
            ["coachEnabled"] = ("bool", "false"),
            ["coachWatchesGaze"] = ("bool", "false"),
            ["coachGazeSensitivity"] = ("real", "1"),
            ["lastScene"] = ("string", ""),
            ["sceneOverrides"] = ("dict", "")
        };
        foreach (var (key, value) in settings.AdditionalProperties ?? new Dictionary<string, System.Text.Json.JsonElement>())
        {
            if (values.ContainsKey(key)) continue;
            values[key] = value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.True => ("bool", "true"),
                System.Text.Json.JsonValueKind.False => ("bool", "false"),
                System.Text.Json.JsonValueKind.Number => value.TryGetInt64(out _) ? ("integer", value.GetRawText()) : ("real", value.GetRawText()),
                System.Text.Json.JsonValueKind.Array => ("array-json", value.GetRawText()),
                System.Text.Json.JsonValueKind.Object => ("dict-json", value.GetRawText()),
                System.Text.Json.JsonValueKind.String => ("string", value.GetString() ?? ""),
                _ => ("string", "")
            };
        }
        foreach (var key in SettingsSnapshot.ExportedKeys)
        {
            if (values.ContainsKey(key)) continue;
            values[key] = BoolKeys.Contains(key) ? ("bool", "false")
                : RealKeys.Contains(key) ? ("real", "0")
                : ArrayKeys.Contains(key) ? (key.EndsWith("IDs", StringComparison.Ordinal) ? "array" : "dict", "")
                : ("string", "");
        }
        var xml = new XElement("plist", new XAttribute("version", "1.0"),
            new XElement("dict", values
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .SelectMany(pair => new object[]
                {
                    new XElement("key", pair.Key),
                    pair.Value.Type switch
                    {
                        "bool" => new XElement(pair.Value.Value == "true" ? "true" : "false"),
                        "real" => new XElement("real", pair.Value.Value),
                        "integer" => new XElement("integer", pair.Value.Value),
                        "array" => new XElement("array"),
                        "dict" => new XElement("dict"),
                        "array-json" => JsonToPlist(System.Text.Json.JsonDocument.Parse(pair.Value.Value).RootElement),
                        "dict-json" => JsonToPlist(System.Text.Json.JsonDocument.Parse(pair.Value.Value).RootElement),
                        _ => new XElement("string", pair.Value.Value)
                    }
                })));
        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
            xml);
        return System.Text.Encoding.UTF8.GetBytes(document.ToString());
    }

    public static AppSettings Apply(byte[] plist, AppSettings current)
    {
        try
        {
            var document = XDocument.Parse(System.Text.Encoding.UTF8.GetString(plist));
            var dictionary = document.Root?.Element("dict");
            if (dictionary is null) return current;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var children = dictionary.Elements().ToList();
            for (var index = 0; index + 1 < children.Count; index += 2)
            {
                if (children[index].Name != "key") continue;
                var key = children[index].Value;
                var value = children[index + 1];
                values[key] = value.Name == "true" ? "true" : value.Name == "false" ? "false" : value.Value;
            }
            bool Bool(string key, bool fallback) => values.TryGetValue(key, out var text) ? text == "true" : fallback;
            string Text(string key, string fallback) => values.TryGetValue(key, out var text) && text.Length > 0 ? text : fallback;
            var mergedUnknown = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
            foreach (var (key, value) in current.AdditionalProperties ?? new Dictionary<string, System.Text.Json.JsonElement>())
                mergedUnknown[key] = value.Clone();
            for (var index = 0; index + 1 < children.Count; index += 2)
            {
                if (children[index].Name != "key") continue;
                var key = children[index].Value;
                if (SettingsSnapshot.ExportedKeys.Contains(key)) continue;
                mergedUnknown[key] = System.Text.Json.JsonSerializer.SerializeToElement(ElementToNode(children[index + 1]));
            }
            return current with
            {
                AdditionalProperties = mergedUnknown,
                BaseUrl = Text("apiBaseURL", current.BaseUrl),
                Model = Text("apiModel", current.Model),
                NotesModel = Text("notesModelID", current.NotesModel),
                LocalModel = Text("localStreamingModel", current.LocalModel),
                SecondPass = Bool("secondPassEnabled", current.SecondPass),
                CaptureMicrophone = Bool("microphoneEnabled", current.CaptureMicrophone),
                AutomaticAnswers = Bool("automaticAnswers", current.AutomaticAnswers),
                AutoSummarize = Bool("autoSummarize", current.AutoSummarize),
                UseSemanticRetrieval = Bool("semanticRetrievalEnabled", current.UseSemanticRetrieval),
                WebSearch = values.TryGetValue("webSearchMode", out var search) ? search != "off" : current.WebSearch,
                CodexModel = Text("codexModelID", current.CodexModel),
            };
        }
        catch (System.Xml.XmlException) { return current; }
    }

    /// <summary>G6: plist value to JSON, used only for keys this build does not understand.</summary>
    private static System.Text.Json.Nodes.JsonNode? ElementToNode(XElement element) => element.Name.LocalName switch
    {
        "true" => System.Text.Json.Nodes.JsonValue.Create(true),
        "false" => System.Text.Json.Nodes.JsonValue.Create(false),
        "integer" => long.TryParse(element.Value, out var integer) ? System.Text.Json.Nodes.JsonValue.Create(integer) : System.Text.Json.Nodes.JsonValue.Create(0),
        "real" => double.TryParse(element.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var real) ? System.Text.Json.Nodes.JsonValue.Create(real) : System.Text.Json.Nodes.JsonValue.Create(0),
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

    private static XElement JsonToPlist(System.Text.Json.JsonElement value) => value.ValueKind switch
    {
        System.Text.Json.JsonValueKind.True => new XElement("true"),
        System.Text.Json.JsonValueKind.False => new XElement("false"),
        System.Text.Json.JsonValueKind.Number => value.TryGetInt64(out _) ? new XElement("integer", value.GetRawText()) : new XElement("real", value.GetRawText()),
        System.Text.Json.JsonValueKind.Array => new XElement("array", value.EnumerateArray().Select(JsonToPlist)),
        System.Text.Json.JsonValueKind.Object => new XElement("dict", value.EnumerateObject().SelectMany(property => new object[]
        {
            new XElement("key", property.Name), JsonToPlist(property.Value)
        })),
        _ => new XElement("string", value.GetString() ?? "")
    };
}
