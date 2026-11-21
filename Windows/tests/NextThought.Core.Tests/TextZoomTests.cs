using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// J7: translated from TextZoom in Sources/NextThought/DesignSystem.swift (Mac v3.4.0). Six
/// fixed steps, ⌘+ / ⌘− / ⌘0 semantics, the "textZoom" preference key, canGrow/canShrink and the
/// DynamicType size the live page follows.
/// </summary>
public sealed class TextZoomTests
{
    [Fact]
    public void StepsAreTheMacSizes()
    {
        Assert.Equal(new[] { 0.9, 1, 1.1, 1.2, 1.3, 1.45 }, TextZoom.Steps);
        Assert.Equal("textZoom", TextZoom.PreferenceKey);
    }

    [Fact]
    public void FactorDefaultsToActualSizeAndIgnoresUnknownSavedValues()
    {
        Assert.Equal(1, new TextZoom().Factor);
        Assert.Equal(1, new TextZoom(1.15).Factor);
        Assert.Equal(1.2, new TextZoom(1.2).Factor);
        Assert.Equal(1.45, new TextZoom(1.45).Factor);
        Assert.Equal(0.9, new TextZoom(0.9).Factor);
    }

    [Fact]
    public void CanGrowAndCanShrinkOnlyStopAtTheEnds()
    {
        Assert.True(new TextZoom(0.9).CanGrow);
        Assert.False(new TextZoom(0.9).CanShrink);
        Assert.True(new TextZoom(1).CanGrow);
        Assert.True(new TextZoom(1).CanShrink);
        Assert.False(new TextZoom(1.45).CanGrow);
        Assert.True(new TextZoom(1.45).CanShrink);
    }

    [Fact]
    public void GrowShrinkAndResetWalkTheSteps()
    {
        var zoom = new TextZoom();
        zoom.Grow();
        Assert.Equal(1.1, zoom.Factor);
        zoom.Grow();
        Assert.Equal(1.2, zoom.Factor);
        zoom.Grow();
        Assert.Equal(1.3, zoom.Factor);
        zoom.Grow();
        Assert.Equal(1.45, zoom.Factor);
        zoom.Grow();
        Assert.Equal(1.45, zoom.Factor);
        zoom.Shrink();
        Assert.Equal(1.3, zoom.Factor);
        var bottom = new TextZoom(0.9);
        bottom.Shrink();
        Assert.Equal(0.9, bottom.Factor);
        bottom.Reset();
        Assert.Equal(1, bottom.Factor);
        var high = new TextZoom(1.45);
        high.Reset();
        Assert.Equal(1, high.Factor);
        var actual = new TextZoom();
        actual.Reset();
        Assert.Equal(1, actual.Factor);
    }

    [Fact]
    public void EveryChangeIsSavedUnderTheMacKeyOnlyWhenItChanges()
    {
        var saved = new List<double>();
        var zoom = new TextZoom(null, saved.Add);
        zoom.Grow();
        Assert.Equal(new[] { 1.1 }, saved);
        zoom.Reset();
        Assert.Equal(new[] { 1.1, 1.0 }, saved);
        zoom.Reset();
        Assert.Equal(new[] { 1.1, 1.0 }, saved);
        zoom.Shrink();
        Assert.Equal(new[] { 1.1, 1.0, 0.9 }, saved);

        var restoredSaved = new List<double>();
        var restored = new TextZoom(1.3, restoredSaved.Add);
        Assert.Equal(1.3, restored.Factor);
        Assert.Empty(restoredSaved);
    }

    [Fact]
    public void DynamicTypeFollowsTheMacThresholds()
    {
        Assert.Equal(TextZoom.DynamicTypeSize.Medium, new TextZoom(0.9).DynamicType);
        Assert.Equal(TextZoom.DynamicTypeSize.Large, new TextZoom(1).DynamicType);
        Assert.Equal(TextZoom.DynamicTypeSize.XLarge, new TextZoom(1.1).DynamicType);
        Assert.Equal(TextZoom.DynamicTypeSize.XXLarge, new TextZoom(1.2).DynamicType);
        Assert.Equal(TextZoom.DynamicTypeSize.XXXLarge, new TextZoom(1.3).DynamicType);
        Assert.Equal(TextZoom.DynamicTypeSize.Accessibility1, new TextZoom(1.45).DynamicType);
    }

    [Fact]
    public void ScaledRoundsToTheNearestWholePointAwayFromZero()
    {
        Assert.Equal(10, new TextZoom().Scaled(10));
        Assert.Equal(15, new TextZoom(1.45).Scaled(10));
        Assert.Equal(9, new TextZoom(0.9).Scaled(10));
        Assert.Equal(14, new TextZoom(1.1).Scaled(13));
    }
}
