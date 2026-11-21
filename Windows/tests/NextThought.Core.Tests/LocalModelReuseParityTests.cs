using System.Security.Cryptography;
using System.Text;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from Tests/NextThoughtCoreTests/LocalModelReuseTests.swift: finding already
/// downloaded model assets and copying the verified ones into a new snapshot.
/// </summary>
public sealed class LocalModelReuseParityTests
{
    private static string Scratch() => Directory.CreateTempSubdirectory("ih-local-assets-").FullName;

    private static void TryDelete(string path)
    {
        try { Directory.Delete(path, true); } catch { }
    }

    private static LocalModelFile Spec(string name, string text) => new(
        name,
        Encoding.UTF8.GetByteCount(text),
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))));

    [Fact]
    public void RefusesTraversalAndCancellationLeavesNoCommittedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-reuse-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<InvalidDataException>(() =>
                LocalModelReuse.Reuse([new LocalModelFile("../escape", 1, "x")], [root], root));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() =>
                LocalModelReuse.Search([root], [], new HashSet<string> { "model.safetensors" },
                    cancellationToken: cancellation.Token));
            Assert.Empty(Directory.GetFileSystemEntries(root));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>
    /// The Mac test pins a symlink inside a hidden cache. Creating a symlink on Windows needs
    /// Administrator or Developer Mode, so on a machine without that privilege the same bytes are
    /// written as a regular file: what is asserted (hidden cache found, only verified files copied,
    /// partial reuse, source untouched, second run copies nothing) is the same either way.
    /// </summary>
    private static bool TryCreateSymbolicLink(string path, string target)
    {
        try
        {
            File.CreateSymbolicLink(path, target);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    [Fact]
    public void HiddenCacheSymlinksAndPartialReuse()
    {
        var root = Scratch();
        try
        {
            var cache = Path.Combine(root, ".cache", "huggingface", "hub", "models--test", "snapshots", "pinned");
            Directory.CreateDirectory(cache);
            var blob = Path.Combine(root, "blob");
            File.WriteAllText(blob, "correct");
            var model = Path.Combine(cache, "model.safetensors");
            if (!TryCreateSymbolicLink(model, blob)) File.Copy(blob, model);
            File.WriteAllText(Path.Combine(cache, "config.json"), "wrong!!");

            var search = LocalModelReuse.Search([root], null, new HashSet<string> { "model.safetensors" });
            Assert.Contains(search.Directories, directory => directory == cache);
            Assert.False(search.Limited);

            var target = Path.Combine(root, "installed");
            var files = new[] { Spec("model.safetensors", "correct"), Spec("config.json", "configs") };
            var reused = LocalModelReuse.Reuse(files, search.Directories, target);
            Assert.Equal(1, reused.ReusedFiles);
            Assert.Equal(7, reused.ReusedBytes);
            Assert.Equal("correct", File.ReadAllText(Path.Combine(target, "model.safetensors")));
            Assert.False(File.Exists(Path.Combine(target, "config.json")));
            Assert.Equal("correct", File.ReadAllText(blob));
            Assert.Equal(0, LocalModelReuse.Reuse(files, search.Directories, target).ReusedFiles);
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void CorruptedDestinationIsReplacedOnlyWithValidData()
    {
        var root = Scratch();
        try
        {
            var candidate = Path.Combine(root, "candidate");
            var target = Path.Combine(root, "target");
            Directory.CreateDirectory(candidate);
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "model.safetensors"), "broken!");
            File.WriteAllText(Path.Combine(candidate, "model.safetensors"), "correct");
            var file = Spec("model.safetensors", "correct");
            Assert.Equal(1, LocalModelReuse.Reuse([file], [candidate], target).ReusedFiles);
            Assert.True(LocalModelReuse.Matches(Path.Combine(target, file.Path), file));
            Assert.Equal([file.Path], Directory.GetFileSystemEntries(target).Select(Path.GetFileName).ToArray());
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void PartialNamedRevisionIsDiscoveredWithoutWeightFile()
    {
        var root = Scratch();
        try
        {
            var revision = Path.Combine(root, ".cache", "hub", "snapshots", "pinned-revision");
            Directory.CreateDirectory(revision);
            File.WriteAllText(Path.Combine(revision, "config.json"), "configs");
            var search = LocalModelReuse.Search([root], null, new HashSet<string> { "model.safetensors", "pinned-revision" });
            var result = LocalModelReuse.Reuse([Spec("config.json", "configs")], search.Directories,
                Path.Combine(root, "output"));
            Assert.Equal(1, result.ReusedFiles);
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void IndexedExternalFilesAndBoundedTraversal()
    {
        var root = Scratch();
        try
        {
            for (var index = 0; index < 4; index++) File.WriteAllText(Path.Combine(root, "file-" + index), "");
            var external = Path.Combine(root, "external", "model.safetensors");
            var result = LocalModelReuse.Search([root], [external], new HashSet<string> { "model.safetensors" },
                maximumEntries: 1);
            Assert.True(result.Limited);
            Assert.Contains(result.Directories, directory => directory == Path.GetDirectoryName(external));
        }
        finally { TryDelete(root); }
    }
}
