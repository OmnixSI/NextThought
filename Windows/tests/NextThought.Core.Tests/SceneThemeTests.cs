using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>D6: ten scenes each have four Mac pigments, a symbol, a promise and three moments.</summary>
public sealed class SceneThemeTests
{
    [Fact]
    public void AllTenScenesHaveMacThemeData()
    {
        var modes = Enum.GetValues<MeetingMode>();
        Assert.Equal(10, modes.Length);
        foreach (var mode in modes)
        {
            var theme = SceneTheme.For(mode);
            Assert.Equal(mode, theme.Mode);
            foreach (var color in new[] { theme.TintLight, theme.TintDark, theme.DeepLight, theme.DeepDark })
                Assert.Matches("^#[0-9a-f]{6}$", color);
            Assert.False(string.IsNullOrWhiteSpace(theme.Symbol));
            Assert.False(string.IsNullOrWhiteSpace(theme.Promise));
            Assert.Equal(3, theme.Moments.Count);
        }
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
