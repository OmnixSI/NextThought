using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B18: translated from WorkspaceTests.swift series behaviour members.</summary>
public sealed class WorkspaceSeriesBehaviorParityTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    [Fact]
    public void EffectiveModeUsesCustomBaseAndNeverReturnsCustom()
    {
        var direct = new WorkspaceSeries("s", "系列", Start, [], []) { Mode = "labMeeting" };
        Assert.Equal(MeetingMode.LabMeeting, direct.EffectiveMode);
        var custom = new WorkspaceSeries("s", "系列", Start, [], []) { Mode = "custom", CustomBaseMode = "interview" };
        Assert.Equal(MeetingMode.Interview, custom.EffectiveMode);
        var nested = new WorkspaceSeries("s", "系列", Start, [], []) { Mode = "custom", CustomBaseMode = "custom" };
        Assert.Equal(MeetingMode.General, nested.EffectiveMode);
    }

    [Fact]
    public void MaterialTextAddsLeftoverFieldsAndFreeNotes()
    {
        var series = new WorkspaceSeries("s", "系列", Start, [], [])
        {
            Mode = "meeting",
            Fields = new Dictionary<string, string> { ["domain"] = "支付", ["oldField"] = "保留值" },
            Notes = "  这是一段背景笔记。  "
        };
        var text = series.MaterialText();
        Assert.Contains("领域：支付", text);
        Assert.Contains("oldField：保留值", text);
        Assert.Contains("这是一段背景笔记。", text);
        Assert.EndsWith("这是一段背景笔记。", text);
    }

    [Fact]
    public void QuestionBankBecomesASearchableDocumentOnlyWithKnowledgeOn()
    {
        var series = new WorkspaceSeries("s", "系列", Start, [], [])
        {
            QuestionBank =
            [
                new QuestionBankItem { Question = "如何保证一致性？", Answer = "先更新数据库再删缓存" },
                new QuestionBankItem { Question = "只有问题", Answer = "" }
            ]
        };
        var document = Assert.Single(series.RetrievalDocuments, item => item.Category == "题库");
        Assert.Equal("题库（历次复盘）", document.Name);
        Assert.Single(document.Pages);
        Assert.Contains("问题：如何保证一致性？\n准备好的回答：先更新数据库再删缓存", document.Pages[0]);
        var noKnowledge = series with { UseKnowledge = false };
        Assert.DoesNotContain(noKnowledge.RetrievalDocuments, item => item.Category == "题库");
    }

    [Fact]
    public void DefaultTemplatePreferencesAreRecognised()
    {
        var profile = ModeProfile.For(MeetingMode.Interview);
        Assert.True(profile.MatchesDefaultTemplatePreferences(profile.TemplatePreferences));
        Assert.True(profile.MatchesDefaultTemplatePreferences(""));
        Assert.False(profile.MatchesDefaultTemplatePreferences("我自己的偏好"));
    }
}
