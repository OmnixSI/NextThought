import Foundation

public enum CodexOutputEvent: Sendable, Equatable {
    case message(String)
    case failure(String)
}

public enum CodexOutputParser {
    public static func parse(_ line: String) -> CodexOutputEvent? {
        guard let data = line.data(using: .utf8),
              let event = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return nil }

        let type = event["type"] as? String ?? ""
        if type == "error" || type == "turn.failed" {
            return .failure(errorMessage(from: event))
        }
        guard type == "item.completed",
              let item = event["item"] as? [String: Any],
              item["type"] as? String == "agent_message",
              let text = item["text"] as? String,
              !text.isEmpty else { return nil }
        return .message(text)
    }

    private static func errorMessage(from event: [String: Any]) -> String {
        if let message = event["message"] as? String { return message }
        if let error = event["error"] as? String { return error }
        if let error = event["error"] as? [String: Any], let message = error["message"] as? String { return message }
        return "Codex CLI 报告了未知错误。"
    }
}
