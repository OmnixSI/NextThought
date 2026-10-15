import AppKit
import CryptoKit
import Foundation
import InterviewHackerCore
import Observation

/// Checks GitHub Releases for a newer build, downloads and verifies it in the
/// background, and on “重启安装” replaces the app and relaunches it.
///
/// The repository is private, so the GitHub API needs a token: a fine-grained
/// personal access token with read-only “Contents” permission saved in the Keychain,
/// or the token of a logged-in GitHub CLI (`gh auth login`).
@MainActor
@Observable
final class Updater {
    static let shared = Updater()

    enum Phase: Equatable {
        case idle
        case checking
        case upToDate
        case available
        case downloading(Double)
        case ready
        case installing
        case failed(String)
    }

    struct Available: Equatable {
        var release: GitHubRelease
        var manifest: UpdateManifest?
        var zip: GitHubReleaseAsset
        var sha256: String?
        var displayVersion: String {
            if let manifest { return manifest.tag == UpdateCatalog.rollingTag ? "main 构建 #\(manifest.build)" : "\(manifest.version)（#\(manifest.build)）" }
            return release.tagName
        }
    }

    private(set) var phase: Phase = .idle
    private(set) var available: Available?
    private(set) var lastChecked: Date?
    /// Signature of the downloaded build relative to the running one.
    private(set) var stagedNote: String?
    var channel: UpdateChannel = UpdateChannel(rawValue: UserDefaults.standard.string(forKey: "updateChannel") ?? "") ?? .stable
    var automaticChecks: Bool = UserDefaults.standard.object(forKey: "automaticUpdateChecks") as? Bool ?? true
    var repository: String = UserDefaults.standard.string(forKey: "updateRepository") ?? "OmnixSI/InterviewHacker"
    private(set) var hasToken = !KeychainStore.read("github.token").isEmpty
    private(set) var signature: LocalSigning.SignatureInfo?

    @ObservationIgnored private var stagedApp: URL?
    @ObservationIgnored private var checkLoop: Task<Void, Never>?
    @ObservationIgnored private var downloadTask: Task<Void, Never>?

    private init() {}

