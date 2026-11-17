import AppKit
import CryptoKit
import Foundation
import NextThoughtCore
import Observation

@MainActor
final class WorkspaceArtifactDeletionPlan {
    var journal: WorkspaceDeletionJournal
    var workspaceID: UUID { journal.workspaceID }
    var targetIDs: Set<UUID> { Set(journal.jobs.map(\.id)) }
    var targetJobs: [DocumentJob] { journal.jobs }

    init(journal: WorkspaceDeletionJournal) { self.journal = journal }
}

/// Owns preparation independently of whichever workspace the user is viewing.
/// Only finished, validated files are committed to workspaces.
@MainActor @Observable
final class DocumentPreparation {
    private(set) var jobs: [DocumentJob] = []
    var error: String?
    @ObservationIgnored private var store: DocumentJobStore?
    @ObservationIgnored private var tasks: [UUID: Task<Void, Never>] = [:]
    @ObservationIgnored private var intake: [UUID: Task<Void, Never>] = [:]
    @ObservationIgnored private var ticker: Task<Void, Never>?
    @ObservationIgnored private var loaded = false
    private(set) var ready = false
    /// Files whose own text is already in their workspace while their pictures are read.
    private(set) var textReady: Set<UUID> = []
    static var root: URL { AppPaths.root.appendingPathComponent("Preparation", isDirectory: true) }
    static var originals: URL { AppPaths.root.appendingPathComponent("Originals", isDirectory: true) }

    func start(testing: Bool = false) {
        guard !loaded, (!DemoMode.isEnabled || testing) else { return }
        loaded = true
        Task {
            do {
                let url = Self.root.appendingPathComponent("jobs.sqlite")
                store = try await Task.detached { try DocumentJobStore(url: url) }.value
                let recovered = try await store!.all(recoverInterrupted: true)
                let currentIDs = Set(jobs.map(\.id))
                jobs = recovered.filter { !currentIDs.contains($0.id) } + jobs
                ready = true
                for job in jobs where job.stage == .receiving { receive(job, source: nil) }
                ticker = Task { [weak self] in
                    while !Task.isCancelled {
                        self?.pump()
                        do { try await Task.sleep(for: .milliseconds(500)) } catch { return }
                    }
                }
            } catch { self.error = "任务队列无法打开：" + error.localizedDescription }
        }
    }

    func enqueue(_ urls: [URL], workspace: Workspace, category: String?, options: DocumentConversionOptions) {
        start()
        for url in urls {
            var job = DocumentJob(workspaceID: workspace.id, fileName: url.lastPathComponent, category: category,
                categories: ModeProfile.for(workspace.effectiveMode).documentCategories, provider: options.provider.rawValue,
                model: options.model, effort: options.effort, fallback: options.fallback, timeout: options.timeout)
            job.localModelID = options.localModelID
            // Retain explicit user selection across a crash during the copy.
            job.bookmark = try? url.bookmarkData(options: [.withSecurityScope], includingResourceValuesForKeys: nil, relativeTo: nil)
            jobs.append(job)
            receive(job, source: url)
        }
    }

