namespace NextThought.Core;

/// <summary>E26: what a scene sets up when a meeting starts.</summary>
public sealed record SceneDefaults(
    string LiveMode = "自动回答",
    string AnswerStyle = "简洁",
    string NotesTemplate = "标准纪要",
    string CueStyle = "只看要点");
