import Foundation

/// Preflight the system zipinfo output before extracting a portable backup.
public enum BackupArchivePolicy {
    public enum Failure: LocalizedError {
        case unsafePath, symlink, resourceLimit, invalidArchive
        public var errorDescription: String? {
            switch self {
            case .unsafePath: "备份包含不安全的路径。"
            case .symlink: "备份不能包含符号链接。"
            case .resourceLimit: "备份展开后超出资源限制或可用磁盘空间。"
            case .invalidArchive: "无法完整读取备份压缩目录。"
            }
        }
    }
    public static func validate(names: String, listing: String, freeBytes: Int64) throws {
        let paths = names.split(separator: "\n").map(String.init)
        guard !paths.isEmpty, paths.count <= 100000 else { throw Failure.resourceLimit }
        for path in paths {
            let normalized = path.hasSuffix("/") ? String(path.dropLast()) : path
            guard IncrementalBackupStore.safePath(normalized), !path.contains("\r"), !path.contains("\u{0}") else { throw Failure.unsafePath }
        }
        guard Set(paths).count == paths.count else { throw Failure.invalidArchive }
        if listing.split(separator: "\n").contains(where: { $0.hasPrefix("l") }) { throw Failure.symlink }
        let pattern = #"(\d+) files?, (\d+) bytes uncompressed"#
        let regex = try NSRegularExpression(pattern: pattern), text = listing as NSString
        guard let match = regex.matches(in: listing,range: NSRange(location:0,length:text.length)).last,
              let count = Int(text.substring(with: match.range(at:1))), count == paths.count,
              let size = Int64(text.substring(with: match.range(at:2))) else { throw Failure.invalidArchive }
        let maximum: Int64 = 64 * 1024 * 1024 * 1024
        guard size <= maximum, freeBytes >= 512 * 1024 * 1024, size <= (freeBytes - 512 * 1024 * 1024) / 2 else { throw Failure.resourceLimit }
    }
}
