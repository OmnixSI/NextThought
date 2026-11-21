namespace NextThought.Core;

/// <summary>D6: the ten scene pigments, glyph symbols, promise and three moments from Stage.swift.</summary>
public sealed record SceneTheme(
    MeetingMode Mode, string TintLight, string TintDark,
    string DeepLight, string DeepDark, string Symbol, string Promise,
    IReadOnlyList<string> Moments)
{
    /// <summary>The scene's tint for the current appearance (Mac <c>Color.dynamic(light:dark:)</c>).</summary>
    public string Tint(bool dark) => dark ? TintDark : TintLight;

    /// <summary>The darker end of the pigment gradient (Mac <c>deep</c>; currently reserved).</summary>
    public string Deep(bool dark) => dark ? DeepDark : DeepLight;

    public static SceneTheme For(MeetingMode mode) => mode switch
    {
        MeetingMode.Interview => new(MeetingMode.Interview, "#3f5b8c", "#93aee0", "#2c4370", "#6f8cc4", "person.crop.rectangle", "被问到时，答案就在镜头旁",
            ["听到问题自动给出回答", "提词贴着摄像头，不用低头", "会后逐题复盘，存进题库"]),
        MeetingMode.LabMeeting => new(MeetingMode.LabMeeting, "#4a7d68", "#8cc7ad", "#355e4d", "#66a88c", "flask", "专心讨论，导师的话一句不漏",
            ["默认只记录，被问住再求助", "纪要按进展、意见、下一步整理", "鱼骨图梳理卡住的问题"]),
        MeetingMode.Academic => new(MeetingMode.Academic, "#775a9c", "#b9a0e0", "#5a417c", "#957bc2", "graduationcap", "跟着讲稿走，问答环节有底",
            ["认出当前幻灯片，提示这一页要点", "听众提问时自动给出回答", "纪要记下被问了什么、怎么答更好"]),
        MeetingMode.GroupProject => new(MeetingMode.GroupProject, "#a5683c", "#e0a476", "#824e28", "#c28555", "person.3", "谁做什么、哪天交，散会就清楚",
            ["记下每个人领到的任务", "意见不一致时帮你找折中", "纪要按方案、分工、截止时间整理"]),
        MeetingMode.Client => new(MeetingMode.Client, "#2f7a8c", "#7cc3d4", "#1f5c6b", "#57a3b5", "person.2", "听懂需求，答应的事不落空",
            ["客户提问时给出有依据的回应", "价格和承诺以资料为准", "纪要列出需求、顾虑和我们答应的事"]),
        MeetingMode.Meeting => new(MeetingMode.Meeting, "#5b6573", "#a9b3c1", "#434c58", "#8691a1", "briefcase", "决定、待办、风险，会后即得",
            ["默认只记录，不打扰", "需要时一键求助", "纪要按决定、待办、风险整理"]),
        MeetingMode.Lesson => new(MeetingMode.Lesson, "#5f7d40", "#a8cb85", "#475f2d", "#86ac63", "book", "只管听，笔记自己长出来",
            ["跟着课件记录要点", "没听懂的地方随时问", "笔记按提纲、概念、疑问整理"]),
        MeetingMode.Discussion => new(MeetingMode.Discussion, "#a5526b", "#e395ad", "#813a51", "#c4748d", "bubble.left.and.bubble.right", "各方观点摆清楚，结论自然来",
            ["区分每个人说了什么", "观点对照表一眼看清分歧", "纪要按议题、观点、结论整理"]),
        MeetingMode.General => new(MeetingMode.General, "#6b6862", "#b5b2ab", "#4d4a45", "#93908a", "sparkles", "先录下来，其他的之后再说",
            ["只记录，需要时求助", "回答简短", "纪要按总结、决定、待办整理"]),
        _ => new(MeetingMode.Custom, "#6b6862", "#b5b2ab", "#4d4a45", "#93908a", "slider.horizontal.3", "先录下来，其他的之后再说",
            ["只记录，需要时求助", "回答简短", "纪要按总结、决定、待办整理"])
    };
}
