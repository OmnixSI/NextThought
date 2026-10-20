import AppKit
import Foundation
import InterviewHackerCore
import PDFKit
import Vision

/// Turns every material format the app accepts into one structured Markdown
/// document.
///
/// The point of the pipeline is that the model never has to care whether a
/// material arrived as a PDF, a Word file or a slide deck: everything ends up in
/// the same shape — a title, headings, lists, tables — which is also what gets
/// archived on disk, shown in the material list and cited by page.
enum MarkdownConverter {
    struct Result {
        var name: String
        var markdown: String
        /// One chunk per heading, used for retrieval and for citing a section.
        var sections: [String]
        /// Source format of the file ("pdf", "docx", "pptx", …).
        var format: String
        /// Which converter produced the Markdown.
        var converter: String
        var characterCount: Int { markdown.count }
    }

    static let supportedExtensions = ["txt", "md", "markdown", "csv", "json", "rtf", "pdf", "docx", "doc",
                                      "odt", "html", "htm", "pptx", "xlsx", "png", "jpg", "jpeg", "heic", "tiff"]

    /// Converts one local file. Runs off the main thread.
    static func convert(_ url: URL) throws -> Result {
        let format = url.pathExtension.lowercased()
        let name = url.deletingPathExtension().lastPathComponent

        // An external converter (Microsoft MarkItDown, Pandoc) does a better job
        // on complex layouts; the built-in one keeps working without it.
        if let external = ExternalMarkdownConverter.convert(url, format: format) {
            return Result(name: name, markdown: external.markdown, sections: external.sections,
                          format: format, converter: external.tool)
        }

        var document = MarkdownDocument()
        switch format {
        case "pdf":
            try document = pdf(url)
        case "docx":
            document = try word(url, part: "word/document.xml", kind: .docx)
        case "odt":
            document = try word(url, part: "content.xml", kind: .odt)
        case "rtf", "doc":
            document = attributed(url, type: format == "rtf" ? .rtf : .docFormat)
        case "html", "htm":
            document = html(try text(at: url))
        case "pptx":
            document = try slides(url)
        case "xlsx":
            document = try workbook(url)
        case "csv":
            document = try csv(url)
        case "png", "jpg", "jpeg", "heic", "tiff":
            document = try image(url)
        case "md", "markdown":
            document = try markdownFile(url)
        case "txt", "json":
            document = try plainText(url)
        default:
            throw AppFailure.capture("不支持 \(format)。支持 TXT、Markdown、CSV、JSON、RTF、PDF、Word、PowerPoint、Excel、HTML 和图片。")
        }

        let markdown = document.markdown
        return Result(name: name, markdown: markdown, sections: document.sections,
                      format: format, converter: "内置")
    }
}

// MARK: - The document model

extension MarkdownConverter {
    /// The formats the document model reads: whole pages, not text files.
    static let documentModelFormats: Set<String> = ["pdf", "png", "jpg", "jpeg", "heic", "tiff"]

    /// Converts one local file, letting the document model read scanned pages
    /// and images.
    ///
    /// The model reads whole pages and returns Markdown with headings, lists
    /// and tables, which the built-in reader cannot do. When the model is not
    /// available or fails, the built-in reader takes over, so an import never
    /// breaks because of the model.
    static func convert(_ url: URL, useDocumentModel: Bool) async throws -> Result {
        let format = url.pathExtension.lowercased()
        if useDocumentModel, documentModelFormats.contains(format),
           let result = try? await documentModelResult(url, format: format) {
            return result
        }
        return try convert(url)
    }

