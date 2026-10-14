import Foundation

public enum TranscriptContextBuilder {
    public static func recent(_ entries: [(date: Date, text: String)], now: Date = Date()) -> String {
        let cutoff = now.addingTimeInterval(-300)
        return entries.filter { $0.date > cutoff }.map(\.text).joined(separator: "\n")
    }
}
