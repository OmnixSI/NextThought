using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NextThought.Core;

public enum CodexInboundKind
{
    Response,
    Notification,
    ServerRequest
}

public sealed record CodexInbound(
    CodexInboundKind Kind,
    int? ResponseId = null,
    JsonObject? Result = null,
    string? Error = null,
    string? Method = null,
    JsonNode? ServerRequestId = null,
    JsonObject? Params = null);

public abstract record CodexTurnEvent;
public sealed record CodexDelta(string ItemId, string Text) : CodexTurnEvent;
public sealed record CodexAgentMessageCompleted(string ItemId, string Text, string? Phase) : CodexTurnEvent;
public sealed record CodexWebSearch(string ItemId, bool Completed, int QueryCount) : CodexTurnEvent;
public sealed record CodexRetrying(string Message) : CodexTurnEvent;
public sealed record CodexCompleted(string Status, string? Error) : CodexTurnEvent;

/// <summary>
/// Wire helpers for the Codex app-server (v2) protocol (port of CodexAppServerProtocol):
/// newline-delimited JSON, JSON-RPC-like messages without the "jsonrpc" field.
/// </summary>
public static class CodexAppServerProtocol
{
    public const string ClientName = "interview_hacker";

    public static byte[] Request(int id, string method, JsonObject parameters) =>
        Line(new JsonObject { ["id"] = id, ["method"] = method, ["params"] = parameters });

    public static byte[] Notification(string method, JsonObject? parameters = null)
    {
        var message = new JsonObject { ["method"] = method };
        if (parameters is not null) message["params"] = parameters;
        return Line(message);
    }

    /// <summary>Answer a server-initiated request we do not support; approvals get an explicit decline.</summary>
    public static byte[] Reply(JsonNode? id, string method) => method switch
    {
        "item/commandExecution/requestApproval" or "item/fileChange/requestApproval" =>
            Line(new JsonObject { ["id"] = id?.DeepClone(), ["result"] = new JsonObject { ["decision"] = "decline" } }),
        "execCommandApproval" or "applyPatchApproval" =>
            Line(new JsonObject { ["id"] = id?.DeepClone(), ["result"] = new JsonObject { ["decision"] = "denied" } }),
        "mcpServer/elicitation/request" =>
            Line(new JsonObject { ["id"] = id?.DeepClone(), ["result"] = new JsonObject { ["action"] = "decline" } }),
        "item/tool/requestUserInput" =>
            Line(new JsonObject { ["id"] = id?.DeepClone(), ["result"] = new JsonObject { ["answers"] = new JsonObject() } }),
        _ => Line(new JsonObject
        {
            ["id"] = id?.DeepClone(),
            ["error"] = new JsonObject { ["code"] = -32601, ["message"] = "unsupported by NextThought" }
        })
    };

    public static JsonObject InitializeParams(string version) =>
        new() { ["clientInfo"] = new JsonObject { ["name"] = ClientName, ["title"] = "NextThought", ["version"] = version } };

    /// <summary>"fast" turns on Codex Fast mode; null asks for standard speed.</summary>
    public static JsonNode? ServiceTier(bool fast) => fast ? "fast" : null;

    public static JsonObject ThreadStartParams(string model, string effort, string cwd, bool fast = false, bool webSearch = false) => new()
    {
        ["model"] = model,
        ["serviceTier"] = ServiceTier(fast),
        ["cwd"] = cwd,
        ["approvalPolicy"] = "never",
        ["sandbox"] = "read-only",
        ["ephemeral"] = true,
        ["config"] = new JsonObject
        {
            ["model_reasoning_effort"] = effort,
            ["web_search"] = webSearch ? "live" : "disabled",
            ["features.shell_tool"] = false,
            ["features.apps"] = false,
            ["features.multi_agent"] = false
        }
    };

