using System.Text.Json.Serialization;

namespace NextThought.Core;

/// <summary>
/// E27: a saved OpenAI-compatible answer service (port of <c>APIPreset</c>, APIPresets.swift):
/// address, model and thinking strength; its key lives in the credential store under
/// <see cref="KeyAccount"/>. Serialized with the Mac field names so the Mac key <c>apiPresets</c>
/// keeps its shape.
/// </summary>
public sealed record APIPreset
{
    public string Id { get; init; } = NewId();

    public string Name { get; init; } = "";

    [JsonPropertyName("baseURL")] public string BaseUrl { get; init; } = "";

    public string Model { get; init; } = "";

    /// <summary>"default" (not sent), or "minimal" / "low" / "medium" / "high" as reasoning_effort.</summary>
    public string Effort { get; init; } = "default";

    public string KeyAccount { get; init; } = "";

    public APIPreset() { }

    public APIPreset(string id, string name, string baseUrl, string model = "", string effort = "default",
        string? keyAccount = null)
    {
        Id = id;
        Name = name;
        BaseUrl = baseUrl;
        Model = model;
        Effort = effort;
        KeyAccount = keyAccount ?? AccountFor(id);
    }

    /// <summary>Mac's <c>UUID().uuidString</c>: uppercase with hyphens.</summary>
    public static string NewId() => Guid.NewGuid().ToString("D").ToUpperInvariant();

    public static string AccountFor(string id) => "answer.api." + id;

    /// <summary>Mac <c>APIPreset.efforts</c>, in order, with the Mac titles.</summary>
    public static IReadOnlyList<(string Value, string Title)> Efforts { get; } =
    [
        ("default", "默认（不指定）"),
        ("minimal", "minimal · 最快"),
        ("low", "low"),
        ("medium", "medium"),
        ("high", "high · 最强")
    ];

    /// <summary>Mac <c>APIPreset.templates</c>: the common providers offered for a new preset.</summary>
    public static IReadOnlyList<(string Name, string Url)> Templates { get; } =
    [
        ("OpenAI", "https://api.openai.com/v1"),
        ("DeepSeek", "https://api.deepseek.com/v1"),
        ("通义千问", "https://dashscope.aliyuncs.com/compatible-mode/v1"),
        ("Moonshot Kimi", "https://api.moonshot.cn/v1"),
        ("智谱 GLM", "https://open.bigmodel.cn/api/paas/v4"),
        ("火山方舟 豆包", "https://ark.cn-beijing.volces.com/api/v3"),
        ("Groq", "https://api.groq.com/openai/v1"),
        ("OpenRouter", "https://openrouter.ai/api/v1"),
        ("本机 Ollama", "http://localhost:11434/v1"),
        ("本机 LM Studio", "http://localhost:1234/v1")
    ];
}

/// <summary>Mac <c>EngineTestState</c> (Onboarding.swift): connectivity test of the answer service.</summary>
public enum EngineTestState { Idle, Running, Passed, Failed }

/// <summary>
/// E27: pure rules for the saved answer services (port of <c>AppState.loadAPIPresets</c> and the
/// add/rename/delete/select helpers in APIPresets.swift). The settings object is the single source
/// of truth; the credential for a preset stays in the platform credential store.
/// </summary>
public static class APIPresetLogic
{
    public static List<APIPreset> LoadAll(AppSettings settings) => settings.ApiPresets.ToList();

    /// <summary>The active preset, or null when the store is empty.</summary>
    public static APIPreset? Active(AppSettings settings) =>
        settings.ApiPresets.FirstOrDefault(preset => preset.Id == settings.ActiveApiPresetId)
        ?? settings.ApiPresets.FirstOrDefault();

    /// <summary>
    /// First run with presets: the existing single API configuration becomes "默认" (Mac names it
    /// after the known provider whose host matches, otherwise 默认). Always returns a usable store.
    /// </summary>
    public static AppSettings EnsureLoaded(AppSettings settings)
    {
        if (settings.ApiPresets.Count == 0)
        {
            var name = PresetNameFor(settings.BaseUrl);
            var preset = new APIPreset(APIPreset.NewId(), name, settings.BaseUrl, settings.Model,
                "default", "answer.api");
            settings = settings with { ApiPresets = [preset], ActiveApiPresetId = preset.Id };
        }
        var active = Active(settings)!;
        return settings with { ActiveApiPresetId = active.Id };
    }

