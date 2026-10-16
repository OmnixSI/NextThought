import Foundation

/// Every file InterviewHacker keeps lives under one root:
/// `~/Library/Application Support/InterviewHacker` — workspaces, sessions, backups,
/// models, signing identity and logs. Updating or re-signing the app never touches it.
/// `INTERVIEWHACKER_DATA_DIR` redirects everything (self-tests, demo screenshots).
enum AppPaths {
    static let root: URL = {
        if let custom = ProcessInfo.processInfo.environment["INTERVIEWHACKER_DATA_DIR"], !custom.isEmpty {
            return URL(fileURLWithPath: custom, isDirectory: true)
        }
        return FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("InterviewHacker", isDirectory: true)
    }()

    static var workspaces: URL { root.appendingPathComponent("Workspaces", isDirectory: true) }
    static var sessions: URL { root.appendingPathComponent("Sessions", isDirectory: true) }
    /// Skill library (imported SKILL.md files and their settings).
    static var skills: URL { root.appendingPathComponent("Skills", isDirectory: true).appendingPathComponent("library.json") }
    static var backups: URL { root.appendingPathComponent("Backups", isDirectory: true) }
    static var models: URL { root.appendingPathComponent("models", isDirectory: true) }
    static var signing: URL { root.appendingPathComponent("Signing", isDirectory: true) }
    static var logs: URL { root.appendingPathComponent("Logs", isDirectory: true) }
    static var legacyPresets: URL { root.appendingPathComponent("presets.json") }
    static var legacySessions: URL { root.appendingPathComponent("sessions.json") }
}
