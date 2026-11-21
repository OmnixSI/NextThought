namespace NextThought.Desktop.Views;

/// <summary>
/// J8: Mac 3.4.0 <c>LiveSessionAcceptanceSelfTest.LiveRenderCount</c>. SwiftUI re-evaluates a view
/// body when observable state changes; Windows has no body evaluation, so the equivalent are the
/// explicit refresh points the live page runs. Counting them answers the same question as the Mac
/// walkthrough: does a long meeting keep the page's work proportional to the new lines, instead of
/// rebuilding the whole transcript each time?
/// <para>
/// Read by <c>--demo-churn=&lt;file&gt;</c> (Mac <c>NEXTTHOUGHT_DEMO_CHURN</c>) and by
/// <c>--self-test=live-churn</c>.
/// </para>
/// </summary>
public static class LiveRenderCount
{
    /// <summary>The page itself was attached to a state.</summary>
    public static int Page;
    /// <summary>The one-row top bar (timer/status/scene/answer mode) was re-rendered.</summary>
    public static int TopBar;
    /// <summary>The more menu was opened/popped up.</summary>
    public static int MoreMenu;
    /// <summary>The transcript list changed.</summary>
    public static int Transcript;
    /// <summary>The right column (实时纪要 / 问答 / 我的笔记) changed tab or content.</summary>
    public static int RightColumn;
    /// <summary>The partial/answer area was re-rendered.</summary>
    public static int Partial;
    /// <summary>One transcript row was added.</summary>
    public static int Line;

    public static void Reset()
    {
        Page = TopBar = MoreMenu = Transcript = RightColumn = Partial = Line = 0;
    }

    public static Dictionary<string, int> Snapshot() => new()
    {
        ["page"] = Page,
        ["topBar"] = TopBar,
        ["moreMenu"] = MoreMenu,
        ["transcript"] = Transcript,
        ["rightColumn"] = RightColumn,
        ["partial"] = Partial,
        ["line"] = Line
    };

    public static string Report() =>
        $"page {Page} top {TopBar} more {MoreMenu} transcript {Transcript} right {RightColumn} partial {Partial} line {Line}";
}
