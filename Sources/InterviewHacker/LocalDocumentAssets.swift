import AppKit
import Foundation
import InterviewHackerCore

/// On-demand local discovery. Reads filenames first and hashes only compatible-sized candidates.
/// No broad scan runs during view refresh, answering or ordinary app startup.
enum LocalDocumentAssets {
    struct Outcome {
        var models = 0
        var reusedBytes: Int64 = 0
        var runtime = false
        var limited = false
        var inaccessible = 0
    }
    static var roots: [URL] {
        let home = FileManager.default.homeDirectoryForCurrentUser
        var roots = [AppPaths.models, home.appendingPathComponent(".cache/huggingface/hub"),
                     home.appendingPathComponent("Library/Caches/huggingface/hub"),
                     home.appendingPathComponent("Downloads"), home.appendingPathComponent("Documents"),
                     home.appendingPathComponent(".minimax/sessions"), home.appendingPathComponent(".codex"), home]
        let env = ProcessInfo.processInfo.environment
        if let cache = env["HF_HUB_CACHE"] { roots.insert(URL(fileURLWithPath: cache), at: 0) }
        if let cache = env["HF_HOME"] { roots.insert(URL(fileURLWithPath: cache).appendingPathComponent("hub"), at: 0) }
        var seen = Set<String>()
        return roots.filter { FileManager.default.fileExists(atPath: $0.path) && seen.insert($0.standardizedFileURL.path).inserted }
    }
    private static func indexedFiles(names: Set<String>) async throws -> [URL] {
        let stage = DocumentRuntime.temporary.appendingPathComponent("local-scan-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: stage, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: stage) }
        let output = stage.appendingPathComponent("paths")
        FileManager.default.createFile(atPath: output.path, contents: nil)
        let handle = try FileHandle(forWritingTo: output)
        defer { try? handle.close() }
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/mdfind")
        // These names come exclusively from the signed, pinned catalog, never from documents.
        process.arguments = ["-0", names.sorted().map { "kMDItemFSName == \"\($0)\"" }.joined(separator: " || ")]
        process.standardOutput = handle; process.standardError = FileHandle.nullDevice
        do { try process.run() } catch { return [] }
        defer { if process.isRunning { process.terminate() } }
        let deadline = Date().addingTimeInterval(5)
        while process.isRunning {
            try Task.checkCancellation()
            if Date() > deadline { process.terminate(); return [] }
            try await Task.sleep(for: .milliseconds(50))
        }
        try handle.synchronize()
        guard process.terminationStatus == 0,
              (try output.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0) <= 4_000_000 else { return [] }
        return try Data(contentsOf: output).split(separator: 0).compactMap { bytes in
            String(data: Data(bytes), encoding: .utf8).map { URL(fileURLWithPath: $0) }
        }
    }
    static func reuse(models: [ManagedModelDescriptor], runtime: Bool,
                      searchRoots: [URL]? = nil, verifyInstalled: Bool = false, reinstallRuntime: Bool = false) async throws -> Outcome {
        let missing = models.filter { verifyInstalled || !ManagedModelStore.isInstalled($0) }
        let needsRuntime = runtime && (reinstallRuntime || !DocumentRuntime.installed)
        guard !missing.isEmpty || needsRuntime else { return Outcome() }
        let manifest = try JSONDecoder().decode(DocumentRuntimeInstaller.Manifest.self, from: Data(contentsOf: DocumentRuntimeInstaller.manifestURL))
        let names = Set(["model.safetensors", "inference.onnx", manifest.asset] + missing.flatMap { [$0.id, $0.revision] })
        let indexed = searchRoots == nil ? try await indexedFiles(names: names) : []
        let roots = searchRoots ?? self.roots
        let search = try await Task.detached(priority: .utility) {
            try LocalModelReuse.search(roots: roots, indexedFiles: indexed, names: names)
        }.valueWithCancellation()
        var outcome = Outcome(); outcome.limited = search.limited; outcome.inaccessible = search.inaccessible
        for model in missing {
            try Task.checkCancellation()
            let result = try await Task.detached(priority: .utility) {
                let files = model.files.map { LocalModelReuse.File(path: $0.path, bytes: $0.size, sha256: $0.sha256) }
                let result = try LocalModelReuse.reuse(files, from: search.directories, into: model.directory)
                if try files.allSatisfy({ try LocalModelReuse.matches(model.directory.appendingPathComponent($0.path), file: $0) }) {
                    try Task.checkCancellation()
                    try JSONSerialization.data(withJSONObject: ["revision": model.revision])
                        .write(to: model.directory.appendingPathComponent("verified.json"), options: .atomic)
                }
                return result
            }.valueWithCancellation()
            outcome.reusedBytes += result.reusedBytes
            if ManagedModelStore.isInstalled(model) { outcome.models += 1 }
        }
        if needsRuntime {
            let specification = LocalModelReuse.File(path: manifest.asset, bytes: manifest.downloadBytes, sha256: manifest.sha256)
            for directory in search.directories {
                try Task.checkCancellation()
                let archive = directory.appendingPathComponent(manifest.asset)
                let matches = try await Task.detached(priority: .utility) { try LocalModelReuse.matches(archive, file: specification) }.valueWithCancellation()
                guard matches else { continue }
                try await DocumentRuntimeInstaller.installArchive(archive, manifest: manifest)
                outcome.runtime = true; break
            }
        }
        return outcome
    }
}

private extension Task where Failure == Error {
    /// Detached filesystem work must be cancelled before a deletion can proceed.
    func valueWithCancellation() async throws -> Success {
        try await withTaskCancellationHandler { try await value } onCancel: { cancel() }
    }
}

extension AppState {
    func scanLocalDocumentAssets(in directory: URL? = nil) {
        guard documentModelsChecked, !documentSetupBusy else { return }
        isScanningLocalAssets = true
        localAssetScanStatus = "正在查找本机已有组件，匹配版本后校验并复用…"
        localAssetScanTask = Task {
            defer { isScanningLocalAssets = false; localAssetScanTask = nil }
            do {
                await refreshManagedModelStatus()
                try Task.checkCancellation()
                let missing = ModelCatalog.all.filter { !installedDocumentModels.contains($0.id) }
                let result = try await LocalDocumentAssets.reuse(models: missing, runtime: true,
                                                                searchRoots: directory.map { [$0] }, verifyInstalled: true)
                try Task.checkCancellation()
                documentModelInstallRevision += 1
                await refreshManagedModelStatus()
                let bytes = ByteCountFormatter.string(fromByteCount: result.reusedBytes, countStyle: .file)
                localAssetScanStatus = "本应用现有 \(installedDocumentModels.count) 项文档模型，支持组件\(runtimeInstalled ? "已安装" : "未安装")。本次复用 \(bytes)\(result.runtime ? "及本地运行包" : "")，没有联网下载。"
                if result.limited || result.inaccessible > 0 {
                    localAssetScanStatus += " 部分位置未完成扫描，可选择具体目录继续。"
                } else if installedDocumentModels.count < ModelCatalog.all.count || !runtimeInstalled {
                    localAssetScanStatus += " 未找到匹配部分，可选择其他目录或按需下载。"
                }
            } catch is CancellationError {
                documentModelInstallRevision += 1; await refreshManagedModelStatus()
                localAssetScanStatus = "已取消扫描；已校验并复用的文件保留，原文件没有改变。"
            } catch {
                documentModelInstallRevision += 1; await refreshManagedModelStatus()
                localAssetScanStatus = "本机扫描失败：\(error.localizedDescription)；可选择具体目录重试。"
            }
        }
    }
    func chooseLocalDocumentAssets() {
        let panel = NSOpenPanel(); panel.canChooseFiles = false; panel.canChooseDirectories = true
        panel.allowsMultipleSelection = false; panel.message = "选择已下载权重或运行包所在目录，应用会检查兼容性并复用，原文件保留。"
        guard panel.runModal() == .OK, let directory = panel.url else { return }
        scanLocalDocumentAssets(in: directory)
    }
}
