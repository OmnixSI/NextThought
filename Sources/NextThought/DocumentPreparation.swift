import AppKit
import CryptoKit
import Foundation
import NextThoughtCore
import Observation

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
                categories: ModeProfile.for(workspace.mode).documentCategories, provider: options.provider.rawValue,
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

    func deleteWorkspaceArtifacts(_ workspaceID: UUID, remainingWorkspaces: [Workspace]) async throws {
        guard let store, ready else { throw AppFailure.capture("资料任务队列正在加载，请稍后删除系列。") }
        if let reason = workspaceDeletionBlockReason(workspaceID) { throw AppFailure.capture(reason) }
        let targetJobs = jobs.filter { $0.workspaceID == workspaceID }
        guard !targetJobs.isEmpty else { return }
        let targetIDs = Set(targetJobs.map(\.id))
        let retainedHashes = Set(jobs.filter { !targetIDs.contains($0.id) }.compactMap(\.sourceHash))
            .union(remainingWorkspaces.flatMap(\.documents).compactMap { $0.content?.sourceHash })
        let originalsRoot = Self.originals.standardizedFileURL.path + "/"
        var artifacts = targetJobs.map { Self.root.appendingPathComponent($0.id.uuidString, isDirectory: true) }
        for job in targetJobs where !retainedHashes.contains(job.sourceHash ?? "") {
            guard let path = job.originalPath else { continue }
            let file = URL(fileURLWithPath: path).standardizedFileURL
            if file.path.hasPrefix(originalsRoot) { artifacts.append(file) }
        }
        var removed = Set<String>()
        for artifact in artifacts where removed.insert(artifact.standardizedFileURL.path).inserted {
            if FileManager.default.fileExists(atPath: artifact.path) { try FileManager.default.removeItem(at: artifact) }
        }
        try await store.delete(ids: targetIDs)
        jobs.removeAll { targetIDs.contains($0.id) }
    }
    private func mutate(_ id: UUID, _ change: (inout DocumentJob) -> Void) {
        guard let i = jobs.firstIndex(where: { $0.id == id }) else { return }
        change(&jobs[i]); jobs[i].updatedAt = Date()
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
        for job in jobs where job.stage == .waiting && Date().timeIntervalSince(job.updatedAt) > 5 { mutate(job.id) { $0.stage = .queued } }
        let activeJobs = jobs.filter { tasks[$0.id] != nil }
        var light = activeJobs.filter { Self.isLight($0) }.count, heavy = activeJobs.count - light
        for job in jobs where [.queued, .embedding].contains(job.stage) && tasks[job.id] == nil {
            let simple = Self.isLight(job)
            guard simple ? light < 2 : heavy < 1 else { continue }
            if simple { light += 1 } else { heavy += 1 }
            process(job)
        }
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
                if (["docx", "pptx", "xlsx"].contains(ext) || job.provider != "system"), !DocumentRuntime.installed {
                    mutate(job.id) { $0.stage = .waiting; $0.message = "请在设置 → 资料准备共用文档组件。" }; return
                }
                if job.provider == "paddle", let model = ModelCatalog.descriptor(for: job.localModelID ?? ModelCatalog.paddleOCR.id),
                   (!ManagedModelStore.isInstalled(model) || !ManagedModelStore.isInstalled(ModelCatalog.layout)) {
                    mutate(job.id) { $0.stage = .waiting; $0.message = "所选 OCR 模型尚未就绪，请到设置准备组件。" }; return
                }
                if job.provider == "codex", !AppState.shared.codexReady {
                    mutate(job.id) { $0.stage = .waiting; $0.message = "等待连接 Codex。" }; return
                }
                if ext == "ppt", !LegacyOfficeConverter.available {
                    mutate(job.id) { $0.stage = .waiting; $0.message = "旧版 PPT 需要 LibreOffice。安装后会自动继续。" }; return
                }
                var documentID = job.documentID
                if let id = documentID, !AppState.shared.workspaces.workspace(job.workspaceID)!.documents.contains(where: { $0.id == id }) { documentID = nil }
                if documentID == nil {
                    var options = DocumentConversionOptions(provider: DocumentOCRProvider(rawValue: job.provider) ?? .system,
                        fallback: job.fallback, model: job.model, effort: job.effort, timeout: job.timeout)
                    options.localModelID = job.localModelID ?? ModelCatalog.paddleOCR.id
                    options.sourceHash = job.sourceHash
                    options.checkpointDirectory = Self.root.appendingPathComponent(job.id.uuidString, isDirectory: true)
                    options.onProgress = { [weak self] event in Task { @MainActor in self?.progress(job.id, event) } }
                    let worker = Task.detached(priority: .utility) { try await MarkdownConverter.convert(URL(fileURLWithPath: path), options: options) }
                    let result = try await withTaskCancellationHandler { try await worker.value } onCancel: { worker.cancel() }
                    try Task.checkCancellation()
                    guard !AppState.shared.isRunning, var workspace = AppState.shared.workspaces.workspace(job.workspaceID) else { throw CancellationError() }
                    progress(job.id, .init(.validating, message: "OCR / 解析已完成，正在检查内容。"))
                    guard result.sections.contains(where: { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }) else { throw AppFailure.capture("没有提取到内容，请查看文件是否为空或不可读。") }
                    do {
                        let existing = workspace.documents.first(where: { $0.content?.sourceHash == job.sourceHash && $0.sourceFormat == ext })
                        let id = existing?.id ?? UUID(), workspaceID = job.workspaceID
                        let category = job.category ?? DocumentClassifier.classify(name: job.fileName, content: String(result.markdown.prefix(20000)), categories: job.categories)
                        var document = try await Task.detached(priority: .utility) { () -> ContextDocument in
                            let archived = try MaterialArchive.archive(result.markdown, name: (job.fileName as NSString).deletingPathExtension, format: ext, workspaceID: workspaceID)
                            if let content = result.content { try JSONEncoder().encode(content).write(to: archived.deletingPathExtension().appendingPathExtension("json"), options: .atomic) }
                            return ContextDocument(id: id, name: job.fileName, pages: result.sections, category: category, markdownPath: MaterialArchive.relative(archived), sourceFormat: ext, content: result.content)
                        }.value
                        try Task.checkCancellation()
                        guard !AppState.shared.isRunning else { throw CancellationError() }
                        // Refresh after awaiting disk I/O, preserving concurrent file commits.
                        guard let latest = AppState.shared.workspaces.workspace(job.workspaceID) else { throw CancellationError() }
                        workspace = latest
                        if let position = workspace.documents.firstIndex(where: { $0.content?.sourceHash == job.sourceHash && $0.sourceFormat == ext }) {
                            document.id = workspace.documents[position].id
                            workspace.documents[position] = document
                        } else { workspace.documents.append(document) }
                        AppState.shared.workspaces.update(workspace)
                        documentID = document.id
                    }
                    mutate(job.id) { $0.documentID = documentID; $0.stage = .lexical; $0.message = "OCR 已完成，正在建立文字索引。" }
                    if AppState.shared.activeWorkspace.id == job.workspaceID { AppState.shared.rebuildContextIndex() }
                }
                try Task.checkCancellation()
                if AppState.shared.semanticRetrievalEnabled {
                    guard AppState.shared.embeddingModelInstalled, AppState.shared.runtimeInstalled else {
                        mutate(job.id) { $0.stage = .waiting; $0.message = "文字检索已可用；等待语义查找组件。" }; return
                    }
                    try await AppState.shared.prepareSemanticWorkspace(job.workspaceID) { [weak self] completed, total in
                        Task { @MainActor in self?.progress(job.id, .init(.embedding, completed: completed, total: total, message: "文字检索已可用，正在生成向量。")) }
                    }
                }
                let warnings = AppState.shared.workspaces.workspace(job.workspaceID)?.documents.first(where: { $0.id == documentID })?.content?.warnings ?? []
                mutate(job.id) { $0.stage = warnings.isEmpty ? .ready : .warning; $0.completed = $0.total ?? $0.completed; $0.message = warnings.isEmpty ? (AppState.shared.semanticRetrievalEnabled ? "Embedding 已完成，资料已就绪。" : "文字检索已就绪，语义查找未启用。") : warnings.joined(separator: "\n") }
            } catch is CancellationError {
                if jobs.first(where: { $0.id == job.id })?.stage != .cancelled { mutate(job.id) { $0.stage = .paused; $0.message = "已保存检查点，稍后继续。" } }
            } catch { mutate(job.id) { $0.stage = .failed; $0.message = error.localizedDescription } }
        }
    }
}
