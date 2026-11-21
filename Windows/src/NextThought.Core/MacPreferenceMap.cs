using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NextThought.Core;

/// <summary>
/// G5: maps the Mac preference keys onto <see cref="AppSettings"/>. A key is "mapped" when
/// Windows has a typed setting with the same meaning; the value is converted in both directions
/// (macOS spellings vs the Windows field). Keys Windows has no typed field for yet are kept as
/// raw JSON in <see cref="AppSettings.AdditionalProperties"/> so they still use the Mac key name
/// and Mac default, and survive every save (G6).
/// </summary>
public static class MacPreferenceMap
{
    private static readonly string[] MappedJsonNames =
    [
        "baseUrl", "model", "notesModel", "reasoningEffort", "localModel", "secondPass", "captureMicrophone",
        "vocabulary", "autoSummarize", "automaticAnswers", "autoCheckUpdates", "cloudSttModel", "cloudStt", "recordSessions",
        "codexModel", "useCodex", "webSearch", "useSemanticRetrieval", "subtitleFontSize", "cueSurface", "presenterSlideNotes",
        "coachWatchesGaze", "coachGazeSensitivity",
        "onboardingCompletedVersion", "companionRelayUrl", "sceneDefaults", "insightScreenRegion",
        "answerGloss", "answerLanguage", "appearance", "capturePrivacy", "cueAlwaysOpen", "cueWidth",
        "cueScreen", "cueSurface", "teleprompterHideBackground", "presenterMode", "presenterSlidesScreen", "presenterSlideNotes",
        "insightScreenWatch", "insightScreenInterval", "insightFollowSlides",
        "teleprompterOpacity", "teleprompterFontSize", "reducedEffects", "textZoom",
        "apiPresets", "activeApiPresetId", "codexFastMode", "updateRepository",
        "documentOcrProvider", "documentProviderWasSelected", "localOcrModelId",
        "ocrFallbackToVision", "codexOcrModel", "codexOcrEffort", "codexOcrTimeout"
    ];

    /// <summary>JSON property names of the typed settings that already use the Mac key name.</summary>
    public static IReadOnlyList<string> MappedSettingsJsonNames => MappedJsonNames;

    private static readonly HashSet<string> MappedKeysTemp =
    [
        "answerBackend", "answerGloss", "answerLanguage", "apiBaseURL", "apiModel", "appearance", "autoSummarize",
        "automaticAnswers", "automaticUpdateChecks", "capturePrivacy", "cloudModel", "cloudProvider", "recordSessions",
        "codexModelID", "codexReasoning", "companionRelayURL", "cueAlwaysOpen", "cueScreen", "cueSurface", "cueWidth", "presenterSlideNotes",
        "coachWatchesGaze", "coachGazeSensitivity",
        "insightFollowSlides", "insightScreenInterval", "insightScreenRegion", "insightScreenWatch",
        "localStreamingModel", "microphoneEnabled", "notesModelID", "onboardingGuideCompletedVersion",
        "presenterMode", "presenterSlidesScreen", "recognitionVocabulary", "sceneOverrides", "notesTemplateByType", "secondPassEnabled",
        "semanticRetrievalEnabled", "subtitleBarFontSize", "teleprompterHideBackground", "transcriptionBackend",
        "webSearchMode", "teleprompterOpacity", "teleprompterFontSize", "reducedEffects", "textZoom", "textZoom",
        "apiPresets", "activeAPIPresetID", "codexFastMode", "updateRepository",
        "documentOCRProvider", "documentProviderWasSelected", "localOCRModelID",
        "ocrFallbackToVision", "codexOCRModel", "codexOCREffort", "codexOCRTimeout"
    ];

    private static readonly Dictionary<string, string> ModeNames = new(StringComparer.Ordinal)
    {
        ["general"] = "General", ["interview"] = "Interview", ["academic"] = "Academic",
        ["labMeeting"] = "LabMeeting", ["meeting"] = "Meeting", ["client"] = "Client",
        ["lesson"] = "Lesson", ["discussion"] = "Discussion", ["groupProject"] = "GroupProject",
        ["custom"] = "Custom"
    };

