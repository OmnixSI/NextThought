import Foundation
import InterviewHackerCore
import Observation

/// Optional judge model (“JEV”) for decisions the local rules are unsure about:
/// is this a question to answer, does this question need a web search. Any
/// OpenAI-compatible endpoint works; the contract is in docs/JUDGE-API.md. Every call
/// has a short deadline and falls back to the local rules, so a slow or missing
/// judge never delays an answer.
@MainActor
@Observable
final class JudgeService {
    static let keyAccount = "judge.api"

    var enabled = UserDefaults.standard.bool(forKey: "judgeEnabled")
    var baseURL = UserDefaults.standard.string(forKey: "judgeBaseURL") ?? ""
    var model = UserDefaults.standard.string(forKey: "judgeModel") ?? ""
    var key = KeychainStore.read(JudgeService.keyAccount)
    var forQuestions = UserDefaults.standard.object(forKey: "judgeForQuestions") as? Bool ?? true
    var forWebSearch = UserDefaults.standard.object(forKey: "judgeForWebSearch") as? Bool ?? true
    /// Deadline per decision; past it the local rule result stands.
    var timeout: Double = 1.5
    private(set) var status = ""

    @ObservationIgnored private let client = OpenAICompatibleClient(resourceTimeout: 10)

    var configured: Bool { !baseURL.trimmingCharacters(in: .whitespaces).isEmpty && !model.trimmingCharacters(in: .whitespaces).isEmpty }
    var handlesQuestions: Bool { enabled && configured && forQuestions }
    var handlesWebSearch: Bool { enabled && configured && forWebSearch }

    func save() {
        let defaults = UserDefaults.standard
        defaults.set(enabled, forKey: "judgeEnabled")
        defaults.set(baseURL, forKey: "judgeBaseURL")
        defaults.set(model, forKey: "judgeModel")
        defaults.set(forQuestions, forKey: "judgeForQuestions")
        defaults.set(forWebSearch, forKey: "judgeForWebSearch")
        KeychainStore.write(key.trimmingCharacters(in: .whitespacesAndNewlines), account: Self.keyAccount)
    }

    func isQuestion(_ text: String, context: String, mode: SessionMode, ruleScore: Double) async -> JudgeProtocol.Verdict? {
        await decide(.questionDetection, text: text, context: context, mode: mode.rawValue, ruleScore: ruleScore)
    }

    func needsWebSearch(_ question: String) async -> Bool? {
        let rules = WebSearchNeed.assess(question)
        return await decide(.webSearchNeed, text: question, context: "", mode: "general", ruleScore: rules.score)?.yes
    }

    /// Sends a known-easy case and reports the latency.
    func test() async {
        status = "正在测试…"
        let started = Date()
        let saved = timeout
        timeout = 8
        defer { timeout = saved }
        if let verdict = await decide(.webSearchNeed, text: "今天美元兑人民币汇率是多少？", context: "", mode: "general", ruleScore: 0.9) {
            status = String(format: "可以使用 · %.2f 秒 · 判断为“%@”", Date().timeIntervalSince(started), verdict.yes ? "需要联网" : "不需要联网")
        } else if status == "正在测试…" {
            status = "没有得到可解析的回复（需要返回 {\"answer\": true/false} 的 JSON）"
        }
    }

    private func decide(_ task: JudgeTask, text: String, context: String, mode: String, ruleScore: Double) async -> JudgeProtocol.Verdict? {
        guard configured else { return nil }
        let parts = JudgeProtocol.request(task: task, text: text, context: context, mode: mode, ruleScore: ruleScore, today: AnswerPromptBuilder.todayText())
        let request = AnswerRequest(system: parts.system, user: parts.user, model: model, effort: "low")
        let client = client, base = baseURL, key = key, deadline = timeout
        let started = Date()
        do {
            let reply = try await withThrowingTaskGroup(of: String.self) { group in
                group.addTask { try await client.answer(request, baseURL: base, apiKey: key) { _ in } }
                group.addTask { try await Task.sleep(for: .seconds(deadline)); throw AnswerEngineError.timeout }
                defer { group.cancelAll() }
                return try await group.next() ?? ""
            }
            let verdict = JudgeProtocol.parse(reply)
            Diagnostics.record("judge.decided", ["task": task.rawValue, "seconds": Date().timeIntervalSince(started), "parsed": verdict != nil])
            return verdict
        } catch {
            if case AnswerEngineError.timeout = error { status = String(format: "上次判断超过 %.1f 秒，已按本地规则处理", deadline) }
            else { status = "判断模型出错：\(error.localizedDescription)" }
            Diagnostics.record("judge.failed", ["task": task.rawValue])
            return nil
        }
    }
}
