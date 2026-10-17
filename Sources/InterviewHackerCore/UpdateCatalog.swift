import Foundation

/// GitHub Releases payloads and the rules for picking an update. Network and
/// installation live in the app; this part is pure and unit-tested.
public struct GitHubReleaseAsset: Codable, Sendable, Equatable {
    public var id: Int
    public var name: String
    public var size: Int
    public var url: String
    public var browserDownloadURL: String?

    enum CodingKeys: String, CodingKey {
        case id, name, size, url
        case browserDownloadURL = "browser_download_url"
    }

    public init(id: Int, name: String, size: Int, url: String, browserDownloadURL: String? = nil) {
        self.id = id; self.name = name; self.size = size; self.url = url; self.browserDownloadURL = browserDownloadURL
    }
}

public struct GitHubRelease: Codable, Sendable, Equatable {
    public var tagName: String
    public var name: String?
    public var body: String?
    public var draft: Bool
    public var prerelease: Bool
    public var publishedAt: Date?
    public var assets: [GitHubReleaseAsset]

    enum CodingKeys: String, CodingKey {
        case name, body, draft, prerelease, assets
        case tagName = "tag_name"
        case publishedAt = "published_at"
    }

    public init(tagName: String, name: String? = nil, body: String? = nil, draft: Bool = false, prerelease: Bool = false, publishedAt: Date? = nil, assets: [GitHubReleaseAsset] = []) {
        self.tagName = tagName; self.name = name; self.body = body; self.draft = draft
        self.prerelease = prerelease; self.publishedAt = publishedAt; self.assets = assets
    }

    public static func decodeList(_ data: Data) throws -> [GitHubRelease] {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode([GitHubRelease].self, from: data)
    }

    public func asset(named name: String) -> GitHubReleaseAsset? { assets.first { $0.name == name } }
    public var zipAsset: GitHubReleaseAsset? { assets.first { $0.name.hasSuffix("-macOS-arm64.zip") } }
}

/// `update.json`, published next to the ZIP by the release workflow.
public struct UpdateManifest: Codable, Sendable, Equatable {
    public var tag: String
    public var version: String
    /// CFBundleVersion, ordered only within the same marketing version.
    public var build: Int
    public var zip: String
    public var sha256: String
    public var commit: String?
    public var publishedAt: Date?

    public init(tag: String, version: String, build: Int, zip: String, sha256: String, commit: String? = nil, publishedAt: Date? = nil) {
        self.tag = tag; self.version = version; self.build = build; self.zip = zip; self.sha256 = sha256
        self.commit = commit; self.publishedAt = publishedAt
    }

    public static func decode(_ data: Data) throws -> UpdateManifest {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(UpdateManifest.self, from: data)
    }
}

public enum UpdateChannel: String, Codable, Sendable, CaseIterable, Identifiable {
    /// Versioned releases (vX.Y.Z, including betas).
    case stable
    /// The rolling `main-latest` build of the main branch.
    case main
    public var id: String { rawValue }
    public var title: String { self == .stable ? "正式版本" : "main 最新构建" }
}

public enum UpdateCatalog {
    public static let rollingTag = "main-latest"

    /// The release to offer on a channel, or nil.
    public static func candidate(in releases: [GitHubRelease], channel: UpdateChannel) -> GitHubRelease? {
        let usable = releases.filter { !$0.draft && $0.zipAsset != nil }
        switch channel {
        case .main:
            // A formal release may be newer than the last rolling build (release
            // commits do not necessarily publish main-latest).
            return usable.filter { $0.tagName == rollingTag || parseVersion($0.tagName) != nil }
                .max { ($0.publishedAt ?? .distantPast) < ($1.publishedAt ?? .distantPast) }
        case .stable:
            let versioned = usable.filter { $0.tagName != rollingTag && parseVersion($0.tagName) != nil }
            return versioned.max { lhs, rhs in
                compareVersions(lhs.tagName, rhs.tagName) == .orderedAscending
            }
        }
    }

    /// Whether `manifest` (or, without one, the release tag) is newer than the running app.
    public static func isNewer(manifest: UpdateManifest?, releaseTag: String, currentVersion: String, currentBuild: Int) -> Bool {
        if let manifest {
            let versionOrder = compareVersions(manifest.version, currentVersion)
            if versionOrder != .orderedSame { return versionOrder == .orderedDescending }
            return manifest.build > currentBuild
        }
        guard parseVersion(releaseTag) != nil else { return false }
        return compareVersions(releaseTag, currentVersion) == .orderedDescending
    }

    /// "v1.2.3-beta.4" → ([1, 2, 3], "beta.4").
    public static func parseVersion(_ raw: String) -> (numbers: [Int], prerelease: String?)? {
        var text = raw.trimmingCharacters(in: .whitespaces)
        if text.hasPrefix("v") || text.hasPrefix("V") { text.removeFirst() }
        let parts = text.split(separator: "-", maxSplits: 1).map(String.init)
        guard let core = parts.first else { return nil }
        let numbers = core.split(separator: ".").map { Int($0) }
        guard !numbers.isEmpty, numbers.allSatisfy({ $0 != nil }) else { return nil }
        return (numbers.compactMap { $0 }, parts.count > 1 ? parts[1] : nil)
    }

    /// Semantic-version ordering; a prerelease sorts before its release.
    public static func compareVersions(_ a: String, _ b: String) -> ComparisonResult {
        guard let left = parseVersion(a), let right = parseVersion(b) else { return a.compare(b) }
        for index in 0..<max(left.numbers.count, right.numbers.count) {
            let l = index < left.numbers.count ? left.numbers[index] : 0
            let r = index < right.numbers.count ? right.numbers[index] : 0
            if l != r { return l < r ? .orderedAscending : .orderedDescending }
        }
        switch (left.prerelease, right.prerelease) {
        case (nil, nil): return .orderedSame
        case (nil, _): return .orderedDescending
        case (_, nil): return .orderedAscending
        case let (l?, r?):
            // Compare dot-separated identifiers numerically when both are numbers.
            let lp = l.split(separator: "."), rp = r.split(separator: ".")
            for index in 0..<max(lp.count, rp.count) {
                guard index < lp.count else { return .orderedAscending }
                guard index < rp.count else { return .orderedDescending }
                if let li = Int(lp[index]), let ri = Int(rp[index]) {
                    if li != ri { return li < ri ? .orderedAscending : .orderedDescending }
                } else if lp[index] != rp[index] {
                    return lp[index] < rp[index] ? .orderedAscending : .orderedDescending
                }
            }
            return .orderedSame
        }
    }

    /// Finds the hash for `file` in a `shasum -a 256` listing.
    public static func sha256(for file: String, in sums: String) -> String? {
        for line in sums.components(separatedBy: .newlines) {
            let parts = line.split(separator: " ", omittingEmptySubsequences: true)
            guard parts.count >= 2 else { continue }
            let name = parts[parts.count - 1].trimmingCharacters(in: CharacterSet(charactersIn: "*"))
            if name == file, parts[0].count == 64 { return String(parts[0]).lowercased() }
        }
        return nil
    }
}
