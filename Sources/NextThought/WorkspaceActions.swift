import AppKit
import Foundation
import NextThoughtCore
import UniformTypeIdentifiers

/// Workspace (scenario) management, materials, knowledge and data import/export.
@MainActor
extension AppState {
    // MARK: Materials

    /// Materials per scenario. Shared with the retrieval index so a material
    /// that can be imported is also a material that can be retrieved.
    static let maxDocuments = SessionContextIndex.maximumDocuments

    /// Imports files into the current scenario.
    ///
    /// Every file is converted to Markdown, archived on disk, filed into the
    /// material slot that fits it and added to the retrieval index in one go, so
    /// the user can drop a whole folder in without sorting it by hand. A file
    /// that cannot be read only fails that one file.
    func importDocuments(_ urls: [URL], category: String? = nil) {
        documentPreparation.enqueue(urls, workspace: activeWorkspace, category: category, options: documentConversionOptions)
        importStatus = "已加入准备队列，可继续上传。"
    }

    func rebuildContextIndex() {
        // Meetings retain the prepared index captured at start; preparation only
        // publishes another revision outside the meeting.
        guard !isRunning else { return }
        insights.sideChain?.cancel()
        insights.slideNotes.removeAll(); insights.generatingNotes.removeAll()
        insights.slide=nil
        let revision = UUID(); documentRevision = revision
        let snapshot = activeWorkspace.retrievalDocuments
        let semantic = SemanticIndexStore.load(modelID: ModelCatalog.qwen3Embedding.id, workspaceID: activeWorkspace.id)
        semanticIndex = semantic
        // Never reuse a disabled/removed document while a replacement index builds.
        selectedExcerpts = []
        isIndexingDocuments = true
        Task {
            let index = await Task.detached(priority: .userInitiated) {
                HybridContextIndex(documents: snapshot, semantic: semantic)
            }.value
            guard documentRevision == revision else { return }
            contextIndex = index
            isIndexingDocuments = false
        }
        // Changed material needs new vectors; the ones that did not change are kept.
        if semanticRetrievalEnabled, embeddingModelInstalled, documentPreparation.pendingCount == 0 { rebuildSemanticIndex() }
    }

    func removeDocument(_ id: UUID) {
        guard !isRunning else { errorMessage = "请在会议结束后移除资料。"; return }
        if let path = documents.first(where: { $0.id == id })?.markdownPath, let url = MaterialArchive.resolve(path),
           workspaces.workspaces.flatMap(\.documents).filter({ $0.markdownPath == path }).count <= 1 {
            try? FileManager.default.removeItem(at: url)
            try? FileManager.default.removeItem(at: url.deletingPathExtension().appendingPathExtension("json"))
        }
        documents.removeAll { $0.id == id }; rebuildContextIndex()
    }

    func setDocumentEnabled(_ id: UUID, enabled: Bool) {
        guard !isRunning else { errorMessage = "会议中使用已锁定的资料版本。"; return }
        guard let index = documents.firstIndex(where: { $0.id == id }) else { return }
        documents[index].enabled = enabled; rebuildContextIndex()
    }

    func setDocumentCategory(_ id: UUID, category: String?) {
        guard let index = documents.firstIndex(where: { $0.id == id }) else { return }
        documents[index].category = category; rebuildContextIndex()
    }

    func applyTemplatePreferences() {
        answerPreferences = ModeProfile.for(sessionMode).templatePreferences
    }

    // MARK: Workspaces

    func switchWorkspace(_ id: UUID) {
        guard id != activeWorkspace.id else { return }
        guard !isRunning else { errorMessage = "会议中保持当前资料版本；请结束会议后切换场景。"; return }
        cancelAnswer()
        workspaces.activate(id)
        tracker.mode = activeWorkspace.effectiveMode
        rebuildContextIndex()
        Diagnostics.record("workspace.switched", ["mode": sessionMode.rawValue])
    }

    func createWorkspace(mode: SessionMode, name: String? = nil) {
        workspaces.create(mode: mode, name: name)
        tracker.mode = activeWorkspace.effectiveMode
        rebuildContextIndex()
    }

