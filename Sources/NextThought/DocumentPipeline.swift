import AppKit
import Foundation
import PDFKit
import ImageIO
import Vision
import NextThoughtCore

extension MarkdownConverter {
    static func convert(_ url: URL, options: DocumentConversionOptions) async throws -> Result {
        try Task.checkCancellation()
        if url.pathExtension.lowercased() == "ppt" {
            let directory = (options.checkpointDirectory ?? DocumentRuntime.temporary.appendingPathComponent(UUID().uuidString)).appendingPathComponent("legacy")
            options.onProgress(.init(.parsing, message: "正在本机转换旧版 PPT。"))
            let converted = try await LegacyOfficeConverter.convert(url, into: directory)
            var result = try await convert(converted, options: options)
            result.name = url.deletingPathExtension().lastPathComponent; result.format = "ppt"
            return result
        }
        if options.provider == .codex && url.pathExtension.lowercased() != "pdf" { return try await CodexDocumentReader.read(url, options: options) }
        let job = DocumentRuntime.temporary.appendingPathComponent(UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: job, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: job) }
        let format = url.pathExtension.lowercased()
        let sourceHash = options.sourceHash ?? ManagedModelStore.sha256(of: url) ?? ""
        let checkpoint = DocumentCheckpoint(options: options, sourceHash: sourceHash)
        var rawPages: [String: String] = [:]
        var blocks: [DocumentBlock] = [], warnings: [String] = []
        var codexAccessChecked = false
        func add(_ text: String, _ locator: DocumentLocator) { blocks += DocumentStructure.parse(text, locator: locator) }
        func finished(_ blocks: [DocumentBlock], _ warnings: [String]) -> Result {
            var content = DocumentContent(pipeline:options.provider.rawValue,sourceHash:sourceHash,blocks:blocks,warnings:DocumentNotice.unique(warnings),
                components:["pipeline":"5","ocrModelID":options.localModelID,"paddle":options.provider == .paddle ? (ModelCatalog.descriptor(for: options.localModelID)?.revision ?? "unknown") : "unused"])
            content.rawPages = rawPages.isEmpty ? nil : rawPages
            return Result(name:url.deletingPathExtension().lastPathComponent,markdown:content.markdown,
                sections:blocks.map(\.markdown),format:format,converter:options.provider.title,content:content)
        }
        func waitForForeground() async throws {
            while await options.isPaused() { try await Task.sleep(for: .milliseconds(200)); try Task.checkCancellation() }
        }
        func ocr(_ image: CGImage, locator: DocumentLocator) async throws -> String {
            try await waitForForeground()
            try Task.checkCancellation()
            if options.provider == .codex {
                guard DocumentRuntime.installed else {
                    throw DocumentDependencyError(reason: .runtime, message: String(localized: "Codex 文件代读需要文档组件，请准备后继续。"))
                }
                if !codexAccessChecked {
                    guard CodexBridge.executableURL() != nil else {
                        throw DocumentDependencyError(reason: .codexConnection, message: String(localized: "未检测到 Codex，连接后可继续读取扫描页面。"))
                    }
                    let status = await CodexBridge.loginStatus()
                    try Task.checkCancellation()
                    guard status.lowercased().contains("logged in") || status == "Codex 已登录" else {
                        throw DocumentDependencyError(reason: .codexConnection, message: String(localized: "需要登录 Codex 后才能读取扫描页面。"))
                    }
                    codexAccessChecked = true
                }
                let file = job.appendingPathComponent(UUID().uuidString + ".png")
                guard let bytes = NSBitmapImageRep(cgImage: image).representation(using: .png, properties: [:]) else {
                    throw AppFailure.capture("页面图像编码失败。")
                }
                try bytes.write(to: file)
                defer { try? FileManager.default.removeItem(at: file) }
                var pageOptions = options
                pageOptions.sourceHash = nil
                pageOptions.checkpointDirectory = nil
                let result = try await CodexDocumentReader.read(file, options: pageOptions)
                warnings += (result.content?.warnings ?? []).map { locator.label + "：" + $0 }
                return result.markdown
            }
            if options.provider == .paddle {
                do {
                    let ocrModel = ModelCatalog.localOCRChoices.first { $0.id == options.localModelID } ?? ModelCatalog.paddleOCR
                    guard ManagedModelStore.isInstalled(ocrModel), ManagedModelStore.isInstalled(ModelCatalog.layout) else {
                        throw DocumentDependencyError(reason: .ocrModels, message: "Paddle 或版面模型尚未完整安装。")
                    }
                    guard DocumentRuntime.installed else { throw DocumentDependencyError(reason: .runtime, message: "需要准备文档运行组件。") }
                    let file = job.appendingPathComponent(UUID().uuidString+".png")
                    defer { try? FileManager.default.removeItem(at: file) }
                    guard let encoded = NSBitmapImageRep(cgImage: image).representation(using: .png, properties: [:]) else {
                        throw AppFailure.capture("页面图像编码失败。")
                    }
                    try encoded.write(to: file)
                    let result = try await DocumentWorker.shared.request("ocr", ["image":file.path,
                        "model_directory":ocrModel.directory.path,
                        "layout_directory":ModelCatalog.layout.directory.path], timeout: min(options.timeout, 90), onProgress: { completed, total in
                        options.onProgress(.init(.ocr, completed: completed, total: total, message: locator.label + " · 识别区域"))
                    })
                    // Empty is an answer too: a photo or a plot has nothing to read.
                    guard let text = result["markdown"] as? String else { throw AppFailure.capture("模型未返回文字。") }
                    warnings += (result["warnings"] as? [String] ?? []).map { locator.label + "：" + $0 }
                    return text
                } catch is CancellationError { throw CancellationError() }
                catch let dependency as DocumentDependencyError { throw dependency }
                catch {
                    guard options.fallback else { throw error }
                    warnings.append("\(locator.label)：Paddle 失败，由系统 OCR 补读；\(error.localizedDescription)")
                }
            }
            return try systemOCR(image)
        }
        if format == "pdf" {
            guard let pdf = PDFDocument(url: url), !pdf.isLocked, pdf.pageCount > 0 else { throw AppFailure.capture("PDF 无法读取、为空或已加密。") }
            guard pdf.pageCount <= DocumentLimits.maximumPages else { throw AppFailure.capture("PDF 超过 2000 页，请拆分后导入（未导入任何页面）。") }
            // The text layer of every page first: which lines are page furniture, and whether
            // this is prose with numbered sections, can only be told from the whole document.
            let layers = (0..<pdf.pageCount).map { index in autoreleasepool { pdf.page(at: index)?.string ?? "" } }
            let furniture = PDFTextLayout.furniture(pages: layers)
            let isProse = PDFTextLayout.isProse(pages: layers)
            for index in 0..<pdf.pageCount {
                try await waitForForeground(); try Task.checkCancellation()
                let unit = "page-\(index + 1)"
                if let cached = checkpoint.load(unit) {
                    blocks += cached.blocks; warnings += cached.warnings; rawPages[unit] = cached.rawText
                    options.onProgress(.init(.parsing, completed: index + 1, total: pdf.pageCount, message: "已复用完成页面。")); continue
                }
                guard let page = pdf.page(at: index) else { throw AppFailure.capture("PDF 第 \(index+1) 页无法读取。") }
                // A section runs on over the page break: the page starts under the heading the last one ended in.
                let section = blocks.last { $0.locator.page != nil && !$0.locator.headingPath.contains("图像识别补充") }?.locator.headingPath ?? []
                let locator = DocumentLocator(page: index + 1, headingPath: section), text = layers[index]
                let readable = PDFTextLayout.markdown(text, furniture: furniture, numberedHeadings: isProse)
                rawPages[unit] = text
                let firstBlock = blocks.count, firstWarning = warnings.count
                // Read the text layer directly. Math and columns do not require
                // whole-page OCR, but a header alone must not hide a scanned body.
                let textCount = text.trimmingCharacters(in: .whitespacesAndNewlines).count
                let hasImage = hasLargeEmbeddedImage(page)
                if textCount >= 8 && !(hasImage && textCount < 512) {
                    add(readable, locator)
                    if hasImage {
                        warnings.append(String(localized: "p.\(index+1)：已提取原生文字；页内图像中的文字、图表或公式尚未读取，请核对原件。"))
                    }
                }
                else {
                    options.onProgress(.init(.ocr, completed: index, total: pdf.pageCount, message: "正在识别 p.\(index+1)"))
                    let recognized: String
                    do {
                        recognized = try await ocr(try autoreleasepool { try pageImage(page) }, locator: locator)
                    } catch is CancellationError { throw CancellationError() }
                    catch let dependency as DocumentDependencyError { throw dependency }
                    catch {
                        // Keep completed pages usable. This page remains uncached so a
                        // retry resumes here instead of repeating the whole document.
                        if !text.isEmpty { add(readable, locator) }
                        else { add("[此页识别失败，请重试或查看原件]", locator) }
                        warnings.append("p.\(index+1)：识别失败，其他页面已保留；\(error.localizedDescription)")
                        options.onProgress(.init(.parsing, completed: index + 1, total: pdf.pageCount, message: "此页未完成，继续读取其余页面。"))
                        continue
                    }
                    if recognized.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty {
                        if !text.isEmpty { add(readable, locator); warnings.append("p.\(index+1)：OCR 未提取内容，保留原始文字层，请核对图像区域。") }
                        else { add("[空白页或未识别到文字]", locator); warnings.append("p.\(index+1)：未识别到文字，请核对原件。") }
                    } else if !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                        // OCR can misread even a correct header or equation. Keep
                        // the exact native layer alongside the image transcription.
                        add(readable, locator); add(recognized, .init(page: index+1, headingPath: ["图像识别补充"]))
                    } else { add(recognized, locator) }
                }
                try checkpoint.save(unit, blocks: Array(blocks.dropFirst(firstBlock)), warnings: Array(warnings.dropFirst(firstWarning)), rawText: text)
                options.onProgress(.init(.parsing, completed: index + 1, total: pdf.pageCount, message: "页面解析完成。"))
            }
        } else if documentImageFormats.contains(format) {
            guard let imageSource=CGImageSourceCreateWithURL(url as CFURL,nil) else { throw AppFailure.capture("图片无法读取。") }
            let count=format == "tiff" ? CGImageSourceGetCount(imageSource) : 1
            guard count>0,count<=DocumentLimits.maximumPages else { throw AppFailure.capture("图片为空或超过 2000 页，请拆分后导入。") }
            for frame in 0..<count {
                try Task.checkCancellation()
                guard let image=CGImageSourceCreateThumbnailAtIndex(imageSource,frame,[kCGImageSourceCreateThumbnailFromImageAlways:true,kCGImageSourceThumbnailMaxPixelSize:2400,kCGImageSourceCreateThumbnailWithTransform:true] as CFDictionary) else { throw AppFailure.capture("图片第 \(frame+1) 页无法读取。") }
                let locator=DocumentLocator(page:count>1 ? frame+1 : nil)
                let unit = "image-\(frame+1)", firstBlock = blocks.count, firstWarning = warnings.count
                if let cached = checkpoint.load(unit) { blocks += cached.blocks; warnings += cached.warnings; continue }
                let text=try await ocr(image,locator:locator)
                if text.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty {
                    warnings.append("\(locator.label)：未识别到文字，请核对原件。")
                    add("[图像页未识别到文字]",locator)
                } else { add(text,locator) }
                try checkpoint.save(unit, blocks: Array(blocks.dropFirst(firstBlock)), warnings: Array(warnings.dropFirst(firstWarning)))
                options.onProgress(.init(.ocr, completed: frame + 1, total: count, message: "图像识别完成。"))
            }
        } else if (["docx","pptx","xlsx"].contains(format) || (["html","htm"].contains(format) && DocumentRuntime.installed)) {
            let result = try await DocumentWorker.shared.request("convert", ["path":url.path,"output_directory":job.path],timeout:options.timeout, onProgress: { completed, total in
                options.onProgress(.init(.parsing, completed: completed, total: total))
            })
            let parts = result["parts"] as? [[String:Any]] ?? []
            warnings += result["warnings"] as? [String] ?? []
            // The same picture on many slides is the template's artwork (a logo, a footer
            // band): read nothing from it. Any other picture is read once, however often it appears.
            let imageUses = Dictionary(grouping: parts.compactMap { $0["image"] as? String }, by: { $0 }).mapValues(\.count)
            var imageTexts: [String: String] = [:]
            // The words of the slides, tables and notes are all here already; a picture
            // takes seconds to read. The text goes ahead, so the file can be used meanwhile.
            // They are parsed once: the finished file keeps these very blocks, so what was
            // indexed from the text alone is not indexed again.
            var words: [Int: [DocumentBlock]] = [:]
            if parts.contains(where: { ($0["image"] as? String).map { imageUses[$0, default: 1] <= 2 } ?? false }) {
                for (index, part) in parts.enumerated() where part["image"] == nil {
                    let loc = part["locator"] as? [String:Any] ?? [:]
                    words[index] = checkpoint.load("office-\(index)")?.blocks
                        ?? DocumentStructure.parse(part["markdown"] as? String ?? "", locator: DocumentLocator(slide:loc["slide"] as? Int,sheet:loc["sheet"] as? String))
                }
                let text = words.keys.sorted().flatMap { words[$0] ?? [] }
                if text.contains(where: { !$0.markdown.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }) {
                    await options.onText(finished(text, warnings))
                    try Task.checkCancellation()
                }
            }
            for (partIndex, part) in parts.enumerated() {
                let unit = "office-\(partIndex)", firstBlock = blocks.count, firstWarning = warnings.count
                if let cached = checkpoint.load(unit) { blocks += cached.blocks; warnings += cached.warnings; continue }
                try Task.checkCancellation()
                let loc = part["locator"] as? [String:Any] ?? [:]
                var locator = DocumentLocator(slide:loc["slide"] as? Int,sheet:loc["sheet"] as? String)
                if let path = part["image"] as? String {
                    locator.headingPath = ["嵌图 \(part["name"] as? String ?? "")"]
                    if imageUses[path, default: 1] > 2 {
                        // Template artwork.
                    } else if let known = imageTexts[path] {
                        add(known, locator)
                    } else if let cg = NSImage(contentsOfFile:path)?.cgImage(forProposedRect:nil,context:nil,hints:nil) {
                        var text = ""
                        // The local model takes seconds per picture and drifts on ones without
                        // words; a quick look for lines of text decides whether it runs at all.
                        if cg.width >= 48, cg.height >= 48, options.provider == .system || hasText(cg) {
                            do { text = try await ocr(cg, locator: locator) }
                            catch is CancellationError { throw CancellationError() }
                            catch let dependency as DocumentDependencyError { throw dependency }
                            catch {
                                // A picture that cannot be read never costs the slides around it.
                                text = (try? systemOCR(cg)) ?? ""
                                warnings.append("\(locator.label)：嵌图由系统 OCR 补读；\(error.localizedDescription)")
                            }
                        }
                        // An icon reads as a stray character or two ("二", "←", "@"): not content.
                        if text.filter({ $0.isLetter || $0.isNumber }).count < 6 { text = "" }
                        imageTexts[path] = text
                        add(text, locator)
                    } else {
                        let message = "\(locator.label)：嵌图格式无法解码，请将原件导出 PDF 后重试。"
                        guard options.fallback else { throw AppFailure.capture(message) }
                        warnings.append(message); add("[嵌图未识别：请导出 PDF 后重新导入]",locator)
                    }
                } else if let parsed = words[partIndex] { blocks += parsed }
                else { add(part["markdown"] as? String ?? "",locator) }
                try checkpoint.save(unit, blocks: Array(blocks.dropFirst(firstBlock)), warnings: Array(warnings.dropFirst(firstWarning)), rawText: part["markdown"] as? String)
                options.onProgress(.init(.parsing, completed: partIndex + 1, total: parts.count, message: "Office 内容解析完成。"))
            }
        } else {
            let converted = try convert(url)
            add(converted.markdown,.init())
        }
        options.onProgress(.init(.validating, completed: blocks.count, total: blocks.count, message: "OCR / 解析已完成。"))
        return finished(blocks, warnings)
    }
    static let documentImageFormats: Set<String> = ["png","jpg","jpeg","heic","tiff"]
    static func systemOCR(_ image: CGImage) throws -> String {
        let request = VNRecognizeTextRequest();request.recognitionLevel = .accurate
        request.recognitionLanguages = ["zh-Hans","en-US"];request.usesLanguageCorrection = true
        try VNImageRequestHandler(cgImage:image,options:[:]).perform([request])
        return (request.results ?? []).compactMap { $0.topCandidates(1).first?.string }.joined(separator:"\n")
    }
    /// Whether a picture shows lines of text at all (any script), in a few milliseconds.
    static func hasText(_ image: CGImage) -> Bool {
        let request = VNDetectTextRectanglesRequest()
        request.reportCharacterBoxes = false
        guard (try? VNImageRequestHandler(cgImage: image, options: [:]).perform([request])) != nil else { return true }
        return !(request.results ?? []).isEmpty
    }
    static func pageImage(_ page: PDFPage) throws -> CGImage {
        let rect=page.bounds(for:.mediaBox), scale=min(3,2400/max(rect.width,rect.height,1))
        guard let cg=page.thumbnail(of:NSSize(width:rect.width*scale,height:rect.height*scale),for:.mediaBox)
            .cgImage(forProposedRect:nil,context:nil,hints:nil) else { throw AppFailure.capture("页面渲染失败。") }
        return cg
    }

    /// Inspect resource dictionaries without decoding pixels or walking every
    /// character. Nested Form XObjects and inherited page resources are common
    /// in scanner/exporter PDFs. Bounded traversal also handles cyclic resources.
    static func hasLargeEmbeddedImage(_ page: PDFPage) -> Bool {
        guard let reference = page.pageRef else { return false }
        var visited = Set<CGPDFDictionaryRef>()
        func scan(_ resources: CGPDFDictionaryRef, depth: Int) -> Bool {
            // An unusually deep form graph is uncertain coverage, never proof
            // that no image exists. Prefer OCR/a visible warning in this case.
            guard depth < 16 else { return true }
            var objects: CGPDFDictionaryRef?
            guard CGPDFDictionaryGetDictionary(resources, "XObject", &objects), let objects,
                  visited.insert(objects).inserted else { return false }
            var found = false
            CGPDFDictionaryApplyBlock(objects, { _, object, _ in
                var stream: CGPDFStreamRef?
                guard CGPDFObjectGetValue(object, .stream, &stream), let stream else { return true }
                guard let dictionary = CGPDFStreamGetDictionary(stream) else { return true }
                var subtype: UnsafePointer<CChar>?
                guard CGPDFDictionaryGetName(dictionary, "Subtype", &subtype), let subtype else { return true }
                switch String(cString: subtype) {
                case "Image":
                    var width: CGPDFInteger = 0, height: CGPDFInteger = 0
                    if CGPDFDictionaryGetInteger(dictionary, "Width", &width),
                       CGPDFDictionaryGetInteger(dictionary, "Height", &height), width >= 256, height >= 128 {
                        found = true
                    }
                case "Form":
                    var nested: CGPDFDictionaryRef?
                    if CGPDFDictionaryGetDictionary(dictionary, "Resources", &nested), let nested {
                        found = scan(nested, depth: depth + 1)
                    }
                default: break
                }
                return !found
            }, nil)
            return found
        }
        var dictionary: CGPDFDictionaryRef? = reference.dictionary
        for _ in 0..<32 {
            guard let current = dictionary else { return false }
            var resources: CGPDFDictionaryRef?
            if CGPDFDictionaryGetDictionary(current, "Resources", &resources), let resources {
                return scan(resources, depth: 0)
            }
            var parent: CGPDFDictionaryRef?
            guard CGPDFDictionaryGetDictionary(current, "Parent", &parent) else { return false }
            dictionary = parent
        }
        return true
    }
    static func isComplex(_ page: PDFPage) -> Bool {
        let text = page.string ?? ""
        if text.contains("\t") || text.unicodeScalars.contains(where: { "∑Σ∫∇√≤≥≠θη".unicodeScalars.contains($0) }) { return true }
        let pageWidth = page.bounds(for: .mediaBox).width
        if page.numberOfCharacters > 40_000 { return true }
        var previous: CGRect?
        for index in 0..<page.numberOfCharacters {
            let box = page.characterBounds(at: index)
            if box.width <= 0 { continue }
            if let old = previous, abs(box.midY-old.midY) < max(box.height,old.height)*0.6,
               box.minX-old.maxX > pageWidth*0.08 { return true }
            previous = box
        }
        let lines=page.selection(for: page.bounds(for: .mediaBox))?.selectionsByLine() ?? []
        let width=page.bounds(for:.mediaBox).width
        for (i,line) in lines.enumerated() {
            let a=line.bounds(for:page)
            if lines.dropFirst(i+1).contains(where:{ other in
                let b=other.bounds(for:page)
                return abs(a.midY-b.midY)<max(a.height,b.height)*0.6 && max(b.minX-a.maxX,a.minX-b.maxX)>width*0.08
            }) { return true }
        }
        return false
    }
}
