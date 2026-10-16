import Foundation
import InterviewHackerCore

/// Who searches the web for the background “联网” answer.
enum WebSearchProvider: String, CaseIterable, Identifiable {
    /// Codex's own live web search (ChatGPT sign-in, no extra key).
    case codex
    /// Tavily search API; results are handed to the current answer engine.
    case tavily
    var id: String { rawValue }
    var title: String {
        switch self {
        case .codex: "Codex 联网搜索（ChatGPT 账号）"
        case .tavily: "Tavily 搜索 API（任意回答引擎）"
        }
    }
}

/// A background lookup for one answer (keyed by the answer's id).
struct WebLookup: Equatable {
    enum Status: Equatable { case searching, done, failed(String) }
    var status: Status = .searching
    var question: String
    var answer = ""
    var sources: [WebResult] = []
    var seconds: Double?
    var provider: WebSearchProvider
    var startedAt = Date()
}

enum TavilyClient {
    static let keyAccount = "websearch.tavily"

    static func search(_ query: String, key: String) async throws -> [WebResult] {
        var request = URLRequest(url: URL(string: "https://api.tavily.com/search")!, timeoutInterval: 20)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("Bearer \(key)", forHTTPHeaderField: "Authorization")
        let body: [String: Any] = ["query": String(query.prefix(380)), "max_results": 6, "search_depth": "basic", "include_answer": false]
        request.httpBody = try JSONSerialization.data(withJSONObject: body)
        let (data, response) = try await URLSession.shared.data(for: request)
        let status = (response as? HTTPURLResponse)?.statusCode ?? 0
        guard (200..<300).contains(status) else {
            throw AnswerEngineError.http(status, String(decoding: data.prefix(300), as: UTF8.self))
        }
        let root = try JSONSerialization.jsonObject(with: data) as? [String: Any]
        return (root?["results"] as? [[String: Any]] ?? []).compactMap { item in
            guard let url = item["url"] as? String else { return nil }
            return WebResult(title: item["title"] as? String ?? url, url: url, snippet: item["content"] as? String ?? "")
        }
    }
}

@MainActor
extension AppState {
    // MARK: Displayed answer

    /// The answer record on screen: the browsed history entry, else the latest.
    var displayedRecordID: UUID? {
        if let historyCursor, answerHistory.indices.contains(historyCursor) { return answerHistory[historyCursor].id }
        return currentAnswerID
    }

    var displayedWebLookup: WebLookup? { displayedRecordID.flatMap { webLookups[$0] } }

    /// The web-updated version replaces the quick answer unless the user switched back.
    var showsWebAnswer: Bool {
        guard let id = displayedRecordID, let lookup = webLookups[id], lookup.status == .done, !lookup.answer.isEmpty else { return false }
        return !showOriginalAnswer.contains(id)
    }

    func toggleWebAnswer() {
        guard let id = displayedRecordID else { return }
        if showOriginalAnswer.contains(id) { showOriginalAnswer.remove(id) } else { showOriginalAnswer.insert(id) }
    }

    var webSearchAvailable: Bool {
        switch webSearchProvider {
        case .codex: codexReady
        case .tavily: !tavilyKey.isEmpty
        }
    }

    func saveTavilyKey() { KeychainStore.write(tavilyKey.trimmingCharacters(in: .whitespacesAndNewlines), account: TavilyClient.keyAccount) }

    func saveWebSearchSettings() {
        UserDefaults.standard.set(webSearchMode.rawValue, forKey: "webSearchMode")
        UserDefaults.standard.set(webSearchProvider.rawValue, forKey: "webSearchProvider")
    }

    // MARK: Deciding

    /// Mode, then the local rules.
    func shouldSearchWeb(_ question: String) async -> (search: Bool, reason: String) {
        switch webSearchMode {
        case .off: return (false, "联网已关闭")
        case .always: return (true, "每次都联网")
        case .auto:
            let rules = WebSearchNeed.assess(question)
            return (rules.needsSearch, rules.reason)
        }
    }

    // MARK: Looking up

