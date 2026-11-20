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
}
