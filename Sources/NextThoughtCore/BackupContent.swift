import Foundation

public enum BackupImportStatus: String, Sendable { case new, identical, conflict }

public enum BackupContent {
    /// Dates in a ZIP are not edit revisions; compare the actual relative file contents.
    public static func fingerprint(_ directory: URL) throws -> String {
        let fm = FileManager.default
        guard fm.fileExists(atPath: directory.path) else { throw CocoaError(.fileNoSuchFile) }
        var failure: Error?
        guard let walker = fm.enumerator(at: directory, includingPropertiesForKeys: [.isRegularFileKey, .isSymbolicLinkKey],
            errorHandler: { _, error in failure = error; return false }) else { throw CocoaError(.fileReadUnknown) }
        // The enumerator hands back resolved paths (/private/var/…) even when the
        // directory was given through a symlink (/var/…); measure the relative path
        // against the resolved root, or the same files fingerprint differently.
        let root = directory.resolvingSymlinksInPath().path
        var entries: [String] = []
        for case let file as URL in walker {
            try Task.checkCancellation()
            if file.lastPathComponent.hasPrefix("._") || file.lastPathComponent == ".DS_Store" { continue }
            let values = try file.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey])
            guard values.isSymbolicLink != true else { throw CocoaError(.fileReadUnsupportedScheme) }
            guard values.isRegularFile == true else { continue }
            let resolved = file.resolvingSymlinksInPath().path
            guard resolved.hasPrefix(root + "/") else { throw CocoaError(.fileReadUnsupportedScheme) }
            let path = String(resolved.dropFirst(root.count + 1))
            entries.append(path + "\u{0}" + IncrementalBackupStore.digest(try Data(contentsOf: file, options: .mappedIfSafe)))
        }
        if let failure { throw failure }
        return IncrementalBackupStore.digest(Data(entries.sorted().joined(separator: "\n").utf8))
    }
    public static func status(local: String?, incoming: String) -> BackupImportStatus {
        guard let local else { return .new }
        return local == incoming ? .identical : .conflict
    }
}
