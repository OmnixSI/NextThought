using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the display half of PresenterMode.swift (PresenterLayout).</summary>
public sealed class PresenterModeTests
{
    private static DisplayInfo Builtin(bool primary = true, string id = "display:0") =>
        new(id, "笔记本屏幕", 0, 0, 3000, 2000, primary, true);

    private static DisplayInfo External(string id = "display:1", string name = "外接显示器", bool primary = false) =>
        new(id, name, 3000, 0, 3840, 2160, primary, false);

    [Fact]
    public void OneDisplayCannotPresent()
    {
        var plan = PresenterLayout.Resolve([Builtin()], null);
        Assert.False(plan.CanActivate);
        Assert.Null(plan.Slides);
        Assert.Equal(Builtin().Id, plan.Camera!.Id);
        Assert.Equal(PresenterLayout.NeedsSecondDisplay, plan.Summary);
    }

    [Fact]
    public void TwoDisplaysPickTheOneWithoutTheCamera()
    {
        var plan = PresenterLayout.Resolve([Builtin(), External()], null);
        Assert.True(plan.CanActivate);
        Assert.Equal("display:0", plan.Camera!.Id);
        Assert.Equal("display:1", plan.Slides!.Id);
        Assert.True(plan.SlidesIsSeparate);
        Assert.Contains("外接显示器", plan.Summary);
        Assert.Contains("笔记本屏幕", plan.Summary);
    }

    [Fact]
    public void TheChosenSlidesDisplayWins()
    {
        var second = External("display:2", "投影仪");
        var plan = PresenterLayout.Resolve([Builtin(), External(), second], "display:2");
        Assert.Equal("display:2", plan.Slides!.Id);
    }

    [Fact]
    public void AMissingPreferenceFallsBackToTheFirstOtherDisplay()
    {
        var plan = PresenterLayout.Resolve([Builtin(), External("display:1"), External("display:2", "投影仪")], "display:9");
        Assert.Equal("display:1", plan.Slides!.Id);
    }

    [Fact]
    public void APreferencePointingAtTheCameraIsIgnored()
    {
        var plan = PresenterLayout.Resolve([Builtin(), External()], "display:0");
        Assert.Equal("display:1", plan.Slides!.Id);
    }

    [Fact]
    public void TheBuiltinDisplayIsTheCameraEvenWhenAnotherIsPrimary()
    {
        var plan = PresenterLayout.Resolve([Builtin(primary: false), External("display:1", "外接显示器", primary: true)], null);
        Assert.Equal("display:0", plan.Camera!.Id);
        Assert.Equal("display:1", plan.Slides!.Id);
    }

    [Fact]
    public void WithoutABuiltinDisplayThePrimaryIsTheCamera()
    {
        var primary = External("display:1", "主屏", primary: true);
        var other = External("display:2", "副屏");
        var plan = PresenterLayout.Resolve([primary, other], null);
        Assert.Equal("display:1", plan.Camera!.Id);
        Assert.Equal("display:2", plan.Slides!.Id);
    }

    [Fact]
    public void NoDisplaysMeansNoPlan()
    {
        var plan = PresenterLayout.Resolve([], "display:1");
        Assert.False(plan.CanActivate);
        Assert.Null(plan.Camera);
        Assert.Equal(PresenterLayout.NoDisplay, plan.Summary);
    }

    [Fact]
    public void SummarySaysOffWhenDisabled()
    {
        Assert.Equal(PresenterLayout.Off, PresenterLayout.Summary([Builtin(), External()], null, enabled: false));
        Assert.Contains("幻灯片在", PresenterLayout.Summary([Builtin(), External()], null, enabled: true));
        Assert.Equal(PresenterLayout.NeedsSecondDisplay, PresenterLayout.Summary([Builtin()], null, enabled: true));
    }

    [Fact]
    public void BoundsCoverTheDisplayOrigin()
    {
        var screen = External();
        Assert.True(screen.Contains(3000, 0));
        Assert.True(screen.Contains(6839, 2159));
        Assert.False(screen.Contains(2999, 0));
        Assert.False(screen.Contains(3000, 2160));
        Assert.Equal("3000,0,3840,2160", screen.Bounds);
    }
}
