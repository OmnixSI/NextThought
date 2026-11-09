import Foundation

enum AppPaths {
    static let root: URL = resolveRoot()

    static var workspaces: URL { root.appendingPathComponent("Workspaces", isDirectory: true) }
    static var sessions: URL { root.appendingPathComponent("Sessions", isDirectory: true) }
    static var sessionsPresenceMarker: URL { root.appendingPathComponent("sessions-archive-present.marker") }
    static var skills: URL { root.appendingPathComponent("Skills", isDirectory: true).appendingPathComponent("library.json") }
    static var backups: URL { root.appendingPathComponent("Backups", isDirectory: true) }
    static var models: URL { root.appendingPathComponent("models", isDirectory: true) }
    static var index: URL { root.appendingPathComponent("Index", isDirectory: true) }

    // The bound certificate and keychain were created at this established path.
    static var signing: URL {
        applicationSupport.appendingPathComponent("InterviewHacker", isDirectory: true)
            .appendingPathComponent("Signing", isDirectory: true)
    }

    static var logs: URL { root.appendingPathComponent("Logs", isDirectory: true) }
    static var legacyPresets: URL { root.appendingPathComponent("presets.json") }
    static var legacySessions: URL { root.appendingPathComponent("sessions.json") }

    private static var applicationSupport: URL {
        FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
    }

    private static func resolveRoot() -> URL {
        let environment = AppEnvironment.values
        if let path = environment["NEXTTHOUGHT_DATA_DIR"] ?? environment["INTERVIEWHACKER_DATA_DIR"], !path.isEmpty {
            return URL(fileURLWithPath: path, isDirectory: true)
        }

        let bundleID = Bundle.main.bundleIdentifier ?? "com.nextthought.app"
        if bundleID.hasPrefix("com.nextthought.app.ci") || bundleID.hasPrefix("com.interviewhacker.app.ci") {
            let reviewMode = Bundle.main.object(forInfoDictionaryKey: "NextThoughtReviewMode") as? Bool == true
                || Bundle.main.object(forInfoDictionaryKey: "InterviewHackerReviewMode") as? Bool == true
            if reviewMode {
                return Bundle.main.bundleURL.deletingLastPathComponent()
                    .appendingPathComponent(".nextthought-review", isDirectory: true)
            }
        }

        let current = applicationSupport.appendingPathComponent("NextThought", isDirectory: true)
        let legacy = applicationSupport.appendingPathComponent("InterviewHacker", isDirectory: true)
        return migrateLegacyData(from: legacy, to: current)
    }

    private static func migrateLegacyData(from legacy: URL, to current: URL) -> URL {
        let manager = FileManager.default
        guard manager.fileExists(atPath: legacy.path) else { return current }

        var movedItems: [(source: URL, destination: URL)] = []
        do {
            try manager.createDirectory(at: current, withIntermediateDirectories: true)
            try mergeDirectory(from: legacy, to: current, excluding: ["Signing"],
                               archiveRoot: current.appendingPathComponent("Previous Data", isDirectory: true), movedItems: &movedItems)
            return current
        } catch {
            for move in movedItems.reversed() where manager.fileExists(atPath: move.destination.path) && !manager.fileExists(atPath: move.source.path) {
                do {
                    try manager.createDirectory(at: move.source.deletingLastPathComponent(), withIntermediateDirectories: true)
                    try manager.moveItem(at: move.destination, to: move.source)
                } catch { }
            }
            NSLog("NextThought data migration failed; continuing with the existing data directory: %@", String(describing: error))
            return legacy
        }
    }

    private static func mergeDirectory(from source: URL, to destination: URL, excluding: Set<String>, archiveRoot: URL,
                                       movedItems: inout [(source: URL, destination: URL)]) throws {
        let manager = FileManager.default
        let items = try manager.contentsOfDirectory(at: source, includingPropertiesForKeys: [.isDirectoryKey], options: [])
        for item in items {
            guard !excluding.contains(item.lastPathComponent) else { continue }
            let target = destination.appendingPathComponent(item.lastPathComponent, isDirectory: true)
            var isDirectory: ObjCBool = false
            let sourceIsDirectory = manager.fileExists(atPath: item.path, isDirectory: &isDirectory) && isDirectory.boolValue

            if !manager.fileExists(atPath: target.path) {
                try manager.moveItem(at: item, to: target)
                movedItems.append((item, target))
            } else if sourceIsDirectory {
                var targetIsDirectory: ObjCBool = false
                guard manager.fileExists(atPath: target.path, isDirectory: &targetIsDirectory), targetIsDirectory.boolValue else {
                    try preserveLegacyItem(item, relativeTo: source, archiveRoot: archiveRoot, movedItems: &movedItems)
                    continue
                }
                try mergeDirectory(from: item, to: target, excluding: [],
                                   archiveRoot: archiveRoot.appendingPathComponent(item.lastPathComponent, isDirectory: true), movedItems: &movedItems)
                if (try? manager.contentsOfDirectory(atPath: item.path).isEmpty) == true {
                    try manager.removeItem(at: item)
                }
            } else {
                try preserveLegacyItem(item, relativeTo: source, archiveRoot: archiveRoot, movedItems: &movedItems)
            }
        }
    }

    private static func preserveLegacyItem(_ item: URL, relativeTo source: URL, archiveRoot: URL,
                                           movedItems: inout [(source: URL, destination: URL)]) throws {
        let relativePath = String(item.path.dropFirst(source.path.count + 1))
        let archive = archiveRoot.appendingPathComponent(relativePath, isDirectory: false)
        try FileManager.default.createDirectory(at: archive.deletingLastPathComponent(), withIntermediateDirectories: true)
        if FileManager.default.fileExists(atPath: archive.path) {
            let alternate = archive.deletingLastPathComponent()
                .appendingPathComponent("\(archive.lastPathComponent)-\(UUID().uuidString)", isDirectory: false)
            try FileManager.default.moveItem(at: item, to: alternate)
            movedItems.append((item, alternate))
        } else {
            try FileManager.default.moveItem(at: item, to: archive)
            movedItems.append((item, archive))
        }
    }
}
