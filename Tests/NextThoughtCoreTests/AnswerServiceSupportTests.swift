import Foundation
import Testing
@testable import NextThoughtCore

/// Connecting an answer service: default backend, providers, key input, failures.
struct AnswerServiceSupportTests {
    // MARK: Default backend

    @Test func freshInstallWithoutCodexStartsWithAPI() {
        #expect(AnswerBackendDefault.rawValue(saved: nil, codexInstalled: false) == "openAICompatible")
        #expect(AnswerBackendDefault.rawValue(saved: nil, codexInstalled: true) == "codexAppServer")
    }

    @Test func savedChoiceIsNeverChanged() {
        for saved in ["codexAppServer", "codexExec", "openAICompatible"] {
            #expect(AnswerBackendDefault.rawValue(saved: saved, codexInstalled: false) == saved)
            #expect(AnswerBackendDefault.rawValue(saved: saved, codexInstalled: true) == saved)
        }
        // An unknown stored value counts as "no choice".
        #expect(AnswerBackendDefault.rawValue(saved: "retired", codexInstalled: false) == "openAICompatible")
        #expect(AnswerBackendDefault.rawValue(saved: "", codexInstalled: true) == "codexAppServer")
    }

    // MARK: Providers

    @Test func addressesAreComparedWithoutCaseOrTrailingSlash() {
        #expect(APIProviders.normalized("  HTTPS://API.DeepSeek.com/v1/ ") == "https://api.deepseek.com/v1")
        #expect(APIProviders.guide(forBaseURL: "https://api.deepseek.com/v1/")?.suggestedModel == "deepseek-chat")
        #expect(APIProviders.guide(forBaseURL: "https://example.invalid/v1") == nil)
    }

    @Test func chineseProvidersComeBeforeOpenAI() {
        let deepseek = APIProviders.order(forBaseURL: "https://api.deepseek.com/v1")
        let qwen = APIProviders.order(forBaseURL: "https://dashscope.aliyuncs.com/compatible-mode/v1")
        let openAI = APIProviders.order(forBaseURL: "https://api.openai.com/v1")
        let ollama = APIProviders.order(forBaseURL: "http://localhost:11434/v1")
        #expect(deepseek == 0 && qwen == 1)
        #expect(deepseek < openAI && qwen < openAI && openAI < ollama)
        #expect(APIProviders.order(forBaseURL: "https://example.invalid/v1") == Int.max)
    }

    @Test func onlyKnownKeyPagesAreListed() {
        #expect(APIProviders.guide(forBaseURL: "https://api.openai.com/v1")?.keyPageURL == "https://platform.openai.com/api-keys")
        #expect(APIProviders.guide(forBaseURL: "https://api.deepseek.com/v1")?.keyPageURL == "https://platform.deepseek.com/api_keys")
        #expect(APIProviders.guide(forBaseURL: "https://api.moonshot.cn/v1")?.keyPageURL == "https://platform.moonshot.cn/console/api-keys")
        #expect(APIProviders.guide(forBaseURL: "https://open.bigmodel.cn/api/paas/v4")?.keyPageURL == "https://open.bigmodel.cn/usercenter/apikeys")
        #expect(APIProviders.guide(forBaseURL: "https://openrouter.ai/api/v1")?.keyPageURL == "https://openrouter.ai/keys")
        #expect(APIProviders.guide(forBaseURL: "https://api.groq.com/openai/v1")?.keyPageURL == "https://console.groq.com/keys")
        // Not certain: left out.
        #expect(APIProviders.guide(forBaseURL: "https://dashscope.aliyuncs.com/compatible-mode/v1")?.keyPageURL == nil)
        #expect(APIProviders.guide(forBaseURL: "https://ark.cn-beijing.volces.com/api/v3")?.keyPageURL == nil)
    }

    @Test func localServicesNeedNoKey() {
        #expect(APIProviders.isLocal(baseURL: "http://localhost:11434/v1"))
        #expect(APIProviders.isLocal(baseURL: " http://127.0.0.1:8080/v1 "))
        #expect(!APIProviders.isLocal(baseURL: "https://api.openai.com/v1"))
        #expect(!APIProviders.isLocal(baseURL: "not a url"))
        #expect(APIProviders.guide(forBaseURL: "http://localhost:1234/v1")?.isLocal == true)
    }

