using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    private static readonly JsonSerializerOptions WriteOptions = new(JsonDefaults.Options)
    {
        WriteIndented = true
    };

    /// <summary>
    /// G5: settings.json uses the Mac preference key names for all 113 preferences (G6 keeps
    /// unknown keys untouched). Files written by older Windows builds keep working: their
    /// camelCase fields deserialize into <see cref="AppSettings"/> as before and are rewritten
    /// under the Mac key names on the next save.
    /// </summary>
    public AppSettings Load()
    {
        if (!File.Exists(paths.Settings)) return new AppSettings();
        var text = File.ReadAllText(paths.Settings);
        JsonObject? document = null;
        try { document = JsonNode.Parse(text) as JsonObject; }
        catch (JsonException) { }
        AppSettings settings;
        if (document is not null)
        {
            // Mac-typed keys that do not fit the Windows typed model are applied after deserializing.
            var typed = MacPreferenceMap.WithoutTypedShapeConflicts(document);
            settings = typed.Deserialize<AppSettings>(JsonDefaults.Options) ?? new AppSettings();
            MacPreferenceMap.ApplyJson(settings, document);
        }
        else
        {
            settings = JsonSerializer.Deserialize<AppSettings>(text, JsonDefaults.Options) ?? new AppSettings();
        }
        return settings;
    }

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        var document = MacPreferenceMap.ToJsonObject(settings);
        return AppPaths.AtomicWriteAsync(paths.Settings, document.ToJsonString(WriteOptions), ct);
    }

    /// <summary>G5: every Mac preference key with its current value (Mac defaults where unset).</summary>
    public Dictionary<string, object?> LoadMacPreferences() => MacPreferenceMap.ReadAll(Load());

    /// <summary>G5: one Mac preference key, with the Mac default when the key is not stored.</summary>
    public object? LoadMacValue(string key)
    {
        if (!MacPreferences.IsKnown(key)) throw new ArgumentOutOfRangeException(nameof(key), key, "not a Mac preference key");
        return LoadMacPreferences()[key];
    }

    /// <summary>G5: writes Mac preference keys using their Mac names/defaults, then saves.</summary>
    public async Task SaveMacPreferencesAsync(IReadOnlyDictionary<string, object?> values, CancellationToken ct = default)
    {
        foreach (var key in values.Keys)
            if (!MacPreferences.IsKnown(key)) throw new ArgumentOutOfRangeException(nameof(values), key, "not a Mac preference key");
        var settings = Load();
        MacPreferenceMap.ApplyValues(settings, values);
        await SaveAsync(settings, ct).ConfigureAwait(false);
    }

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

    // --- E27: per-account secrets (API presets, GitHub token) -----------------------------
    // Mac keeps each account in the Keychain; on Windows each account gets its own DPAPI file
    // under Credentials/ (named by a hash so any account string is safe as a file name).

    public string ReadSecret(string account)
    {
        var file = SecretFile(account);
        if (!File.Exists(file)) return "";
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file), SecretEntropy(account),
            DataProtectionScope.CurrentUser));
    }

    public void SaveSecret(string account, string secret)
    {
        var file = SecretFile(account);
        if (secret.Length == 0)
        {
            if (File.Exists(file)) File.Delete(file);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), SecretEntropy(account),
            DataProtectionScope.CurrentUser);
        var temporary = file + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, file, true);
    }

    /// <summary>E27: the GitHub token used by the updater (Mac keychain account github.token).</summary>
    public string ReadGithubToken() => ReadSecret("github.token");

    public void SaveGithubToken(string token) => SaveSecret("github.token", token);

    private string SecretFile(string account)
    {
        var name = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(account)))[..24];
        return Path.Combine(paths.Root, "Credentials", name + ".dpapi");
    }

    private static byte[] SecretEntropy(string account) =>
        SHA256.HashData(Encoding.UTF8.GetBytes("com.nextthought.windows/" + account + "/v1"));
}
