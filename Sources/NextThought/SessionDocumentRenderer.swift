import AppKit
import CoreGraphics
import CoreText
import Darwin
import Foundation

/// Parses the shared Markdown export into blocks for editable DOCX and readable PDF.
/// AppKit's Markdown importer flattens tables and nested lists into one paragraph,
/// so both document formats preserve the block structure directly.
enum SessionDocumentRenderer {
    private enum Block {
        case paragraph(String, ParagraphKind)
        case listItem(String, level: Int, numID: Int, ordered: Bool)
        case table([[String]])
    }

    private enum ParagraphKind {
        case normal
        case heading(Int)
        case quote
        case code
        case tableHeader
    }

    private struct ListNumbering {
        var numID: Int
        var abstractNumID: Int
    }

    private enum WriterError: LocalizedError {
        case archive(String)

        var errorDescription: String? {
            switch self {
            case let .archive(message): "无法生成 Word 文档：\(message)"
            }
        }
    }

    static func writeDocx(_ markdown: String, to destination: URL) throws {
        let fileManager = FileManager.default
        let temporary = fileManager.temporaryDirectory.appendingPathComponent("nextthought-docx-\(UUID().uuidString)", isDirectory: true)
        let package = temporary.appendingPathComponent("package", isDirectory: true)
        try fileManager.createDirectory(at: package.appendingPathComponent("_rels", isDirectory: true), withIntermediateDirectories: true)
        try fileManager.createDirectory(at: package.appendingPathComponent("word/_rels", isDirectory: true), withIntermediateDirectories: true)
        defer { try? fileManager.removeItem(at: temporary) }

        let parsed = parse(markdown)
        try write("[Content_Types].xml", contentTypesXML, package: package)
        try write("_rels/.rels", packageRelationshipsXML, package: package)
        try write("word/_rels/document.xml.rels", documentRelationshipsXML, package: package)
        try write("word/styles.xml", stylesXML, package: package)
        try write("word/numbering.xml", numberingXML(for: parsed.numbering), package: package)
        try write("word/document.xml", documentXML(for: parsed.blocks), package: package)

        let temporaryArchive = destination.deletingLastPathComponent()
            .appendingPathComponent(".\(destination.lastPathComponent).\(UUID().uuidString).tmp")
        defer { try? fileManager.removeItem(at: temporaryArchive) }
        try archive(package: package, to: temporaryArchive)
        let result = temporaryArchive.path.withCString { source in
            destination.path.withCString { target in Darwin.rename(source, target) }
        }
        guard result == 0 else {
            throw POSIXError(POSIXErrorCode(rawValue: errno) ?? .EIO)
        }
    }

    static func pdfData(for markdown: String) throws -> Data {
        let parsed = parse(markdown)
        let text = pdfAttributedString(blocks: parsed.blocks, numbering: parsed.numbering)
        let output = NSMutableData()
        guard let consumer = CGDataConsumer(data: output as CFMutableData) else {
            throw CocoaError(.fileWriteUnknown)
        }
        var page = CGRect(x: 0, y: 0, width: 595, height: 842)
        guard let context = CGContext(consumer: consumer, mediaBox: &page, nil) else {
            throw CocoaError(.fileWriteUnknown)
        }
        let framesetter = CTFramesetterCreateWithAttributedString(text as CFAttributedString)
        let path = CGPath(rect: CGRect(x: 44, y: 44, width: 507, height: 754), transform: nil)
        var location = 0
        while location < text.length {
            context.beginPDFPage(nil)
            let frame = CTFramesetterCreateFrame(framesetter, CFRange(location: location, length: 0), path, nil)
            let visible = CTFrameGetVisibleStringRange(frame)
            guard visible.length > 0 else {
                context.endPDFPage()
                context.closePDF()
                throw CocoaError(.fileWriteUnknown)
            }
            CTFrameDraw(frame, context)
            context.endPDFPage()
            location += visible.length
        }
        if text.length == 0 {
            context.beginPDFPage(nil)
            context.endPDFPage()
        }
        context.closePDF()
        return output as Data
    }

    private static func write(_ path: String, _ content: String, package: URL) throws {
        try content.write(to: package.appendingPathComponent(path), atomically: true, encoding: .utf8)
    }

