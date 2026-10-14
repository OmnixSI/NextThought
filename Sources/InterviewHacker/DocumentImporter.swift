import AppKit
import Foundation
import InterviewHackerCore
import PDFKit
import Vision

/// Parses only user-selected local files. No macros, scripts, links or remote
/// resources are executed or fetched. Scanned PDFs and images use on-device OCR.
enum DocumentImporter {
    static let supportedExtensions = ["txt", "md", "markdown", "csv", "json", "rtf", "pdf", "docx", "doc", "odt", "html", "htm", "pptx", "xlsx", "png", "jpg", "jpeg", "heic", "tiff"]

    static func read(_ urls: [URL]) async throws -> [ContextDocument] {
        try await Task.detached(priority: .userInitiated) {
            var documents: [ContextDocument] = []
            for url in urls.prefix(12) {
                try Task.checkCancellation()
                let accessed = url.startAccessingSecurityScopedResource()
                defer { if accessed { url.stopAccessingSecurityScopedResource() } }
                let attributes = try url.resourceValues(forKeys: [.fileSizeKey, .isRegularFileKey])
                guard attributes.isRegularFile == true, let size = attributes.fileSize, size <= 20 * 1024 * 1024 else {
                    throw AppFailure.capture("\(url.lastPathComponent)：仅支持不超过 20 MiB 的普通文件。")
                }
                let pages = try pages(of: url)
                guard pages.contains(where: { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }) else {
                    throw AppFailure.capture("\(url.lastPathComponent)：没有可提取的文字。")
                }
                guard pages.reduce(0, { $0 + $1.count }) <= 200_000 else {
                    throw AppFailure.capture("\(url.lastPathComponent)：文字超过 20 万字，请按章节拆分。")
                }
                documents.append(ContextDocument(name: url.lastPathComponent, pages: pages))
            }
            return documents
        }.value
    }

