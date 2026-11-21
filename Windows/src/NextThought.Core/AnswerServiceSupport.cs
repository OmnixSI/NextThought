namespace NextThought.Core;

/// <summary>Which answer backend is used when the user has not chosen one yet.</summary>
public static class AnswerBackendDefault
{
    public const string CodexAppServer = "codexAppServer";
    public const string CodexExec = "codexExec";
    public const string OpenAICompatible = "openAICompatible";

    /// <summary>A saved choice always wins; otherwise an API key unless Codex is installed.</summary>
    public static string RawValue(string? saved, bool codexInstalled) =>
        saved is CodexAppServer or CodexExec or OpenAICompatible ? saved : codexInstalled ? CodexAppServer : OpenAICompatible;
}

/// <summary>What the app knows about a well-known OpenAI-compatible service.</summary>
public sealed record APIProviderGuide(int Order, string? KeyPageUrl, string SuggestedModel, string? ModelHint, bool IsLocal);

public static class APIProviders
{
    /// <summary>Lower-cased, trimmed, without a trailing slash.</summary>
    public static string Normalized(string baseUrl)
    {
        var value = baseUrl.Trim().ToLowerInvariant();
        while (value.EndsWith('/')) value = value[..^1];
        return value;
    }

    private static readonly Dictionary<string, APIProviderGuide> Table = new(StringComparer.Ordinal)
    {
        ["https://api.deepseek.com/v1"] = new(0, "https://platform.deepseek.com/api_keys", "deepseek-chat", null, false),
        ["https://dashscope.aliyuncs.com/compatible-mode/v1"] = new(1, null, "qwen-plus", null, false),
        ["https://api.moonshot.cn/v1"] = new(2, "https://platform.moonshot.cn/console/api-keys", "", null, false),
        ["https://open.bigmodel.cn/api/paas/v4"] = new(3, "https://open.bigmodel.cn/usercenter/apikeys", "glm-4-flash", null, false),
        ["https://ark.cn-beijing.volces.com/api/v3"] = new(4, null, "", "填方舟控制台里的接入点 ID（ep- 开头）或模型名", false),
        ["https://api.openai.com/v1"] = new(5, "https://platform.openai.com/api-keys", "", null, false),
        ["https://openrouter.ai/api/v1"] = new(6, "https://openrouter.ai/keys", "", null, false),
        ["https://api.groq.com/openai/v1"] = new(7, "https://console.groq.com/keys", "", null, false),
        ["http://localhost:11434/v1"] = new(8, null, "", "填 ollama pull 下载过的模型名", true),
        ["http://localhost:1234/v1"] = new(9, null, "", "填 LM Studio 里已加载的模型名", true)
    };

    public static APIProviderGuide? Guide(string baseUrl) => Table.GetValueOrDefault(Normalized(baseUrl));

    /// <summary>Sort key for the picker; unknown addresses go last.</summary>
    public static int OrderFor(string baseUrl) => Guide(baseUrl)?.Order ?? int.MaxValue;

    /// <summary>True for an address on this machine, where no key is needed.</summary>
    public static bool IsLocal(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)) return false;
        var host = uri.Host.ToLowerInvariant();
        return host is "localhost" or "127.0.0.1" or "::1" or "[::1]";
    }
}

/// <summary>What picking a provider in the picker does to the saved presets.</summary>
public enum APIProviderSwitch
{
    /// <summary>A preset for this address already exists: use it (it keeps its key and model).</summary>
    Select,

    /// <summary>The active preset is still empty: turn it into this provider instead of leaving it unused.</summary>
    ReuseActive,

    /// <summary>Keep the active preset and create another one.</summary>
    AddNew
}

public sealed record APIProviderPresetRef(Guid Id, string BaseUrl);

public static class APIProviderSwitchPlanner
{
    public static APIProviderSwitch Plan(string target, IReadOnlyList<APIProviderPresetRef> presets, Guid? activeId, bool activeIsPristine)
    {
        var wanted = APIProviders.Normalized(target);
        var active = presets.FirstOrDefault(preset => preset.Id == activeId);
        if (active is not null && APIProviders.Normalized(active.BaseUrl) == wanted) return APIProviderSwitch.Select;
        var existing = presets.FirstOrDefault(preset => APIProviders.Normalized(preset.BaseUrl) == wanted);
        if (existing is not null) return APIProviderSwitch.Select;
        return activeIsPristine ? APIProviderSwitch.ReuseActive : APIProviderSwitch.AddNew;
    }
}

public static class APIKeyInput
{
    /// <summary>A pasted key without surrounding whitespace, or null when it cannot be a key.</summary>
    public static string? Clean(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length is < 8 or > 400) return null;
        if (trimmed.Any(char.IsWhiteSpace)) return null;
        if (!trimmed.All(character => character < 128)) return null;
        return trimmed;
    }

    /// <summary>"sk-••••••••3f9a" for display; a short value is fully hidden.</summary>
    public static string Masked(string key)
    {
        var trimmed = key.Trim();
        if (trimmed.Length <= 8) return new string('•', 8);
        return trimmed[..3] + new string('•', 8) + trimmed[^4..];
    }
}

