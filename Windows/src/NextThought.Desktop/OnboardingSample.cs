namespace NextThought.Desktop;

/// <summary>
/// E3: the scripted sample meeting of the first-run guide (port of Mac
/// <c>Sources/NextThought/OnboardingSampleMeeting.swift</c>). It plays no audio, asks for no
/// permission and calls no model; the lines and notes are written ahead of time.
/// </summary>
public static class OnboardingSample
{
    public const double Duration = 34;

    public static string Title => UiText.T("面试示例");

    public static IReadOnlyList<OnboardingSampleLine> Lines { get; } =
    [
        new(0, Other, UiText.T("先聊聊你简历里的检索项目。上周加入重排之后，结果怎么样？"), 0.8),
        new(1, Me, UiText.T("这组验证集上，Recall@10 从 72% 到了 78%，但候选数量也同时增加了。"), 5.0),
        new(2, Other, UiText.T("那提升来自重排，还是更多候选？需要怎样的对照才能判断？"), 10.5),
        new(3, Me, UiText.T("现在还不能确定。我会固定候选数量，再做一组去掉重排的消融。"), 16.0),
        new(4, Other, UiText.T("好，还要看延迟分布。平均值可能会掩盖长尾，别只报一个数字。"), 21.0),
        new(5, Me, UiText.T("我会同时报告 P95 延迟，并把失败样例一起整理出来。"), 26.5),
        new(6, Other, UiText.T("最后一个问题：结论换一组数据还成立吗？"), 29.5)
    ];

    /// <summary>The notes grow while the call goes on.</summary>
    public static IReadOnlyList<OnboardingSampleTakeaway> Takeaways { get; } =
    [
        new(0, DecisionLabel, UiText.T("已答：Recall@10 从 72% 到 78%，候选数同时变化"), 8.5),
        new(1, DecisionLabel, UiText.T("追问：提升来自重排，还是更多候选？"), 14.0),
        new(2, TodoLabel, UiText.T("复盘：固定候选数，补充去掉重排的消融"), 19.0),
        new(3, DecisionLabel, UiText.T("追问：报告延迟分布，关注长尾"), 24.0),
        new(4, TodoLabel, UiText.T("复盘：补充 P95 延迟与失败样例"), 28.0),
        new(5, TodoLabel, UiText.T("复盘：准备换一组数据的验证"), 32.0)
    ];

    public static OnboardingSampleSuggestion Suggestion { get; } = new(
        UiText.T("提升来自重排，还是更多候选？"),
        UiText.T("目前不能区分。需要固定候选数量，对比有无重排；同时记录检索质量和延迟，再判断提升来源。"),
        13.0);

    private static string Other => UiText.T("面试官");
    private static string Me => UiText.T("我");
    private static string DecisionLabel => UiText.T("记录");
    private static string TodoLabel => UiText.T("待办");
}

public sealed record OnboardingSampleLine(int Id, string Speaker, string Text, double At)
{
    public bool IsMe => Speaker == UiText.T("我");
}

public sealed record OnboardingSampleTakeaway(int Id, string Kind, string Text, double At);

public sealed record OnboardingSampleSuggestion(string Question, string Answer, double At);
