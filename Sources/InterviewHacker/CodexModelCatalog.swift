import Foundation

struct CodexModelOption: Identifiable, Sendable {
    let id: String
    let name: String
    let efforts: [String]
    let defaultEffort: String
    /// Description of the Fast service tier (e.g. "1.5x speed, increased usage"); nil when the model has none.
    var fastTier: String? = nil
    /// Some models run Fast unless asked for standard speed.
    var defaultsToFast = false

    /// "1.5x speed, increased usage" → "约 1.5 倍速度 · 用量更多".
    var fastLabel: String {
        guard let fastTier else { return "" }
        var parts: [String] = []
        if let match = fastTier.range(of: #"[0-9.]+(?=x)"#, options: .regularExpression) { parts.append("约 \(fastTier[match]) 倍速度") }
        if fastTier.lowercased().contains("usage") { parts.append("用量更多") }
        return parts.isEmpty ? fastTier : parts.joined(separator: " · ")
    }
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
            // Same check as Codex itself: a "priority" tier, or the older "fast" speed tier.
            let tiers = model["service_tiers"] as? [[String: Any]] ?? []
            let priority = tiers.first { $0["id"] as? String == "priority" }
            let legacyFast = (model["additional_speed_tiers"] as? [String] ?? []).contains("fast")
            let fastTier = priority.map { $0["description"] as? String ?? "Fast" } ?? (legacyFast ? "1.5x speed, increased usage" : nil)
            return CodexModelOption(id: id, name: model["display_name"] as? String ?? id,
                                    efforts: efforts, defaultEffort: efforts.contains(preferred) ? preferred : efforts[0],
                                    fastTier: fastTier, defaultsToFast: model["default_service_tier"] as? String == "priority")
        }
    }
}
