import CryptoKit
import Foundation

/// Only explicitly pinned data assets can be reused; filenames alone never establish compatibility.
public enum LocalModelReuse {
    public struct File: Sendable {
        public let path: String
        public let bytes: Int64
        public let sha256: String
        public init(path: String, bytes: Int64, sha256: String) {
            self.path = path; self.bytes = bytes; self.sha256 = sha256
        }
    }
    public struct Search: Sendable {
        public var directories: [URL] = []
        public var limited = false
        public var inaccessible = 0
        public init() {}
    }
    public struct Result: Sendable {
        public var reusedFiles = 0
        public var reusedBytes: Int64 = 0
        public init() {}
    }

    /// Enumerates names and metadata only, including hidden model caches. Symlink directories are
    /// not followed; symlinked HF snapshot *files* are supported when validating a candidate.
    public static func search(roots: [URL], indexedFiles: [URL] = [], names: Set<String>,
                              maximumEntries: Int = 300_000, seconds: TimeInterval = 20) throws -> Search {
        var result = Search(), seen = Set<String>(), visitedDirectories = Set<String>(), count = 0
        let deadline = Date().addingTimeInterval(seconds)
        func add(_ directory: URL) {
            let url = directory.standardizedFileURL
            if seen.insert(url.path).inserted { result.directories.append(url) }
        }
        for file in indexedFiles where names.contains(file.lastPathComponent) {
            add((try? file.resourceValues(forKeys: [.isDirectoryKey]).isDirectory) == true ? file : file.deletingLastPathComponent())
        }
        let skipped: Set<String> = [".git", ".Trash", "node_modules", ".build", ".venv", "venv", "site-packages", "DerivedData", "Library"]
        let fm = FileManager.default
        for root in roots {
            try Task.checkCancellation()
            guard visitedDirectories.insert(root.standardizedFileURL.path).inserted else { continue }
            // A selected root may itself be a model directory or an unindexed mount.
            add(root)
            guard let walker = fm.enumerator(at: root, includingPropertiesForKeys: [.isDirectoryKey, .isSymbolicLinkKey],
                                             options: [.skipsPackageDescendants], errorHandler: { _, _ in result.inaccessible += 1; return true }) else {
                result.inaccessible += 1; continue
            }
            for case let file as URL in walker {
                try Task.checkCancellation()
                count += 1
                if count > maximumEntries || Date() > deadline { result.limited = true; return result }
                let info = try? file.resourceValues(forKeys: [.isDirectoryKey, .isSymbolicLinkKey])
                if info?.isDirectory == true {
                    if names.contains(file.lastPathComponent) { add(file) }
                    if skipped.contains(file.lastPathComponent) || info?.isSymbolicLink == true || !visitedDirectories.insert(file.standardizedFileURL.path).inserted { walker.skipDescendants() }
                } else if names.contains(file.lastPathComponent) { add(file.deletingLastPathComponent()) }
            }
        }
        return result
    }

    public static func matches(_ url: URL, file: File) throws -> Bool {
        try Task.checkCancellation()
        let resolved = url.resolvingSymlinksInPath()
        guard (try? resolved.resourceValues(forKeys: [.fileSizeKey, .isRegularFileKey]).isRegularFile) == true,
              (try? resolved.resourceValues(forKeys: [.fileSizeKey]).fileSize) == Int(file.bytes),
              let handle = try? FileHandle(forReadingFrom: resolved) else { return false }
        defer { try? handle.close() }
        var hash = SHA256()
        do {
            while let data = try handle.read(upToCount: 4 * 1024 * 1024), !data.isEmpty {
                try Task.checkCancellation(); hash.update(data: data)
            }
        } catch is CancellationError { throw CancellationError() } catch { return false }
        return hash.finalize().map { String(format: "%02x", $0) }.joined() == file.sha256
    }

    /// Imports matching files independently, so an incomplete snapshot still saves downloading its
    /// intact assets. Originals remain untouched. Every staged copy is verified before atomic commit.
    public static func reuse(_ files: [File], from directories: [URL], into destination: URL) throws -> Result {
        let fm = FileManager.default
        var result = Result()
        for file in files {
            guard !file.path.hasPrefix("/"), !file.path.split(separator: "/").contains("..") else {
                throw CocoaError(.fileReadInvalidFileName)
            }
            try Task.checkCancellation()
            let target = destination.appendingPathComponent(file.path)
            if try matches(target, file: file) { continue }
            var seen = Set<String>()
            for directory in directories {
                let candidate = directory.appendingPathComponent(file.path)
                let resolved = candidate.resolvingSymlinksInPath().path
                guard resolved != target.resolvingSymlinksInPath().path, seen.insert(resolved).inserted else { continue }
                guard try matches(candidate, file: file) else { continue }
                try fm.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
                let stage = target.deletingLastPathComponent().appendingPathComponent(".reuse-" + UUID().uuidString)
                defer { try? fm.removeItem(at: stage) }
                try fm.copyItem(at: URL(fileURLWithPath: resolved), to: stage)
                guard try matches(stage, file: file) else { throw CocoaError(.fileReadCorruptFile) }
                try Task.checkCancellation()
                if fm.fileExists(atPath: target.path) { _ = try fm.replaceItemAt(target, withItemAt: stage) }
                else { try fm.moveItem(at: stage, to: target) }
                result.reusedFiles += 1; result.reusedBytes += file.bytes
                break
            }
        }
        return result
    }
}
