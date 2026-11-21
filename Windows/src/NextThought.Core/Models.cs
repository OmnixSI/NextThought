using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextThought.Core;

public enum AudioSource { System, Microphone }
public enum MeetingMode { General, Meeting, Client, Academic, LabMeeting, Lesson, Discussion, Interview, GroupProject, Custom }
public sealed record AudioFrame(AudioSource Source, short[] Samples, long StartSample, DateTimeOffset Origin);
public sealed record TranscriptUpdate(Guid Id, AudioSource Source, string Text, bool IsFinal, DateTimeOffset Start, DateTimeOffset End, double InferenceSeconds = 0);
public sealed record TranscriptLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Date { get; init; }
    public string Speaker { get; init; } = "remote";
    public string Text { get; init; } = "";
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public bool? SuspectedEcho { get; init; }
    public int? Voice { get; init; }

    /// <summary>Mac session transcript stores the translation on the line itself.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("translatedText")]
    public string? TranslatedText { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("translationInputRevision")]
    public string? TranslationInputRevision { get; init; }

    public SessionTranslation? Translation { get; init; }

    /// <summary>Unknown Mac line fields survive an import/export round-trip.</summary>
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalProperties { get; init; }
}
public sealed record SessionVoiceTag(Guid Id, int Voice);
public sealed record SessionTranslation(Guid Id, int Revision, string SourceHash, string Direction, string Text, string Model, DateTimeOffset Date, string? InputRevision = null);
public sealed record AnswerRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Date { get; init; } = DateTimeOffset.UtcNow;
    public string Question { get; init; } = "";
    public string Answer { get; init; } = "";
    public string Model { get; init; } = "";
    public double? Seconds { get; init; }
    public double? FirstTokenSeconds { get; init; }
    public bool Automatic { get; init; }
    public IReadOnlyList<SourceCitation>? Sources { get; init; }
    public string? ScreenshotPath { get; init; }
    public string? WebAnswer { get; init; }
    public IReadOnlyList<WebResult>? WebSources { get; init; }
    public EvidenceBundle? Evidence { get; init; }

    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalProperties { get; init; }
}
public sealed record SessionMeta
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = "新会议";
    public string Mode { get; set; } = "meeting";
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public int TranscriptCount { get; set; }
    public int AnswerCount { get; set; }
    public int QuestionCount { get; set; }
    public string AudioSource { get; set; } = "";
    public string Recognizer { get; set; } = "X-ASR";
    public string AnswerModel { get; set; } = "";
    /// <summary>A7: the series this meeting belongs to (Mac writes these into meta.json).</summary>
    [JsonPropertyName("workspaceID")]
    public string? WorkspaceId { get; set; }

    [JsonPropertyName("workspaceName")]
    public string? WorkspaceName { get; set; }

    public string SummaryState { get; set; } = "none";
    public string? SummaryError { get; set; }

    /// <summary>Voices recognised as the user (Mac meVoices).</summary>
    [JsonPropertyName("meVoices")]
    public IReadOnlyList<int>? MeVoices { get; set; }

    /// <summary>Names given to voices in this meeting (Mac voiceNames).</summary>
    [JsonPropertyName("voiceNames")]
    public IReadOnlyDictionary<string, string>? VoiceNames { get; set; }

    /// <summary>Notes were deferred at recording time (Mac notesDeferred).</summary>
    public bool? NotesDeferred { get; set; }

    /// <summary>True when deferred notes are still owed: finished, recorded and no summary attempt yet.</summary>
    [JsonIgnore]
    public bool NeedsDeferredNotes =>
        NotesDeferred == true && TranscriptCount > 0 && EndedAt is not null && SummaryState == "none";

    /// <summary>
    /// B22: portable interrupted-recording rule (SessionMeta.recoverInterruptedRecording).
    /// Returns whether a transcript was found and the meta now reflects it.
    /// </summary>
    public bool RecoverInterruptedRecording(byte[]? transcriptData, DateTimeOffset? lastTranscriptWrite)
    {
        var lines = JSONLines.Decode<TranscriptLine>(transcriptData ?? []);
        if (lines.Count > 0)
        {
            TranscriptCount = lines.Count;
            EndedAt = lastTranscriptWrite ?? EndedAt ?? DateTimeOffset.UtcNow;
            SummaryState = "none";
            SummaryError = null;
            return true;
        }
        TranscriptCount = lines.Count;
        SummaryState = "none";
        SummaryError = null;
        return false;
    }
    public bool InPerson { get; set; }
    public string Platform { get; init; } = "windows";
    [JsonIgnore] public string LocalStartedAt => UiText.EnglishUi
        ? StartedAt.ToLocalTime().ToString("MMM d, HH:mm", System.Globalization.CultureInfo.InvariantCulture)
        : StartedAt.ToLocalTime().ToString("MM-dd HH:mm");
    [JsonExtensionData] public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }

    /// <summary>A8: keys a Mac meta.json actually carried, so a rewrite does not add Windows defaults.</summary>
    [JsonIgnore] public HashSet<string>? SourceKeys { get; set; }

    public override string ToString() => $"{StartedAt.ToLocalTime():MM-dd HH:mm}  {Title}";
}
public sealed record SessionData(SessionMeta Meta, IReadOnlyList<TranscriptLine> Lines, IReadOnlyList<AnswerRecord> Answers, string Summary)
{
    /// <summary>A9: corrupted append-only lines that were skipped while loading.</summary>
    public int SkippedLines { get; init; }
}
public sealed record ModelProgress(double Fraction, string Message);
public sealed record AnswerOptions(string BaseUrl, string Model, string ApiKey, string ReasoningEffort = "", int? MaxOutputTokens = null);
public sealed record AppSettings
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "";
    public string NotesModel { get; set; } = "";
    public string ReasoningEffort { get; set; } = "";
    public string LocalModel { get; set; } = "xasr480";
    public bool SecondPass { get; set; } = true;
    public bool CaptureSystem { get; set; } = true;
    public bool CaptureMicrophone { get; set; } = true;
    public double MicrophoneGain { get; set; } = 1;
    public string? SystemDeviceId { get; set; }
    public string? MicrophoneDeviceId { get; set; }
    public string Vocabulary { get; set; } = "";
    public string LastMeetingTitle { get; set; } = "";
    public bool AutomaticAnswers { get; set; } = true;
    public bool AutoSummarize { get; set; } = true;

    /// <summary>Mac key <c>recordSessions</c> (default true): keep the transcript and in-meeting
    /// answers of each meeting. Off, the meeting is not written to the history at all.</summary>
    public bool RecordSessions { get; set; } = true;
    public bool UseSemanticRetrieval { get; set; }
    public bool SeparateSpeakers { get; set; }
    public bool TranslateSpeech { get; set; }
    public bool UseMaterials { get; set; }
    public bool WebSearch { get; set; } = true;
    public bool UseCodex { get; set; }
    public string CodexModel { get; set; } = "";

    /// <summary>E10: Mac <c>coachWatchesGaze</c> 鈥?the camera eye-contact coach, off until asked.</summary>
    public bool CoachWatchesGaze { get; set; }

    /// <summary>E10: Mac <c>coachGazeSensitivity</c> 鈥?relaxed / standard / strict (LiveCoach.GazeSensitivity).</summary>
    public string CoachGazeSensitivity { get; set; } = "standard";

    /// <summary>E27: saved OpenAI-compatible providers (Mac key <c>apiPresets</c>, Mac field names).</summary>
    public List<APIPreset> ApiPresets { get; set; } = [];

    /// <summary>E27: the active provider id (Mac key <c>activeAPIPresetID</c>, uppercase UUID).</summary>
    public string ActiveApiPresetId { get; set; } = "";

    /// <summary>E27: Mac key <c>updateRepository</c>: GitHub repository the updater reads releases from.</summary>
    public string UpdateRepository { get; set; } = "OmnixSI/NextThought";

    /// <summary>E27: Mac key <c>codexFastMode</c>; nil follows the model's own default.</summary>
    public bool? CodexFastMode { get; set; }

    /// <summary>Per-action key overrides for the Ctrl+Alt global shortcuts (id → key name).</summary>
    public Dictionary<string, string> Shortcuts { get; set; } = [];

    /// <summary>G8/Mac <c>appLanguagePreference</c>: "system" (default), "zh-Hans" or "en".</summary>
    public string Language { get; set; } = "system";

    /// <summary>D10: the Live/Library context sidebar is collapsed.</summary>
    public bool ContextSidebarHidden { get; set; }

    /// <summary>E9: follow the speaker by jumping to the slide whose title is mentioned.</summary>
    public bool FollowSlides { get; set; }

    
    /// <summary>E9: read the screen once every interval during a meeting. Default off.</summary>
    public bool InsightScreenWatch { get; set; }

    /// <summary>E9: seconds between reads; the UI clamps to at least 1.</summary>
    public double InsightScreenInterval { get; set; } = 1;

    /// <summary>E9: follow the presentation from the read text. Default on; only effective with watch.</summary>
    public bool InsightFollowSlides { get; set; } = true;

    /// <summary>E9: display / window / region.</summary>
    public string InsightScreenSource { get; set; } = "display";

    /// <summary>E9: region rectangle x,y,width,height in physical pixels.</summary>
    public string InsightScreenRegion { get; set; } = "";

    /// <summary>E23: Mac <c>documentOCRProvider</c> — how documents are read: system, paddle (本机增强) or codex (代读).</summary>
    public string DocumentOcrProvider { get; set; } = "system";

    /// <summary>E23: Mac <c>documentProviderWasSelected</c> — the first-run guide has already chosen a reader.</summary>
    public bool DocumentProviderWasSelected { get; set; }

    /// <summary>E23: Mac <c>localOCRModelID</c> — the installed local enhancement model (Mac default; Windows ships PaddleOCR-VL 1.6 for native x64).</summary>
    public string LocalOcrModelId { get; set; } = "paddleocr-vl-1-6-mlx-8bit";

    /// <summary>E23: Mac <c>ocrFallbackToVision</c> — fall back to the system reader and mark degraded text.</summary>
    public bool OcrFallbackToVision { get; set; } = true;

    /// <summary>E23: Mac <c>codexOCRModel</c> — the Codex model used when reading documents with Codex.</summary>
    public string CodexOcrModel { get; set; } = "gpt-6-luna";

    /// <summary>E23: Mac <c>codexOCREffort</c> — low / medium / high / xhigh.</summary>
    public string CodexOcrEffort { get; set; } = "medium";

    /// <summary>E23: Mac <c>codexOCRTimeout</c> — seconds per file, clamped to 30–3600.</summary>
    public double CodexOcrTimeout { get; set; } = 600;

    /// <summary>G6: settings this build does not understand survive a JSON round-trip untouched.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }

    /// <summary>E11: turn translation on automatically when the speech is mostly non-Chinese.</summary>
    public bool AutoTranslateForeign { get; set; }

    /// <summary>
    /// E11/Mac UserDefaults "liveTranslation": the user's remembered 同声传译 choice. Null until they
    /// touch the switch; untouched, hearing English turns interpretation on for that meeting only.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("liveTranslation")]
    public bool? LiveTranslation { get; set; }

    /// <summary>E6: answer language: auto / zh / en.</summary>
    public string AnswerLanguage { get; set; } = "auto";

    /// <summary>E4/Mac UserDefaults "webSearchMode": off / auto / always.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("webSearchMode")]
    public string WebSearchMode { get; set; } = "auto";

    /// <summary>E6: attach a Chinese gloss to English answers (Mac default: on).</summary>
    public bool AnswerGloss { get; set; } = true;

    /// <summary>E3: last app version whose onboarding guide was completed.</summary>
    public string OnboardingCompletedVersion { get; set; } = "";

    /// <summary>E1: app version for which 稍后继续 deferred the first-run guide.</summary>
    public string OnboardingDeferredVersion { get; set; } = "";

    /// <summary>E1: app version for which the home resume row was dismissed.</summary>
    public string OnboardingDismissedVersion { get; set; } = "";

    /// <summary>E1: the guide step 稍后继续 stopped on (0..3).</summary>
    public int OnboardingDeferredStep { get; set; }

    /// <summary>E26: per-scene defaults, keyed by MeetingMode name.</summary>
    public Dictionary<string, SceneDefaults> SceneDefaults { get; set; } = [];


    /// <summary>Appearance: "system" (default), "light" or "dark".</summary>
    public string Appearance { get; set; } = "system";

    /// <summary>Floating cue island width: "narrow" (440), "regular" (540) or "wide" (660).</summary>
    public string CueWidth { get; set; } = "regular";

    /// <summary>Mac key <c>cueAlwaysOpen</c>: keep the island expanded during a meeting.</summary>
    public bool CueAlwaysOpen { get; set; }

    /// <summary>E26b: Mac key <c>cueSurface</c> — "island" (提词岛) or "window" (浮窗).</summary>
    public string CueSurface { get; set; } = "island";

    /// <summary>Mac key <c>teleprompterHideBackground</c>: transparent window background, opaque text.</summary>
    public bool TeleprompterHideBackground { get; set; }

    /// <summary>Mac key <c>teleprompterOpacity</c>: cue window opacity, Mac default 1.0, range 0.35–1.</summary>
    public double TeleprompterOpacity { get; set; } = 1.0;

    /// <summary>Mac key <c>teleprompterFontSize</c>: answer font size, Mac default 22, range 14–36.</summary>
    public double TeleprompterFontSize { get; set; } = 22.0;

    /// <summary>Mac key <c>reducedEffects</c>: the app's own "less visual effects" switch (macOS default off).</summary>
    public bool ReducedEffects { get; set; }

    /// <summary>Mac key <c>textZoom</c> (3.4.0): global text scale, Mac default 1, steps 0.9/1/1.1/1.2/1.3/1.45 (J7).</summary>
    public double TextZoom { get; set; } = 1.0;

    /// <summary>Mac key <c>cueScreen</c>: display name the cue island sits on; empty picks the built-in/primary.</summary>
    public string CueScreen { get; set; } = "";

    /// <summary>Mac key <c>capturePrivacy</c>: exclude all app windows from screen sharing.</summary>
    public bool CapturePrivacy { get; set; } = true;

    /// <summary>Saved subtitle-bar position in screen pixels (Mac subtitleBar.position); null = default.</summary>
    public double? SubtitleBarX { get; set; }
    public double? SubtitleBarY { get; set; }

    /// <summary>Floating subtitle bar width in device-independent pixels.</summary>
    public int SubtitleWidth { get; set; } = 920;

    /// <summary>Primary subtitle line font size (Mac key subtitleBarFontSize, default 24).</summary>
    public int SubtitleFontSize { get; set; } = 24;

    /// <summary>Show the translated second line in the floating subtitle bar.</summary>
    public bool SubtitleShowTranslation { get; set; } = true;

    /// <summary>Check for a new release silently after launch and every six hours.</summary>
    public bool AutoCheckUpdates { get; set; } = true;

    /// <summary>Cloud STT provider id ("", "soniox", "openai", "deepgram").</summary>
    public string CloudStt { get; set; } = "";

    public string CloudSttModel { get; set; } = "";

    /// <summary>"auto", "zh" or "en".</summary>
    public string CloudSttLanguage { get; set; } = "auto";

    /// <summary>Optional WebSocket override (loopback QA only).</summary>
    public string CloudSttEndpoint { get; set; } = "";
    /// <summary>Mac key <c>presenterMode</c>: dual-screen presenting (PresenterMode.swift).</summary>
    public bool PresenterMode { get; set; }
    /// <summary>Mac key <c>presenterSlidesScreen</c>: display id the slides are on; empty picks the first non-camera display.</summary>
    public string PresenterSlidesScreen { get; set; } = "";

    /// <summary>E26b: Mac key <c>presenterSlideNotes</c> — show this slide's notes on the island while presenting.</summary>
    public bool PresenterSlideNotes { get; set; } = true;
    /// <summary>Optional HTTPS relay origin for the phone companion (CompanionRelayClient).</summary>
    public string CompanionRelayUrl { get; set; } = "";
    /// <summary>Mac key <c>meetingAutoEndMode</c>: prompt / end / off (MeetingEndWatch).</summary>
    public string MeetingAutoEndMode { get; set; } = "prompt";
    /// <summary>Mac key <c>meetingAutoEndMinutes</c>: how long without recognised words counts as the end.</summary>
    public int MeetingAutoEndMinutes { get; set; } = MeetingEndWatch.Config.DefaultMinutes;
    [JsonIgnore] public int CaptureProcessId { get; set; }
    [JsonIgnore] public long CaptureProcessStartTicks { get; set; }
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = CreateOptions();
    public static readonly JsonSerializerOptions ArchiveOptions = CreateArchiveOptions(includeWorkspaceSeriesConverter: true);

    private static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Mac archive JSON is UTF-8 text; escaping every Chinese character made files unreadable.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters =
        {
            // A6: UUIDs are upper-case with hyphens and Mac translation lines stay minimal.
            new UpperCaseGuidJsonConverter(), new SessionTranslationJsonConverter(),
            new SessionModeJsonConverter(), new GlossaryKindJsonConverter(), new GlossaryJsonConverter(),
            new StructuredNotesPayloadJsonConverter(), new AnswerCompletionJsonConverter(),
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new GlossaryEntryConverter()
        }
    };

    internal static JsonSerializerOptions CreateArchiveOptions(bool includeWorkspaceSeriesConverter)
    {
        var options = new JsonSerializerOptions(CreateOptions());
        options.Converters.Insert(0, new ArchiveDateConverter());
        if (includeWorkspaceSeriesConverter) options.Converters.Insert(0, new WorkspaceSeriesJsonConverter());
        return options;
    }
    private sealed class ArchiveDateConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Number
                ? DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64())
                : reader.GetDateTimeOffset();
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) => writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture));
    }
}
