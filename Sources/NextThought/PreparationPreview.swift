import Foundation
import NextThoughtCore
import Observation

/// A preparation request has its own lifetime and output. It never overwrites a
/// live answer or silently switches to whichever series is selected later.
@MainActor @Observable
final class PreparationPreview {
    static let shared = PreparationPreview()
    private(set) var text = ""
    private(set) var error: String?
    private(set) var isWorking = false
    @ObservationIgnored private var task: Task<Void, Never>?
    @ObservationIgnored private var generation = UUID()
    @ObservationIgnored private var source: Workspace?
    @ObservationIgnored private var plan: MeetingPlan?

    func isCurrent(series: Workspace, plan: MeetingPlan) -> Bool {
        source == series && self.plan == plan
    }
    func cancel() {
        generation = UUID()
        task?.cancel(); task = nil
        text = ""; error = nil; isWorking = false; source = nil; plan = nil
    }
    func read(series: Workspace, plan: MeetingPlan, using state: AppState) {
        cancel()
        source = series; self.plan = plan
        let id = generation
        let backend = state.answerBackend
        let model = state.currentAnswerModel
        let effort = backend == .openAICompatible ? state.apiReasoningEffort : state.codexReasoning
        let tier = state.activeServiceTier
        isWorking = true
        task = Task {
            defer { if generation == id { isWorking = false; task = nil } }
            do {
                let workspace = await state.meetingWorkspace(for: plan, in: series)
                try Task.checkCancellation()
                var remaining = 24_000
                let materials = workspace.retrievalDocuments.compactMap { document -> String? in
                    guard document.enabled, remaining > 0 else { return nil }
                    var excerpts: [String] = []
                    for page in document.pages where remaining > 0 {
                        let part = String(page.prefix(remaining))
                        excerpts.append(part)
                        remaining -= part.count
                    }
                    let excerpt = excerpts.joined(separator: "\n")
                    return "来源：\(document.labeledName)\n\(excerpt)"
                }.joined(separator: "\n\n")
                let mode = workspace.effectiveMode
                let request = AnswerRequest(
                    system: PreparationPrompt.system(for: mode),
                    user: "为本次\(mode.title)做准备。背景：\(workspace.materialText)\n\(workspace.experienceNotes())\n\(materials)\n\(PreparationPrompt.output(for: mode))",
                    model: model, effort: effort, serviceTier: tier)
                let reply = try await state.runAnswer(request, backend: backend) { [weak self] delta in
                    Task { @MainActor in
                        guard let self, self.generation == id, self.isWorking else { return }
                        if delta.count >= self.text.count { self.text = delta }
                    }
                }
                try Task.checkCancellation()
                if generation == id { text = reply }
            } catch is CancellationError { }
            catch { if generation == id { self.error = AnswerServiceError.explain(error) } }
        }
    }
}
