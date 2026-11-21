namespace NextThought.Core;

/// <summary>A translation revision of one transcript line (port of SessionTranscriptTranslation).</summary>
public sealed record SessionTranscriptTranslation
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");
    public string Text { get; init; } = "";
    public string? InputRevision { get; init; }
}

/// <summary>
/// The complete on-disk meeting record (port of SessionRecord): transcript, translations,
/// question/answer events and the summary. Windows stores the same fields through SessionData;
/// this aggregate is used when importing or exporting archive JSON.
/// </summary>
public sealed record SessionRecord
{
    /// <summary>Unknown Mac fields (workspaceID, documents, mode…) are kept for a lossless round-trip.</summary>
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalProperties { get; init; }

    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = "";
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; init; }
    public string Summary { get; init; } = "";
    public string? SummaryError { get; init; }
    public IReadOnlyList<TranscriptLine> Transcript { get; init; } = [];
    public IReadOnlyList<SessionTranscriptTranslation> Translations { get; init; } = [];
    public IReadOnlyList<SessionQuestionEvent> Questions { get; init; } = [];
    public IReadOnlyList<SessionAnswerEvent> Answers { get; init; } = [];
    public IReadOnlyList<string> SpeakerOrder { get; init; } = [];

    public static SessionRecord FromSessionData(SessionData data) => new()
    {
        Id = data.Meta.Id,
        Title = data.Meta.Title,
        StartedAt = data.Meta.StartedAt,
        EndedAt = data.Meta.EndedAt,
        Summary = data.Summary,
        SummaryError = data.Meta.SummaryError,
        Transcript = data.Lines,
        Answers = data.Answers.Select(answer => new SessionAnswerEvent
        {
            Id = answer.Id.ToString("D"),
            Date = answer.Date,
            Question = answer.Question,
            Answer = answer.Answer,
            Model = answer.Model,
            Automatic = answer.Automatic
        }).ToList()
    };

    public SessionData ToSessionData()
    {
        var meta = new SessionMeta
        {
            Id = Id,
            Title = Title,
            StartedAt = StartedAt,
            EndedAt = EndedAt,
            SummaryError = SummaryError
        };
        var answers = Answers.Select(answer => new AnswerRecord
        {
            Id = Guid.TryParse(answer.Id, out var answerId) ? answerId : Guid.NewGuid(),
            Date = answer.Date,
            Question = answer.Question,
            Answer = answer.Answer,
            Model = answer.Model,
            Automatic = answer.Automatic
        }).ToList();
        return new SessionData(meta, Transcript, answers, Summary);
    }
}
