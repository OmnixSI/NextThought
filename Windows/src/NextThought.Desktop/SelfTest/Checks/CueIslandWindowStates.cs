using NextThought.Core;
using NextThought.Desktop.Views.Island;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

/// <summary>
/// §6.6 / C5–C9: the scripted island states the screenshot batch and the island self-check drive.
/// The capture copy is the Mac demo script (DemoScript / DemoIslands); it lives next to the other
/// self-test code because it is demonstration data, not interface wording.
/// </summary>
public partial class CueIslandWindow
{
    /// <summary>
    /// C5–C9: puts the island into one of the Mac screenshot states. The demo data is the Mac
    /// demo script (DemoScript), the layout state follows the same model flags the real phases use
    /// (pinned for the deck, always-open for the kept-open states, hover for the launcher), and the
    /// clock/countdown are the Mac screenshot values.
    /// </summary>
    public void ApplyStateForCheck(string stateName)
    {
        trackingSuspended = true;
        var check = (stateName ?? "cue").Trim().ToLowerInvariant();
        if (check == "collapsed") check = "compact";

        // Reset to a quiet meeting first.
        state.Recording = false;
        state.ListeningPaused = false;
        state.AnswerHistoryCursor = null;
        state.AnswerText = "";
        state.DetectedQuestion = "";
        state.SystemPartial = "";
        state.SystemPartialTranslation = "";
        state.IslandTranslation = "";
        state.LiveTranslationEnabled = false;
        state.PresenterLayoutActive = false;
        state.Settings.CueAlwaysOpen = false;
        state.Settings.AutomaticAnswers = true;
        state.Settings.PresenterSlideNotes = true;
        state.SelectedMode = MeetingMode.Interview;
        state.CueIslandDemoCandidate("");
        state.CueIslandDemoSeriesName(null);
        state.CueIslandDemoAnswerReady(true);
        state.ClearIslandSlide();
        model.Backdrop = true;
        model.ForcedPhase = null;
        model.DemoAutoEndSeconds = null;
        model.DemoClock = "12:36";
        model.CollapsedKey = null;
        model.ForceCue = false;
        model.SetHovering(false);
        model.ClearPinned();

        switch (check)
        {
            case "compact":
                state.Recording = true;
                break;
            case "open":
                state.Recording = true;
                state.Settings.CueAlwaysOpen = true;
                state.DetectedQuestion = DemoScript.LiveQuestion;
                state.AnswerText = DemoScript.LiveAnswer;
                model.SetHovering(true);
                break;
            case "hearing":
                state.Recording = true;
                state.SystemPartial = DemoScript.LivePartialTranscript;
                state.CueIslandDemoCandidate(DemoScript.LivePartialTranscript);
                break;
            case "listening":
                state.Recording = true;
                state.Settings.CueAlwaysOpen = true;
                state.SystemPartial = DemoIslands.For("pinned").ExtraTranscriptLine ?? "";
                state.SystemPartialTranslation = DemoIslands.For("pinned").ExtraTranscriptTranslation ?? "";
                state.LiveTranslationEnabled = true;
                break;
            case "deck":
                state.Recording = true;
                state.CueIslandDemoSeriesName("字节后端二面");
                state.SystemPartial = DemoScript.LivePartialTranscript;
                model.Pin();
                break;
            case "launcher":
                state.Recording = false;
                model.SetHovering(true);
                break;
            case "bilingual":
                state.Recording = true;
                state.Settings.CueAlwaysOpen = true;
                state.DetectedQuestion = DemoScript.IslandBilingualQuestion;
                state.AnswerText = DemoScript.IslandBilingualAnswer;
                state.IslandTranslation = DemoScript.IslandBilingualTranslation;
                break;
            case "pinned":
                state.Recording = true;
                state.Settings.CueAlwaysOpen = true;
                state.DetectedQuestion = DemoScript.IslandBilingualQuestion;
                state.AnswerText = DemoScript.IslandBilingualAnswer;
                state.LiveTranslationEnabled = true;
                state.SystemPartial = DemoIslands.For("pinned").ExtraTranscriptLine ?? "";
                state.SystemPartialTranslation = DemoIslands.For("pinned").ExtraTranscriptTranslation ?? "";
                break;
            case "slide":
                state.Recording = true;
                state.CueIslandDemoSeriesName("字节后端二面");
                state.PresenterLayoutActive = true;
                state.Settings.PresenterSlideNotes = true;
                state.ApplyIslandSlide("后端二面 · 项目介绍.pptx", 4, 12,
                    "缓存一致性：先更新数据库，再删除缓存",
                    [
                        "先说规模：日均 2 亿次读取，P99 从 120 ms 降到 18 ms",
                        "一致性：先更新数据库再删缓存，binlog 订阅兜底",
                        "删除失败进重试队列，热点 key 延迟双删",
                        "我负责方案设计、灰度上线和回滚预案"
                    ],
                    "讲完数字，接着讲上线时踩过的坑", covered: 1);
                break;
            case "autoend":
                state.Recording = true;
                state.SystemPartial = DemoScript.LivePartialTranscript;
                model.DemoAutoEndSeconds = 42;
                break;
            default:
                state.Recording = true;
                state.DetectedQuestion = DemoScript.LiveQuestion;
                state.AnswerText = DemoScript.LiveAnswer;
                break;
        }
        model.Refresh();
        SyncLayout();
        SettleLayoutForCheck();
        ApplyFrame();
    }

    /// <summary>Puts the morph at its final frame: the screenshot states are static.</summary>
    public void SettleLayoutForCheck()
    {
        StopSpring();
        contentWidth = model.TargetContentWidth;
        expand = model.Idle ? 0 : 1;
        ApplyFrame();
    }
}
