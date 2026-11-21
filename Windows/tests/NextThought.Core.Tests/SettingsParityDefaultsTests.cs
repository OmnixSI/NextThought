using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// G5: every one of the 114 Mac preference keys keeps the Mac key name, type, default value and
/// range. The expected values below are copied verbatim from the Swift sources
/// (AppState.swift, LiveInsightsState.swift, CueIsland.swift, PresenterMode.swift, Updater.swift,
/// LiveCoach.swift, Onboarding.swift, AssistantLayout.swift, SceneSettings.swift and the Core
/// backup exportedKeys list). Do not change an expectation without changing the Swift source.
/// </summary>
public sealed class SettingsParityDefaultsTests
{
    private sealed record Expected(
        MacPreferenceKind Kind,
        object? Default,
        string[]? Allowed = null,
        double? Minimum = null,
        double? Maximum = null);

    private static object? Json(string json) =>
        MacPreferenceMap.JsonElementToValue(JsonDocument.Parse(json).RootElement);

    private static readonly Dictionary<string, Expected> MacDefaults = new(StringComparer.Ordinal)
    {
        ["AppleLanguages"] = new(MacPreferenceKind.Array, null),
        ["activeAPIPresetID"] = new(MacPreferenceKind.String, null),
        ["activeWorkspaceID"] = new(MacPreferenceKind.String, null),
        ["advancedSettingsEnabled"] = new(MacPreferenceKind.Bool, false),
        ["answerBackend"] = new(MacPreferenceKind.String, "openAICompatible",
            ["codexAppServer", "codexExec", "openAICompatible"]),
        ["answerGloss"] = new(MacPreferenceKind.Bool, true),
        ["answerLanguage"] = new(MacPreferenceKind.String, "auto", ["auto", "zh", "en"]),
        ["answerPreferences"] = new(MacPreferenceKind.String, ""),
        ["answerStyle"] = new(MacPreferenceKind.String, "concise"),
        ["apiBaseURL"] = new(MacPreferenceKind.String, "https://api.openai.com/v1"),
        ["apiModel"] = new(MacPreferenceKind.String, ""),
        ["apiPresets"] = new(MacPreferenceKind.Array, Json("[]")),
        ["apiVisionEnabled"] = new(MacPreferenceKind.Bool, false),
        ["appearance"] = new(MacPreferenceKind.String, "system", ["system", "light", "dark"]),
        ["assistantLayout"] = new(MacPreferenceKind.Dict, Json(
            """{"slots":[{"module":"conversation","column":0,"weight":1.0},{"module":"history","column":0,"weight":0.45},{"module":"question","column":1,"weight":1.0},{"module":"answer","column":1,"weight":1.0}],"detached":[],"split":0.44}""")),
        ["autoApplyLessons"] = new(MacPreferenceKind.Bool, true),
        ["autoBackup"] = new(MacPreferenceKind.Bool, true),
        ["autoCollectKnowledgeCandidates"] = new(MacPreferenceKind.Bool, true),
        ["autoSummarize"] = new(MacPreferenceKind.Bool, true),
        ["automaticAnswers"] = new(MacPreferenceKind.Bool, true),
        ["automaticUpdateChecks"] = new(MacPreferenceKind.Bool, true),
        ["capturePrivacy"] = new(MacPreferenceKind.Bool, true),
        ["cloudConsent"] = new(MacPreferenceKind.Bool, false),
        ["cloudModel"] = new(MacPreferenceKind.String, ""),
        ["cloudProvider"] = new(MacPreferenceKind.String, "soniox", ["soniox", "openai", "deepgram"]),
        ["coachEnabled"] = new(MacPreferenceKind.Bool, true),
        ["coachGazeSensitivity"] = new(MacPreferenceKind.String, "standard", ["relaxed", "standard", "strict"]),
        ["coachWatchesGaze"] = new(MacPreferenceKind.Bool, false),
        ["codexFastMode"] = new(MacPreferenceKind.Bool, null),
        ["codexModelID"] = new(MacPreferenceKind.String, ""),
        ["codexOCREffort"] = new(MacPreferenceKind.String, "medium", ["low", "medium", "high", "xhigh"]),
        ["codexOCRModel"] = new(MacPreferenceKind.String, "gpt-6-luna"),
        ["codexOCRTimeout"] = new(MacPreferenceKind.Real, 600.0, null, 30.0, 3600.0),
        ["codexReasoning"] = new(MacPreferenceKind.String, "low"),
        ["companionPort"] = new(MacPreferenceKind.Integer, 8765L),
        ["companionRelayURL"] = new(MacPreferenceKind.String, ""),
        ["cueAlwaysOpen"] = new(MacPreferenceKind.Bool, false),
        ["cueAvoidsSharing"] = new(MacPreferenceKind.Bool, true),
        ["cueLauncher"] = new(MacPreferenceKind.Bool, true),
        ["cueScreen"] = new(MacPreferenceKind.String, ""),
        ["cueStyle"] = new(MacPreferenceKind.String, "scene", ["scene", "keyPoints", "script"]),
        ["cueSurface"] = new(MacPreferenceKind.String, "island", ["island", "window"]),
        ["cueWidth"] = new(MacPreferenceKind.String, "regular", ["narrow", "regular", "wide"]),
        ["customNotesTemplates"] = new(MacPreferenceKind.Array, Json("[]")),
        ["customShortcuts"] = new(MacPreferenceKind.Dict, Json("{}")),
        ["darkMode"] = new(MacPreferenceKind.Bool, false),
        ["demoNotes"] = new(MacPreferenceKind.String, ""),
        ["demoOnboarding"] = new(MacPreferenceKind.Integer, 0L),
        ["demoSettings"] = new(MacPreferenceKind.Integer, 0L),
        ["documentOCRProvider"] = new(MacPreferenceKind.String, "system", ["system", "paddle", "codex"]),
        ["documentProviderWasSelected"] = new(MacPreferenceKind.Bool, false),
        ["echoSuppression"] = new(MacPreferenceKind.Bool, true),
        ["globalSkillIDs"] = new(MacPreferenceKind.Array, Json("[]")),
        ["insightAnswerProgress"] = new(MacPreferenceKind.Bool, true),
        ["insightFollowSlides"] = new(MacPreferenceKind.Bool, true),
        ["insightScreenInterval"] = new(MacPreferenceKind.Real, 1.0, null, 1.0, null),
        ["insightScreenRegion"] = new(MacPreferenceKind.Array, null),
        ["insightScreenTarget"] = new(MacPreferenceKind.String, ""),
        ["insightScreenWatch"] = new(MacPreferenceKind.Bool, false),
        ["keepOnTop"] = new(MacPreferenceKind.Bool, true),
        ["keepRecognizerWarm"] = new(MacPreferenceKind.Bool, true),
        ["lastScene"] = new(MacPreferenceKind.String, "interview",
            ["general", "interview", "academic", "labMeeting", "meeting", "client", "lesson", "discussion", "groupProject", "custom"]),
        ["liveTranslation"] = new(MacPreferenceKind.Bool, null),
        ["localOCRModelID"] = new(MacPreferenceKind.String, "paddleocr-vl-1-6-mlx-8bit",
            ["paddleocr-vl-1-6-mlx-8bit", "paddleocr-vl-1-6-mlx-4bit"]),
        ["localStreamingModel"] = new(MacPreferenceKind.String, "xasr480", ["xasr480", "xasr160", "legacy"]),
        ["meetingVenue"] = new(MacPreferenceKind.String, "online", ["online", "offline"]),
        ["microphoneEnabled"] = new(MacPreferenceKind.Bool, true),
        ["microphoneVoiceProcessing"] = new(MacPreferenceKind.Bool, false),
        ["notesLanguage"] = new(MacPreferenceKind.String, "auto", ["auto", "zh", "en"]),
        ["notesModelID"] = new(MacPreferenceKind.String, ""),
        ["notesReasoning"] = new(MacPreferenceKind.String, "high"),
        ["notesTemplateByType"] = new(MacPreferenceKind.Dict, Json("{}")),
        ["ocrFallbackToVision"] = new(MacPreferenceKind.Bool, true),
        ["onboardingCompleted"] = new(MacPreferenceKind.Bool, false),
        ["onboardingGuideCompletedVersion"] = new(MacPreferenceKind.String, ""),
        ["onboardingGuideDeferredVersion"] = new(MacPreferenceKind.String, ""),
        ["onboardingGuideDismissedVersion"] = new(MacPreferenceKind.String, ""),
        ["onboardingGuideEnabled"] = new(MacPreferenceKind.Bool, false),
        ["onboardingGuideLayout"] = new(MacPreferenceKind.Integer, null),
        ["onboardingGuideStartedVersion"] = new(MacPreferenceKind.String, null),
        ["onboardingResumeRowDismissedVersion"] = new(MacPreferenceKind.String, ""),
        ["pinnedWorkspaceIDs"] = new(MacPreferenceKind.String, ""),
        ["presenterMode"] = new(MacPreferenceKind.Bool, false),
        ["presenterSlideNotes"] = new(MacPreferenceKind.Bool, true),
        ["presenterSlidesScreen"] = new(MacPreferenceKind.String, ""),
        ["profile"] = new(MacPreferenceKind.String, ""),
        ["projectLibrarySort"] = new(MacPreferenceKind.String, "recent"),
        ["quickNotesWorkspaceID"] = new(MacPreferenceKind.String, null),
        ["quickStartStep"] = new(MacPreferenceKind.Integer, 0L, null, 0.0, null),
        ["recognitionLanguage"] = new(MacPreferenceKind.String, "auto", ["auto", "zh", "en"]),
        ["recognitionVocabulary"] = new(MacPreferenceKind.String, ""),
        ["recordSessions"] = new(MacPreferenceKind.Bool, true),
        ["recordingNoticeShown"] = new(MacPreferenceKind.Bool, false),
        ["reducedEffects"] = new(MacPreferenceKind.Bool, false),
        ["sceneOverrides"] = new(MacPreferenceKind.Dict, Json("{}")),
        ["secondPassEnabled"] = new(MacPreferenceKind.Bool, true),
        ["selectedTargetID"] = new(MacPreferenceKind.String, ""),
        ["semanticRetrievalEnabled"] = new(MacPreferenceKind.Bool, false),
        ["sessionMode"] = new(MacPreferenceKind.String, "general"),
        ["settingsGroup"] = new(MacPreferenceKind.String, ""),
        ["simplifiedInterface"] = new(MacPreferenceKind.Bool, true),
        ["subtitleBarFontSize"] = new(MacPreferenceKind.Real, 24.0, null, 16.0, 44.0),
        ["teleprompterFontSize"] = new(MacPreferenceKind.Real, 22.0, null, 14.0, 36.0),
        ["teleprompterHideBackground"] = new(MacPreferenceKind.Bool, false),
        ["teleprompterOpacity"] = new(MacPreferenceKind.Real, 1.0, null, 0.35, 1.0),
        ["textZoom"] = new(MacPreferenceKind.Real, 1.0, null, 0.9, 1.45),
        ["transcriptionBackend"] = new(MacPreferenceKind.String, "zipformer", ["zipformer", "apple", "cloud"]),
        ["updateChannel"] = new(MacPreferenceKind.String, "stable", ["stable", "main"]),
        ["updateRepository"] = new(MacPreferenceKind.String, "OmnixSI/NextThought"),
        ["verifyUncertainWithModel"] = new(MacPreferenceKind.Bool, false),
        ["webSearchMode"] = new(MacPreferenceKind.String, "auto", ["off", "auto", "always"]),
        ["webSearchProvider"] = new(MacPreferenceKind.String, "codex", ["codex", "tavily"]),
        ["workspaceEditorFocusSection"] = new(MacPreferenceKind.String, "records",
            ["records", "documents", "knowledge", "basic", "requirements", "skills", "review"]),
        ["workspaceMigrationCompleted"] = new(MacPreferenceKind.Bool, false)
    };

