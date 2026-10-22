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
    var generation = UUID()
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
            return WebResult(title: item["title"] as? String ?? url, url: url, snippet: item["content"] as? String ?? "", publishedAt: item["published_date"] as? String, retrievedAt: Date().ISO8601Format())
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
        guard let id = displayedRecordID, let lookup = webLookups[id], !lookup.answer.isEmpty else { return false }
        if case .failed = lookup.status { return false }
        return lookup.status == .done && acceptedWebAnswers.contains(id) && !showOriginalAnswer.contains(id)
    }

    func toggleWebAnswer() {
        guard let id = displayedRecordID else { return }
        if showsWebAnswer { showOriginalAnswer.insert(id) }
        else { acceptedWebAnswers.insert(id); showOriginalAnswer.remove(id) }
        if showsWebAnswer, let lookup = webLookups[id] { insights.beginAnswer(id: id, text: lookup.answer, since: Date()) }
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
        if webSearchMode != .off, webSearchProvider == .codex { Task { await webAppServer.prewarm() } }
    }

    // MARK: Deciding

    /// Mode, then the local rules.
    func shouldSearchWeb(_ question: String) async -> (search: Bool, reason: String) {
        let decision = await assessWebSearch(question)
        return (decision.search, decision.reason)
    }

    // MARK: Looking up

    /// Starts the background web answer for answer `id`. `parts` rebuilds the prompt
    /// (with search results, or allowing the engine's own search).
    func startWebLookup(id: UUID, question: String, query: String? = nil,
                        parts: @escaping (_ webSearch: Bool, _ results: [WebResult]?) -> (system: String, user: String)) {
        guard webSearchAvailable else { return }
        webLookupTask?.cancel()
        let provider = webSearchProvider
        let lookup = WebLookup(question: question, provider: provider)
        let generation = lookup.generation
        webLookups[id] = lookup
        // Old lookups only clutter memory; keep the latest dozen.
        if webLookups.count > 12, let oldest = webLookups.min(by: { $0.value.startedAt < $1.value.startedAt })?.key { webLookups[oldest] = nil }
        let started = Date()
        let backend = answerBackend, effort = codexReasoning, tier = activeServiceTier
        let codexModel = codexModelID, apiModel = currentAnswerModel, key = tavilyKey
        let lookupModel = provider == .codex || backend != .openAICompatible ? codexModel : apiModel
        Diagnostics.record("web.started", ["request_id": id.uuidString, "provider": provider.rawValue,
                                           "model": lookupModel, "reasoning": effort])
        webLookupTask = Task { [weak self] in
            guard let self else { return }
            do {
                let result: (text: String, sources: [WebResult]) = try await withThrowingTaskGroup(of: (String, [WebResult]).self) { group in
                    group.addTask {
                        switch provider {
                        case .codex:
                            var prompt = WebLookupPromptBuilder.makeParts(userJSON: parts(true, nil).user)
                            if let query { prompt.user += "\n检索对象：" + String(query.prefix(500)) }
                            let request = AnswerRequest(system: prompt.system, user: prompt.user, model: codexModel, effort: effort, serviceTier: tier, webSearch: true)
                            return (try await self.runWebCodex(request) { [weak self] text in
                                Task { @MainActor in
                                    guard let self, self.webLookups[id]?.generation == generation,
                                          self.webLookups[id]?.status == .searching else { return }
                                    if self.webLookups[id]?.answer.isEmpty == true {
                                        Diagnostics.record("web.first_text", ["request_id": id.uuidString, "seconds": Date().timeIntervalSince(started)])
                                    }
                                    self.webLookups[id]?.answer = text
                                }
                            }, [])
                        case .tavily:
                            let results = try await TavilyClient.search(query ?? question, key: key)
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
                try Task.checkCancellation()
                let text = result.text.trimmingCharacters(in: .whitespacesAndNewlines)
                guard !text.isEmpty else { throw AnswerEngineError.empty }
                guard self.webLookups[id]?.generation == generation else { return }
                let sources = result.sources.isEmpty ? Self.sources(in: text) : result.sources
                self.finishWebLookup(id: id, text: text, sources: sources, seconds: Date().timeIntervalSince(started), model: lookupModel)
            } catch is CancellationError {
                guard self.webLookups[id]?.generation == generation else { return }
                if self.webLookups[id]?.status == .searching { self.webLookups[id]?.status = .failed("已取消") }
            } catch {
                guard self.webLookups[id]?.generation == generation else { return }
                self.webLookups[id]?.status = .failed(error.localizedDescription)
                Diagnostics.record("web.failed", ["request_id": id.uuidString, "provider": provider.rawValue, "seconds": Date().timeIntervalSince(started)])
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
        let skills = activeSkillSnippets
        // With the embedding model the question is turned into a vector first,
        // so the passages are looked up in the background.
        Task { [weak self] in
            guard let self else { return }
            let passages = await self.retrieveExcerpts(question: question)
            self.startWebLookup(id: id, question: question) { webSearch, results in
                AnswerPromptBuilder.makeParts(question: question, context: context, profile: workspace.materialText, turns: [], hasScreenshot: false,
                                              preferences: workspace.preferences, mode: workspace.mode, style: workspace.style, excerpts: passages,
                                              experience: workspace.experienceNotes(), skills: skills, webResults: results, webSearch: webSearch)
            }
        }
    }

    private func finishWebLookup(id: UUID, text: String, sources: [WebResult], seconds: Double, model: String) {
        guard var lookup = webLookups[id] else { return }
        lookup.status = .done
        lookup.answer = text
        lookup.sources = sources
        lookup.seconds = seconds
        webLookups[id] = lookup
        Diagnostics.record("web.completed", ["provider": lookup.provider.rawValue, "seconds": seconds, "sources": sources.count])
        // Follow-ups should build on the updated answer.
        replaceTurnAnswer(question: lookup.question, with: text)
        answerCoordinator.update(id: id) { $0.evidence.webSources = sources }
        if let position = answerHistory.firstIndex(where: { $0.id == id }) {
            answerHistory[position].webAnswer = text; answerHistory[position].webSources = sources
            recordAnswer(answerHistory[position])
        }
    }

    nonisolated static func sources(in text: String) -> [WebResult] {
        guard let regex = try? NSRegularExpression(pattern: #"\[([^\]]+)\]\((https?://[^\s)]+)\)"#) else { return [] }
        let ns = text as NSString
        var seen = Set<String>()
        return regex.matches(in: text, range: NSRange(location: 0, length: ns.length)).compactMap {
            let url = ns.substring(with: $0.range(at: 2))
            guard let parsed = URL(string: url), parsed.host != nil, seen.insert(url).inserted else { return nil }
            return WebResult(title: ns.substring(with: $0.range(at: 1)), url: url, snippet: "", retrievedAt: Date().ISO8601Format())
        }
    }

    nonisolated private func runWebCodex(_ request: AnswerRequest, onDelta: @escaping @Sendable (String) -> Void) async throws -> String {
        try await withTaskCancellationHandler {
            do {
                return try await webAppServer.answer(request, onDelta: onDelta)
            } catch let AnswerEngineError.appServerUnavailable(detail) {
                try Task.checkCancellation()
                Diagnostics.record("web.fallback", ["reason": detail])
                return try await webCodex.run(prompt: request.fullPrompt, model: request.model, reasoning: request.effort, serviceTier: request.serviceTier,
                                              webSearch: true, timeout: 90) { onDelta($0) }
            }
        } onCancel: {
            // Also unblock initialize/account/read, before a turn exists.
            self.webAppServer.shutdown()
            self.webCodex.cancel()
        }
    }

    /// Search and slide notes use different transports: each app-server client
    /// owns one active turn, including its notification handler and cancellation.
    nonisolated func runWebAnswer(_ request: AnswerRequest, backend: AnswerBackend, forSlides: Bool = false) async throws -> String {
        let server = forSlides ? slideAppServer : webAppServer
        let bridge = forSlides ? slideCodex : webCodex
        let api = forSlides ? slideAPIClient : webAPIClient
        return try await withTaskCancellationHandler {
            switch backend {
            case .openAICompatible:
                let (base, key, effort) = await MainActor.run { (self.apiBaseURL, self.apiKey, self.apiEffortParameter) }
                return try await api.answer(request, baseURL: base, apiKey: key, reasoningEffort: effort) { _ in }
            case .codexAppServer, .codexExec:
                do { return try await server.answer(request) { _ in } }
                catch AnswerEngineError.appServerUnavailable {
                    try Task.checkCancellation()
                    return try await bridge.run(prompt: request.fullPrompt, model: request.model, reasoning: request.effort, serviceTier: request.serviceTier, timeout: 90) { _ in }
                }
            }
        } onCancel: {
            server.shutdown()
            bridge.cancel()
        }
    }
}
