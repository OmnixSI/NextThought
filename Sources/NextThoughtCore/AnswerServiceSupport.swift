import Foundation

// Pure rules behind "connect an answer service": which backend a fresh install
// starts with, the known OpenAI-compatible providers, how a pasted key is
// cleaned, and how a failed request is explained in plain words.

// MARK: - Default backend

/// Which answer backend is used when the user has not chosen one yet. The raw
/// values are the ones of the app's `AnswerBackend` enum.
public enum AnswerBackendDefault {
    public static let codexAppServer = "codexAppServer"
    public static let codexExec = "codexExec"
    public static let openAICompatible = "openAICompatible"

    /// A saved choice always wins (existing users keep theirs). Without one, an API
    /// key is the default unless Codex is already installed on this Mac.
    public static func rawValue(saved: String?, codexInstalled: Bool) -> String {
        if let saved, [codexAppServer, codexExec, openAICompatible].contains(saved) { return saved }
        return codexInstalled ? codexAppServer : openAICompatible
    }
}

// MARK: - Providers

/// What the app knows about a well-known OpenAI-compatible service. Only facts
/// that are certain are listed: an unknown model name is left empty, so the
/// "获取列表" button fetches the real list from the service instead.
public struct APIProviderGuide: Equatable, Sendable {
    /// Position in the provider picker; Chinese users' common services come first.
    public var order: Int
    /// The service's own page for creating a key. nil when not known for certain.
    public var keyPageURL: String?
    /// A model name that is safe to pre-fill, or "".
    public var suggestedModel: String
    /// A one-line hint for the model field.
    public var modelHint: String?
    /// A service on this Mac: no key needed.
    public var isLocal: Bool
}

public enum APIProviders {
    /// Lower-cased, trimmed, without a trailing slash: the form addresses are compared in.
    public static func normalized(_ baseURL: String) -> String {
        var value = baseURL.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        while value.hasSuffix("/") { value.removeLast() }
        return value
    }

    private static let table: [String: APIProviderGuide] = [
        "https://api.deepseek.com/v1": .init(order: 0, keyPageURL: "https://platform.deepseek.com/api_keys",
                                             suggestedModel: "deepseek-chat", modelHint: nil, isLocal: false),
        "https://dashscope.aliyuncs.com/compatible-mode/v1": .init(order: 1, keyPageURL: nil,
                                                                   suggestedModel: "qwen-plus", modelHint: nil, isLocal: false),
        "https://api.moonshot.cn/v1": .init(order: 2, keyPageURL: "https://platform.moonshot.cn/console/api-keys",
                                            suggestedModel: "", modelHint: nil, isLocal: false),
        "https://open.bigmodel.cn/api/paas/v4": .init(order: 3, keyPageURL: "https://open.bigmodel.cn/usercenter/apikeys",
                                                      suggestedModel: "glm-4-flash", modelHint: nil, isLocal: false),
        "https://ark.cn-beijing.volces.com/api/v3": .init(order: 4, keyPageURL: nil, suggestedModel: "",
                                                          modelHint: "填方舟控制台里的接入点 ID（ep- 开头）或模型名", isLocal: false),
        "https://api.openai.com/v1": .init(order: 5, keyPageURL: "https://platform.openai.com/api-keys",
                                           suggestedModel: "", modelHint: nil, isLocal: false),
        "https://openrouter.ai/api/v1": .init(order: 6, keyPageURL: "https://openrouter.ai/keys",
                                              suggestedModel: "", modelHint: nil, isLocal: false),
        "https://api.groq.com/openai/v1": .init(order: 7, keyPageURL: "https://console.groq.com/keys",
                                                suggestedModel: "", modelHint: nil, isLocal: false),
        "http://localhost:11434/v1": .init(order: 8, keyPageURL: nil, suggestedModel: "",
                                           modelHint: "填 ollama pull 下载过的模型名", isLocal: true),
        "http://localhost:1234/v1": .init(order: 9, keyPageURL: nil, suggestedModel: "",
                                          modelHint: "填 LM Studio 里已加载的模型名", isLocal: true),
    ]

