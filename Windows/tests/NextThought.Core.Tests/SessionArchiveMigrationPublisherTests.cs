using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the archive migration publisher and its rollback.</summary>
public sealed class SessionArchiveMigrationPublisherTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("nt-migration-").FullName;

    public void Dispose()
    {
        try { Delete(root); } catch { }
    }

    private static void Delete(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    private static void Stage(string id, string folder) => Stage(id, folder, "content");

    private static void Stage(string id, string folder, string text)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "meta.json"), text);
    }

    private static void Validate(string id, string folder)
    {
        if (!File.Exists(Path.Combine(folder, "meta.json"))) throw new InvalidDataException("missing meta");
    }

    [Fact]
    public void PublishesStagedFoldersAndRemovesStaging()
    {
        var archive = Path.Combine(root, "Sessions");
        var staged = new List<string>();
        SessionArchiveMigrationPublisher.Publish(["a", "b"], archive,
            (id, folder) => { staged.Add(id); Stage(id, folder, id); }, Validate);
        Assert.Equal(["a", "b"], staged);
        Assert.Equal("a", File.ReadAllText(Path.Combine(archive, "a", "meta.json")));
        Assert.False(Directory.EnumerateDirectories(Path.GetDirectoryName(archive)!, ".*migration-*").Any());
    }

    [Fact]
    public void DuplicateOrUnsafeIdsAreRejected()
    {
        var archive = Path.Combine(root, "Sessions");
        Assert.Throws<ArgumentException>(() => SessionArchiveMigrationPublisher.Publish(["a", "a"], archive, Stage, Validate));
        Assert.Throws<ArgumentException>(() => SessionArchiveMigrationPublisher.Publish(["../x"], archive, Stage, Validate));
        Assert.Throws<ArgumentException>(() => SessionArchiveMigrationPublisher.Publish([""], archive, Stage, Validate));
    }

    [Fact]
    public void ExistingFolderBlocksPublishUnlessAllowed()
    {
        var archive = Path.Combine(root, "Sessions");
        Directory.CreateDirectory(Path.Combine(archive, "a"));
        File.WriteAllText(Path.Combine(archive, "a", "meta.json"), "old");
        Assert.Throws<IOException>(() => SessionArchiveMigrationPublisher.Publish(["a"], archive, Stage, Validate));
        Assert.Equal("old", File.ReadAllText(Path.Combine(archive, "a", "meta.json")));
    }

    [Fact]
    public void ReplacementBacksUpTheOldFolder()
    {
        var archive = Path.Combine(root, "Sessions");
        Directory.CreateDirectory(Path.Combine(archive, "a"));
        File.WriteAllText(Path.Combine(archive, "a", "meta.json"), "old");
        SessionArchiveMigrationPublisher.Publish(["a"], archive,
            (id, folder) => Stage(id, folder, "new"), Validate, canReplaceExisting: (_, _) => true);
        Assert.Equal("new", File.ReadAllText(Path.Combine(archive, "a", "meta.json")));
    }

    [Fact]
    public void FailedCommitRollsBackPublishedFolders()
    {
        var archive = Path.Combine(root, "Sessions");
        Directory.CreateDirectory(Path.Combine(archive, "b"));
        File.WriteAllText(Path.Combine(archive, "b", "meta.json"), "old-b");
        Assert.Throws<InvalidOperationException>(() => SessionArchiveMigrationPublisher.Publish(["a", "b"], archive,
            Stage, Validate, canReplaceExisting: (_, _) => true,
            commit: () => throw new InvalidOperationException("commit failed")));
        Assert.False(Directory.Exists(Path.Combine(archive, "a")));
        Assert.Equal("old-b", File.ReadAllText(Path.Combine(archive, "b", "meta.json")));
    }

    [Fact]
    public void MoveFailureSurfacesAndCleansStaging()
    {
        var archive = Path.Combine(root, "Sessions");
        var error = Assert.Throws<IOException>(() =>
            SessionArchiveMigrationPublisher.Publish(["a"], archive, Stage, Validate,
                move: (_, _) => throw new IOException("move failed")));
        Assert.Contains("move failed", error.Message);
        Assert.False(Directory.Exists(Path.Combine(archive, "a")));
        Assert.Null(SessionArchiveMigrationPublisher.PendingRecoveryDirectory(archive));
    }

    [Fact]
    public void PendingRecoveryIsNullWhenThereIsNoLeftover()
    {
        var archive = Path.Combine(root, "Sessions");
        Directory.CreateDirectory(archive);
        Assert.Null(SessionArchiveMigrationPublisher.PendingRecoveryDirectory(archive));
        Assert.Null(SessionArchiveMigrationPublisher.PendingRecoveryDirectory(Path.Combine(root, "missing")));
    }
}
