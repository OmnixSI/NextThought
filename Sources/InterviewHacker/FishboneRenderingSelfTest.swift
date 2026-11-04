import AppKit
import SwiftUI
import Vision
import InterviewHackerCore

/// Render the real SwiftUI view; copying Markdown alone cannot detect a blank diagram.
@MainActor
enum FishboneRenderingSelfTest {
    // Synthetic long notes keep personal meeting contents out of the regression fixture.
    static let notes = FishboneNotes(head: "如何完善并验证复杂研究方法的实验与论证", categories:
        zip(["观测条件", "论证表达", "基线局限", "更新机制", "环境适应", "对比验证"], [3, 5, 3, 6, 6, 4]).map { name, count in
            FishboneCategory(name: name, causes: (0..<count).map { index in
                FishboneCause(text: "第\(index + 1)项：记录并核对长句说明及实验验证过程中需要保留的完整依据", at: index * 62)
            })
        })

    static func run() async throws -> String {
        let output = AppPaths.root.appendingPathComponent("fishbone-rendering")
        try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)
        for width: CGFloat in [720, 420] {
            let root = ScrollView {
                FishboneView(notes: notes, onJump: { _ in }).padding(16)
            }.frame(width: width, height: 1800).environment(\.colorScheme, .light)
            let host = NSHostingView(rootView: root)
            let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: width, height: 1800),
                                  styleMask: [.borderless], backing: .buffered, defer: false)
            window.isReleasedWhenClosed = false
            window.contentView = host
            window.orderBack(nil)
            defer { window.close() }
            try await Task.sleep(for: .milliseconds(600))
            host.layoutSubtreeIfNeeded()
            guard let bitmap = host.bitmapImageRepForCachingDisplay(in: host.bounds) else { throw SelfTest.Failure("no rendered bitmap") }
            host.cacheDisplay(in: host.bounds, to: bitmap)
            let path = output.appendingPathComponent("fishbone-\(Int(width)).png")
            try bitmap.representation(using: .png, properties: [:])?.write(to: path)
            guard let cgImage = bitmap.cgImage else { throw SelfTest.Failure("no rendered image") }
            let request = VNRecognizeTextRequest()
            request.recognitionLevel = .accurate
            request.recognitionLanguages = ["zh-Hans", "en-US"]
            try VNImageRequestHandler(cgImage: cgImage).perform([request])
            let text = (request.results ?? []).compactMap { $0.topCandidates(1).first?.string }.joined(separator: "\n")
            try text.write(to: output.appendingPathComponent("fishbone-\(Int(width)).txt"), atomically: true, encoding: .utf8)
            for category in notes.categories {
                guard text.contains(category.name) else { throw SelfTest.Failure("rendered \(Int(width))pt view is missing \(category.name); image: \(path.path)") }
            }
        }
        return "wide and narrow SwiftUI screenshots contain all six categories; no model calls"
    }
}
