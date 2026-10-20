import AppKit
import Foundation
import InterviewHackerCore
import UniformTypeIdentifiers

/// Workspace (scenario) management, materials, knowledge and data import/export.
@MainActor
extension AppState {
    // MARK: Materials

    static let maxDocuments = 40

    /// Imports files into the current scenario.
    ///
    /// Every file is converted to Markdown, archived on disk, filed into the
    /// material slot that fits it and added to the retrieval index in one go, so
    /// the user can drop a whole folder in without sorting it by hand. A file
    /// that cannot be read only fails that one file.
    func importDocuments(_ urls: [URL], category: String? = nil) {
        guard !isImportingDocuments else { return }
        let workspaceID = activeWorkspace.id
        let categories = ModeProfile.for(activeWorkspace.mode).documentCategories
        isImportingDocuments = true
        importStatus = "正在把 \(urls.count) 个文件转成 Markdown…"
        importTask = Task {
            defer { isImportingDocuments = false; importTask = nil }
            do {
                let outcome = try await DocumentImporter.read(urls, categories: categories)
                try Task.checkCancellation()
                guard var workspace = workspaces.workspace(workspaceID) else { return }
                guard workspace.documents.count + outcome.documents.count <= Self.maxDocuments else {
                    throw AppFailure.capture("每个场景最多 \(Self.maxDocuments) 份资料，请先移除不需要的文件。")
                }
                var names = Set(workspace.documents.map(\.name))
                var total = workspace.documents.reduce(0) { $0 + $1.characterCount }
                var added = 0
                var skipped = 0
                for imported in outcome.documents {
                    // The size limit is per scenario, not per import: a file that
                    // does not fit is reported instead of discarding the files
                    // that do fit.
                    if total + imported.characterCount > 600_000 {
                        skipped += 1
                        continue
                    }
                    // The Markdown is archived silently next to the other app
                    // data; that file is the material the model reads.
                    let archived = try? MaterialArchive.archive(imported.markdown, name: imported.name,
                                                               format: imported.format, workspaceID: workspaceID)
                    var document = ContextDocument(name: imported.fileName,
                                                  pages: imported.sections,
                                                  enabled: true,
                                                  category: category ?? imported.suggestedCategory,
                                                  markdownPath: archived?.path,
                                                  sourceFormat: imported.format)
                    let original = document.name
                    var suffix = 2
                    while names.contains(document.name) { document.name = "\(original) (\(suffix))"; suffix += 1 }
                    names.insert(document.name)
                    workspace.documents.append(document)
                    total += imported.characterCount
                    added += 1
                }
                guard added > 0 else {
                    throw AppFailure.capture("这个场景的资料总量超过 60 万字，请先移除部分资料再导入。")
                }
                workspaces.update(workspace)
                rebuildContextIndex()
                importStatus = Self.importSummary(added: added, failures: outcome.failures.count, skipped: skipped)
                if !outcome.failures.isEmpty {
                    errorMessage = "有 \(outcome.failures.count) 个文件没有导入：\n"
                        + outcome.failures.map { "\($0.name)：\($0.message)" }.joined(separator: "\n")
                }
            } catch is CancellationError {
                importStatus = ""
            } catch {
                importStatus = ""
                errorMessage = "资料导入失败：\(error.localizedDescription)"
            }
        }
    }

    private static func importSummary(added: Int, failures: Int, skipped: Int) -> String {
        var parts = ["已导入 \(added) 份资料"]
        if added > 0 { parts.append("已归档为 Markdown") }
        if failures > 0 { parts.append("\(failures) 个文件失败") }
        if skipped > 0 { parts.append("\(skipped) 个文件超出 60 万字上限") }
        return parts.joined(separator: " · ")
    }

    func rebuildContextIndex() {
        let revision = UUID(); documentRevision = revision
        let snapshot = activeWorkspace.retrievalDocuments
        // Never reuse a disabled/removed document while a replacement index builds.
        contextIndex = SessionContextIndex(); selectedExcerpts = []
        isIndexingDocuments = true
        Task {
            let index = await Task.detached(priority: .userInitiated) { SessionContextIndex(documents: snapshot) }.value
            guard documentRevision == revision else { return }
            contextIndex = index
            isIndexingDocuments = false
        }
    }

    func removeDocument(_ id: UUID) { documents.removeAll { $0.id == id }; rebuildContextIndex() }

    func setDocumentEnabled(_ id: UUID, enabled: Bool) {
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
        workspaces.activate(id)
        tracker.mode = sessionMode
        rebuildContextIndex()
        Diagnostics.record("workspace.switched", ["mode": sessionMode.rawValue])
    }

    func createWorkspace(mode: SessionMode, name: String? = nil) {
        workspaces.create(mode: mode, name: name)
        tracker.mode = sessionMode
        rebuildContextIndex()
    }

    func duplicateWorkspace(_ id: UUID) {
        workspaces.duplicate(id)
        rebuildContextIndex()
    }

    func deleteWorkspace(_ id: UUID) {
        workspaces.delete(id)
        rebuildContextIndex()
    }

    /// Pre-session preparation: likely questions with short talking points from the materials.
    func predictQuestions() {
        let workspace = activeWorkspace
        guard !workspace.documents.filter(\.enabled).isEmpty || !workspace.materialText.isEmpty else {
            errorMessage = "请先在场景里填写信息或添加资料。"; return
        }
        section = .assistant
        let topic = workspace.documents.filter(\.enabled).map(\.labeledName).joined(separator: "、")
        let who = ModeProfile.for(workspace.mode).role
        ask("请根据这个场景的资料（\(topic.isEmpty ? "场景信息" : topic)），预测本次\(workspace.mode.title)中我（\(who)）最可能被问到的 8 个问题，并为每个问题给出 1–2 句可直接说的回答要点；引用资料时标注来源页码；题库里已有准备的问题优先使用准备好的答案。", screenshot: nil)
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
        runDataTask("正在导出…") {
            try await Task.detached(priority: .userInitiated) { try DataBackup.exportAll(to: url, includeSecrets: includeSecrets, workspaces: items, skills: skills) }.value
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
        do {
            let summary = try DataBackup.importArchive(url, applySettings: applySettings)
            archive.load()
            rebuildContextIndex()
            dataStatus = summary.description + (applySettings && summary.settingsApplied > 0 ? " 设置在重新打开应用后完全生效。" : "")
        } catch {
            errorMessage = "导入失败：\(error.localizedDescription)"
        }
    }

    func backupNow() {
        runDataTask("正在备份…") {
            let url = try DataBackup.automaticBackup(reason: "manual")
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
