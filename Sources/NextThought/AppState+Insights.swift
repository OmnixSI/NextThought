import AppKit
import Foundation
import NextThoughtCore

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
        screenContextService.invalidate()
        insights.screen = nil; insights.slide = nil; insights.slideSeenAt = nil
        guard insights.screenWatch, !DemoMode.isEnabled else { return }
        screenWatchTask = Task { [weak self] in
            while !Task.isCancelled {
                guard let self else { return }
                if self.isRunning, !self.isPaused, !self.isStopping { await self.readScreen(force: false) }
                // After a failure (no Screen Recording permission) try again slowly.
                let interval = max(1, self.insights.screenInterval)
                try? await Task.sleep(for: .seconds(self.insights.screenError == nil ? interval : max(interval, 20)))
            }
        }
    }

    var screenTargetID: String { insights.screenTarget.isEmpty ? selectedTargetID : insights.screenTarget }
    var screenTargetTitle: String { captureTargets.first(where: { $0.id == screenTargetID })?.title ?? (screenTargetID.isEmpty ? "主显示器" : screenTargetID) }
    func readScreen(force: Bool = true) async {
        guard !insights.isReadingScreen else { return }
        insights.isReadingScreen = true
        defer { insights.isReadingScreen = false }
        do {
            if let context = try await screenContextService.refresh(targetID: screenTargetID, source: screenTargetTitle,
                region: insights.screenRegion, force: force,
                localModelID: documentOCRProvider == .paddle ? selectedOCRModel.id : nil) {
                insights.screenError = nil; await screenRead(context.text)
            } else {
                insights.screen = nil; insights.slide = nil; insights.slideSeenAt = nil
            }
        } catch is CancellationError { }
        catch {
            insights.screen = nil; insights.slide = nil; insights.slideSeenAt = nil
            insights.screenError = "读不到选定画面：" + error.localizedDescription + "。请检查来源和屏幕录制权限。"
            Diagnostics.record("insight.screen_failed")
        }
    }

    private func screenRead(_ text: String) async {
        // Re-evaluate even when pixels are unchanged: the current question or
        // speaker may have changed. A previous slide is invalidated on no match.
        let onSlide = insights.followSlides && followSlide(text)
        let speech = contextEntries.suffix(4).map(\.text).joined(separator: " ")
        let relevance = onSlide ? 1.0 : (ScreenText.heuristicRelevance(screen: text, recentSpeech: speech) ? 0.6 : 0.2)
        insights.screen = .init(text: text, kind: onSlide ? .slides : nil, relevance: relevance,
            capturedAt: screenContextService.latest?.capturedAt ?? Date())
    }

    func screenForQuestion(_ question: String) async -> QuestionScreenContext {
        let reference = ScreenReference.isReference(question)
        guard insights.screenWatch else { return .init(note: reference ? "屏幕感知未开启，无法确认当前指代。" : nil) }
        do {
            var context = screenContextService.latest
            if reference, context?.isFresh() != true {
                context = try await screenContextService.refresh(targetID: screenTargetID, source: screenTargetTitle,
                    region: insights.screenRegion, force: true,
                    localModelID: documentOCRProvider == .paddle ? selectedOCRModel.id : nil)
            }
            guard let context, context.isFresh(maximumAge: reference ? 2 : 5), ScreenReference.isRelated(question, context: context) else {
                return .init(note: reference ? "尚未取得当前画面，不能把旧页面当作当前内容。" : nil)
            }
            await screenRead(context.text)
            let needsImage = reference && (ScreenReference.needsImage(question) || context.lowConfidence)
            if needsImage, answerBackend == .openAICompatible, !apiVisionEnabled {
                return .init(context: context, note: "当前 API 未启用图像输入，本次仅提供 OCR 文字；图形或位置不足时需要澄清。", slide: currentSlideExcerpt)
            }
            let image = needsImage ? try screenContextService.questionImage(frameID: context.frameID) : nil
            return .init(context: context, image: image, slide: currentSlideExcerpt)
        } catch {
            insights.slide = nil; insights.slideSeenAt = nil
            return .init(note: "当前屏幕读取失败：" + error.localizedDescription)
        }
    }

    var relevantScreenText: String? {
        guard insights.screenWatch, let context = screenContextService.latest, context.isFresh(maximumAge: 5) else { return nil }
        return ScreenText.trimmed(context.text, limit: 5000)
    }
    var currentSlideExcerpt: ContextExcerpt? {
        guard insights.screenWatch, let slide = insights.slide, let seen = insights.slideSeenAt,
              Date().timeIntervalSince(seen) < 5, screenContextService.latest?.isFresh(maximumAge: 5) == true else { return nil }
        return ContextExcerpt(source: slide.documentName, page: slide.page, text: slide.text, locator: slide.locator ?? .init(section: slide.page))
    }

    // MARK: Following slides

    /// Pages of the imported decks (PowerPoint, Keynote, multi-page PDF).
    var slidePages: [SlidePage] {
        activeWorkspace.documents.filter { $0.enabled && SlideMatcher.isDeck(name: $0.name, pageCount: $0.pages.count) }.flatMap { document in
            if let content = document.content {
                let grouped = Dictionary(grouping: content.blocks.filter { $0.locator.slide != nil || $0.locator.page != nil }, by: { $0.locator.slide ?? $0.locator.page! })
                return grouped.keys.sorted().map { number in SlidePage(documentID: document.id, documentName: document.name, page: number, pageCount: grouped.count, text: grouped[number]!.map(\.markdown).joined(separator: "\n\n"), locator: grouped[number]!.first?.locator) }
            }
            return document.pages.enumerated().map { SlidePage(documentID: document.id, documentName: document.name, page: $0.offset + 1, pageCount: document.pages.count, text: $0.element, locator: .init(section: $0.offset+1)) }
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
            if let current = insights.slide, candidates.contains(where: { $0.page == current && $0.score >= 0.45 }) { page = current }
        case .none: break
        }
        guard let page else { insights.slide = nil; insights.slideSeenAt = nil; return false }
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
        let workspaceID=activeWorkspace.id, revision=documentRevision
        insights.generatingNotes.insert(page.key)
        insights.slideNotesError = nil
        let deck = slidePages.filter { $0.documentID == page.documentID }
        let previous = deck.first { $0.page == page.page - 1 }?.text
        let next = deck.first { $0.page == page.page + 1 }?.text
        let backend = answerBackend
        let previousJob = insights.sideChain
        insights.sideChain = Task { [weak self] in
            await previousJob?.value
            guard let self, !Task.isCancelled, self.activeWorkspace.id == workspaceID, self.documentRevision == revision else { return }
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
                try Task.checkCancellation()
                guard self.activeWorkspace.id == workspaceID, self.documentRevision == revision else { return }
                if let notes = SlideNotes.parse(text) {
                    self.insights.slideNotes[page.key] = notes
                    Diagnostics.record("insight.slide_notes", ["page": page.page, "points": notes.points.count, "seconds": Date().timeIntervalSince(started)])
                } else {
                    self.insights.slideNotesError = "讲稿提示没有按格式返回，可以点“重新生成”。"
                }
            } catch {
                guard !Task.isCancelled, self.activeWorkspace.id == workspaceID, self.documentRevision == revision else { return }
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
        select(.settings)
    }
}
