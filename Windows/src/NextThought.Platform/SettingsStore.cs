using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

public sealed class SettingsStore(AppPaths paths)
{
    private readonly string secretFile = Path.Combine(paths.Root, "Credentials", "answer-api.dpapi");
    private static readonly byte[] entropy = Encoding.UTF8.GetBytes("com.nextthought.windows/answer.api/v1");
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
}
