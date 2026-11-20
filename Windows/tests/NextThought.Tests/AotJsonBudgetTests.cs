using Xunit;

namespace NextThought.Tests;

/// <summary>H6: new code must not increase the Native AOT JSON warning list.</summary>
public sealed class AotJsonBudgetTests
{
    private const int Budget = 161;

    [Fact]
    public void JsonWarningRowsDoNotGrow()
    {
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", ".."));
        var file = Path.Combine(directory, "AOT_JSON_WARNINGS.csv");
        Assert.True(File.Exists(file), file);
        var rows = File.ReadAllLines(file).Count(line => line.Length > 0) - 1;
        Assert.True(rows <= Budget, $"AOT JSON warnings grew: {rows} > {Budget}");
    }
}