    func duplicateWorkspace(_ id: UUID) {
        workspaces.duplicate(id)
        rebuildContextIndex()
    }

    func renameWorkspace(_ id: UUID, to name: String) {
        guard var workspace = workspaces.workspace(id), workspace.name != name else { return }
        let formerName = workspace.name
        let includeLegacyNameOnly = workspaces.workspaces.filter { $0.name == formerName }.count == 1
        workspace.name = name
        workspaces.update(workspace)
        workspaces.flush()
        updateWorkspaceNameInArchive(id, from: formerName, to: name, includeLegacyNameOnly: includeLegacyNameOnly)
    }

    func saveWorkspaceEdit(_ draft: Workspace, target: WorkspaceEditTarget) throws {
        guard let current = workspaces.workspace(draft.id) else { throw CocoaError(.fileNoSuchFile) }
        var updated = current
        switch target {
        case .basic:
            updated.name = draft.name.trimmingCharacters(in: .whitespacesAndNewlines)
            updated.notes = draft.notes
            updated.fields = draft.fields
        case .requirements:
            updated.preferences = draft.preferences
        case .lesson(let lessonID):
            guard let draftLesson = draft.lessons.first(where: { $0.id == lessonID }),
                  let index = updated.lessons.firstIndex(where: { $0.id == lessonID }) else {
                throw CocoaError(.fileNoSuchFile)
            }
            updated.lessons[index].text = draftLesson.text
        case .question(let itemID):
            guard let draftItem = draft.questionBank.first(where: { $0.id == itemID }),
                  let index = updated.questionBank.firstIndex(where: { $0.id == itemID }) else {
                throw CocoaError(.fileNoSuchFile)
            }
            KnowledgeMerger.editQuestionBankItem(&updated.questionBank[index],
                                                 question: draftItem.question, answer: draftItem.answer)
        }

        let formerName = current.name
        let includeLegacyNameOnly = workspaces.workspaces.filter { $0.name == formerName }.count == 1
        try workspaces.commit(updated)
        if formerName != updated.name {
            updateWorkspaceNameInArchive(updated.id, from: formerName, to: updated.name,
                                         includeLegacyNameOnly: includeLegacyNameOnly)
        }
        if updated.id == activeWorkspace.id, target != .requirements {
            rebuildContextIndex()
        }
    }

    private func updateWorkspaceNameInArchive(_ id: UUID, from formerName: String, to name: String,
                                              includeLegacyNameOnly: Bool) {
        archive.renameWorkspace(id, from: formerName, to: name, includeLegacyNameOnly: includeLegacyNameOnly)
    }

    func workspaceDeletionDisabledReason(_ id: UUID) -> String? {
        if DemoMode.isEnabled { return String(localized: "演示资料不可删除。") }
        if isRunning { return String(localized: "会议中不能管理系列") }
        if let workspaceDeletionRecoveryIssue { return workspaceDeletionRecoveryIssue }
        if let reason = archive.destructiveOperationDisabledReason { return reason }
        guard let workspace = workspaces.workspace(id) else { return String(localized: "系列不存在。") }
        guard workspaces.canDelete(id) else { return String(localized: "系列不存在。") }
        let includeLegacyNameOnly = workspaces.workspaces.filter { $0.name == workspace.name }.count == 1
        let sessionIDs = Set(archive.workspaceSessionIDs(id, formerName: workspace.name,
                                                        includeLegacyNameOnly: includeLegacyNameOnly))
        if sessionIDs.contains(where: { archive.summarizing.contains($0) }) {
            return String(localized: "该系列有纪要正在生成，请完成后再删除。")
        }
        return documentPreparation.workspaceDeletionBlockReason(id)
    }

