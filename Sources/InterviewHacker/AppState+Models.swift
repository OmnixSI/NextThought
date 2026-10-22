import Foundation
import InterviewHackerCore

extension AppState {
    var paddleOCRInstalled: Bool { installedDocumentModels.contains(ModelCatalog.paddleOCR.id) && installedDocumentModels.contains(ModelCatalog.layout.id) }
    var embeddingModelInstalled: Bool { installedDocumentModels.contains(ModelCatalog.qwen3Embedding.id) }
    var semanticRetrievalReady: Bool { semanticRetrievalEnabled && embeddingModelInstalled && runtimeInstalled && !semanticIndex.isEmpty }
    var documentConversionOptions: DocumentConversionOptions {
        .init(provider: documentOCRProvider, fallback: ocrFallbackToVision, model: codexOCRModel, effort: codexOCREffort, timeout: codexOCRTimeout, isPaused: { await MainActor.run { self.isRunning || self.isAnswering } })
    }
    func downloadManagedModel(_ descriptor: ManagedModelDescriptor) {
        guard !documentSetupBusy else { return }
        isDownloadingManagedModel = true; managedModelDownloadID = descriptor.id
        managedModelDownloadProgress = 0
        managedModelDownloadStatus = "先查找并校验本机文件，缺少部分才下载…"
        managedDownloadTask = Task {
            defer { isDownloadingManagedModel = false; managedModelDownloadID = ""; managedDownloadTask = nil }
            do {
                try await ManagedModelStore.download(descriptor) { [weak self] fraction, message in
                    Task { @MainActor in self?.managedModelDownloadProgress = fraction; self?.managedModelDownloadStatus = message }
                }
                try Task.checkCancellation()
                documentModelInstallRevision += 1
                installedDocumentModels.insert(descriptor.id)
                managedModelDownloadStatus = "\(descriptor.name) 已通过完整校验。"
                if descriptor.kind == .ocr || descriptor.kind == .layout {
                    if paddleOCRInstalled && !documentProviderWasSelected { documentOCRProvider = .paddle; updateSettings() }
                } else { rebuildSemanticIndex() }
            } catch is CancellationError {
                documentModelInstallRevision += 1; await refreshManagedModelStatus()
                managedModelDownloadStatus = "准备已取消，已校验文件将在重试时复用。"
            } catch {
                documentModelInstallRevision += 1; await refreshManagedModelStatus()
                managedModelDownloadStatus = "准备失败：\(error.localizedDescription)；重试可修复。"
            }
        }
    }
    func cancelManagedModelDownload() { managedDownloadTask?.cancel() }
    func deleteManagedModel(_ descriptor: ManagedModelDescriptor) {
        documentModelInstallRevision += 1
        Task {
            localAssetScanTask?.cancel(); await localAssetScanTask?.value
            documentSetupTask?.cancel(); await documentSetupTask?.value
            if managedModelDownloadID == descriptor.id { managedDownloadTask?.cancel(); await managedDownloadTask?.value }
            installedDocumentModels.remove(descriptor.id)
            semanticGeneration = UUID(); semanticIndexTask?.cancel(); await semanticIndexTask?.value
            importTask?.cancel(); await importTask?.value
            if descriptor.kind == .embedding {
                await SemanticRetrievalService.shared.unload()
                semanticIndex = SemanticIndex(); isBuildingSemanticIndex = false
            } else { await DocumentWorker.shared.unload() }
            do {
                try ManagedModelStore.remove(descriptor)
                if descriptor.kind == .embedding { try SemanticIndexStore.remove(modelID: descriptor.id) }
                rebuildContextIndex()
                managedModelDownloadStatus = "已卸载并删除 \(descriptor.name)。"
            } catch { managedModelDownloadStatus = "删除失败：\(error.localizedDescription)" }
        }
    }
    func refreshManagedModelStatus() async {
        let revision = documentModelInstallRevision
        let installed = await Task.detached(priority: .utility) {
            Set(ModelCatalog.all.filter { descriptor in
                guard ManagedModelStore.isInstalled(descriptor) else { return false }
                return (try? ManagedModelStore.verify(descriptor, at: descriptor.directory)) != nil
            }.map(\.id))
        }.value
        guard revision == documentModelInstallRevision else { return }
        installedDocumentModels = installed; runtimeInstalled = DocumentRuntime.installed
        documentModelsChecked = true
        semanticIndex = SemanticIndexStore.load(modelID: ModelCatalog.qwen3Embedding.id, workspaceID: activeWorkspace.id)
        rebuildContextIndex()
    }
    func rebuildSemanticIndex() {
        semanticIndexTask?.cancel(); semanticGeneration = UUID()
        let generation = semanticGeneration, workspaceID = activeWorkspace.id, revision = documentRevision
        guard embeddingModelInstalled && semanticRetrievalEnabled && runtimeInstalled else { isBuildingSemanticIndex = false; return }
        let documents = activeWorkspace.retrievalDocuments
        let cached = SemanticIndexStore.load(modelID: ModelCatalog.qwen3Embedding.id, workspaceID: workspaceID)
        isBuildingSemanticIndex = true
        semanticIndexTask = Task {
            defer { if semanticGeneration == generation { isBuildingSemanticIndex = false } }
            do {
                let chunks = await Task.detached(priority: .utility) { ContextChunker.chunks(documents: documents) }.value
                let oldVectors = Dictionary(cached.entries.map { ($0.fingerprint, $0.vector) }, uniquingKeysWith: { a, _ in a })
                var index = SemanticIndex()
                let embedder = await SemanticRetrievalService.shared.embedder()
                for (position, chunk) in chunks.enumerated() {
                    try Task.checkCancellation()
                    while isAnswering || isRunning { try await Task.sleep(for: .milliseconds(200)); try Task.checkCancellation() }
                    let vector: [Float]
                    if let old = oldVectors[chunk.fingerprint], old.count == 1024 { vector = old }
                    else { vector = try await embedder.embedPassage(chunk.text) }
                    index.setVector(vector, fingerprint: chunk.fingerprint, for: position)
                    if position % 10 == 0 { semanticIndexStatus = "正在生成向量 \(position + 1)/\(chunks.count)…" }
                }
                try Task.checkCancellation()
                guard semanticGeneration == generation, activeWorkspace.id == workspaceID, documentRevision == revision, embeddingModelInstalled else { return }
                try SemanticIndexStore.save(index, modelID: ModelCatalog.qwen3Embedding.id, workspaceID: workspaceID)
                semanticIndex = index; contextIndex = HybridContextIndex(documents: documents, semantic: index)
                semanticIndexStatus = "已缓存 \(index.embeddedCount) 个片段向量。"
            } catch is CancellationError { }
            catch { if semanticGeneration == generation { semanticIndexStatus = "向量索引失败：\(error.localizedDescription)" } }
        }
    }
    func retrieveExcerpts(question: String, snapshot: HybridContextIndex? = nil, maximumCharacters: Int = 8_000) async -> [ContextExcerpt] {
        let index = snapshot ?? contextIndex
        guard semanticRetrievalReady, index.hasVectors, !Task.isCancelled else { return index.retrieve(question: question, maximumCharacters: maximumCharacters) }
        let vector = await SemanticRetrievalService.shared.queryVector(question)
        guard !Task.isCancelled else { return [] }
        return index.retrieve(question: question, queryVector: vector, maximumCharacters: maximumCharacters)
    }
}
