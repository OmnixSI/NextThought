import Foundation

struct CodexModelOption: Identifiable, Sendable {
    let id: String
    let name: String
    let efforts: [String]
    let defaultEffort: String
}

enum CodexModelCatalog {
    static func load() -> [CodexModelOption] {
        let directory = ProcessInfo.processInfo.environment["CODEX_HOME"].map { URL(fileURLWithPath: $0) }
            ?? FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".codex")
        guard let data = try? Data(contentsOf: directory.appendingPathComponent("models_cache.json")),
              let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let models = root["models"] as? [[String: Any]] else { return [] }
        var seen = Set<String>()
        return models.compactMap { model in
            guard let id = model["slug"] as? String, model["visibility"] as? String == "list", seen.insert(id).inserted else { return nil }
            let allowed = Set(["none", "minimal", "low", "medium", "high", "xhigh", "max", "ultra"])
            let efforts = (model["supported_reasoning_levels"] as? [[String: Any]] ?? []).compactMap { $0["effort"] as? String }.filter { allowed.contains($0) }
            guard !efforts.isEmpty else { return nil }
            let preferred = model["default_reasoning_level"] as? String ?? ""
            return CodexModelOption(id: id, name: model["display_name"] as? String ?? id,
                                    efforts: efforts, defaultEffort: efforts.contains(preferred) ? preferred : efforts[0])
        }
    }
}