    @discardableResult
    func deleteWorkspace(_ id: UUID) async -> Bool {
        if let reason = workspaceDeletionDisabledReason(id) { errorMessage = reason; return false }
        guard let workspace = workspaces.workspace(id) else { errorMessage = String(localized: "系列不存在。"); return false }
        func recheckDeletionAuthorization() -> Bool {
            guard let reason = workspaceDeletionDisabledReason(id) else { return true }
            errorMessage = reason
            return false
        }
        let includeLegacyNameOnly = workspaces.workspaces.filter { $0.name == workspace.name }.count == 1
        let sessionIDs = archive.workspaceSessionIDs(id, formerName: workspace.name,
                                                     includeLegacyNameOnly: includeLegacyNameOnly)
        let deletionFiles = sessionIDs.map { archive.folder($0) } + [
            MaterialArchive.folder(for: id),
            AppPaths.index.appendingPathComponent(id.uuidString, isDirectory: true),
            workspaces.fileURL(id)
        ]
        do {
            workspaces.flush()
            for sessionID in sessionIDs {
                guard await archive.flush(sessionID: sessionID) else {
                    let reason = archive.persistenceBySession[sessionID]?.lastFailure
                    errorMessage = "系列会议内容尚未全部保存，未删除系列或会议记录。" +
                        (reason.map { "\n" + $0 } ?? "请重试保存后再删除。")
                    return false
                }
                guard recheckDeletionAuthorization() else { return false }
            }
            let didDelete = try await WorkspaceDeletionTransaction.execute(
                prepare: {
                    try await documentPreparation.stageWorkspaceArtifactDeletion(
                        id, additionalFiles: deletionFiles)
                },
                isAuthorized: { plan in
                    recheckDeletionAuthorization()
                        && documentPreparation.workspaceArtifactDeletionPlanIsCurrent(plan)
                },
                rollback: { plan in
                    try await documentPreparation.rollbackWorkspaceArtifactDeletion(plan)
                },
                stage: { plan in
                    // Persist edits made while queue rows were being staged,
                    // then move the workspace JSON with the other resources.
                    workspaces.flush()
                    let currentRemainingWorkspaces = workspaces.workspaces.filter { $0.id != id }
                    try documentPreparation.stageWorkspaceArtifactFiles(
                        for: plan, remainingWorkspaces: currentRemainingWorkspaces)
                },
                commit: { plan in
                    try documentPreparation.markWorkspaceArtifactDeletionCommitted(plan)
                    archive.forgetWorkspaceSessionsAfterStaging(Set(sessionIDs))
                    workspaces.deleteRecordAfterStaging(id)
                    documentPreparation.finalizeWorkspaceArtifactDeletion(plan)
                })
            guard didDelete else { return false }
        } catch {
            errorMessage = String(localized: "删除失败：") + error.localizedDescription
            return false
        }
        rebuildContextIndex()
        return true
    }

    /// Pre-session preparation: what the materials say, and likely questions with
    /// short talking points (shown as "AI 读到的重点 · 可能会问").
    func predictQuestions() {
        let workspace = activeWorkspace
        guard !workspace.documents.filter(\.enabled).isEmpty || !workspace.materialText.isEmpty else {
            errorMessage = String(localized: "先添加资料，AI 才能读出重点。"); return
        }
        let topic = workspace.documents.filter(\.enabled).map(\.labeledName).joined(separator: "、")
        let who = ModeProfile.for(workspace.effectiveMode).role
        let lessons = workspace.experienceNotes(limit: 800)
        let carried = lessons.isEmpty ? "" : "以前的经验（结合上一场提醒我）：\n\(lessons)\n"
        // l10n-ignore: prompt for the model
        ask("请根据这个系列的资料（\(topic.isEmpty ? "系列信息" : topic)）为本次\(workspace.effectiveMode.title)做会前准备，我是\(who)。\(carried)严格按下面两节输出：\n## 我读到的重点\n3 条，每条一句，写这场会最需要记住的事（例如岗位偏重什么、上一场卡在哪里）。\n## 可能会问\n5 个最可能被问到的问题，每个问题后面用一两句给出简洁自然的参考回答要点；引用资料时标注来源页码；题库里已有准备的问题优先使用准备好的答案。", screenshot: nil)
    }