    [Fact]
    public void RegistryHasThe114MacKeys()
    {
        Assert.Equal(114, MacDefaults.Count);
        Assert.Equal(114, MacPreferences.All.Count);
        Assert.Equal(MacDefaults.Keys.OrderBy(key => key, StringComparer.Ordinal),
            MacPreferences.Keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(114, MacPreferences.ByKey.Count);
    }

    [Fact]
    public void EveryKeyKeepsTheMacDefault()
    {
        foreach (var (key, expected) in MacDefaults)
        {
            var preference = MacPreferences.Find(key);
            Assert.True(preference is not null, key);
            Assert.True(MacPreferences.ValuesEqual(expected.Default, preference!.Default),
                $"{key}: expected {Describe(expected.Default)}, found {Describe(preference.Default)}");
        }
    }

    [Fact]
    public void EveryKeyKeepsTheMacType()
    {
        foreach (var (key, expected) in MacDefaults)
            Assert.Equal(expected.Kind, MacPreferences.Find(key)!.Kind);
    }

    [Fact]
    public void EveryKeyKeepsTheMacAllowedValuesAndRange()
    {
        foreach (var (key, expected) in MacDefaults)
        {
            var preference = MacPreferences.Find(key)!;
            Assert.Equal(expected.Allowed, preference.AllowedValues);
            Assert.Equal(expected.Minimum, preference.Minimum);
            Assert.Equal(expected.Maximum, preference.Maximum);
        }
    }

    [Fact]
    public void EveryDefaultComesFromASwiftExpressionInTheMacSources()
    {
        foreach (var preference in MacPreferences.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(preference.MacDefault), preference.Key);
            Assert.False(string.IsNullOrWhiteSpace(preference.MacView), preference.Key);
        }
    }

