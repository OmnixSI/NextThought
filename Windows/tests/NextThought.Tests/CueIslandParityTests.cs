using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// §6.6 / C5–C9: the cue island is redone against CueIsland.swift. These are the contract tests
/// written before the implementation (they describe the six states, the deck's four actions, the
/// slide/coach/auto-end extras, the bilingual rows, the scene edge and the morph spring).
/// The UI itself is source-scanned like the other desktop parity tests; the geometry and timing
/// numbers are checked against <see cref="IslandLayout"/> directly.
/// </summary>
public sealed class CueIslandParityTests
{

    private static string ReadCheck(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return File.ReadAllText(Path.Combine([directory.FullName, "Windows", "src", "NextThought.Desktop", .. parts]));
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root");
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "CueIslandWindow.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static string IslandXaml => Read("Windows", "src", "NextThought.Desktop", "Views", "CueIslandWindow.axaml");
    private static string IslandCode => Read("Windows", "src", "NextThought.Desktop", "Views", "CueIslandWindow.axaml.cs");
    private static string IslandModel => Read("Windows", "src", "NextThought.Desktop", "Views", "Island", "CueIslandViewModel.cs");
    private static string IslandData => Read("Windows", "src", "NextThought.Desktop", "AppState.Cue.cs");

    [Fact]
    public void C5TheSixStatesUseTheMacWidthsAndTheNotchMetrics()
    {
        // The Mac island hangs from a 186 pt notch with a 32 pt bar; opening relaxes 6→14 pt flare.
        var metrics = IslandMetrics.Notch;
        Assert.True(metrics.HasNotch);
        Assert.Equal(186, metrics.NotchWidth);
        Assert.Equal(32, metrics.BarHeight);

        var ears = IslandLayout.EarsWidth(metrics);
        Assert.Equal(186 + 2 * 76, ears);
        Assert.Equal(ears, IslandLayout.ContentWidth(CueIslandPhase.Compact, metrics, CueIslandWidth.Regular));
        Assert.Equal(440, IslandLayout.ContentWidth(CueIslandPhase.Hearing, metrics, CueIslandWidth.Wide));
        Assert.Equal(540, IslandLayout.ContentWidth(CueIslandPhase.Cue, metrics, CueIslandWidth.Regular));
        Assert.Equal(660, IslandLayout.ContentWidth(CueIslandPhase.Cue, metrics, CueIslandWidth.Wide));
        Assert.Equal(470, IslandLayout.ContentWidth(CueIslandPhase.Deck, metrics, CueIslandWidth.Narrow));
        Assert.Equal(500, IslandLayout.ContentWidth(CueIslandPhase.Launcher, metrics, CueIslandWidth.Regular));
        Assert.Equal(470, IslandLayout.ContentWidth(CueIslandPhase.AutoEnd, metrics, CueIslandWidth.Regular));
        Assert.Equal(540, IslandLayout.ContentWidth(CueIslandPhase.Listening, metrics, CueIslandWidth.Regular));
        Assert.Equal(540, IslandLayout.ContentWidth(CueIslandPhase.Slide, metrics, CueIslandWidth.Regular));

        Assert.Equal(6, IslandLayout.Flare(idle: true, metrics));
        Assert.Equal(14, IslandLayout.Flare(idle: false, metrics));
        Assert.Equal(14, IslandLayout.Radius(idle: true, metrics));
        Assert.Equal(24, IslandLayout.Radius(idle: false, metrics));
    }

    [Fact]
    public void C5TheXamlHasOnePanelPerStateAndTheBarElements()
    {
        foreach (var panel in new[]
                 {
                     "BarRow", "HearingPanel", "ListeningPanel", "CuePanel", "SlidePanel", "DeckPanel",
                     "LauncherPanel", "AutoEndPanel", "ControlsRow"
                 })
            Assert.Contains("x:Name=\"" + panel + "\"", IslandXaml);
        // Top bar: status dot, the row of sound dots, and the clock (or the coach) on the right.
        Assert.Contains("x:Name=\"StatusDot\"", IslandXaml);
        Assert.Contains("x:Name=\"SoundWave\"", IslandXaml);
        Assert.Contains("x:Name=\"ClockText\"", IslandXaml);
        Assert.Contains("x:Name=\"CoachEar\"", IslandXaml);
        Assert.Contains("x:Name=\"SlidePageText\"", IslandXaml);
        // The island's copy (the model owns the strings, the view binds them).
        Assert.Contains("对方在说", IslandXaml);
        Assert.Contains("开启同声传译", IslandXaml);
        Assert.Contains("正在想", IslandModel + IslandData);
        Assert.Contains("被问到时，回答会出现在这里。", IslandModel + IslandData);
        Assert.Contains("开始一场", IslandXaml);
        Assert.Contains("点一个场景，立即开始。", IslandXaml);
        Assert.Contains("打开主窗口", IslandXaml);
    }

    [Fact]
    public void C6TheDeckHasTheStatusRowTitleSceneTagModeAndHeardLine()
    {
        foreach (var element in new[]
                 {
                     "DeckTitleText", "DeckSceneTag", "DeckModeText", "DeckWarningDot", "DeckHeardSpeaker",
                     "DeckHeardText", "DeckHeardTranslation"
                 })
            Assert.Contains("x:Name=\"" + element + "\"", IslandXaml);
        Assert.Contains("DeckHeardSpeaker", IslandXaml);
        Assert.Contains("对方", IslandXaml);
        Assert.Contains("自动回答", IslandXaml + IslandModel);
        Assert.Contains("只记录", IslandXaml + IslandModel);
        Assert.Contains("已暂停", IslandData);
    }

    [Fact]
    public void C6TheDeckHasTheFourPillActionsBoundToTheMacCommands()
    {
        foreach (var button in new[]
                 {
                     "DeckForceAnswerButton", "DeckRecordOnlyButton", "DeckPauseButton", "DeckEndButton",
                     "DeckPreviousAnswerButton"
                 })
            Assert.Contains("x:Name=\"" + button + "\"", IslandXaml);
        // The four visible actions: answer now, switch to record-only (or back), pause, end.
        Assert.Contains("Command=\"{Binding ForceAnswerCommand}\"", IslandXaml);
        Assert.Contains("Command=\"{Binding ToggleRecordOnlyCommand}\"", IslandXaml);
        Assert.Contains("Command=\"{Binding TogglePauseCommand}\"", IslandXaml);
        Assert.Contains("Command=\"{Binding EndMeetingCommand}\"", IslandXaml);
        Assert.Contains("立即回答", IslandXaml);
        Assert.Contains("改为只记录", IslandXaml + IslandModel);
        Assert.Contains("暂停", IslandXaml);
        Assert.Contains("结束", IslandXaml);
        // The four pills are the Mac IslandAction: a white prominent one and three quiet ones
        // (end is destructive, red seal text plus a red square icon).
        Assert.Contains("Classes=\"islandAction prominent\"", IslandXaml);
        Assert.Contains("Classes=\"islandAction destructive\"", IslandXaml);
        // Only the buttons take clicks; the window stays click-through everywhere else.
        Assert.Contains("SetClickThrough", IslandCode);
        Assert.Contains("InteractiveControls", IslandCode);
        Assert.Contains("CursorOverControl", IslandCode);
    }

    [Fact]
    public void C7TheSlideNotesCoachHintAndAutoEndAreBound()
    {
        foreach (var element in new[]
                 {
                     "SlideHeaderText", "SlidePointsHost", "SlideTransitionText", "SlideEmptyHint",
                     "AutoEndText", "AutoEndIcon", "KeepRecordingButton", "EndNowButton"
                 })
            Assert.Contains("x:Name=\"" + element + "\"", IslandXaml);
        Assert.Contains("我的讲稿", IslandData);
        Assert.Contains("下一页", IslandData);
        Assert.Contains("正在准备这一页的要点", IslandModel + IslandData);
        // C7: the auto-end reminder counts down and offers 继续记录 / 现在结束.
        Assert.Contains("好像没人说话了，%lld 秒后结束记录", IslandData);
        Assert.Contains("Command=\"{Binding KeepRecordingCommand}\"", IslandXaml);
        Assert.Contains("Command=\"{Binding EndMeetingCommand}\"", IslandXaml);
        Assert.Contains("现在结束", IslandXaml);
        // C7: the coach hint rides on the notch ear.
        Assert.Contains("CoachText", IslandXaml);
        Assert.Contains("CueIslandCoachHint", IslandData + IslandModel);
    }

    [Fact]
    public void C8TheBilingualQuestionRowAndTranslationsAreBound()
    {
        // C8: the question of a bilingual answer gets its own row (BilingualAnswer.question),
        // gated by the island's asked mark; the translation lane keeps using IslandTranslation.
        Assert.Contains("CueAsked", IslandXaml);
        Assert.Contains("x:Name=\"CueAskedText\"", IslandXaml);
        Assert.Contains("BilingualAnswer.Question(", IslandData);
        Assert.Contains("IslandTranslation", IslandXaml);
        Assert.Contains("x:Name=\"ListeningTranslationText\"", IslandXaml);
        Assert.Contains("DeckHeardTranslation", IslandXaml);
        Assert.Contains("x:Name=\"HearingTranslationText\"", IslandXaml);
    }

    [Fact]
    public void C9TheShapeHasTheConcaveShouldersTheSceneEdgeAndTheMorphSpring()
    {
        var shape = Read("Windows", "src", "NextThought.Desktop", "Views", "Island", "IslandFrame.cs");
        // The outline: four quad curves, the two shoulders curving out to the screen edge.
        Assert.Contains("QuadraticBezierSegment", shape);
        Assert.Contains("Flare", shape);
        Assert.Contains("Radius", shape);
        // The scene-pigment edge: a gradient that only shows near the lower edge, stroked 1.2 pt.
        Assert.Contains("LinearGradientBrush", IslandModel);
        Assert.Contains("EdgeBrush", IslandXaml);
        // The soft shadow under the frame.
        Assert.Contains("BoxShadow", IslandXaml);

        // The morph spring is the Mac Motion.morph token (response 0.5, damping 0.82).
        Assert.Contains("Motion.Kind.Morph", IslandCode);
        Assert.Contains("Motion.Progress", IslandCode);
        Assert.Contains("Motion.Reduce", IslandCode);
        var morph = MotionTokens.Morph;
        Assert.Equal(0.5, morph.Response);
        Assert.Equal(0.82, morph.DampingFraction);
        // Reduced motion: no animation at all, the island snaps to its final width.
        Assert.Contains("Motion.Reduce", IslandModel);
        // Never steal focus, click through when put away.
        Assert.Contains("WsExNoActivate", IslandCode);
        Assert.Contains("WsExTransparent", IslandCode);
        Assert.Contains("ShowActivated=\"False\"", IslandXaml);
    }

    [Fact]
    public void C9TheHoverDwellIsTheMac022And02Seconds()
    {
        Assert.Equal(0.22, IslandLayout.HoverOpenSeconds);
        Assert.Equal(0.2, IslandLayout.HoverCloseSeconds);
        Assert.Contains("HoverOpenSeconds", IslandCode);
        Assert.Contains("HoverCloseSeconds", IslandCode);
    }

    [Fact]
    public void TheScreenshotBatchCoversEveryIslandStateInBothThemes()
    {
        var runner = Read("Windows", "src", "NextThought.Desktop", "SelfTest", "RunScreenshotsAsync.cs");
        foreach (var state in new[]
                 {
                     "\"cue\"", "\"hearing\"", "\"listening\"", "\"deck\"", "\"launcher\"", "\"pinned\"",
                     "\"autoend\"", "\"compact\"", "\"open\"", "\"slide\"", "\"bilingual\""
                 })
            Assert.Contains(state, runner);
        Assert.Contains("island-", runner);
        Assert.Contains("ApplyStateForCheck", runner);
        Assert.Contains("ClearStateForCheck", runner);
    }

    [Fact]
    public void TheIslandTakesClicksAsAWholeAndWithdrawsWhileSharing()
    {
        var vm = ReadCheck("Views", "Island", "CueIslandViewModel.cs");
        // decisions §3.2 island 5: hover / pinned / peeking -> the whole panel takes clicks.
        Assert.Contains("WantsClicks => !shareAvoiding && (hovering || pinned || Peeking)", vm);
        var window = ReadCheck("Views", "CueIslandWindow.axaml.cs");
        Assert.Contains("model.WantsClicks", window);
        // island 6: NextThought's own window in front keeps the island compact.
        Assert.Contains("MainWindowFront", vm);
        Assert.Contains("var covered = mainWindowFront && !forceCue;", vm);
        // island 7: slide manual tag and the tracked slide source.
        Assert.Contains("SlideIsManual", vm);
        Assert.Contains("SlideIsManual", ReadCheck("Views", "CueIslandWindow.axaml"));
        Assert.Contains("lastSlideSource", ReadCheck("AppState.SlideFollow.cs"));
        // island 9: sharing makes the island withdraw by itself (existing ScreenShareDetector).
        Assert.Contains("if (shareAvoiding) return CueIslandPhase.Hidden;", vm);
        Assert.Contains("ScreenShareDetector.IsSharingActive", window);
        Assert.Contains("model.SetShareAvoiding", window);
    }

    [Fact]
    public void TheSelfTestDrivesEveryIslandState()
    {
        var check = Read("Windows", "src", "NextThought.Desktop", "SelfTest", "Checks", "RunIslandCheckAsync.cs");
        foreach (var state in new[]
                 {
                     "cue", "hearing", "listening", "deck", "launcher", "pinned", "autoend", "compact",
                     "open", "slide", "bilingual"
                 })
            Assert.Contains("\"" + state + "\"", check);
        Assert.Contains("island-check", check);
    }
}
