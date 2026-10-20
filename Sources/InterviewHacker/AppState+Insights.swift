import AppKit
import Foundation
import InterviewHackerCore

/// Screen awareness, slide following and answer progress, all on local rules; none
/// blocks an answer.
@MainActor
extension AppState {
    // MARK: Reading the screen

    /// Background loop: while a session runs (not paused), read the screen every few
    /// seconds. Started at launch and when the setting changes.
    func startScreenWatch() {
        screenWatchTask?.cancel()
        screenWatchTask = nil
        guard insights.screenWatch, !DemoMode.isEnabled else { insights.screen = nil; return }
        screenWatchTask = Task { [weak self] in
            while !Task.isCancelled {
                guard let self else { return }
                if self.isRunning, !self.isPaused, !self.isStopping { await self.readScreen() }
                // After a failure (no Screen Recording permission) try again slowly.
                let interval = max(2, self.insights.screenInterval)
                try? await Task.sleep(for: .seconds(self.insights.screenError == nil ? interval : max(interval, 20)))
            }
        }
    }

    func readScreen() async {
        guard !insights.isReadingScreen else { return }
        insights.isReadingScreen = true
        defer { insights.isReadingScreen = false }
        do {
            let image = try await ScreenshotService.captureForReading()
            let text = try await ScreenshotService.recognizeText(image).trimmingCharacters(in: .whitespacesAndNewlines)
            insights.screenError = nil
            await screenRead(text)
        } catch {
            if insights.screenError == nil { Diagnostics.record("insight.screen_failed") }
            insights.screenError = "读不到屏幕：请在 系统设置 → 隐私与安全性 → 屏幕与系统音频录制 里允许 InterviewHacker。（\(error.localizedDescription)）"
        }
    }

    private func screenRead(_ text: String) async {
        if let previous = insights.screen, ScreenText.isSameScreen(previous.text, text) {
            insights.screen?.capturedAt = Date()
            if insights.slide != nil, previous.kind == .slides { insights.slideSeenAt = Date() }
            return
        }
        var onSlide = false
        if insights.followSlides { onSlide = followSlide(text) }
        var state = LiveInsights.ScreenState(text: text, kind: onSlide ? .slides : nil, relevance: onSlide ? 1 : 0, capturedAt: Date())
        if !onSlide {
            let speech = contextEntries.suffix(4).map(\.text).joined(separator: " ")
            state.relevance = ScreenText.heuristicRelevance(screen: text, recentSpeech: speech) ? 0.6 : 0.2
        }
        insights.screen = state
        Diagnostics.record("insight.screen", ["kind": state.kind?.rawValue ?? "unknown", "relevance": state.relevance, "characters": text.count])
    }

    /// Screen text for answers: read in the last two minutes and relevant to the talk.
    /// While a slide is followed, the slide itself is sent instead.
    var relevantScreenText: String? {
        guard insights.screenWatch, let screen = insights.screen, screen.isRelevant,
              Date().timeIntervalSince(screen.capturedAt) < 120, !(screen.kind == .slides && insights.slide != nil) else { return nil }
        return ScreenText.trimmed(screen.text, limit: 2_500)
    }

    /// The slide on screen within the last two minutes.
    var currentSlideExcerpt: ContextExcerpt? {
        guard insights.screenWatch, let slide = insights.slide, let seen = insights.slideSeenAt, Date().timeIntervalSince(seen) < 120 else { return nil }
        return ContextExcerpt(source: slide.documentName, page: slide.page, text: slide.text)
    }

    // MARK: Following slides

    /// Pages of the imported decks (PowerPoint, Keynote, multi-page PDF).
    var slidePages: [SlidePage] {
        activeWorkspace.documents.filter { $0.enabled && SlideMatcher.isDeck(name: $0.name, pageCount: $0.pages.count) }.flatMap { document in
            document.pages.enumerated().map { SlidePage(documentID: document.id, documentName: document.name, page: $0.offset + 1, pageCount: document.pages.count, text: $0.element) }
        }
    }

    /// Finds the slide on screen; true when one is showing.
    private func followSlide(_ text: String) -> Bool {
        let pages = slidePages
        guard !pages.isEmpty else { insights.slide = nil; return false }
        let ranked = SlideMatcher.rank(screen: text, pages: pages)
        var page: SlidePage?
        switch SlideMatcher.decide(ranked, previous: insights.slide) {
        case let .page(found): page = found
        case let .ambiguous(candidates):
            if let best = candidates.first, best.score >= 0.35 { page = best.page }
        case .none: break
        }
        guard let page else { return false }
        insights.slideSeenAt = Date()
        if insights.slide != page {
            insights.slide = page
            insights.slideSince = Date()
            Diagnostics.record("insight.slide", ["page": page.page, "pages": page.pageCount])
            prepareSlideNotes(page)
            // The next slide's notes are ready before it is shown.
            if let next = pages.first(where: { $0.documentID == page.documentID && $0.page == page.page + 1 }) { prepareSlideNotes(next) }
        }
        return true
    }

