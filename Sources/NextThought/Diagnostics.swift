import Foundation

enum Diagnostics {
    static let launchID = UUID().uuidString
    static let directory = AppPaths.root.appendingPathComponent("diagnostics", isDirectory: true)
    private static let queue = DispatchQueue(label: "app.nextthought.diagnostics")
    private static let eventsURL = directory.appendingPathComponent("events-\(launchID).jsonl")

    static func record(_ event: String, _ fields: [String: Any] = [:]) {
        var data = fields
        data["event"] = event
        data["launch_id"] = launchID
        data["timestamp"] = Date().timeIntervalSince1970
        data["uptime"] = ProcessInfo.processInfo.systemUptime
        guard let json = try? JSONSerialization.data(withJSONObject: data, options: [.sortedKeys]) else { return }
        queue.async {
            do {
                try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
                if !FileManager.default.fileExists(atPath: eventsURL.path) {
                    FileManager.default.createFile(atPath: eventsURL.path, contents: nil)
                }
                let handle = try FileHandle(forWritingTo: eventsURL)
                defer { try? handle.close() }
                try handle.seekToEnd()
                try handle.write(contentsOf: json)
                try handle.write(contentsOf: Data([10]))
            } catch { }
        }
    }

    /// Keeps the 10 most recent launch logs; older diagnostics are deleted.
    static func pruneOldLogs() {
        queue.async {
            let files = (try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: [.contentModificationDateKey])) ?? []
            let sorted = files.sorted {
                let a = (try? $0.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate) ?? .distantPast
                let b = (try? $1.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate) ?? .distantPast
                return a > b
            }
            for file in sorted.dropFirst(10) { try? FileManager.default.removeItem(at: file) }
        }
    }

    static func runtimeLogURL() -> URL {
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory.appendingPathComponent("asr-\(launchID).log")
    }
}
