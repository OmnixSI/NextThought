namespace NextThought.Core;

/// <summary>
/// A single meeting's choices (port of MeetingPlan): applying them makes a value copy and never
/// changes the reusable series. Materials are filtered to the baseline documents, and the previous
/// meeting's transcript is attached when the carry selection asks for it.
/// </summary>
public sealed record MeetingPlan
{
    public string? Title { get; init; }
    public MeetingMode Mode { get; init; } = MeetingMode.General;
    public string? CustomTypeName { get; init; }
    public MeetingMode CustomBaseMode { get; init; } = MeetingMode.General;
    public MeetingCarry Carry { get; init; } = MeetingCarry.Standard;
    public IReadOnlySet<string> BaselineDocumentIds { get; init; } = new HashSet<string>();
    public string? PreviousSessionId { get; init; }
    /// <summary>Scene-level answer length; nil keeps the series/type default.</summary>
    public string? SceneStyle { get; init; }

    public MeetingMode EffectiveMode => Mode == MeetingMode.Custom ? CustomBaseMode : Mode;

    /// <summary>The series as this meeting may use it; the original series is untouched.</summary>
    public WorkspaceSeries PreparedWorkspace(WorkspaceSeries series, ContextDocument? previousTranscript = null)
    {
        var prepared = Carry.WorkspaceSnapshot(series, BaselineDocumentIds);
        var effective = EffectiveMode;
        var seriesMode = Enum.TryParse<MeetingMode>(series.Mode, true, out var parsed) ? parsed : effective;
        var seriesDefault = DefaultStyle(seriesMode);
        // Only the type's own default preferences are replaced; explicit requirements survive.
        var preferences = string.IsNullOrEmpty(series.Preferences)
            ? ModeProfile.For(seriesMode).TemplatePreferences
            : series.Preferences;
        if (seriesMode != effective && preferences == ModeProfile.For(seriesMode).TemplatePreferences)
            preferences = ModeProfile.For(effective).TemplatePreferences;
        var style = series.Style is { Length: > 0 } seriesStyle ? seriesStyle : seriesDefault;
        if (seriesMode != effective) style = DefaultStyle(effective);
        if (style == seriesDefault) style = SceneStyle ?? DefaultStyle(effective);
        prepared = prepared with
        {
            Mode = Raw(Mode),
            Style = style,
            Preferences = preferences,
            CustomTypeName = Mode == MeetingMode.Custom ? CustomTypeName : prepared.CustomTypeName,
            CustomBaseMode = Mode == MeetingMode.Custom ? Raw(effective) : prepared.CustomBaseMode
        };
        if (!Carry.Transcript || previousTranscript is null) return prepared;
        return prepared with
        {
            Documents = [.. prepared.Documents, previousTranscript],
            DocumentIds = [.. prepared.DocumentIds, previousTranscript.Id]
        };
    }

    private static string DefaultStyle(MeetingMode mode) =>
        mode == MeetingMode.LabMeeting ? "detailed" : "concise";

    private static string Raw(MeetingMode mode)
    {
        var name = mode.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
