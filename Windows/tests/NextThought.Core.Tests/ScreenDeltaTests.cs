using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Port of ScreenDeltaTests.swift.</summary>
public sealed class ScreenDeltaTests
{
    [Fact]
    public void InitialUnchangedAndLocalizedCrop()
    {
        const int width = 100, height = 100;
        var empty = new byte[width * height * 4];
        Assert.Equal([0d, 0d, 1d, 1d], ScreenDelta.Region(null, empty, width, height));
        Assert.Null(ScreenDelta.Region(empty, empty, width, height));

        var changed = (byte[])empty.Clone();
        for (var y = 20; y < 30; y++)
            for (var x = 60; x < 75; x++)
                changed[(y * width + x) * 4] = 255;
        var region = ScreenDelta.Region(empty, changed, width, height)!;
        Assert.True(region[0] <= 0.6 && region[0] + region[2] >= 0.75);
        Assert.True(region[1] <= 0.2 && region[1] + region[3] >= 0.3);
        Assert.True(region[2] * region[3] < 0.1);
    }
}
