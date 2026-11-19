using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

public sealed class ShortcutKeysTests
{
    [Theory]
    [InlineData("R", 0x52u)]
    [InlineData("r", 0x52u)]
    [InlineData("Left", 0x25u)]
    [InlineData("PageDown", 0x22u)]
    [InlineData("F12", 0x7Bu)]
    [InlineData("Space", 0x20u)]
    public void ParsesNamesAndKeys(string name, uint expected) => Assert.Equal(expected, ShortcutKeys.Parse(name, 0));

    [Fact]
    public void InvalidNamesFallBackAndNamesRoundTrip()
    {
        Assert.Equal(0x52u, ShortcutKeys.Parse("", 0x52));
        Assert.Equal(0x52u, ShortcutKeys.Parse("not-a-key", 0x52));
        Assert.Equal("Left", ShortcutKeys.Name(0x25));
        Assert.Equal("F12", ShortcutKeys.Name(0x7B));
        Assert.Equal(19, ShortcutKeys.Actions.Count);
        Assert.Contains("record=R", ShortcutKeys.DefaultText());
        Assert.Contains("slideNext=PageDown", ShortcutKeys.DefaultText());
    }
}