    [Fact]
    public void ValuesAreNormalisedLikeTheMacAppDoes()
    {
        // Unknown enum raw values fall back to the Mac default (Swift: `Enum(rawValue:) ?? .default`).
        Assert.Equal("regular", MacPreferences.Normalize("cueWidth", "huge"));
        Assert.Equal("system", MacPreferences.Normalize("appearance", "blue"));
        Assert.Equal("auto", MacPreferences.Normalize("webSearchMode", "sometimes"));
        Assert.Equal("standard", MacPreferences.Normalize("coachGazeSensitivity", 42));
        // Numbers are clamped to the Mac slider/field ranges.
        Assert.Equal(1.0, MacPreferences.Normalize("teleprompterOpacity", 2.5));
        Assert.Equal(0.35, MacPreferences.Normalize("teleprompterOpacity", 0.1));
        Assert.Equal(36.0, MacPreferences.Normalize("teleprompterFontSize", 99));
        Assert.Equal(44.0, MacPreferences.Normalize("subtitleBarFontSize", 999));
        Assert.Equal(1.0, MacPreferences.Normalize("insightScreenInterval", 0.2));
        Assert.Equal(30.0, MacPreferences.Normalize("codexOCRTimeout", 1));
        Assert.Equal(3600.0, MacPreferences.Normalize("codexOCRTimeout", 99999));
        // Values of the wrong type fall back to the Mac default as well.
        Assert.Equal(true, MacPreferences.Normalize("capturePrivacy", "yes"));
        Assert.Equal(8765L, MacPreferences.Normalize("companionPort", "8765"));
        // nil stays nil for keys whose Mac default is absent.
        Assert.Null(MacPreferences.Normalize("insightScreenRegion", null));
        Assert.Null(MacPreferences.Normalize("codexFastMode", null));
    }