    public static bool IsMapped(string key) => MappedKeysTemp.Contains(key);

    public static IReadOnlyCollection<string> MappedKeys => MappedKeysTemp;

    /// <summary>The Windows value for one Mac key, converted and normalised, or null when unmapped.</summary>
    public static object? Read(AppSettings settings, string key) => key switch
    {
        "apiBaseURL" => settings.BaseUrl,
        "apiModel" => settings.Model,
        "notesModelID" => settings.NotesModel,
        "codexReasoning" => string.IsNullOrEmpty(settings.ReasoningEffort) ? "low" : settings.ReasoningEffort,
        "localStreamingModel" => settings.LocalModel,
        "secondPassEnabled" => settings.SecondPass,
        "microphoneEnabled" => settings.CaptureMicrophone,
        "recognitionVocabulary" => settings.Vocabulary,
        "autoSummarize" => settings.AutoSummarize,
        "automaticAnswers" => settings.AutomaticAnswers,
        "recordSessions" => settings.RecordSessions,
        "automaticUpdateChecks" => settings.AutoCheckUpdates,
        "answerBackend" => settings.UseCodex ? "codexAppServer" : "openAICompatible",
        "webSearchMode" => settings.WebSearchMode,
        "semanticRetrievalEnabled" => settings.UseSemanticRetrieval,
        "codexModelID" => settings.CodexModel,
        "answerGloss" => settings.AnswerGloss,
        "answerLanguage" => settings.AnswerLanguage,
        "appearance" => settings.Appearance,
        "capturePrivacy" => settings.CapturePrivacy,
        "cueAlwaysOpen" => settings.CueAlwaysOpen,
        "cueWidth" => settings.CueWidth,
        "cueScreen" => settings.CueScreen,
        "cueSurface" => settings.CueSurface,
        "presenterSlideNotes" => settings.PresenterSlideNotes,
        "teleprompterHideBackground" => settings.TeleprompterHideBackground,
        "presenterMode" => settings.PresenterMode,
        "presenterSlidesScreen" => settings.PresenterSlidesScreen,
        "insightScreenWatch" => settings.InsightScreenWatch,
        "insightScreenInterval" => settings.InsightScreenInterval,
        "insightFollowSlides" => settings.InsightFollowSlides,
        "insightScreenRegion" => RegionToMac(settings.InsightScreenRegion),
        "teleprompterOpacity" => settings.TeleprompterOpacity,
        "teleprompterFontSize" => settings.TeleprompterFontSize,
        "reducedEffects" => settings.ReducedEffects,
        "textZoom" => settings.TextZoom,
        "coachWatchesGaze" => settings.CoachWatchesGaze,
        "coachGazeSensitivity" => settings.CoachGazeSensitivity,
        "apiPresets" => settings.ApiPresets.Select(preset => (object?)new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = preset.Id,
            ["name"] = preset.Name,
            ["baseURL"] = preset.BaseUrl,
            ["model"] = preset.Model,
            ["effort"] = preset.Effort,
            ["keyAccount"] = preset.KeyAccount
        }).ToList(),
        "activeAPIPresetID" => settings.ActiveApiPresetId.Length == 0 ? null : settings.ActiveApiPresetId,
        "codexFastMode" => settings.CodexFastMode,
        "updateRepository" => settings.UpdateRepository,
        "documentOCRProvider" => settings.DocumentOcrProvider,
        "documentProviderWasSelected" => settings.DocumentProviderWasSelected,
        "localOCRModelID" => settings.LocalOcrModelId,
        "ocrFallbackToVision" => settings.OcrFallbackToVision,
        "codexOCRModel" => settings.CodexOcrModel,
        "codexOCREffort" => settings.CodexOcrEffort,
        "codexOCRTimeout" => settings.CodexOcrTimeout,
        "cloudModel" => settings.CloudSttModel,
        "cloudProvider" => string.IsNullOrEmpty(settings.CloudStt) ? "soniox" : settings.CloudStt,
        "transcriptionBackend" => string.IsNullOrEmpty(settings.CloudStt) ? "zipformer" : "cloud",
        "companionRelayURL" => settings.CompanionRelayUrl,
        "subtitleBarFontSize" => (double)settings.SubtitleFontSize,
        "onboardingGuideCompletedVersion" => settings.OnboardingCompletedVersion,
        "sceneOverrides" => SceneOverridesToMac(settings.SceneDefaults),
        "notesTemplateByType" => NotesTemplateByTypeToMac(settings.SceneDefaults),
        _ => null
    };

    /// <summary>Writes one Mac key into the typed settings (unmapped keys are handled by the store).</summary>
    public static void Apply(AppSettings settings, string key, object? value)
    {
        if (!IsMapped(key)) return;
        var normalized = MacPreferences.Normalize(key, value);
        switch (key)
        {
            case "apiBaseURL": settings.BaseUrl = AsString(normalized, settings.BaseUrl); break;
            case "apiModel": settings.Model = AsString(normalized, settings.Model); break;
            case "notesModelID": settings.NotesModel = AsString(normalized, settings.NotesModel); break;
            case "codexReasoning": settings.ReasoningEffort = AsString(normalized, "low"); break;
            case "localStreamingModel": settings.LocalModel = AsString(normalized, settings.LocalModel); break;
            case "secondPassEnabled": settings.SecondPass = normalized is bool secondPass ? secondPass : settings.SecondPass; break;
            case "microphoneEnabled": settings.CaptureMicrophone = normalized is bool microphone ? microphone : settings.CaptureMicrophone; break;
            case "recognitionVocabulary": settings.Vocabulary = AsString(normalized, settings.Vocabulary); break;
            case "autoSummarize": settings.AutoSummarize = normalized is bool summarize ? summarize : settings.AutoSummarize; break;
            case "automaticAnswers": settings.AutomaticAnswers = normalized is bool automatic ? automatic : settings.AutomaticAnswers; break;
            case "recordSessions": settings.RecordSessions = normalized is bool record ? record : settings.RecordSessions; break;
            case "automaticUpdateChecks": settings.AutoCheckUpdates = normalized is bool checks ? checks : settings.AutoCheckUpdates; break;
            case "answerBackend":
                settings.UseCodex = normalized is string backend && backend is "codexAppServer" or "codexExec";
                break;
            case "webSearchMode":
            {
                // 决议 §3.3: the Mac three-state value (off / auto / always), not a Windows bool.
                // Old settings files stored the bool mapping; accept it once and migrate. Read the
                // raw value: Normalize would turn the legacy bool into the string preference's
                // default before this switch sees it.
                var mode = value switch
                {
                    bool enabled => enabled ? "auto" : "off",
                    string text when text is "off" or "auto" or "always" => text,
                    _ => MacPreferences.Normalize(key, value) as string ?? settings.WebSearchMode
                };
                settings.WebSearchMode = mode;
                settings.WebSearch = mode != "off";
                break;
            }
            case "semanticRetrievalEnabled":
                settings.UseSemanticRetrieval = normalized is bool semantic ? semantic : settings.UseSemanticRetrieval;
                break;
            case "codexModelID": settings.CodexModel = AsString(normalized, settings.CodexModel); break;
            case "answerGloss": settings.AnswerGloss = normalized is bool gloss ? gloss : settings.AnswerGloss; break;
            case "answerLanguage": settings.AnswerLanguage = AsString(normalized, settings.AnswerLanguage); break;
            case "appearance": settings.Appearance = AsString(normalized, settings.Appearance); break;
            case "capturePrivacy": settings.CapturePrivacy = normalized is bool privacy ? privacy : settings.CapturePrivacy; break;
            case "cueAlwaysOpen": settings.CueAlwaysOpen = normalized is bool alwaysOpen ? alwaysOpen : settings.CueAlwaysOpen; break;
            case "cueWidth": settings.CueWidth = AsString(normalized, settings.CueWidth); break;
            case "cueScreen": settings.CueScreen = AsString(normalized, settings.CueScreen); break;
            case "teleprompterHideBackground":
                settings.TeleprompterHideBackground = normalized is bool hideBackground ? hideBackground : settings.TeleprompterHideBackground;
                break;
            case "presenterMode": settings.PresenterMode = normalized is bool presenting ? presenting : settings.PresenterMode; break;
            case "presenterSlidesScreen": settings.PresenterSlidesScreen = AsString(normalized, settings.PresenterSlidesScreen); break;
            case "cueSurface":
            {
                var surface = AsString(normalized, settings.CueSurface);
                settings.CueSurface = surface is "island" or "window" ? surface : settings.CueSurface;
                break;
            }
            case "presenterSlideNotes":
                settings.PresenterSlideNotes = normalized is bool notes ? notes : settings.PresenterSlideNotes;
                break;
            case "insightScreenWatch": settings.InsightScreenWatch = normalized is bool watch ? watch : settings.InsightScreenWatch; break;
            case "insightScreenInterval":
                settings.InsightScreenInterval = normalized is double interval ? interval : settings.InsightScreenInterval;
                break;
            case "insightFollowSlides": settings.InsightFollowSlides = normalized is bool follow ? follow : settings.InsightFollowSlides; break;
            case "insightScreenRegion": settings.InsightScreenRegion = RegionFromMac(normalized); break;
            case "teleprompterOpacity":
                settings.TeleprompterOpacity = normalized is double opacity ? opacity : settings.TeleprompterOpacity;
                break;
            case "teleprompterFontSize":
                settings.TeleprompterFontSize = normalized is double fontSize ? fontSize : settings.TeleprompterFontSize;
                break;
            case "reducedEffects":
                settings.ReducedEffects = normalized is bool reduced ? reduced : settings.ReducedEffects;
                break;
            case "textZoom":
                settings.TextZoom = normalized is double zoom ? zoom : settings.TextZoom;
                break;
            case "coachWatchesGaze":
                settings.CoachWatchesGaze = normalized is bool coachWatch ? coachWatch : settings.CoachWatchesGaze;
                break;
            case "coachGazeSensitivity":
            {
                var sensitivity = AsString(normalized, settings.CoachGazeSensitivity);
                settings.CoachGazeSensitivity = sensitivity is "relaxed" or "standard" or "strict"
                    ? sensitivity
                    : settings.CoachGazeSensitivity;
                break;
            }
            case "apiPresets":
                settings.ApiPresets = ApplyPresets(normalized);
                break;
            case "activeAPIPresetID":
            {
                var id = normalized as string ?? "";
                settings.ActiveApiPresetId = Guid.TryParse(id, out var guid)
                    ? guid.ToString("D").ToUpperInvariant()
                    : id;
                break;
            }
            case "codexFastMode":
                settings.CodexFastMode = normalized is bool fast ? fast : null;
                break;
            case "updateRepository":
                settings.UpdateRepository = AsString(normalized, settings.UpdateRepository);
                break;
            case "documentOCRProvider":
            {
                var provider = AsString(normalized, settings.DocumentOcrProvider);
                settings.DocumentOcrProvider = provider is "system" or "paddle" or "codex"
                    ? provider
                    : settings.DocumentOcrProvider;
                break;
            }
            case "documentProviderWasSelected":
                settings.DocumentProviderWasSelected = normalized is bool selected
                    ? selected
                    : settings.DocumentProviderWasSelected;
                break;
            case "localOCRModelID":
                settings.LocalOcrModelId = AsString(normalized, settings.LocalOcrModelId);
                break;
            case "ocrFallbackToVision":
                settings.OcrFallbackToVision = normalized is bool fallback ? fallback : settings.OcrFallbackToVision;
                break;
            case "codexOCRModel":
                settings.CodexOcrModel = AsString(normalized, settings.CodexOcrModel);
                break;
            case "codexOCREffort":
            {
                var effort = AsString(normalized, settings.CodexOcrEffort);
                settings.CodexOcrEffort = effort is "low" or "medium" or "high" or "xhigh"
                    ? effort
                    : settings.CodexOcrEffort;
                break;
            }
            case "codexOCRTimeout":
                settings.CodexOcrTimeout = normalized switch
                {
                    double real => Math.Clamp(real, 30, 3600),
                    long integer => Math.Clamp(integer, 30, 3600),
                    int integer => Math.Clamp(integer, 30, 3600),
                    _ => settings.CodexOcrTimeout
                };
                break;
            case "cloudModel": settings.CloudSttModel = AsString(normalized, settings.CloudSttModel); break;
            case "companionRelayURL": settings.CompanionRelayUrl = AsString(normalized, settings.CompanionRelayUrl); break;
            case "subtitleBarFontSize":
                settings.SubtitleFontSize = normalized is double size ? (int)Math.Round(size) : settings.SubtitleFontSize;
                break;
            case "onboardingGuideCompletedVersion":
                settings.OnboardingCompletedVersion = AsString(normalized, settings.OnboardingCompletedVersion);
                break;
            case "sceneOverrides": settings.SceneDefaults = ApplySceneOverrides(settings.SceneDefaults, normalized); break;
            case "notesTemplateByType": settings.SceneDefaults = ApplyNotesTemplateByType(settings.SceneDefaults, normalized); break;
            case "transcriptionBackend":
            {
                var path = normalized as string ?? "zipformer";
                settings.CloudStt = path == "cloud"
                    ? (settings.CloudStt.Length > 0 ? settings.CloudStt : "soniox")
                    : "";
                break;
            }
            case "cloudProvider":
            {
                var provider = normalized as string ?? "soniox";
                if (settings.CloudStt.Length > 0) settings.CloudStt = provider;
                break;
            }
        }
    }

    /// <summary>Every Mac key with its current Windows value (mapped) or stored/raw/default value.</summary>
    public static Dictionary<string, object?> ReadAll(AppSettings settings)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var preference in MacPreferences.All)
        {
            object? value;
            if (IsMapped(preference.Key))
            {
                value = MacPreferences.Normalize(preference, Read(settings, preference.Key));
            }
            else if (TryGetRaw(settings, preference.Key, out var raw))
            {
                value = JsonElementToValue(raw);
            }
            else
            {
                value = MacPreferences.Clone(preference.Default);
            }
            values[preference.Key] = value;
        }
        return values;
    }

    /// <summary>Applies every Mac key found in a settings.json document.</summary>
    public static void ApplyJson(AppSettings settings, JsonObject document)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var preference in MacPreferences.All)
        {
            if (!document.TryGetPropertyValue(preference.Key, out var node)) continue;
            values[preference.Key] = JsonNodeToValue(node);
        }
        ApplyValues(settings, values);
    }

    /// <summary>Applies a set of Mac-key values (from settings.json or the Mac settings.plist).</summary>
    public static void ApplyValues(AppSettings settings, IReadOnlyDictionary<string, object?> values)
    {
        settings.AdditionalProperties ??= [];
        // transcriptionBackend selects the backend; cloudProvider only applies on the cloud path.
        if (values.TryGetValue("transcriptionBackend", out var backendValue))
        {
            var backend = MacPreferences.Normalize("transcriptionBackend", backendValue) as string ?? "zipformer";
            if (backend == "cloud")
            {
                var provider = values.TryGetValue("cloudProvider", out var providerValue)
                    ? MacPreferences.Normalize("cloudProvider", providerValue) as string
                    : null;
                settings.CloudStt = provider is "soniox" or "openai" or "deepgram"
                    ? provider
                    : (settings.CloudStt.Length > 0 ? settings.CloudStt : "soniox");
            }
            else
            {
                // "apple" is the macOS system recognizer; the Windows port has no such backend
                // (allowed difference: there is no system speech recognition on Windows).
                settings.CloudStt = "";
            }
        }
        else if (values.TryGetValue("cloudProvider", out var onlyProvider) && settings.CloudStt.Length > 0)
        {
            var provider = MacPreferences.Normalize("cloudProvider", onlyProvider) as string;
            if (provider is "soniox" or "openai" or "deepgram") settings.CloudStt = provider;
        }
        if (values.TryGetValue("cloudModel", out var cloudModel))
            settings.CloudSttModel = MacPreferences.Normalize("cloudModel", cloudModel) as string ?? settings.CloudSttModel;

        foreach (var preference in MacPreferences.All)
        {
            if (preference.Key is "transcriptionBackend" or "cloudProvider" or "cloudModel") continue;
            if (!values.TryGetValue(preference.Key, out var value)) continue;
            var normalized = MacPreferences.Normalize(preference, value);
            if (IsMapped(preference.Key))
            {
                Apply(settings, preference.Key, normalized);
                settings.AdditionalProperties.Remove(preference.Key);
            }
            else
            {
                settings.AdditionalProperties[preference.Key] =
                    JsonSerializer.SerializeToElement(normalized, JsonDefaults.Options);
            }
        }
        foreach (var key in MappedKeysTemp) settings.AdditionalProperties.Remove(key);
    }

    /// <summary>Builds the settings.json document: Mac key names for all 113 preferences.</summary>
    public static JsonObject ToJsonObject(AppSettings settings)
    {
        var node = JsonSerializer.SerializeToNode(settings, JsonDefaults.Options) as JsonObject ?? [];
        foreach (var name in MappedJsonNames) node.Remove(name);
        foreach (var preference in MacPreferences.All)
        {
            if (IsMapped(preference.Key))
            {
                node[preference.Key] = ValueToJsonNode(MacPreferences.Normalize(preference, Read(settings, preference.Key)));
            }
            else if (TryGetRaw(settings, preference.Key, out var raw))
            {
                node[preference.Key] = JsonNode.Parse(raw.GetRawText());
            }
            else
            {
                node[preference.Key] = ValueToJsonNode(MacPreferences.Clone(preference.Default));
            }
        }
        return node;
    }

    public static bool TryGetRaw(AppSettings settings, string key, out JsonElement value)
    {
        if (settings.AdditionalProperties is { } properties && properties.TryGetValue(key, out value)) return true;
        value = default;
        return false;
    }

    /// <summary>
    /// Mac keys whose JSON value shape differs from the Windows typed property with the same name
    /// (insightScreenRegion is an [x,y,w,h] array on the Mac and a "x,y,w,h" string in AppSettings),
    /// so the typed deserializer must not see the Mac value. <see cref="ApplyJson"/> must be called
    /// afterwards to apply it.
    /// </summary>
    private static readonly string[] TypedShapeConflicts = ["insightScreenRegion"];

    public static JsonObject WithoutTypedShapeConflicts(JsonObject document)
    {
        var clone = (JsonObject)document.DeepClone();
        foreach (var name in TypedShapeConflicts) clone.Remove(name);
        return clone;
    }

    public static void SetRaw(AppSettings settings, string key, object? value)
    {
        settings.AdditionalProperties ??= [];
        settings.AdditionalProperties[key] = JsonSerializer.SerializeToElement(value, JsonDefaults.Options);
    }

    // --- value <-> JSON ---------------------------------------------------
    public static object? JsonNodeToValue(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value => ValueFromJsonValue(value),
        JsonArray array => array.Select(JsonNodeToValue).ToList(),
        JsonObject map => map.ToDictionary(pair => pair.Key, pair => JsonNodeToValue(pair.Value), StringComparer.Ordinal),
        _ => null
    };

    public static object? JsonElementToValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDouble(),
        JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToValue).ToList(),
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(property => property.Name, property => JsonElementToValue(property.Value), StringComparer.Ordinal),
        _ => null
    };

    public static JsonNode? ValueToJsonNode(object? value) => value switch
    {
        null => null,
        bool flag => JsonValue.Create(flag),
        string text => JsonValue.Create(text),
        long integer => JsonValue.Create(integer),
        int integer => JsonValue.Create(integer),
        double real => JsonValue.Create(real),
        float real => JsonValue.Create((double)real),
        IReadOnlyDictionary<string, object?> map => new JsonObject(map.ToDictionary(
            pair => pair.Key, pair => ValueToJsonNode(pair.Value), StringComparer.Ordinal)),
        System.Collections.IEnumerable items when value is not string => new JsonArray(
            items.Cast<object?>().Select(ValueToJsonNode).ToArray()),
        _ => JsonValue.Create(value.ToString())
    };

    private static object? ValueFromJsonValue(JsonValue value)
    {
        if (value.TryGetValue<bool>(out var flag)) return flag;
        if (value.TryGetValue<long>(out var integer)) return integer;
        if (value.TryGetValue<double>(out var real)) return real;
        if (value.TryGetValue<string>(out var text)) return text;
        return value.ToJsonString();
    }

    // --- insightScreenRegion ---------------------------------------------
    private static object? RegionToMac(string region)
    {
        if (string.IsNullOrWhiteSpace(region)) return null;
        var parts = region.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return null;
        var numbers = new List<object?>(4);
        foreach (var part in parts)
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return null;
            numbers.Add(number);
        }
        return numbers;
    }

    private static string RegionFromMac(object? value)
    {
        if (value is null || value is string || value is not System.Collections.IEnumerable items) return "";
        var numbers = new List<double>(4);
        foreach (var item in items)
        {
            if (!double.TryParse(Convert.ToString(item, CultureInfo.InvariantCulture), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var number)) return "";
            numbers.Add(number);
        }
        return numbers.Count == 4
            ? string.Join(",", numbers.Select(number => number.ToString("R", CultureInfo.InvariantCulture)))
            : "";
    }

    // --- sceneOverrides <-> SceneDefaults ---------------------------------
    private static Dictionary<string, object?> SceneOverridesToMac(Dictionary<string, SceneDefaults> scenes)
    {
        var overrides = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (modeName, defaults) in scenes)
        {
            if (!Enum.TryParse<MeetingMode>(modeName, out var mode)) continue;
            var raw = char.ToLowerInvariant(modeName[0]) + modeName[1..];
            if (!ModeNames.ContainsKey(raw)) continue;
            // Mac stores only the fields that differ from the built-in row; the template lives in
            // notesTemplateByType, never in sceneOverrides.
            var builtIn = SceneDefaults.For(mode);
            var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (defaults.LiveMode != builtIn.LiveMode) fields["liveMode"] = LiveModeToMac(defaults.LiveMode);
            if (defaults.AnswerStyle != builtIn.AnswerStyle) fields["style"] = AnswerStyleToMac(defaults.AnswerStyle);
            if (defaults.CueStyle != builtIn.CueStyle) fields["cueStyle"] = CueStyleToMac(defaults.CueStyle);
            if (fields.Count > 0) overrides[raw] = fields;
        }
        return overrides;
    }

    private static Dictionary<string, object?> NotesTemplateByTypeToMac(Dictionary<string, SceneDefaults> scenes)
    {
        var byType = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (modeName, defaults) in scenes)
        {
            if (!Enum.TryParse<MeetingMode>(modeName, out var mode)) continue;
            var raw = char.ToLowerInvariant(modeName[0]) + modeName[1..];
            if (!ModeNames.ContainsKey(raw)) continue;
            // The typed round-trip keeps every stored per-type template: Mac stores the key only
            // when it was chosen, and the scene page already stores overrides only.
            if (defaults.NotesTemplate.Length > 0) byType[raw] = defaults.NotesTemplate;
        }
        return byType;
    }

    private static Dictionary<string, SceneDefaults> ApplyNotesTemplateByType(Dictionary<string, SceneDefaults> scenes, object? value)
    {
        if (value is not IReadOnlyDictionary<string, object?> byType) return scenes;
        var next = new Dictionary<string, SceneDefaults>(scenes, StringComparer.Ordinal);
        foreach (var (rawMode, rawId) in byType)
        {
            if (!ModeNames.TryGetValue(rawMode, out var modeName) || !Enum.TryParse<MeetingMode>(modeName, out var mode)) continue;
            if (rawId is not string id || id.Length == 0) continue;
            var current = next.GetValueOrDefault(modeName) ?? SceneDefaults.For(mode);
            next[modeName] = current with { NotesTemplate = id };
        }
        return next;
    }

    private static Dictionary<string, SceneDefaults> ApplySceneOverrides(Dictionary<string, SceneDefaults> scenes,
        object? value)
    {
        if (value is not IReadOnlyDictionary<string, object?> overrides) return scenes;
        var next = new Dictionary<string, SceneDefaults>(scenes, StringComparer.Ordinal);
        foreach (var (rawMode, rawFields) in overrides)
        {
            if (!ModeNames.TryGetValue(rawMode, out var modeName) || rawFields is not IReadOnlyDictionary<string, object?> fields) continue;
            if (!Enum.TryParse<MeetingMode>(modeName, out var mode)) continue;
            var current = next.GetValueOrDefault(modeName) ?? SceneDefaults.For(mode);
            next[modeName] = current with
            {
                LiveMode = fields.TryGetValue("liveMode", out var live) ? LiveModeFromMac(live, current.LiveMode) : current.LiveMode,
                AnswerStyle = fields.TryGetValue("style", out var style) ? AnswerStyleFromMac(style, current.AnswerStyle) : current.AnswerStyle,
                CueStyle = fields.TryGetValue("cueStyle", out var cue) ? CueStyleFromMac(cue, current.CueStyle) : current.CueStyle
            };
        }
        return next;
    }

    private static string LiveModeToMac(string value) => value switch
    {
        "只记录" => "notesOnly",
        _ => "prompting"
    };

    private static string LiveModeFromMac(object? value, string fallback) => value switch
    {
        "notesOnly" => "只记录",
        "prompting" => "自动回答",
        _ => fallback
    };

    private static string AnswerStyleToMac(string value) => value switch
    {
        "详细" => "detailed",
        _ => "concise"
    };

    private static string AnswerStyleFromMac(object? value, string fallback) => value switch
    {
        "detailed" => "详细",
        "concise" => "简洁",
        _ => fallback
    };

    private static string CueStyleToMac(string value) => value switch
    {
        "逐句" => "script",
        "跟随场景" => "scene",
        _ => "keyPoints"
    };

    private static string CueStyleFromMac(object? value, string fallback) => value switch
    {
        "script" => "逐句",
        "keyPoints" => "只看要点",
        "scene" => "跟随场景",
        _ => fallback
    };

    private static string AsString(object? value, string fallback) => value as string ?? fallback;

    /// <summary>Mac <c>apiPresets</c>: an array of APIPreset objects with the Mac field names.</summary>
    private static List<APIPreset> ApplyPresets(object? value)
    {
        var presets = new List<APIPreset>();
        if (value is null || value is string || value is not System.Collections.IEnumerable items) return presets;
        foreach (var item in items)
        {
            if (item is not IReadOnlyDictionary<string, object?> map) continue;
            string Text(string field) => map.TryGetValue(field, out var entry)
                ? Convert.ToString(entry, CultureInfo.InvariantCulture) ?? ""
                : "";
            var name = Text("name");
            if (name.Length == 0) continue;
            var id = Text("id");
            if (id.Length == 0) id = APIPreset.NewId();
            var effort = Text("effort");
            var keyAccount = Text("keyAccount");
            presets.Add(new APIPreset(id, name, Text("baseURL"), Text("model"),
                effort.Length == 0 ? "default" : effort, keyAccount.Length == 0 ? null : keyAccount));
        }
        return presets;
    }
}
