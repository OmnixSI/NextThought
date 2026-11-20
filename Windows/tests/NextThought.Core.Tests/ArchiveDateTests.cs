using System.Text.Json;
using System.Text.RegularExpressions;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>A1: every archive date is ISO-8601 UTC, whole seconds, ending in Z.</summary>
public sealed class ArchiveDateTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nt-archive-date-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(directory, true); } catch { /* best effort */ }
    }

    [Fact]
    public void ArchiveDatesAreUtcWholeSecondsWithZ()
    {
        var now = DateTimeOffset.Now;
        SessionUserContentStore.Save(new SessionUserContent { UpdatedAt = now }, directory);
        AssertArchiveDate(File.ReadAllText(Path.Combine(directory, SessionUserContent.FileName)), "updatedAt");

        var skills = new SkillStore(directory);
        skills.Save([new Skill { Name = "date-check", ImportedAt = now }]);
        AssertArchiveDate(File.ReadAllText(skills.FilePath), "importedAt");

        var payload = new StructuredNotesPayload(NotesFormat.Standard, generatedAt: now);
        AssertArchiveDate(JsonSerializer.Serialize(payload, JsonDefaults.ArchiveOptions), "generatedAt");
    }

    private static void AssertArchiveDate(string json, string property)
    {
        var match = Regex.Match(json, "\"" + property + "\"\\s*:\\s*\"([^\"]+)\"");
        Assert.True(match.Success, property + " missing in " + json);
        Assert.Matches(new Regex("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}Z$"), match.Groups[1].Value);
    }
}
