import Foundation

// Turns the text layer of PDF pages into text worth indexing.
//
// A PDF stores lines, not paragraphs: the text of a page comes out with a break at the
// end of every printed line, the running header and the page number in the middle of
// the prose, and section titles looking like any other line. Indexed that way, a page
// is one block with no section, every chunk starts with the author's name from the
// header, and a sentence split over two lines matches neither half of a question.
//
// This is text work only (no fonts, no coordinates), so it stays conservative: it joins
// what is clearly one sentence, drops what is clearly page furniture, and marks only
// the headings it can be sure of. Slide-like pages, where every line is its own
// thought, come through unchanged apart from the furniture.
public enum PDFTextLayout {
    /// Lines that repeat across the pages and belong to none of them: running headers
    /// and footers, wherever on the page the text layer puts them. Digits are ignored,
    /// so "Report · 12" and "Report · 13" are the same line.
    public static func furniture(pages: [String]) -> Set<String> {
        guard pages.count >= 4 else { return [] }
        var anywhere: [String: Int] = [:], edges: [String: Int] = [:], repeated = Set<String>()
        for page in pages {
            let lines = page.components(separatedBy: .newlines).map(key).filter(isFurnitureCandidate)
            var onPage: [String: Int] = [:]
            for line in lines { onPage[line, default: 0] += 1 }
            // A header is on a page once (twice with a footer). A line that fills the page
            // is the page's content: rows of a table, a list of like entries.
            repeated.formUnion(onPage.filter { $0.value > 2 }.keys)
            for line in onPage.keys { anywhere[line, default: 0] += 1 }
            for line in Set(lines.prefix(2) + lines.suffix(2)) { edges[line, default: 0] += 1 }
        }
        let common = max(3, Int((Double(pages.count) * 0.5).rounded(.up)))
        let atEdges = max(3, Int((Double(pages.count) * 0.3).rounded(.up)))
        return Set(anywhere.filter { $0.value >= common }.keys).union(edges.filter { $0.value >= atEdges }.keys).subtracting(repeated)
    }

    /// With the digits gone, a row of numbers is the same line on every page ("12.5 45.1" →
    /// ". ."): only a line with words in it can be a header.
    static func isFurnitureCandidate(_ key: String) -> Bool {
        key.count <= 80 && key.filter(\.isLetter).count >= 4
    }