    private func receive(_ job: DocumentJob, source: URL?) {
        guard intake[job.id] == nil else { return }
        intake[job.id] = Task { [weak self] in
            guard let self else { return }
            defer { intake[job.id] = nil }
            do {
                while store == nil { try await Task.sleep(for: .milliseconds(100)); try Task.checkCancellation() }
                try await store!.save(job)
                let root = Self.originals
                let accepted = try await Task.detached(priority: .utility) { () -> (String, String) in
                    var stale = false
                    let url: URL
                    if let source { url = source }
                    else if let bookmark = job.bookmark { url = try URL(resolvingBookmarkData: bookmark, options: [.withSecurityScope, .withoutUI], relativeTo: nil, bookmarkDataIsStale: &stale) }
                    else { throw AppFailure.capture("请重新选择尚未接收完成的文件。") }
                    let accessed = url.startAccessingSecurityScopedResource()
                    defer { if accessed { url.stopAccessingSecurityScopedResource() } }
                    let values = try url.resourceValues(forKeys: [.isRegularFileKey, .fileSizeKey])
                    guard values.isRegularFile == true, let size = values.fileSize, size <= DocumentLimits.maximumFileBytes else { throw AppFailure.capture("仅支持不超过 512 MiB 的普通文件。") }
                    guard DocumentImporter.supportedExtensions.contains(url.pathExtension.lowercased()) else { throw AppFailure.capture("暂不支持 .\(url.pathExtension) 格式。") }
                    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
                    let space = try? root.resourceValues(forKeys: [.volumeAvailableCapacityForImportantUsageKey]).volumeAvailableCapacityForImportantUsage
                    guard space == nil || space! > Int64(size) + 256 * 1024 * 1024 else { throw AppFailure.capture("磁盘剩余空间不足以接收和处理文件。") }
                    let temp = root.appendingPathComponent(".incoming-" + job.id.uuidString)
                    defer { try? FileManager.default.removeItem(at: temp) }
                    try? FileManager.default.removeItem(at: temp)
                    try FileManager.default.copyItem(at: url, to: temp)
                    try Task.checkCancellation()
                    guard let hash = ManagedModelStore.sha256(of: temp) else { throw AppFailure.capture("无法校验收到的文件。") }
                    let target = root.appendingPathComponent(hash + "." + url.pathExtension.lowercased())
                    if !FileManager.default.fileExists(atPath: target.path) { try FileManager.default.moveItem(at: temp, to: target) }
                    return (target.path, hash)
                }.value
                guard self.jobs.first(where: { $0.id == job.id })?.stage != .cancelled else { return }
                mutate(job.id) { $0.originalPath = accepted.0; $0.sourceHash = accepted.1; $0.bookmark = nil; $0.stage = .queued; $0.message = "已接收，等待处理。" }
            } catch is CancellationError { }
            catch { mutate(job.id) { $0.stage = .failed; $0.message = error.localizedDescription } }
        }
    }

    func shutdown() {
        ticker?.cancel(); ticker = nil
        for task in tasks.values { task.cancel() }
        for task in intake.values { task.cancel() }
    }
    func cancel(_ id: UUID) {
        mutate(id) { $0.stage = .cancelled; $0.message = "已取消，可重试。" }
        intake[id]?.cancel(); tasks[id]?.cancel()
    }
    func retry(_ id: UUID) {
        guard let job = jobs.first(where: { $0.id == id }), tasks[id] == nil, intake[id] == nil else { return }
        if job.originalPath == nil {
            if job.bookmark != nil { mutate(id) { $0.stage = .receiving }; receive(job, source: nil) }
            else { error = "文件未接收，请重新选择 \(job.fileName)。" }
        } else { mutate(id) {
            if $0.stage == .warning { $0.documentID = nil }
            $0.stage = $0.documentID == nil ? .queued : .embedding; $0.message = "重试中，复用已完成内容。"
        } }
    }
    func pauseForMeeting() {
        for (id, task) in tasks { mutate(id) { $0.stage = .paused; $0.message = "会议中使用已准备资料，结束后继续。" }; task.cancel() }
    }
    var pendingCount: Int { jobs.filter { !$0.stage.terminal }.count }
    func pendingCount(workspaceID: UUID) -> Int { jobs.filter { $0.workspaceID == workspaceID && !$0.stage.terminal }.count }

    func workspaceDeletionBlockReason(_ workspaceID: UUID) -> String? {
        guard ready, store != nil else { return String(localized: "资料任务队列正在加载，请稍后删除系列。") }
        let hasActiveJob = jobs.contains { job in
            job.workspaceID == workspaceID && (!job.stage.terminal || tasks[job.id] != nil || intake[job.id] != nil)
        }
        return hasActiveJob ? String(localized: "该系列仍有资料任务在处理，请完成或取消任务后再删除。") : nil
    }