    // MARK: Knowledge (experience bank and question bank)

    func addLesson(_ text: String) {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        activeWorkspace.lessons.append(WorkspaceLesson(text: trimmed))
    }

    func updateLesson(_ lesson: WorkspaceLesson) {
        guard let index = activeWorkspace.lessons.firstIndex(where: { $0.id == lesson.id }) else { return }
        activeWorkspace.lessons[index] = lesson
    }

    func deleteLesson(_ id: UUID) { activeWorkspace.lessons.removeAll { $0.id == id } }

    func addBankItem(question: String, answer: String) {
        guard !question.trimmingCharacters(in: .whitespaces).isEmpty else { return }
        activeWorkspace.questionBank.append(QuestionBankItem(question: question, answer: answer, timesAsked: 0))
        rebuildContextIndex()
    }

    func updateBankItem(_ item: QuestionBankItem) {
        guard let index = activeWorkspace.questionBank.firstIndex(where: { $0.id == item.id }) else { return }
        var next = activeWorkspace.questionBank[index]
        KnowledgeMerger.editQuestionBankItem(&next, question: item.question, answer: item.answer)
        activeWorkspace.questionBank[index] = next
        rebuildContextIndex()
    }

    func restoreBankAnswer(itemID: UUID, revisionID: UUID) {
        guard let index = activeWorkspace.questionBank.firstIndex(where: { $0.id == itemID }) else { return }
        var next = activeWorkspace.questionBank[index]
        guard KnowledgeMerger.restoreAnswerRevision(revisionID, on: &next) else { return }
        activeWorkspace.questionBank[index] = next
        rebuildContextIndex()
    }

    func deleteBankItem(_ id: UUID) {
        activeWorkspace.questionBank.removeAll { $0.id == id }
        rebuildContextIndex()
    }

    /// Generated review output is queued as suggestions. It never changes reusable
    /// knowledge until the user makes a per-candidate decision.
    @discardableResult
    func collectReviewCandidates(_ summary: SessionSummary, sessionID: UUID, workspaceID: UUID?) -> Int {
        guard !isPreparingForTermination else { return 0 }
        guard summary.appliedToWorkspace != true, let workspaceID, var workspace = workspaces.workspace(workspaceID) else { return 0 }
        let ids = KnowledgeMerger.collectCandidates(lessons: summary.lessons ?? [], bank: summary.bank ?? [],
                                                    into: &workspace, session: sessionID, originID: sessionID)
        guard !ids.isEmpty else { return 0 }
        do { try workspaces.commit(workspace) }
        catch {
            errorMessage = "保存知识候选失败：\(error.localizedDescription)"
            return 0
        }
        Diagnostics.record("knowledge.candidates.collected", ["count": ids.count])
        return ids.count
    }

    func reviewKnowledgeCandidate(_ candidateID: String, decision: WorkspaceKnowledgeDecision,
                                  workspaceID: UUID, operationID: UUID = UUID()) {
        guard !isPreparingForTermination else { return }
        guard var workspace = workspaces.workspace(workspaceID) else { return }
        guard let receipt = KnowledgeMerger.applyCandidate(candidateID, decision: decision,
                                                            operationID: operationID, into: &workspace) else {
            if KnowledgeMerger.refreshCandidateBase(candidateID, in: &workspace) {
                do { try workspaces.commit(workspace) }
                catch { errorMessage = "保存候选状态失败：\(error.localizedDescription)"; return }
                errorMessage = "这个问题的现有答法刚刚有变化。请核对当前答案后再选择。"
            } else {
                errorMessage = "这个候选已变化或不再可用，请重新打开后核对。"
            }
            return
        }
        do {
            try workspaces.commit(workspace)
            if workspaceID == activeWorkspace.id { rebuildContextIndex() }
            Diagnostics.record("knowledge.candidate.reviewed", ["decision": decision.rawValue,
                                                                   "source_counted": String(receipt.sourceCounted)])
        } catch {
            errorMessage = "保存知识更新失败：\(error.localizedDescription)"
        }
    }

