import Foundation

/// Availability includes every dependency; choosing a provider never makes it ready.
public struct DocumentFeatureAvailability: Sendable {
    public enum Feature: String, Sendable { case paddle, codex, semantic, office }
    public let checking: Bool
    public let runtime: Bool
    public let ocr: Bool
    public let layout: Bool
    public let embedding: Bool
    public let codexConnected: Bool

    public init(checking: Bool, runtime: Bool, ocr: Bool, layout: Bool, embedding: Bool, codexConnected: Bool) {
        self.checking = checking; self.runtime = runtime; self.ocr = ocr; self.layout = layout
        self.embedding = embedding; self.codexConnected = codexConnected
    }

    public func ready(_ feature: Feature) -> Bool {
        guard !checking else { return false }
        switch feature {
        case .paddle: return runtime && ocr && layout
        case .codex: return runtime && codexConnected
        case .semantic: return runtime && embedding
        case .office: return runtime
        }
    }
}
