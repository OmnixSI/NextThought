import Foundation
import Testing
@testable import InterviewHackerCore

struct UpdateCatalogTests {
    func release(_ tag: String, prerelease: Bool = false, draft: Bool = false, zip: Bool = true) -> GitHubRelease {
        GitHubRelease(tagName: tag, draft: draft, prerelease: prerelease,
                      assets: zip ? [GitHubReleaseAsset(id: 1, name: "InterviewHacker-\(tag)-macOS-arm64.zip", size: 1, url: "u")] : [])
    }

    @Test func versionOrdering() {
        #expect(UpdateCatalog.compareVersions("v1.2.10", "v1.2.9") == .orderedDescending)
        #expect(UpdateCatalog.compareVersions("1.2", "v1.2.0") == .orderedSame)
        #expect(UpdateCatalog.compareVersions("v1.3.0-beta.2", "v1.3.0") == .orderedAscending)
        #expect(UpdateCatalog.compareVersions("v1.3.0-beta.10", "v1.3.0-beta.9") == .orderedDescending)
        #expect(UpdateCatalog.parseVersion("main-latest") == nil)
    }

    @Test func picksNewestVersionedOrRollingRelease() {
        let releases = [release("main-latest", prerelease: true), release("v1.1.0"), release("v1.2.0-beta.1", prerelease: true),
                        release("v1.3.0", draft: true), release("v1.0.9"), release("v2.0.0", zip: false)]
        #expect(UpdateCatalog.candidate(in: releases, channel: .stable)?.tagName == "v1.2.0-beta.1")
        #expect(UpdateCatalog.candidate(in: releases, channel: .main)?.tagName == "main-latest")
    }

    @Test func buildNumberDecidesWhenManifestExists() {
        let manifest = UpdateManifest(tag: "main-latest", version: "0.2.0", build: 42, zip: "z.zip", sha256: String(repeating: "a", count: 64))
        #expect(UpdateCatalog.isNewer(manifest: manifest, releaseTag: "main-latest", currentVersion: "0.2.0", currentBuild: 41))
        #expect(!UpdateCatalog.isNewer(manifest: manifest, releaseTag: "main-latest", currentVersion: "0.2.0", currentBuild: 42))
        #expect(!UpdateCatalog.isNewer(manifest: nil, releaseTag: "main-latest", currentVersion: "0.2.0", currentBuild: 1))
        #expect(UpdateCatalog.isNewer(manifest: nil, releaseTag: "v0.3.0", currentVersion: "0.2.0", currentBuild: 1))
    }

    @Test func marketingVersionPreventsDowngradeAcrossBuildCounters() {
        let oldMain = UpdateManifest(tag: "main-latest", version: "1.4.1", build: 34, zip: "z.zip", sha256: "a")
        #expect(!UpdateCatalog.isNewer(manifest: oldMain, releaseTag: "main-latest", currentVersion: "1.6.0", currentBuild: 3))
        let formal = UpdateManifest(tag: "v1.6.0", version: "1.6.0", build: 3, zip: "z.zip", sha256: "a")
        #expect(UpdateCatalog.isNewer(manifest: formal, releaseTag: "v1.6.0", currentVersion: "1.4.1", currentBuild: 34))
        #expect(!UpdateCatalog.isNewer(manifest: formal, releaseTag: "v1.6.0", currentVersion: "1.6.0", currentBuild: 3))
        #expect(UpdateCatalog.isNewer(manifest: formal, releaseTag: "v1.6.0", currentVersion: "1.6.0", currentBuild: 2))
    }

    @Test func mainChannelUsesFormalReleaseWhenRollingBuildIsStale() {
        var rolling = release("main-latest", prerelease: true)
        rolling.publishedAt = Date(timeIntervalSince1970: 100)
        var formal = release("v1.6.0")
        formal.publishedAt = Date(timeIntervalSince1970: 200)
        #expect(UpdateCatalog.candidate(in: [rolling, formal], channel: .main)?.tagName == "v1.6.0")
        rolling.publishedAt = Date(timeIntervalSince1970: 300)
        #expect(UpdateCatalog.candidate(in: [formal, rolling], channel: .main)?.tagName == "main-latest")
        #expect(UpdateCatalog.candidate(in: [rolling, formal], channel: .stable)?.tagName == "v1.6.0")
    }

    @Test func decodesGitHubPayloadAndChecksums() throws {
        let json = """
        [{"tag_name":"v1.0.0","name":"InterviewHacker v1.0.0","draft":false,"prerelease":false,"published_at":"2026-10-01T10:00:00Z",
          "assets":[{"id":7,"name":"InterviewHacker-1.0.0-macOS-arm64.zip","size":123,"url":"https://api.github.com/repos/o/r/releases/assets/7","browser_download_url":"https://github.com/o/r/releases/download/v1.0.0/x.zip"}]}]
        """
        let releases = try GitHubRelease.decodeList(Data(json.utf8))
        #expect(releases.first?.zipAsset?.id == 7)
        let sums = "\(String(repeating: "b", count: 64))  InterviewHacker-1.0.0-macOS-arm64.zip\n\(String(repeating: "c", count: 64))  InterviewHacker-1.0.0-macOS-arm64.dmg\n"
        #expect(UpdateCatalog.sha256(for: "InterviewHacker-1.0.0-macOS-arm64.zip", in: sums) == String(repeating: "b", count: 64))
        #expect(UpdateCatalog.sha256(for: "missing.zip", in: sums) == nil)
    }
}