    /// Queue only note entries whose current transcript evidence was explicitly supported.
    /// The review pane is deliberately not trusted as the source of truth; rebuild the
    /// references from the persisted session snapshot before committing proposals.
    func collectSeriesChangeCandidates(sessionID: UUID, workspaceID: UUID) async -> Int {
        guard !isPreparingForTermination,
              let meta = archive.sessions.first(where: { $0.id == sessionID }),
              meta.workspaceID == workspaceID, meta.workspaceDeleted != true else { return 0 }
        guard await archive.flush(sessionID: sessionID) else {
            errorMessage = "本场纪要尚未保存，无法收集系列事项。请等待保存完成后重试。"
            return 0
        }
        guard !isPreparingForTermination,
              let detail = await archive.detail(sessionID),
              detail.meta.workspaceID == workspaceID, detail.meta.workspaceDeleted != true,
              archive.sessions.contains(where: { $0.id == sessionID && $0.workspaceID == workspaceID && $0.workspaceDeleted != true }),
              var workspace = workspaces.workspace(workspaceID) else { return 0 }
        let evidence = SessionEvidenceBuilder.build(meta: detail.meta, lines: detail.lines,
                                                    notes: detail.notes, summary: detail.summary)
        let added = WorkspaceSeriesChangeMerger.collectCandidates(from: evidence, sessionID: sessionID,
                                                                  into: &workspace)
        guard !added.isEmpty else { return 0 }
        do { try workspaces.commit(workspace) }
        catch {
            errorMessage = "保存系列事项候选失败：\(error.localizedDescription)"
            return 0
        }
        return added.count
    }

    /// Review one proposed series item. Replace is guarded by the revision shown in the
    /// card, and the proposal's evidence must still be current and explicitly supported.
    func reviewSeriesChangeCandidate(_ candidateID: String, decision: WorkspaceSeriesChangeDecision,
                                     workspaceID: UUID, targetItemID: UUID? = nil,
                                     expectedRevision: Int? = nil,
                                     reviewedDetails: WorkspaceSeriesChangeReviewDetails? = nil,
                                     operationID: UUID = UUID()) async {
        guard !isPreparingForTermination,
              let initialWorkspace = workspaces.workspace(workspaceID),
              let initialCandidate = initialWorkspace.seriesChangeCandidates?.first(where: { $0.id == candidateID }) else { return }
        // A receipt is the durable result of the user's first click. Replays remain
        // successful even if the source is later edited or removed.
        if initialCandidate.receipt != nil || initialCandidate.status != .pending { return }
        var currentEvidence: SessionEvidenceReference?
        if decision != .reject {
            guard let meta = archive.sessions.first(where: { $0.id == initialCandidate.sourceSessionID }),
                  meta.workspaceID == workspaceID, meta.workspaceDeleted != true,
                  await archive.flush(sessionID: initialCandidate.sourceSessionID) else {
                errorMessage = "本场纪要尚未保存，无法确认系列依据。请等待保存完成后重试。"
                return
            }
            guard !isPreparingForTermination,
                  let detail = await archive.detail(initialCandidate.sourceSessionID),
                  detail.meta.workspaceID == workspaceID, detail.meta.workspaceDeleted != true,
                  archive.sessions.contains(where: {
                      $0.id == initialCandidate.sourceSessionID && $0.workspaceID == workspaceID && $0.workspaceDeleted != true
                  }),
                  let current = SessionEvidenceBuilder.build(meta: detail.meta, lines: detail.lines,
                                                             notes: detail.notes, summary: detail.summary)
                    .first(where: { $0.id == initialCandidate.evidenceID }) else {
                errorMessage = "这条系列建议的依据已变化或不可用，请重新核对后再接受。"
                return
            }
            currentEvidence = current
        }

        // `archive.detail` suspends this main-actor method. Rebase on the latest workspace
        // after that suspension so a concurrent review or action-state change is not lost.
        guard !isPreparingForTermination,
              var workspace = workspaces.workspace(workspaceID),
              let candidate = workspace.seriesChangeCandidates?.first(where: { $0.id == candidateID }) else { return }
        if candidate.receipt != nil { return }
        if decision != .reject {
            guard let currentEvidence,
                  WorkspaceSeriesChangeMerger.matchesCurrentEvidence(candidate, reference: currentEvidence) else {
                errorMessage = "这条系列建议的依据已变化或不可用，请重新核对后再接受。"
                return
            }
        }
        guard let receipt = WorkspaceSeriesChangeMerger.applyCandidate(candidateID, decision: decision,
                                                                        operationID: operationID,
                                                                        targetItemID: targetItemID,
                                                                        expectedRevision: expectedRevision,
                                                                        reviewedDetails: reviewedDetails,
                                                                        into: &workspace) else {
            errorMessage = "系列内容刚刚有变化，请刷新后核对当前版本。"
            return
        }
        do {
            try workspaces.commit(workspace)
            Diagnostics.record("series.change.reviewed", ["decision": receipt.decision.rawValue,
                                                             "kind": candidate.kind.rawValue])
        } catch {
            errorMessage = "保存系列变更失败：\(error.localizedDescription)"
        }
    }

