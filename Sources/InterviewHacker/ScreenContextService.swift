import AppKit
import CryptoKit
import Foundation
import InterviewHackerCore
import Observation
import Vision

struct QuestionScreenContext {
    var context: ScreenContext? = nil
    var image: URL? = nil
    var note: String? = nil
    var slide: ContextExcerpt? = nil
}

@MainActor @Observable final class ScreenContextService {
    typealias Capture = @MainActor (String, [Double]?) async throws -> CGImage
    @ObservationIgnored private let capture: Capture
    init(capture: @escaping Capture = { target, region in try await ScreenshotService.captureForReading(targetID: target, region: region) }) { self.capture = capture }
    private(set) var latest: ScreenContext?
    private(set) var status = "尚未读取屏幕"
    private(set) var error: String?
    @ObservationIgnored private var image: CGImage?
    @ObservationIgnored private var previousPreview: [UInt8]?
    @ObservationIgnored private var candidate = ""
    @ObservationIgnored private var candidateSince = Date.distantPast
    @ObservationIgnored private var targetKey = ""
    @ObservationIgnored private var generation = UUID()
    @ObservationIgnored private var reading: Task<ScreenContext?, Error>?
    @ObservationIgnored private var enhancing: Task<Void, Never>?

    func invalidate() {
        generation = UUID(); reading?.cancel(); reading = nil; enhancing?.cancel(); enhancing = nil
        latest = nil; image = nil; candidate = ""; previousPreview = nil; error = nil; status = "等待新画面"
    }
    func refresh(targetID: String, source: String, region: [Double]?, force: Bool = false,
                 localModelID: String? = nil) async throws -> ScreenContext? {
        let key = targetID + (region?.description ?? "")
        if targetKey != key { invalidate(); targetKey = key }
        if let reading { return try await reading.value }
        let epoch = generation
        let task = Task { [weak self] () throws -> ScreenContext? in
            guard let self else { return nil }
            let frame = try await capture(targetID, region)
            try Task.checkCancellation()
            guard epoch == generation else { throw CancellationError() }
            let now = Date()
            let bytes = frame.dataProvider?.data as Data? ?? Data()
            var hasher = SHA256(); hasher.update(data: Data("\(frame.width)x\(frame.height):\(frame.bytesPerRow)".utf8)); hasher.update(data: bytes)
            let hash = hasher.finalize().map { String(format: "%02x", $0) }.joined()
            if latest?.frameID == hash {
                latest?.capturedAt = now; image = frame; error = nil
                return latest
            }
            if candidate != hash {
                candidate = hash; candidateSince = now
                latest = nil; image = nil; enhancing?.cancel(); enhancing = nil
                status = "画面已变化，等待稳定…"
            }
            guard force || now.timeIntervalSince(candidateSince) >= 0.5 else { return nil }
            status = "正在识别当前画面…"
            let preview = Self.preview(frame)
            let changed = preview.flatMap { ScreenDelta.region(previous: previousPreview, current: $0, width: 160, height: 90) }
            let regions = try await Self.recognize(frame)
            try Task.checkCancellation()
            guard epoch == generation, candidate == hash else { throw CancellationError() }
            let context = ScreenContext(frameID: hash, targetID: targetID, source: source, capturedAt: now, regions: regions, captureRegion: region)
            latest = context; image = frame; error = nil; previousPreview = preview
            status = regions.isEmpty ? "当前画面未识别到文字，可附图分析" : "已读取当前画面"
            if let localModelID, let changed, context.lowConfidence || Self.complex(context.text) {
                enhance(frame, context: context, changed: changed, modelID: localModelID, epoch: epoch)
            }
            return context
        }
        reading = task
        defer { if epoch == generation { reading = nil } }
        do { return try await withTaskCancellationHandler { try await task.value } onCancel: { task.cancel() } }
        catch is CancellationError { throw CancellationError() }
        catch { if epoch == generation { latest = nil; image = nil; self.error = error.localizedDescription; status = "屏幕读取失败" }; throw error }
    }
    static func complex(_ text: String) -> Bool {
        let numbers = (try? NSRegularExpression(pattern: #"\d+(?:[.,]\d+)*%?"#).numberOfMatches(in: text, range: NSRange(location: 0, length: (text as NSString).length))) ?? 0
        return numbers >= 6 || text.contains("\t") || text.range(of: #"[∑Σ∫∇√≤≥≠θη]|(?:\d+(?:\.\d+)?\s+){3}"#, options: .regularExpression) != nil
    }
    nonisolated static func recognize(_ image: CGImage) async throws -> [ScreenTextRegion] {
        let task = Task.detached(priority: .userInitiated) { () throws -> [ScreenTextRegion] in
            try Task.checkCancellation()
            let request = VNRecognizeTextRequest(); request.recognitionLevel = .accurate
            request.recognitionLanguages = ["zh-Hans", "en-US"]; request.usesLanguageCorrection = false
            try VNImageRequestHandler(cgImage: image, options: [:]).perform([request])
            return (request.results ?? []).compactMap { observation in
                guard let text = observation.topCandidates(1).first else { return nil }
                let box = observation.boundingBox
                return .init(text: text.string, bounds: [box.minX, 1-box.maxY, box.width, box.height], confidence: Double(text.confidence))
            }
        }
        return try await withTaskCancellationHandler { try await task.value } onCancel: { task.cancel() }
    }
    nonisolated static func preview(_ frame: CGImage) -> [UInt8]? {
        var pixels = [UInt8](repeating: 0, count: 160 * 90 * 4)
        let success = pixels.withUnsafeMutableBytes { bytes -> Bool in
            guard let c = CGContext(data: bytes.baseAddress, width: 160, height: 90, bitsPerComponent: 8, bytesPerRow: 160 * 4,
                space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue | CGBitmapInfo.byteOrder32Big.rawValue) else { return false }
            c.interpolationQuality = .low; c.draw(frame, in: CGRect(x: 0,y: 0,width: 160,height: 90)); return true
        }
        return success ? pixels : nil
    }
    private func enhance(_ frame: CGImage, context: ScreenContext, changed: [Double], modelID: String, epoch: UUID) {
        guard let model = ModelCatalog.descriptor(for: modelID), ManagedModelStore.isInstalled(model), ManagedModelStore.isInstalled(ModelCatalog.layout) else { return }
        status = "已读取文字，正在本机增强识别…"
        enhancing = Task { [weak self] in
            guard let self else { return }
            let directory = DocumentRuntime.temporary.appendingPathComponent("screen-" + UUID().uuidString)
            defer { try? FileManager.default.removeItem(at: directory) }
            do {
                try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
                let file = directory.appendingPathComponent("frame.png")
                let box = CGRect(x: changed[0] * Double(frame.width), y: changed[1] * Double(frame.height), width: changed[2] * Double(frame.width), height: changed[3] * Double(frame.height)).integral
                guard let crop = frame.cropping(to: box) else { throw AppFailure.screenshot("无法定位变化区域。") }
                try await Task.detached(priority: .utility) {
                    guard let data = NSBitmapImageRep(cgImage: crop).representation(using: .png, properties: [:]) else { throw AppFailure.screenshot("无法编码屏幕变化区域。") }
                    try data.write(to: file)
                }.value
                let result = try await DocumentWorker.realtime.request("ocr", ["image": file.path,
                    "model_directory": model.directory.path, "layout_directory": ModelCatalog.layout.directory.path], timeout: 60)
                try Task.checkCancellation()
                guard generation == epoch, latest?.frameID == context.frameID else { return }
                if let text = result["markdown"] as? String, !text.isEmpty {
                    if changed == [0,0,1,1] { latest?.enhancedText = text }
                    else {
                        // Keep the system evidence outside the changed crop; the
                        // model cannot replace text it was never shown.
                        let area = CGRect(x: changed[0], y: changed[1], width: changed[2], height: changed[3])
                        let outside = context.regions.filter { r in
                            let b = r.bounds; return b.count == 4 && !area.intersects(CGRect(x: b[0], y: b[1], width: b[2], height: b[3]))
                        }.map(\.text).joined(separator: "\n")
                        latest?.enhancedText = outside + "\n\n[变化区域 " + changed.map { String(format: "%.3f",$0) }.joined(separator: ",") + "]\n" + text
                    }
                    latest?.enhancedRegion = changed; latest?.recognizer = model.name; latest?.recognizedAt = Date()
                    status = "本机增强识别已完成"
                }
            } catch is CancellationError { }
            catch { if generation == epoch, latest?.frameID == context.frameID { status = "增强未完成，保留系统识别；必要时附图分析" } }
        }
    }
    /// Copies the currently pinned frame. Later screen changes cannot change the
    /// image already attached to an answer request.
    func questionImage(frameID: String) throws -> URL? {
        guard latest?.frameID == frameID, let image else { return nil }
        let directory = AppPaths.root.appendingPathComponent("Temporary/Screen", isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let url = directory.appendingPathComponent(UUID().uuidString + ".jpg")
        guard let data = NSBitmapImageRep(cgImage: image).representation(using: .jpeg, properties: [.compressionFactor: 0.92]) else { throw AppFailure.screenshot("无法附加当前画面。") }
        try data.write(to: url, options: .atomic)
        return url
    }
}
