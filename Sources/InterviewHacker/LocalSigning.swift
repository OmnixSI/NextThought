import Foundation

/// Runs a command-line tool and returns its output.
enum Shell {
    struct Result {
        var status: Int32
        var output: String
        var error: String
    }

    @discardableResult
    static func run(_ executable: String, _ arguments: [String], input: String? = nil) throws -> Result {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = arguments
        let output = Pipe(), errors = Pipe()
        process.standardOutput = output
        process.standardError = errors
        if let input {
            let stdin = Pipe()
            process.standardInput = stdin
            try process.run()
            stdin.fileHandleForWriting.write(Data(input.utf8))
            try? stdin.fileHandleForWriting.close()
        } else {
            process.standardInput = FileHandle.nullDevice
            try process.run()
        }
        // Read before waiting so a chatty tool cannot fill the pipe and block.
        let outData = output.fileHandleForReading.readDataToEndOfFile()
        let errData = errors.fileHandleForReading.readDataToEndOfFile()
        process.waitUntilExit()
        return Result(status: process.terminationStatus,
                      output: String(decoding: outData, as: UTF8.self),
                      error: String(decoding: errData, as: UTF8.self))
    }

    @discardableResult
    static func check(_ executable: String, _ arguments: [String], _ what: String) throws -> Result {
        let result = try run(executable, arguments)
        guard result.status == 0 else {
            throw LocalSigningError.command(what, (result.error.isEmpty ? result.output : result.error).trimmingCharacters(in: .whitespacesAndNewlines))
        }
        return result
    }
}

enum LocalSigningError: LocalizedError {
    case command(String, String)
    case notAnAppBundle
    case missingIdentity

    var errorDescription: String? {
        switch self {
        case let .command(what, detail): "\(what)失败：\(detail.prefix(300))"
        case .notAnAppBundle: "当前不是从 .app 应用包运行（例如开发构建），无法绑定签名或自动安装更新。"
        case .missingIdentity: "本机签名身份不完整，请在设置中重新绑定。"
        }
    }
}

/// “Bind to this Mac”: macOS ties screen-recording and microphone permission to an
/// app's code-signing identity. Downloaded builds are ad-hoc signed, so every update
/// looks like a new app and permission must be granted again. Binding creates a
/// self-signed code-signing certificate that never leaves this Mac, re-signs the app
/// with it, and the updater re-signs every new version with the same identity — so
/// permissions granted once stay valid across updates.
enum LocalSigning {
    static let bundleIdentifier = "com.interviewhacker.app"
    static let commonName = "InterviewHacker Local Signing"