    func updateSeriesActionState(_ status: WorkspaceSeriesItem.ActionState, itemID: UUID,
                                 expectedRevision: Int, workspaceID: UUID) {
        guard !isPreparingForTermination, var workspace = workspaces.workspace(workspaceID),
              WorkspaceSeriesChangeMerger.setActionState(status, itemID: itemID,
                                                          expectedRevision: expectedRevision,
                                                          into: &workspace) else { return }
        do { try workspaces.commit(workspace) }
        catch { errorMessage = "保存事项进度失败：\(error.localizedDescription)" }
    }

    // MARK: Stage review

    /// Reviews every summarized session of a workspace together: trends, recurring
    /// questions and weaknesses, a preparation list, and new lessons.
    func generateStageReview(for workspaceID: UUID) {
        guard !stageReviewing.contains(workspaceID), let workspace = workspaces.workspace(workspaceID) else { return }
        let model = currentAnswerModel
        guard !model.isEmpty else { errorMessage = "请先在设置中选择回答模型。"; return }
        let metas = archive.sessions.filter { $0.workspaceID == workspaceID && $0.summaryState == .ready }
        guard !metas.isEmpty else { errorMessage = "这个场景还没有带摘要的会话记录。先完成一次会话并生成摘要。"; return }
        stageReviewing.insert(workspaceID)
        let backend = answerBackend, reasoning = codexReasoning
        Task {
            defer { stageReviewing.remove(workspaceID) }
            var digests: [StageReviewPrompt.SessionDigest] = []
            for meta in metas {
                if let summary = await archive.detail(meta.id)?.summary {
                    digests.append(.init(title: summary.title, date: meta.startedAt, summary: summary.markdown))
                }
            }
            do {
                let request = AnswerRequest(system: StageReviewPrompt.system, user: StageReviewPrompt.make(workspace: workspace, sessions: digests),
                                            model: model, effort: reasoning, imageURL: nil)
                let markdown: String
                switch backend {
                case .openAICompatible:
                    markdown = try await summaryAPIClient.answer(request, baseURL: apiBaseURL, apiKey: apiKey) { _ in }
                case .codexExec, .codexAppServer:
                    markdown = try await summaryCodex.run(prompt: request.fullPrompt, model: model, reasoning: reasoning, timeout: 600) { _ in }
                }
                guard !isPreparingForTermination, var latest = workspaces.workspace(workspaceID) else { return }
                latest.stageReview = markdown
                latest.stageReviewAt = Date()
                let parsed = SessionSummaryParser.parseAll(markdown)
                _ = KnowledgeMerger.collectCandidates(lessons: parsed.lessons, bank: parsed.bank,
                                                       into: &latest, session: nil, originID: workspaceID)
                try workspaces.commit(latest)
                Diagnostics.record("stage_review.completed", ["sessions": digests.count])
            } catch {
                errorMessage = "阶段复盘生成失败：\(error.localizedDescription)"
            }
        }
    }

