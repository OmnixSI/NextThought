namespace NextThought.Core;

/// <summary>
/// E26: what a scene sets up when a meeting starts. Mirrors Mac <c>SceneSettings</c>: the fields are
/// stored only when they differ from the built-in defaults (MeetingTypeDefaults + ScenePreset), so a
/// later change to a default still reaches everyone who never touched the row.
/// </summary>
public sealed record SceneDefaults(
    string LiveMode = "只记录",
    string AnswerStyle = "简洁",
    string NotesTemplate = "general",
    string CueStyle = "只看要点")
{
    /// <summary>The built-in row of one scene (MeetingTypeDefaults + ScenePreset.cueStyle).</summary>
    public static SceneDefaults For(MeetingMode mode)
    {
        var defaults = MeetingTypeDefaults.For(mode);
        return new SceneDefaults(
            defaults.LiveMode == NextThought.Core.LiveMode.Prompting ? "自动回答" : "只记录",
            defaults.Style == NextThought.Core.AnswerStyle.Detailed ? "详细" : "简洁",
            defaults.Template.Raw(),
            ScenePreset.For(mode).CueStyle == NextThought.Core.CueStyle.Script ? "逐句" : "只看要点");
    }

    /// <summary>True when every field still equals the built-in row.</summary>
    public bool IsDefaultFor(MeetingMode mode) => this == For(mode);

    /// <summary>True when the person changed at least one field of the row (Mac isCustomised).</summary>
    public bool IsCustomisedFor(MeetingMode mode) => !IsDefaultFor(mode);

    /// <summary>True when the stored row has no override left.</summary>
    public bool IsEmptyFor(MeetingMode mode)
    {
        var defaults = For(mode);
        return (LiveMode.Length == 0 || LiveMode == defaults.LiveMode)
               && (AnswerStyle.Length == 0 || AnswerStyle == defaults.AnswerStyle)
               && (NotesTemplate.Length == 0 || NotesTemplate == defaults.NotesTemplate)
               && (CueStyle.Length == 0 || CueStyle == defaults.CueStyle);
    }
}
