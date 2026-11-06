import AppKit
import Foundation

/// Archives the Markdown of every imported material on disk.
///
/// The conversion happens in memory, but the Markdown is written next to the
/// other app data as well: a material is then a plain `.md` file that can be
/// opened, checked, diffed or re-imported later, and the scenario folder shows
/// exactly what the model was given. This happens silently — importing a file
/// never opens a window or asks anything.
enum MaterialArchive {
    static func resolve(_ path: String) -> URL? {
        let root = AppPaths.root.resolvingSymlinksInPath()
        let url = (path.hasPrefix("/") ? URL(fileURLWithPath: path) : root.appendingPathComponent(path)).resolvingSymlinksInPath()
        guard url.path.hasPrefix(root.appendingPathComponent("Materials").path + "/") else { return nil }
        return url
    }
    static func relative(_ url: URL) -> String { String(url.path.dropFirst(AppPaths.root.path.count + 1)) }
    /// The folder holding the archived Markdown of one scenario.
    static func folder(for workspaceID: UUID) -> URL {
        AppPaths.root
            .appendingPathComponent("Materials", isDirectory: true)
            .appendingPathComponent(workspaceID.uuidString, isDirectory: true)
    }

    static func deleteWorkspaceFiles(_ workspaceID: UUID) throws {
        let directory = folder(for: workspaceID)
        if FileManager.default.fileExists(atPath: directory.path) {
            try FileManager.default.removeItem(at: directory)
        }
    }

    /// Writes the Markdown and returns the file URL. Existing files with the
    /// same name are replaced, so re-importing a file refreshes it.
    @discardableResult
    static func archive(_ markdown: String, name: String, format: String, workspaceID: UUID) throws -> URL {
        let directory = folder(for: workspaceID)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let url = uniqueURL(in: directory, name: name)
        let header = """
        ---
        source-format: \(format)
        archived-at: \(ISO8601DateFormatter().string(from: Date()))
        ---

        """
        try (header + markdown).write(to: url, atomically: true, encoding: .utf8)
        return url
    }

    /// A file name that is safe on disk and does not overwrite a sibling.
    private static func uniqueURL(in directory: URL, name: String) -> URL {
        let base = sanitize(name)
        var url = directory.appendingPathComponent(base).appendingPathExtension("md")
        var suffix = 2
        while FileManager.default.fileExists(atPath: url.path) {
            url = directory.appendingPathComponent("\(base) (\(suffix))").appendingPathExtension("md")
            suffix += 1
        }
        return url
    }

    /// Removes the characters that a file name cannot hold.
    static func sanitize(_ name: String) -> String {
        let illegal = CharacterSet(charactersIn: "/\\:\0")
        let cleaned = name.components(separatedBy: illegal).joined(separator: "-")
        let trimmed = cleaned.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? "material" : String(trimmed.prefix(120))
    }
}
