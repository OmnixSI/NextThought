using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>F7: only wss:// and the Mac provider hosts may receive the cloud STT key.</summary>
public sealed class CloudSttEndpointPolicyTests
{
    [Fact]
    public void AcceptsOnlyWssAndKnownProviderHosts()
    {
        Assert.Null(CloudSttEndpointPolicy.FromSetting(""));
        Assert.Equal("stt-rt.soniox.com", CloudSttEndpointPolicy.FromSetting("wss://stt-rt.soniox.com/transcribe")!.Host);
        Assert.Throws<CloudTranscriptException>(() => CloudSttEndpointPolicy.FromSetting("ws://stt-rt.soniox.com/transcribe"));
        Assert.Throws<CloudTranscriptException>(() => CloudSttEndpointPolicy.FromSetting("wss://evil.example/stt"));
        Assert.Throws<CloudTranscriptException>(() => CloudSttEndpointPolicy.FromSetting("https://api.openai.com/v1"));
    }

    [Fact]
    public void FailedKeyReadDoesNotDeleteTheStoredSecret()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-key-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(root, "Credentials", "cloud-stt.dpapi");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bytes = new byte[] { 1, 2, 3, 4 };
            File.WriteAllBytes(path, bytes);
            var store = new SettingsStore(new AppPaths(root));
            Assert.Equal("", store.ReadCloudSttKey());
            Assert.True(store.CloudSttKeyReadFailed);
            store.SaveCloudSttKey("");
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
