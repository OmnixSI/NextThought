import Foundation
import CryptoKit

public struct RestorePoint: Codable, Sendable, Equatable {
    public var version = 1
    public var createdAt: Date
    public var reason: String
    public var fingerprint: String
    public var files: [String: String]
}

/// Content-addressed recovery storage. A published manifest is a commit: objects
/// are durable and verified before it appears, and pruning only follows success.
public actor IncrementalBackupStore {
    public let root: URL
    public init(root: URL) { self.root = root.standardizedFileURL.resolvingSymlinksInPath() }
    private var objects: URL { root.appendingPathComponent("Objects", isDirectory: true) }
    private var points: URL { root.appendingPathComponent("RestorePoints", isDirectory: true) }
    public static func digest(_ data: Data) -> String { SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined() }
    public static func safePath(_ path: String) -> Bool {
        !path.isEmpty && !path.hasPrefix("/") && !path.contains("\\") && path.split(separator: "/", omittingEmptySubsequences: false).allSatisfy { !$0.isEmpty && $0 != "." && $0 != ".." }
    }
    public func create(files: [String: Data], directories: [String: URL] = [:], reason: String,
                       keep: Int = 10, budget: Int64 = 2 * 1024 * 1024 * 1024) throws -> URL {
        let fm = FileManager.default
        try fm.createDirectory(at: objects, withIntermediateDirectories: true)
        try fm.createDirectory(at: points, withIntermediateDirectories: true)
        var refs: [String: String] = [:]
        func store(_ data: Data, _ name: String) throws {
            guard Self.safePath(name) else { throw CocoaError(.fileReadInvalidFileName) }
            let hash = Self.digest(data), target = objects.appendingPathComponent(hash)
            let existingHash = (try? Data(contentsOf: target, options: .mappedIfSafe)).map(Self.digest)
            if existingHash != hash {
                try data.write(to: target, options: .atomic)
            }
            refs[name] = hash
        }
        for (name, data) in files { try store(data, name) }
        for (prefix, inputDirectory) in directories {
            let directory = inputDirectory.standardizedFileURL.resolvingSymlinksInPath()
            guard Self.safePath(prefix) else { throw CocoaError(.fileReadInvalidFileName) }
            do {
                guard try directory.resourceValues(forKeys: [.isDirectoryKey]).isDirectory == true else { throw CocoaError(.fileReadUnsupportedScheme) }
            } catch let error as NSError where error.domain == NSCocoaErrorDomain && error.code == NSFileReadNoSuchFileError {
                continue // Optional data directories may not have been created yet.
            }
            var enumerationError: Error?
            guard let enumerator = fm.enumerator(at: directory, includingPropertiesForKeys: [.isRegularFileKey, .isSymbolicLinkKey], errorHandler: { _, error in
                enumerationError = error
                return false
            }) else { throw CocoaError(.fileReadUnknown) }
            for case let file as URL in enumerator {
                let properties = try file.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey])
                guard properties.isSymbolicLink != true else { throw CocoaError(.fileReadUnsupportedScheme) }
                if properties.isRegularFile == true {
                    let relative = String(file.path.dropFirst(directory.path.count + 1))
                    try store(Data(contentsOf: file, options: .mappedIfSafe), prefix + "/" + relative)
                }
            }
            // A stopped enumerator is not evidence that every file was visited.
            // Never publish/prune when any subtree could not be read.
            if let enumerationError { throw enumerationError }
        }
        let fingerprint = Self.digest(Data(refs.keys.sorted().map { "\($0)\u{0}\(refs[$0]!)" }.joined(separator: "\n").utf8))
        let existing = try manifests()
        if let latest = existing.first, latest.1.fingerprint == fingerprint {
            try verify(latest.1)
            return latest.0.resolvingSymlinksInPath()
        }
        let point = RestorePoint(createdAt: Date(), reason: reason, fingerprint: fingerprint, files: refs)
        try verify(point)
        let url = points.appendingPathComponent("\(UUID().uuidString)-\(reason).ntrestore")
        let encoder = JSONEncoder(); encoder.outputFormatting = [.sortedKeys]
        try encoder.encode(point).write(to: url, options: .atomic)
        try prune(keep: max(1, keep), budget: budget)
        return url.resolvingSymlinksInPath()
    }
    public func storageBytes() throws -> Int64 {
        try FileManager.default.contentsOfDirectory(at: objects, includingPropertiesForKeys: [.fileSizeKey]).reduce(0) {
            $0 + Int64(try $1.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0)
        }
    }
    public func restore(_ url: URL, into destination: URL) throws {
        // Recovery pointers must belong to this store; all objects are checked
        // before any destination file is written.
        guard url.deletingLastPathComponent().standardizedFileURL.resolvingSymlinksInPath() == points.standardizedFileURL.resolvingSymlinksInPath() else { throw CocoaError(.fileReadNoPermission) }
        let point = try JSONDecoder().decode(RestorePoint.self, from: Data(contentsOf: url))
        try verify(point)
        for (name, hash) in point.files {
            let file = destination.appendingPathComponent(name)
            try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
            try Data(contentsOf: objects.appendingPathComponent(hash), options: .mappedIfSafe).write(to: file, options: .atomic)
        }
    }
    private func verify(_ point: RestorePoint) throws {
        guard point.version == 1 else { throw CocoaError(.coderReadCorrupt) }
        for (name, hash) in point.files {
            guard Self.safePath(name), hash.count == 64, hash.allSatisfy({ $0.isHexDigit }),
                  Self.digest(try Data(contentsOf: objects.appendingPathComponent(hash), options: .mappedIfSafe)) == hash else { throw CocoaError(.fileReadCorruptFile) }
        }
        let expected = Self.digest(Data(point.files.keys.sorted().map { "\($0)\u{0}\(point.files[$0]!)" }.joined(separator: "\n").utf8))
        guard point.fingerprint == expected else { throw CocoaError(.fileReadCorruptFile) }
    }
    private func manifests() throws -> [(URL, RestorePoint)] {
        try FileManager.default.contentsOfDirectory(at: points, includingPropertiesForKeys: nil).filter { ["ntrestore", "ihrestore"].contains($0.pathExtension) }.map {
            ($0, try JSONDecoder().decode(RestorePoint.self, from: Data(contentsOf: $0)))
        }.sorted { $0.1.createdAt > $1.1.createdAt }
    }
    private func prune(keep: Int, budget: Int64) throws {
        var remaining = try manifests()
        func hashes() -> Set<String> { Set(remaining.flatMap { $0.1.files.values }) }
        func size() -> Int64 {
            hashes().reduce(0) { sum, hash in sum + ((try? objects.appendingPathComponent(hash).resourceValues(forKeys: [.fileSizeKey]).fileSize).map(Int64.init) ?? 0) }
        }
        while remaining.count > 1 && (remaining.count > keep || size() > budget) {
            let old = remaining.removeLast(); try FileManager.default.removeItem(at: old.0)
        }
        let referenced = hashes()
        for file in try FileManager.default.contentsOfDirectory(at: objects, includingPropertiesForKeys: nil) where !referenced.contains(file.lastPathComponent) {
            try FileManager.default.removeItem(at: file)
        }
    }
}