    // MARK: Provider switch

    @Test func pickingAProviderReusesAnEmptyPresetOrAddsOne() {
        let first = UUID(), second = UUID()
        let presets = [APIProviderSwitch.PresetRef(id: first, baseURL: "https://api.openai.com/v1"),
                       APIProviderSwitch.PresetRef(id: second, baseURL: "https://api.deepseek.com/v1/")]
        // Already saved: select it, even with a trailing slash.
        #expect(APIProviderSwitch.plan(target: "https://api.deepseek.com/v1", presets: presets, activeID: first, activeIsPristine: true) == .select(second))
        // The active one is already the target.
        #expect(APIProviderSwitch.plan(target: "https://API.openai.com/v1", presets: presets, activeID: first, activeIsPristine: true) == .select(first))
        // New provider: an untouched active preset is reused, a used one is kept.
        #expect(APIProviderSwitch.plan(target: "https://api.moonshot.cn/v1", presets: presets, activeID: first, activeIsPristine: true) == .reuseActive)
        #expect(APIProviderSwitch.plan(target: "https://api.moonshot.cn/v1", presets: presets, activeID: first, activeIsPristine: false) == .addNew)
        #expect(APIProviderSwitch.plan(target: "https://api.moonshot.cn/v1", presets: [], activeID: nil, activeIsPristine: false) == .addNew)
    }

    // MARK: Key input

    @Test func pastedKeysAreCleanedAndSentencesRejected() {
        #expect(APIKeyInput.clean("  sk-abc123def456\n") == "sk-abc123def456")
        #expect(APIKeyInput.clean("sk-short") == "sk-short")        // exactly 8 characters
        #expect(APIKeyInput.clean("sk-abc1") == nil)                 // 7 characters
        #expect(APIKeyInput.clean("") == nil)
        #expect(APIKeyInput.clean("my key is sk-abc123def456") == nil)
        #expect(APIKeyInput.clean("sk-abc123\nsk-def456") == nil)
        #expect(APIKeyInput.clean("这里是我的密钥abcdefgh") == nil)
        #expect(APIKeyInput.clean(String(repeating: "a", count: 401)) == nil)
        #expect(APIKeyInput.clean(String(repeating: "a", count: 400)) != nil)
    }

    @Test func maskedKeyShowsOnlyTheEnds() {
        #expect(APIKeyInput.masked("sk-abcdefgh1234") == "sk-" + String(repeating: "•", count: 8) + "1234")
        #expect(APIKeyInput.masked("short") == String(repeating: "•", count: 8))
        #expect(APIKeyInput.masked("  sk-abcdefgh1234 ") == "sk-" + String(repeating: "•", count: 8) + "1234")
    }

    // MARK: Failures

    @Test func httpStatusesMapToPlainCauses() {
        #expect(APIFailureExplainer.classify(status: 401) == .badKey)
        #expect(APIFailureExplainer.classify(status: 403) == .forbidden)
        #expect(APIFailureExplainer.classify(status: 402) == .outOfCredit)
        #expect(APIFailureExplainer.classify(status: 404) == .badAddress)
        #expect(APIFailureExplainer.classify(status: 404, body: "Not Found") == .badAddress)
        #expect(APIFailureExplainer.classify(status: 429, body: "Rate limit reached for requests") == .rateLimited)
        #expect(APIFailureExplainer.classify(status: 500) == .serverBusy)
        #expect(APIFailureExplainer.classify(status: 503) == .serverBusy)
        #expect(APIFailureExplainer.classify(status: 418) == .rejected(418))
        #expect(APIFailureExplainer.classify(status: nil) == .unknown)
    }