    /// One page as Markdown: furniture and the page number removed, wrapped lines joined
    /// into sentences, numbered and well-known section titles marked as headings.
    ///
    /// `numberedHeadings` is for documents that are prose (see `isProse`): on a page of
    /// slides or of speaker notes a line starting with a number is a step or a wrapped
    /// sentence far more often than a section title.
    public static func markdown(_ page: String, furniture: Set<String> = [], numberedHeadings: Bool = true) -> String {
        var lines = page.components(separatedBy: .newlines).map { $0.trimmingCharacters(in: .whitespaces) }.filter { !$0.isEmpty }
        // Furniture is a line or two. If it would take a third of the page, it is not furniture here.
        let kept = lines.filter { !furniture.contains(key($0)) }
        if kept.count * 3 >= lines.count * 2 { lines = kept }
        // The page number sits alone on the first or the last line.
        if let last = lines.last, isPageNumber(last) { lines.removeLast() }
        if let first = lines.first, isPageNumber(first) { lines.removeFirst() }
        guard !lines.isEmpty else { return "" }

        let widths = lines.map(width).filter { $0 >= 20 }.sorted()
        // The width of a full line of prose; nil on a page of short lines (a slide, a table).
        let full: Int? = widths.count >= 6 ? widths[widths.count * 3 / 4] : nil

        var out: [String] = []
        var paragraph = ""
        func flush() {
            if !paragraph.isEmpty { out.append(paragraph); paragraph = "" }
        }
        for (index, raw) in lines.enumerated() {
            let line = raw.replacingOccurrences(of: #"(\s*\.){4,}\s*"#, with: " … ", options: .regularExpression)
            let next = index + 1 < lines.count ? lines[index + 1] : nil
            if let heading = heading(line, next: next, numbered: numberedHeadings && full.map({ width(line) < $0 * 82 / 100 }) ?? false) {
                flush()
                if !out.isEmpty { out.append("") }
                out.append(String(repeating: "#", count: heading.level) + " " + heading.title)
                out.append("")
                continue
            }
            if paragraph.isEmpty { paragraph = line } else { paragraph = join(paragraph, line) }
            // A page of short lines keeps them as they are; the last line closes what is open.
            guard let full, let next else { flush(); continue }
            let isFull = width(line) >= full * 82 / 100
            let ends = line.last.map { ".。!?！？:：;；".contains($0) } ?? false
            let continues = isFull || (!ends && (next.first?.isLowercase ?? false))
            if continues, self.heading(next, next: nil, numbered: numberedHeadings && width(next) < full * 82 / 100) == nil { continue }
            // A short line that ends a sentence closes the paragraph; anything else is a
            // line of its own (a caption, a list entry) and stays with what follows.
            if ends, paragraph.count >= 200 { flush(); out.append("") } else { flush() }
        }
        flush()
        // No run of blank lines, none at the ends.
        var result: [String] = []
        for line in out where !(line.isEmpty && (result.last?.isEmpty ?? true)) { result.append(line) }
        while result.last?.isEmpty == true { result.removeLast() }
        return result.joined(separator: "\n")
    }

    /// A report or a paper rather than slides: its pages are full of text.
    public static func isProse(pages: [String]) -> Bool {
        guard !pages.isEmpty else { return false }
        return pages.reduce(0) { $0 + $1.count } / pages.count > 1_200
    }

    // MARK: Pieces

    static func key(_ line: String) -> String {
        let kept = line.lowercased().unicodeScalars.filter { !CharacterSet.decimalDigits.contains($0) }
        return String(String.UnicodeScalarView(kept)).split(whereSeparator: \.isWhitespace).joined(separator: " ")
    }

    static func isPageNumber(_ line: String) -> Bool {
        line.count <= 14 && line.range(of: #"^(page\s*|第\s*|[-–—]\s*)?\d{1,4}(\s*(/|of|页|[-–—])\s*\d{0,4})?\s*(页)?$"#, options: [.regularExpression, .caseInsensitive]) != nil
    }

    /// Printed width: a CJK character takes the room of two Latin ones.
    static func width(_ line: String) -> Int {
        line.unicodeScalars.reduce(0) { $0 + ($1.value >= 0x2E80 ? 2 : 1) }
    }

    /// Joins a wrapped line to the text before it: no space inside Chinese, the hyphen
    /// of a word broken at the margin taken out.
    static func join(_ head: String, _ tail: String) -> String {
        guard let last = head.unicodeScalars.last, let first = tail.unicodeScalars.first else { return head + tail }
        if last == "-", head.count >= 2, head.dropLast().last?.isLetter == true, tail.first?.isLowercase == true {
            return String(head.dropLast()) + tail
        }
        return (last.value >= 0x2E80 || first.value >= 0x2E80) ? head + tail : head + " " + tail
    }

    static let named: Set<String> = ["abstract", "introduction", "references", "bibliography", "acknowledgements", "acknowledgments",
                                     "acknowledgement", "conclusion", "conclusions", "related work", "contents", "table of contents",
                                     "摘要", "引言", "绪论", "参考文献", "致谢", "结论", "结语", "附录", "目录"]

    /// The numbering of journal papers: "II. SYSTEM MODEL" (a section, in capitals) and
    /// "A. Array and Channel Model" (a subsection, in title case), plus "APPENDIX B".
    static func journalHeading(_ line: String, next: String?) -> (level: Int, title: String)? {
        if line.range(of: #"^APPENDIX( [A-Z])?$"#, options: .regularExpression) != nil { return (2, line) }
        let letters = line.filter(\.isLetter)
        if line.range(of: #"^(I|II|III|IV|V|VI|VII|VIII|IX|X|XI|XII)\.\s+[A-Z]"#, options: .regularExpression) != nil {
            // Capitals throughout; a title this long may wrap, so the line after is not looked at.
            guard letters.count >= 4, letters.allSatisfy(\.isUppercase), !line.contains(",") || line.count < 70 else { return nil }
            return (2, line)
        }
        guard line.range(of: #"^[A-H]\.\s+[A-Z][a-z]"#, options: .regularExpression) != nil else { return nil }
        // An author in the reference list starts the same way: "G. Marcus, and A. Keller, …".
        let title = line.dropFirst(2).trimmingCharacters(in: .whitespaces)
        guard !title.contains(where: { ".,;“”\"".contains($0) }), title.split(separator: " ").count <= 10,
              let last = title.last, last.isLetter, next?.first?.isLowercase != true else { return nil }
        return (3, line)
    }

    /// "3.2 Research questions" → level 3; "Abstract" → level 2. Nil unless it can only be a title.
    static func heading(_ line: String, next: String?, numbered: Bool = true) -> (level: Int, title: String)? {
        guard line.count <= 90 else { return nil }
        if named.contains(line.lowercased().trimmingCharacters(in: CharacterSet(charactersIn: " :：."))) { return (2, line) }
        guard numbered else { return nil }
        if let journal = journalHeading(line, next: next) { return journal }
        guard let match = line.range(of: #"^\d{1,2}(\.\d{1,2}){0,3}\.?\s+"#, options: .regularExpression) else { return nil }
        let number = line[match].trimmingCharacters(in: CharacterSet(charactersIn: " ."))
        let title = String(line[match.upperBound...])
        let depth = number.split(separator: ".").count
        guard let first = title.unicodeScalars.first, first.value >= 0x2E80 || CharacterSet.uppercaseLetters.contains(first) else { return nil }
        guard depth > 1 || (1...20).contains(Int(number) ?? 99) else { return nil }
        // A sentence, a contents entry, a table row or the first line of a wrapped sentence is not a title.
        guard let last = title.last, last.isLetter || ")）".contains(last) else { return nil }
        if title.contains(where: { "…。，；！？".contains($0) }) || title.split(separator: " ").count > 12 || width(title) > 64 { return nil }
        let letters = title.filter(\.isLetter).count
        guard letters >= 2, Double(letters) / Double(title.filter { !$0.isWhitespace }.count) >= 0.7 else { return nil }
        if let next, next.first?.isLowercase == true { return nil }
        return (min(depth + 1, 4), number + " " + title)
    }
}
