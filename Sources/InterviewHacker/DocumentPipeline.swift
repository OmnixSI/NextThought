import AppKit
import Foundation
import PDFKit
import ImageIO
import Vision
import InterviewHackerCore

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
        if options.provider == .codex { return try await CodexDocumentReader.read(url, options: options) }
        let job = DocumentRuntime.temporary.appendingPathComponent(UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: job, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: job) }
        let format = url.pathExtension.lowercased()
        let sourceHash = options.sourceHash ?? ManagedModelStore.sha256(of: url) ?? ""
        let checkpoint = DocumentCheckpoint(options: options, sourceHash: sourceHash)
        var rawPages: [String: String] = [:]
        var blocks: [DocumentBlock] = [], warnings: [String] = []
        func add(_ text: String, _ locator: DocumentLocator) { blocks += DocumentStructure.parse(text, locator: locator) }
        func waitForForeground() async throws {
            while await options.isPaused() { try await Task.sleep(for: .milliseconds(200)); try Task.checkCancellation() }
        }
        func ocr(_ image: CGImage, locator: DocumentLocator) async throws -> String {
            try await waitForForeground()
            if options.provider == .paddle {
                do {
                    let ocrModel = ModelCatalog.localOCRChoices.first { $0.id == options.localModelID } ?? ModelCatalog.paddleOCR
                    guard ManagedModelStore.isInstalled(ocrModel), ManagedModelStore.isInstalled(ModelCatalog.layout) else {
                        throw AppFailure.capture("Paddle 或版面模型尚未完整安装。")
                    }
                    let file = job.appendingPathComponent(UUID().uuidString+".png")
                    try NSBitmapImageRep(cgImage: image).representation(using: .png, properties: [:])!.write(to: file)
                    let result = try await DocumentWorker.shared.request("ocr", ["image":file.path,
                        "model_directory":ocrModel.directory.path,
                        "layout_directory":ModelCatalog.layout.directory.path], timeout: options.timeout, onProgress: { completed, total in
                        options.onProgress(.init(.ocr, completed: completed, total: total, message: locator.label + " · 识别区域"))
                    })
                    guard let text = result["markdown"] as? String, !text.isEmpty else { throw AppFailure.capture("模型未返回文字。") }
                    return text
                } catch is CancellationError { throw CancellationError() }
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
            for index in 0..<pdf.pageCount {
                try await waitForForeground(); try Task.checkCancellation()
                let unit = "page-\(index + 1)"
                if let cached = checkpoint.load(unit) {
                    blocks += cached.blocks; warnings += cached.warnings; rawPages[unit] = cached.rawText
                    options.onProgress(.init(.parsing, completed: index + 1, total: pdf.pageCount, message: "已复用完成页面。")); continue
                }
                guard let page = pdf.page(at: index) else { throw AppFailure.capture("PDF 第 \(index+1) 页无法读取。") }
                let locator = DocumentLocator(page:index+1), text = page.string ?? ""
                rawPages[unit] = text
                let firstBlock = blocks.count, firstWarning = warnings.count
                let complex = isComplex(page)
                if text.trimmingCharacters(in: .whitespacesAndNewlines).count >= 8 && !complex { add(text, locator) }
                else {
                    options.onProgress(.init(.ocr, completed: index, total: pdf.pageCount, message: "正在识别 p.\(index+1)"))
                    let recognized = try await ocr(try pageImage(page), locator: locator)
                    if recognized.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty {
                        if !text.isEmpty { add(text, locator); warnings.append("p.\(index+1)：OCR 未提取内容，保留原始文字层，请核对图像区域。") }
                        else { add("[空白页或未识别到文字]", locator); warnings.append("p.\(index+1)：未识别到文字，请核对原件。") }
                    } else if text.count >= 80, recognized.count < text.count / 2 {
                        add(text, locator); add(recognized, .init(page: index+1, headingPath: ["图像识别补充"]))
                        warnings.append("p.\(index+1)：识别结果少于原文字层，原文与补充识别均已保留。")
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
            for (partIndex, part) in parts.enumerated() {
                let unit = "office-\(partIndex)", firstBlock = blocks.count, firstWarning = warnings.count
                if let cached = checkpoint.load(unit) { blocks += cached.blocks; warnings += cached.warnings; continue }
                try Task.checkCancellation()
                let loc = part["locator"] as? [String:Any] ?? [:]
                var locator = DocumentLocator(slide:loc["slide"] as? Int,sheet:loc["sheet"] as? String)
                if let path = part["image"] as? String {
                    locator.headingPath = ["嵌图 \(part["name"] as? String ?? "")"]
                    if let cg = NSImage(contentsOfFile:path)?.cgImage(forProposedRect:nil,context:nil,hints:nil) {
                        add(try await ocr(cg,locator:locator),locator)
                    } else {
                        let message = "\(locator.label)：嵌图格式无法解码，请将原件导出 PDF 后重试。"
                        guard options.fallback else { throw AppFailure.capture(message) }
                        warnings.append(message); add("[嵌图未识别：请导出 PDF 后重新导入]",locator)
                    }
                } else { add(part["markdown"] as? String ?? "",locator) }
                try checkpoint.save(unit, blocks: Array(blocks.dropFirst(firstBlock)), warnings: Array(warnings.dropFirst(firstWarning)), rawText: part["markdown"] as? String)
                options.onProgress(.init(.parsing, completed: partIndex + 1, total: parts.count, message: "Office 内容解析完成。"))
            }
        } else {
            let converted = try convert(url)
            add(converted.markdown,.init())
        }
        var content = DocumentContent(pipeline:options.provider.rawValue,sourceHash:sourceHash,blocks:blocks,warnings:warnings,
            components:["pipeline":"2","ocrModelID":options.localModelID,"paddle":options.provider == .paddle ? (ModelCatalog.descriptor(for: options.localModelID)?.revision ?? "unknown") : "unused"])
        content.rawPages = rawPages.isEmpty ? nil : rawPages
        options.onProgress(.init(.validating, completed: blocks.count, total: blocks.count, message: "OCR / 解析已完成。"))
        return Result(name:url.deletingPathExtension().lastPathComponent,markdown:content.markdown,
            sections:blocks.map(\.markdown),format:format,converter:options.provider.title,content:content)
    }
    static let documentImageFormats: Set<String> = ["png","jpg","jpeg","heic","tiff"]
    static func systemOCR(_ image: CGImage) throws -> String {
        let request = VNRecognizeTextRequest();request.recognitionLevel = .accurate
        request.recognitionLanguages = ["zh-Hans","en-US"];request.usesLanguageCorrection = true
        try VNImageRequestHandler(cgImage:image,options:[:]).perform([request])
        return (request.results ?? []).compactMap { $0.topCandidates(1).first?.string }.joined(separator:"\n")
    }
    static func pageImage(_ page: PDFPage) throws -> CGImage {
        let rect=page.bounds(for:.mediaBox), scale=min(3,2400/max(rect.width,rect.height,1))
        guard let cg=page.thumbnail(of:NSSize(width:rect.width*scale,height:rect.height*scale),for:.mediaBox)
            .cgImage(forProposedRect:nil,context:nil,hints:nil) else { throw AppFailure.capture("页面渲染失败。") }
        return cg
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
