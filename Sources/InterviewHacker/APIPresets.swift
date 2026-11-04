import Foundation
import InterviewHackerCore

/// A saved OpenAI-compatible provider: address, model, thinking strength; its key
/// lives in the Keychain under `keyAccount`.
struct APIPreset: Codable, Identifiable, Equatable {
    var id = UUID()
    var name: String
    var baseURL: String
    var model: String = ""
    /// "default" (not sent), or "minimal" / "low" / "medium" / "high" as `reasoning_effort`.
    var effort: String = "default"
    var keyAccount: String

    init(id: UUID = UUID(), name: String, baseURL: String, model: String = "", effort: String = "default", keyAccount: String? = nil) {
        self.id = id
        self.name = name
        self.baseURL = baseURL
        self.model = model
        self.effort = effort
        self.keyAccount = keyAccount ?? "answer.api.\(id.uuidString)"
    }

    static let efforts: [(String, String)] = [("default", "默认（不指定）"), ("minimal", "minimal · 最快"), ("low", "low"), ("medium", "medium"), ("high", "high · 最强")]

    /// Common providers for “新建预设”.
    static let templates: [(name: String, url: String)] = [
        ("OpenAI", "https://api.openai.com/v1"),
        ("DeepSeek", "https://api.deepseek.com/v1"),
        ("通义千问", "https://dashscope.aliyuncs.com/compatible-mode/v1"),
        ("Moonshot Kimi", "https://api.moonshot.cn/v1"),
        ("智谱 GLM", "https://open.bigmodel.cn/api/paas/v4"),
        ("火山方舟 豆包", "https://ark.cn-beijing.volces.com/api/v3"),
        ("Groq", "https://api.groq.com/openai/v1"),
        ("OpenRouter", "https://openrouter.ai/api/v1"),
        ("本机 Ollama", "http://localhost:11434/v1"),
        ("本机 LM Studio", "http://localhost:1234/v1"),
    ]

    static func loadAll() -> [APIPreset] {
        guard let data = UserDefaults.standard.data(forKey: "apiPresets"),
              let presets = try? JSONDecoder().decode([APIPreset].self, from: data) else { return [] }
        return presets
    }

    static func saveAll(_ presets: [APIPreset]) {
        if let data = try? JSONEncoder().encode(presets) { UserDefaults.standard.set(data, forKey: "apiPresets") }
    }
}

@MainActor
extension AppState {
    var activeAPIPreset: APIPreset? { apiPresets.first { $0.id == activeAPIPresetID } }

    /// First run with presets: the existing single API configuration becomes “默认”.
    func loadAPIPresets() {
        var presets = APIPreset.loadAll()
        if presets.isEmpty {
            let host = URL(string: apiBaseURL)?.host ?? "默认"
            let name = APIPreset.templates.first { URL(string: $0.url)?.host == host }?.name ?? "默认"
            presets = [APIPreset(name: name, baseURL: apiBaseURL, model: apiModel, keyAccount: "answer.api")]
            APIPreset.saveAll(presets)
        }
        apiPresets = presets
        let saved = UserDefaults.standard.string(forKey: "activeAPIPresetID").flatMap(UUID.init(uuidString:))
        let active = presets.first { $0.id == saved } ?? presets[0]
        activeAPIPresetID = active.id
        apply(active)
    }

    private func apply(_ preset: APIPreset) {
        apiBaseURL = preset.baseURL
        apiModel = preset.model
        apiReasoningEffort = preset.effort
        apiKey = KeychainStore.read(preset.keyAccount)
        apiModelChoices = []
        apiModelsStatus = ""
    }

    /// Writes the edited fields back into the active preset.
    func storeActiveAPIPreset() {
        guard let index = apiPresets.firstIndex(where: { $0.id == activeAPIPresetID }) else { return }
        apiPresets[index].baseURL = apiBaseURL
        apiPresets[index].model = apiModel
        apiPresets[index].effort = apiReasoningEffort
        APIPreset.saveAll(apiPresets)
        UserDefaults.standard.set(activeAPIPresetID?.uuidString, forKey: "activeAPIPresetID")
    }

    func selectAPIPreset(_ id: UUID) {
        guard id != activeAPIPresetID, let preset = apiPresets.first(where: { $0.id == id }) else { return }
        storeActiveAPIPreset()
        activeAPIPresetID = id
        apply(preset)
        UserDefaults.standard.set(id.uuidString, forKey: "activeAPIPresetID")
        UserDefaults.standard.set(apiBaseURL, forKey: "apiBaseURL")
        UserDefaults.standard.set(apiModel, forKey: "apiModel")
        engineTest = .idle
        Diagnostics.record("api.preset_selected")
    }

    func addAPIPreset(name: String, baseURL: String) {
        storeActiveAPIPreset()
        var unique = name, counter = 2
        while apiPresets.contains(where: { $0.name == unique }) { unique = "\(name) \(counter)"; counter += 1 }
        let preset = APIPreset(name: unique, baseURL: baseURL)
        apiPresets.append(preset)
        APIPreset.saveAll(apiPresets)
        selectAPIPreset(preset.id)
    }

    func renameAPIPreset(_ id: UUID, to name: String) {
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, let index = apiPresets.firstIndex(where: { $0.id == id }) else { return }
        apiPresets[index].name = trimmed
        APIPreset.saveAll(apiPresets)
    }

    func deleteAPIPreset(_ id: UUID) {
        guard apiPresets.count > 1, let preset = apiPresets.first(where: { $0.id == id }) else { return }
        KeychainStore.write("", account: preset.keyAccount)
        apiPresets.removeAll { $0.id == id }
        APIPreset.saveAll(apiPresets)
        if activeAPIPresetID == id {
            activeAPIPresetID = nil
            selectAPIPreset(apiPresets[0].id)
        }
    }

    /// `reasoning_effort` to send, or nil for the provider default.
    var apiEffortParameter: String? { apiReasoningEffort == "default" ? nil : apiReasoningEffort }

    /// GET {base}/models — most OpenAI-compatible services list their models there.
    func fetchAPIModels() async {
        guard let endpoint = ChatCompletionStreamParser.endpoint(from: apiBaseURL),
              let models = URL(string: endpoint.absoluteString.replacingOccurrences(of: "/chat/completions", with: "/models")) else {
            apiModelsStatus = "API 地址无效"; return
        }
        apiModelsStatus = "正在获取…"
        var request = URLRequest(url: models, timeoutInterval: 15)
        if !apiKey.isEmpty { request.setValue("Bearer \(apiKey)", forHTTPHeaderField: "Authorization") }
        do {
            let (data, response) = try await URLSession.shared.data(for: request)
            let status = (response as? HTTPURLResponse)?.statusCode ?? 0
            guard (200..<300).contains(status) else {
                apiModelsStatus = String(localized: String.LocalizationValue(APIFailureExplainer.modelListMessage(status: status)))
                return
            }
            let ids = ModelListParser.ids(from: data)
            apiModelChoices = ids
            apiModelsStatus = ids.isEmpty ? "服务没有返回模型列表，请手动填写模型 ID" : "共 \(ids.count) 个模型"
        } catch {
            apiModelsStatus = AnswerServiceError.explain(error)
        }
    }
}
