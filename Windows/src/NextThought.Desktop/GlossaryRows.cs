namespace NextThought.Desktop;

/// <summary>
/// E22: one transcript line found through the glossary, with the spelling that matched. A line
/// found only through a recorded misheard spelling carries MisheardMark (GlossaryView.swift).
/// </summary>
public sealed record GlossaryTranscriptHitRow(TranscriptRow Row, string Form, bool IsMisheard)
{
    public string Text => Row.Text;
    public bool HasMisheardMark => IsMisheard;
    public string MisheardMarkText => GlossMisheardMark(Form);
    public string MisheardTooltip => UiText.T("这是“术语和人名”里记下的听错写法，搜索时一并找出来");

    /// <summary>MisheardMark's line: 匹配到听错的写法：&lt;form&gt;.</summary>
    public static string GlossMisheardMark(string form) => UiText.Format("匹配到听错的写法：%@", form);
}
