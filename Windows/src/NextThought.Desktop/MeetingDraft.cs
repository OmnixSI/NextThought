using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// E2: the meeting on the preparation page, before it starts (port of Mac
/// <c>Sources/NextThought/AppState+Meeting.swift</c> <c>MeetingDraft</c>). Kept in memory only.
/// </summary>
public sealed class MeetingDraft
{
    public string Title { get; set; } = "";
    public MeetingMode Mode { get; set; } = MeetingMode.General;
    /// <summary>Name and base type of a custom type ("自定义…").</summary>
    public string CustomTypeName { get; set; } = "";
    public MeetingMode CustomBaseMode { get; set; } = MeetingMode.General;
    /// <summary>The user picked the type: stop guessing it from the title.</summary>
    public bool TypeChosen { get; set; }
    /// <summary>The series the meeting uses once bound (materials are imported into it).</summary>
    public string? SeriesId { get; set; }
    /// <summary>Series the user said this meeting does not belong to.</summary>
    public HashSet<string> DeclinedSeriesIds { get; } = new(StringComparer.Ordinal);
    public MeetingCarry Carry { get; set; } = MeetingCarry.Standard;
    /// <summary>Materials the bound series had before this meeting.</summary>
    public HashSet<string> BaselineDocumentIds { get; set; } = new(StringComparer.Ordinal);
    public string? PreviousSessionId { get; set; }
    /// <summary>When the page opened (files added since then are "这次新加的资料").</summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>The type as the user sees it: the custom name, or the built-in scene.</summary>
    public string TypeDisplayName => Mode == MeetingMode.Custom && CustomTypeName.Trim().Length > 0
        ? CustomTypeName.Trim()
        : UiText.T(SceneTitle(Mode));

    public MeetingMode EffectiveMode => Mode == MeetingMode.Custom ? CustomBaseMode : Mode;

    internal static string SceneTitle(MeetingMode mode) => mode switch
    {
        MeetingMode.General => "通用",
        MeetingMode.Interview => "面试",
        MeetingMode.Academic => "汇报 / 答辩",
        MeetingMode.LabMeeting => "组会",
        MeetingMode.Meeting => "工作会议",
        MeetingMode.Client => "客户沟通",
        MeetingMode.Lesson => "讲座 / 课程",
        MeetingMode.Discussion => "小组讨论",
        MeetingMode.GroupProject => "小组作业",
        _ => "自定义"
    };
}

/// <summary>
/// E2: one line on what a scene sets up (Mac <c>SessionMode.defaultsSummary</c>) and what to add
/// before that kind of meeting (<c>preparationHint</c>). Shortcut names follow G10 for Windows.
/// </summary>
public static class SceneSummary
{
    public static string Defaults(MeetingMode mode) => UiText.T(mode switch
    {
        MeetingMode.General => "会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：总结、要点、决定、待办",
        MeetingMode.Interview => "会中自动回答 · 回答简洁 · 纪要：逐题复盘（存进题库）、说得不好的地方、下一轮",
        MeetingMode.Academic => "会中自动回答，跟着讲稿 · 回答简洁 · 纪要：听众提问逐题复盘（存进题库）、要改的地方、答应的跟进",
        MeetingMode.LabMeeting => "会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答详细 · 纪要：讨论的问题、没讨论透的地方、意见与要求、下一步",
        MeetingMode.Meeting => "会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：决定、待办、风险、下次会议",
        MeetingMode.Client => "会中自动回答 · 回答简洁 · 纪要：需求、顾虑与回应、我们答应的、下一步",
        MeetingMode.Lesson => "会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答详细 · 纪要：要点、概念、例子、我的疑问",
        MeetingMode.Discussion => "会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：各方观点、结论与分歧、分工",
        MeetingMode.GroupProject => "会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：定下的方案、分工、我的任务、下次碰头",
        _ => "选一个类型作为基础，再改名字"
    });

    public static string Hint(MeetingMode mode) => mode switch
    {
        MeetingMode.Interview => UiText.T("简历、职位描述、公司信息"),
        MeetingMode.Academic => UiText.T("讲稿、PPT、论文"),
        MeetingMode.LabMeeting => UiText.T("本周进展、相关论文"),
        MeetingMode.Meeting => UiText.T("议程"),
        MeetingMode.Client => UiText.T("产品资料、报价"),
        MeetingMode.Lesson => UiText.T("课件"),
        MeetingMode.Discussion => UiText.T("讨论题目"),
        MeetingMode.GroupProject => UiText.T("作业要求、评分标准"),
        _ => ""
    };

    /// <summary>Mac TypePicker summary: "名称：默认值 · 建议放：资料".</summary>
    public static string Line(MeetingDraft draft)
    {
        var effective = draft.EffectiveMode;
        var name = draft.Mode == MeetingMode.Custom && draft.CustomTypeName.Trim().Length > 0
            ? draft.CustomTypeName.Trim()
            : UiText.T(MeetingDraft.SceneTitle(draft.Mode));
        var line = UiText.Format("%@：%@", name, Defaults(effective));
        var hint = Hint(effective);
        return hint.Length == 0 ? line : UiText.Format("%@ · 建议放：%@", line, hint);
    }
}

/// <summary>E2: one row in the materials block (Mac FileRow): name, parsed size and any warning.</summary>
public sealed record PrepareMaterialRow(string Name, string Status, bool Ready, bool Failed);
