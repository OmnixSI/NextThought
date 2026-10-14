import Foundation

struct DiscoveredWhisperModel: Identifiable, Sendable {
    let url: URL
    let bytes: Int64
    var id: String { url.path }
    var name: String { url.deletingPathExtension().lastPathComponent }
    var format: String { url.pathExtension.lowercased() == "pt" ? "Whisper · 需本机转换" : "whisper.cpp · 可直接导入" }
}

enum LocalModelScanner {
    /// Search model caches only; do not crawl the user's documents or load weights.
    static func scan() -> [DiscoveredWhisperModel] {
        let fm = FileManager.default
        let home = fm.homeDirectoryForCurrentUser
        var roots = [
            home.appendingPathComponent(".cache/whisper"),
            home.appendingPathComponent(".local/share/whisper"),
            home.appendingPathComponent(".cache/whisper.cpp"),
            home.appendingPathComponent("Library/Application Support/whisper.cpp"),
            home.appendingPathComponent("Library/Caches/whisper"),
            home.appendingPathComponent("Library/Application Support/MacWhisper"),
            LocalWhisperService.modelsDirectory,
            URL(fileURLWithPath: "/opt/homebrew/share/whisper"),
            URL(fileURLWithPath: "/usr/local/share/whisper")
        ]
        let environment = ProcessInfo.processInfo.environment
        if let cache = environment["XDG_CACHE_HOME"] { roots.append(URL(fileURLWithPath: cache).appendingPathComponent("whisper")) }
        var hubs = [home.appendingPathComponent(".cache/huggingface/hub")]
        if let hf = environment["HF_HOME"] { hubs.append(URL(fileURLWithPath: hf).appendingPathComponent("hub")) }
        if let hub = environment["HUGGINGFACE_HUB_CACHE"] { hubs.append(URL(fileURLWithPath: hub)) }
        for hub in hubs {
            roots += ((try? fm.contentsOfDirectory(at: hub, includingPropertiesForKeys: nil)) ?? [])
                .filter { $0.lastPathComponent.lowercased().contains("whisper") }
        }
        var found: [String: DiscoveredWhisperModel] = [:]
        let keys: [URLResourceKey] = [.isRegularFileKey, .isDirectoryKey, .fileSizeKey]
        for root in roots {
            guard let enumerator = fm.enumerator(at: root, includingPropertiesForKeys: keys, options: [.skipsPackageDescendants]) else { continue }
            while let url = enumerator.nextObject() as? URL {
                if enumerator.level > 8 { enumerator.skipDescendants(); continue }
                let ext = url.pathExtension.lowercased()
                guard ext == "pt" || (ext == "bin" && (url.lastPathComponent.hasPrefix("ggml-") || url.lastPathComponent.hasPrefix("imported-"))) else { continue }
                let resolved = url.resolvingSymlinksInPath()
                guard let values = try? resolved.resourceValues(forKeys: Set(keys)), values.isRegularFile == true,
                      let size = values.fileSize, size > 1_000_000 else { continue }
                found[resolved.path] = DiscoveredWhisperModel(url: url, bytes: Int64(size))
            }
        }
        return found.values.sorted { $0.url.path.localizedStandardCompare($1.url.path) == .orderedAscending }
    }
}