    /// The guide for a known provider address, or nil for any other address.
    public static func guide(forBaseURL baseURL: String) -> APIProviderGuide? {
        table[normalized(baseURL)]
    }

    /// Sort key for the picker; unknown addresses go last.
    public static func order(forBaseURL baseURL: String) -> Int {
        guide(forBaseURL: baseURL)?.order ?? Int.max
    }

    /// True for an address on this Mac, where no key is needed.
    public static func isLocal(baseURL: String) -> Bool {
        guard let host = URLComponents(string: baseURL.trimmingCharacters(in: .whitespacesAndNewlines))?.host?.lowercased() else { return false }
        return ["localhost", "127.0.0.1", "::1", "[::1]"].contains(host)
    }
}

/// What picking a provider in the picker does to the saved presets.
public enum APIProviderSwitch: Equatable, Sendable {
    /// A preset for this address already exists: use it (it keeps its key and model).
    case select(UUID)
    /// The active preset is still empty (no key, no model): turn it into this provider
    /// instead of leaving an unused preset behind.
    case reuseActive
    /// Keep the active preset and create another one.
    case addNew

    public struct PresetRef: Equatable, Sendable {
        public var id: UUID
        public var baseURL: String
        public init(id: UUID, baseURL: String) { self.id = id; self.baseURL = baseURL }
    }

    public static func plan(target: String, presets: [PresetRef], activeID: UUID?, activeIsPristine: Bool) -> APIProviderSwitch {
        let wanted = APIProviders.normalized(target)
        if let active = presets.first(where: { $0.id == activeID }), APIProviders.normalized(active.baseURL) == wanted {
            return .select(active.id)
        }
        if let existing = presets.first(where: { APIProviders.normalized($0.baseURL) == wanted }) { return .select(existing.id) }
        return activeIsPristine ? .reuseActive : .addNew
    }
}

// MARK: - Key input

public enum APIKeyInput {
    /// A pasted or typed key without the whitespace around it, or nil when it cannot
    /// be a key (a sentence, a multi-line paste, a very short or very long text).
    public static func clean(_ raw: String) -> String? {
        let trimmed = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmed.count >= 8, trimmed.count <= 400 else { return nil }
        guard !trimmed.contains(where: { $0.isWhitespace }) else { return nil }
        guard trimmed.unicodeScalars.allSatisfy({ $0.isASCII }) else { return nil }
        return trimmed
    }

    /// "sk-••••••••3f9a" for display; a short value is fully hidden.
    public static func masked(_ key: String) -> String {
        let trimmed = key.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmed.count > 8 else { return String(repeating: "•", count: 8) }
        return String(trimmed.prefix(3)) + String(repeating: "•", count: 8) + String(trimmed.suffix(4))
    }
}

// MARK: - Failures

public enum APIFailure: Equatable, Sendable {
    case badKey, forbidden, badAddress, cannotReachHost, badModel, outOfCredit, rateLimited
    case serverBusy, timeout, offline, secureConnection
    /// Any other HTTP status.
    case rejected(Int)
    case unknown
}

/// Turns an HTTP status, a response body or a URLSession error code into a cause
/// and a sentence a non-technical person can act on.
public enum APIFailureExplainer {
    // NSURLError codes, kept as numbers so this file needs nothing but Foundation.
    private static let timedOut = -1001
    private static let cannotFindHost = -1003
    private static let cannotConnectToHost = -1004
    private static let connectionLost = -1005
    private static let dnsLookupFailed = -1006
    private static let notConnected = -1009

    private static let keyWords = ["api key", "apikey", "api_key", "incorrect key", "invalid key", "authentication", "unauthorized", "密钥", "api-key"]
    private static let modelProblemWords = ["not found", "not_found", "does not exist", "not exist", "invalid", "unknown", "not support", "unsupported",
                                            "no access", "不存在", "无效", "不支持", "无权"]
    private static let creditWords = ["insufficient", "quota", "balance", "credit", "billing", "余额", "额度", "欠费", "充值"]