    public static JsonObject TurnStartParams(string threadId, string text, string? imagePath, string effort, bool fast = false)
    {
        var input = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = text } };
        if (imagePath is not null) input.Add(new JsonObject { ["type"] = "localImage", ["path"] = imagePath });
        return new JsonObject
        {
            ["threadId"] = threadId,
            ["input"] = input,
            ["effort"] = effort,
            ["summary"] = "none",
            ["serviceTier"] = ServiceTier(fast)
        };
    }

    private static byte[] Line(JsonObject message)
    {
        var bytes = Encoding.UTF8.GetBytes(message.ToJsonString(new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }) + "\n");
        return bytes;
    }

    public static CodexInbound? Parse(string line)
    {
        JsonObject? json;
        try
        {
            json = JsonNode.Parse(line) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
        if (json is null) return null;
        var method = Text(json["method"]);
        var idNode = json["id"];
        if (method is not null && idNode is not null)
            return new CodexInbound(CodexInboundKind.ServerRequest, Method: method, ServerRequestId: idNode.DeepClone(), Params: json["params"] as JsonObject ?? []);
        if (method is not null)
            return new CodexInbound(CodexInboundKind.Notification, Method: method, Params: json["params"] as JsonObject ?? []);
        var responseId = json["id"] is JsonValue value && value.TryGetValue<int>(out var number)
            ? number
            : int.TryParse(Text(json["id"]), out var parsed) ? parsed : (int?)null;
        if (responseId is null) return null;
        if (json["error"] is JsonObject error)
            return new CodexInbound(CodexInboundKind.Response, responseId, Error: Text(error["message"]) ?? "Codex app-server error");
        return new CodexInbound(CodexInboundKind.Response, responseId, Result: json["result"] as JsonObject ?? []);
    }

    public static string? ThreadIdFromThreadStart(JsonObject result) =>
        Text((result["thread"] as JsonObject)?["id"]) ?? Text(result["threadId"]);

    public static string? TurnIdFromTurnStart(JsonObject result) =>
        Text((result["turn"] as JsonObject)?["id"]) ?? Text(result["turnId"]);

    /// <summary>account/read: logged in when an account is present or auth is not required.</summary>
    public static bool IsLoggedIn(JsonObject accountRead)
    {
        if (accountRead["account"] is JsonObject account && account.Count > 0) return true;
        return accountRead["requiresOpenaiAuth"] is JsonValue value && value.TryGetValue<bool>(out var required) && !required;
    }

    /// <summary>Maps a notification to a turn event; notifications for other turns are dropped.</summary>
    public static CodexTurnEvent? TurnEvent(string method, JsonObject parameters, string? turnId)
    {
        var incoming = Text(parameters["turnId"]);
        if (incoming is not null && turnId is not null && incoming != turnId) return null;
        switch (method)
        {
            case "item/agentMessage/delta":
                var delta = Text(parameters["delta"]);
                return delta is null ? null : new CodexDelta(Text(parameters["itemId"]) ?? "", delta);
            case "item/started" or "item/completed":
                if (parameters["item"] is not JsonObject item) return null;
                if (Text(item["type"]) == "webSearch")
                {
                    var action = item["action"] as JsonObject ?? [];
                    var count = (action["queries"] as JsonArray)?.Count ?? (action["query"] is JsonValue ? 1 : 0);
                    return new CodexWebSearch(Text(item["id"]) ?? "", method == "item/completed", count);
                }
                if (method != "item/completed" || Text(item["type"]) != "agentMessage" || Text(item["text"]) is not { } text) return null;
                return new CodexAgentMessageCompleted(Text(item["id"]) ?? "", text, Text(item["phase"]));
            case "turn/completed":
                var turn = parameters["turn"] as JsonObject ?? [];
                var incomingTurn = Text(turn["id"]);
                if (incomingTurn is not null && turnId is not null && incomingTurn != turnId) return null;
                var error = Text((turn["error"] as JsonObject)?["message"]);
                return new CodexCompleted(Text(turn["status"]) ?? "completed", error);
            case "error":
                var errorObject = parameters["error"] as JsonObject;
                var message = Text(errorObject?["message"]) ?? "Codex 报告错误";
                if (parameters["willRetry"] is JsonValue retry && retry.TryGetValue<bool>(out var willRetry) && willRetry)
                    return new CodexRetrying(message);
                return new CodexCompleted("failed", message);
            default:
                return null;
        }
    }

    internal static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}

/// <summary>Accumulates streamed agent-message text; several items may appear in one turn.</summary>
public sealed class AgentMessageAccumulator
{
    private readonly List<string> order = [];
    private readonly Dictionary<string, string> texts = new(StringComparer.Ordinal);
    private string? finalItem;

    public void Apply(CodexTurnEvent turnEvent)
    {
        switch (turnEvent)
        {
            case CodexDelta delta:
                if (!texts.ContainsKey(delta.ItemId)) order.Add(delta.ItemId);
                texts[delta.ItemId] = texts.GetValueOrDefault(delta.ItemId) + delta.Text;
                break;
            case CodexAgentMessageCompleted completed:
                if (!texts.ContainsKey(completed.ItemId)) order.Add(completed.ItemId);
                texts[completed.ItemId] = completed.Text;
                if (completed.Phase == "final_answer") finalItem = completed.ItemId;
                break;
        }
    }

