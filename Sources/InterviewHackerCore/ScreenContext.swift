import Foundation

public struct ScreenTextRegion: Codable, Sendable, Equatable {
    public var text: String
    /// Normalized top-left x/y/width/height, independent of Retina scale.
    public var bounds: [Double]
    public var confidence: Double
    public init(text: String, bounds: [Double], confidence: Double) { self.text = text; self.bounds = bounds; self.confidence = confidence }
}
public struct ScreenContext: Codable, Sendable, Equatable {
    public var frameID: String
    public var targetID: String
    public var source: String
    public var capturedAt: Date
    public var recognizedAt: Date
    public var regions: [ScreenTextRegion]
    public var captureRegion: [Double]?
    public var enhancedText: String?
    public var enhancedRegion: [Double]?
    public var recognizer: String
    public init(frameID: String, targetID: String, source: String, capturedAt: Date = Date(), regions: [ScreenTextRegion], captureRegion: [Double]? = nil, recognizer: String = "system") {
        self.frameID = frameID; self.targetID = targetID; self.source = source; self.capturedAt = capturedAt
        recognizedAt = Date(); self.regions = regions; self.captureRegion = captureRegion; self.recognizer = recognizer
    }
    public var text: String { enhancedText ?? regions.map(\.text).joined(separator: "\n") }
    public var lowConfidence: Bool { regions.isEmpty || regions.contains { $0.confidence < 0.8 } }
    public func isFresh(now: Date = Date(), maximumAge: Double = 2) -> Bool {
        let age = now.timeIntervalSince(capturedAt)
        return age >= 0 && age <= maximumAge
    }
}
public enum ScreenReference {
    public static func isReference(_ question: String) -> Bool {
        let q = question.lowercased()
        return ["这一页", "这页", "这个图", "这张图", "这个表", "这张表", "这个公式", "这个地方", "这里", "这部分", "屏幕", "左边", "右边", "上面这个", "下面这个", "这段代码", "这个文件", "this slide", "this page", "this figure", "this chart", "this table", "this equation", "on screen", "on the screen", "left side", "right side", "this code", "this part"].contains { q.contains($0) }
    }
    public static func needsImage(_ question: String) -> Bool {
        let q = question.lowercased()
        return ["图", "表", "公式", "这里", "这个地方", "左边", "右边", "位置", "箭头", "颜色", "figure", "chart", "table", "equation", "diagram", "left", "right", "here"].contains { q.contains($0) }
    }
    public static func isRelated(_ question: String, context: ScreenContext) -> Bool {
        isReference(question) || ScreenText.heuristicRelevance(screen: context.text, recentSpeech: question)
    }
}
