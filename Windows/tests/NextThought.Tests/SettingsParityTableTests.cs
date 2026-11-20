using Xunit;

namespace NextThought.Tests;

/// <summary>G5: the parity table lists all 113 Mac preference keys.</summary>
public sealed class SettingsParityTableTests
{
    [Fact]
    public void TableHasEveryMacPreferenceKey()
    {
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", ".."));
        var file = Path.Combine(directory, "SETTINGS_PARITY.md");
        Assert.True(File.Exists(file), file);
        var rows = File.ReadAllLines(file).Count(line => line.StartsWith("| `", StringComparison.Ordinal));
        Assert.Equal(113, rows);
    }
}
