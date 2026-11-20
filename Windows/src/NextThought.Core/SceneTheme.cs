namespace NextThought.Core;

/// <summary>D6: the ten scene pigments, glyph symbols, promise and three moments from Stage.swift.</summary>
public sealed record SceneTheme(
    MeetingMode Mode, string TintLight, string TintDark,
    string DeepLight, string DeepDark, string Symbol, string Promise,
    IReadOnlyList<string> Moments)
{
    public static SceneTheme For(MeetingMode mode) => mode switch
    {
        MeetingMode.Interview => new(MeetingMode.Interview, "#3f5b8c", "#93aee0", "#2c4370", "#6f8cc4", "person.crop.rectangle", "被问到时，答案就在镜头旁", ["被问到时，答案就在镜头旁", "听到问题自动给出回答", "提词贴着摄像头，不用低头"]),
        MeetingMode.LabMeeting => new(MeetingMode.LabMeeting, "#4a7d68", "#8cc7ad", "#355e4d", "#66a88c", "flask", "专心讨论，导师的话一句不漏", ["专心讨论，导师的话一句不漏", "默认只记录，被问住再求助", "纪要按进展、意见、下一步整理"]),
        MeetingMode.Academic => new(MeetingMode.Academic, "#775a9c", "#b9a0e0", "#5a417c", "#957bc2", "graduationcap", "跟着讲稿走，问答环节有底", ["跟着讲稿走，问答环节有底", "认出当前幻灯片，提示这一页要点", "听众提问时自动给出回答"]),
        MeetingMode.GroupProject => new(MeetingMode.GroupProject, "#a5683c", "#e0a476", "#824e28", "#c28555", "person.3", "谁做什么、哪天交，散会就清楚", ["谁做什么、哪天交，散会就清楚", "记下每个人领到的任务", "意见不一致时帮你找折中"]),
        MeetingMode.Client => new(MeetingMode.Client, "#2f7a8c", "#7cc3d4", "#1f5c6b", "#57a3b5", "person.2", "听懂需求，答应的事不落空", ["听懂需求，答应的事不落空", "客户提问时给出有依据的回应", "价格和承诺以资料为准"]),
        MeetingMode.Meeting => new(MeetingMode.Meeting, "#5b6573", "#a9b3c1", "#434c58", "#8691a1", "briefcase", "决定、待办、风险，会后即得", ["决定、待办、风险，会后即得", "默认只记录，不打扰", "需要时一键求助"]),
        MeetingMode.Lesson => new(MeetingMode.Lesson, "#5f7d40", "#a8cb85", "#475f2d", "#86ac63", "book", "只管听，笔记自己长出来", ["只管听，笔记自己长出来", "跟着课件记录要点", "没听懂的地方随时问"]),
        MeetingMode.Discussion => new(MeetingMode.Discussion, "#a5526b", "#e395ad", "#813a51", "#c4748d", "bubble.left.and.bubble.right", "各方观点摆清楚，结论自然来", ["各方观点摆清楚，结论自然来", "区分每个人说了什么", "观点对照表一眼看清分歧"]),
        MeetingMode.General => new(MeetingMode.General, "#6b6862", "#b5b2ab", "#4d4a45", "#93908a", "sparkles", "先录下来，其他的之后再说", ["先录下来，其他的之后再说", "只记录，需要时求助", "回答简短"]),
        _ => new(MeetingMode.Custom, "#6b6862", "#b5b2ab", "#4d4a45", "#93908a", "slider.horizontal.3", "先录下来，其他的之后再说", ["先录下来，其他的之后再说", "只记录，需要时求助", "回答简短"])
    };
}