    public static func classify(status: Int?, body: String = "", urlErrorCode: Int? = nil) -> APIFailure {
        if let code = urlErrorCode {
            switch code {
            case timedOut: return .timeout
            case cannotFindHost, dnsLookupFailed: return .cannotReachHost
            case cannotConnectToHost: return .cannotReachHost
            case connectionLost, notConnected: return .offline
            case -1206 ... -1200: return .secureConnection
            default: return .unknown
            }
        }
        guard let status else { return .unknown }
        let lower = body.lowercased()
        let mentionsModel = lower.contains("model") || body.contains("模型")
        let modelProblem = mentionsModel && modelProblemWords.contains { lower.contains($0) || body.contains($0) }
        let creditProblem = creditWords.contains { lower.contains($0) || body.contains($0) }
        switch status {
        case 401: return .badKey
        case 403: return .forbidden
        case 402: return .outOfCredit
        case 404: return modelProblem ? .badModel : .badAddress
        case 400, 422:
            if keyWords.contains(where: { lower.contains($0) || body.contains($0) }), lower.contains("invalid") || lower.contains("incorrect") || lower.contains("not valid") || body.contains("无效") {
                return .badKey
            }
            return modelProblem ? .badModel : .rejected(status)
        case 429: return creditProblem ? .outOfCredit : .rateLimited
        case 500 ... 599: return .serverBusy
        default: return .rejected(status)
        }
    }

    /// The sentence shown to the user (Simplified Chinese; the app localizes it).
    /// `rejected` has no fixed sentence: the app adds the HTTP code itself.
    public static func message(_ failure: APIFailure) -> String {
        switch failure {
        case .badKey: "Key 不对。检查是否复制完整，或到服务商后台重新创建一个。"
        case .forbidden: "这个 Key 没有权限，或所在地区暂不支持该服务。"
        case .badAddress: "地址不对。检查接口地址是否写对，通常以 /v1 之类的版本号结尾。"
        case .cannotReachHost: "连不上这个地址。检查地址是否写对；本机服务（Ollama、LM Studio）请先启动。"
        case .badModel: "模型名不对，或这个 Key 没有该模型的权限。点“获取列表”重新选一个。"
        case .outOfCredit: "余额或额度用完了。到服务商后台充值，或换一个 Key。"
        case .rateLimited: "请求太频繁，服务商暂时限流。Key 本身没问题，稍后再试。"
        case .serverBusy: "服务商那边暂时出错。稍后再试。"
        case .timeout: "等了太久没有回应。检查网络或代理，或换一个更快的服务商。"
        case .offline: "网络不通。检查这台 Mac 的网络连接或代理。"
        case .secureConnection: "安全连接失败。检查系统时间，或代理和公司网络是否拦截了 HTTPS。"
        case .rejected: "服务商拒绝了这次请求。"
        case .unknown: "检查没有通过。"
        }
    }

    /// Why fetching the model list failed. Many services have no list at all, which
    /// is not an error in the address or the key.
    public static func modelListMessage(status: Int) -> String {
        switch status {
        case 401, 403: message(.badKey)
        case 404, 405: "这个服务没有提供模型列表。请直接填写模型名。"
        case 429: message(.rateLimited)
        case 500 ... 599: message(.serverBusy)
        default: "获取模型列表失败。可以直接填写模型名。"
        }
    }
}

// MARK: - Sample meeting clock

/// Which items of a scripted replay are visible `elapsed` seconds after it starts.
public enum ScriptTimeline {
    /// How many items (sorted by time) have appeared. `times` must be ascending.
    public static func visibleCount(times: [Double], at elapsed: Double) -> Int {
        times.prefix { $0 <= elapsed }.count
    }

    /// 0...1 progress through a replay of `duration` seconds.
    public static func progress(elapsed: Double, duration: Double) -> Double {
        guard duration > 0 else { return 1 }
        return min(max(elapsed / duration, 0), 1)
    }
}
