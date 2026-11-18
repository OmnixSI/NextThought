using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for workspace deletion staging and the transaction wrapper.</summary>
public sealed class WorkspaceDeletionTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("nt-ws-delete-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch { }
    }

    private string FileWith(string name, string text = "x")
    {
        var path = Path.Combine(root, "data", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void ManifestIsOrderedAndUnique()
    {
        var first = FileWith("a.txt");
        var second = FileWith("b.txt");
        var moves = WorkspaceDeletionFileStaging.Manifest([first, second, first], Path.Combine(root, "staging"));
        Assert.Equal(2, moves.Count);
        Assert.Equal(first, moves[0].Source);
        Assert.EndsWith("0-a.txt", moves[0].Staged);
        Assert.EndsWith("1-b.txt", moves[1].Staged);
    }

    [Fact]
    public void SharedOriginalsAreKeptWhenAnotherJobReferencesThem()
    {
        var originals = Path.Combine(root, "Originals");
        var sharedPath = Path.Combine(originals, "shared.pdf");
        Directory.CreateDirectory(originals);
        File.WriteAllText(sharedPath, "x");
        var deleting = new[] { Job("job-1", sharedPath, "hash-1"), Job("job-2", sharedPath, "hash-1") };
        var moves = WorkspaceDeletionFileStaging.Manifest([sharedPath], Path.Combine(root, "staging"));

        var keptByHash = WorkspaceDeletionFileStaging.ExcludingSharedOriginals(moves, deleting,
            [Job("job-3", Path.Combine(originals, "other.pdf"), "hash-1")], new HashSet<string>(), originals);
        Assert.Empty(keptByHash);

        var keptByPath = WorkspaceDeletionFileStaging.ExcludingSharedOriginals(moves, deleting,
            [Job("job-3", sharedPath, "other-hash")], new HashSet<string>(), originals);
        Assert.Empty(keptByPath);

        var keptByWorkspace = WorkspaceDeletionFileStaging.ExcludingSharedOriginals(moves, deleting, [],
            new HashSet<string> { "hash-1" }, originals);
        Assert.Empty(keptByWorkspace);
    }

    [Fact]
    public void UnsharedOriginalsAndOtherFilesStayInTheMoveSet()
    {
        var originals = Path.Combine(root, "Originals");
        var original = Path.Combine(originals, "only.pdf");
        var other = Path.Combine(root, "data", "notes.json");
        Directory.CreateDirectory(originals);
        Directory.CreateDirectory(Path.GetDirectoryName(other)!);
        File.WriteAllText(original, "x");
        File.WriteAllText(other, "y");
        var deleting = new[] { Job("job-1", original, "hash-1") };
        var moves = WorkspaceDeletionFileStaging.Manifest([original, other], Path.Combine(root, "staging"));
        var kept = WorkspaceDeletionFileStaging.ExcludingSharedOriginals(moves, deleting, [],
            new HashSet<string>(), originals);
        Assert.Equal([Path.GetFullPath(original), Path.GetFullPath(other)], kept.Select(move => move.Source));

        var noDeletingJob = WorkspaceDeletionFileStaging.ExcludingSharedOriginals(moves, [], [],
            new HashSet<string>(), originals);
        Assert.Equal(2, noDeletingJob.Count);
    }

    [Fact]
    public void ADocumentJobNotBeingDeletedDoesNotRetainItself()
    {
        var originals = Path.Combine(root, "Originals");
        var original = Path.Combine(originals, "only.pdf");
        Directory.CreateDirectory(originals);
        File.WriteAllText(original, "x");
        var sameJob = Job("job-1", original, "hash-1");
        var moves = WorkspaceDeletionFileStaging.Manifest([original], Path.Combine(root, "staging"));
        var kept = WorkspaceDeletionFileStaging.ExcludingSharedOriginals(moves, [sameJob], [sameJob],
            new HashSet<string>(), originals);
        Assert.Single(kept);
    }

    private static NextThought.Core.DocumentJob Job(string id, string path, string hash) =>
        NextThought.Core.DocumentJob.Create("ws", Path.GetFileName(path)) with
        {
            Id = id,
            OriginalPath = path,
            SourceHash = hash
        };

    [Fact]
    public void StageMovesOnlyExistingSources()
    {
        var existing = FileWith("a.txt");
        var missing = Path.Combine(root, "data", "missing.txt");
        var moves = WorkspaceDeletionFileStaging.Manifest([existing, missing], Path.Combine(root, "staging"));
        WorkspaceDeletionFileStaging.Stage(moves);
        Assert.False(File.Exists(existing));
        Assert.True(File.Exists(moves[0].Staged));
        Assert.True(Directory.Exists(Path.Combine(root, "staging")));
    }

    [Fact]
    public void RollbackRestoresFilesAndRemovesStaging()
    {
        var first = FileWith("a.txt", "A");
        var second = FileWith("dir/b.txt", "B");
        var staging = Path.Combine(root, "staging");
        var moves = WorkspaceDeletionFileStaging.Manifest([first, second], staging);
        WorkspaceDeletionFileStaging.Stage(moves);
        Assert.False(File.Exists(first));
        WorkspaceDeletionFileStaging.Rollback(moves);
        Assert.Equal("A", File.ReadAllText(first));
        Assert.Equal("B", File.ReadAllText(second));
        Assert.False(Directory.Exists(staging));
    }

    [Fact]
    public void RollbackRefusesToOverwrite()
    {
        var first = FileWith("a.txt", "A");
        var moves = WorkspaceDeletionFileStaging.Manifest([first], Path.Combine(root, "staging"));
        WorkspaceDeletionFileStaging.Stage(moves);
        File.WriteAllText(first, "new");
        Assert.Throws<IOException>(() => WorkspaceDeletionFileStaging.Rollback(moves));
    }

    [Fact]
    public void RemoveStagedFilesIsIdempotent()
    {
        var staging = Path.Combine(root, "staging");
        Directory.CreateDirectory(staging);
        WorkspaceDeletionFileStaging.RemoveStagedFiles(staging);
        Assert.False(Directory.Exists(staging));
        WorkspaceDeletionFileStaging.RemoveStagedFiles(staging);
    }

    [Fact]
    public async Task TransactionRunsCommitOnlyWhenAuthorized()
    {
        var steps = new List<string>();
        var ran = await WorkspaceDeletionTransaction.Execute(
            prepare: () => { steps.Add("prepare"); return Task.FromResult("plan"); },
            isAuthorized: _ => { steps.Add("authorize"); return true; },
            rollback: _ => { steps.Add("rollback"); return Task.CompletedTask; },
            commit: _ => { steps.Add("commit"); return Task.CompletedTask; },
            stage: _ => { steps.Add("stage"); return Task.CompletedTask; });
        Assert.True(ran);
        Assert.Equal(["prepare", "authorize", "stage", "authorize", "commit"], steps);
    }

    [Fact]
    public async Task UnauthorizedPlanRollsBackWithoutCommitting()
    {
        var steps = new List<string>();
        var ran = await WorkspaceDeletionTransaction.Execute(
            prepare: () => { steps.Add("prepare"); return Task.FromResult("plan"); },
            isAuthorized: _ => { steps.Add("authorize"); return false; },
            rollback: _ => { steps.Add("rollback"); return Task.CompletedTask; },
            commit: _ => { steps.Add("commit"); return Task.CompletedTask; });
        Assert.False(ran);
        Assert.Equal(["prepare", "authorize", "rollback"], steps);
    }

    [Fact]
    public async Task StageFailureRollsBackAndRethrows()
    {
        var steps = new List<string>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => WorkspaceDeletionTransaction.Execute(
            prepare: () => Task.FromResult("plan"),
            isAuthorized: _ => true,
            rollback: _ => { steps.Add("rollback"); return Task.CompletedTask; },
            commit: _ => { steps.Add("commit"); return Task.CompletedTask; },
            stage: _ => throw new InvalidOperationException("stage")));
        Assert.Equal(["rollback"], steps);
    }

    [Fact]
    public async Task CommitFailureRollsBackAndRethrows()
    {
        var steps = new List<string>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => WorkspaceDeletionTransaction.Execute(
            prepare: () => Task.FromResult("plan"),
            isAuthorized: _ => true,
            rollback: _ => { steps.Add("rollback"); return Task.CompletedTask; },
            commit: _ => throw new InvalidOperationException("commit")));
        Assert.Equal(["rollback"], steps);
    }
}
