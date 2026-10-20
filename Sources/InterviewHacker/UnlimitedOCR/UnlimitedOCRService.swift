import CoreGraphics
import Foundation
import PDFKit

/// Reads documents with the Unlimited-OCR Core ML bundle.
///
/// The model turns whole pages into structured Markdown (headings, lists,
/// tables, formulas) and can read several pages in one pass, which is why a
/// scanned PDF is handled page by page instead of line by line.
///
/// Everything happens on this machine: the weights are Core ML models in the
/// app data directory, the pages are rendered locally and the text never
/// leaves the device.
enum UnlimitedOCRService {
    /// The model can only be used from macOS 15 on: the Core ML state of the
    /// decoder (the key/value cache) was introduced with macOS 15.
    static var isSupported: Bool {
        if #available(macOS 15.0, *) { return true }
        return false
    }

    /// The model files are present and complete.
    static var isInstalled: Bool {
        ManagedModelStore.isInstalled(ModelCatalog.unlimitedOCR)
    }

    /// Why the model cannot be used, for the settings list.
    static var unavailableReason: String? {
        if !isSupported { return "需要 macOS 15 或更高版本。" }
        if !isInstalled { return "尚未下载模型。" }
        return nil
    }

    /// Reads a PDF into Markdown.
    ///
    /// - Parameter maximumPages: how many pages of the document to read; the
    ///   rest is left to the caller's fallback.
    static func read(pdf: PDFDocument, maximumPages: Int) async throws -> String {
        guard isInstalled else { throw UnlimitedOCRError.unavailable }
        if #available(macOS 15.0, *) {
            return try await readWithModel(pdf: pdf, maximumPages: maximumPages)
        }
        throw UnlimitedOCRError.unavailable
    }

    /// Reads a single image into Markdown.
    static func read(image: CGImage) async throws -> String {
        guard isInstalled else { throw UnlimitedOCRError.unavailable }
        if #available(macOS 15.0, *) {
            return try await readWithModel(image: image)
        }
        throw UnlimitedOCRError.unavailable
    }

    @available(macOS 15.0, *)
    private static func readWithModel(pdf: PDFDocument, maximumPages: Int) async throws -> String {
        guard pdf.pageCount > 0 else { throw UnlimitedOCRError.unavailable }
        var markdown: [String] = []
        var page = 0
        while page < min(pdf.pageCount, maximumPages) {
            let count = min(pagesPerRead, pdf.pageCount - page)
            var pages: [UnlimitedOcrPage] = []
            for offset in 0..<count {
                guard let pdfPage = pdf.page(at: page + offset) else { continue }
                pages.append(try UnlimitedOcrPagePreprocessor.page(from: pdfPage))
            }
            guard !pages.isEmpty else { break }
            let text = try await Runtime.shared.read(pages: pages)
            let parsed = UnlimitedOcrDocument(parsing: text).markdown
            if !parsed.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                markdown.append(parsed)
            }
            page += count
        }
        return markdown.joined(separator: "\n\n---\n\n")
    }

    @available(macOS 15.0, *)
    private static func readWithModel(image: CGImage) async throws -> String {
        let page = try UnlimitedOcrPagePreprocessor.page(from: image)
        let text = try await Runtime.shared.read(pages: [page])
        return UnlimitedOcrDocument(parsing: text).markdown
    }

    /// How many pages one generation can hold: the decoder bundles are built
    /// for a prefix of up to 2,304 tokens and a page costs 273 tokens.
    static var pagesPerRead: Int { 8 }

    enum UnlimitedOCRError: LocalizedError {
        case unavailable

        var errorDescription: String? {
            switch self {
            case .unavailable: "Unlimited-OCR 不可用：模型未安装或系统版本过低。"
            }
        }
    }

    /// Holds the loaded model.
    ///
    /// Loading compiles the Core ML packages, which takes a while, so the
    /// manager is kept around and reused for the next document.
    @available(macOS 15.0, *)
    private actor Runtime {
        static let shared = Runtime()
        private var manager: UnlimitedOcrManager?

        func read(pages: [UnlimitedOcrPage]) async throws -> String {
            let manager = try await load()
            var text = ""
            for try await event in manager.read(pages: pages, prompt: pages.count > 1 ? "multi" : "single") {
                if case .text(let part) = event { text += part }
            }
            return text
        }

        private func load() async throws -> UnlimitedOcrManager {
            if let manager { return manager }
            let directory = ModelCatalog.unlimitedOCR.directory
            let manager = try await UnlimitedOcrManager.load(from: directory, computeUnits: .cpuAndGPU)
            self.manager = manager
            return manager
        }
    }
}
