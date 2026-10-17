import Foundation

/// Wire helpers for the Codex `app-server` (v2) protocol: newline-delimited JSON,
/// JSON-RPC-like messages without the "jsonrpc" field.
public enum CodexAppServerProtocol {
    public static let clientName = "interview_hacker"

    public static func request(id: Int, method: String, params: [String: Any]) -> Data {
        line(["id": id, "method": method, "params": params])
    }

    public static func notification(method: String, params: [String: Any]? = nil) -> Data {
        var object: [String: Any] = ["method": method]
        if let params { object["params"] = params }
        return line(object)
    }

    /// Answer a server-initiated request we do not support (approvals, tool calls).
    /// Approval requests get an explicit decline so the turn never hangs.
    public static func reply(toServerRequest id: Any, method: String) -> Data {
        switch method {
        case "item/commandExecution/requestApproval", "item/fileChange/requestApproval":
            return line(["id": id, "result": ["decision": "decline"]])
        case "execCommandApproval", "applyPatchApproval":
            return line(["id": id, "result": ["decision": "denied"]])
        case "mcpServer/elicitation/request":
            return line(["id": id, "result": ["action": "decline"]])
        case "item/tool/requestUserInput":
            return line(["id": id, "result": ["answers": [String: Any]()]])
        default:
            return line(["id": id, "error": ["code": -32601, "message": "unsupported by InterviewHacker"]])
        }
    }

    public static func initializeParams(version: String) -> [String: Any] {
        ["clientInfo": ["name": clientName, "title": "InterviewHacker", "version": version]]
    }

    /// `serviceTier`: "fast" turns on Codex Fast mode (accepted by every app-server
    /// version; newer ones normalise it to "priority"); `null` asks for standard speed,
    /// also on models whose catalog default is Fast.
    public static func serviceTier(fast: Bool) -> Any { fast ? "fast" : NSNull() }

    /// `webSearch`: Codex may use its live web search (background “联网” answers only).
    public static func threadStartParams(model: String, effort: String, cwd: String, fast: Bool = false, webSearch: Bool = false) -> [String: Any] {
        [
            "model": model,
            "serviceTier": serviceTier(fast: fast),
            "cwd": cwd,
            "approvalPolicy": "never",
            "sandbox": "read-only",
            "ephemeral": true,
            "config": [
                "model_reasoning_effort": effort,
                "web_search": webSearch ? "live" : "disabled",
                "features.shell_tool": false,
                "features.apps": false,
                "features.multi_agent": false,
            ] as [String: Any],
        ]
    }

    public static func turnStartParams(threadID: String, text: String, imagePath: String?, effort: String, fast: Bool = false) -> [String: Any] {
        var input: [[String: Any]] = [["type": "text", "text": text]]
        if let imagePath { input.append(["type": "localImage", "path": imagePath]) }
        return ["threadId": threadID, "input": input, "effort": effort, "summary": "none", "serviceTier": serviceTier(fast: fast)]
    }

    private static func line(_ object: [String: Any]) -> Data {
        var data = (try? JSONSerialization.data(withJSONObject: object, options: [.sortedKeys])) ?? Data("{}".utf8)
        data.append(10)
        return data
    }

    public enum Inbound: @unchecked Sendable {
        case response(id: Int, result: [String: Any]?, error: String?)
        case notification(method: String, params: [String: Any])
        case serverRequest(id: Any, method: String, params: [String: Any])
    }

