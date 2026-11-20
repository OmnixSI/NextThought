using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>D6: ten scenes each have the four Mac pigments, symbol, promise and three moments.</summary>
public sealed class SceneThemeTests
{
    /// <summary>The ten scenes of Stage.swift (SessionMode.theme), value by value.</summary>
    public static readonly TheoryData<MeetingMode, string, string, string, string, string, string, string, string, string> MacThemes = new()
    {
        // mode, tint light, tint dark, deep light, deep dark, symbol, promise, moment1, moment2, moment3
        { MeetingMode.Interview, "#3f5b8c", "#93aee0", "#2c4370", "#6f8cc4", "person.crop.rectangle", "被问到时，答案就在镜头旁",
            "听到问题自动给出回答", "提词贴着摄像头，不用低头", "会后逐题复盘，存进题库" },
        { MeetingMode.LabMeeting, "#4a7d68", "#8cc7ad", "#355e4d", "#66a88c", "flask", "专心讨论，导师的话一句不漏",
            "默认只记录，被问住再求助", "纪要按进展、意见、下一步整理", "鱼骨图梳理卡住的问题" },
        { MeetingMode.Academic, "#775a9c", "#b9a0e0", "#5a417c", "#957bc2", "graduationcap", "跟着讲稿走，问答环节有底",
            "认出当前幻灯片，提示这一页要点", "听众提问时自动给出回答", "纪要记下被问了什么、怎么答更好" },
        { MeetingMode.GroupProject, "#a5683c", "#e0a476", "#824e28", "#c28555", "person.3", "谁做什么、哪天交，散会就清楚",
            "记下每个人领到的任务", "意见不一致时帮你找折中", "纪要按方案、分工、截止时间整理" },
        { MeetingMode.Client, "#2f7a8c", "#7cc3d4", "#1f5c6b", "#57a3b5", "person.2", "听懂需求，答应的事不落空",
            "客户提问时给出有依据的回应", "价格和承诺以资料为准", "纪要列出需求、顾虑和我们答应的事" },
        { MeetingMode.Meeting, "#5b6573", "#a9b3c1", "#434c58", "#8691a1", "briefcase", "决定、待办、风险，会后即得",
            "默认只记录，不打扰", "需要时一键求助", "纪要按决定、待办、风险整理" },
        { MeetingMode.Lesson, "#5f7d40", "#a8cb85", "#475f2d", "#86ac63", "book", "只管听，笔记自己长出来",
            "跟着课件记录要点", "没听懂的地方随时问", "笔记按提纲、概念、疑问整理" },
        { MeetingMode.Discussion, "#a5526b", "#e395ad", "#813a51", "#c4748d", "bubble.left.and.bubble.right", "各方观点摆清楚，结论自然来",
            "区分每个人说了什么", "观点对照表一眼看清分歧", "纪要按议题、观点、结论整理" },
        { MeetingMode.General, "#6b6862", "#b5b2ab", "#4d4a45", "#93908a", "sparkles", "先录下来，其他的之后再说",
            "只记录，需要时求助", "回答简短", "纪要按总结、决定、待办整理" },
        { MeetingMode.Custom, "#6b6862", "#b5b2ab", "#4d4a45", "#93908a", "slider.horizontal.3", "先录下来，其他的之后再说",
            "只记录，需要时求助", "回答简短", "纪要按总结、决定、待办整理" }
    };

    [Theory]
    [MemberData(nameof(MacThemes))]
    public void EverySceneMatchesTheMacStageTheme(MeetingMode mode, string tintLight, string tintDark, string deepLight,
        string deepDark, string symbol, string promise, string moment1, string moment2, string moment3)
    {
        var theme = SceneTheme.For(mode);
        Assert.Equal(mode, theme.Mode);
        Assert.Equal(tintLight, theme.TintLight);
        Assert.Equal(tintDark, theme.TintDark);
        Assert.Equal(deepLight, theme.DeepLight);
        Assert.Equal(deepDark, theme.DeepDark);
        Assert.Equal(symbol, theme.Symbol);
        Assert.Equal(promise, theme.Promise);
        Assert.Equal(new[] { moment1, moment2, moment3 }, theme.Moments.ToArray());
        foreach (var color in new[] { theme.TintLight, theme.TintDark, theme.DeepLight, theme.DeepDark })
            Assert.Matches("^#[0-9a-f]{6}$", color);
    }

    [Fact]
    public void ThemePickersFollowTheAppearance()
    {
        var interview = SceneTheme.For(MeetingMode.Interview);
        Assert.Equal(interview.TintLight, interview.Tint(dark: false));
        Assert.Equal(interview.TintDark, interview.Tint(dark: true));
        Assert.Equal(interview.DeepLight, interview.Deep(dark: false));
        Assert.Equal(interview.DeepDark, interview.Deep(dark: true));
    }

    [Fact]
    public void AllTenScenesAreCovered()
    {
        Assert.Equal(10, Enum.GetValues<MeetingMode>().Length);
        Assert.All(Enum.GetValues<MeetingMode>(), mode => Assert.Equal(mode, SceneTheme.For(mode).Mode));
    }

    [Fact]
    public void CustomSharesGeneralPigmentsButHasItsOwnSymbol()
    {
        var general = SceneTheme.For(MeetingMode.General);
        var custom = SceneTheme.For(MeetingMode.Custom);
        Assert.Equal(general.TintLight, custom.TintLight);
        Assert.Equal(general.DeepDark, custom.DeepDark);
        Assert.NotEqual(general.Symbol, custom.Symbol);
    }
}