    // MARK: Data export / import

    func exportAllData(includeSecrets: Bool) {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [UTType(filenameExtension: "ntbackup") ?? .zip, UTType(filenameExtension: "ihbackup") ?? .zip]
        panel.nameFieldStringValue = "NextThought-\(Date.now.formatted(.iso8601.year().month().day())).ntbackup"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        workspaces.flush()
        let items = workspaces.workspaces
        let skills = SkillLibrary.shared.skills
        let glossary = GlossaryStore.shared.glossary
        runDataTask("正在导出…") {
            try await Task.detached(priority: .userInitiated) { try DataBackup.exportAll(to: url, includeSecrets: includeSecrets, workspaces: items, skills: skills, glossary: glossary) }.value
            return "已导出到 \(url.lastPathComponent)。"
        }
    }

    func exportWorkspace(_ id: UUID, includeSessions: Bool) {
        guard let workspace = workspaces.workspace(id) else { return }
        let panel = NSSavePanel()
        panel.allowedContentTypes = [UTType(filenameExtension: "ntbackup") ?? .zip, UTType(filenameExtension: "ihbackup") ?? .zip]
        panel.nameFieldStringValue = "\(workspace.name.replacingOccurrences(of: "/", with: "-")).ntbackup"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        workspaces.flush()
        let sessions = includeSessions ? archive.sessions.filter { $0.workspaceID == id }.map(\.id) : []
        let skills = SkillLibrary.shared.skills
        runDataTask("正在导出场景…") {
            try await Task.detached(priority: .userInitiated) { try DataBackup.exportWorkspace(workspace, sessions: sessions, to: url, skills: skills) }.value
            return "已导出“\(workspace.name)”。"
        }
    }

    /// Asks for a file and merges it (settings optional).
    func importData(applySettings: Bool) {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [UTType(filenameExtension: "ntbackup") ?? .zip, UTType(filenameExtension: "ihbackup") ?? .zip, .zip]
        panel.allowsMultipleSelection = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        importArchive(url, applySettings: applySettings)
    }

    func importArchive(_ url: URL, applySettings: Bool) {
        guard !isRunning else { errorMessage = "请先停止辅助再导入数据。"; return }
        runDataTask("正在校验并恢复…") {
            let summary = try await DataBackup.importArchive(url, applySettings: applySettings)
            self.archive.load(); self.rebuildContextIndex()
            return summary.description + (applySettings && summary.settingsApplied > 0 ? " 设置在重新打开应用后完全生效。" : "")
        }
    }

    func backupNow() {
        runDataTask("正在备份…") {
            let url = try await DataBackup.automaticBackup(reason: "manual")
            return "已备份到 \(url.lastPathComponent)。"
        }
    }

    private func runDataTask(_ progress: String, _ work: @escaping () async throws -> String) {
        dataStatus = progress
        isDataBusy = true
        Task {
            defer { isDataBusy = false }
            do { dataStatus = try await work() } catch { dataStatus = ""; errorMessage = "操作失败：\(error.localizedDescription)" }
        }
    }

    // MARK: Shortcuts

    func shortcutLabel(_ action: HotkeyAction) -> String {
        (customShortcuts[String(action.rawValue)] ?? action.defaultBinding).label
    }

    func setShortcut(_ action: HotkeyAction, binding: ShortcutBinding) {
        var next = customShortcuts
        next[String(action.rawValue)] = binding
        var effective = Dictionary(uniqueKeysWithValues: HotkeyAction.allCases.map { (String($0.rawValue), $0.defaultBinding) })
        effective.merge(next) { _, value in value }
        if let error = ShortcutBinding.validate(effective) { errorMessage = error; return }
        customShortcuts = next
        UserDefaults.standard.set(try? JSONEncoder().encode(next), forKey: "customShortcuts")
        registerHotkeys()
    }

    func resetShortcuts() {
        customShortcuts = [:]; UserDefaults.standard.removeObject(forKey: "customShortcuts"); registerHotkeys()
    }
}
