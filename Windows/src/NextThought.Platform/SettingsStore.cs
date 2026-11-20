using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

public sealed class SettingsStore(AppPaths paths)
{
    private readonly string secretFile = Path.Combine(paths.Root, "Credentials", "answer-api.dpapi");
    private readonly string cloudSttSecretFile = Path.Combine(paths.Root, "Credentials", "cloud-stt.dpapi");
    private readonly string tavilySecretFile = Path.Combine(paths.Root, "Credentials", "tavily.dpapi");
    private static readonly byte[] entropy = Encoding.UTF8.GetBytes("com.nextthought.windows/answer.api/v1");
    private static readonly byte[] cloudSttEntropy = Encoding.UTF8.GetBytes("com.nextthought.windows/cloud.stt/v1");
    private static readonly byte[] tavilyEntropy = Encoding.UTF8.GetBytes("com.nextthought.windows/tavily/v1");
    public AppSettings Load() => File.Exists(paths.Settings)
        ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(paths.Settings), JsonDefaults.Options) ?? new() : new();
    public Task SaveAsync(AppSettings settings, CancellationToken ct = default) => AppPaths.AtomicWriteAsync(paths.Settings, JsonSerializer.Serialize(settings, JsonDefaults.Options), ct);
    public string ReadApiKey()
    {
        if (!File.Exists(secretFile)) return "";
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(secretFile), entropy, DataProtectionScope.CurrentUser));
    }
    public void SaveApiKey(string key)
    {
        if (key.Length == 0) { if (File.Exists(secretFile)) File.Delete(secretFile); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(secretFile)!);
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), entropy, DataProtectionScope.CurrentUser);
        var temporary = secretFile + ".tmp";
        File.WriteAllBytes(temporary, bytes); File.Move(temporary, secretFile, true);
    }

    /// <summary>True when the DPAPI read failed; an empty key must then not overwrite the file.</summary>
    public bool CloudSttKeyReadFailed { get; private set; }

    public string ReadCloudSttKey()
    {
        CloudSttKeyReadFailed = false;
        try
        {
            if (!File.Exists(cloudSttSecretFile)) return "";
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(cloudSttSecretFile), cloudSttEntropy, DataProtectionScope.CurrentUser));
        }
        catch
        {
            CloudSttKeyReadFailed = true;
            return "";
        }
    }

    public void SaveCloudSttKey(string key)
    {
        if (key.Length == 0)
        {
            if (CloudSttKeyReadFailed && File.Exists(cloudSttSecretFile)) return;
            if (File.Exists(cloudSttSecretFile)) File.Delete(cloudSttSecretFile);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(cloudSttSecretFile)!);
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), cloudSttEntropy, DataProtectionScope.CurrentUser);
        var temporary = cloudSttSecretFile + ".tmp";
        File.WriteAllBytes(temporary, bytes); File.Move(temporary, cloudSttSecretFile, true);
    }

    public string ReadTavilyKey()
    {
        if (!File.Exists(tavilySecretFile)) return "";
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(tavilySecretFile), tavilyEntropy, DataProtectionScope.CurrentUser));
    }

    public void SaveTavilyKey(string key)
    {
        if (key.Length == 0) { if (File.Exists(tavilySecretFile)) File.Delete(tavilySecretFile); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(tavilySecretFile)!);
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), tavilyEntropy, DataProtectionScope.CurrentUser);
        var temporary = tavilySecretFile + ".tmp";
        File.WriteAllBytes(temporary, bytes); File.Move(temporary, tavilySecretFile, true);
    }
}