    [Fact]
    public void DefaultsAreDeepCopiedSoCallersCannotMutateTheRegistry()
    {
        var first = (List<object?>)MacPreferences.Default("apiPresets")!;
        first.Add("mutated");
        Assert.Empty((List<object?>)MacPreferences.Default("apiPresets")!);

        var map = (Dictionary<string, object?>)MacPreferences.Default("assistantLayout")!;
        map["split"] = 0.9;
        Assert.Equal(0.44, ((Dictionary<string, object?>)MacPreferences.Default("assistantLayout")!)["split"]);
    }

    [Fact]
    public void WindowsSettingsDefaultsAreTheMacDefaults()
    {
        // The typed Windows settings must not drift from the Mac defaults: read every Mac key
        // from a fresh AppSettings and compare with the Mac value.
        var values = MacPreferenceMap.ReadAll(new AppSettings());
        var defaults = MacPreferences.CreateDefaults();
        foreach (var (key, expected) in MacDefaults)
        {
            Assert.True(MacPreferences.ValuesEqual(expected.Default, values[key]),
                $"{key} is not the Mac default: {Describe(values[key])} != {Describe(expected.Default)}");
            Assert.True(MacPreferences.ValuesEqual(defaults[key], values[key]), key);
        }
    }

    [Fact]
    public void EveryKeyRoundTripsThroughTheTypedSettings()
    {
        var settings = new AppSettings();
        var overrides = NonDefaultValues();
        MacPreferenceMap.ApplyValues(settings, overrides);
        var read = MacPreferenceMap.ReadAll(settings);
        var defaults = MacPreferences.CreateDefaults();
        foreach (var preference in MacPreferences.All)
        {
            var expected = overrides.TryGetValue(preference.Key, out var value)
                ? MacPreferences.Normalize(preference, value)
                : defaults[preference.Key];
            Assert.True(MacPreferences.ValuesEqual(expected, read[preference.Key]),
                $"{preference.Key}: wrote {Describe(expected)}, read back {Describe(read[preference.Key])}");
        }
    }