    /// Talking points for one slide, written once by the answer engine on the
    /// background clients (the live answer is never blocked).
    func prepareSlideNotes(_ page: SlidePage, force: Bool = false) {
        guard force || insights.slideNotes[page.key] == nil, !insights.generatingNotes.contains(page.key) else { return }
        let model = currentAnswerModel
        guard !model.isEmpty else { insights.slideNotesError = "还没有选择回答模型，讲稿提示需要回答引擎。"; return }
        insights.generatingNotes.insert(page.key)
        insights.slideNotesError = nil
        let deck = slidePages.filter { $0.documentID == page.documentID }
        let previous = deck.first { $0.page == page.page - 1 }?.text
        let next = deck.first { $0.page == page.page + 1 }?.text
        let backend = answerBackend
        let previousJob = insights.sideChain
        insights.sideChain = Task { [weak self] in
            await previousJob?.value
            guard let self else { return }
            defer { self.insights.generatingNotes.remove(page.key) }
            let started = Date()
            do {
                // Supporting material (the paper, the script), not the deck itself.
                let excerpts = await self.retrieveExcerpts(question: page.text, maximumCharacters: 3_000)
                    .filter { !$0.source.hasSuffix(page.documentName) }
                let prompt = SlideNotes.prompt(page: page, previous: previous, next: next, excerpts: excerpts, mode: self.sessionMode, preferences: self.activeWorkspace.preferences)
                let request = AnswerRequest(system: prompt.system, user: prompt.user, model: model,
                                            effort: backend == .openAICompatible ? self.codexReasoning : "low", serviceTier: self.activeServiceTier)
                let text = try await self.runWebAnswer(request, backend: backend, forSlides: true)
                if let notes = SlideNotes.parse(text) {
                    self.insights.slideNotes[page.key] = notes
                    Diagnostics.record("insight.slide_notes", ["page": page.page, "points": notes.points.count, "seconds": Date().timeIntervalSince(started)])
                } else {
                    self.insights.slideNotesError = "讲稿提示没有按格式返回，可以点“重新生成”。"
                }
            } catch {
                self.insights.slideNotesError = "讲稿提示生成失败：\(error.localizedDescription)"
            }
        }
    }

    // MARK: Finished lines

    /// Every finished, non-echo line from either side.
    func insightsDidReceive(_ entry: TranscriptEntry) {
        guard isRunning, !isPaused else { return }
        if entry.isMe { updateCoverage() }
    }

    /// Which sentences of the current answer, and of the slide's talking points, the
    /// user has already said.
    private func updateCoverage() {
        guard insights.answerProgress, !insights.coverageRunning else { return }
        var jobs: [(key: String, points: [String], covered: Set<Int>, since: Date)] = []
        if let id = insights.answerID, id == currentAnswerID, historyCursor == nil, !insights.answerPoints.isEmpty {
            jobs.append(("answer", insights.answerPoints, insights.answerCovered, insights.answerSince))
        }
        if let slide = insights.slide, let notes = insights.slideNotes[slide.key] {
            jobs.append((slide.key, notes.points, insights.slideCovered[slide.key] ?? [], insights.slideSince))
        }
        guard !jobs.isEmpty else { return }
        insights.coverageRunning = true
        Task {
            defer { insights.coverageRunning = false }
            for job in jobs {
                let speech = entries.filter { $0.isMe && $0.suspectedEcho != true && $0.date >= job.since.addingTimeInterval(-2) }.map(\.text).joined(separator: " ")
                guard speech.count >= 4 else { continue }
                let open = job.points.indices.filter { !job.covered.contains($0) }
                guard !open.isEmpty else { continue }
                var covered = job.covered
                covered.formUnion(open.filter { AnswerPoints.heuristicCovered(job.points[$0], speech: speech) })
                guard covered != job.covered else { continue }
                if job.key == "answer" {
                    if insights.answerID != nil { insights.answerCovered.formUnion(covered) }
                } else {
                    insights.slideCovered[job.key, default: []].formUnion(covered)
                }
            }
        }
    }

    func openInsightSettings() {
        UserDefaults.standard.set(SettingsGroup.recognition.rawValue, forKey: "settingsGroup")
        section = .settings
    }
}