    @Test func bodiesSeparateAWrongModelFromAWrongAddress() {
        let missing = #"{"error":{"message":"The model `gpt-9` does not exist","code":"model_not_found"}}"#
        #expect(APIFailureExplainer.classify(status: 404, body: missing) == .badModel)
        #expect(APIFailureExplainer.classify(status: 400, body: "model xyz is not supported") == .badModel)
        #expect(APIFailureExplainer.classify(status: 404, body: #"{"error":"model_not_found"}"#) == .badModel)
        #expect(APIFailureExplainer.classify(status: 400, body: "max_tokens is too large") == .rejected(400))
        #expect(APIFailureExplainer.classify(status: 400, body: "Invalid API key provided") == .badKey)
        #expect(APIFailureExplainer.classify(status: 429, body: "You exceeded your current quota") == .outOfCredit)
        #expect(APIFailureExplainer.classify(status: 429, body: "账户余额不足") == .outOfCredit)
    }

    @Test func networkErrorsMapToPlainCauses() {
        #expect(APIFailureExplainer.classify(status: nil, urlErrorCode: -1001) == .timeout)
        #expect(APIFailureExplainer.classify(status: nil, urlErrorCode: -1009) == .offline)
        #expect(APIFailureExplainer.classify(status: nil, urlErrorCode: -1005) == .offline)
        #expect(APIFailureExplainer.classify(status: nil, urlErrorCode: -1003) == .cannotReachHost)
        #expect(APIFailureExplainer.classify(status: nil, urlErrorCode: -1004) == .cannotReachHost)
        #expect(APIFailureExplainer.classify(status: nil, urlErrorCode: -1200) == .secureConnection)
        #expect(APIFailureExplainer.classify(status: nil, urlErrorCode: -1202) == .secureConnection)
        #expect(APIFailureExplainer.classify(status: nil, urlErrorCode: -999) == .unknown)
        // A network error wins over a stale status.
        #expect(APIFailureExplainer.classify(status: 200, urlErrorCode: -1001) == .timeout)
    }

    @Test func everyCauseHasItsOwnSentence() {
        let causes: [APIFailure] = [.badKey, .forbidden, .badAddress, .cannotReachHost, .badModel, .outOfCredit, .rateLimited,
                                    .serverBusy, .timeout, .offline, .secureConnection, .rejected(400), .unknown]
        let sentences = causes.map { APIFailureExplainer.message($0) }
        #expect(sentences.allSatisfy { !$0.isEmpty })
        #expect(Set(sentences).count == causes.count)
        #expect(APIFailureExplainer.message(.badKey).contains("Key"))
        #expect(APIFailureExplainer.message(.badAddress).contains("地址"))
        #expect(APIFailureExplainer.message(.timeout).contains("网络"))
    }

    @Test func modelListFailuresDoNotBlameTheAddress() {
        #expect(APIFailureExplainer.modelListMessage(status: 401) == APIFailureExplainer.message(.badKey))
        #expect(APIFailureExplainer.modelListMessage(status: 404).contains("没有提供模型列表"))
        #expect(APIFailureExplainer.modelListMessage(status: 405).contains("没有提供模型列表"))
        #expect(!APIFailureExplainer.modelListMessage(status: 404).contains("地址不对"))
    }

    // MARK: Sample clock

    @Test func scriptItemsAppearAtTheirTime() {
        let times = [0.6, 4.0, 9.0]
        #expect(ScriptTimeline.visibleCount(times: times, at: 0) == 0)
        #expect(ScriptTimeline.visibleCount(times: times, at: 0.6) == 1)
        #expect(ScriptTimeline.visibleCount(times: times, at: 8.99) == 2)
        #expect(ScriptTimeline.visibleCount(times: times, at: 100) == 3)
        #expect(ScriptTimeline.visibleCount(times: [], at: 5) == 0)
        #expect(ScriptTimeline.visibleCount(times: times, at: -3) == 0)
    }

    @Test func progressIsClamped() {
        #expect(ScriptTimeline.progress(elapsed: 15, duration: 30) == 0.5)
        #expect(ScriptTimeline.progress(elapsed: -1, duration: 30) == 0)
        #expect(ScriptTimeline.progress(elapsed: 45, duration: 30) == 1)
        #expect(ScriptTimeline.progress(elapsed: 1, duration: 0) == 1)
    }
}
