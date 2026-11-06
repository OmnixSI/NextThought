import Foundation
import NextThoughtCore
import Observation

/// Owns the current request and its evidence. Every asynchronous producer must
/// carry the request ID; stale screen/search/model work cannot mutate a new answer.
@MainActor @Observable final class AnswerCoordinator {
    private(set) var state: AnswerState?
    private(set) var evidenceHistory: [UUID: EvidenceBundle] = [:]
    func begin(id: UUID, question: String) { state = .init(id: id, question: question) }
    func update(id: UUID, _ change: (inout AnswerState) -> Void) {
        guard state?.id == id else { return }
        change(&state!); state!.revision += 1
        evidenceHistory[id] = state!.evidence
        if evidenceHistory.count > 100, let first = evidenceHistory.keys.first(where: { $0 != id }) { evidenceHistory[first] = nil }
    }
    func draft(id: UUID, text: String) {
        update(id: id) { $0.phase = .generating; $0.text = text }
    }
    func finish(id: UUID, text: String) { update(id: id) { $0.phase = .complete; $0.text = text } }
    func cancel() { if let id = state?.id, [.waiting, .generating].contains(state!.phase) { update(id: id) { $0.phase = .cancelled } } }
}
