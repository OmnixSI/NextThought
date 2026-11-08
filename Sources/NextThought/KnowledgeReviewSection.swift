import SwiftUI
import NextThoughtCore

/// Review generated knowledge beside the meeting or in its series. Suggestions are inert
/// until one of the explicit actions below writes them into reusable context.
struct KnowledgeReviewSection: View {
    @Environment(AppState.self) private var state
    let workspaceID: UUID
    var sessionID: UUID? = nil
    var summary: SessionSummary? = nil
    @State private var seriesEvidence: [UUID: [SessionEvidenceReference]] = [:]

    private var seriesEvidenceTaskID: String {
        let candidates = (state.workspaces.workspace(workspaceID)?.seriesChangeCandidates ?? [])
            .filter { $0.status == .pending && (sessionID == nil || $0.sourceSessionID == sessionID) }
            .map { "\($0.sourceSessionID.uuidString)|\($0.id)" }.sorted().joined(separator: ";")
        return "\(workspaceID.uuidString)|\(sessionID?.uuidString ?? "")|\(state.archive.searchableContentRevision)|\(state.archive.notesRevision)|\(candidates)"
    }

    var body: some View {
        Group {
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
                let pendingSeriesChanges = (workspace.seriesChangeCandidates ?? []).filter { candidate in
                    candidate.status == .pending && (sessionID == nil || candidate.sourceSessionID == sessionID)
                }
                let currentCandidateIDs = Set(pendingSeriesChanges.compactMap { candidate -> String? in
                    guard let reference = seriesEvidence[candidate.sourceSessionID]?.first(where: { $0.id == candidate.evidenceID }),
                          WorkspaceSeriesChangeMerger.matchesCurrentEvidence(candidate, reference: reference) else { return nil }
                    return candidate.id
                })
                let canCollectSeriesChanges: Bool = {
                    guard let sessionID else { return false }
                    var preview = workspace
                    return !WorkspaceSeriesChangeMerger.collectCandidates(from: seriesEvidence[sessionID] ?? [],
                                                                          sessionID: sessionID,
                                                                          into: &preview).isEmpty
                }()

                if !pending.isEmpty || canCollect || !pendingSeriesChanges.isEmpty || canCollectSeriesChanges {
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

                        if canCollectSeriesChanges, let sessionID {
                            Button("收集已核实的决定与行动") {
                                Task { _ = await state.collectSeriesChangeCandidates(sessionID: sessionID, workspaceID: workspaceID) }
                            }
                            .buttonStyle(SecondaryButtonStyle())
                        }
                        ForEach(pendingSeriesChanges) { candidate in
                            SeriesChangeCandidateCard(candidate: candidate, workspace: workspace, workspaceID: workspaceID,
                                                      isCurrent: currentCandidateIDs.contains(candidate.id))
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
        .task(id: seriesEvidenceTaskID) {
            var sourceIDs = Set((state.workspaces.workspace(workspaceID)?.seriesChangeCandidates ?? [])
                .filter { $0.status == .pending && (sessionID == nil || $0.sourceSessionID == sessionID) }
                .map(\.sourceSessionID))
            if let sessionID { sourceIDs.insert(sessionID) }
            var loaded: [UUID: [SessionEvidenceReference]] = [:]
            for sourceID in sourceIDs.sorted(by: { $0.uuidString < $1.uuidString }) {
                guard !Task.isCancelled, let detail = await state.archive.detail(sourceID),
                      detail.meta.workspaceID == workspaceID, detail.meta.workspaceDeleted != true else { continue }
                loaded[sourceID] = SessionEvidenceBuilder.build(meta: detail.meta, lines: detail.lines,
                                                                 notes: detail.notes, summary: detail.summary)
            }
            guard !Task.isCancelled else { return }
            seriesEvidence = loaded
        }
    }
}

private struct SeriesChangeCandidateCard: View {
    @Environment(AppState.self) private var state
    let candidate: WorkspaceSeriesChangeCandidate
    let workspace: Workspace
    let workspaceID: UUID
    let isCurrent: Bool
    @State private var isWorking = false
    @State private var confirmedOwner = ""
    @State private var confirmedDue = ""

    private var targets: [WorkspaceSeriesItem] {
        (workspace.seriesItems ?? []).filter { $0.kind == candidate.kind }
            .sorted { $0.updatedAt > $1.updatedAt }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack {
                Text(candidate.kind == .decision ? String(localized: "决定建议") : String(localized: "行动建议"))
                    .font(Typo.label).foregroundStyle(Ink.secondary)
                Spacer()
                Button("忽略") { review(.reject) }
                    .buttonStyle(.plain).font(Typo.caption).foregroundStyle(Ink.secondary)
                    .disabled(isWorking)
            }
            Text(verbatim: candidate.text).font(Typo.body).textSelection(.enabled)
            if candidate.kind == .action {
                Text("确认负责人和期限；期限会保留原话，不会自动改写相对日期。")
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                TextField("负责人（未确认可留空）", text: $confirmedOwner)
                    .textFieldStyle(.roundedBorder)
                TextField("期限原话（例如：下周一，悉尼时间）", text: $confirmedDue)
                    .textFieldStyle(.roundedBorder)
            }
            if let quote = candidate.sourceQuote, !quote.isEmpty {
                Text("依据：“\(quote)” · \(candidate.location ?? "")")
                    .font(Typo.caption).foregroundStyle(Ink.secondary).textSelection(.enabled)
            }

            if !isCurrent {
                Label("原话或负责人/期限已变化；此建议不能接受。", systemImage: "exclamationmark.triangle.fill")
                    .font(Typo.caption).foregroundStyle(Ink.caution)
                Button("忽略过期建议") { review(.reject) }
                    .buttonStyle(SecondaryButtonStyle()).disabled(isWorking)
            }

            if isCurrent && !targets.isEmpty {
                VStack(alignment: .leading, spacing: Space.s2) {
                    Text("更新已有内容（保留旧版本）")
                        .font(Typo.caption).foregroundStyle(Ink.secondary)
                    ForEach(targets) { item in
                        HStack(alignment: .top, spacing: Space.s2) {
                            Text(verbatim: item.text).font(Typo.caption).lineLimit(2)
                            Spacer(minLength: 0)
                            Button("更新") { review(.replace, target: item) }
                                .buttonStyle(SecondaryButtonStyle()).disabled(isWorking)
                        }
                    }
                }
            }
            if isCurrent {
                let itemType = candidate.kind == .decision ? String(localized: "决定") : String(localized: "行动")
                Button(String(localized: "作为新\(itemType)加入系列")) { review(.add) }
                    .buttonStyle(PrimaryButtonStyle()).disabled(isWorking)
            }
        }
        .padding(Space.s4)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Ink.surface, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous)
            .stroke(Ink.hairline, lineWidth: 1))
        .onAppear {
            confirmedOwner = candidate.owner ?? ""
            confirmedDue = candidate.due ?? ""
        }
    }

    private func review(_ decision: WorkspaceSeriesChangeDecision, target: WorkspaceSeriesItem? = nil) {
        guard !isWorking else { return }
        isWorking = true
        let details = candidate.kind == .action
            ? WorkspaceSeriesChangeReviewDetails(owner: cleaned(confirmedOwner), due: cleaned(confirmedDue))
            : nil
        Task {
            await state.reviewSeriesChangeCandidate(candidate.id, decision: decision, workspaceID: workspaceID,
                                                    targetItemID: target?.id, expectedRevision: target?.revision,
                                                    reviewedDetails: details)
            isWorking = false
        }
    }

    private func cleaned(_ value: String) -> String? {
        let result = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return result.isEmpty ? nil : result
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
                Text(candidate.kind == .lesson ? String(localized: "经验建议") : String(localized: "答法建议"))
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
            Text(String(localized: String.LocalizationValue(stringLiteral: label)))
                .font(Typo.caption).foregroundStyle(Ink.secondary)
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
