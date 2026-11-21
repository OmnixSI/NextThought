using Xunit;

namespace NextThought.Tests;

/// <summary>
/// J2 (§6.7): 这场用什么 — five rows on the preparation page: the first is read-only
/// 「始终开启」, the other four switch the persisted <c>MeetingFeatures</c> (Core, J2), the choice
/// stays with the series, the meeting applies it when it starts, and 恢复这个场景的默认 resets it.
/// Written from Sources/NextThought/PrepareView.swift:284-347 and LiveNotesEngine.swift:144-178.
/// </summary>
public sealed class PrepareFeaturesParityTests
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

    private static string Desktop(params string[] parts) =>
        Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]);

    private static string Read(params string[] parts) => File.ReadAllText(Desktop(parts));

    [Fact]
    public void ThePreparePageHasTheFiveFeatureRowsInTheMacOrder()
    {
        var xaml = Read("Views", "PrepareView.axaml");
        Assert.Contains("这场用什么", xaml);
        Assert.Contains("不用的可以关掉", xaml);
        Assert.Contains("恢复这个场景的默认", xaml);
        Assert.Contains("ResetMeetingFeaturesCommand", xaml);
        Assert.Contains("始终开启", xaml);
        var order = new[]
        {
            "实时转写", "自动回答", "实时纪要", "同声传译", "区分说话人"
        };
        var positions = order.Select(title => xaml.IndexOf(title, StringComparison.Ordinal)).ToArray();
        Assert.All(positions, position => Assert.True(position >= 0, "missing a feature row"));
        for (var index = 1; index < positions.Length; index++) Assert.True(positions[index - 1] < positions[index]);
        // The four switches bind to MeetingFeatures; 实时转写 has no switch.
        foreach (var binding in new[]
                 {
                     "MeetingFeatureAnswers", "MeetingFeatureLiveNotes", "MeetingFeatureTranslation", "MeetingFeatureSpeakers"
                 })
            Assert.Contains("IsChecked=\"{Binding " + binding + ", Mode=TwoWay}\"", xaml);
        foreach (var detail in new[] { "MeetingFeatureAnswersDetail", "MeetingFeatureLiveNotesDetail", "MeetingFeatureTranslationDetail", "MeetingFeatureSpeakersDetail" })
            Assert.Contains(detail, xaml);
        // Mac FeaturePicker rows: 14 pt icon in a 24 pt column, small switch, 始终开启 for transcription.
        Assert.Contains("InkSwitch", xaml);
        Assert.Contains("始终开启", xaml);
    }

    [Fact]
    public void TheAppStateKeepsTheChoicesOnTheSeriesAndAppliesThemAtStart()
    {
        var state = Read("AppState.Prepare.cs");
        foreach (var element in new[]
                 {
                     "MeetingFeatureChoices", "MeetingFeatureChoices.FileName", "ForSeries(",
                     "StandardMeetingFeatures", "StandardPrompting", "SpeakerSeparationEnabled",
                     "prompting: StandardPrompting", "speakers: SpeakerSeparationEnabled",
                     "MeetingFeatureSeriesKey", "ResetMeetingFeatures", "RaiseMeetingFeatures",
                     "ActiveMeetingFeatures", "ApplyActiveMeetingFeatures"
                 })
            Assert.Contains(element, state);
        // Mac: the choices are per series and read back for the next meeting (featuresKey).
        Assert.Contains("meetingDraft?.SeriesId ?? SelectedSeries?.Id", state);
        // Mac FeatureReset: shown only while the switches differ from the scene's defaults.
        Assert.Contains("MeetingFeaturesAreDefault", state);
        // The start path applies the four switches (Mac startPreparedMeeting).
        var live = Read("AppState.Live.cs");
        Assert.Contains("ApplyActiveMeetingFeatures();", live);
        Assert.Contains("ActiveMeetingFeatures.Speakers", live);
        Assert.Contains("Settings.AutomaticAnswers = AnswerEngineReady && ActiveMeetingFeatures.Answers;", state);
        Assert.Contains("LiveNotesEnabled = ActiveMeetingFeatures.LiveNotes;", state);
        Assert.Contains("if (!ActiveMeetingFeatures.Translation) LiveTranslationEnabled = false;", state);
        // Mid-meeting the same tools stay switchable: the page's more menu carries 实时纪要/同声传译
        // and the top bar the 自动回答/只记录 modes.
        var liveView = Read("Views", "LiveView.axaml");
        Assert.Contains("实时纪要", liveView);
        Assert.Contains("同声传译", liveView);
        Assert.Contains("自动回答", liveView);
        Assert.Contains("只记录", liveView);
        // The demo shows the Mac screenshots' configured state (Mac DemoMode.isEnabled does the same).
        Assert.Contains("StartupOptions.Demo", state);
    }
}
