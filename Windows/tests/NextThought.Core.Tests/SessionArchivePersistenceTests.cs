using System.Text;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for archive directory reading, markers, receipts, appends and leftovers.</summary>
public sealed class SessionArchivePersistenceTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("nt-archive-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch { }
    }

    [Fact]
    public void MissingRootIsNewUnlessContentWasExpected()
    {
        var fresh = SessionArchiveDirectoryReader.Read(root, false, _ => throw new DirectoryNotFoundException(), _ => true);
        Assert.Empty(Assert.IsType<SessionArchiveDirectoryReadResult.Contents>(fresh).Items);

        var known = SessionArchiveDirectoryReader.Read(root, true, _ => throw new DirectoryNotFoundException(), _ => true);
        var unavailable = Assert.IsType<SessionArchiveDirectoryReadResult.Unavailable>(known);
        Assert.Equal(root, unavailable.Path);
        Assert.Contains("原记录未显示或删除", unavailable.Reason);
    }

    [Fact]
    public void OtherReadErrorsSurfaceTheirReason()
    {
        var result = SessionArchiveDirectoryReader.Read(root, false, _ => throw new IOException("拒绝访问"), _ => false);
        Assert.Equal("拒绝访问", Assert.IsType<SessionArchiveDirectoryReadResult.Unavailable>(result).Reason);
    }

    [Fact]
    public void DirectoryContentsAreReturned()
    {
        var result = SessionArchiveDirectoryReader.Read(root, false, _ => ["a", "b"], _ => false);
        Assert.Equal(["a", "b"], Assert.IsType<SessionArchiveDirectoryReadResult.Contents>(result).Items);
    }

    [Fact]
    public void ContentMarkerRecordsItsEvidence()
    {
        var marker = Path.Combine(root, "marker", ".sessions-archive");
        Assert.False(SessionArchiveContentMarker.WasPreviouslyPopulated(marker));
        SessionArchiveContentMarker.Record(marker);
        Assert.True(SessionArchiveContentMarker.WasPreviouslyPopulated(marker));
        Assert.Equal("sessions-archive-present-v1", File.ReadAllText(marker));
        SessionArchiveContentMarker.Record(marker);
        Assert.Equal("sessions-archive-present-v1", File.ReadAllText(marker));
    }

    [Fact]
    public void ReceiptsTrackOnlyPositiveRevisions()
    {
        var receipts = new SessionTranslationWriteReceipts();
        Assert.False(receipts.HasPending);
        receipts.RecordSubmittedRevision(0);
        receipts.RecordSubmittedRevision(-1);
        receipts.RecordSubmittedRevision(2);
        receipts.RecordSubmittedRevision(3);
        Assert.True(receipts.HasPending);
        Assert.False(receipts.Acknowledge(1));
        Assert.True(receipts.Acknowledge(2));
        Assert.True(receipts.HasPending);
        Assert.True(receipts.Acknowledge(5));
        Assert.False(receipts.HasPending);
        Assert.False(receipts.Acknowledge(5));
    }

    [Fact]
    public void AppendKeepsLinesSeparated()
    {
        var path = Path.Combine(root, "nested", "transcript.jsonl");
        var appender = SessionArchiveFileAppender.Live();
        appender.AppendLine("{\"a\":1}", path);
        appender.AppendLine("{\"b\":2}", path);
        Assert.Equal("{\"a\":1}\n{\"b\":2}\n", File.ReadAllText(path));
        Assert.Equal(2, File.ReadAllLines(path).Length);
    }

    [Fact]
    public void AppendRepairsAHistoricalMissingNewline()
    {
        var path = Path.Combine(root, "transcript.jsonl");
        File.WriteAllText(path, "old");
        SessionArchiveFileAppender.Live().AppendLine("new", path);
        Assert.Equal("old\nnew\n", File.ReadAllText(path));
    }

    [Fact]
    public void AppendPreservesAnExistingNewline()
    {
        var path = Path.Combine(root, "transcript.jsonl");
        File.WriteAllText(path, "old\n");
        SessionArchiveFileAppender.Live().AppendLine("new", path);
        Assert.Equal("old\nnew\n", File.ReadAllText(path));
    }

    [Fact]
    public void ReplaceOverwritesAtomically()
    {
        var path = Path.Combine(root, "meta.json");
        File.WriteAllText(path, "old");
        SessionArchiveFileAppender.Live().Replace(Encoding.UTF8.GetBytes("new"), path);
        Assert.Equal("new", File.ReadAllText(path));
        Assert.Empty(Directory.EnumerateFiles(root, "*.tmp-*"));
    }

    [Fact]
    public void CustomAppenderDelegatesEveryMode()
    {
        var calls = new List<SessionArchiveWriteMode>();
        var appender = new SessionArchiveFileAppender((_, _, mode) => calls.Add(mode));
        appender.Append([], "x");
        appender.Replace([], "x");
        Assert.Equal([SessionArchiveWriteMode.Append, SessionArchiveWriteMode.Replace], calls);
    }

    [Fact]
    public void LeftoversNeedNoMetaAndNoRecordingFiles()
    {
        Assert.True(SessionArchiveLeftover.IsLeftover([]));
        Assert.True(SessionArchiveLeftover.IsLeftover([".hidden", "notes.json", "summary.json"]));
        Assert.False(SessionArchiveLeftover.IsLeftover(["meta.json", "notes.json"]));
        Assert.False(SessionArchiveLeftover.IsLeftover(["transcript.jsonl"]));
        Assert.False(SessionArchiveLeftover.IsLeftover(["context.json"]));
        Assert.True(SessionArchiveLeftover.IsLeftover(["user-content.json"]));
    }
}