    private static func archive(package: URL, to destination: URL) throws {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/zip")
        process.currentDirectoryURL = package
        process.arguments = ["-q", "-X", destination.path,
                             "[Content_Types].xml", "_rels/.rels", "word/document.xml",
                             "word/styles.xml", "word/numbering.xml", "word/_rels/document.xml.rels"]
        let errors = Pipe()
        process.standardOutput = FileHandle.nullDevice
        process.standardError = errors
        try process.run()
        let errorData = errors.fileHandleForReading.readDataToEndOfFile()
        process.waitUntilExit()
        guard process.terminationStatus == 0 else {
            throw WriterError.archive(String(decoding: errorData, as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines))
        }
    }

    private static func parse(_ markdown: String) -> (blocks: [Block], numbering: [ListNumbering]) {
        let lines = markdown.components(separatedBy: .newlines)
        var blocks: [Block] = []
        var numbering: [ListNumbering] = []
        var nextNumID = 1
        var activeListsByLevel: [Int: ListNumbering] = [:]
        var codeLines: [String]?
        var index = 0

        while index < lines.count {
            let line = lines[index]
            if line.trimmingCharacters(in: .whitespaces).hasPrefix("```") {
                if let current = codeLines {
                    blocks.append(.paragraph(current.joined(separator: "\n"), .code))
                    codeLines = nil
                } else {
                    codeLines = []
                }
                activeListsByLevel.removeAll()
                index += 1
                continue
            }
            if codeLines != nil {
                codeLines?.append(line)
                index += 1
                continue
            }

            if let cells = tableCells(in: line), index + 1 < lines.count,
               let separator = tableCells(in: lines[index + 1]), isTableSeparator(separator) {
                activeListsByLevel.removeAll()
                var rows = [cells]
                index += 2
                while index < lines.count, let row = tableCells(in: lines[index]) {
                    rows.append(row)
                    index += 1
                }
                blocks.append(.table(rows))
                continue
            }

            if let list = listItem(in: line) {
                activeListsByLevel = activeListsByLevel.filter { $0.key <= list.level }
                let abstractNumID = list.ordered ? 1 : 0
                if activeListsByLevel[list.level]?.abstractNumID != abstractNumID {
                    let scheme = ListNumbering(numID: nextNumID, abstractNumID: list.ordered ? 1 : 0)
                    nextNumID += 1
                    numbering.append(scheme)
                    activeListsByLevel[list.level] = scheme
                }
                let scheme = activeListsByLevel[list.level]!
                blocks.append(.listItem(list.text, level: list.level, numID: scheme.numID, ordered: list.ordered))
                index += 1
                continue
            }

            if line.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                activeListsByLevel.removeAll()
                index += 1
                continue
            }
            activeListsByLevel.removeAll()

            if let heading = heading(in: line) {
                blocks.append(.paragraph(heading.text, .heading(heading.level)))
            } else if line.hasPrefix("> ") {
                blocks.append(.paragraph(String(line.dropFirst(2)), .quote))
            } else if isHorizontalRule(line) {
                blocks.append(.paragraph("", .normal))
            } else {
                blocks.append(.paragraph(line, .normal))
            }
            index += 1
        }
        if let current = codeLines {
            blocks.append(.paragraph(current.joined(separator: "\n"), .code))
        }
        return (blocks, numbering)
    }

    private static func heading(in line: String) -> (level: Int, text: String)? {
        let prefix = line.prefix(while: { $0 == "#" })
        guard !prefix.isEmpty, prefix.count <= 6,
              line.dropFirst(prefix.count).first?.isWhitespace == true else { return nil }
        let text = line.dropFirst(prefix.count).trimmingCharacters(in: .whitespaces)
        return (prefix.count, text)
    }

    private static func listItem(in line: String) -> (text: String, level: Int, ordered: Bool)? {
        let indentation = line.prefix(while: { $0 == " " || $0 == "\t" })
        let indentSpaces = indentation.reduce(into: 0) { count, char in count += char == "\t" ? 4 : 1 }
        let content = line.dropFirst(indentation.count)
        if let first = content.first, ["-", "*", "+"].contains(first), content.dropFirst().first?.isWhitespace == true {
            return (String(content.dropFirst(2)), min(8, indentSpaces / 2), false)
        }
        guard let range = content.range(of: #"^\d{1,3}[.)、]\s+"#, options: .regularExpression) else { return nil }
        return (String(content[range.upperBound...]), min(8, indentSpaces / 2), true)
    }

    private static func tableCells(in line: String) -> [String]? {
        let trimmed = line.trimmingCharacters(in: .whitespaces)
        guard trimmed.contains("|") else { return nil }
        var source = Array(trimmed)
        if source.first == "|" { source.removeFirst() }
        if source.last == "|" { source.removeLast() }
        var cells: [String] = []
        var current = ""
        var escaped = false
        for char in source {
            if escaped {
                current.append(char)
                escaped = false
            } else if char == "\\" {
                escaped = true
            } else if char == "|" {
                cells.append(current.trimmingCharacters(in: .whitespaces))
                current = ""
            } else {
                current.append(char)
            }
        }
        if escaped { current.append("\\") }
        cells.append(current.trimmingCharacters(in: .whitespaces))
        return cells.count > 1 ? cells : nil
    }

    private static func isTableSeparator(_ cells: [String]) -> Bool {
        !cells.isEmpty && cells.allSatisfy { cell in
            cell.range(of: #"^:?-{3,}:?$"#, options: .regularExpression) != nil
        }
    }

    private static func isHorizontalRule(_ line: String) -> Bool {
        let compact = line.filter { !$0.isWhitespace }
        return compact.count >= 3 && (compact.allSatisfy { $0 == "-" } || compact.allSatisfy { $0 == "*" } || compact.allSatisfy { $0 == "_" })
    }

    private static func documentXML(for blocks: [Block]) -> String {
        let body = blocks.map { block -> String in
            switch block {
            case let .paragraph(text, kind):
                return paragraph(text, properties: paragraphProperties(for: kind), kind: kind)
            case let .listItem(text, level, numID, _):
                let properties = "<w:numPr><w:ilvl w:val=\"\(level)\"/><w:numId w:val=\"\(numID)\"/></w:numPr><w:spacing w:after=\"80\"/>"
                return paragraph(text, properties: properties, kind: .normal)
            case let .table(rows):
                return table(rows)
            }
        }.joined()
        return """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>\(body)<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134" w:header="567" w:footer="567" w:gutter="0"/></w:sectPr></w:body></w:document>
        """
    }

    private static func paragraphProperties(for kind: ParagraphKind) -> String {
        switch kind {
        case .normal:
            "<w:spacing w:after=\"120\"/>"
        case .heading(let level):
            "<w:pStyle w:val=\"Heading\(min(6, max(1, level)))\"/><w:keepNext/>"
        case .quote:
            "<w:ind w:left=\"360\"/><w:spacing w:after=\"120\"/>"
        case .code:
            "<w:ind w:left=\"240\"/><w:spacing w:after=\"100\"/>"
        case .tableHeader:
            "<w:spacing w:after=\"40\"/>"
        }
    }

    private static func paragraph(_ markdown: String, properties: String, kind: ParagraphKind) -> String {
        let style = pdfStyle(for: kind)
        let runs = inlineRuns(markdown, style: InlineStyle(bold: style.bold, italic: style.italic, code: style.code))
        return "<w:p><w:pPr>\(properties)</w:pPr>\(runs)</w:p>"
    }

    private struct InlineStyle {
        var bold = false
        var italic = false
        var strike = false
        var code = false
    }

    private static func inlineRuns(_ markdown: String, style defaultStyle: InlineStyle = InlineStyle()) -> String {
        var result = ""
        var pending = ""
        var source = markdown[...]
        while !source.isEmpty {
            if let link = markdownLinkPrefix(source) {
                if !pending.isEmpty { result += run(pending, style: defaultStyle); pending = "" }
                result += run(link.label + " (" + link.url + ")", style: defaultStyle)
                source = source.dropFirst(link.length)
                continue
            }
            if let (marker, markedStyle) = markupPrefix(source),
               let close = source.dropFirst(marker.count).range(of: marker) {
                if !pending.isEmpty { result += run(pending, style: defaultStyle); pending = "" }
                let content = String(source[source.index(source.startIndex, offsetBy: marker.count)..<close.lowerBound])
                result += run(content, style: markedStyle)
                source = source[close.upperBound...]
                continue
            }
            pending.append(source.removeFirst())
        }
        if !pending.isEmpty { result += run(pending, style: defaultStyle) }
        return result.isEmpty ? "<w:r><w:t></w:t></w:r>" : result
    }

    private static func markdownLinkPrefix(_ text: Substring) -> (label: String, url: String, length: Int)? {
        guard text.first == "[", let labelEnd = text.firstIndex(of: "]"),
              text.index(after: labelEnd) < text.endIndex,
              text[text.index(after: labelEnd)] == "(",
              let urlEnd = text[text.index(after: labelEnd)...].firstIndex(of: ")") else { return nil }
        let label = String(text[text.index(after: text.startIndex)..<labelEnd])
        let openParen = text.index(after: labelEnd)
        let urlStart = text.index(after: openParen)
        let url = String(text[urlStart..<urlEnd])
        let length = text.distance(from: text.startIndex, to: text.index(after: urlEnd))
        return (label, url, length)
    }

    private static func markupPrefix(_ text: Substring) -> (marker: String, style: InlineStyle)? {
        if text.hasPrefix("**") { return ("**", InlineStyle(bold: true)) }
        if text.hasPrefix("__") { return ("__", InlineStyle(bold: true)) }
        if text.hasPrefix("~~") { return ("~~", InlineStyle(strike: true)) }
        if text.hasPrefix("`") { return ("`", InlineStyle(code: true)) }
        if text.hasPrefix("*") { return ("*", InlineStyle(italic: true)) }
        if text.hasPrefix("_") { return ("_", InlineStyle(italic: true)) }
        return nil
    }

    private static func run(_ text: String, style: InlineStyle) -> String {
        var properties = ""
        if style.bold { properties += "<w:b/>" }
        if style.italic { properties += "<w:i/>" }
        if style.strike { properties += "<w:strike/>" }
        if style.code {
            properties += "<w:rFonts w:ascii=\"Menlo\" w:hAnsi=\"Menlo\" w:eastAsia=\"PingFang SC\" w:cs=\"Menlo\"/><w:shd w:val=\"clear\" w:fill=\"F3F4F6\"/>"
        }
        let safeText = escapeXML(text)
        return "<w:r>\(properties.isEmpty ? "" : "<w:rPr>\(properties)</w:rPr>")<w:t xml:space=\"preserve\">\(safeText)</w:t></w:r>"
    }

    private static func escapeXML(_ text: String) -> String {
        String(text.unicodeScalars.compactMap { scalar -> String? in
            let value = scalar.value
            guard value == 0x9 || value == 0xA || value == 0xD || value >= 0x20 else { return nil }
            switch scalar {
            case "&": return "&amp;"
            case "<": return "&lt;"
            case ">": return "&gt;"
            case "\"": return "&quot;"
            case "'": return "&apos;"
            default: return String(scalar)
            }
        }.joined())
    }

    private static func table(_ rows: [[String]]) -> String {
        let columnCount = max(1, rows.map(\.count).max() ?? 1)
        let columnWidth = max(720, 9_360 / columnCount)
        let grid = (0..<columnCount).map { _ in "<w:gridCol w:w=\"\(columnWidth)\"/>" }.joined()
        let border = "<w:tblBorders><w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B8C0CC\"/><w:left w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B8C0CC\"/><w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B8C0CC\"/><w:right w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B8C0CC\"/><w:insideH w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B8C0CC\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B8C0CC\"/></w:tblBorders>"
        let body = rows.enumerated().map { rowIndex, row in
            let padded = row + Array(repeating: "", count: max(0, columnCount - row.count))
            let cells = padded.map { value in
                let text = inlineRuns(value, style: InlineStyle(bold: rowIndex == 0))
                let shading = rowIndex == 0 ? "<w:shd w:val=\"clear\" w:fill=\"EEF1F5\"/>" : ""
                return "<w:tc><w:tcPr><w:tcW w:w=\"\(columnWidth)\" w:type=\"dxa\"/>\(shading)<w:vAlign w:val=\"center\"/></w:tcPr><w:p><w:pPr><w:spacing w:after=\"40\"/></w:pPr>\(text)</w:p></w:tc>"
            }.joined()
            let headerRow = rowIndex == 0 ? "<w:trPr><w:tblHeader w:val=\"true\"/></w:trPr>" : ""
            return "<w:tr>\(headerRow)\(cells)</w:tr>"
        }.joined()
        return "<w:tbl><w:tblPr><w:tblStyle w:val=\"TableGrid\"/><w:tblW w:w=\"0\" w:type=\"auto\"/>\(border)</w:tblPr><w:tblGrid>\(grid)</w:tblGrid>\(body)</w:tbl>"
    }

    private static func pdfAttributedString(blocks: [Block], numbering: [ListNumbering]) -> NSAttributedString {
        let output = NSMutableAttributedString()
        let orderedListIDs = Set(numbering.filter { $0.abstractNumID == 1 }.map(\.numID))
        var counters: [Int: [Int]] = [:]

        func append(_ text: String, kind: ParagraphKind, level: Int = 0) {
            let style = pdfStyle(for: kind, level: level)
            output.append(NSAttributedString(string: text + "\n", attributes: style.attributes))
        }

        for block in blocks {
            switch block {
            case let .paragraph(text, kind):
                append(pdfText(plainText(text)), kind: kind)
            case let .listItem(text, level, numID, ordered):
                var marker = "•"
                if ordered || orderedListIDs.contains(numID) {
                    var values = counters[numID] ?? []
                    if values.count <= level { values += Array(repeating: 0, count: level - values.count + 1) }
                    values[level] += 1
                    if values.count > level + 1 { values.removeSubrange((level + 1)..<values.count) }
                    counters[numID] = values
                    marker = "\(values[level])."
                } else {
                    marker = ["•", "◦", "▪"][level % 3]
                }
                append(String(repeating: "  ", count: level) + marker + " " + pdfText(plainText(text)),
                       kind: .normal, level: level)
            case let .table(rows):
                guard let first = rows.first else { continue }
                let columnCount = max(1, rows.map(\.count).max() ?? 1)
                // At 9 pt, 84 monospaced cells leave a safe margin inside the A4 page.
                let maximumRowWidth = 84
                let widths = (0..<columnCount).map { column in
                    min(28, max(3, rows.compactMap { row -> Int? in
                        guard row.indices.contains(column) else { return nil }
                        return displayWidth(plainText(row[column]))
                    }.max() ?? 3))
                }
                let gridWidth = widths.reduce(0, +) + (columnCount * 3) + 1
                // Very wide tables stay readable as labeled lines instead of a grid
                // that would exceed the page or compress cells until the labels break.
                guard gridWidth <= maximumRowWidth else {
                    for row in rows.dropFirst() {
                        for column in 0..<columnCount {
                            let label = column < first.count ? plainText(first[column]) : "Column \(column + 1)"
                            let value = column < row.count ? plainText(row[column]) : ""
                            append("\(label): \(value)", kind: .normal)
                        }
                    }
                    continue
                }
                let renderedRows = rows.map { tableRow($0, widths: widths) }
                let rowWidth = renderedRows.flatMap { $0 }.map(displayWidth).max() ?? 0
                let rule = String(repeating: "─", count: max(1, rowWidth - 2))
                append("┌" + rule + "┐", kind: .code)
                for line in renderedRows[0] { append(line, kind: .tableHeader) }
                append("├" + rule + "┤", kind: .code)
                for row in renderedRows.dropFirst() {
                    for line in row { append(line, kind: .code) }
                }
                append("└" + rule + "┘", kind: .code)
            }
        }
        return output
    }

    private struct PDFStyle {
        var attributes: [NSAttributedString.Key: Any]
        var bold: Bool
        var italic: Bool
        var code: Bool
    }

    private static func pdfStyle(for kind: ParagraphKind, level: Int = 0) -> PDFStyle {
        let size: CGFloat
        let bold: Bool
        let italic: Bool
        let code: Bool
        let before: CGFloat
        let after: CGFloat
        switch kind {
        case .normal:
            size = 10.5; bold = false; italic = false; code = false; before = 0; after = 5
        case .heading(let level):
            size = [0, 18, 15, 12.5, 11.5, 11, 10.5][min(6, max(1, level))]
            bold = true; italic = false; code = false; before = 8; after = 4
        case .quote:
            size = 10.5; bold = false; italic = true; code = false; before = 0; after = 5
        case .code:
            size = 9; bold = false; italic = false; code = true; before = 0; after = 2
        case .tableHeader:
            size = 9; bold = true; italic = false; code = true; before = 0; after = 2
        }
        let font: NSFont
        if code {
            font = NSFont.monospacedSystemFont(ofSize: size, weight: bold ? .bold : .regular)
        } else if bold && italic {
            font = NSFontManager.shared.convert(NSFont.systemFont(ofSize: size, weight: .bold), toHaveTrait: .italicFontMask)
        } else if bold {
            font = NSFont.systemFont(ofSize: size, weight: .bold)
        } else if italic {
            font = NSFontManager.shared.convert(NSFont.systemFont(ofSize: size), toHaveTrait: .italicFontMask)
        } else {
            font = NSFont.systemFont(ofSize: size)
        }
        let paragraph = NSMutableParagraphStyle()
        paragraph.lineSpacing = 2
        paragraph.paragraphSpacingBefore = before
        paragraph.paragraphSpacing = after
        if case .quote = kind { paragraph.headIndent = 16; paragraph.firstLineHeadIndent = 16 }
        if level > 0 {
            paragraph.headIndent = CGFloat(level) * 14 + 14
            paragraph.firstLineHeadIndent = CGFloat(level) * 14
        }
        return PDFStyle(attributes: [.font: font, .foregroundColor: NSColor.black, .paragraphStyle: paragraph],
                        bold: bold, italic: italic, code: code)
    }

    private static func tableRow(_ cells: [String], widths: [Int]) -> [String] {
        let columns = widths.enumerated().map { index, width in
            let text = index < cells.count ? plainText(cells[index]) : ""
            return wrapTableCell(text, toDisplayWidth: width)
        }
        let lineCount = max(1, columns.map(\.count).max() ?? 1)
        return (0..<lineCount).map { lineIndex in
            let rendered = widths.enumerated().map { index, width in
                let text = lineIndex < columns[index].count ? columns[index][lineIndex] : ""
                return " " + pad(text, toDisplayWidth: width) + " "
            }
            return "│" + rendered.joined(separator: "│") + "│"
        }
    }

    private static func wrapTableCell(_ text: String, toDisplayWidth width: Int) -> [String] {
        guard width > 0 else { return [text] }
        var lines: [String] = []
        var current = ""
        var currentWidth = 0
        for character in text {
            if character == "\n" {
                lines.append(current)
                current = ""
                currentWidth = 0
                continue
            }
            let characterWidth = displayWidth(String(character))
            if !current.isEmpty && currentWidth + characterWidth > width {
                lines.append(current)
                current = ""
                currentWidth = 0
            }
            current.append(character)
            currentWidth += characterWidth
        }
        lines.append(current)
        return lines
    }

    /// Zero-width spaces give CoreText line-break opportunities in otherwise
    /// unbreakable URLs without adding visible punctuation to the exported PDF.
    private static func pdfText(_ text: String) -> String {
        text.split(separator: " ", omittingEmptySubsequences: false).map { part in
            let token = String(part)
            guard token.contains("://"), token.count > 36 else { return token }
            var wrapped = ""
            for character in token {
                wrapped.append(character)
                if ["/", "-", "?", "&", "=", "_"].contains(character) { wrapped.append("\u{200B}") }
            }
            return wrapped
        }.joined(separator: " ")
    }

    private static func pad(_ text: String, toDisplayWidth width: Int) -> String {
        let difference = max(0, width - displayWidth(text))
        return text + String(repeating: " ", count: difference)
    }

    private static func displayWidth(_ text: String) -> Int {
        text.unicodeScalars.reduce(0) { partial, scalar in
            let boxDrawing = (0x2500...0x259F).contains(scalar.value)
            return partial + (scalar.value >= 0x2E80 && !boxDrawing ? 2 : 1)
        }
    }

    private static func plainText(_ markdown: String) -> String {
        var result = ""
        var source = markdown[...]
        while !source.isEmpty {
            if let link = markdownLinkPrefix(source) {
                result += link.label + " (" + link.url + ")"
                source = source.dropFirst(link.length)
            } else if let (marker, _) = markupPrefix(source),
                      let close = source.dropFirst(marker.count).range(of: marker) {
                let content = source[source.index(source.startIndex, offsetBy: marker.count)..<close.lowerBound]
                result += plainText(String(content))
                source = source[close.upperBound...]
            } else {
                result.append(source.removeFirst())
            }
        }
        return result
    }

    private static func numberingXML(for listNumbering: [ListNumbering]) -> String {
        let bullets = """
        <w:abstractNum w:abstractNumId="0"><w:multiLevelType w:val="multilevel"/>\(numberingLevels(ordered: false))</w:abstractNum>
        <w:abstractNum w:abstractNumId="1"><w:multiLevelType w:val="multilevel"/>\(numberingLevels(ordered: true))</w:abstractNum>
        """
        let numbers = listNumbering.map { "<w:num w:numId=\"\($0.numID)\"><w:abstractNumId w:val=\"\($0.abstractNumID)\"/></w:num>" }.joined()
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:numbering xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">\(bullets)\(numbers)</w:numbering>"
    }

    private static func numberingLevels(ordered: Bool) -> String {
        (0...8).map { level in
            let indent = 720 * (level + 1)
            let marker = ordered ? "%\(level + 1)." : ["•", "◦", "▪"][level % 3]
            let format = ordered ? "decimal" : "bullet"
            let font = ordered ? "" : "<w:rPr><w:rFonts w:ascii=\"Symbol\" w:hAnsi=\"Symbol\" w:eastAsia=\"PingFang SC\"/></w:rPr>"
            return "<w:lvl w:ilvl=\"\(level)\"><w:start w:val=\"1\"/><w:numFmt w:val=\"\(format)\"/><w:lvlText w:val=\"\(marker)\"/><w:lvlJc w:val=\"left\"/><w:pPr><w:tabs><w:tab w:val=\"num\" w:pos=\"\(indent)\"/></w:tabs><w:ind w:left=\"\(indent)\" w:hanging=\"360\"/></w:pPr>\(font)</w:lvl>"
        }.joined()
    }

    private static let contentTypesXML = """
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
      <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
      <Default Extension="xml" ContentType="application/xml"/>
      <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
      <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
      <Override PartName="/word/numbering.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml"/>
    </Types>
    """

    private static let packageRelationshipsXML = """
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
    </Relationships>
    """

    private static let documentRelationshipsXML = """
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
      <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering" Target="numbering.xml"/>
    </Relationships>
    """

    private static let stylesXML = """
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
      <w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial" w:eastAsia="PingFang SC" w:cs="Arial"/><w:sz w:val="21"/><w:szCs w:val="21"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="120" w:line="300" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>
      <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:qFormat/></w:style>
      <w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before="360" w:after="180"/></w:pPr><w:rPr><w:b/><w:sz w:val="36"/><w:szCs w:val="36"/></w:rPr></w:style>
      <w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before="300" w:after="140"/></w:pPr><w:rPr><w:b/><w:sz w:val="30"/><w:szCs w:val="30"/></w:rPr></w:style>
      <w:style w:type="paragraph" w:styleId="Heading3"><w:name w:val="heading 3"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before="240" w:after="100"/></w:pPr><w:rPr><w:b/><w:sz w:val="25"/><w:szCs w:val="25"/></w:rPr></w:style>
      <w:style w:type="paragraph" w:styleId="Heading4"><w:name w:val="heading 4"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:rPr><w:b/><w:sz w:val="23"/><w:szCs w:val="23"/></w:rPr></w:style>
      <w:style w:type="paragraph" w:styleId="Heading5"><w:name w:val="heading 5"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:rPr><w:b/><w:sz w:val="22"/><w:szCs w:val="22"/></w:rPr></w:style>
      <w:style w:type="paragraph" w:styleId="Heading6"><w:name w:val="heading 6"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:rPr><w:b/><w:sz w:val="21"/><w:szCs w:val="21"/></w:rPr></w:style>
      <w:style w:type="table" w:styleId="TableGrid"><w:name w:val="Table Grid"/><w:tblPr><w:tblBorders><w:top w:val="single" w:sz="4"/><w:left w:val="single" w:sz="4"/><w:bottom w:val="single" w:sz="4"/><w:right w:val="single" w:sz="4"/><w:insideH w:val="single" w:sz="4"/><w:insideV w:val="single" w:sz="4"/></w:tblBorders></w:tblPr></w:style>
    </w:styles>
    """
}
