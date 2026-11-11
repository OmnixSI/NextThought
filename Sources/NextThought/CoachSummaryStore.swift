import Foundation
import NextThoughtCore

/// The coach's figures for one meeting, kept as `coach.json` beside the meeting's other files.
///
/// Session folders are copied whole by backups and restore points, so the extra file travels
/// with them and is fingerprinted like the rest. The archive reads only the files it knows
/// (`meta.json`, `transcript.jsonl`, `notes.json`, ...), so it ignores this one.
enum CoachSummaryStore {
    static let filename = "coach.json"

    private static func url(for sessionID: UUID) -> URL {
        AppPaths.sessions.appendingPathComponent(sessionID.uuidString, isDirectory: true).appendingPathComponent(filename)
    }

    /// Atomic write; silent when the folder is gone, in demo mode, or when the write fails
    /// (the figures are a nicety, never a reason to disturb ending a meeting).
    @MainActor
    static func save(_ summary: CoachSummary, for sessionID: UUID) {
        guard !DemoMode.isEnabled else { return }
        let target = url(for: sessionID)
        Task.detached(priority: .utility) {
            guard FileManager.default.fileExists(atPath: target.deletingLastPathComponent().path),
                  let data = try? JSONEncoder().encode(summary) else { return }
            try? data.write(to: target, options: .atomic)
        }
    }

    static func load(_ sessionID: UUID) async -> CoachSummary? {
        let source = url(for: sessionID)
        return await Task.detached(priority: .utility) {
            guard let data = try? Data(contentsOf: source) else { return nil }
            return try? JSONDecoder().decode(CoachSummary.self, from: data)
        }.value
    }
}

extension CoachSummary {
    /// "语速 236/分 · 我说了 41% · 最长一次连续说了 2 分 10 秒 · 看镜头 78%", only the parts that exist.
    var factsLine: String? {
        var parts: [String] = []
        if let pace { parts.append(String(localized: "语速 \(Int(pace.rounded()))/分")) }
        if let talkShare { parts.append(String(localized: "我说了 \(Int((talkShare * 100).rounded()))%")) }
        if longestMonologue >= CoachSummaryAccumulator.minimumMonologue {
            let seconds = Int(longestMonologue.rounded())
            let text = seconds >= 60 ? String(localized: "\(seconds / 60) 分 \(seconds % 60) 秒") : String(localized: "\(seconds) 秒")
            parts.append(String(localized: "最长一次连续说了 \(text)"))
        }
        if let eyeContact { parts.append(String(localized: "看镜头 \(Int((eyeContact * 100).rounded()))%")) }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }
}
