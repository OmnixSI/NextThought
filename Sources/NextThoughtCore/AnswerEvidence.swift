import Foundation

public struct SearchDecision: Codable, Sendable, Equatable {
    public var search: Bool
    public var reason: String
    public var query: String
    public var mechanism: String
    public init(search: Bool, reason: String, query: String, mechanism: String) {
        self.search = search; self.reason = reason; self.query = query; self.mechanism = mechanism
    }
    public static func parse(_ output: String, question: String) -> SearchDecision? {
        let cleaned = output.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let start = cleaned.firstIndex(of: "{"), let end = cleaned.lastIndex(of: "}"), start <= end,
              let data = String(cleaned[start...end]).data(using: .utf8),
              let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let search = json["needsSearch"] as? Bool, let reason = json["reason"] as? String else { return nil }
        let query = (json["query"] as? String ?? question).trimmingCharacters(in: .whitespacesAndNewlines)
        return .init(search: search, reason: String(reason.prefix(200)), query: String((query.isEmpty ? question : query).prefix(500)), mechanism: "model")
    }
}
public struct EvidenceBundle: Codable, Sendable, Equatable {
    public var requestID: UUID
    public var materials: [ContextExcerpt] = []
    public var screen: ScreenContext?
    public var screenImageAttached = false
    public var retrieval = "关键词查找"
    public var search: SearchDecision?
    public var webSources: [WebResult] = []
    public var notes: [String] = []
    public init(requestID: UUID) { self.requestID = requestID }
}
public struct AnswerState: Codable, Sendable, Equatable {
    public enum Phase: String, Codable, Sendable { case waiting, generating, complete, cancelled, failed }
    public var id: UUID
    public var question: String
    public var text: String
    public var phase: Phase
    public var revision: Int
    public var error: String?
    public var evidence: EvidenceBundle
    public init(id: UUID, question: String) {
        self.id = id; self.question = question; text = ""; phase = .waiting; revision = 0
        evidence = .init(requestID: id)
    }
}

public enum AnswerGrounding {
    public static func pureRewrite(_ question: String) -> Bool {
        ["把上一条回答压缩", "Rewrite the previous answer", "把上一条回答改写为"].contains { question.hasPrefix($0) }
    }
    public static func needsVerification(_ answer: String) -> Bool {
        ["可能不是最新", "可能已过时", "需要联网核实", "需要实时核查", "无法确认最新", "may be outdated", "need to verify the current"].contains { answer.lowercased().contains($0.lowercased()) }
    }
    public static func explicitSearch(_ question: String) -> Bool {
        ["联网查", "上网查", "搜索一下", "查一下最新", "search the web", "look it up", "browse the web", "verify online"].contains { question.lowercased().contains($0) }
    }
}
