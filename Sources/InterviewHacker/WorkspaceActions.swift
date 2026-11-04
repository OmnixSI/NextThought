import AppKit
import Foundation
import InterviewHackerCore
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
            updated.questionBank[index].question = draftItem.question
            updated.questionBank[index].answer = draftItem.answer
            updated.questionBank[index].updatedAt = Date()
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
        let includeLegacyNameOnly = workspaces.workspaces.filter { $0.name == workspace.name }.count == 1
        do {
            workspaces.flush()
            let remainingWorkspaces = workspaces.workspaces.filter { $0.id != id }
            try await documentPreparation.deleteWorkspaceArtifacts(id, remainingWorkspaces: remainingWorkspaces)
            await archive.flush()
            try archive.deleteWorkspaceSessions(id, formerName: workspace.name,
                                                includeLegacyNameOnly: includeLegacyNameOnly)
            try MaterialArchive.deleteWorkspaceFiles(id)
            try SemanticIndexStore.removeWorkspace(id)
            try workspaces.delete(id)
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
        var next = item; next.updatedAt = Date()
        activeWorkspace.questionBank[index] = next
        rebuildContextIndex()
    }

    func deleteBankItem(_ id: UUID) {
        activeWorkspace.questionBank.removeAll { $0.id == id }
        rebuildContextIndex()
    }

    /// Merges a session review's lessons and improved answers into its workspace.
    @discardableResult
    func applyReview(_ summary: SessionSummary, sessionID: UUID, workspaceID: UUID?) -> (lessons: Int, questions: Int)? {
        guard let workspaceID, var workspace = workspaces.workspace(workspaceID) else { return nil }
        let result = KnowledgeMerger.merge(lessons: summary.lessons ?? [], bank: (summary.bank ?? []).map { ($0.question, $0.answer) },
                                           into: &workspace, session: sessionID)
        workspaces.update(workspace)
        if workspaceID == activeWorkspace.id { rebuildContextIndex() }
        var applied = summary
        applied.appliedToWorkspace = true
        archive.saveSummary(applied, for: sessionID)
        Diagnostics.record("knowledge.applied", ["lessons": result.lessons, "questions": result.questions])
        return result
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
                guard var latest = workspaces.workspace(workspaceID) else { return }
                latest.stageReview = markdown
                latest.stageReviewAt = Date()
                if autoApplyLessons {
                    _ = KnowledgeMerger.merge(lessons: SessionSummaryParser.parseAll(markdown).lessons, bank: [], into: &latest, session: nil)
                }
                workspaces.update(latest)
                Diagnostics.record("stage_review.completed", ["sessions": digests.count])
            } catch {
                errorMessage = "阶段复盘生成失败：\(error.localizedDescription)"
            }
        }
    }

    // MARK: Data export / import

    func exportAllData(includeSecrets: Bool) {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [UTType(filenameExtension: "ihbackup") ?? .zip]
        panel.nameFieldStringValue = "InterviewHacker-\(Date.now.formatted(.iso8601.year().month().day())).ihbackup"
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
        panel.allowedContentTypes = [UTType(filenameExtension: "ihbackup") ?? .zip]
        panel.nameFieldStringValue = "\(workspace.name.replacingOccurrences(of: "/", with: "-")).ihbackup"
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
        panel.allowedContentTypes = [UTType(filenameExtension: "ihbackup") ?? .zip, .zip]
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
