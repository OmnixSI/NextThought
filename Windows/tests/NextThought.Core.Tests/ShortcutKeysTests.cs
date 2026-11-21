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
    public void ParsesModifierCombos()
    {
        var bare = ShortcutKeys.ParseBinding("F9", 0);
        Assert.Equal(ShortcutKeys.ControlModifier | ShortcutKeys.AltModifier, bare.Modifiers);
        Assert.Equal(0x78u, bare.Key);
        var ctrlShift = ShortcutKeys.ParseBinding("Ctrl+Shift+R", 0);
        Assert.Equal(ShortcutKeys.ControlModifier | ShortcutKeys.ShiftModifier, ctrlShift.Modifiers);
        Assert.Equal(0x52u, ctrlShift.Key);
        Assert.Equal("Ctrl+Shift+R", ShortcutKeys.Format(ctrlShift.Modifiers, ctrlShift.Key));
        var alt = ShortcutKeys.ParseBinding("Alt+F12", 0);
        Assert.Equal(ShortcutKeys.AltModifier, alt.Modifiers);
        Assert.Equal(0x7Bu, alt.Key);
    }

    [Fact]
    public void InvalidNamesFallBackAndNamesRoundTrip()
    {
        Assert.Equal(0x52u, ShortcutKeys.Parse("", 0x52));
        Assert.Equal(0x52u, ShortcutKeys.Parse("not-a-key", 0x52));
        Assert.Equal("Left", ShortcutKeys.Name(0x25));
        Assert.Equal("F12", ShortcutKeys.Name(0x7B));
        Assert.Equal(18, ShortcutKeys.Actions.Count);
        Assert.Contains("record=Ctrl+Alt+M", ShortcutKeys.DefaultText());
        Assert.Contains("slideNext=Ctrl+Alt+Shift+.", ShortcutKeys.DefaultText());
    }

    [Fact]
    public void ActionsMatchTheMacDefaults()
    {
        // Letter keys are unchanged; ⌘⇧→Ctrl+Alt, ⌘⌃→Ctrl+Alt, ⌃⌥→Ctrl+Alt+Shift.
        var expected = new (string Id, uint Key, uint Extra)[]
        {
            ("window", 0x41, 0), ("region", 0x53, 0), ("record", 0x4D, 0), ("regenerate", 0x52, 0),
            ("force", 0x0D, 0), ("pause", 0x50, 0), ("teleprompter", 0x4F, 0), ("cancel", 0x1B, 0),
            ("copy", 0x59, 0), ("through", 0x4B, 0), ("hide", 0x47, 0), ("previous", 0x25, 0),
            ("next", 0x27, 0), ("shorter", 0x31, 0), ("live", 0x53, ShortcutKeys.ShiftModifier),
            ("island", 0x45, 0), ("slidePrevious", 0xBC, ShortcutKeys.ShiftModifier),
            ("slideNext", 0xBE, ShortcutKeys.ShiftModifier)
        };
        Assert.Equal(expected.Length, ShortcutKeys.Actions.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            var action = ShortcutKeys.Actions[index];
            Assert.Equal(expected[index].Id, action.Id);
            Assert.Equal(expected[index].Key, action.DefaultKey);
            Assert.Equal(ShortcutKeys.ControlModifier | ShortcutKeys.AltModifier | expected[index].Extra, action.DefaultModifiers);
            Assert.False(string.IsNullOrWhiteSpace(action.Label));
        }
        Assert.DoesNotContain(ShortcutKeys.Actions, action => action.Id == "main");
    }

    [Fact]
    public void ValidationMatchesTheMacRules()
    {
        Assert.Equal("快捷键必须包含 Command、Option 或 Control，不能只使用普通按键。",
            ShortcutKeys.Validate(new Dictionary<string, (uint, uint)> { ["a"] = (0, 0x52) }));
        Assert.Equal("两个动作不能使用相同的快捷键。",
            ShortcutKeys.Validate(new Dictionary<string, (uint, uint)>
            {
                ["a"] = (ShortcutKeys.ControlModifier | ShortcutKeys.AltModifier, 0x52),
                ["b"] = (ShortcutKeys.ControlModifier | ShortcutKeys.AltModifier, 0x52)
            }));
        Assert.Null(ShortcutKeys.Validate(new Dictionary<string, (uint, uint)>
        {
            ["a"] = (ShortcutKeys.ControlModifier | ShortcutKeys.AltModifier, 0x52),
            ["b"] = (ShortcutKeys.ControlModifier | ShortcutKeys.AltModifier | ShortcutKeys.ShiftModifier, 0x53)
        }));
    }
}
