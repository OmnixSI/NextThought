using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the content-addressed incremental backup store.</summary>
public sealed class IncrementalBackupTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("nt-backup-store-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch { }
    }

    private static byte[] Bytes(params byte[] values) => values;

    private IncrementalBackupStore Store() => new(Path.Combine(root, "store"));

    private static string[] Manifests(IncrementalBackupStore store) =>
        Directory.GetFiles(Path.Combine(store.Root, "RestorePoints"), "*.ntrestore");

    [Fact]
    public void CreatesAndRestoresContentAddressedFiles()
    {
        var store = Store();
        var point = store.Create(new Dictionary<string, byte[]>
        {
            ["a.txt"] = Bytes(1, 2, 3),
            ["dir/b.txt"] = Bytes(4)
        }, reason: "manual");

        Assert.True(File.Exists(point));
        var destination = Path.Combine(root, "restored");
        store.Restore(point, destination);
        Assert.Equal(Bytes(1, 2, 3), File.ReadAllBytes(Path.Combine(destination, "a.txt")));
        Assert.Equal(Bytes(4), File.ReadAllBytes(Path.Combine(destination, "dir", "b.txt")));
        Assert.Equal(4, store.StorageBytes());
        Assert.Single(Manifests(store));
    }

    [Fact]
    public void IdenticalSnapshotsDoNotCreateNewPoints()
    {
        var store = Store();
        var files = new Dictionary<string, byte[]> { ["a.txt"] = Bytes(1) };
        var first = store.Create(files, reason: "one");
        var second = store.Create(files, reason: "two");
        Assert.Equal(first, second);
        Assert.Single(Manifests(store));
    }

    [Fact]
    public void SameContentIsStoredOnce()
    {
        var store = Store();
        store.Create(new Dictionary<string, byte[]>
        {
            ["a.txt"] = Bytes(9, 9),
            ["b.txt"] = Bytes(9, 9)
        });
        Assert.Single(Directory.GetFiles(Path.Combine(store.Root, "Objects")));
    }

    [Fact]
    public void SnapshotsShareUnchangedObjects()
    {
        var store = Store();
        store.Create(new Dictionary<string, byte[]> { ["a.txt"] = Bytes(1) });
        store.Create(new Dictionary<string, byte[]> { ["a.txt"] = Bytes(1), ["c.txt"] = Bytes(3) });
        Assert.Equal(2, Directory.GetFiles(Path.Combine(store.Root, "Objects")).Length);
        Assert.Equal(2, Manifests(store).Length);
    }

    [Fact]
    public void DirectorySnapshotsUsePrefixes()
    {
        var sessions = Path.Combine(root, "Sessions");
        Directory.CreateDirectory(Path.Combine(sessions, "nested"));
        File.WriteAllBytes(Path.Combine(sessions, "nested", "x.txt"), Bytes(7));
        var store = Store();
        var point = store.Create(new Dictionary<string, byte[]>(),
            new Dictionary<string, string> { ["Sessions"] = sessions, ["Missing"] = Path.Combine(root, "nope") });
        var destination = Path.Combine(root, "restored");
        store.Restore(point, destination);
        Assert.Equal(Bytes(7), File.ReadAllBytes(Path.Combine(destination, "Sessions", "nested", "x.txt")));
    }

    [Fact]
    public void TamperedObjectsAreRejected()
    {
        var store = Store();
        var point = store.Create(new Dictionary<string, byte[]> { ["a.txt"] = Bytes(1, 2, 3) });
        foreach (var file in Directory.GetFiles(Path.Combine(store.Root, "Objects")))
            File.WriteAllBytes(file, Bytes(9, 9, 9));
        Assert.Throws<InvalidDataException>(() => store.Restore(point, Path.Combine(root, "restored")));
    }

    [Fact]
    public void ForeignManifestsAreRejected()
    {
        var store = Store();
        var point = store.Create(new Dictionary<string, byte[]> { ["a.txt"] = Bytes(1) });
        var copied = Path.Combine(root, "copy.ntrestore");
        File.Copy(point, copied);
        Assert.Throws<UnauthorizedAccessException>(() => store.Restore(copied, Path.Combine(root, "restored")));
    }

    [Theory]
    [InlineData("../x", false)]
    [InlineData("/abs", false)]
    [InlineData("a\\b", false)]
    [InlineData("a//b", false)]
    [InlineData("", false)]
    [InlineData("a/b", true)]
    [InlineData("a.txt", true)]
    public void SafePathsStayInsideTheStore(string path, bool expected)
    {
        Assert.Equal(expected, IncrementalBackupStore.SafePath(path));
    }

    [Fact]
    public void PruningKeepsTheRequestedNumberOfPoints()
    {
        var store = Store();
        for (var index = 0; index < 3; index++)
            store.Create(new Dictionary<string, byte[]> { ["a.txt"] = Bytes((byte)index) }, keep: 2);
        Assert.Equal(2, Manifests(store).Length);
        var latest = Manifests(store).OrderByDescending(path => File.GetLastWriteTimeUtc(path)).First();
        var destination = Path.Combine(root, "restored");
        store.Restore(latest, destination);
        Assert.True(File.Exists(Path.Combine(destination, "a.txt")));
    }

    [Fact]
    public void UnreferencedObjectsAreDeleted()
    {
        var store = Store();
        store.Create(new Dictionary<string, byte[]> { ["a.txt"] = Bytes(1) });
        store.Create(new Dictionary<string, byte[]> { ["b.txt"] = Bytes(2) }, keep: 1);
        Assert.Single(Directory.GetFiles(Path.Combine(store.Root, "Objects")));
    }
}
