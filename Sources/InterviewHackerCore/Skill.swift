import Foundation

/// A user skill: a Markdown file (SKILL.md style) describing how to answer — tone,
/// method, domain knowledge. Optional front matter gives the name and description:
///
///     ---
///     name: 面试口语化
///     description: 回答像真人说话
///     ---
///     正文…
public struct Skill: Codable, Identifiable, Sendable, Equatable {
    public var id: UUID
    public var name: String
    public var summary: String
    public var content: String
    /// Applies to every scenario.
    public var global: Bool
    public var enabled: Bool
    public var importedAt: Date

    public init(id: UUID = UUID(), name: String, summary: String = "", content: String, global: Bool = false, enabled: Bool = true, importedAt: Date = Date()) {
        self.id = id; self.name = name; self.summary = summary; self.content = content
        self.global = global; self.enabled = enabled; self.importedAt = importedAt
    }

    enum CodingKeys: String, CodingKey { case id, name, summary, content, global, enabled, importedAt }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        id = try c.decode(UUID.self, forKey: .id)
        name = try c.decodeIfPresent(String.self, forKey: .name) ?? "技能"
        summary = try c.decodeIfPresent(String.self, forKey: .summary) ?? ""
        content = try c.decodeIfPresent(String.self, forKey: .content) ?? ""
        global = try c.decodeIfPresent(Bool.self, forKey: .global) ?? false
        enabled = try c.decodeIfPresent(Bool.self, forKey: .enabled) ?? true
        importedAt = try c.decodeIfPresent(Date.self, forKey: .importedAt) ?? Date()
    }

    /// Parses a Markdown file; the name falls back to the first heading, then the file name.
    public static func parse(_ text: String, fileName: String) -> Skill {
        var body = text.replacingOccurrences(of: "\r\n", with: "\n")
        var name = "", summary = ""
        if body.hasPrefix("---\n"), let end = body.range(of: "\n---", range: body.index(body.startIndex, offsetBy: 4)..<body.endIndex) {
            let header = body[body.index(body.startIndex, offsetBy: 4)..<end.lowerBound]
            for line in header.split(separator: "\n") {
                let parts = line.split(separator: ":", maxSplits: 1).map { $0.trimmingCharacters(in: .whitespaces) }
                guard parts.count == 2 else { continue }
                let value = parts[1].trimmingCharacters(in: CharacterSet(charactersIn: "\"'"))
                switch parts[0].lowercased() {
                case "name", "title": name = value
                case "description", "summary": summary = value
                default: break
                }
            }
            body = String(body[end.upperBound...]).trimmingCharacters(in: .newlines)
        }
        if name.isEmpty, let heading = body.split(separator: "\n").first(where: { $0.hasPrefix("# ") }) {
            name = heading.dropFirst(2).trimmingCharacters(in: .whitespaces)
        }
        if name.isEmpty {
            name = (fileName as NSString).deletingPathExtension
            if name.uppercased() == "SKILL" || name.isEmpty { name = "未命名技能" }
        }
        return Skill(name: name, summary: summary, content: body.trimmingCharacters(in: .whitespacesAndNewlines))
    }

    /// Skills that apply to a scenario: enabled, and global or attached to it.
    public static func active(_ library: [Skill], attached: [UUID]) -> [Skill] {
        library.filter { $0.enabled && ($0.global || attached.contains($0.id)) }
    }

    public static let starterTemplate = """
    ---
    name: 新技能
    description: 一句话说明这个技能做什么
    ---
    # 新技能

    在这里写给模型的要求，例如：
    - 回答像我平时说话：先说结论，再讲一个具体例子。
    - 被问到项目时，按“背景 → 我做了什么 → 结果数字”讲。
    - 不知道的就说“这块我了解得不深，我的理解是……”，不要硬编。
    """
}