    public static func parse(_ line: String) -> Inbound? {
        guard let data = line.data(using: .utf8),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return nil }
        let method = object["method"] as? String
        if let method, let id = object["id"] {
            return .serverRequest(id: id, method: method, params: object["params"] as? [String: Any] ?? [:])
        }
        if let method { return .notification(method: method, params: object["params"] as? [String: Any] ?? [:]) }
        guard let id = (object["id"] as? Int) ?? (object["id"] as? String).flatMap(Int.init) else { return nil }
        if let error = object["error"] as? [String: Any] {
            return .response(id: id, result: nil, error: error["message"] as? String ?? "Codex app-server error")
        }
        return .response(id: id, result: object["result"] as? [String: Any] ?? [:], error: nil)
    }

    public static func threadID(fromThreadStart result: [String: Any]) -> String? {
        ((result["thread"] as? [String: Any])?["id"] as? String) ?? result["threadId"] as? String
    }

    public static func turnID(fromTurnStart result: [String: Any]) -> String? {
        ((result["turn"] as? [String: Any])?["id"] as? String) ?? result["turnId"] as? String
    }

    /// `account/read`: logged in when an account is present or auth is not required.
    public static func isLoggedIn(accountRead result: [String: Any]) -> Bool {
        if let account = result["account"] as? [String: Any], !account.isEmpty { return true }
        return (result["requiresOpenaiAuth"] as? Bool) == false
    }

    public enum TurnEvent: Equatable, Sendable {
        case delta(itemID: String, text: String)
        case agentMessageCompleted(itemID: String, text: String, phase: String?)
        case webSearch(itemID: String, completed: Bool, queryCount: Int)
        case retrying(String)
        case completed(status: String, error: String?)
    }

    /// Maps a notification to a turn event. Notifications for other turns are dropped
    /// when both ids are known; older servers omit `turnId`.
    public static func turnEvent(method: String, params: [String: Any], turnID: String?) -> TurnEvent? {
        if let incoming = params["turnId"] as? String, let turnID, incoming != turnID { return nil }
        switch method {
        case "item/agentMessage/delta":
            guard let delta = params["delta"] as? String else { return nil }
            return .delta(itemID: params["itemId"] as? String ?? "", text: delta)
        case "item/started", "item/completed":
            guard let item = params["item"] as? [String: Any] else { return nil }
            if item["type"] as? String == "webSearch" {
                let action = item["action"] as? [String: Any] ?? [:]
                let count = (action["queries"] as? [String])?.count ?? (action["query"] is String ? 1 : 0)
                return .webSearch(itemID: item["id"] as? String ?? "", completed: method == "item/completed", queryCount: count)
            }
            guard method == "item/completed", item["type"] as? String == "agentMessage",
                  let text = item["text"] as? String else { return nil }
            return .agentMessageCompleted(itemID: item["id"] as? String ?? "", text: text, phase: item["phase"] as? String)
        case "turn/completed":
            let turn = params["turn"] as? [String: Any] ?? [:]
            if let incoming = turn["id"] as? String, let turnID, incoming != turnID { return nil }
            let error = (turn["error"] as? [String: Any])?["message"] as? String
            return .completed(status: turn["status"] as? String ?? "completed", error: error)
        case "error":
            let error = params["error"] as? [String: Any]
            let message = error?["message"] as? String ?? "Codex 报告错误"
            if params["willRetry"] as? Bool == true { return .retrying(message) }
            return .completed(status: "failed", error: message)
        default:
            return nil
        }
    }
}

/// Accumulates streamed agent-message text. Several agentMessage items may appear
/// in one turn (commentary, then final answer); the last one is shown.
public struct AgentMessageAccumulator: Sendable, Equatable {
    private var order: [String] = []
    private var texts: [String: String] = [:]
    private var finalItem: String?
    public init() {}

    public mutating func apply(_ event: CodexAppServerProtocol.TurnEvent) {
        switch event {
        case let .delta(itemID, text):
            if texts[itemID] == nil { order.append(itemID) }
            texts[itemID, default: ""] += text
        case let .agentMessageCompleted(itemID, text, phase):
            if texts[itemID] == nil { order.append(itemID) }
            texts[itemID] = text
            if phase == "final_answer" { finalItem = itemID }
        default: break
        }
    }

    public var text: String {
        if let finalItem, let text = texts[finalItem] { return text }
        return order.last.flatMap { texts[$0] } ?? ""
    }
}

