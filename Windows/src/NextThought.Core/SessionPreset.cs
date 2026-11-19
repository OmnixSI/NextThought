namespace NextThought.Core;

/// <summary>Stored session mode (port of SessionMode); mirrors the app's MeetingMode.</summary>
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

/// <summary>One imported material of a preset (port of ContextDocument).</summary>
public sealed record ContextDocument
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");
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