    /// Removes only the persisted queue rows. Artifact files and in-memory jobs
    /// remain untouched until the caller rechecks deletion authorization.
    func stageWorkspaceArtifactDeletion(_ workspaceID: UUID,
                                       additionalFiles: [URL]) async throws -> WorkspaceArtifactDeletionPlan {
        guard let store, ready else { throw AppFailure.capture("资料任务队列正在加载，请稍后删除系列。") }
        if let reason = workspaceDeletionBlockReason(workspaceID) { throw AppFailure.capture(reason) }
        let targetJobs = jobs.filter { $0.workspaceID == workspaceID }
        let originalsRoot = Self.originals.standardizedFileURL.path + "/"
        var sources = additionalFiles
        sources += targetJobs.map { Self.root.appendingPathComponent($0.id.uuidString, isDirectory: true) }
        for job in targetJobs {
            guard let path = job.originalPath else { continue }
            let file = URL(fileURLWithPath: path).standardizedFileURL
            if file.path.hasPrefix(originalsRoot) { sources.append(file) }
        }
        let stagingRoot = Self.root.deletingLastPathComponent()
            .appendingPathComponent(".workspace-delete-\(UUID().uuidString)", isDirectory: true)
        let moves = WorkspaceDeletionFileStaging.manifest(sources: sources, stagingRoot: stagingRoot)
        let journal = WorkspaceDeletionJournal(workspaceID: workspaceID, stagingRoot: stagingRoot,
                                               jobs: targetJobs, moves: moves)
        try await store.stageWorkspaceDeletion(journal)
        return WorkspaceArtifactDeletionPlan(journal: journal)
    }

    func workspaceArtifactDeletionPlanIsCurrent(_ plan: WorkspaceArtifactDeletionPlan) -> Bool {
        Set(jobs.filter { $0.workspaceID == plan.workspaceID }.map(\.id)) == plan.targetIDs
            && workspaceDeletionBlockReason(plan.workspaceID) == nil
    }

    /// Reverses the queue-row staging when a read failure or other guard cancels
    /// the deletion. Restore the latest in-memory snapshot for each staged job.
    func rollbackWorkspaceArtifactDeletion(_ plan: WorkspaceArtifactDeletionPlan) async throws {
        guard let store else { throw AppFailure.capture("资料任务队列尚未就绪，无法恢复待删除任务。") }
        try WorkspaceDeletionFileStaging.rollback(plan.journal.moves, stagingRoot: plan.journal.stagingRoot)
        try await store.restoreWorkspaceDeletionRows(plan.journal.transactionID)
    }

    /// Moves files out of their live paths so later synchronous workspace
    /// deletion can still roll them back if another operation fails.
    func stageWorkspaceArtifactFiles(for plan: WorkspaceArtifactDeletionPlan,
                                     remainingWorkspaces: [Workspace],
                                     move: (URL, URL) throws -> Void = { try FileManager.default.moveItem(at: $0, to: $1) }) throws {
        guard workspaceArtifactDeletionPlanIsCurrent(plan) else {
            throw AppFailure.capture("系列资料任务已变化，请重新确认删除。")
        }
        let currentWorkspaceSourceHashes = Set(remainingWorkspaces.flatMap(\.documents).compactMap { $0.content?.sourceHash })
        let currentMoves = WorkspaceDeletionFileStaging.excludingSharedOriginals(
            plan.journal.moves,
            deletingJobs: plan.targetJobs,
            currentJobs: jobs,
            currentWorkspaceSourceHashes: currentWorkspaceSourceHashes,
            originalsRoot: Self.originals)
        if currentMoves != plan.journal.moves {
            var updatedJournal = plan.journal
            updatedJournal.moves = currentMoves
            try DocumentJobStore.updateWorkspaceDeletionJournal(
                updatedJournal, at: Self.root.appendingPathComponent("jobs.sqlite"))
            plan.journal = updatedJournal
        }
        try WorkspaceDeletionFileStaging.stage(currentMoves, move: move)
    }

    /// Persists the transaction's commit point. The following in-memory cleanup
    /// is non-throwing, while a crash is finished by the launch recovery path.
    func markWorkspaceArtifactDeletionCommitted(_ plan: WorkspaceArtifactDeletionPlan) throws {
        try DocumentJobStore.markWorkspaceDeletionCommitted(
            plan.journal.transactionID,
            at: Self.root.appendingPathComponent("jobs.sqlite"))
    }