/// Server-sent events parser for OpenAI-compatible `chat/completions` streams
/// (OpenAI, DeepSeek, Qwen/DashScope compatible mode, Moonshot, Groq, OpenRouter,
/// Ollama, LM Studio). Feed one line at a time.
public struct ChatCompletionStreamParser: Sendable {
    public enum Event: Equatable, Sendable {
        case delta(String)
        case done
        case failure(String)
    }
    public init() {}

    public func parse(line raw: String) -> Event? {
        let line = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard line.hasPrefix("data:") else {
            // Some servers return a plain JSON error body instead of SSE.
            if line.hasPrefix("{"), let message = Self.errorMessage(line) { return .failure(message) }
            return nil
        }
        let payload = line.dropFirst(5).trimmingCharacters(in: .whitespaces)
        if payload == "[DONE]" { return .done }
        guard let data = payload.data(using: .utf8),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return nil }
        if let message = Self.errorMessage(object) { return .failure(message) }
        guard let choice = (object["choices"] as? [[String: Any]])?.first else { return nil }
        if let delta = choice["delta"] as? [String: Any], let content = delta["content"] as? String, !content.isEmpty {
            return .delta(content)
        }
        if let message = choice["message"] as? [String: Any], let content = message["content"] as? String, !content.isEmpty {
            return .delta(content)
        }
        return nil
    }

    static func errorMessage(_ line: String) -> String? {
        guard let data = line.data(using: .utf8), let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return nil }
        return errorMessage(object)
    }

    static func errorMessage(_ object: [String: Any]) -> String? {
        if let error = object["error"] as? [String: Any] { return error["message"] as? String ?? "模型服务返回错误" }
        if let error = object["error"] as? String { return error }
        return nil
    }

    /// Request body. The image is sent inline as a data URL when present.
    /// `reasoningEffort` goes out as `reasoning_effort`; reasoning models reject a custom
    /// temperature, so it is left out when an effort is set.
    public static func body(model: String, system: String, user: String, imageJPEGBase64: String?, temperature: Double = 0.3,
                            reasoningEffort: String? = nil) -> Data {
        var content: Any = user
        if let imageJPEGBase64 {
            content = [["type": "text", "text": user],
                       ["type": "image_url", "image_url": ["url": "data:image/jpeg;base64," + imageJPEGBase64]]]
        }
        var object: [String: Any] = [
            "model": model,
            "stream": true,
            "messages": [["role": "system", "content": system], ["role": "user", "content": content]],
        ]
        if let reasoningEffort, !reasoningEffort.isEmpty { object["reasoning_effort"] = reasoningEffort } else { object["temperature"] = temperature }
        return (try? JSONSerialization.data(withJSONObject: object, options: [.sortedKeys])) ?? Data()
    }

    /// Accepts "https://host/v1", "https://host/v1/" or a full ".../chat/completions" URL.
    public static func endpoint(from base: String) -> URL? {
        var text = base.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { return nil }
        if text.hasSuffix("/") { text.removeLast() }
        if !text.hasSuffix("/chat/completions") { text += "/chat/completions" }
        guard let url = URL(string: text), let scheme = url.scheme?.lowercased(), scheme == "https" || scheme == "http", url.host != nil else { return nil }
        // Plain HTTP only for local servers (Ollama / LM Studio); never send keys in clear text.
        if scheme == "http", !["localhost", "127.0.0.1", "::1"].contains(url.host?.lowercased() ?? "") { return nil }
        return url
    }
}


/// `GET /models` of OpenAI-compatible services: `{"data":[{"id":…}]}` (OpenAI, DeepSeek,
/// OpenRouter…) or `{"models":[{"name"|"id":…}]}` (some local servers).
public enum ModelListParser {
    public static func ids(from data: Data) -> [String] {
        guard let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return [] }
        let items = (root["data"] as? [[String: Any]]) ?? (root["models"] as? [[String: Any]]) ?? []
        var seen = Set<String>()
        return items.compactMap { ($0["id"] as? String) ?? ($0["name"] as? String) }
            .filter { !$0.isEmpty && seen.insert($0).inserted }
            .sorted { $0.localizedStandardCompare($1) == .orderedAscending }
    }
}