    /// Reads a file with the document model.
    private static func documentModelResult(_ url: URL, format: String) async throws -> Result {
        let name = url.deletingPathExtension().lastPathComponent
        let markdown: String
        if format == "pdf" {
            guard let pdf = PDFDocument(url: url), !pdf.isLocked, pdf.pageCount > 0 else {
                throw AppFailure.capture("PDF 无法读取或已加密。")
            }
            markdown = try await UnlimitedOCRService.read(pdf: pdf, maximumPages: documentModelPageLimit)
        } else {
            guard let image = NSImage(contentsOf: url),
                  let cgImage = image.cgImage(forProposedRect: nil, context: nil, hints: nil) else {
                throw AppFailure.capture("图片无法读取。")
            }
            markdown = try await UnlimitedOCRService.read(image: cgImage)
        }
        guard !markdown.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            throw AppFailure.capture("模型没有读出文字。")
        }
        var document = MarkdownDocument()
        document.raw(markdown)
        return Result(name: name, markdown: document.markdown, sections: document.sections,
                      format: format, converter: "Unlimited-OCR")
    }

    /// How many pages the document model reads per document.
    static var documentModelPageLimit: Int {
        let stored = UserDefaults.standard.object(forKey: "unlimitedOCRMaxPages") as? Int ?? 20
        return max(1, min(200, stored))
    }
}

// MARK: - Markdown document

/// Accumulates Markdown lines and derives the retrieval sections from them:
/// every heading starts a new section and keeps its heading, so a citation can
/// name the section it came from.
struct MarkdownDocument {
    private(set) var lines: [String] = []

