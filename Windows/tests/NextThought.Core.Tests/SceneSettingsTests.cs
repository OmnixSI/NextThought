using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// E26: the per-scene defaults are the Mac tables (MeetingType.swift MeetingTypeDefaults and
/// Scene.swift ScenePreset.cueStyle), sceneOverrides stores only differences, and the per-type
/// notes template is the Mac notesTemplateByType key.
/// </summary>
public sealed class SceneSettingsTests
{
    [Fact]
    public void TheBuiltInRowsMatchTheMacTables()
    {
        Assert.Equal(new SceneDefaults("自动回答", "简洁", "interview", "只看要点"), SceneDefaults.For(MeetingMode.Interview));
        Assert.Equal(new SceneDefaults("自动回答", "简洁", "talk", "逐句"), SceneDefaults.For(MeetingMode.Academic));
        Assert.Equal(new SceneDefaults("只记录", "详细", "academic", "只看要点"), SceneDefaults.For(MeetingMode.LabMeeting));
        Assert.Equal(new SceneDefaults("只记录", "简洁", "work", "只看要点"), SceneDefaults.For(MeetingMode.Meeting));
        Assert.Equal(new SceneDefaults("自动回答", "简洁", "client", "只看要点"), SceneDefaults.For(MeetingMode.Client));
        Assert.Equal(new SceneDefaults("只记录", "详细", "lecture", "只看要点"), SceneDefaults.For(MeetingMode.Lesson));
        Assert.Equal(new SceneDefaults("只记录", "简洁", "discussion", "只看要点"), SceneDefaults.For(MeetingMode.Discussion));
        Assert.Equal(new SceneDefaults("只记录", "简洁", "groupProject", "只看要点"), SceneDefaults.For(MeetingMode.GroupProject));
        Assert.Equal(new SceneDefaults("只记录", "简洁", "general", "只看要点"), SceneDefaults.For(MeetingMode.General));
        Assert.True(SceneDefaults.For(MeetingMode.Interview).IsDefaultFor(MeetingMode.Interview));
        Assert.False(SceneDefaults.For(MeetingMode.Interview).IsCustomisedFor(MeetingMode.Interview));
    }

    [Fact]
    public void SceneOverridesStoresOnlyTheFieldsThatDiffer()
    {
        var settings = new AppSettings();
        settings.SceneDefaults[MeetingMode.Interview.ToString()] = SceneDefaults.For(MeetingMode.Interview) with { LiveMode = "只记录" };
        settings.SceneDefaults[MeetingMode.Meeting.ToString()] = SceneDefaults.For(MeetingMode.Meeting);
        var raw = MacPreferenceMap.Read(settings, "sceneOverrides");
        var overrides = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(raw);
        var interview = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(overrides["interview"]);
        Assert.Equal("notesOnly", interview["liveMode"]);
        Assert.False(interview.ContainsKey("style"));
        Assert.False(interview.ContainsKey("cueStyle"));
        Assert.DoesNotContain("meeting", overrides.Keys);
    }

    [Fact]
    public void NotesTemplateByTypeRoundTrips()
    {
        var settings = new AppSettings();
        MacPreferenceMap.Apply(settings, "notesTemplateByType", new Dictionary<string, object?>
        {
            ["interview"] = "talk",
            ["labMeeting"] = "academic"
        });
        Assert.Equal("talk", MacPreferenceMap.Read(settings, "notesTemplateByType") is IReadOnlyDictionary<string, object?> byType
            ? byType["interview"]?.ToString()
            : null);
        Assert.Equal("talk", settings.SceneDefaults["Interview"].NotesTemplate);
        Assert.Equal("academic", settings.SceneDefaults["LabMeeting"].NotesTemplate);
        Assert.True(MacPreferenceMap.IsMapped("notesTemplateByType"));
    }
}
