using Xunit;

namespace NextThought.Tests;

/// <summary>H3: only Platform declares native entry points; Desktop uses Win32Native.</summary>
public sealed class NativeBoundaryTests
{
    [Fact]
    public void DesktopHasNoDllImportOrLibraryImport()
    {
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..", "src", "NextThought.Desktop"));
        Assert.True(Directory.Exists(directory), directory);
        var offenders = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("DllImport", StringComparison.Ordinal)
                           || File.ReadAllText(file).Contains("LibraryImport", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(offenders);
    }
}