    private static func pages(of url: URL) throws -> [String] {
        switch url.pathExtension.lowercased() {
        case "pdf":
            return try pdfPages(url)
        case "rtf":
            return [try attributedText(url, type: .rtf)]
        case "docx":
            return [try attributedText(url, type: .officeOpenXML)]
        case "doc":
            return [try attributedText(url, type: .docFormat)]
        case "odt":
            return [try attributedText(url, type: .openDocument)]
        case "html", "htm":
            // NSAttributedString's HTML importer needs the main thread and WebKit;
            // plain tag stripping is enough for retrieval.
            return [htmlText(try String(contentsOf: url, encoding: .utf8))]
        case "pptx":
            // One page per slide so answers can cite “p.N” = slide N.
            return try zippedXML(url, matching: #"^ppt/slides/slide\d+\.xml$"#)
        case "xlsx":
            return try zippedXML(url, matching: #"^xl/sharedStrings\.xml$"#)
        case "png", "jpg", "jpeg", "heic", "tiff":
            guard let image = NSImage(contentsOf: url), let cgImage = image.cgImage(forProposedRect: nil, context: nil, hints: nil) else {
                throw AppFailure.capture("\(url.lastPathComponent)：无法读取图片。")
            }
            return [try recognizeText(cgImage)]
        case "txt", "md", "markdown", "csv", "json":
            if let text = try? String(contentsOf: url, encoding: .utf8) { return [text] }
            // GB18030 is common for Chinese text files exported on Windows.
            let gb18030 = String.Encoding(rawValue: CFStringConvertEncodingToNSStringEncoding(CFStringEncoding(CFStringEncodings.GB_18030_2000.rawValue)))
            if let text = try? String(contentsOf: url, encoding: gb18030) { return [text] }
            throw AppFailure.capture("\(url.lastPathComponent)：无法识别文本编码，请另存为 UTF-8。")
        default:
            throw AppFailure.capture("不支持 \(url.pathExtension)。支持 TXT、Markdown、CSV、JSON、RTF、PDF、Word、PowerPoint、Excel、HTML 和图片。")
        }
    }

    private static func attributedText(_ url: URL, type: NSAttributedString.DocumentType) throws -> String {
        // Remote resources in HTML/Word are not fetched for plain-text extraction.
        try NSAttributedString(url: url, options: [.documentType: type], documentAttributes: nil).string
    }

    private static func htmlText(_ html: String) -> String {
        var text = html.replacingOccurrences(of: #"(?is)<(script|style)[^>]*>.*?</\1>"#, with: " ", options: .regularExpression)
        text = text.replacingOccurrences(of: #"(?i)<(br|/p|/div|/li|/h\d|/tr)[^>]*>"#, with: "\n", options: .regularExpression)
        text = text.replacingOccurrences(of: #"<[^>]+>"#, with: "", options: .regularExpression)
        return text.replacingOccurrences(of: "&nbsp;", with: " ").replacingOccurrences(of: "&lt;", with: "<")
            .replacingOccurrences(of: "&gt;", with: ">").replacingOccurrences(of: "&amp;", with: "&")
            .replacingOccurrences(of: #"[ \t]+"#, with: " ", options: .regularExpression)
    }

    private static func pdfPages(_ url: URL) throws -> [String] {
        guard let pdf = PDFDocument(url: url), !pdf.isLocked, pdf.pageCount <= 200 else {
            throw AppFailure.capture("\(url.lastPathComponent)：PDF 无法读取、已加密或超过 200 页。")
        }
        var pages: [String] = []
        var characters = 0
        var ocrPages = 0
        for index in 0..<pdf.pageCount {
            try Task.checkCancellation()
            guard let page = pdf.page(at: index) else { pages.append(""); continue }
            var text = page.string ?? ""
            // Scanned page: render and OCR on device (bounded to keep import responsive).
            if text.trimmingCharacters(in: .whitespacesAndNewlines).count < 8, ocrPages < 60 {
                let bounds = page.bounds(for: .mediaBox)
                let scale = min(3, 2_400 / max(bounds.width, bounds.height, 1))
                let thumbnail = page.thumbnail(of: NSSize(width: bounds.width * scale, height: bounds.height * scale), for: .mediaBox)
                if let cgImage = thumbnail.cgImage(forProposedRect: nil, context: nil, hints: nil) {
                    text = (try? recognizeText(cgImage)) ?? ""
                    ocrPages += 1
                }
            }
            characters += text.count
            guard characters <= 200_000 else { throw AppFailure.capture("PDF 文字超过 20 万字，请按章节拆分。") }
            pages.append(text)
        }
        return pages
    }

    private static func recognizeText(_ image: CGImage) throws -> String {
        let request = VNRecognizeTextRequest()
        request.recognitionLevel = .accurate
        request.recognitionLanguages = ["zh-Hans", "en-US"]
        request.usesLanguageCorrection = true
        try VNImageRequestHandler(cgImage: image, options: [:]).perform([request])
        return (request.results ?? []).compactMap { $0.topCandidates(1).first?.string }.joined(separator: "\n")
    }

    /// Reads XML parts from an Office zip with the system `unzip` (no third-party code).
    private static func zippedXML(_ url: URL, matching pattern: String) throws -> [String] {
        let listing = try run(["-Z1", url.path])
        let names = listing.split(separator: "\n").map(String.init).filter { $0.range(of: pattern, options: .regularExpression) != nil }
        guard !names.isEmpty else { throw AppFailure.capture("\(url.lastPathComponent)：文件中没有可读取的文字内容。") }
        return try OfficeXMLText.numericOrder(names).prefix(200).map { name in
            OfficeXMLText.extract(try run(["-p", url.path, name]))
        }
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
        guard process.terminationStatus == 0 else { throw AppFailure.capture("无法解压 Office 文件，文件可能已损坏或受密码保护。") }
        guard data.count <= 40 * 1024 * 1024 else { throw AppFailure.capture("Office 文件内容过大。") }
        return String(decoding: data, as: UTF8.self)
    }
}
