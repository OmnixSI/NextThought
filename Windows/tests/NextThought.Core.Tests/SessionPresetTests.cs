using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for stored session modes, material documents and presets.</summary>
public sealed class SessionPresetTests
{
    [Fact]
    public void ModesHaveTitlesAndRoundTripToMeetingModes()
    {
        Assert.Equal("面试", SessionMode.Interview.Title());
        Assert.Equal("工作会议", SessionMode.Meeting.Title());
        foreach (var mode in Enum.GetValues<SessionMode>())
        {
            Assert.NotEmpty(mode.Title());
            Assert.Equal(mode, SessionModes.FromMeetingMode(mode.ToMeetingMode()));
        }
        Assert.Equal(MeetingMode.General, ((SessionMode)99).ToMeetingMode());
    }

    [Fact]
    public void ContextDocumentsCountCharactersAndEnabledState()
    {
        var document = new ContextDocument
        {
            Name = "论文",
            Pages = ["第一页内容", "第二页"],
            Category = "论文",
            SourceFormat = "pdf"
        };
        Assert.Equal(5 + 3, document.CharacterCount);
        Assert.True(document.Enabled);
        Assert.Equal("论文", document.Category);
        Assert.False((document with { Enabled = false }).Enabled);
    }

    [Fact]
    public void PresetsCarryModeStylePreferencesAndDocuments()
    {
        var preset = new SessionPreset
        {
            Name = "技术面",
            Mode = SessionMode.Interview,
            Style = AnswerStyle.Concise,
            Preferences = "先给结论",
            Profile = "后端工程师",
            Documents =
            [
                new ContextDocument { Name = "简历", Pages = ["简历内容"] },
                new ContextDocument { Name = "JD", Pages = ["JD"], Enabled = false }
            ]
        };
        Assert.Equal("技术面", preset.Name);
        Assert.Equal(SessionMode.Interview, preset.Mode);
        Assert.Equal(AnswerStyle.Concise, preset.Style);
        Assert.Equal(1, preset.EnabledDocumentCount);
        Assert.Equal(2, preset.Documents.Count);
    }

    [Fact]
    public void PresetTemplatePreferencesFollowTheModeProfile()
    {
        Assert.StartsWith("我是候选人", SessionPreset.TemplatePreferences(SessionMode.Interview));
        Assert.StartsWith("我是报告人", SessionPreset.TemplatePreferences(SessionMode.Academic));
        Assert.StartsWith("直接回答当前问题", SessionPreset.TemplatePreferences(SessionMode.Custom));
    }

    [Fact]
    public void EmptyPresetsHaveSafeDefaults()
    {
        var preset = new SessionPreset();
        Assert.Equal(SessionMode.General, preset.Mode);
        Assert.Equal("", preset.Name);
        Assert.Empty(preset.Documents);
        Assert.Equal(0, preset.EnabledDocumentCount);
        Assert.True(preset.UpdatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }
}
