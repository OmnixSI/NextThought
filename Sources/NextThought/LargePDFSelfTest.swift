import AppKit
import CoreText
import PDFKit
import NextThoughtCore

/// Exercises the actual PDF converter with a long, searchable scientific document.
enum LargePDFSelfTest {
    static func run(_ options: [String: String]) async throws -> String {
        let directory = DocumentRuntime.temporary.appendingPathComponent("pdf-benchmark-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let source = directory.appendingPathComponent("research.pdf")
        var media = CGRect(x: 0, y: 0, width: 612, height: 792)
        guard let context = CGContext(source as CFURL, mediaBox: &media, nil) else { throw SelfTest.Failure("PDF context") }
        let count = 400
        let font = CTFontCreateWithName("Helvetica" as CFString, 12, nil)
        for index in 1...count {
            context.beginPDFPage(nil)
            for line in 0..<24 {
                let text = "Research page \(index), line \(line): alpha ∑ beta, experiment evidence and next steps."
                let attributed = NSAttributedString(string: text, attributes: [NSAttributedString.Key(kCTFontAttributeName as String): font])
                context.textPosition = CGPoint(x: 30, y: 750 - line * 27)
                CTLineDraw(CTLineCreateWithAttributedString(attributed), context)
            }
            context.endPDFPage()
        }
        context.closePDF()
        var conversion = DocumentConversionOptions(provider: .paddle, fallback: false)
        conversion.checkpointDirectory = directory.appendingPathComponent("checkpoints")
        let started = Date()
        let result = try await MarkdownConverter.convert(source, options: conversion)
        let cold = Date().timeIntervalSince(started)
        guard result.content?.blocks.contains(where: { $0.locator.page == count }) == true,
              result.markdown.contains("Research page 400"), result.content?.warnings.isEmpty == true else {
            throw SelfTest.Failure("text-layer PDF lost pages or unexpectedly invoked OCR")
        }
        let resumed = Date()
        let repeatResult = try await MarkdownConverter.convert(source, options: conversion)
        guard repeatResult.markdown == result.markdown else { throw SelfTest.Failure("checkpoint changes content") }
        let cachedSeconds = Date().timeIntervalSince(resumed)
        guard let document = PDFDocument(url: source), let first = document.page(at: 0) else { throw SelfTest.Failure("PDF fixture") }
        let mixed = directory.appendingPathComponent("mixed.pdf")
        guard let scanned = CGContext(mixed as CFURL, mediaBox: &media, nil) else { throw SelfTest.Failure("scan context") }
        let image = try MarkdownConverter.pageImage(first)
        scanned.beginPDFPage(nil)
        scanned.draw(image, in: media)
        scanned.endPDFPage()
        scanned.closePDF()
        guard let mixedDocument = PDFDocument(url: mixed), let nativePage = document.page(at: 1)?.copy() as? PDFPage else { throw SelfTest.Failure("mixed fixture") }
        mixedDocument.insert(nativePage, at: 1)
        guard mixedDocument.write(to: mixed) else { throw SelfTest.Failure("mixed PDF write") }
        let scanStarted = Date()
        let scan = try await MarkdownConverter.convert(mixed, options: .init(provider: .system))
        guard Set(scan.content?.blocks.compactMap { $0.locator.page } ?? []) == [1, 2],
              scan.content?.blocks.contains(where: { $0.locator.page == 1 && $0.markdown.localizedCaseInsensitiveContains("Research") }) == true else {
            throw SelfTest.Failure("scanned PDF did not retain recognized text and locator")
        }
        let scanSeconds = Date().timeIntervalSince(scanStarted)

        // Scanner PDFs often keep a searchable header/page number while their
        // entire body is an image. This is a different case from separate native
        // and scanned pages, and must never take the header-only fast path.
        func makeImagePage(_ name: String, nativeLines: Int) throws -> URL {
            let target = directory.appendingPathComponent(name)
            guard let canvas = CGContext(target as CFURL, mediaBox: &media, nil) else { throw SelfTest.Failure("hybrid PDF context") }
            canvas.beginPDFPage(nil)
            canvas.draw(image, in: media)
            for line in 0..<nativeLines {
                let text = nativeLines == 1 ? "Native header only" : "Native paragraph \(line): readable text with a figure whose labels remain in the image."
                let attributed = NSAttributedString(string: text, attributes: [NSAttributedString.Key(kCTFontAttributeName as String): font])
                canvas.textPosition = CGPoint(x: 30, y: 775 - line * 25)
                CTLineDraw(CTLineCreateWithAttributedString(attributed), canvas)
            }
            canvas.endPDFPage(); canvas.closePDF()
            return target
        }
        let headerScan = try makeImagePage("scan-with-header.pdf", nativeLines: 1)
        guard let headerPage = PDFDocument(url: headerScan)?.page(at: 0),
              (headerPage.string ?? "").count >= 8,
              !(headerPage.string ?? "").contains("experiment evidence"),
              MarkdownConverter.hasLargeEmbeddedImage(headerPage) else { throw SelfTest.Failure("header-plus-scan fixture") }
        let hybridStarted = Date()
        let headerResult = try await MarkdownConverter.convert(headerScan, options: .init(provider: .system))
        guard headerResult.markdown.contains("Native header only"),
              headerResult.markdown.localizedCaseInsensitiveContains("experiment evidence"),
              Set(headerResult.content?.blocks.compactMap { $0.locator.page } ?? []) == [1] else {
            throw SelfTest.Failure("native header caused scanned body to be silently skipped")
        }
        let hybridSeconds = Date().timeIntervalSince(hybridStarted)
        let illustrated = try makeImagePage("native-with-figure.pdf", nativeLines: 20)
        let illustratedResult = try await MarkdownConverter.convert(illustrated, options: .init(provider: .system))
        guard illustratedResult.markdown.contains("Native paragraph 19"),
              !illustratedResult.markdown.localizedCaseInsensitiveContains("experiment evidence"),
              illustratedResult.content?.warnings.isEmpty == false else {
            throw SelfTest.Failure("native figure scope was not disclosed or native text was discarded")
        }

        // No service call is made: dependency guards must run before any online
        // reader starts, even when the document itself has usable native pages.
        var dependencyChecks = 0
        if !DocumentRuntime.installed {
            do {
                _ = try await MarkdownConverter.convert(headerScan, options: .init(provider: .codex))
                throw SelfTest.Failure("missing Codex runtime became a successful partial import")
            } catch let dependency as DocumentDependencyError {
                guard dependency.reason == .runtime else { throw SelfTest.Failure("wrong Codex dependency") }
                dependencyChecks += 1
            }
        }
        if !ManagedModelStore.isInstalled(ModelCatalog.paddleOCR) || !ManagedModelStore.isInstalled(ModelCatalog.layout) {
            do {
                _ = try await MarkdownConverter.convert(headerScan, options: .init(provider: .paddle, fallback: true))
                throw SelfTest.Failure("missing selected OCR model was silently replaced")
            } catch let dependency as DocumentDependencyError {
                guard dependency.reason == .ocrModels else { throw SelfTest.Failure("wrong OCR dependency") }
                dependencyChecks += 1
            }
        }
        return "\(count) scientific text pages, cold=\(cold)s, cached=\(cachedSeconds)s, chars=\(result.markdown.count); scanned page=\(scanSeconds)s; same-page header+scan=\(hybridSeconds)s; native figures disclosed; dependency guards=\(dependencyChecks); locators retained"
    }
}