    static var directory: URL {
        FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("InterviewHacker/Signing", isDirectory: true)
    }
    static var keychain: URL { directory.appendingPathComponent("signing.keychain-db") }
    private static var identityFile: URL { directory.appendingPathComponent("identity-sha1") }
    private static var certificateFile: URL { directory.appendingPathComponent("certificate.pem") }
    /// Password of the dedicated signing keychain. Kept as an owner-only file (like
    /// scripts/setup-local-signing.sh) rather than in the login keychain, whose access
    /// list is tied to the code signature and would prompt after every re-signing.
    private static var passwordFile: URL { directory.appendingPathComponent("keychain-password") }
    private static var keychainPassword: String {
        ((try? String(contentsOf: passwordFile, encoding: .utf8)) ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
    }

    static var identity: String? {
        guard let value = try? String(contentsOf: identityFile, encoding: .utf8) else { return nil }
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.count == 40 ? trimmed : nil
    }

    static var isBound: Bool {
        identity != nil && FileManager.default.fileExists(atPath: keychain.path) && !keychainPassword.isEmpty
    }

    static var designatedRequirement: String? {
        identity.map { "designated => identifier \"\(bundleIdentifier)\" and certificate leaf = H\"\($0)\"" }
    }

    // MARK: Inspecting signatures

    struct SignatureInfo: Equatable {
        var isAdHoc: Bool
        var authority: String?
        var requirement: String?
        var summary: String {
            if isAdHoc { return "临时签名（ad-hoc）· 每个版本签名不同，更新后系统会要求重新授权" }
            return "固定签名 · \(authority ?? "自签名证书")"
        }
    }

    static func signature(of app: URL) -> SignatureInfo? {
        guard let details = try? Shell.run("/usr/bin/codesign", ["-dvv", app.path]) else { return nil }
        let text = details.error + details.output
        let isAdHoc = text.contains("Signature=adhoc") || text.contains("flags=0x2(adhoc)")
        let authority = text.components(separatedBy: "\n").first { $0.hasPrefix("Authority=") }.map { String($0.dropFirst("Authority=".count)) }
        return SignatureInfo(isAdHoc: isAdHoc, authority: authority, requirement: requirement(of: app))
    }

    static func requirement(of app: URL) -> String? {
        guard let result = try? Shell.run("/usr/bin/codesign", ["-d", "-r-", app.path]) else { return nil }
        let text = result.output + "\n" + result.error
        return text.components(separatedBy: "\n").first { $0.contains("designated =>") }
            .map { line in String(line[line.range(of: "designated =>")!.lowerBound...]).trimmingCharacters(in: .whitespaces) }
    }

    // MARK: Identity

    /// Creates the private signing identity on first use. macOS may ask once for the
    /// login password to trust the certificate for code signing.
    static func ensureIdentity() throws -> String {
        if isBound, let identity { return identity }
        let fileManager = FileManager.default
        try? fileManager.removeItem(at: directory)
        try fileManager.createDirectory(at: directory, withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700])
        let work = directory.appendingPathComponent("tmp-\(UUID().uuidString)", isDirectory: true)
        try fileManager.createDirectory(at: work, withIntermediateDirectories: true)
        defer { try? fileManager.removeItem(at: work) }

        let password = UUID().uuidString + UUID().uuidString
        let passwordFile = work.appendingPathComponent("password")
        try password.write(to: passwordFile, atomically: true, encoding: .utf8)
        try fileManager.setAttributes([.posixPermissions: 0o600], ofItemAtPath: passwordFile.path)
        let config = work.appendingPathComponent("openssl.cnf")
        try """
        [req]
        prompt = no
        distinguished_name = subject
        x509_extensions = codesign
        [subject]
        CN = \(commonName)
        O = InterviewHacker
        [codesign]
        basicConstraints = critical,CA:FALSE
        keyUsage = critical,digitalSignature
        extendedKeyUsage = critical,codeSigning
        subjectKeyIdentifier = hash
        """.write(to: config, atomically: true, encoding: .utf8)

        let key = work.appendingPathComponent("key.pem"), p12 = work.appendingPathComponent("identity.p12")
        try Shell.check("/usr/bin/openssl", ["req", "-x509", "-newkey", "rsa:3072", "-sha256", "-nodes", "-days", "3650",
                                             "-config", config.path, "-keyout", key.path, "-out", certificateFile.path], "生成证书")
        try Shell.check("/usr/bin/openssl", ["pkcs12", "-export", "-name", commonName, "-inkey", key.path, "-in", certificateFile.path,
                                             "-keypbe", "PBE-SHA1-3DES", "-certpbe", "PBE-SHA1-3DES", "-out", p12.path,
                                             "-passout", "file:\(passwordFile.path)"], "导出签名身份")
        let fingerprint = try Shell.check("/usr/bin/openssl", ["x509", "-in", certificateFile.path, "-noout", "-fingerprint", "-sha1"], "读取证书指纹").output
        let sha1 = fingerprint.components(separatedBy: "=").last?.replacingOccurrences(of: ":", with: "")
            .trimmingCharacters(in: .whitespacesAndNewlines).uppercased() ?? ""
        guard sha1.count == 40 else { throw LocalSigningError.missingIdentity }

        try Shell.check("/usr/bin/security", ["create-keychain", "-p", password, keychain.path], "创建签名钥匙串")
        _ = try? Shell.run("/usr/bin/security", ["set-keychain-settings", keychain.path])
        try addToSearchList()
        try Shell.check("/usr/bin/security", ["unlock-keychain", "-p", password, keychain.path], "解锁签名钥匙串")
        try Shell.check("/usr/bin/security", ["import", p12.path, "-k", keychain.path, "-f", "pkcs12", "-P", password, "-x", "-T", "/usr/bin/codesign"], "导入签名身份")
        _ = try? Shell.run("/usr/bin/security", ["set-key-partition-list", "-S", "apple-tool:,apple:", "-s", "-k", password, keychain.path])
        // Trusting the certificate for code signing shows a system password prompt once.
        // Signing works without it on current macOS, so a cancelled prompt is not fatal.
        let trust = try? Shell.run("/usr/bin/security", ["add-trusted-cert", "-r", "trustRoot", "-p", "codeSign", "-k", keychain.path, certificateFile.path])
        Diagnostics.record("signing.identity_created", ["trusted": trust?.status == 0])

        try password.write(to: passwordFile, atomically: true, encoding: .utf8)
        try fileManager.setAttributes([.posixPermissions: 0o600], ofItemAtPath: passwordFile.path)
        try sha1.write(to: identityFile, atomically: true, encoding: .utf8)
        return sha1
    }

    private static func addToSearchList() throws {
        let current = try Shell.run("/usr/bin/security", ["list-keychains", "-d", "user"]).output
        var paths = current.components(separatedBy: "\n")
            .map { $0.trimmingCharacters(in: .whitespaces).trimmingCharacters(in: CharacterSet(charactersIn: "\"")) }
            .filter { !$0.isEmpty }
        guard !paths.contains(keychain.path) else { return }
        paths.append(keychain.path)
        try Shell.check("/usr/bin/security", ["list-keychains", "-d", "user", "-s"] + paths, "登记签名钥匙串")
    }

    /// Signs an app bundle (not the running one) with the local identity.
    static func sign(_ app: URL) throws {
        guard let identity, let requirement = designatedRequirement else { throw LocalSigningError.missingIdentity }
        let password = keychainPassword
        guard !password.isEmpty else { throw LocalSigningError.missingIdentity }
        try Shell.check("/usr/bin/security", ["unlock-keychain", "-p", password, keychain.path], "解锁签名钥匙串")
        try addToSearchList()
        _ = try? Shell.run("/usr/bin/xattr", ["-cr", app.path])
        try Shell.check("/usr/bin/codesign", ["--force", "--sign", identity, "--keychain", keychain.path, "--timestamp=none",
                                              "--identifier", bundleIdentifier, "--requirements", "=" + requirement, app.path], "签名应用")
        try Shell.check("/usr/bin/codesign", ["--verify", "--strict", app.path], "校验签名")
    }

    /// Copies the running app, signs the copy and swaps it in on relaunch.
    static func bindRunningApp() throws -> URL {
        let current = Bundle.main.bundleURL
        guard current.pathExtension == "app" else { throw LocalSigningError.notAnAppBundle }
        _ = try ensureIdentity()
        let staging = FileManager.default.temporaryDirectory.appendingPathComponent("ih-bind-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
        let copy = staging.appendingPathComponent(current.lastPathComponent)
        try Shell.check("/usr/bin/ditto", [current.path, copy.path], "复制应用")
        try sign(copy)
        Diagnostics.record("signing.bound")
        return copy
    }
}
