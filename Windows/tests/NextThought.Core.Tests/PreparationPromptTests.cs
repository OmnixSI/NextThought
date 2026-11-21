using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the pre-meeting preparation prompt.</summary>
public sealed class PreparationPromptTests
{
    [Fact]
    public void SectionsFollowTheMode()
    {
        Assert.Equal("资料里的亮点", PreparationPrompt.For(MeetingMode.Interview).First);
        Assert.Equal("客户关心什么", PreparationPrompt.For(MeetingMode.Client).First);
        Assert.Equal("要定下来的事", PreparationPrompt.For(MeetingMode.Meeting).First);
        Assert.Equal("进展和证据", PreparationPrompt.For(MeetingMode.LabMeeting).First);
        Assert.Equal("资料重点", PreparationPrompt.For(MeetingMode.General).First);
        Assert.Equal("资料重点", PreparationPrompt.For(MeetingMode.Custom).First);
    }

    [Fact]
    public void SystemPromptCarriesModeRoleAndNoFabricationRule()
    {
        var system = PreparationPrompt.System(MeetingMode.Interview);
        Assert.Contains("帮我准备这场面试", system);
        Assert.Contains("我的身份是候选人", system);
        Assert.Contains("不补造资料里没有的事实", system);
        Assert.Contains("准备时关注：按问题逐条评价我的回答", system);
    }

    [Fact]
    public void OutputNamesBothSectionsAndTheDisclaimer()
    {
        var output = PreparationPrompt.Output(MeetingMode.Academic);
        Assert.StartsWith("严格按两节输出：\n## 要讲清楚的重点", output);
        Assert.Contains("## 可能被问到的问题", output);
        Assert.EndsWith("资料仅展示有限摘录，不声称已完整读懂。", output);
    }

    [Fact]
    public void EveryModeHasATitleAndRole()
    {
        foreach (var mode in Enum.GetValues<MeetingMode>())
        {
            Assert.NotEmpty(ModeProfile.Title(mode));
            Assert.NotEmpty(ModeProfile.For(mode).Role);
            Assert.NotEmpty(ModeProfile.For(mode).ReviewFocus);
            Assert.NotEmpty(PreparationPrompt.For(mode).SecondHint);
        }
    }

    [Fact]
    public void ModeFieldsStartWithTheDomainAndMarkLongInputs()
    {
        var interview = ModeProfile.For(MeetingMode.Interview);
        Assert.Equal("domain", interview.Fields[0].Id);
        Assert.Equal(6, interview.Fields.Count);
        Assert.True(interview.Fields.Single(field => field.Id == "highlights").Long);
        Assert.False(interview.Fields.Single(field => field.Id == "company").Long);
        Assert.Contains("专业词", ModeField.Domain.Placeholder);
    }

    [Fact]
    public void EveryModeHasFieldsCategoriesAndPreferences()
    {
        foreach (var mode in Enum.GetValues<MeetingMode>())
        {
            var profile = ModeProfile.For(mode);
            Assert.NotEmpty(profile.Fields);
            Assert.Equal("domain", profile.Fields[0].Id);
            Assert.NotEmpty(profile.DocumentCategories);
            Assert.NotEmpty(profile.TemplatePreferences);
        }
        Assert.Contains("简历", ModeProfile.For(MeetingMode.Interview).DocumentCategories);
        Assert.Contains("论文", ModeProfile.For(MeetingMode.Academic).DocumentCategories);
        Assert.Contains("报价 / 合同", ModeProfile.For(MeetingMode.Client).DocumentCategories);
    }

    [Fact]
    public void AnswerPreferencesNameTheRightIdentity()
    {
        Assert.StartsWith("我是候选人", ModeProfile.For(MeetingMode.Interview).TemplatePreferences);
        Assert.StartsWith("我是报告人", ModeProfile.For(MeetingMode.Academic).TemplatePreferences);
        Assert.StartsWith("我在组会上汇报", ModeProfile.For(MeetingMode.LabMeeting).TemplatePreferences);
        Assert.StartsWith("直接回答当前问题", ModeProfile.For(MeetingMode.General).TemplatePreferences);
    }

    [Fact]
    public void CustomUsesTheGeneralDefaults()
    {
        Assert.Equal(ModeProfile.For(MeetingMode.General).Role, ModeProfile.For(MeetingMode.Custom).Role);
        Assert.Equal(PreparationPrompt.For(MeetingMode.General), PreparationPrompt.For(MeetingMode.Custom));
    }

    [Fact]
    public void ReviewFocusMatchesTheSessionModeFamily()
    {
        Assert.Contains("面试官", ModeProfile.For(MeetingMode.Interview).ReviewFocus);
        Assert.Contains("听众", ModeProfile.For(MeetingMode.Academic).ReviewFocus);
        Assert.Contains("客户", ModeProfile.For(MeetingMode.Client).ReviewFocus);
        Assert.Contains("导师", ModeProfile.For(MeetingMode.LabMeeting).ReviewFocus);
    }
}
