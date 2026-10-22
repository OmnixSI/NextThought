import Foundation

/// Optional local adapter for the binary PowerPoint format. Each conversion has
/// a private LibreOffice profile; no running user profile is opened or modified.
enum LegacyOfficeConverter {
    static var executable: URL? {
        let paths = ["/Applications/LibreOffice.app/Contents/MacOS/soffice", "/opt/homebrew/bin/soffice", "/usr/local/bin/soffice"]
        return paths.first(where: { FileManager.default.isExecutableFile(atPath: $0) }).map { URL(fileURLWithPath: $0) }
    }
    static var available: Bool { executable != nil }
    static func convert(_ source: URL, into directory: URL) async throws -> URL {
        guard let executable else { throw AppFailure.capture("旧版 .ppt 需要 LibreOffice 本地转换组件；安装后可重试，无需手动转换资料。") }
        return try await Task.detached(priority: .utility) {
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            let profile = directory.appendingPathComponent("profile", isDirectory: true)
            let user = profile.appendingPathComponent("user", isDirectory: true)
            try FileManager.default.createDirectory(at: user, withIntermediateDirectories: true)
            try """
            <?xml version="1.0" encoding="UTF-8"?><oor:items xmlns:oor="http://openoffice.org/2001/registry"><item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop></item></oor:items>
            """.write(to: user.appendingPathComponent("registrymodifications.xcu"), atomically: true, encoding: .utf8)
            let process = Process(); process.executableURL = executable
            process.arguments = ["-env:UserInstallation=" + profile.absoluteString, "--headless", "--norestore", "--nodefault", "--nolockcheck", "--convert-to", "pptx", "--outdir", directory.path, source.path]
            process.standardOutput = FileHandle.nullDevice; process.standardError = FileHandle.nullDevice
            try process.run()
            let deadline = Date().addingTimeInterval(120)
            while process.isRunning {
                if Task.isCancelled || Date() > deadline { process.terminate(); throw AppFailure.capture("PPT 转换已取消或超时，可重试。") }
                Thread.sleep(forTimeInterval: 0.1)
            }
            let output = directory.appendingPathComponent(source.deletingPathExtension().lastPathComponent + ".pptx")
            guard process.terminationStatus == 0, FileManager.default.fileExists(atPath: output.path) else { throw AppFailure.capture("LibreOffice 无法转换此 PPT；文件可能损坏或受密码保护。") }
            return output
        }.value
    }
}
