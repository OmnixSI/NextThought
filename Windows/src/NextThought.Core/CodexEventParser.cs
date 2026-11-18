using System.Text.Json;

namespace NextThought.Core;

/// <summary>One line of Codex CLI output (port of CodexEventParser.swift).</summary>
public sealed record CodexOutputEvent(string Kind, string Text)
{
    public bool IsMessage => Kind == "message";

    public bool IsFailure => Kind == "failure";
}

public static class CodexOutputParser
{
    public static CodexOutputEvent? Parse(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var type = String(root, "type");
            if (type is "error" or "turn.failed") return new CodexOutputEvent("failure", ErrorMessage(root));
            if (type != "item.completed") return null;
            if (!root.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object) return null;
            if (String(item, "type") != "agent_message") return null;
            var text = String(item, "text");
            return text.Length == 0 ? null : new CodexOutputEvent("message", text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ErrorMessage(JsonElement root)
    {
        var message = String(root, "message");
        if (message.Length > 0) return message;
        var error = String(root, "error");
        if (error.Length > 0) return error;
        if (root.TryGetProperty("error", out var nested) && nested.ValueKind == JsonValueKind.Object)
        {
            var inner = String(nested, "message");
            if (inner.Length > 0) return inner;
        }
        return "Codex CLI 报告了未知错误。";
    }

    private static string String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}
