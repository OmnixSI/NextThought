import AppKit
import CoreGraphics
import ImageIO
import ScreenCaptureKit
import Vision

enum ScreenshotKind: Equatable {
    case window
    case region
}

@MainActor
enum ScreenshotService {
    static func availableWindows() throws -> [ScreenshotWindow] {
        guard let rawWindows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] else {
            throw AppFailure.screenshot("无法读取当前桌面窗口。请检查屏幕录制权限后重试。")
        }
        let ownPID = NSRunningApplication.current.processIdentifier
        return rawWindows.compactMap { info in
            guard let number = (info[kCGWindowNumber as String] as? NSNumber)?.uint32Value,
                  let ownerPID = (info[kCGWindowOwnerPID as String] as? NSNumber)?.int32Value,
                  ownerPID != ownPID,
                  (info[kCGWindowLayer as String] as? NSNumber)?.intValue == 0 else { return nil }
            let owner = info[kCGWindowOwnerName as String] as? String ?? "未知应用"
            let title = (info[kCGWindowName as String] as? String)?.trimmingCharacters(in: .whitespacesAndNewlines)
            return ScreenshotWindow(id: number, title: title.flatMap { $0.isEmpty ? nil : $0 } ?? "未命名窗口", owner: owner)
        }
    }

    static func availableDisplays() async throws -> [ScreenshotDisplay] {
        let content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: true)
        return content.displays.enumerated().map { index, display in
            ScreenshotDisplay(
                id: display.displayID,
                title: NSScreen.screens.first(where: { screen in
                    (screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber)?.uint32Value == display.displayID
                })?.localizedName ?? "显示器 \(index + 1)",
                width: display.width,
                height: display.height)
        }
    }

    static func captureDisplay(_ displayID: UInt32, hidingAppWindows: Bool = true) async throws -> NSImage {
        let content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: true)
        guard let display = content.displays.first(where: { $0.displayID == displayID }) else {
            throw AppFailure.screenshot("所选显示器已不可用。请刷新显示器列表后重试。")
        }
        if hidingAppWindows { await MainActor.run { WindowCoordinator.hide() } }
        defer { if hidingAppWindows { WindowCoordinator.show() } }
        if hidingAppWindows { try? await Task.sleep(nanoseconds: 250_000_000) }

        let filter = SCContentFilter(display: display, excludingWindows: [])
        let configuration = SCStreamConfiguration()
        let scale = NSScreen.screens.first(where: { screen in
            (screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber)?.uint32Value == displayID
        })?.backingScaleFactor ?? 1
        configuration.width = max(1, Int(CGFloat(display.width) * scale))
        configuration.height = max(1, Int(CGFloat(display.height) * scale))
        configuration.showsCursor = false
        let image = try await SCScreenshotManager.captureImage(contentFilter: filter, configuration: configuration)
        return NSImage(cgImage: image, size: NSSize(width: image.width, height: image.height))
    }

    /// The main display without this app's own windows (assistant, teleprompter): for
    /// reading the screen in the background, so nothing on screen has to be hidden.
    static func captureForReading(targetID: String = "", region: [Double]? = nil, maxWidth: Int = 2400) async throws -> CGImage {
        let content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: true)
        let filter: SCContentFilter
        let width: Double, height: Double
        if targetID.hasPrefix("window:"), let id = UInt32(targetID.dropFirst(7)) {
            guard let window = content.windows.first(where: { $0.windowID == id }),
                  window.owningApplication?.processID != NSRunningApplication.current.processIdentifier else { throw AppFailure.screenshot("选定演示窗口已关闭或不可用，请重新选择。") }
            filter = SCContentFilter(desktopIndependentWindow: window)
            width = window.frame.width; height = window.frame.height
        } else {
            let requested = targetID.hasPrefix("display:") ? UInt32(targetID.dropFirst(8)) : nil
            let mainID = (NSScreen.main?.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber)?.uint32Value
            guard let display = content.displays.first(where: { $0.displayID == (requested ?? mainID) }) ?? (requested == nil ? content.displays.first : nil) else { throw AppFailure.screenshot("选定显示器已断开，请重新选择。") }
            let own = content.applications.filter { $0.processID == NSRunningApplication.current.processIdentifier }
            filter = SCContentFilter(display: display, excludingApplications: own, exceptingWindows: [])
            width = Double(display.width); height = Double(display.height)
        }
        let scale = min(2, Double(maxWidth) / max(1, width))
        let configuration = SCStreamConfiguration()
        configuration.width = max(1, Int(width * scale)); configuration.height = max(1, Int(height * scale))
        configuration.showsCursor = false
        let image = try await SCScreenshotManager.captureImage(contentFilter: filter, configuration: configuration)
        if let region, region.count == 4 {
            let rect = CGRect(x: region[0], y: region[1], width: region[2], height: region[3]).standardized.intersection(CGRect(x: 0, y: 0, width: 1, height: 1))
            guard rect.width >= 0.015, rect.height >= 0.015,
                  let cropped = image.cropping(to: CGRect(x: rect.minX * Double(image.width), y: rect.minY * Double(image.height), width: rect.width * Double(image.width), height: rect.height * Double(image.height)).integral) else { throw AppFailure.screenshot("屏幕选区无效，请重新框选。") }
            return cropped
        }
        return image
    }

    /// On-device text recognition (Chinese + English), off the main thread.
    nonisolated static func recognizeText(_ image: CGImage) async throws -> String {
        try await Task.detached(priority: .utility) {
            let request = VNRecognizeTextRequest()
            request.recognitionLevel = .accurate
            request.recognitionLanguages = ["zh-Hans", "en-US"]
            request.usesLanguageCorrection = false
            try VNImageRequestHandler(cgImage: image, options: [:]).perform([request])
            return (request.results ?? []).compactMap { $0.topCandidates(1).first?.string }.joined(separator: "\n")
        }.value
    }

    /// A frame as a PNG file for the screenshot-answer flow.
    static func writePNG(_ image: CGImage) throws -> (url: URL, preview: NSImage) {
        let url = FileManager.default.temporaryDirectory.appendingPathComponent("nextthought-\(UUID().uuidString).png")
        guard let data = NSBitmapImageRep(cgImage: image).representation(using: .png, properties: [:]) else {
            throw AppFailure.screenshot("无法保存屏幕截图。")
        }
        try data.write(to: url, options: .atomic)
        return (url, NSImage(cgImage: image, size: NSSize(width: image.width, height: image.height)))
    }

    static func crop(image: NSImage, normalizedRect: CGRect) throws -> (url: URL, preview: NSImage) {
        guard let source = image.cgImage(forProposedRect: nil, context: nil, hints: nil) else {
            throw AppFailure.screenshot("无法读取显示器截图。")
        }
        let unitRect = normalizedRect.standardized.intersection(CGRect(x: 0, y: 0, width: 1, height: 1))
        guard unitRect.width >= 0.015, unitRect.height >= 0.015 else {
            throw AppFailure.screenshot("选取范围太小，请拖动选择完整题目或内容。")
        }
        let pixelRect = CGRect(
            x: unitRect.minX * CGFloat(source.width),
            y: unitRect.minY * CGFloat(source.height),
            width: unitRect.width * CGFloat(source.width),
            height: unitRect.height * CGFloat(source.height)).integral
        guard let cropped = source.cropping(to: pixelRect) else {
            throw AppFailure.screenshot("无法裁切所选区域，请重新选择。")
        }
        let url = FileManager.default.temporaryDirectory.appendingPathComponent("nextthought-\(UUID().uuidString).png")
        let bitmap = NSBitmapImageRep(cgImage: cropped)
        guard let data = bitmap.representation(using: .png, properties: [:]) else {
            throw AppFailure.screenshot("无法保存所选截图区域。")
        }
        try data.write(to: url, options: .atomic)
        return (url, NSImage(cgImage: cropped, size: NSSize(width: cropped.width, height: cropped.height)))
    }

    static func captureWindow(_ window: ScreenshotWindow) async throws -> (url: URL, preview: NSImage)? {
        await MainActor.run { WindowCoordinator.hide() }
        defer { WindowCoordinator.show() }
        try? await Task.sleep(nanoseconds: 300_000_000)

        let url = FileManager.default.temporaryDirectory.appendingPathComponent("nextthought-\(UUID().uuidString).png")
        let status = await runScreencapture(arguments: ["-x", "-o", "-l", String(window.id), url.path])
        guard status == 0, let data = try? Data(contentsOf: url), let image = NSImage(data: data) else {
            try? FileManager.default.removeItem(at: url)
            throw AppFailure.screenshot("窗口“\(window.title)”已关闭或无法捕获。请刷新窗口列表后重试。")
        }
        return (url, image)
    }

    private static func runScreencapture(arguments: [String]) async -> Int32 {
        await Task.detached(priority: .userInitiated) { () -> Int32 in
            let process = Process()
            process.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
            process.arguments = arguments
            do {
                try process.run()
                process.waitUntilExit()
                return process.terminationStatus
            } catch {
                return -1
            }
        }.value
    }

    static func optimizedImageURL(from sourceURL: URL) throws -> URL {
        let data = try Data(contentsOf: sourceURL)
        guard let source = CGImageSourceCreateWithData(data as CFData, nil) else {
            throw AppFailure.screenshot("无法读取截图文件。")
        }
        let options = [
            kCGImageSourceCreateThumbnailFromImageAlways: true,
            kCGImageSourceThumbnailMaxPixelSize: 2_400,
            kCGImageSourceCreateThumbnailWithTransform: true
        ] as CFDictionary
        guard let image = CGImageSourceCreateThumbnailAtIndex(source, 0, options) else {
            throw AppFailure.screenshot("无法处理截图图像。")
        }
        let bitmap = NSBitmapImageRep(cgImage: image)
        guard let jpeg = bitmap.representation(using: .jpeg, properties: [.compressionFactor: 0.9]) else {
            throw AppFailure.screenshot("无法压缩截图。")
        }
        let result = FileManager.default.temporaryDirectory.appendingPathComponent("nextthought-\(UUID().uuidString).jpg")
        try jpeg.write(to: result, options: .atomic)
        return result
    }
}

@MainActor
enum WindowCoordinator {
    private static var hiddenWindows: [NSWindow] = []

    static func hide() {
        hiddenWindows = NSApp.windows.filter(\.isVisible)
        hiddenWindows.forEach { $0.orderOut(nil) }
    }

    static func show() {
        let windows = hiddenWindows
        hiddenWindows.removeAll()
        NSApp.activate(ignoringOtherApps: true)
        windows.forEach { $0.orderFrontRegardless() }
    }
}