    /// Finishes after the journal records a committed deletion.
    func finalizeWorkspaceArtifactDeletion(_ plan: WorkspaceArtifactDeletionPlan) {
        jobs.removeAll { plan.targetIDs.contains($0.id) }
        DocumentJobStore.finishWorkspaceDeletion(plan.journal.transactionID,
                                                 at: Self.root.appendingPathComponent("jobs.sqlite"))
    }
    private func mutate(_ id: UUID, _ change: (inout DocumentJob) -> Void) {
        guard let i = jobs.firstIndex(where: { $0.id == id }) else { return }
        change(&jobs[i])
        if jobs[i].stage != .waiting { jobs[i].waitingReason = nil }
        jobs[i].updatedAt = Date()
        let value = jobs[i], store = store
        Task { [weak self] in do { try await store?.save(value) } catch { self?.error = "队列保存失败：" + error.localizedDescription } }
    }
    private func progress(_ id: UUID, _ event: DocumentJobEvent) {
        guard let job = jobs.first(where: { $0.id == id }), !job.stage.terminal, job.stage != .paused else { return }
        mutate(id) { $0.stage = event.stage; $0.completed = event.completed; $0.total = event.total; $0.message = event.message }
    }
    private func pump() {
        guard let state = loaded ? AppState.shared : nil, !state.isRunning, !state.isChangingSession else { return }
        for job in jobs where job.stage == .paused { mutate(job.id) { $0.stage = $0.documentID == nil ? .queued : .embedding; $0.message = "会议已结束，继续准备。" } }
        for job in jobs where job.stage == .waiting && Date().timeIntervalSince(job.updatedAt) > 5 {
            let available: Bool = switch job.waitingReason {
            case .runtime: state.runtimeInstalled
            case .ocrModels:
                DocumentRuntime.installed
                    && (ModelCatalog.descriptor(for: job.localModelID ?? ModelCatalog.paddleOCR.id).map(ManagedModelStore.isInstalled) ?? false)
                    && ManagedModelStore.isInstalled(ModelCatalog.layout)
            case .codexConnection: state.codexReady
            case .legacyOffice: LegacyOfficeConverter.available
            case .semantic: state.documentAvailability.ready(.semantic)
            case nil: true // Older queues acquire a typed reason on their next attempt.
            }
            if available { mutate(job.id) { $0.stage = $0.documentID == nil ? .queued : .embedding } }
        }
        // Reading a file and making vectors for one that is read are different work: the
        // next file is read while the previous one is indexed. Two files are read at a
        // time (the local reader takes their pictures in turn, so one model is loaded).
        let active = jobs.filter { tasks[$0.id] != nil }
        var light = active.filter { Self.isLight($0) }.count
        var vectors = active.filter { $0.documentID != nil }.count
        var reads = active.count - light - vectors
        for job in jobs where [.queued, .embedding].contains(job.stage) && tasks[job.id] == nil {
            if Self.isLight(job) { guard light < 2 else { continue }; light += 1 }
            else if job.documentID != nil { guard vectors < 1 else { continue }; vectors += 1 }
            else { guard reads < 2 else { continue }; reads += 1 }
            process(job)
        }
    }
    /// Puts a read file into its workspace, in place of an earlier read of the same file.
    private func commit(_ result: MarkdownConverter.Result, for job: DocumentJob, format ext: String) async throws -> UUID {
        guard let workspace = AppState.shared.workspaces.workspace(job.workspaceID) else { throw CancellationError() }
        let existing = workspace.documents.first(where: { $0.content?.sourceHash == job.sourceHash && $0.sourceFormat == ext })
        let id = existing?.id ?? UUID(), workspaceID = job.workspaceID
        let category = job.category ?? existing?.category ?? DocumentClassifier.classify(name: job.fileName, content: String(result.markdown.prefix(20000)), categories: job.categories)
        var document = try await Task.detached(priority: .utility) { () -> ContextDocument in
            let archived = try MaterialArchive.archive(result.markdown, name: (job.fileName as NSString).deletingPathExtension, format: ext, workspaceID: workspaceID)
            if let content = result.content { try JSONEncoder().encode(content).write(to: archived.deletingPathExtension().appendingPathExtension("json"), options: .atomic) }
            return ContextDocument(id: id, name: job.fileName, pages: result.sections, category: category, markdownPath: MaterialArchive.relative(archived), sourceFormat: ext, content: result.content)
        }.value
        try Task.checkCancellation()
        // Refresh after awaiting disk I/O, preserving concurrent file commits.
        guard !AppState.shared.isRunning, var latest = AppState.shared.workspaces.workspace(job.workspaceID) else { throw CancellationError() }
        if let position = latest.documents.firstIndex(where: { $0.content?.sourceHash == job.sourceHash && $0.sourceFormat == ext }) {
            document.id = latest.documents[position].id
            document.enabled = latest.documents[position].enabled
            latest.documents[position] = document
        } else { latest.documents.append(document) }
        AppState.shared.workspaces.update(latest)
        return document.id
    }

