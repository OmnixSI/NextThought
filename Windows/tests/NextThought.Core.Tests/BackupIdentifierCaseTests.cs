using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>A6: Mac upper-cases UUIDs on export; importing that copy must not duplicate series,
/// lessons or questions, and Windows writes the upper-case file name.</summary>
public sealed class BackupIdentifierCaseTests
{
    [Fact]
    public void UpperCasedIdentifiersMergeIntoTheSameSeries()
    {
        var seriesId = Guid.NewGuid().ToString("D");
        var lessonId = Guid.NewGuid().ToString("D");
        var questionId = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var lesson = new WorkspaceLesson { Id = lessonId, Text = "\u4E00\u6761\u7ECF\u9A8C", CreatedAt = now };
        var question = new QuestionBankItem { Id = questionId, Question = "\u4E3A\u4EC0\u4E48", Answer = "\u56E0\u4E3A", TimesAsked = 2 };
        var original = new WorkspaceSeries(seriesId, "\u7CFB\u5217", now, [], [lesson]) { QuestionBank = [question] };
        var upper = original with
        {
            Id = seriesId.ToUpperInvariant(),
            Lessons = [lesson with { Id = lessonId.ToUpperInvariant() }],
            QuestionBank = [question with { Id = questionId.ToUpperInvariant() }]
        };

        var merged = BackupMerge.Workspaces([original], [upper]);
        var series = Assert.Single(merged.Merged);
        Assert.Single(series.Lessons);
        Assert.Single(series.QuestionBank);
        Assert.Equal(0, merged.Added);
    }

    [Fact]
    public void TheSeriesFileIsWrittenWithAnUpperCaseUuid()
    {
        var id = Guid.NewGuid().ToString("D");
        var directory = Path.Combine(Path.GetTempPath(), "nt-workspace-case-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new WorkspaceStore(directory);
            store.Save(new WorkspaceData([new WorkspaceSeries(id, "\u7CFB\u5217", DateTimeOffset.UtcNow, [], [])]));
            var path = Path.Combine(directory, "Workspaces", id.ToUpperInvariant() + ".json");
            Assert.True(File.Exists(path), path);
        }
        finally { try { Directory.Delete(directory, true); } catch { } }
    }
}