public enum APIFailure
{
    BadKey,
    Forbidden,
    BadAddress,
    CannotReachHost,
    BadModel,
    OutOfCredit,
    RateLimited,
    ServerBusy,
    Timeout,
    Offline,
    SecureConnection,
    Rejected,
    Unknown
}

/// <summary>Turns an HTTP status, a response body or a URL error into a cause and a plain sentence.</summary>
public static class APIFailureExplainer
{
    private static readonly string[] KeyWords =
        ["api key", "apikey", "api_key", "incorrect key", "invalid key", "authentication", "unauthorized", "密钥", "api-key"];
    private static readonly string[] ModelProblemWords =
        ["not found", "not_found", "does not exist", "not exist", "invalid", "unknown", "not support", "unsupported", "no access", "不存在", "无效", "不支持", "无权"];
    private static readonly string[] CreditWords =
        ["insufficient", "quota", "balance", "credit", "billing", "余额", "额度", "欠费", "充值"];

    public static APIFailure Classify(int? status, string body = "", int? urlErrorCode = null)
    {
        if (urlErrorCode is { } code)
        {
            return code switch
            {
                -1001 => APIFailure.Timeout,
                -1003 or -1006 or -1004 => APIFailure.CannotReachHost,
                -1005 or -1009 => APIFailure.Offline,
                >= -1206 and <= -1200 => APIFailure.SecureConnection,
                _ => APIFailure.Unknown
            };
        }
        if (status is not { } http) return APIFailure.Unknown;
        var lower = body.ToLowerInvariant();
        var mentionsModel = lower.Contains("model") || body.Contains("模型");
        var modelProblem = mentionsModel && ModelProblemWords.Any(word => lower.Contains(word) || body.Contains(word));
        var creditProblem = CreditWords.Any(word => lower.Contains(word) || body.Contains(word));
        switch (http)
        {
            case 401: return APIFailure.BadKey;
            case 403: return APIFailure.Forbidden;
            case 402: return APIFailure.OutOfCredit;
            case 404: return modelProblem ? APIFailure.BadModel : APIFailure.BadAddress;
            case 400 or 422:
                var keyProblem = KeyWords.Any(word => lower.Contains(word) || body.Contains(word));
                var invalid = lower.Contains("invalid") || lower.Contains("incorrect") || lower.Contains("not valid") || body.Contains("无效");
                return keyProblem && invalid ? APIFailure.BadKey : modelProblem ? APIFailure.BadModel : APIFailure.Rejected;
            case 429: return creditProblem ? APIFailure.OutOfCredit : APIFailure.RateLimited;
            case >= 500 and <= 599: return APIFailure.ServerBusy;
            default: return APIFailure.Rejected;
        }
    }

    /// <summary>The sentence shown to the user; `Rejected` adds the HTTP code in the app.</summary>
    public static string Message(APIFailure failure) => failure switch
    {
        APIFailure.BadKey => "Key 不对。检查是否复制完整，或到服务商后台重新创建一个。",
        APIFailure.Forbidden => "这个 Key 没有权限，或所在地区暂不支持该服务。",
        APIFailure.BadAddress => "地址不对。检查接口地址是否写对，通常以 /v1 之类的版本号结尾。",
        APIFailure.CannotReachHost => "连不上这个地址。检查地址是否写对；本机服务（Ollama、LM Studio）请先启动。",
        APIFailure.BadModel => "模型名不对，或这个 Key 没有该模型的权限。点“获取列表”重新选一个。",
        APIFailure.OutOfCredit => "余额或额度用完了。到服务商后台充值，或换一个 Key。",
        APIFailure.RateLimited => "请求太频繁，服务商暂时限流。Key 本身没问题，稍后再试。",
        APIFailure.ServerBusy => "服务商那边暂时出错。稍后再试。",
        APIFailure.Timeout => "等了太久没有回应。检查网络或代理，或换一个更快的服务商。",
        APIFailure.Offline => "网络不通。检查这台机器的网络连接或代理。",
        APIFailure.SecureConnection => "安全连接失败。检查系统时间，或代理和公司网络是否拦截了 HTTPS。",
        APIFailure.Rejected => "服务商拒绝了这次请求。",
        _ => "检查没有通过。"
    };

    /// <summary>Why fetching the model list failed; many services have no list at all.</summary>
    public static string ModelListMessage(int status) => status switch
    {
        401 or 403 => Message(APIFailure.BadKey),
        404 or 405 => "这个服务没有提供模型列表。请直接填写模型名。",
        429 => Message(APIFailure.RateLimited),
        >= 500 and <= 599 => Message(APIFailure.ServerBusy),
        _ => "获取模型列表失败。可以直接填写模型名。"
    };
}

/// <summary>Which items of a scripted replay are visible `elapsed` seconds after it starts.</summary>
public static class ScriptTimeline
{
    /// <summary>How many items (sorted by time) have appeared.</summary>
    public static int VisibleCount(IReadOnlyList<double> times, double elapsed) =>
        times.TakeWhile(time => time <= elapsed).Count();

    /// <summary>0...1 progress through a replay of <paramref name="duration"/> seconds.</summary>
    public static double Progress(double elapsed, double duration) =>
        duration > 0 ? Math.Clamp(elapsed / duration, 0, 1) : 1;
}
