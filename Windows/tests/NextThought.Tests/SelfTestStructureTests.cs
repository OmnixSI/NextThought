using Xunit;

namespace NextThought.Tests;

/// <summary>H1: main window keeps only window code; self-checks live under Desktop/SelfTest.</summary>
public sealed class SelfTestStructureTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }



    [Fact]
    public void LegacyPerCheckFlagsAreGone()
    {
        var root = Root();
        var startup = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "StartupOptions.cs"));
        Assert.DoesNotContain("SelfTestOptions", startup);
        Assert.False(File.Exists(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "SelfTestOptions.cs")));
    }











    [Fact]
    public void RecordingHidesNavigationForFocusMode()
    {
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        var marker = "IsVisible=\"{Binding !Recording}\"";
        Assert.True(main.Split(marker).Length - 1 >= 3);
    }



    [Fact]
    public void AcademicAnswersUseWholeSeriesMaterial()
    {
        var notes = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.StructuredNotes.cs"));
        Assert.Contains("SelectedMode == MeetingMode.Academic", notes);
        Assert.Contains("WholeMaterialOf(DocumentsForRetrieval())", notes);
    }

    [Fact]
    public void ContextSidebarIsCollapsibleAndPersisted()
    {
        var root = Root();
        var main = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("ContextSidebarVisible", main);
        Assert.Contains("ToggleContextSidebarCommand", main);
        var nav = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "AppState.Navigation.cs"));
        Assert.Contains("ContextSidebarHidden", nav);
        var settings = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Core", "Models.cs"));
        Assert.Contains("ContextSidebarHidden", settings);
        var model = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "ViewModels", "SettingsViewModel.cs"));
        Assert.Contains("ContextSidebarHidden", model);
    }





















    [Fact]
    public void HistoryHasFullTextSearchUi()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("全文搜索", xaml);
        Assert.Contains("SearchHistoryContentCommand", xaml);
        Assert.Contains("SearchHit_Click", xaml);
    }

    [Fact]
    public void HistoryIsGroupedBySeries()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("HistoryGroups", xaml);
        Assert.Contains("HistoryGroupRow", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.StructuredNotes.cs"));
        Assert.Contains("HistoryGroups.Add", state);
    }

    [Fact]
    public void HistoryHasFilteredBatchDelete()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("DeleteFilteredSessionsCommand", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Deletion.cs"));
        Assert.Contains("DeleteFilteredSessionsAsync", state);
    }

    [Fact]
    public void HistoryRowsOfferSessionRename()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("重命名会议", xaml);
        Assert.Contains("RenameSession_Tapped", xaml);
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "RenameSessionDialog.axaml")));
    }

    [Fact]
    public void HistoryRowsOfferSessionDeletion()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("删除会议", xaml);
        Assert.Contains("DeleteSessionCommand", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Deletion.cs"));
        Assert.Contains("Sessions.DeleteAsync", state);
    }

    [Fact]
    public void PrepareBuildsAMeetingPlan()
    {
        var prepare = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Prepare.cs"));
        Assert.Contains("ActivePlan", prepare);
        Assert.Contains("PreparedWorkspace", prepare);
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("BeginPreparedMeeting(planSeries)", live);
    }

    [Fact]
    public void PrepareCheckCardsArePresent()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "PrepareView.axaml"));
        Assert.Contains("采音", xaml);
        Assert.Contains("场地", xaml);
        Assert.Contains("回答服务", xaml);
        Assert.Contains("手机伴侣", xaml);
        Assert.Contains("资料与模型", xaml);
    }

    [Fact]
    public void PrepareHasStickyStartBar()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "PrepareView.axaml"));
        Assert.Contains("<Grid RowDefinitions=\"*,Auto\">", xaml);
        Assert.Contains("Grid.Row=\"1\"", xaml);
        Assert.Contains("开始记录", xaml);
    }

    [Fact]
    public void PrepareMaterialsInsightIsShown()
    {
        var library = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "ViewModels", "LibraryViewModel.cs"));
        Assert.Contains("MaterialsInsight", library);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "PrepareView.axaml"));
        Assert.Contains("Library.MaterialsInsight", xaml);
    }

    [Fact]
    public void PrepareSeriesBriefingIsExposed()
    {
        var series = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Series.cs"));
        Assert.Contains("SelectedSeriesBriefing", series);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "PrepareView.axaml"));
        Assert.Contains("系列简报", xaml);
        Assert.Contains("SelectedSeriesBriefing.LatestSessions", xaml);
    }

    [Fact]
    public void PrepareTranscriptCarryAttachesPreviousMeeting()
    {
        var prepare = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Prepare.cs"));
        Assert.Contains("PreviousTranscriptContext", prepare);
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("PreparedDocuments()", live);
    }

    [Fact]
    public void PrepareCarrySwitchesGateMeetingContext()
    {
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("BeginPreparedMeeting", live);
        Assert.Contains("ActiveCarry?.Materials == false", live);
        var series = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Series.cs"));
        Assert.Contains("ActiveCarry?.Lessons == false", series);
        Assert.Contains("ActiveCarry?.Review == false", series);
    }

    [Fact]
    public void PrepareCarryPlanIsExposed()
    {
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Prepare.cs"));
        Assert.Contains("CarryPlan", state);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "PrepareView.axaml"));
        Assert.Contains("CarryMaterials", xaml);
        Assert.Contains("CarryTranscript", xaml);
    }

    [Fact]
    public void PreparePageIsHosted()
    {
        var shell = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "SelfTest", "Checks", "ShowSection.cs"));
        Assert.Contains("AppSection.Prepare => new PrepareView()", shell);
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "PrepareView.axaml")));
        var home = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HomeView.axaml"));
        Assert.Contains("CommandParameter=\"Prepare\"", home);
    }

    [Fact]
    public void QuickSettingsPanelExists()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "QuickSettingsPanel.axaml"));
        Assert.Contains("自动回答", xaml);
        Assert.Contains("提词宽度", xaml);
        Assert.Contains("全部设置", xaml);
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("QuickSettingsPanel", main);
    }

    [Fact]
    public void SceneDefaultsAlsoSetTemplateAndCue()
    {
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("sceneDefaults.NotesTemplate", live);
        Assert.Contains("sceneDefaults.CueStyle", live);
        Assert.Contains("SelectedTemplateId = TemplateKind.Raw()", live);
    }

    [Fact]
    public void SceneDefaultsAreAppliedAtMeetingStart()
    {
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("SceneDefaultsFor(SelectedMode)", live);
        Assert.Contains("Settings.AutomaticAnswers = sceneDefaults.LiveMode", live);
        Assert.Contains("sceneDefaults.AnswerStyle == \"详细\"", live);
    }

    [Fact]
    public void SceneSettingsRowsAreWired()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        Assert.Contains("SceneSettingsRows", xaml);
        Assert.Contains("SceneOption_Click", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.SceneSettings.cs"));
        Assert.Contains("LoadSceneSettings", state);
        Assert.Contains("SceneDefaults", state);
    }

    [Fact]
    public void SettingsCardsAreAssignedToSixPages()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        foreach (var page in new[] { "Scene", "Answer", "Coach", "Notes", "General", "Advanced" })
            Assert.Contains("Tag=\"" + page + "\"", xaml);
        var code = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs"));
        Assert.Contains("ShowPage", code);
        Assert.Contains("card.Tag as string == currentPage", code);
    }

    [Fact]
    public void SettingsRailHasSixGroups()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        foreach (var tag in new[] { "Scene", "Answer", "Coach", "Notes", "General", "Advanced" })
            Assert.Contains("Tag=\"" + tag + "\"", xaml);
        var code = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs"));
        Assert.Contains("SettingsPage_Click", code);
    }

    [Fact]
    public void SettingsIsAnOverlay()
    {
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("IsVisible=\"{Binding OnSettings}\"", main);
        Assert.Contains("<views:SettingsView", main);
        Assert.Contains("CommandParameter=\"Home\"", main);
    }

    [Fact]
    public void SidebarIsHiddenAndSettingsIsInTopBar()
    {
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("ColumnDefinitions=\"Auto,*\"", main);
        Assert.Contains("CommandParameter=\"Settings\"", main);
        Assert.Contains("IsVisible=\"False\"", main);
    }


    [Fact]
    public void TopNavigationHasSnappyBrushTransition()
    {
        var controls = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Styles", "Controls.axaml"));
        Assert.Contains("BrushTransition Property=\"Background\" Duration=\"0:0:0.28\"", controls);
    }


    [Fact]
    public void CueIslandHasFiveStateEvidenceHook()
    {
        var code = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "CueIslandWindow.axaml.cs"));
        Assert.Contains("ApplyStateForCheck", code);
        Assert.Contains("\"collapsed\"", code);
        Assert.Contains("\"launcher\"", code);
        var runner = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "SelfTest", "RunScreenshotsAsync.cs"));
        Assert.Contains("island-\" + islandState", runner);
    }

    [Fact]
    public void TopNavigationCapsuleExists()
    {
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("GroupName=\"topnav\"", main);
        Assert.Contains("CommandParameter=\"Home\"", main);
        Assert.Contains("CommandParameter=\"Live\"", main);
        Assert.Contains("CommandParameter=\"Library\"", main);
        var controls = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Styles", "Controls.axaml"));
        Assert.Contains("RadioButton.nav:checked", controls);
    }

    [Fact]
    public void ViewsDoNotUseTextCharactersForIcons()
    {
        var views = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views");
        foreach (var file in Directory.GetFiles(views, "*.axaml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var symbol in new[] { "✕", "✓", "⚠" })
                Assert.DoesNotContain(symbol, text);
        }
        var tp = File.ReadAllText(Path.Combine(views, "TeleprompterWindow.axaml"));
        Assert.Contains("&#xE711;", tp);
    }

    [Fact]
    public void FontsAndLicensesAreBundled()
    {
        var dir = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Assets", "Fonts");
        foreach (var file in new[] { "InterVariable.ttf", "NotoSansSC-VF.ttf", "SourceSerif4-VF.ttf", "NotoSerifSC-VF.ttf",
                     "LICENSE-Inter.txt", "OFL-NotoSansSC.txt", "OFL-SourceSerif4.txt", "OFL-NotoSerifSC.txt" })
            Assert.True(File.Exists(Path.Combine(dir, file)), file);
        var theme = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Styles", "Theme.axaml"));
        Assert.Contains("InterVariable.ttf#Inter Variable", theme);
        Assert.Contains("SourceSerif4-VF.ttf#Source Serif 4", theme);
    }

    [Fact]
    public void IconMapCoversEverySwiftSymbol()
    {
        var root = Root();
        var sw = Directory.GetFiles(Path.Combine(root, "Sources", "NextThought"), "*.swift", SearchOption.AllDirectories)
            .SelectMany(path => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path), "systemName: \"([^\"]+)\"")
                .Select(match => match.Groups[1].Value))
            .Distinct().ToList();
        var icons = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Styles", "Icons.axaml"));
        var map = File.ReadAllText(Path.Combine(root, "Windows", "ICON_MAP.md"));
        Assert.True(sw.Count >= 50);
        foreach (var symbol in sw)
            Assert.Contains(symbol, map);
        Assert.Equal(sw.Count, System.Text.RegularExpressions.Regex.Matches(icons, "<x:String x:Key=").Count);
    }

    [Fact]
    public void SceneGlyphTagAndInkWashAreWiredIntoHome()
    {
        var root = Root();
        foreach (var name in new[] { "SceneGlyph.cs", "SceneTag.cs", "InkWash.cs" })
            Assert.True(File.Exists(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "Controls", name)));
        var home = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "HomeView.axaml"));
        Assert.Contains("<controls:SceneGlyph", home);
        Assert.Contains("<controls:InkWash", home);
    }

    [Fact]
    public void MotionTokensAndReduceMotionArePresent()
    {
        var text = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Motion.cs"));
        foreach (var token in new[] { "Hover", "Expand", "Phase", "Snappy", "Morph", "SystemParametersInfo", "FadeIn", "FadeOut" })
            Assert.Contains(token, text);
        var shell = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.Shell.cs"));
        Assert.Contains("Motion.FadeIn()", shell);
        Assert.Contains("Motion.FadeOut()", shell);
        Assert.DoesNotContain("TimeSpan.FromMilliseconds(160)", shell);
        Assert.DoesNotContain("TimeSpan.FromMilliseconds(120)", shell);
    }

    [Fact]
    public void AppStateMainFileIsBelow600Lines()
    {
        var path = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.cs");
        Assert.True(File.ReadAllLines(path).Length < 600);
        Assert.True(Directory.GetFiles(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop"), "AppState.*.cs").Length >= 15);
    }

    [Fact]
    public void ProductionStartupOptionsOnlyExposeThreeSwitches()
    {
        var path = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "StartupOptions.cs");
        var text = File.ReadAllText(path);
        Assert.Contains("Demo", text);
        Assert.Contains("Screenshots", text);
        Assert.Contains("SelfTest", text);
        Assert.DoesNotContain("--knowledge-check", text);
        Assert.True(File.ReadAllLines(path).Length < 40);
    }

    [Fact]
    public void MainWindowFileIsBelow400Lines()
    {
        var path = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs");
        Assert.True(File.ReadAllLines(path).Length < 400);
    }

    [Fact]
    public void SelfChecksLiveOutsideTheWindowFile()
    {
        var root = Root();
        Assert.True(File.Exists(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "SelfTest", "Checks", "GlobalUsings.cs")));
        var window = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs"));
        Assert.DoesNotContain("private async Task RunAcceptanceCheckAsync", window);
        Assert.True(Directory.GetFiles(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "SelfTest", "Checks"), "*.cs").Length > 40);
    }
}