    /// Starts the background web answer for answer `id`. `parts` rebuilds the prompt
    /// (with search results, or allowing the engine's own search).
    func startWebLookup(id: UUID, question: String, model: String,
                        parts: @escaping (_ webSearch: Bool, _ results: [WebResult]?) -> (system: String, user: String)) {
        guard webSearchAvailable else { return }
        webLookupTask?.cancel()
        let provider = webSearchProvider
        webLookups[id] = WebLookup(question: question, provider: provider)
        // Old lookups only clutter memory; keep the latest dozen.
        if webLookups.count > 12, let oldest = webLookups.min(by: { $0.value.startedAt < $1.value.startedAt })?.key { webLookups[oldest] = nil }
        let started = Date()
        let backend = answerBackend, effort = codexReasoning, tier = activeServiceTier
        let codexModel = codexModelID, apiModel = currentAnswerModel, key = tavilyKey
        Diagnostics.record("web.started", ["provider": provider.rawValue])
        webLookupTask = Task { [weak self] in
            guard let self else { return }
            do {
                let result: (text: String, sources: [WebResult]) = try await withThrowingTaskGroup(of: (String, [WebResult]).self) { group in
                    group.addTask {
                        switch provider {
                        case .codex:
                            let prompt = parts(true, nil)
                            let request = AnswerRequest(system: prompt.system, user: prompt.user, model: codexModel, effort: effort, serviceTier: tier, webSearch: true)
                            return (try await self.runWebCodex(request), [])
                        case .tavily:
                            let results = try await TavilyClient.search(question, key: key)
                            guard !results.isEmpty else { throw AnswerEngineError.empty }
                            let prompt = parts(false, results)
                            let request = AnswerRequest(system: prompt.system, user: prompt.user, model: backend == .openAICompatible ? apiModel : codexModel,
                                                        effort: effort, serviceTier: tier)
                            return (try await self.runWebAnswer(request, backend: backend), results)
                        }
                    }
                    group.addTask {
                        try await Task.sleep(for: .seconds(90))
                        throw AnswerEngineError.timeout
                    }
                    defer { group.cancelAll() }
                    return try await group.next()!
                }
                let text = result.text.trimmingCharacters(in: .whitespacesAndNewlines)
                guard !text.isEmpty else { throw AnswerEngineError.empty }
                self.finishWebLookup(id: id, text: text, sources: result.sources, seconds: Date().timeIntervalSince(started), model: model)
            } catch is CancellationError {
                if self.webLookups[id]?.status == .searching { self.webLookups[id]?.status = .failed("已取消") }
            } catch {
                self.webLookups[id]?.status = .failed(error.localizedDescription)
                Diagnostics.record("web.failed", ["provider": provider.rawValue])
            }
        }
    }

    /// Manual “联网查一下” for the answer on screen.
    func lookUpDisplayedAnswer() {
        guard let id = displayedRecordID else { return }
        let question = displayedQuestion
        guard !question.isEmpty else { return }
        guard webSearchAvailable else { errorMessage = "联网搜索还没设置：在 设置 → 回答 → 联网搜索 选择 Codex 或填写 Tavily Key。"; return }
        let workspace = activeWorkspace
        let context = TranscriptContextBuilder.recent(contextEntries.map { (date: $0.date, text: $0.contextualText) })
        let excerpts = contextIndex.retrieve(question: question)
        let skills = activeSkillSnippets
        startWebLookup(id: id, question: question, model: currentAnswerModel) { webSearch, results in
            AnswerPromptBuilder.makeParts(question: question, context: context, profile: workspace.materialText, turns: [], hasScreenshot: false,
                                          preferences: workspace.preferences, mode: workspace.mode, style: workspace.style, excerpts: excerpts,
                                          experience: workspace.experienceNotes(), skills: skills, webResults: results, webSearch: webSearch)
        }
    }

    private func finishWebLookup(id: UUID, text: String, sources: [WebResult], seconds: Double, model: String) {
        guard var lookup = webLookups[id] else { return }
        if id == currentAnswerID, insights.answerID == id {
            insights.beginAnswer(id: id, text: text, since: insights.answerSince)
        }
        lookup.status = .done
        lookup.answer = text
        lookup.sources = sources
        lookup.seconds = seconds
        webLookups[id] = lookup
        Diagnostics.record("web.completed", ["provider": lookup.provider.rawValue, "seconds": seconds, "sources": sources.count])
        // Follow-ups should build on the updated answer.
        replaceTurnAnswer(question: lookup.question, with: text)
        recordAnswer(AnswerRecord(question: lookup.question, answer: text, model: "\(model) · 联网", seconds: seconds, firstTokenSeconds: nil, automatic: true))
    }

    nonisolated private func runWebCodex(_ request: AnswerRequest) async throws -> String {
        do {
            return try await webAppServer.answer(request) { _ in }
        } catch AnswerEngineError.appServerUnavailable {
            return try await webCodex.run(prompt: request.fullPrompt, model: request.model, reasoning: request.effort, serviceTier: request.serviceTier,
                                          webSearch: true, timeout: 90) { _ in }
        }
    }

    /// Search results (and other background work, such as slide notes) through the
    /// regular engine, on separate clients so the quick answer and the next question
    /// are never blocked.
    nonisolated func runWebAnswer(_ request: AnswerRequest, backend: AnswerBackend) async throws -> String {
        switch backend {
        case .openAICompatible:
            let (base, key, effort) = await MainActor.run { (self.apiBaseURL, self.apiKey, self.apiEffortParameter) }
            return try await webAPIClient.answer(request, baseURL: base, apiKey: key, reasoningEffort: effort) { _ in }
        case .codexAppServer, .codexExec:
            do { return try await webAppServer.answer(request) { _ in } }
            catch AnswerEngineError.appServerUnavailable {
                return try await webCodex.run(prompt: request.fullPrompt, model: request.model, reasoning: request.effort, serviceTier: request.serviceTier, timeout: 90) { _ in }
            }
        }
    }
}
