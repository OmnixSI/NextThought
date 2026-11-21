using Xunit;

namespace NextThought.Tests;

/// <summary>
/// 决议 §3.3（live 1/2/4/5/6/8/10/11/15/16、translation 2/3）：会中页与翻译界面的收尾修复。
/// 逐条对应《WINDOWS_FINAL_DECISIONS.md》里标「修」的条目；测试只读源码/XAML。
/// </summary>
public sealed class LiveDecisionsTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]));

    private static string Live() => Read("Views", "LiveView.axaml");

    [Fact]
    public void Live1ThePauseButtonCarriesTheMacPauseAndPlayGlyphs()
    {
        var icons = Read("Styles", "Icons.axaml");
        Assert.Contains("x:Key=\"IconPause\">&#xF5A2;", icons);
        Assert.Contains("x:Key=\"IconPlay\">&#xF606;", icons);
        var live = Live();
        Assert.Contains("{StaticResource IconPause}", live);
        Assert.Contains("{StaticResource IconPlay}", live);
        Assert.Contains("IsVisible=\"{Binding !ListeningPaused}\"", live);
        Assert.Contains("IsVisible=\"{Binding ListeningPaused}\"", live);
    }

    [Fact]
    public void Live2TheAnswerEngineReadyDotSitsOnTheModeSegmentedControl()
    {
        var live = Live();
        Assert.Contains("x:Name=\"LiveAnswerEngineDot\"", live);
        Assert.Contains("IsVisible=\"{Binding !AnswerEngineReady}\"", live);
    }

    [Fact]
    public void Live4TheWebAnswerToggleIsWiredToTheWebSearchState()
    {
        var live = Live();
        Assert.Contains("x:Name=\"LiveWebAnswerToggle\"", live);
        Assert.Contains("{Binding WebAnswerToggleLabel}", live);
        Assert.Contains("Command=\"{Binding ToggleWebAnswerCommand}\"", live);
        Assert.Contains("IsVisible=\"{Binding CanToggleWebAnswer}\"", live);
        Assert.Contains("{Binding WebLookupStatusText}", live);
        var state = Read("AppState.WebSearch.cs");
        Assert.Contains("public bool CanToggleWebAnswer", state);
        Assert.Contains("public string WebAnswerToggleLabel", state);
        Assert.Contains("public string WebLookupStatusText", state);
        Assert.Contains("public bool ShowsWebAnswer", state);
    }

    [Fact]
    public void Live5TheRaisedAnswerSheetUsesTheMacContactAndAmbientShadow()
    {
        var live = Live();
        Assert.Contains("BoxShadow=\"{DynamicResource LvAnswerCardShadow}\"", live);
        Assert.Contains("0 1 1 0 #121C1B19, 0 10 20 0 #0F1C1B19", live);
        Assert.Contains("0 1 1 0 #66000000, 0 10 20 0 #52000000", live);
    }

    [Fact]
    public void Live6TheAnswerNowButtonIsTheMacFortyPointControl()
    {
        var live = Live();
        var start = live.IndexOf("Command=\"{Binding ForceAnswerCommand}\"", StringComparison.Ordinal);
        Assert.True(start > 0, "立即回答 button");
        var window = live[start..Math.Min(live.Length, start + 260)];
        Assert.Contains("MinHeight=\"{DynamicResource ControlLarge}\"", window);
    }

    [Fact]
    public void Live8SelectingTranscriptTextPausesTheFollow()
    {
        var live = Live();
        Assert.Contains("PointerReleased=\"TranscriptText_PointerReleased\"", live);
        Assert.Contains("KeyUp=\"TranscriptText_KeyUp\"", live);
        var code = Read("Views", "LiveView.axaml.cs");
        Assert.Contains("TranscriptText_PointerReleased", code);
        Assert.Contains("NoticeTextSelection", code);
        Assert.Contains("LiveSelectedText()", code);
    }

    [Fact]
    public void Live10TheSlideSectionUsesTheScenePigmentAtSixPercent()
    {
        var state = Read("AppState.LiveUi.cs");
        Assert.Contains("LiveSceneTintWashBrush", state);
        Assert.Contains("0.06", state);
        var panel = Read("Views", "InsightsPanel.axaml");
        Assert.Contains("Background=\"{Binding LiveSceneTintWashBrush}\"", panel);
    }

    [Fact]
    public void Live11TheModelPickerUsesTheDesignSystemMenuPicker()
    {
        var live = Live();
        Assert.Contains("<components:MenuPicker", live);
        Assert.DoesNotContain("<ComboBox", live);
    }

    [Fact]
    public void Live12TheMyNotesTabUsesTheMacMeetingUserNotesView()
    {
        var live = Live();
        Assert.Contains("x:Name=\"NotesPanel\"", live);
        Assert.Contains("<views:MeetingUserNotes />", live);
    }

    [Fact]
    public void Live15TheDemoLiveDataMatchesTheMacScreenshots()
    {
        var demo = Read("AppState.Demo.cs");
        Assert.Contains("DemoScript.LivePartialTranscript", demo);
        Assert.Contains("EchoDroppedCount = 1;", demo);
        Assert.Contains("Zoom", demo);
    }

    [Fact]
    public void Live16TheWhoAmICapsuleIsDrawnWithADashedOutline()
    {
        var live = Live();
        Assert.Contains("<controls:DashedCapsule", live);
        Assert.Contains("x:Name=\"LiveWhoAmIButton\"", live);
        var control = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "Controls", "DashedCapsule.cs"));
        Assert.Contains("DashStyle([3, 2]", control);
        Assert.Contains("public sealed class DashedCapsule", control);
    }

    [Fact]
    public void Translation2And3FollowTheMacSwitchAndStatus()
    {
        var translation = Read("AppState.LiveTranslation.cs");
        Assert.Contains("SystemPartialTranslation = \"\";", translation);
        Assert.Contains("MicrophonePartialTranslation = \"\";", translation);
        Assert.Contains("UiText.T(\"自动识别 · 中英互译\")", translation);
        Assert.DoesNotContain("LiveTranslationAutoNotice", Live());
    }

    [Fact]
    public void ThePageScreenshotsWereRetakenAfterTheFixes()
    {
        var directory = Path.Combine(Root(), "Windows", "validation", "screens", "live");
        foreach (var name in new[] { "light.png", "dark.png", "en-light.png", "en-dark.png" })
            Assert.True(File.Exists(Path.Combine(directory, name)), name);
    }
}
