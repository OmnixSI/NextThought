import Foundation

/// Physical provenance is distinct from a position in a Markdown section array.
public struct DocumentLocator: Codable, Sendable, Equatable {
    public var page: Int?
    public var slide: Int?
    public var sheet: String?
    public var section: Int?
    public var headingPath: [String]

    public init(page: Int? = nil, slide: Int? = nil, sheet: String? = nil,
                section: Int? = nil, headingPath: [String] = []) {
        self.page = page; self.slide = slide; self.sheet = sheet
        self.section = section; self.headingPath = headingPath
    }

    public var label: String {
        var parts: [String] = []
        if let page { parts.append("p.\(page)") }
        if let slide { parts.append("幻灯片 \(slide)") }
        if let sheet { parts.append("工作表 \(sheet)") }
        if !headingPath.isEmpty { parts.append(headingPath.joined(separator: " > ")) }
        if parts.isEmpty, let section { parts.append("第 \(section) 段") }
        return parts.joined(separator: " · ")
    }
}

public struct DocumentBlock: Codable, Sendable, Equatable, Identifiable {
    public var id: String
    public var kind: String
    public var markdown: String
    public var locator: DocumentLocator

    public init(id: String = UUID().uuidString, kind: String = "paragraph", markdown: String,
                locator: DocumentLocator = .init()) {
        self.id = id; self.kind = kind; self.markdown = markdown; self.locator = locator
    }
}

public struct DocumentContent: Codable, Sendable, Equatable {
    public var schemaVersion = 1
    public var pipeline: String
    public var sourceHash: String?
    public var blocks: [DocumentBlock]
    public var warnings: [String]
    public var status: String
    public var components: [String: String]

    public init(pipeline: String, sourceHash: String? = nil, blocks: [DocumentBlock],
                warnings: [String] = [], components: [String: String] = [:]) {
        self.pipeline = pipeline; self.sourceHash = sourceHash; self.blocks = blocks
        self.warnings = warnings; self.status = warnings.isEmpty ? "complete" : "degraded"
        self.components = components
    }
    public var markdown: String { blocks.map(\.markdown).joined(separator: "\n\n") }
}

/// A small block parser; tables, fenced code and display math are atomic.
public enum DocumentStructure {
    public static func parse(_ markdown: String, locator: DocumentLocator = .init()) -> [DocumentBlock] {
        let lines = markdown.components(separatedBy: "\n")
        var result: [DocumentBlock] = []
        var headingPath = locator.headingPath
        var position = 0
        while position < lines.count {
            let line = lines[position]
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            if trimmed.isEmpty { position += 1; continue }
            var kind = "paragraph"
            var body: [String] = [line]
            if let heading = heading(trimmed) {
                headingPath = Array(headingPath.prefix(max(0, heading.level - 1)))
                headingPath.append(heading.title)
                kind = "heading"
                position += 1
            } else if trimmed.hasPrefix("```") || trimmed.hasPrefix("~~~") {
                kind = "code"
                let marker = String(trimmed.prefix(3)); position += 1
                while position < lines.count {
                    body.append(lines[position]); position += 1
                    if body.last!.trimmingCharacters(in: .whitespaces).hasPrefix(marker) { break }
                }
            } else if trimmed.hasPrefix("$$") || trimmed.hasPrefix("\\[") {
                kind = "formula"; position += 1
                let end = trimmed.hasPrefix("$$") ? "$$" : "\\]"
                if !(trimmed.count > end.count && trimmed.hasSuffix(end)) {
                    while position < lines.count {
                        body.append(lines[position]); position += 1
                        if body.last!.trimmingCharacters(in: .whitespaces).hasSuffix(end) { break }
                    }
                }
            } else if trimmed.hasPrefix("<table") {
                kind = "table"; position += 1
                while !body.joined().contains("</table>"), position < lines.count {
                    body.append(lines[position]); position += 1
                }
            } else if trimmed.contains("|"), position + 1 < lines.count,
                      isTableSeparator(lines[position + 1]) {
                kind = "table"; position += 1
                while position < lines.count, lines[position].contains("|") {
                    body.append(lines[position]); position += 1
                }
            } else {
                position += 1
                while position < lines.count {
                    let next = lines[position].trimmingCharacters(in: .whitespaces)
                    if next.isEmpty || heading(next) != nil || next.hasPrefix("```") || next.hasPrefix("~~~")
                        || next.hasPrefix("$$") || next.hasPrefix("\\[") || next.hasPrefix("<table") { break }
                    if next.contains("|"), position + 1 < lines.count, isTableSeparator(lines[position + 1]) { break }
                    body.append(lines[position]); position += 1
                }
            }
            var location = locator; location.headingPath = headingPath
            let text = body.joined(separator: "\n")
            let key = ContextFingerprint.hash(location.label + "\n" + kind + "\n" + text)
            let occurrence = result.filter { $0.id.hasPrefix(key + "-") }.count
            result.append(.init(id: key + "-\(occurrence)", kind: kind, markdown: text, locator: location))
        }
        return result
    }