    public string Text => finalItem is not null && texts.TryGetValue(finalItem, out var final) ? final
        : order.Count > 0 ? texts.GetValueOrDefault(order[^1]) ?? "" : "";
}

public abstract record ChatStreamEvent;
public sealed record ChatDelta(string Text) : ChatStreamEvent;
public sealed record ChatDone : ChatStreamEvent;
public sealed record ChatFailure(string Message) : ChatStreamEvent;

/// <summary>
/// Server-sent events parser for OpenAI-compatible chat/completions streams
/// (port of ChatCompletionStreamParser). Feed one line at a time.
/// </summary>
public sealed class ChatCompletionStreamParser
{
    public ChatStreamEvent? Parse(string raw)
    {
        var line = raw.Trim();
        if (!line.StartsWith("data:", StringComparison.Ordinal))
            return line.StartsWith('{') && ErrorMessage(line) is { } plain ? new ChatFailure(plain) : null;
        var payload = line[5..].TrimStart();
        if (payload == "[DONE]") return new ChatDone();
        JsonObject? json;
        try
        {
            json = JsonNode.Parse(payload) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
        if (json is null) return null;
        if (ErrorMessage(json) is { } message) return new ChatFailure(message);
        var choice = (json["choices"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault();
        if (choice is null) return null;
        var content = CodexAppServerProtocol.Text((choice["delta"] as JsonObject)?["content"])
                      ?? CodexAppServerProtocol.Text((choice["message"] as JsonObject)?["content"]);
        return content is { Length: > 0 } ? new ChatDelta(content) : null;
    }

    internal static string? ErrorMessage(string line)
    {
        try
        {
            return JsonNode.Parse(line) is JsonObject json ? ErrorMessage(json) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string? ErrorMessage(JsonObject json) => json["error"] switch
    {
        JsonObject error => CodexAppServerProtocol.Text(error["message"]) ?? "模型服务返回错误",
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        _ => null
    };

    /// <summary>Request body; the image goes inline as a data URL when present.</summary>
    public static byte[] Body(string model, string system, string user, string? imageJpegBase64, double temperature = 0.3, string? reasoningEffort = null)
    {
        JsonNode content = user;
        if (imageJpegBase64 is not null)
            content = new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = user },
                new JsonObject
                {
                    ["type"] = "image_url",
                    ["image_url"] = new JsonObject { ["url"] = "data:image/jpeg;base64," + imageJpegBase64 }
                }
            };
        var body = new JsonObject
        {
            ["model"] = model,
            ["stream"] = true,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject { ["role"] = "user", ["content"] = content }
            }
        };
        if (!string.IsNullOrEmpty(reasoningEffort)) body["reasoning_effort"] = reasoningEffort;
        else body["temperature"] = temperature;
        return Encoding.UTF8.GetBytes(body.ToJsonString(new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }

    /// <summary>Accepts "https://host/v1", "https://host/v1/" or a full ".../chat/completions" URL.</summary>
    public static string? Endpoint(string baseUrl)
    {
        var text = baseUrl.Trim();
        if (text.Length == 0) return null;
        if (text.EndsWith('/')) text = text[..^1];
        if (!text.EndsWith("/chat/completions", StringComparison.Ordinal)) text += "/chat/completions";
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return null;
        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host)) return null;
        // Plain HTTP only for local servers; never send keys in clear text.
        if (scheme == "http" && uri.Host.ToLowerInvariant() is not ("localhost" or "127.0.0.1" or "::1")) return null;
        return uri.ToString();
    }
}

/// <summary>GET /models of OpenAI-compatible services: {"data":[{"id":…}]} or {"models":[{"name"|"id":…}]}.</summary>
public static class ModelListParser
{
    public static IReadOnlyList<string> Ids(string json)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return [];
        }
        if (root is null) return [];
        var items = (root["data"] as JsonArray) ?? (root["models"] as JsonArray) ?? [];
        return items.OfType<JsonObject>()
            .Select(item => CodexAppServerProtocol.Text(item["id"]) ?? CodexAppServerProtocol.Text(item["name"]))
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList()!;
    }
}
