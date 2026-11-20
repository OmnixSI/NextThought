namespace NextThought.Core;

/// <summary>Stored session mode (port of SessionMode); mirrors the app's MeetingMode.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(SessionModeJsonConverter))]
public enum SessionMode
{
    General,
    Interview,
    Academic,
    LabMeeting,
    Meeting,
    Client,
    Lesson,
    Discussion,
    GroupProject,
    Custom
}

public static class SessionModes
{
    public static string Title(this SessionMode mode) => mode switch
    {
        SessionMode.General => "通用",
        SessionMode.Interview => "面试",
        SessionMode.Academic => "汇报 / 答辩",
        SessionMode.LabMeeting => "组会",
        SessionMode.Meeting => "工作会议",
        SessionMode.Client => "客户沟通",
        SessionMode.Lesson => "讲座 / 课程",
        SessionMode.Discussion => "小组讨论",
        SessionMode.GroupProject => "小组作业",
        _ => "自定义"
    };

    /// <summary>Stable raw value used by Mac JSON ("labMeeting", "custom", ...).</summary>
    public static string Raw(this SessionMode mode) => mode switch
    {
        SessionMode.Interview => "interview",
        SessionMode.Academic => "academic",
        SessionMode.LabMeeting => "labMeeting",
        SessionMode.Meeting => "meeting",
        SessionMode.Client => "client",
        SessionMode.Lesson => "lesson",
        SessionMode.Discussion => "discussion",
        SessionMode.GroupProject => "groupProject",
        SessionMode.Custom => "custom",
        _ => "general"
    };

    /// <summary>Tolerant decode: unknown future raw values read as General.</summary>
    public static SessionMode Parse(string? raw) => raw?.Trim() switch
    {
        "interview" => SessionMode.Interview,
        "academic" => SessionMode.Academic,
        "labMeeting" => SessionMode.LabMeeting,
        "meeting" => SessionMode.Meeting,
        "client" => SessionMode.Client,
        "lesson" => SessionMode.Lesson,
        "discussion" => SessionMode.Discussion,
        "groupProject" => SessionMode.GroupProject,
        "custom" => SessionMode.Custom,
        _ => SessionMode.General
    };

    /// <summary>The built-in types; Custom is not one of them (macOS SessionMode.builtIn, 9 values).</summary>
    public static IReadOnlyList<SessionMode> BuiltIn { get; } =
    [
        SessionMode.General, SessionMode.Interview, SessionMode.Academic, SessionMode.LabMeeting,
        SessionMode.Meeting, SessionMode.Client, SessionMode.Lesson, SessionMode.Discussion, SessionMode.GroupProject
    ];

    public static MeetingMode ToMeetingMode(this SessionMode mode) => mode switch
    {
        SessionMode.Interview => MeetingMode.Interview,
        SessionMode.Academic => MeetingMode.Academic,
        SessionMode.LabMeeting => MeetingMode.LabMeeting,
        SessionMode.Meeting => MeetingMode.Meeting,
        SessionMode.Client => MeetingMode.Client,
        SessionMode.Lesson => MeetingMode.Lesson,
        SessionMode.Discussion => MeetingMode.Discussion,
        SessionMode.GroupProject => MeetingMode.GroupProject,
        SessionMode.Custom => MeetingMode.Custom,
        _ => MeetingMode.General
    };

    public static SessionMode FromMeetingMode(MeetingMode mode) => mode switch
    {
        MeetingMode.Interview => SessionMode.Interview,
        MeetingMode.Academic => SessionMode.Academic,
        MeetingMode.LabMeeting => SessionMode.LabMeeting,
        MeetingMode.Meeting => SessionMode.Meeting,
        MeetingMode.Client => SessionMode.Client,
        MeetingMode.Lesson => SessionMode.Lesson,
        MeetingMode.Discussion => SessionMode.Discussion,
        MeetingMode.GroupProject => SessionMode.GroupProject,
        MeetingMode.Custom => SessionMode.Custom,
        _ => SessionMode.General
    };
}

/// <summary>Tolerant Mac JSON: unknown mode strings become General instead of failing the archive.</summary>
public sealed class SessionModeJsonConverter : System.Text.Json.Serialization.JsonConverter<SessionMode>
{
    public override SessionMode Read(ref System.Text.Json.Utf8JsonReader reader, Type type, System.Text.Json.JsonSerializerOptions options) =>
        reader.TokenType == System.Text.Json.JsonTokenType.String ? SessionModes.Parse(reader.GetString()) : SessionMode.General;

    public override void Write(System.Text.Json.Utf8JsonWriter writer, SessionMode value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Raw());
}

/// <summary>One imported material of a preset (port of ContextDocument).</summary>
public sealed record ContextDocument
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D").ToUpperInvariant();
    public string Name { get; init; } = "";
    public IReadOnlyList<string> Pages { get; init; } = [];
    public bool Enabled { get; init; } = true;
    /// <summary>Material slot within the workspace (简历 / 论文 / 议程 …); null for older files.</summary>
    public string? Category { get; init; }
    public string? MarkdownPath { get; init; }
    public string? SourceFormat { get; init; }

    /// <summary>Opaque Mac DocumentContent (pipeline + blocks); kept verbatim for round-trips.</summary>
    public System.Text.Json.JsonElement? Content { get; init; }

    public int CharacterCount => Pages.Sum(page => page.Length);
}

/// <summary>Mac's context.json: the inputs the answers were generated with.</summary>
public sealed record SessionContextSnapshot
{
    public string Mode { get; init; } = "meeting";
    public string Style { get; init; } = "concise";
    public string Preferences { get; init; } = "";
    public string Profile { get; init; } = "";
    public IReadOnlyList<ContextDocument> Documents { get; init; } = [];
    public string? EffectiveMode { get; init; }
    public string? TypeName { get; init; }
    public string? Experience { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("previousSessionID")]
    public string? PreviousSessionId { get; init; }
    public Glossary? Glossary { get; init; }

    /// <summary>Unknown/future Mac context fields are preserved on round-trips.</summary>
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>A reusable meeting setup: mode, style, preferences, profile and documents.</summary>
public sealed record SessionPreset
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");
    public string Name { get; init; } = "";
    public SessionMode Mode { get; init; } = SessionMode.General;
    public AnswerStyle Style { get; init; } = new();
    public string Preferences { get; init; } = "";
    public string Profile { get; init; } = "";
    public IReadOnlyList<ContextDocument> Documents { get; init; } = [];
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Starting points for the answer-requirements field, from the mode profile.</summary>
    public static string TemplatePreferences(SessionMode mode) =>
        ModeProfile.For(mode.ToMeetingMode()).TemplatePreferences;

    public int EnabledDocumentCount => Documents.Count(document => document.Enabled);
}