    /// <summary>Mac: a known provider by URL host, otherwise 默认.</summary>
    public static string PresetNameFor(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            var template = APIPreset.Templates.FirstOrDefault(
                item => Uri.TryCreate(item.Url, UriKind.Absolute, out var candidate)
                        && string.Equals(candidate.Host, uri.Host, StringComparison.OrdinalIgnoreCase));
            if (template.Name is { Length: > 0 }) return template.Name;
        }
        return "默认";
    }

    /// <summary>Writes the edited fields back into the active preset (Mac <c>storeActiveAPIPreset</c>).</summary>
    public static AppSettings StoreActive(AppSettings settings, string? effort = null)
    {
        var active = Active(settings);
        if (active is null) return settings;
        var updated = settings.ApiPresets.Select(preset => preset.Id == active.Id
            ? preset with
            {
                BaseUrl = settings.BaseUrl,
                Model = settings.Model,
                Effort = effort ?? preset.Effort
            }
            : preset).ToList();
        return settings with { ApiPresets = updated };
    }

    /// <summary>Switches to a saved preset and applies its address/model (Mac <c>selectAPIPreset</c>).</summary>
    public static AppSettings Select(AppSettings settings, string id)
    {
        if (settings.ApiPresets.All(preset => preset.Id != id)) return settings;
        settings = StoreActive(settings);
        var preset = settings.ApiPresets.First(item => item.Id == id);
        return settings with
        {
            ActiveApiPresetId = preset.Id,
            BaseUrl = preset.BaseUrl,
            Model = preset.Model
        };
    }

    /// <summary>Adds a preset with a unique name (Mac <c>addAPIPreset</c>) and makes it active.</summary>
    public static (AppSettings Settings, APIPreset Preset) Add(AppSettings settings, string name, string baseUrl)
    {
        settings = StoreActive(settings);
        var unique = UniqueName(settings.ApiPresets, name);
        var preset = new APIPreset(APIPreset.NewId(), unique, baseUrl);
        var next = settings with { ApiPresets = [.. settings.ApiPresets, preset] };
        next = Select(next, preset.Id);
        return (next, preset);
    }

    /// <summary>Mac: "名称", "名称 2", "名称 3", …</summary>
    public static string UniqueName(IEnumerable<APIPreset> presets, string name)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "默认" : name.Trim();
        var existing = presets.Select(preset => preset.Name).ToHashSet(StringComparer.Ordinal);
        if (!existing.Contains(baseName)) return baseName;
        var counter = 2;
        while (existing.Contains($"{baseName} {counter}")) counter++;
        return $"{baseName} {counter}";
    }

    /// <summary>Mac <c>renameAPIPreset</c>: trims and ignores an empty name.</summary>
    public static AppSettings Rename(AppSettings settings, string id, string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) return settings;
        if (settings.ApiPresets.All(preset => preset.Id != id)) return settings;
        return settings with
        {
            ApiPresets = settings.ApiPresets
                .Select(preset => preset.Id == id ? preset with { Name = trimmed } : preset)
                .ToList()
        };
    }

    /// <summary>
    /// Mac <c>deleteAPIPreset</c>: keeps at least one preset; returns the credential account that
    /// must be removed with it. Deleting the active preset activates the first remaining one.
    /// </summary>
    public static (AppSettings Settings, string? RemovedAccount) Delete(AppSettings settings, string id)
    {
        if (settings.ApiPresets.Count <= 1) return (settings, null);
        var removed = settings.ApiPresets.FirstOrDefault(preset => preset.Id == id);
        if (removed is null) return (settings, null);
        var remaining = settings.ApiPresets.Where(preset => preset.Id != id).ToList();
        var next = settings with { ApiPresets = remaining };
        if (settings.ActiveApiPresetId == id)
        {
            // Mac: deleting the active preset activates the first remaining one (no extra store step).
            next = next with
            {
                ActiveApiPresetId = remaining[0].Id,
                BaseUrl = remaining[0].BaseUrl,
                Model = remaining[0].Model
            };
        }
        return (next, removed.KeyAccount);
    }

    /// <summary>The reasoning strength the API preset sends ("default" means the provider default).</summary>
    public static string ActiveEffort(AppSettings settings) => Active(settings)?.Effort ?? "default";

    /// <summary>The effort parameter to send, or null for the provider default.</summary>
    public static string? EffortParameter(AppSettings settings)
    {
        var effort = ActiveEffort(settings);
        return effort == "default" ? null : effort;
    }

    /// <summary>
    /// GET {base}/models next to the chat-completions endpoint (Mac <c>fetchAPIModels</c>).
    /// Null when the address is not a usable absolute http(s) URL.
    /// </summary>
    public static Uri? ModelListEndpoint(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != "https" && uri.Scheme != "http") return null;
        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            path = path[..^"/chat/completions".Length];
        else if (path.Length == 0)
            path = "/v1";
        return new UriBuilder(uri) { Path = path + "/models", Query = "", Fragment = "" }.Uri;
    }

    /// <summary>Mac status line after fetching a model list.</summary>
    public static string ModelListStatus(int count) => count > 0
        ? $"共 {count} 个模型"
        : "服务没有返回模型列表，请手动填写模型 ID";
}