    var currentVersion: String { Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0" }
    var currentBuild: Int { Int(Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? "") ?? 0 }
    var canInstall: Bool { Bundle.main.bundleURL.pathExtension == "app" }

    // MARK: Scheduling

    func start() {
        refreshSignature()
        scheduleChecks()
    }

    private func scheduleChecks() {
        checkLoop?.cancel()
        guard automaticChecks, !DemoMode.isEnabled else { return }
        checkLoop = Task { [weak self] in
            try? await Task.sleep(for: .seconds(20))
            while !Task.isCancelled {
                await self?.check(userInitiated: false)
                try? await Task.sleep(for: .seconds(6 * 3_600))
            }
        }
    }

    /// Persists the update preferences (called after the settings controls change).
    func saveSettings() {
        UserDefaults.standard.set(channel.rawValue, forKey: "updateChannel")
        UserDefaults.standard.set(automaticChecks, forKey: "automaticUpdateChecks")
        UserDefaults.standard.set(repository.trimmingCharacters(in: .whitespaces), forKey: "updateRepository")
        scheduleChecks()
    }

    func refreshSignature() {
        let app = Bundle.main.bundleURL
        Task.detached(priority: .utility) {
            let info = LocalSigning.signature(of: app)
            await MainActor.run { self.signature = info }
        }
    }

    func saveToken(_ token: String) {
        let trimmed = token.trimmingCharacters(in: .whitespacesAndNewlines)
        _ = KeychainStore.write(trimmed, account: "github.token")
        hasToken = !trimmed.isEmpty
    }

    // MARK: Checking

    func check(userInitiated: Bool) async {
        switch phase {
        case .checking, .downloading, .installing: return
        case .ready where !userInitiated: return
        default: break
        }
        phase = .checking
        do {
            let token = await Self.resolveToken()
            let releases = try GitHubRelease.decodeList(try await api("releases?per_page=20", token: token))
            lastChecked = Date()
            guard let release = UpdateCatalog.candidate(in: releases, channel: channel), let zip = release.zipAsset else {
                available = nil; phase = .upToDate; return
            }
            var manifest: UpdateManifest?
            if let asset = release.asset(named: "update.json") {
                manifest = try? UpdateManifest.decode(try await download(asset, token: token))
            }
            guard UpdateCatalog.isNewer(manifest: manifest, releaseTag: release.tagName, currentVersion: currentVersion, currentBuild: currentBuild) else {
                available = nil; phase = .upToDate; return
            }
            var sha = manifest?.zip == zip.name ? manifest?.sha256 : nil
            if sha == nil, let sums = release.asset(named: "SHA256SUMS.txt") {
                sha = UpdateCatalog.sha256(for: zip.name, in: String(decoding: try await download(sums, token: token), as: UTF8.self))
            }
            let next = Available(release: release, manifest: manifest, zip: zip, sha256: sha)
            if next != available { stagedApp = nil; stagedNote = nil }
            available = next
            phase = stagedApp == nil ? .available : .ready
            Diagnostics.record("update.available", ["tag": release.tagName, "build": manifest?.build ?? -1])
            // Download in the background so “重启安装” is instant.
            if stagedApp == nil { downloadUpdate() }
        } catch {
            phase = userInitiated || available == nil ? .failed(error.localizedDescription) : .available
            Diagnostics.record("update.check_failed")
        }
    }

    // MARK: Downloading

    func downloadUpdate() {
        guard let available, downloadTask == nil else { return }
        downloadTask = Task { [weak self] in
            guard let self else { return }
            defer { self.downloadTask = nil }
            do {
                guard let expected = available.sha256 else { throw UpdateError.missingChecksum }
                self.phase = .downloading(0)
                let token = await Self.resolveToken()
                let staging = FileManager.default.temporaryDirectory.appendingPathComponent("ih-update-\(UUID().uuidString)", isDirectory: true)
                try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
                let zip = staging.appendingPathComponent(available.zip.name)
                let downloader = UpdateDownloader()
                try await downloader.download(request(URL(string: available.zip.url)!, token: token, binary: true), to: zip) { fraction in
                    Task { @MainActor in self.phase = .downloading(fraction) }
                }
                let (app, note) = try await Task.detached(priority: .userInitiated) { () throws -> (URL, String?) in
                    try Self.prepare(zip: zip, expectedSHA256: expected, in: staging)
                }.value
                self.stagedApp = app
                self.stagedNote = note
                self.phase = .ready
                Diagnostics.record("update.ready", ["tag": available.release.tagName])
            } catch is CancellationError {
                self.phase = .available
            } catch {
                self.phase = .failed(error.localizedDescription)
                Diagnostics.record("update.download_failed")
            }
        }
    }

    /// Verifies the checksum, unpacks the ZIP and makes sure the new app keeps the
    /// current signing identity (re-signing with the local identity when bound).
    nonisolated private static func prepare(zip: URL, expectedSHA256: String, in staging: URL) throws -> (URL, String?) {
        let handle = try FileHandle(forReadingFrom: zip)
        var hasher = SHA256()
        while let chunk = try handle.read(upToCount: 4 << 20), !chunk.isEmpty { hasher.update(data: chunk) }
        try? handle.close()
        let hash = hasher.finalize().map { String(format: "%02x", $0) }.joined()
        guard hash == expectedSHA256.lowercased() else { throw UpdateError.checksumMismatch }

        let unpacked = staging.appendingPathComponent("app", isDirectory: true)
        try Shell.check("/usr/bin/ditto", ["-x", "-k", zip.path, unpacked.path], "解压更新")
        guard let app = (try FileManager.default.contentsOfDirectory(at: unpacked, includingPropertiesForKeys: nil)).first(where: { $0.pathExtension == "app" }) else {
            throw UpdateError.invalidArchive
        }
        _ = try? Shell.run("/usr/bin/xattr", ["-dr", "com.apple.quarantine", app.path])
        try Shell.check("/usr/bin/codesign", ["--verify", "--strict", app.path], "校验新版本签名")

        let running = LocalSigning.requirement(of: Bundle.main.bundleURL)
        let incoming = LocalSigning.requirement(of: app)
        if let running, running == incoming { return (app, nil) }
        if LocalSigning.isBound {
            try LocalSigning.sign(app)
            return (app, "已用本机签名重新签名，权限保持不变。")
        }
        return (app, "新版本的签名与当前不同，安装后系统会要求重新允许录屏与麦克风。可在设置中“绑定本机签名”避免以后再出现。")
    }

    // MARK: Installing

    /// Replaces the running app with the staged one and relaunches.
    func installAndRelaunch() {
        guard let stagedApp else { return }
        do {
            phase = .installing
            try Self.replaceAndRelaunch(with: stagedApp)
        } catch {
            phase = .failed(error.localizedDescription)
        }
    }

    /// Signs a copy of the running app with the local identity and relaunches into it.
    func bindSignatureAndRelaunch() {
        phase = .installing
        Task.detached(priority: .userInitiated) {
            do {
                let copy = try LocalSigning.bindRunningApp()
                try await MainActor.run { try Updater.replaceAndRelaunch(with: copy) }
            } catch {
                await MainActor.run { self.phase = .failed(error.localizedDescription) }
            }
        }
    }

    static func replaceAndRelaunch(with newApp: URL) throws {
        let destination = Bundle.main.bundleURL
        guard destination.pathExtension == "app" else { throw LocalSigningError.notAnAppBundle }
        guard FileManager.default.isWritableFile(atPath: destination.deletingLastPathComponent().path) else {
            throw UpdateError.notWritable(destination.deletingLastPathComponent().path)
        }
        let script = FileManager.default.temporaryDirectory.appendingPathComponent("ih-relaunch-\(UUID().uuidString).sh")
        try """
        #!/bin/bash
        # Waits for InterviewHacker to quit, swaps the bundle and relaunches it.
        pid="$1"; src="$2"; dst="$3"
        for _ in $(seq 1 150); do kill -0 "$pid" 2>/dev/null || break; sleep 0.2; done
        backup="$dst.previous"
        rm -rf "$backup"
        if mv "$dst" "$backup" && /usr/bin/ditto "$src" "$dst"; then
            rm -rf "$backup"
        else
            rm -rf "$dst"; mv "$backup" "$dst"
        fi
        /usr/bin/xattr -dr com.apple.quarantine "$dst" 2>/dev/null
        /usr/bin/open "$dst"
        rm -rf "$(dirname "$src")" "$0"
        """.write(to: script, atomically: true, encoding: .utf8)
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/nohup")
        process.arguments = ["/bin/bash", script.path, String(ProcessInfo.processInfo.processIdentifier), newApp.path, destination.path]
        process.standardOutput = FileHandle.nullDevice
        process.standardError = FileHandle.nullDevice
        try process.run()
        Diagnostics.record("update.relaunching")
        NSApp.terminate(nil)
    }

    // MARK: GitHub API

    private func api(_ path: String, token: String?) async throws -> Data {
        guard let url = URL(string: "https://api.github.com/repos/\(repository)/\(path)") else { throw UpdateError.badRepository }
        let (data, response) = try await URLSession.shared.data(for: request(url, token: token, binary: false), delegate: StripAuthorizationOnRedirect())
        let status = (response as? HTTPURLResponse)?.statusCode ?? 0
        switch status {
        case 200..<300: return data
        case 401, 403: throw token == nil ? UpdateError.needsToken : UpdateError.tokenRejected
        case 404 where token == nil: throw UpdateError.needsToken
        case 404: throw UpdateError.repositoryNotFound(repository)
        default: throw UpdateError.http(status)
        }
    }

    private func download(_ asset: GitHubReleaseAsset, token: String?) async throws -> Data {
        guard let url = URL(string: asset.url) else { throw UpdateError.invalidArchive }
        let (data, response) = try await URLSession.shared.data(for: request(url, token: token, binary: true), delegate: StripAuthorizationOnRedirect())
        guard let http = response as? HTTPURLResponse, (200..<300).contains(http.statusCode) else { throw UpdateError.http((response as? HTTPURLResponse)?.statusCode ?? 0) }
        return data
    }

    private func request(_ url: URL, token: String?, binary: Bool) -> URLRequest {
        var request = URLRequest(url: url, timeoutInterval: 60)
        request.setValue(binary ? "application/octet-stream" : "application/vnd.github+json", forHTTPHeaderField: "Accept")
        request.setValue("2022-11-28", forHTTPHeaderField: "X-GitHub-Api-Version")
        request.setValue("InterviewHacker/\(currentVersion)", forHTTPHeaderField: "User-Agent")
        if let token, !token.isEmpty { request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization") }
        return request
    }

    /// Keychain token, else the token of a logged-in GitHub CLI.
    nonisolated static func resolveToken() async -> String? {
        let saved = KeychainStore.read("github.token")
        if !saved.isEmpty { return saved }
        return await Task.detached(priority: .utility) { () -> String? in
            for path in ["/opt/homebrew/bin/gh", "/usr/local/bin/gh", "/usr/bin/gh"] where FileManager.default.isExecutableFile(atPath: path) {
                if let result = try? Shell.run(path, ["auth", "token"]), result.status == 0 {
                    let token = result.output.trimmingCharacters(in: .whitespacesAndNewlines)
                    if !token.isEmpty { return token }
                }
            }
            return nil
        }.value
    }
}

enum UpdateError: LocalizedError {
    case needsToken, tokenRejected, badRepository, repositoryNotFound(String), http(Int)
    case missingChecksum, checksumMismatch, invalidArchive, notWritable(String)

    var errorDescription: String? {
        switch self {
        case .needsToken: "仓库是私有的：请在 设置 → 更新 填写 GitHub 访问令牌（只需 Contents 只读权限），或在终端运行 gh auth login。"
        case .tokenRejected: "GitHub 拒绝了访问令牌：请确认令牌未过期，且对该仓库有 Contents 读取权限。"
        case .badRepository: "更新仓库地址无效。"
        case let .repositoryNotFound(name): "找不到仓库 \(name)，或令牌无权访问它。"
        case let .http(code): "GitHub 返回 HTTP \(code)。稍后会自动重试。"
        case .missingChecksum: "该版本没有提供 SHA-256 校验值，为安全起见不自动安装。"
        case .checksumMismatch: "下载的更新未通过 SHA-256 校验，已放弃安装。"
        case .invalidArchive: "更新包里没有找到 InterviewHacker.app。"
        case let .notWritable(path): "没有写入 \(path) 的权限。请把应用放在“应用程序”文件夹并用管理员账户运行，或用 scripts/install-latest.sh 更新。"
        }
    }
}

/// GitHub redirects asset downloads to a pre-signed storage URL that rejects an
/// extra Authorization header; drop it when the host changes.
final class StripAuthorizationOnRedirect: NSObject, URLSessionTaskDelegate {
    func urlSession(_ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse,
                    newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void) {
        var next = request
        if next.url?.host != task.originalRequest?.url?.host { next.setValue(nil, forHTTPHeaderField: "Authorization") }
        completionHandler(next)
    }
}

/// File download with progress and the same redirect handling.
final class UpdateDownloader: NSObject, URLSessionDownloadDelegate, @unchecked Sendable {
    private let lock = NSLock()
    private var continuation: CheckedContinuation<Void, Error>?
    private var destination: URL?
    private var moved = false
    private var progress: (@Sendable (Double) -> Void)?
    private lazy var session = URLSession(configuration: .ephemeral, delegate: self, delegateQueue: OperationQueue())

    func download(_ request: URLRequest, to destination: URL, progress: @escaping @Sendable (Double) -> Void) async throws {
        defer { session.finishTasksAndInvalidate() }
        try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
                lock.withLock {
                    self.continuation = continuation
                    self.destination = destination
                    self.progress = progress
                    self.moved = false
                }
                session.downloadTask(with: request).resume()
            }
        } onCancel: {
            self.session.invalidateAndCancel()
        }
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse,
                    newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void) {
        var next = request
        if next.url?.host != task.originalRequest?.url?.host { next.setValue(nil, forHTTPHeaderField: "Authorization") }
        completionHandler(next)
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didWriteData bytesWritten: Int64,
                    totalBytesWritten: Int64, totalBytesExpectedToWrite: Int64) {
        guard totalBytesExpectedToWrite > 0 else { return }
        let handler = lock.withLock { progress }
        handler?(Double(totalBytesWritten) / Double(totalBytesExpectedToWrite))
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didFinishDownloadingTo location: URL) {
        guard let destination = lock.withLock({ self.destination }),
              let http = downloadTask.response as? HTTPURLResponse, (200..<300).contains(http.statusCode) else { return }
        try? FileManager.default.removeItem(at: destination)
        if (try? FileManager.default.moveItem(at: location, to: destination)) != nil { lock.withLock { moved = true } }
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        let (continuation, moved) = lock.withLock { () -> (CheckedContinuation<Void, Error>?, Bool) in
            let value = (self.continuation, self.moved)
            self.continuation = nil
            return value
        }
        guard let continuation else { return }
        if let error { continuation.resume(throwing: error) }
        else if moved { continuation.resume() }
        else { continuation.resume(throwing: UpdateError.http((task.response as? HTTPURLResponse)?.statusCode ?? 0)) }
    }
}
