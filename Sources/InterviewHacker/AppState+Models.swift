import Foundation
import InterviewHackerCore

/// The models the app manages itself: the document model (Unlimited-OCR) and
/// the embedding model (Qwen3-Embedding).
///
/// Both are downloaded on request into the data directory, checked against the
/// hash of the pinned revision, applied automatically and can be removed again
/// by deleting the model folder. Nothing is uploaded and nothing is called
/// home: the models run on this machine.
extension AppState {
    // MARK: - State of the two models

    /// Whether the Unlimited-OCR model is present and complete.
    var unlimitedOCRInstalled: Bool {
        _ = modelInstallRevision
        return ManagedModelStore.isInstalled(ModelCatalog.unlimitedOCR)
    }

    /// Whether the embedding model is present and complete.
    var embeddingModelInstalled: Bool {
        _ = modelInstallRevision
        return ManagedModelStore.isInstalled(ModelCatalog.qwen3Embedding)
    }

    /// Whether the embedding model can contribute to the material search.
    var semanticRetrievalReady: Bool {
        semanticRetrievalEnabled && embeddingModelInstalled && !semanticIndex.isEmpty
    }

    // MARK: - Downloading and removing

    /// Downloads one of the models and applies it as soon as it is there.
    func downloadManagedModel(_ descriptor: ManagedModelDescriptor) {
        guard !isDownloadingManagedModel else { return }
        isDownloadingManagedModel = true
        managedModelDownloadID = descriptor.id
        managedModelDownloadProgress = 0
        managedModelDownloadStatus = "正在下载 \(descriptor.name)（\(descriptor.formattedSize)）…"
        Task {
            do {
                try await ManagedModelStore.download(descriptor) { [weak self] progress, message in
                    Task { @MainActor in
                        self?.managedModelDownloadProgress = progress
                        self?.managedModelDownloadStatus = "\(message) · \(Int(progress * 100))%"
                    }
                }
                modelInstallRevision += 1
                managedModelDownloadStatus = "\(descriptor.name) 已下载并通过 SHA-256 校验，已自动启用。"
                applyInstalledModel(descriptor)
            } catch is CancellationError {
                managedModelDownloadStatus = "下载已取消。"
            } catch {
                managedModelDownloadStatus = "下载失败：\(error.localizedDescription)"
            }
            isDownloadingManagedModel = false
            managedModelDownloadID = ""
        }
    }

    func cancelManagedModelDownload() {
        managedModelDownloadStatus = "下载已取消。"
    }

    /// Removes a model and everything that belongs to it.
    func deleteManagedModel(_ descriptor: ManagedModelDescriptor) {
        guard !isDownloadingManagedModel else { return }
        Task {
            do {
                try ManagedModelStore.remove(descriptor)
                modelInstallRevision += 1
                managedModelDownloadStatus = "已删除 \(descriptor.name)。"
                if descriptor.kind == .embedding {
                    SemanticIndexStore.remove(modelID: descriptor.id)
                    semanticIndex = SemanticIndex()
                    semanticIndexStatus = ""
                    contextIndex = HybridContextIndex(documents: activeWorkspace.retrievalDocuments)
                }
            } catch {
                managedModelDownloadStatus = "删除失败：\(error.localizedDescription)"
            }
        }
    }

    /// What happens right after a model was downloaded.
    private func applyInstalledModel(_ descriptor: ManagedModelDescriptor) {
        switch descriptor.kind {
        case .ocr:
            // The document model is applied on the next import; nothing to do.
            break
        case .embedding:
            semanticIndex = SemanticIndexStore.load(modelID: descriptor.id)
            if semanticIndex.isEmpty {
                rebuildSemanticIndex()
            } else {
                contextIndex = HybridContextIndex(documents: activeWorkspace.retrievalDocuments, semantic: semanticIndex)
                semanticIndexStatus = "已载入 \(semanticIndex.embeddedCount) 条向量。"
            }
            Task { await SemanticRetrievalService.shared.prepare() }
        }
    }

    /// Re-reads the state of the two models from disk.
    func refreshManagedModelStatus() async {
        modelInstallRevision += 1
        guard embeddingModelInstalled else {
            semanticIndex = SemanticIndex()
            return
        }
        if semanticIndex.isEmpty {
            semanticIndex = SemanticIndexStore.load(modelID: ModelCatalog.qwen3Embedding.id)
        }
        if semanticRetrievalEnabled, !semanticIndex.isEmpty {
            contextIndex = HybridContextIndex(documents: activeWorkspace.retrievalDocuments, semantic: semanticIndex)
        }
    }

    // MARK: - Vector index

    /// Embeds the materials again, so the search knows what they are about.
    ///
    /// The embedding of every chunk takes a moment, so this runs in the
    /// background: the keyword search keeps working while it runs.
    func rebuildSemanticIndex() {
        guard embeddingModelInstalled else {
            semanticIndexStatus = "向量模型尚未下载。"
            return
        }
        guard !isBuildingSemanticIndex else { return }
        isBuildingSemanticIndex = true
        let documents = activeWorkspace.retrievalDocuments
        Task {
            let chunks = ContextChunker.chunks(documents: documents)
            semanticIndexStatus = "正在为 \(chunks.count) 个片段生成向量…"
            do {
                let embedder = try await SemanticRetrievalService.shared.embedder()
                var index = semanticIndex
                var pending = 0
                for (position, chunk) in chunks.enumerated() {
                    try Task.checkCancellation()
                    // A chunk whose text did not change keeps its vector.
                    if index.vector(for: chunk.order, fingerprint: chunk.fingerprint) != nil { continue }
                    pending += 1
                    let vector = try await embedder.embedPassage(chunk.text)
                    index.setVector(vector, fingerprint: chunk.fingerprint, for: chunk.order)
                    if position % 20 == 0 || position == chunks.count - 1 {
                        semanticIndexStatus = "正在生成向量 \(position + 1)/\(chunks.count)…"
                    }
                }
                semanticIndex = index
                try? SemanticIndexStore.save(index, modelID: ModelCatalog.qwen3Embedding.id)
                contextIndex = HybridContextIndex(documents: documents, semantic: index)
                semanticIndexStatus =
                    pending == 0
                    ? "向量索引已是最新（\(index.embeddedCount) 个片段）。"
                    : "已为 \(index.embeddedCount) 个片段生成向量。"
            } catch is CancellationError {
                semanticIndexStatus = "向量索引已取消。"
            } catch {
                semanticIndexStatus = "向量索引失败：\(error.localizedDescription)"
            }
            isBuildingSemanticIndex = false
        }
    }

    /// Retrieves the passages that belong to a question.
    ///
    /// With the embedding model the search also looks at the meaning of the
    /// question, not only at the words in it; the two rankings are merged.
    func retrieveExcerpts(question: String, maximumCharacters: Int = 8_000) async -> [ContextExcerpt] {
        guard semanticRetrievalReady else {
            return contextIndex.retrieve(question: question, maximumCharacters: maximumCharacters)
        }
        let vector = await SemanticRetrievalService.shared.queryVector(question)
        guard let vector, !vector.isEmpty else {
            return contextIndex.retrieve(question: question, maximumCharacters: maximumCharacters)
        }
        return contextIndex.retrieve(
            question: question, queryVector: vector, maximumCharacters: maximumCharacters)
    }
}