    private static Dictionary<string, object?> NonDefaultValues() => new(StringComparer.Ordinal)
    {
        ["AppleLanguages"] = new List<object?> { "en" },
        ["activeAPIPresetID"] = "preset",
        ["activeWorkspaceID"] = "workspace",
        ["advancedSettingsEnabled"] = true,
        ["answerBackend"] = "codexAppServer",
        ["answerGloss"] = false,
        ["answerLanguage"] = "en",
        ["answerPreferences"] = "be brief",
        ["answerStyle"] = "detailed",
        ["apiBaseURL"] = "http://127.0.0.1:1234/v1",
        ["apiModel"] = "local-model",
        ["apiPresets"] = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["id"] = "11111111-1111-1111-1111-111111111111",
                ["name"] = "x",
                ["baseURL"] = "https://x.test/v1",
                ["model"] = "m",
                ["effort"] = "low",
                ["keyAccount"] = "answer.api.11111111-1111-1111-1111-111111111111"
            }
        },
        ["apiVisionEnabled"] = true,
        ["appearance"] = "dark",
        ["assistantLayout"] = new Dictionary<string, object?>
        {
            ["slots"] = new List<object?>(),
            ["detached"] = new List<object?> { "answer" },
            ["split"] = 0.5
        },
        ["autoApplyLessons"] = false,
        ["autoBackup"] = false,
        ["autoCollectKnowledgeCandidates"] = false,
        ["autoSummarize"] = false,
        ["automaticAnswers"] = false,
        ["automaticUpdateChecks"] = false,
        ["capturePrivacy"] = false,
        ["cloudConsent"] = true,
        ["cloudModel"] = "nova-3",
        ["cloudProvider"] = "deepgram",
        ["coachEnabled"] = false,
        ["coachGazeSensitivity"] = "strict",
        ["coachWatchesGaze"] = true,
        ["codexFastMode"] = true,
        ["codexModelID"] = "gpt-5.2-codex",
        ["codexOCREffort"] = "high",
        ["codexOCRModel"] = "other-model",
        ["codexOCRTimeout"] = 120.0,
        ["codexReasoning"] = "high",
        ["companionPort"] = 9000L,
        ["companionRelayURL"] = "https://relay.test",
        ["cueAlwaysOpen"] = true,
        ["cueAvoidsSharing"] = false,
        ["cueLauncher"] = false,
        ["cueScreen"] = "DISPLAY2",
        ["cueStyle"] = "script",
        ["cueSurface"] = "window",
        ["cueWidth"] = "wide",
        ["customNotesTemplates"] = new List<object?> { "t1" },
        ["customShortcuts"] = new Dictionary<string, object?> { ["record"] = "Ctrl+Alt+R" },
        ["darkMode"] = true,
        ["demoNotes"] = "work",
        ["demoOnboarding"] = 1L,
        ["demoSettings"] = 1L,
        ["documentOCRProvider"] = "paddle",
        ["documentProviderWasSelected"] = true,
        ["echoSuppression"] = false,
        ["globalSkillIDs"] = new List<object?> { "skill" },
        ["insightAnswerProgress"] = false,
        ["insightFollowSlides"] = false,
        ["insightScreenInterval"] = 4.0,
        ["insightScreenRegion"] = new List<object?> { 10.0, 20.0, 30.0, 40.0 },
        ["insightScreenTarget"] = "window:1",
        ["insightScreenWatch"] = true,
        ["keepOnTop"] = false,
        ["keepRecognizerWarm"] = false,
        ["lastScene"] = "academic",
        ["liveTranslation"] = true,
        ["localOCRModelID"] = "paddleocr-vl-1-6-mlx-4bit",
        ["localStreamingModel"] = "xasr160",
        ["meetingVenue"] = "offline",
        ["microphoneEnabled"] = false,
        ["microphoneVoiceProcessing"] = true,
        ["notesLanguage"] = "en",
        ["notesModelID"] = "notes-model",
        ["notesReasoning"] = "medium",
        ["notesTemplateByType"] = new Dictionary<string, object?> { ["interview"] = "interview" },
        ["ocrFallbackToVision"] = false,
        ["onboardingCompleted"] = true,
        ["onboardingGuideCompletedVersion"] = "1.8.0",
        ["onboardingGuideDeferredVersion"] = "1.8.0",
        ["onboardingGuideDismissedVersion"] = "1.8.0",
        ["onboardingGuideEnabled"] = true,
        ["onboardingGuideLayout"] = 2L,
        ["onboardingGuideStartedVersion"] = "1.8.0",
        ["onboardingResumeRowDismissedVersion"] = "1.8.0",
        ["pinnedWorkspaceIDs"] = "id1|id2",
        ["presenterMode"] = true,
        ["presenterSlideNotes"] = false,
        ["presenterSlidesScreen"] = "DISPLAY2",
        ["profile"] = "background",
        ["projectLibrarySort"] = "name",
        ["quickNotesWorkspaceID"] = "quick",
        ["quickStartStep"] = 2L,
        ["recognitionLanguage"] = "zh",
        ["recognitionVocabulary"] = "Kubernetes",
        ["recordSessions"] = false,
        ["recordingNoticeShown"] = true,
        ["reducedEffects"] = true,
        ["reducedEffects"] = true,
        ["sceneOverrides"] = new Dictionary<string, object?>
        {
            ["interview"] = new Dictionary<string, object?>
            {
                ["liveMode"] = "notesOnly",
                ["style"] = "detailed",
                ["cueStyle"] = "script"
            }
        },
        ["secondPassEnabled"] = false,
        ["selectedTargetID"] = "window:2",
        ["semanticRetrievalEnabled"] = true,
        ["sessionMode"] = "academic",
        ["settingsGroup"] = "scenes",
        ["simplifiedInterface"] = false,
        ["subtitleBarFontSize"] = 30.0,
        ["teleprompterFontSize"] = 28.0,
        ["teleprompterHideBackground"] = true,
        ["teleprompterOpacity"] = 0.5,
        ["transcriptionBackend"] = "cloud",
        ["updateChannel"] = "main",
        ["updateRepository"] = "Org/Repo",
        ["verifyUncertainWithModel"] = true,
        ["webSearchMode"] = "off",
        ["webSearchProvider"] = "tavily",
        ["workspaceEditorFocusSection"] = "basic",
        ["workspaceMigrationCompleted"] = true
    };

    private static string Describe(object? value) => value switch
    {
        null => "nil",
        string text => $"\"{text}\"",
        System.Collections.IEnumerable items => "[" + string.Join(", ", items.Cast<object?>().Select(Describe)) + "]",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "?"
    };
}