    /// The file's own text is complete; its pictures are still being read.
    private func textArrived(_ result: MarkdownConverter.Result, for job: DocumentJob, format ext: String) async {
        guard let current = jobs.first(where: { $0.id == job.id }), !current.stage.terminal, current.stage != .paused else { return }
        guard (try? await commit(result, for: job, format: ext)) != nil else { return }
        textReady.insert(job.id)
        if AppState.shared.activeWorkspace.id == job.workspaceID { AppState.shared.rebuildContextIndex() }
    }

    private static func isLight(_ job: DocumentJob) -> Bool {
        job.documentID == nil && job.provider == "system" && ["txt", "md", "markdown", "csv", "json", "rtf"].contains((job.fileName as NSString).pathExtension.lowercased())
    }
    private func process(_ job: DocumentJob) {
        mutate(job.id) { $0.stage = job.documentID == nil ? .parsing : .embedding }
        tasks[job.id] = Task { [weak self] in
            guard let self else { return }
            defer { tasks[job.id] = nil }
            do {
                guard let path = job.originalPath else { throw AppFailure.capture("原文件尚未接收。") }
                guard AppState.shared.workspaces.workspace(job.workspaceID) != nil else { throw AppFailure.capture("所属场景已删除。") }
                let ext = (path as NSString).pathExtension.lowercased()
                if ext != "pdf", (["docx", "pptx", "xlsx"].contains(ext) || job.provider != "system"), !DocumentRuntime.installed {
                    mutate(job.id) { $0.stage = .waiting; $0.waitingReason = .runtime; $0.message = "需要准备共用文档组件，已接收的文件会保留。" }; return
                }
                if ext != "pdf", job.provider == "paddle", let model = ModelCatalog.descriptor(for: job.localModelID ?? ModelCatalog.paddleOCR.id),
                   (!ManagedModelStore.isInstalled(model) || !ManagedModelStore.isInstalled(ModelCatalog.layout)) {
                    mutate(job.id) { $0.stage = .waiting; $0.waitingReason = .ocrModels; $0.message = "所选 OCR 模型尚未就绪，请准备组件后继续。" }; return
                }
                if ext != "pdf", job.provider == "codex", !AppState.shared.codexReady {
                    mutate(job.id) { $0.stage = .waiting; $0.waitingReason = .codexConnection; $0.message = "等待连接 Codex。" }; return
                }
                if ext == "ppt", !LegacyOfficeConverter.available {
                    mutate(job.id) { $0.stage = .waiting; $0.waitingReason = .legacyOffice; $0.message = "旧版 PPT 需要 LibreOffice。可先转换为 PPTX 或 PDF 后重新添加。" }; return
                }
                var documentID = job.documentID
                if let id = documentID, !AppState.shared.workspaces.workspace(job.workspaceID)!.documents.contains(where: { $0.id == id }) { documentID = nil }
                // Where the time of an import goes, for the next "it is slow": reading, then vectors.
                let began = Date()
                var readSeconds = 0.0
                if documentID == nil {
                    var options = DocumentConversionOptions(provider: DocumentOCRProvider(rawValue: job.provider) ?? .system,
                        fallback: job.fallback, model: job.model, effort: job.effort, timeout: job.timeout)
                    options.localModelID = job.localModelID ?? ModelCatalog.paddleOCR.id
                    options.sourceHash = job.sourceHash
                    options.checkpointDirectory = Self.root.appendingPathComponent(job.id.uuidString, isDirectory: true)
                    options.onProgress = { [weak self] event in Task { @MainActor in self?.progress(job.id, event) } }
                    options.onText = { [weak self] text in await self?.textArrived(text, for: job, format: ext) }
                    let worker = Task.detached(priority: .utility) { try await MarkdownConverter.convert(URL(fileURLWithPath: path), options: options) }
                    let result = try await withTaskCancellationHandler { try await worker.value } onCancel: { worker.cancel() }
                    try Task.checkCancellation()
                    guard !AppState.shared.isRunning, AppState.shared.workspaces.workspace(job.workspaceID) != nil else { throw CancellationError() }
                    progress(job.id, .init(.validating, message: "OCR / 解析已完成，正在检查内容。"))
                    guard result.sections.contains(where: { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }) else { throw AppFailure.capture("没有提取到内容，请查看文件是否为空或不可读。") }
                    documentID = try await commit(result, for: job, format: ext)
                    textReady.remove(job.id)
                    readSeconds = Date().timeIntervalSince(began)
                    mutate(job.id) { $0.documentID = documentID; $0.stage = .lexical; $0.message = "OCR 已完成，正在建立文字索引。" }
                    if AppState.shared.activeWorkspace.id == job.workspaceID { AppState.shared.rebuildContextIndex() }
                }
                try Task.checkCancellation()
                if AppState.shared.semanticRetrievalEnabled {
                    guard AppState.shared.embeddingModelInstalled, AppState.shared.runtimeInstalled else {
                        mutate(job.id) { $0.stage = .waiting; $0.waitingReason = .semantic; $0.message = "文字检索已可用；语义查找组件尚未准备。" }; return
                    }
                    try await AppState.shared.prepareSemanticWorkspace(job.workspaceID) { [weak self] completed, total in
                        Task { @MainActor in self?.progress(job.id, .init(.embedding, completed: completed, total: total, message: "文字检索已可用，正在生成向量。")) }
                    }
                }
                let lines = AppState.shared.workspaces.workspace(job.workspaceID)?.documents.first(where: { $0.id == documentID })?.content?.warnings ?? []
                let warnings = DocumentNotice.warnings(in: lines)
                Diagnostics.record("document.prepared", ["format": ext, "provider": job.provider, "read_seconds": readSeconds,
                                                         "index_seconds": Date().timeIntervalSince(began) - readSeconds, "warnings": warnings.count])
                let done = AppState.shared.semanticRetrievalEnabled ? "Embedding 已完成，资料已就绪。" : "文字检索已就绪，语义查找未启用。"
                mutate(job.id) {
                    $0.stage = warnings.isEmpty ? .ready : .warning; $0.completed = $0.total ?? $0.completed
                    $0.message = warnings.isEmpty ? (DocumentNotice.note(for: lines) ?? done) : DocumentNotice.unique(lines).joined(separator: "\n")
                }
            } catch let dependency as DocumentDependencyError {
                mutate(job.id) { $0.stage = .waiting; $0.waitingReason = dependency.reason; $0.message = dependency.message }
            } catch is CancellationError {
                if jobs.first(where: { $0.id == job.id })?.stage != .cancelled { mutate(job.id) { $0.stage = .paused; $0.message = "已保存检查点，稍后继续。" } }
            } catch {
                Diagnostics.record("document.failed", ["format": ((job.originalPath ?? job.fileName) as NSString).pathExtension.lowercased(), "provider": job.provider])
                mutate(job.id) { $0.stage = .failed; $0.message = error.localizedDescription }
            }
        }
    }
}