    public static func heading(_ line: String) -> (level: Int, title: String)? {
        let count = line.prefix(while: { $0 == "#" }).count
        guard (1...6).contains(count), line.dropFirst(count).first == " " else { return nil }
        return (count, String(line.dropFirst(count + 1)).trimmingCharacters(in: .whitespaces))
    }

    static func isTableSeparator(_ line: String) -> Bool {
        let text = line.trimmingCharacters(in: .whitespaces)
        return text.contains("-") && text.contains("|") && text.allSatisfy { "|:- \t".contains($0) }
    }

    /// Preserve the canonical table; retrieval-only row groups repeat the header.
    /// Group complete HTML rows, keeping every rowspan within its group. The canonical block retains the full table.
    static func htmlTablePieces(_ text: String, target: Int) -> [String] {
        guard let regex = try? NSRegularExpression(pattern: "<tr(?:\\s[^>]*)?>[\\s\\S]*?</tr>", options: .caseInsensitive) else { return [text] }
        let ns = text as NSString
        let rows = regex.matches(in: text, range: NSRange(location: 0, length: ns.length)).map { ns.substring(with: $0.range) }
        guard rows.count > 2 else { return [text] }
        func span(_ row: String) -> Int {
            guard let r = try? NSRegularExpression(pattern: "rowspan=[\"']?(\\d+)", options: .caseInsensitive) else { return 1 }
            let n = row as NSString
            return r.matches(in: row, range: NSRange(location: 0, length: n.length)).compactMap { Int(n.substring(with: $0.range(at: 1))) }.max() ?? 1
        }
        var headerCount = 1
        while headerCount < rows.count, rows[headerCount].lowercased().contains("<th"), !rows[headerCount].lowercased().contains("<td") { headerCount += 1 }
        var headerEnd = 0
        for i in 0..<headerCount { headerEnd = max(headerEnd, i + span(rows[i])) }
        headerCount = min(rows.count, max(headerCount, headerEnd))
        let header = Array(rows.prefix(headerCount))
        var output: [String] = [], group: [String] = [], position = headerCount
        while position < rows.count {
            var end = min(rows.count, position + span(rows[position]))
            var cursor = position
            while cursor < end { end = min(rows.count, max(end, cursor + span(rows[cursor]))); cursor += 1 }
            let connected = Array(rows[position..<end])
            if !group.isEmpty, (header + group + connected).joined().count + 20 > target {
                output.append("<table>\n" + (header + group).joined(separator: "\n") + "\n</table>"); group = []
            }
            group += connected; position = end
        }
        if !group.isEmpty { output.append("<table>\n" + (header + group).joined(separator: "\n") + "\n</table>") }
        return output.isEmpty ? [text] : output
    }

    public static func pieces(_ block: DocumentBlock, target: Int = 900) -> [String] {
        let text = block.markdown
        if text.count <= target || ["formula", "code"].contains(block.kind) { return [text] }
        if block.kind == "table" {
            let rows = text.components(separatedBy: "\n")
            if text.trimmingCharacters(in: .whitespacesAndNewlines).hasPrefix("<table") {
                return htmlTablePieces(text, target: target)
            }
            guard rows.count > 2, isTableSeparator(rows[1]) else { return [text] }
            let header = rows.prefix(2).joined(separator: "\n")
            var group = header; var pieces: [String] = []
            for row in rows.dropFirst(2) {
                if group != header, group.count + row.count + 1 > target {
                    pieces.append(group); group = header
                }
                group += "\n" + row
            }
            if group != header { pieces.append(group) }
            return pieces.isEmpty ? [text] : pieces
        }
        let chars = Array(text); var start = 0; var pieces: [String] = []
        while start < chars.count {
            let end = min(start + target, chars.count)
            pieces.append(String(chars[start..<end]))
            if end == chars.count { break }
            start = max(start + 1, end - 120)
        }
        return pieces
    }
}

public extension ContextExcerpt {
    var citation: String { source + " " + (locator?.label ?? "第 \(page) 段") }

    /// Choose complete chunks once. Downstream prompt builders must not cut them again.
    static func budget(_ excerpts: [ContextExcerpt], characters: Int, count: Int = 8) -> [ContextExcerpt] {
        var left = characters; var result: [ContextExcerpt] = []
        for excerpt in excerpts {
            guard result.count < count else { break }
            if excerpt.text.count <= left { result.append(excerpt); left -= excerpt.text.count }
        }
        return result
    }
}
