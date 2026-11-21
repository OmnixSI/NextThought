using System.Security.Cryptography;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

public sealed class DeploymentTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "NextThought-deployment-tests", Guid.NewGuid().ToString("N"));
    private string Install => Path.Combine(root, "程序 目录");
    private string Data => Path.Combine(root, "用户 数据");
    private DeploymentStore Store => new(Install, Data);
    private async Task Package(string version)
    {
        var directory = Store.VersionDirectory(version); Directory.CreateDirectory(directory);
        var bytes = System.Text.Encoding.UTF8.GetBytes("public test binary " + version); await File.WriteAllBytesAsync(Path.Combine(directory, "NextThought.exe"), bytes);
        var manifest = new ReleaseManifest(DeploymentStore.Product, version, "test-revision", 1, "unsigned-test-fixture",
            [new("NextThought.exe", bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)))]);
        await File.WriteAllTextAsync(Path.Combine(directory, "release-manifest.json"), JsonSerializer.Serialize(manifest, JsonDefaults.Options));
    }
    [Fact]
    public async Task UpgradeRollbackAndIdempotencePreserveData()
    {
        Directory.CreateDirectory(Data); var file = Path.Combine(Data, "实际资料.txt"); await File.WriteAllTextAsync(file, "existing data must not change"); var original = await File.ReadAllBytesAsync(file);
        await Package("0.3.1"); await Package("0.4.0"); var store = Store;
        var initial = await store.ActivateAsync("0.3.1", "NextThought QA", false); Assert.Null(initial.PreviousVersion);
        await store.ActivateAsync("0.4.0", "NextThought QA", false);
        var again = await store.ActivateAsync("0.4.0", "NextThought QA", false); Assert.Equal("0.3.1", again.PreviousVersion);
        var rollback = await store.RollbackAsync(); Assert.Equal("0.3.1", rollback.CurrentVersion); Assert.Equal("0.4.0", rollback.PreviousVersion);
        Assert.Equal(original, await File.ReadAllBytesAsync(file)); Assert.Equal("0.3.1", store.Load()!.CurrentVersion);
    }
    [Fact]
    public async Task CorruptUpgradeCannotChangeActiveVersion()
    {
        await Package("0.3.1"); await Package("0.4.0"); var store = Store; await store.ActivateAsync("0.3.1", "NextThought QA", false);
        var before = await File.ReadAllBytesAsync(store.StateFile);
        await File.AppendAllTextAsync(Path.Combine(store.VersionDirectory("0.4.0"), "NextThought.exe"), "tampered");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ActivateAsync("0.4.0", "NextThought QA", false));
        Assert.Equal(before, await File.ReadAllBytesAsync(store.StateFile));
    }
    [Fact]
    public async Task CorruptRollbackCannotChangeActiveVersion()
    {
        await Package("0.3.1"); await Package("0.4.0"); var store = Store;
        await store.ActivateAsync("0.3.1", "NextThought QA", false); await store.ActivateAsync("0.4.0", "NextThought QA", false);
        var before = await File.ReadAllBytesAsync(store.StateFile);
        File.Delete(Path.Combine(store.VersionDirectory("0.3.1"), "NextThought.exe"));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.RollbackAsync());
        Assert.Equal(before, await File.ReadAllBytesAsync(store.StateFile));
    }
    [Fact]
    public async Task UnlistedDllAndTraversalAreRejected()
    {
        await Package("0.4.0"); var store = Store;
        await File.WriteAllTextAsync(Path.Combine(store.VersionDirectory("0.4.0"), "unlisted.dll"), "untrusted");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.VerifyAsync("0.4.0"));
        Assert.Throws<InvalidDataException>(() => store.VersionDirectory("../0.4.0"));
        Assert.Throws<InvalidDataException>(() => new DeploymentStore(Data, Data));
        Assert.Throws<InvalidDataException>(() => new DeploymentStore(root, Data));
        Assert.Throws<InvalidDataException>(() => new DeploymentStore(Path.Combine(Data, "versions"), Data));
    }
    [Fact]
    public async Task ShellFailureLeavesActiveStateUntouched()
    {
        await Package("0.3.1"); await Package("0.4.0"); var store = Store; await store.ActivateAsync("0.3.1", "NextThought QA", false);
        var before = await File.ReadAllBytesAsync(store.StateFile);
        await Assert.ThrowsAsync<IOException>(() => store.ActivateAsync("0.4.0", "NextThought QA", false, applyShell: _ => throw new IOException("controlled shell failure")));
        Assert.Equal(before, await File.ReadAllBytesAsync(store.StateFile));
    }
    [Fact]
    public async Task FutureContractIsRejectedBeforeShellChanges()
    {
        await Package("0.3.1"); await Package("0.4.0"); var store = Store; await store.ActivateAsync("0.3.1", "NextThought QA", false);
        var before = await File.ReadAllBytesAsync(store.StateFile); var file = Path.Combine(store.VersionDirectory("0.4.0"), "release-manifest.json");
        var manifest = JsonSerializer.Deserialize<ReleaseManifest>(await File.ReadAllTextAsync(file), JsonDefaults.Options)!;
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(manifest with { ArchiveSchema = 99 }, JsonDefaults.Options));
        var shellCalled = false;
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ActivateAsync("0.4.0", "NextThought QA", false, applyShell: _ => { shellCalled = true; return Task.CompletedTask; }));
        Assert.False(shellCalled); Assert.Equal(before, await File.ReadAllBytesAsync(store.StateFile));
    }
    public void Dispose()
    {
        var absolute = Path.GetFullPath(root);
        if (!absolute.StartsWith(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "NextThought-deployment-tests")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe fixture path");
        if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
    }
}
