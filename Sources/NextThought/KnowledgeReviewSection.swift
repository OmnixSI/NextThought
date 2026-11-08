import SwiftUI
import NextThoughtCore

/// Review generated knowledge beside the meeting or in its series. Suggestions are inert
/// until one of the explicit actions below writes them into reusable context.
struct KnowledgeReviewSection: View {
    @Environment(AppState.self) private var state
    let workspaceID: UUID
    var sessionID: UUID? = nil
    var summary: SessionSummary? = nil

    var body: some View {
        if let workspace = state.workspaces.workspace(workspaceID) {
            let pending = (workspace.knowledgeCandidates ?? []).filter { candidate in
                candidate.status == .pending && (sessionID == nil || candidate.sourceSessionID == sessionID)
            }
            let canCollect: Bool = {
                guard let sessionID, let summary, summary.appliedToWorkspace != true,
                      !((summary.lessons ?? []).isEmpty && (summary.bank ?? []).isEmpty) else { return false }
                var preview = workspace
                return !KnowledgeMerger.collectCandidates(lessons: summary.lessons ?? [], bank: summary.bank ?? [],
                                                          into: &preview, session: sessionID, originID: sessionID).isEmpty
            }()

            if !pending.isEmpty || canCollect {
                VStack(alignment: .leading, spacing: Space.s3) {
                    HStack(alignment: .firstTextBaseline) {
                        VStack(alignment: .leading, spacing: 3) {
                            Text("知识候选")
                                .font(Typo.section)
                            Text("候选先保留在待审阅区；确认后才会影响后续回答。")
                                .font(Typo.caption).foregroundStyle(Ink.secondary)
                        }
                        Spacer()
                        if canCollect, let summary, let sessionID {
                            Button("收集本场候选") {
                                state.collectReviewCandidates(summary, sessionID: sessionID, workspaceID: workspaceID)
                            }
                            .buttonStyle(SecondaryButtonStyle())
                        }
                    }

                    ForEach(pending) { candidate in
                        KnowledgeCandidateCard(candidate: candidate, workspace: workspace, workspaceID: workspaceID)
                    }
                }
                .padding(Space.s4)
                .background(Ink.aiSoft, in: RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
                .overlay {
                    RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous)
                        .stroke(Ink.hairline, lineWidth: 1)
                }
            }
        }
    }
}

private struct KnowledgeCandidateCard: View {
    @Environment(AppState.self) private var state
    let candidate: WorkspaceKnowledgeCandidate
    let workspace: Workspace
    let workspaceID: UUID

    private var matchingQuestion: QuestionBankItem? {
        guard candidate.kind == .question else { return nil }
        return workspace.questionBank.first { KnowledgeMerger.similarQuestion($0.question, candidate.text) }
    }

    private var matchingLesson: WorkspaceLesson? {
        guard candidate.kind == .lesson else { return nil }
        return workspace.lessons.first { KnowledgeMerger.similarity($0.text, candidate.text) >= 0.6 }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack(alignment: .firstTextBaseline) {
                Text(candidate.kind == .lesson ? "经验建议" : "答法建议")
                    .font(Typo.label).foregroundStyle(Ink.secondary)
                Spacer()
                Button("忽略") { decide(.reject) }
                    .buttonStyle(.plain).font(Typo.caption).foregroundStyle(Ink.secondary)
            }
            Text(candidate.text).font(Typo.body).textSelection(.enabled)

            if candidate.kind == .question, let proposed = candidate.proposedAnswer {
                if let existing = matchingQuestion {
                    VStack(alignment: .leading, spacing: Space.s2) {
                        labeledAnswer("系列当前问题", existing.question)
                        labeledAnswer("系列当前答法", existing.answer)
                        labeledAnswer("本次建议", proposed)
                    }
                    HStack(spacing: Space.s2) {
                        Button("保留当前答法") { decide(.keepExisting) }
                            .buttonStyle(SecondaryButtonStyle())
                        if existing.answer != proposed {
                            Button("采用建议并保留旧版") { decide(.replaceAnswer) }
                                .buttonStyle(PrimaryButtonStyle())
                        }
                    }
                } else {
                    labeledAnswer("建议答法", proposed)
                    Button("加入题库") { decide(.add) }
                        .buttonStyle(PrimaryButtonStyle())
                }
            } else if let existing = matchingLesson {
                Text("系列已有相近经验：\(existing.text)")
                    .font(Typo.caption).foregroundStyle(Ink.secondary).textSelection(.enabled)
                Button("保留现有经验并记下来源") { decide(.keepExisting) }
                    .buttonStyle(SecondaryButtonStyle())
            } else {
                Button("加入经验库") { decide(.add) }
                    .buttonStyle(PrimaryButtonStyle())
            }
        }
        .padding(Space.s4)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Ink.surface, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
        .overlay {
            RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous)
                .stroke(Ink.hairline, lineWidth: 1)
        }
    }

    private func labeledAnswer(_ label: String, _ answer: String) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(label).font(Typo.caption).foregroundStyle(Ink.secondary)
            Text(answer).font(Typo.body).textSelection(.enabled)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Space.s3)
        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
    }

    private func decide(_ decision: WorkspaceKnowledgeDecision) {
        state.reviewKnowledgeCandidate(candidate.id, decision: decision, workspaceID: workspaceID)
    }
}
