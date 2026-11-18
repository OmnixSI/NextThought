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
    public SessionTranslation? Translation { get; init; }
}
public sealed record SessionVoiceTag(Guid Id, int Voice);
public sealed record SessionTranslation(Guid Id, int Revision, string SourceHash, string Direction, string Text, string Model, DateTimeOffset Date);
public sealed record AnswerRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Date { get; init; } = DateTimeOffset.UtcNow;
    public string Question { get; init; } = "";
    public string Answer { get; init; } = "";
    public string Model { get; init; } = "";
    public double? Seconds { get; init; }
    public bool Automatic { get; init; }
    public IReadOnlyList<SourceCitation>? Sources { get; init; }
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
    public string SummaryState { get; set; } = "none";
    public string? SummaryError { get; set; }
    public bool InPerson { get; set; }
    public string Platform { get; init; } = "windows";
    [JsonIgnore] public string LocalStartedAt => StartedAt.ToLocalTime().ToString("MM-dd HH:mm");
    [JsonExtensionData] public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
    public override string ToString() => $"{StartedAt.ToLocalTime():MM-dd HH:mm}  {Title}";
}
public sealed record SessionData(SessionMeta Meta, IReadOnlyList<TranscriptLine> Lines, IReadOnlyList<AnswerRecord> Answers, string Summary);
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
    public bool AutomaticAnswers { get; set; }
    public bool AutoSummarize { get; set; }
    public bool UseSemanticRetrieval { get; set; }
    public bool SeparateSpeakers { get; set; }
    public bool TranslateSpeech { get; set; }
    public bool UseMaterials { get; set; }
    public bool UseCodex { get; set; }
    public string CodexModel { get; set; } = "";
    /// <summary>Mac key <c>presenterMode</c>: dual-screen presenting (PresenterMode.swift).</summary>
    public bool PresenterMode { get; set; }
    /// <summary>Mac key <c>presenterSlidesScreen</c>: display id the slides are on; empty picks the first non-camera display.</summary>
    public string PresenterSlidesScreen { get; set; } = "";
    /// <summary>Mac key <c>meetingAutoEndMode</c>: prompt / end / off (MeetingEndWatch).</summary>
    public string MeetingAutoEndMode { get; set; } = "prompt";
    /// <summary>Mac key <c>meetingAutoEndMinutes</c>: how long without recognised words counts as the end.</summary>
    public int MeetingAutoEndMinutes { get; set; } = MeetingEndWatch.Config.DefaultMinutes;
    [JsonIgnore] public int CaptureProcessId { get; set; }
    [JsonIgnore] public long CaptureProcessStartTicks { get; set; }
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    public static readonly JsonSerializerOptions ArchiveOptions = Archive();
    private static JsonSerializerOptions Archive()
    {
        var options = new JsonSerializerOptions(Options);
        options.Converters.Insert(0, new ArchiveDateConverter()); return options;
    }
    private sealed class ArchiveDateConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetDateTimeOffset();
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) => writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture));
    }
}
