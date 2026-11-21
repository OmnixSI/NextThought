using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the post-exit restore script.</summary>
public sealed class UpdateRestoreTests
{
    [Fact]
    public void ScriptWaitsCopiesAndStarts()
    {
        var script = UpdateRestore.Script(@"C:\Backups\0.65.0", @"C:\Program Files\NextThought", @"C:\Program Files\NextThought\NextThought.exe", 4321);
        Assert.Contains("Wait-Process -Id 4321 -Timeout 60", script);
        Assert.Contains(@"Get-ChildItem -LiteralPath 'C:\Backups\0.65.0' -Force | Copy-Item -Destination 'C:\Program Files\NextThought' -Recurse -Force", script);
        Assert.Contains(@"Copy-Item -LiteralPath 'C:\Backups\0.65.0' -Destination 'C:\Program Files\NextThought' -Recurse -Force", script);
        Assert.Contains(@"Start-Process -FilePath 'C:\Program Files\NextThought\NextThought.exe'", script);
    }

    [Fact]
    public void QuotesInPathsAreEscaped()
    {
        var script = UpdateRestore.Script(@"C:\Bob's backup", @"C:\Target", @"C:\Target\App.exe", 1);
        Assert.Contains(@"'C:\Bob''s backup'", script);
    }

    [Fact]
    public void ScriptStopsOnErrors()
    {
        Assert.StartsWith("$ErrorActionPreference = 'Stop'", UpdateRestore.Script("a", "b", "c", 1));
    }
}
