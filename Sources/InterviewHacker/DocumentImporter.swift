import AppKit
import Foundation
import InterviewHackerCore
import PDFKit

/// Parses only user-selected local files. No macros, scripts, links or OCR are executed.
enum DocumentImporter {
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
                var pages: [String]
                switch url.pathExtension.lowercased() {
                case "pdf":
                    guard let pdf = PDFDocument(url: url), !pdf.isLocked, pdf.pageCount <= 200 else {
                        throw AppFailure.capture("\(url.lastPathComponent)：PDF 无法读取、已加密或超过 200 页。")
                    }
                    pages = []
                    var characters = 0
                    for index in 0..<pdf.pageCount {
                        try Task.checkCancellation()
                        let text = pdf.page(at: index)?.string ?? ""
                        characters += text.count
                        guard characters <= 200_000 else { throw AppFailure.capture("PDF 文字超过 20 万字，请按章节拆分。") }
                        pages.append(text)
                    }
                case "rtf":
                    pages = [try NSAttributedString(url: url, options: [.documentType: NSAttributedString.DocumentType.rtf], documentAttributes: nil).string]
                case "txt", "md", "csv", "json":
                    pages = [try String(contentsOf: url, encoding: .utf8)]
                default:
                    throw AppFailure.capture("不支持 \(url.pathExtension)。请使用 UTF-8 TXT、Markdown、CSV、JSON、RTF 或文字 PDF。")
                }
                guard pages.contains(where: { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }) else {
                    throw AppFailure.capture("\(url.lastPathComponent)：没有可提取的文字。扫描版 PDF 请先转为文字版。")
                }
                guard pages.reduce(0, { $0 + $1.count }) <= 200_000 else {
                    throw AppFailure.capture("\(url.lastPathComponent)：文字超过 20 万字，请按章节拆分。")
                }
                documents.append(ContextDocument(name: url.lastPathComponent, pages: pages))
            }
            return documents
        }.value
    }
}
