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


    [Fact]
    public void MacEditedSummaryFieldsSurviveUserContentRoundTrip()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, SessionUserContent.FileName);
        File.WriteAllText(path, """
        {"schemaVersion":1,"revision":2,"updatedAt":"2026-11-20T00:00:00Z","personalNotes":"",
         "editedSummary":{"markdown":"m","title":"Mac 标题","questions":[{"text":"q"}],"model":"mac-model","unknown":{"x":1}}}
        """);
        var loaded = SessionUserContentStore.Load(directory);
        Assert.NotNull(loaded.EditedSummary);
        SessionUserContentStore.Save(loaded, directory);
        var saved = File.ReadAllText(path);
        Assert.Contains("\"title\": \"Mac 标题\"", saved);
        Assert.Contains("\"questions\"", saved);
        Assert.Contains("\"model\": \"mac-model\"", saved);
        Assert.Contains("\"unknown\"", saved);
    }

    private static void AssertArchiveDate(string json, string property)
    {
        var match = Regex.Match(json, "\"" + property + "\"\\s*:\\s*\"([^\"]+)\"");
        Assert.True(match.Success, property + " missing in " + json);
        Assert.Matches(new Regex("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}Z$"), match.Groups[1].Value);
    }
}