    /// Splits a Markdown heading line into its level and its title.
    ///
    /// The title has to come from the second capture group: taking everything
    /// after the whole match would give an empty string, because the match
    /// covers the entire line.
    static func markdownHeading(_ line: String) -> (level: Int, title: String)? {
        guard let regex = try? NSRegularExpression(pattern: #"^(#{1,6})\s+(.+)$"#),
              let match = regex.firstMatch(in: line, range: NSRange(line.startIndex..., in: line)),
              let levelRange = Range(match.range(at: 1), in: line),
              let titleRange = Range(match.range(at: 2), in: line) else { return nil }
        let title = String(line[titleRange]).trimmingCharacters(in: .whitespaces)
        guard !title.isEmpty else { return nil }
        return (level: line[levelRange].count, title: title)
    }

    mutating func heading(_ text: String, level: Int = 2) {
        let clean = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !clean.isEmpty else { return }
        let marker = String(repeating: "#", count: max(1, min(6, level)))
        lines.append("\(marker) \(clean)")
    }

    mutating func paragraph(_ text: String) {
        let clean = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !clean.isEmpty else { return }
        lines.append(clean)
    }

    mutating func quote(_ text: String) {
        let clean = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !clean.isEmpty else { return }
        for line in clean.components(separatedBy: "\n") { lines.append("> \(line)") }
    }

    mutating func bullet(_ text: String, level: Int = 0) {
        let clean = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !clean.isEmpty else { return }
        let indent = String(repeating: "  ", count: max(0, min(4, level)))
        lines.append("\(indent)- \(clean)")
    }

    mutating func numbered(_ text: String, index: Int) {
        let clean = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !clean.isEmpty else { return }
        lines.append("\(index). \(clean)")
    }

    mutating func code(_ text: String) {
        let clean = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !clean.isEmpty else { return }
        lines.append("```")
        lines.append(clean)
        lines.append("```")
    }

    /// A Markdown table. The first row is the header.
    mutating func table(_ rows: [[String]]) {
        let cleaned = rows
            .map { $0.map { escapeCell($0) } }
            .filter { !$0.allSatisfy { $0.isEmpty } }
        guard !cleaned.isEmpty else { return }
        let width = cleaned.map(\.count).max() ?? 0
        guard width > 0 else { return }
        func row(_ cells: [String]) -> String {
            var padded = cells
            while padded.count < width { padded.append("") }
            return "| " + padded.joined(separator: " | ") + " |"
        }
        lines.append(row(cleaned[0]))
        lines.append("|" + String(repeating: " --- |", count: width))
        for cells in cleaned.dropFirst() { lines.append(row(cells)) }
    }

    private func escapeCell(_ text: String) -> String {
        text.replacingOccurrences(of: "|", with: "\\|")
            .replacingOccurrences(of: "\n", with: " ")
            .trimmingCharacters(in: .whitespaces)
    }

    var markdown: String {
        var out = lines.joined(separator: "\n")
        out = out.replacingOccurrences(of: #"\n{3,}"#, with: "\n\n", options: .regularExpression)
        return out.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    /// One chunk per heading; text before the first heading becomes its own chunk.
    var sections: [String] {
        var result: [String] = []
        var current: [String] = []
        for line in lines {
            if line.hasPrefix("#") {
                let chunk = current.joined(separator: "\n").trimmingCharacters(in: .whitespacesAndNewlines)
                if !chunk.isEmpty { result.append(chunk) }
                current = [line]
            } else {
                current.append(line)
            }
        }
        let chunk = current.joined(separator: "\n").trimmingCharacters(in: .whitespacesAndNewlines)
        if !chunk.isEmpty { result.append(chunk) }
        return result
    }

    /// Takes over Markdown that is already formatted.
    ///
    /// The document model returns Markdown with headings, lists and tables, so
    /// it is used as it is instead of being taken apart again.
    mutating func raw(_ markdown: String) {
        for line in markdown.components(separatedBy: "\n") { lines.append(line) }
        while lines.last?.trimmingCharacters(in: .whitespaces).isEmpty == true { lines.removeLast() }
    }
}

// MARK: - Per-format conversion

private extension MarkdownConverter {
    /// PDF: one section per page, so "p.3" still means something in a citation.
    static func pdf(_ url: URL) throws -> MarkdownDocument {
        guard let pdf = PDFDocument(url: url), !pdf.isLocked, pdf.pageCount <= 400 else {
            throw AppFailure.capture("\(url.lastPathComponent)：PDF 无法读取、已加密或超过 400 页。")
        }
        var document = MarkdownDocument()
        var ocrPages = 0
        for index in 0..<pdf.pageCount {
            try Task.checkCancellation()
            guard let page = pdf.page(at: index) else { continue }
            var text = page.string ?? ""
            // A scanned page has no text layer: render it and OCR on device.
            if text.trimmingCharacters(in: .whitespacesAndNewlines).count < 8, ocrPages < 80 {
                let bounds = page.bounds(for: .mediaBox)
                let scale = min(3, 2_400 / max(bounds.width, bounds.height, 1))
                let thumbnail = page.thumbnail(of: NSSize(width: bounds.width * scale, height: bounds.height * scale), for: .mediaBox)
                if let cgImage = thumbnail.cgImage(forProposedRect: nil, context: nil, hints: nil) {
                    text = (try? recognizeText(cgImage)) ?? ""
                    ocrPages += 1
                }
            }
            document.heading("p.\(index + 1)")
            for paragraph in text.components(separatedBy: "\n") {
                if paragraph.trimmingCharacters(in: .whitespaces).isEmpty { continue }
                document.paragraph(paragraph.trimmingCharacters(in: .whitespaces))
            }
        }
        return document
    }

    enum WordKind { case docx, odt }

    /// Word (.docx) and OpenDocument (.odt): the real heading levels and lists
    /// are read from the XML, not guessed from the plain text.
    static func word(_ url: URL, part: String, kind: WordKind) throws -> MarkdownDocument {
        let xml = try ZipPart.text(url, part)
        let delegate = WordXMLDelegate(kind: kind)
        let parser = XMLParser(data: Data(xml.utf8))
        parser.delegate = delegate
        guard parser.parse() else { return MarkdownDocument() }
        return delegate.document
    }

    /// Legacy .doc and .rtf: the system reader gives plain text, so headings are
    /// detected by shape instead of by style.
    static func attributed(_ url: URL, type: NSAttributedString.DocumentType) -> MarkdownDocument {
        guard let text = try? NSAttributedString(url: url, options: [.documentType: type], documentAttributes: nil).string else {
            return MarkdownDocument()
        }
        return plainTextDocument(text)
    }

    /// PowerPoint: one section per slide, its text as a bullet list.
    static func slides(_ url: URL) throws -> MarkdownDocument {
        let listing = try ZipPart.listing(url)
        let names = listing.split(separator: "\n").map(String.init)
            .filter { $0.range(of: #"^ppt/slides/slide\d+\.xml$"#, options: .regularExpression) != nil }
        guard !names.isEmpty else { throw AppFailure.capture("\(url.lastPathComponent)：文件中没有可读取的文字内容。") }
        var document = MarkdownDocument()
        for (index, name) in OfficeXMLText.numericOrder(names).prefix(300).enumerated() {
            let xml = try ZipPart.text(url, name)
            let delegate = SlideXMLDelegate()
            let parser = XMLParser(data: Data(xml.utf8))
            parser.delegate = delegate
            parser.parse()
            document.heading("幻灯片 \(index + 1)")
            for (level, text) in delegate.paragraphs {
                if level == 0 { document.bullet(text) } else { document.bullet(text, level: level) }
            }
        }
        return document
    }

    /// Excel: every sheet becomes a Markdown table.
    static func workbook(_ url: URL) throws -> MarkdownDocument {
        let sharedStrings = (try? ZipPart.text(url, "xl/sharedStrings.xml")).map { parseSharedStrings($0) } ?? []
        let listing = try ZipPart.listing(url)
        let sheetNames = listing.split(separator: "\n").map(String.init)
            .filter { $0.range(of: #"^xl/worksheets/sheet\d+\.xml$"#, options: .regularExpression) != nil }
        guard !sheetNames.isEmpty else { throw AppFailure.capture("\(url.lastPathComponent)：表格中没有可读取的工作表。") }

        var document = MarkdownDocument()
        let titles = parseSheetTitles(try ZipPart.text(url, "xl/workbook.xml"))
        for (index, name) in OfficeXMLText.numericOrder(sheetNames).prefix(60).enumerated() {
            let xml = try ZipPart.text(url, name)
            let rows = parseSheetRows(xml, sharedStrings: sharedStrings)
            guard !rows.isEmpty else { continue }
            document.heading(titles.indices.contains(index) ? titles[index] : "工作表 \(index + 1)")
            document.table(rows)
        }
        return document
    }

    /// CSV: the first line becomes the header of a Markdown table.
    static func csv(_ url: URL) throws -> MarkdownDocument {
        let rows = parseCSV(try text(at: url))
        var document = MarkdownDocument()
        guard !rows.isEmpty else { return document }
        document.table(rows)
        return document
    }

    /// HTML: headings, lists and tables keep their structure and their order.
    static func html(_ source: String) -> MarkdownDocument {
        var body = source
        body = body.replacingOccurrences(of: #"(?is)<(script|style|noscript)[^>]*>.*?</\1>"#, with: "", options: .regularExpression)
        body = body.replacingOccurrences(of: #"(?s)<!--.*?-->"#, with: "", options: .regularExpression)

        var document = MarkdownDocument()

        // One pass over the body, so a table keeps its place in the text
        // instead of being pulled to the end.
        let pattern = #"(?is)<table[^>]*>(.*?)</table>|<(h[1-6]|p|li|blockquote|pre)[^>]*>(.*?)</\2>"#
        guard let regex = try? NSRegularExpression(pattern: pattern, options: [.dotMatchesLineSeparators]) else {
            document.paragraph(htmlToText(source))
            return document
        }
        let ns = body as NSString
        for match in regex.matches(in: body, range: NSRange(location: 0, length: ns.length)) {
            if let tableRange = Range(match.range(at: 1), in: body) {
                appendTable(String(body[tableRange]), to: &document)
                continue
            }
            guard let tagRange = Range(match.range(at: 2), in: body),
                  let innerRange = Range(match.range(at: 3), in: body) else { continue }
            let tag = String(body[tagRange]).lowercased()
            let inner = htmlToText(String(body[innerRange]))
            guard !inner.isEmpty else { continue }
            if tag.hasPrefix("h"), let level = Int(tag.dropFirst()) {
                document.heading(inner, level: level)
            } else if tag == "li" {
                document.bullet(inner)
            } else if tag == "blockquote" {
                document.quote(inner)
            } else if tag == "pre" {
                document.code(inner)
            } else {
                document.paragraph(inner)
            }
        }
        return document
    }

    /// Turns one HTML table into a Markdown table.
    static func appendTable(_ html: String, to document: inout MarkdownDocument) {
        var rows: [[String]] = []
        guard let rowRegex = try? NSRegularExpression(pattern: #"(?is)<tr[^>]*>(.*?)</tr>"#),
              let cellRegex = try? NSRegularExpression(pattern: #"(?is)<t[dh][^>]*>(.*?)</t[dh]>"#) else { return }
        let ns = html as NSString
        for rowMatch in rowRegex.matches(in: html, range: NSRange(location: 0, length: ns.length)) {
            let body = ns.substring(with: rowMatch.range(at: 1))
            let bns = body as NSString
            var cells: [String] = []
            for cellMatch in cellRegex.matches(in: body, range: NSRange(location: 0, length: bns.length)) {
                cells.append(htmlToText(bns.substring(with: cellMatch.range(at: 1))))
            }
            if !cells.isEmpty { rows.append(cells) }
        }
        guard !rows.isEmpty else { return }
        document.table(rows)
    }

    /// Images: on-device OCR, the recognized lines become the section.
    static func image(_ url: URL) throws -> MarkdownDocument {
        guard let image = NSImage(contentsOf: url),
              let cgImage = image.cgImage(forProposedRect: nil, context: nil, hints: nil) else {
            throw AppFailure.capture("\(url.lastPathComponent)：无法读取图片。")
        }
        var document = MarkdownDocument()
        document.heading("图片文字")
        let recognized = try recognizeText(cgImage)
        for line in recognized.components(separatedBy: "\n") {
            let clean = line.trimmingCharacters(in: .whitespaces)
            guard !clean.isEmpty else { continue }
            document.paragraph(clean)
        }
        return document
    }

    /// A file that is already Markdown keeps its own headings.
    static func markdownFile(_ url: URL) throws -> MarkdownDocument {
        var document = MarkdownDocument()
        let source = try text(at: url)
        for line in source.components(separatedBy: "\n") {
            if let heading = MarkdownDocument.markdownHeading(line) {
                document.heading(heading.title, level: heading.level)
            } else if line.trimmingCharacters(in: .whitespaces).isEmpty {
                continue
            } else {
                document.paragraph(line.trimmingCharacters(in: .whitespaces))
            }
        }
        return document
    }

    /// Plain text: short stand-alone lines are treated as headings, so a
    /// structured note keeps its structure.
    static func plainText(_ url: URL) throws -> MarkdownDocument {
        plainTextDocument(try text(at: url))
    }

    static func plainTextDocument(_ source: String) -> MarkdownDocument {
        var document = MarkdownDocument()
        let blocks = source.components(separatedBy: "\n\n")
        for block in blocks {
            let lines = block.components(separatedBy: "\n").map { $0.trimmingCharacters(in: .whitespaces) }
            let meaningful = lines.filter { !$0.isEmpty }
            guard !meaningful.isEmpty else { continue }
            if meaningful.count == 1, meaningful[0].count <= 40, !meaningful[0].hasSuffix("。"),
               !meaningful[0].hasSuffix("！"), !meaningful[0].hasSuffix("？"), !meaningful[0].hasSuffix(".") {
                document.heading(meaningful[0])
            } else {
                for line in meaningful { document.paragraph(line) }
            }
        }
        return document
    }

    // MARK: Helpers

    /// Reads a text file, falling back to GB18030 for files exported on Windows.
    static func text(at url: URL) throws -> String {
        if let text = try? String(contentsOf: url, encoding: .utf8) { return text }
        let gb18030 = String.Encoding(rawValue: CFStringConvertEncodingToNSStringEncoding(CFStringEncoding(CFStringEncodings.GB_18030_2000.rawValue)))
        if let text = try? String(contentsOf: url, encoding: gb18030) { return text }
        throw AppFailure.capture("\(url.lastPathComponent)：无法识别文本编码，请另存为 UTF-8。")
    }

    static func recognizeText(_ image: CGImage) throws -> String {
        let request = VNRecognizeTextRequest()
        request.recognitionLevel = .accurate
        request.recognitionLanguages = ["zh-Hans", "en-US"]
        request.usesLanguageCorrection = true
        try VNImageRequestHandler(cgImage: image, options: [:]).perform([request])
        return (request.results ?? []).compactMap { $0.topCandidates(1).first?.string }.joined(separator: "\n")
    }

    static func htmlToText(_ html: String) -> String {
        var text = html
        text = text.replacingOccurrences(of: #"(?i)<br\s*/?>"#, with: "\n", options: .regularExpression)
        text = text.replacingOccurrences(of: #"(?i)</(p|div|li|tr|h[1-6])>"#, with: "\n", options: .regularExpression)
        text = text.replacingOccurrences(of: #"(?i)<li[^>]*>"#, with: "• ", options: .regularExpression)
        text = text.replacingOccurrences(of: #"<[^>]+>"#, with: "", options: .regularExpression)
        text = decodeEntities(text)
        text = text.replacingOccurrences(of: #"[ \t]+"#, with: " ", options: .regularExpression)
        text = text.replacingOccurrences(of: #"\n{3,}"#, with: "\n\n", options: .regularExpression)
        return text.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    static func decodeEntities(_ text: String) -> String {
        text.replacingOccurrences(of: "&nbsp;", with: " ")
            .replacingOccurrences(of: "&lt;", with: "<")
            .replacingOccurrences(of: "&gt;", with: ">")
            .replacingOccurrences(of: "&quot;", with: "\"")
            .replacingOccurrences(of: "&#39;", with: "'")
            .replacingOccurrences(of: "&apos;", with: "'")
            .replacingOccurrences(of: "&amp;", with: "&")
    }

    /// RFC 4180 style CSV: quoted fields may contain commas and line breaks.
    static func parseCSV(_ source: String) -> [[String]] {
        var rows: [[String]] = []
        var row: [String] = []
        var field = ""
        var inQuotes = false
        var iterator = source.makeIterator()
        var pending: Character?

        func endField() {
            row.append(field.trimmingCharacters(in: .whitespaces))
            field = ""
        }
        func endRow() {
            endField()
            if !(row.count == 1 && row[0].isEmpty) { rows.append(row) }
            row = []
        }

        while let character = pending ?? iterator.next() {
            pending = nil
            if inQuotes {
                if character == "\"" {
                    if let next = iterator.next() {
                        if next == "\"" { field.append("\"") } else { inQuotes = false; pending = next }
                    } else {
                        inQuotes = false
                    }
                } else {
                    field.append(character)
                }
            } else {
                switch character {
                case "\"": inQuotes = true
                case ",": endField()
                case "\n": endRow()
                case "\r": break
                default: field.append(character)
                }
            }
        }
        if !field.isEmpty || !row.isEmpty { endRow() }
        return rows
    }

    /// The cell strings of `xl/sharedStrings.xml`, in order.
    static func parseSharedStrings(_ xml: String) -> [String] {
        var result: [String] = []
        guard let regex = try? NSRegularExpression(pattern: #"(?is)<si>(.*?)</si>"#) else { return result }
        let ns = xml as NSString
        for match in regex.matches(in: xml, range: NSRange(location: 0, length: ns.length)) {
            let block = ns.substring(with: match.range(at: 1))
            var pieces: [String] = []
            guard let textRegex = try? NSRegularExpression(pattern: #"(?is)<t[^>]*>(.*?)</t>"#) else { continue }
            let bns = block as NSString
            for text in textRegex.matches(in: block, range: NSRange(location: 0, length: bns.length)) {
                pieces.append(decodeEntities(bns.substring(with: text.range(at: 1))))
            }
            result.append(pieces.joined())
        }
        return result
    }

    /// The sheet names of `xl/workbook.xml`, in order.
    static func parseSheetTitles(_ xml: String) -> [String] {
        var result: [String] = []
        guard let regex = try? NSRegularExpression(pattern: #"(?is)<sheet[^>]*name="([^"]*)""#) else { return result }
        let ns = xml as NSString
        for match in regex.matches(in: xml, range: NSRange(location: 0, length: ns.length)) {
            result.append(decodeEntities(ns.substring(with: match.range(at: 1))))
        }
        return result
    }

    /// The rows of one worksheet, with the gaps in the column order filled in.
    static func parseSheetRows(_ xml: String, sharedStrings: [String]) -> [[String]] {
        var rows: [[String]] = []
        guard let rowRegex = try? NSRegularExpression(pattern: #"(?is)<row[^>]*>(.*?)</row>"#),
              let cellRegex = try? NSRegularExpression(pattern: #"(?is)<c([^>]*)>(.*?)</c>"#),
              let valueRegex = try? NSRegularExpression(pattern: #"(?is)<v[^>]*>(.*?)</v>"#),
              let textRegex = try? NSRegularExpression(pattern: #"(?is)<t[^>]*>(.*?)</t>"#) else { return rows }

        let ns = xml as NSString
        for rowMatch in rowRegex.matches(in: xml, range: NSRange(location: 0, length: ns.length)) {
            let rowBody = ns.substring(with: rowMatch.range(at: 1))
            let rns = rowBody as NSString
            var cells: [String] = []
            for cellMatch in cellRegex.matches(in: rowBody, range: NSRange(location: 0, length: rns.length)) {
                let attributes = rns.substring(with: cellMatch.range(at: 1))
                let body = rns.substring(with: cellMatch.range(at: 2))
                let column = columnIndex(attributes)
                let type = cellType(attributes)
                var value = ""
                if type == "s" {
                    let vns = body as NSString
                    if let valueMatch = valueRegex.firstMatch(in: body, range: NSRange(location: 0, length: vns.length)),
                       let index = Int(vns.substring(with: valueMatch.range(at: 1))),
                       sharedStrings.indices.contains(index) {
                        value = sharedStrings[index]
                    }
                } else if type == "inlineStr" {
                    let bns = body as NSString
                    var pieces: [String] = []
                    for text in textRegex.matches(in: body, range: NSRange(location: 0, length: bns.length)) {
                        pieces.append(bns.substring(with: text.range(at: 1)))
                    }
                    value = pieces.joined()
                } else {
                    let bns = body as NSString
                    if let valueMatch = valueRegex.firstMatch(in: body, range: NSRange(location: 0, length: bns.length)) {
                        value = bns.substring(with: valueMatch.range(at: 1))
                    }
                }
                value = decodeEntities(value).trimmingCharacters(in: .whitespacesAndNewlines)
                // Pad up to the cell's own column so the table keeps its shape.
                while cells.count < column { cells.append("") }
                cells.append(value)
            }
            if !cells.allSatisfy({ $0.isEmpty }) { rows.append(cells) }
        }
        return rows
    }

    /// The `t="…"` attribute of a cell: "s" for a shared string, "inlineStr"
    /// for an inline one, empty for a number.
    static func cellType(_ attributes: String) -> String {
        guard let regex = try? NSRegularExpression(pattern: #"\bt="([^"]*)""#),
              let match = regex.firstMatch(in: attributes, range: NSRange(attributes.startIndex..., in: attributes)),
              let range = Range(match.range(at: 1), in: attributes) else { return "" }
        return String(attributes[range])
    }

    /// "B7" → 1 (zero based column index).
    static func columnIndex(_ attributes: String) -> Int {
        guard let regex = try? NSRegularExpression(pattern: #"r="([A-Z]+)""#),
              let match = regex.firstMatch(in: attributes, range: NSRange(attributes.startIndex..., in: attributes)),
              let range = Range(match.range(at: 1), in: attributes) else { return 0 }
        var index = 0
        for character in attributes[range].uppercased() {
            guard let value = character.asciiValue.map({ Int($0) - 64 }) else { continue }
            index = index * 26 + value
        }
        return max(0, index - 1)
    }
}

// MARK: - Word XML

/// Reads the heading levels, lists and tables out of a Word or OpenDocument
/// file. Plain text alone would lose exactly the structure the model needs.
private final class WordXMLDelegate: NSObject, XMLParserDelegate {
    private(set) var document = MarkdownDocument()

    private var paragraphText = ""
    private var headingLevel: Int?
    private var isListItem = false
    private var listDepth = 0
    private var inCell = false
    private var cellText = ""
    private var tableRows: [[String]] = []
    private var rowCells: [String] = []

    init(kind: MarkdownConverter.WordKind) { }

    func parser(_ parser: XMLParser, didStartElement elementName: String, namespaceURI: String?, qualifiedName: String?, attributes: [String: String] = [:]) {
        switch elementName {
        case "w:p", "text:p":
            paragraphText = ""
            headingLevel = nil
            isListItem = false
        case "w:pStyle":
            let style = attributes["w:val"] ?? ""
            if style.lowercased().hasPrefix("heading") {
                headingLevel = Int(style.dropFirst("heading".count)) ?? 1
            } else if style.lowercased() == "title" {
                headingLevel = 1
            }
        case "text:h":
            headingLevel = max(1, Int(attributes["text:outline-level"] ?? "") ?? 1)
            paragraphText = ""
        case "w:numPr":
            isListItem = true
        case "text:list":
            listDepth += 1
        case "w:br":
            paragraphText.append("\n")
        case "w:tbl", "table:table":
            tableRows = []
        case "w:tr", "table:table-row":
            rowCells = []
        case "w:tc", "table:table-cell":
            inCell = true
            cellText = ""
        default:
            break
        }
    }

    func parser(_ parser: XMLParser, foundCharacters string: String) {
        paragraphText += string
    }

    func parser(_ parser: XMLParser, didEndElement elementName: String, namespaceURI: String?, qualifiedName: String?) {
        switch elementName {
        case "w:p", "text:p":
            let text = paragraphText.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !text.isEmpty else { return }
            if inCell {
                cellText += (cellText.isEmpty ? "" : " ") + text
            } else if let level = headingLevel {
                document.heading(text, level: level)
            } else if isListItem || listDepth > 0 {
                document.bullet(text)
            } else {
                document.paragraph(text)
            }
        case "text:h":
            let text = paragraphText.trimmingCharacters(in: .whitespacesAndNewlines)
            if !text.isEmpty { document.heading(text, level: headingLevel ?? 1) }
            paragraphText = ""
            headingLevel = nil
        case "text:list":
            listDepth = max(0, listDepth - 1)
        case "w:tbl", "table:table":
            if !tableRows.isEmpty { document.table(tableRows) }
            tableRows = []
        case "w:tr", "table:table-row":
            tableRows.append(rowCells)
            rowCells = []
        case "w:tc", "table:table-cell":
            rowCells.append(cellText.trimmingCharacters(in: .whitespacesAndNewlines))
            inCell = false
            cellText = ""
        default:
            break
        }
    }
}

// MARK: - PowerPoint XML

/// Reads the text of every shape on a slide, with its indent level.
private final class SlideXMLDelegate: NSObject, XMLParserDelegate {
    private(set) var paragraphs: [(level: Int, text: String)] = []
    private var text = ""
    private var level = 0

    func parser(_ parser: XMLParser, didStartElement elementName: String, namespaceURI: String?, qualifiedName: String?, attributes: [String: String] = [:]) {
        switch elementName {
        case "a:p":
            text = ""
            level = 0
        case "a:pPr":
            if let value = attributes["lvl"], let parsed = Int(value) { level = parsed }
        case "a:br":
            text.append("\n")
        default:
            break
        }
    }

    func parser(_ parser: XMLParser, foundCharacters string: String) {
        text += string
    }

    func parser(_ parser: XMLParser, didEndElement elementName: String, namespaceURI: String?, qualifiedName: String?) {
        if elementName == "a:p" {
            let clean = text.trimmingCharacters(in: .whitespacesAndNewlines)
            if !clean.isEmpty { paragraphs.append((level, clean)) }
            text = ""
            level = 0
        }
    }
}

// MARK: - Office zip

/// Reads single parts out of an Office zip with the system `unzip` (no
/// third-party code is bundled).
enum ZipPart {
    static func listing(_ url: URL) throws -> String {
        try run(["-Z1", url.path])
    }

    static func text(_ url: URL, _ part: String) throws -> String {
        try run(["-p", url.path, part])
    }

    private static func run(_ arguments: [String]) throws -> String {
        let process = Process()
        let output = Pipe()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/unzip")
        process.arguments = arguments
        process.standardOutput = output
        process.standardError = FileHandle.nullDevice
        try process.run()
        let data = output.fileHandleForReading.readDataToEndOfFile()
        process.waitUntilExit()
        guard process.terminationStatus == 0 else {
            throw AppFailure.capture("无法解压 Office 文件，文件可能已损坏或受密码保护。")
        }
        guard data.count <= 60 * 1024 * 1024 else { throw AppFailure.capture("Office 文件内容过大。") }
        return String(decoding: data, as: UTF8.self)
    }
}
