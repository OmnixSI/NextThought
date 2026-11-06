import Foundation
import NextThoughtCore

/// The release includes this immutable manifest in the signed app, never an unsigned remote install script.
enum DocumentRuntimeInstaller {
    struct Manifest:Codable {
        var version:String;var asset:String;var sha256:String;var downloadBytes:Int64;var installedBytes:Int64
    }
    static var manifestURL:URL { DocumentRuntime.resources.appendingPathComponent("runtime-manifest.json") }
    static var sizeDescription: String {
        guard let m = try? JSONDecoder().decode(Manifest.self, from: Data(contentsOf: manifestURL)) else { return "运行包大小见发布清单" }
        let downloadSize = ByteCountFormatter.string(fromByteCount: m.downloadBytes, countStyle: .file)
        let installedSize = ByteCountFormatter.string(fromByteCount: m.installedBytes, countStyle: .file)
        return String(localized: "下载 \(downloadSize) · 安装占用 \(installedSize)")
    }
    static func install(searchLocal: Bool = true, progress:@escaping @Sendable (Double)->Void) async throws {
        let manifest=try JSONDecoder().decode(Manifest.self,from:Data(contentsOf:manifestURL))
        guard manifest.version == DocumentRuntime.version, manifest.sha256.count==64 else { throw AppFailure.capture("运行包清单无效。") }
        if searchLocal {
            let local = try await LocalDocumentAssets.reuse(models: [], runtime: true, reinstallRuntime: true)
            try Task.checkCancellation()
            if local.runtime { return }
        }
        let token=await Updater.resolveToken()
        func request(_ url:URL,binary:Bool)->URLRequest {
            var r=URLRequest(url:url,timeoutInterval:120)
            r.setValue(binary ? "application/octet-stream" : "application/vnd.github+json",forHTTPHeaderField:"Accept")
            r.setValue("2022-11-28",forHTTPHeaderField:"X-GitHub-Api-Version")
            if let token { r.setValue("Bearer \(token)",forHTTPHeaderField:"Authorization") }
            return r
        }
        let releaseURL=URL(string:"https://api.github.com/repos/OmnixSI/NextThought/releases/tags/v\(manifest.version)")!
        let (data,response)=try await URLSession.shared.data(for:request(releaseURL,binary:false),delegate:StripAuthorizationOnRedirect())
        guard (response as? HTTPURLResponse)?.statusCode == 200,
              let release=try JSONSerialization.jsonObject(with:data) as? [String:Any],
              let assets=release["assets"] as? [[String:Any]], let asset=assets.first(where: { $0["name"] as? String == manifest.asset }),
              let address=asset["url"] as? String,let url=URL(string:address) else { throw AppFailure.capture("无法获取文档运行包；私有仓库需要设置中的 GitHub 更新授权。") }
        let stage=DocumentRuntime.temporary.appendingPathComponent("runtime-"+UUID().uuidString,isDirectory:true)
        try FileManager.default.createDirectory(at:stage,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:stage) }
        let archive=stage.appendingPathComponent(manifest.asset)
        try await UpdateDownloader().download(request(url,binary:true),to:archive,progress:progress)
        try await installArchive(archive, manifest: manifest)
    }
    static func installArchive(_ archive: URL, manifest: Manifest) async throws {
        let stage=DocumentRuntime.temporary.appendingPathComponent("unpack-"+UUID().uuidString,isDirectory:true)
        try FileManager.default.createDirectory(at:stage,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:stage) }
        try Task.checkCancellation()
        let runtime = try await Task.detached(priority:.utility) { () throws -> URL in
            guard ManagedModelStore.sha256(of:archive)==manifest.sha256 else { throw AppFailure.capture("运行包 SHA-256 不匹配，请重试。") }
            try Shell.check("/usr/bin/tar",["-xzf",archive.path,"-C",stage.path],"解压文档运行包")
            let runtime=stage.appendingPathComponent("document-runtime",isDirectory:true)
            let python=runtime.appendingPathComponent("python/bin/python3")
            guard FileManager.default.isExecutableFile(atPath:python.path) else { throw AppFailure.capture("运行包缺少 Python。") }
            let executable=python.resolvingSymlinksInPath()
            guard executable.path.hasPrefix(runtime.path+"/") else { throw AppFailure.capture("运行包路径不安全。") }
            try Shell.check("/usr/bin/codesign",["--verify","--strict",executable.path],"校验 Python 签名")
            try Shell.check(python.path,["-I","-c","import sys; assert sys.version_info[:2] == (3,12)"],"检查可重定位 Python")
            try JSONEncoder().encode(manifest).write(to:runtime.appendingPathComponent("installed.json"),options:.atomic)
            return runtime
        }.value
        // Parent cancellation is rechecked after extraction, before the installation commit.
        try Task.checkCancellation()
        await DocumentWorker.shared.unload(); await DocumentWorker.embedding.unload()
        try Task.checkCancellation()
        let target=DocumentRuntime.directory
        try FileManager.default.createDirectory(at:target.deletingLastPathComponent(),withIntermediateDirectories:true)
        if FileManager.default.fileExists(atPath:target.path) { _ = try FileManager.default.replaceItemAt(target,withItemAt:runtime) }
        else { try FileManager.default.moveItem(at:runtime,to:target) }

    }
}
extension AppState {
    func deleteDocumentRuntime() {
        Task {
            localAssetScanTask?.cancel(); await localAssetScanTask?.value
            documentSetupTask?.cancel(); await documentSetupTask?.value
            runtimeDownloadTask?.cancel(); await runtimeDownloadTask?.value
            importTask?.cancel(); await importTask?.value
            semanticIndexTask?.cancel(); semanticGeneration=UUID()
            await DocumentWorker.shared.unload(); await DocumentWorker.embedding.unload()
            do {
                try FileManager.default.removeItem(at: DocumentRuntime.directory)
                runtimeInstalled=false; documentRuntimeStatus="运行包已删除。模型文件可分别保留或删除。"
            } catch { documentRuntimeStatus="删除失败：\(error.localizedDescription)" }
        }
    }
    func installDocumentRuntime() {
        guard !documentSetupBusy else { return }
        isInstallingDocumentRuntime=true
        documentRuntimeStatus="先查找并校验本机运行包，缺少时再下载…"
        runtimeDownloadTask = Task {
            defer { isInstallingDocumentRuntime=false; runtimeDownloadTask=nil }
            do {
                await DocumentWorker.shared.unload();await DocumentWorker.embedding.unload()
                try await DocumentRuntimeInstaller.install { [weak self] fraction in Task { @MainActor in self?.documentRuntimeStatus="正在下载运行包 \(Int(fraction*100))%" } }
                runtimeInstalled=DocumentRuntime.installed
                documentRuntimeStatus="运行包已安装；无需系统 Python、pip 或 Homebrew。"
                rebuildSemanticIndex()
            } catch { documentRuntimeStatus="安装失败：\(error.localizedDescription)" }
        }
    }
}
