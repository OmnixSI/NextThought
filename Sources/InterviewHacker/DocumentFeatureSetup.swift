import Foundation
import InterviewHackerCore

extension AppState {
    var documentAvailability: DocumentFeatureAvailability {
        .init(checking: !documentModelsChecked, runtime: runtimeInstalled,
              ocr: installedDocumentModels.contains(ModelCatalog.paddleOCR.id),
              layout: installedDocumentModels.contains(ModelCatalog.layout.id),
              embedding: embeddingModelInstalled, codexConnected: codexReady)
    }
    var documentSetupBusy: Bool { documentSetupFeature != nil || isDownloadingManagedModel || isInstallingDocumentRuntime }

    func documentModels(for feature: DocumentFeatureAvailability.Feature) -> [ManagedModelDescriptor] {
        switch feature {
        case .paddle: [ModelCatalog.paddleOCR, ModelCatalog.layout]
        case .semantic: [ModelCatalog.qwen3Embedding]
        case .codex, .office: []
        }
    }
    func documentDownloadSize(_ feature: DocumentFeatureAvailability.Feature) -> String {
        let runtimeBytes = (try? JSONDecoder().decode(DocumentRuntimeInstaller.Manifest.self,
            from: Data(contentsOf: DocumentRuntimeInstaller.manifestURL)))?.downloadBytes ?? 185_562_618
        let bytes = (runtimeInstalled ? 0 : runtimeBytes) + documentModels(for: feature)
            .filter { !installedDocumentModels.contains($0.id) }.reduce(Int64(0)) { $0 + $1.totalSize }
        return ByteCountFormatter.string(fromByteCount: bytes, countStyle: .file)
    }
    func selectDocumentProvider(_ provider: DocumentOCRProvider) {
        // A user may always return to the built-in route. Missing providers are prepared first.
        guard provider == .system || documentAvailability.ready(provider == .paddle ? .paddle : .codex) else { return }
        documentProviderSelectionRevision += 1
        documentOCRProvider = provider; documentProviderWasSelected = true; updateSettings()
    }
    func selectSemanticRetrieval(_ enabled: Bool) {
        semanticSelectionRevision += 1
        semanticRetrievalEnabled = enabled; updateSettings(); rebuildContextIndex()
    }
    func prepareDocumentFeature(_ feature: DocumentFeatureAvailability.Feature) {
        guard documentModelsChecked, !documentSetupBusy else { return }
        guard feature != .codex || codexReady else {
            documentSetupStatus = "请先连接并登录 Codex，再启用文件代读。"; return
        }
        documentSetupFeature = feature; documentSetupProgress = 0
        documentSetupStatus = "正在准备，已安装的组件会复用…"
        let providerRevision = documentProviderSelectionRevision, semanticRevision = semanticSelectionRevision
        let generation = UUID(); documentSetupGeneration = generation
        let models = documentModels(for: feature).filter { !installedDocumentModels.contains($0.id) }
        let needsRuntime = !runtimeInstalled
        let count = max(1, models.count + (needsRuntime ? 1 : 0))
        documentSetupTask = Task {
            defer {
                if documentSetupGeneration == generation { documentSetupFeature = nil; documentSetupTask = nil }
            }
            @MainActor func progress(_ fraction: Double, _ step: Int, _ title: String) {
                guard documentSetupGeneration == generation, documentSetupFeature == feature else { return }
                documentSetupProgress = (Double(step) + fraction) / Double(count)
                documentSetupStatus = "\(title) · \(Int(fraction * 100))%"
            }
            do {
                var step = 0
                if needsRuntime {
                    try await DocumentRuntimeInstaller.install { fraction in
                        Task { @MainActor in progress(fraction, 0, "下载共用文档组件") }
                    }
                    try Task.checkCancellation()
                    runtimeInstalled = DocumentRuntime.installed; step += 1
                }
                for descriptor in models {
                    try Task.checkCancellation()
                    let position = step
                    try await ManagedModelStore.download(descriptor) { fraction, _ in
                        Task { @MainActor in progress(fraction, position, "下载 \(descriptor.name)") }
                    }
                    try Task.checkCancellation()
                    modelInstallRevision += 1; installedDocumentModels.insert(descriptor.id); step += 1
                }
                try Task.checkCancellation()
                guard documentAvailability.ready(feature) else { throw AppFailure.capture("组件尚未全部就绪，请重试。") }
                switch feature {
                case .paddle: if documentProviderSelectionRevision == providerRevision { selectDocumentProvider(.paddle) }
                case .codex: if documentProviderSelectionRevision == providerRevision { selectDocumentProvider(.codex) }
                case .semantic: if semanticSelectionRevision == semanticRevision { selectSemanticRetrieval(true) }
                case .office: break
                }
                documentSetupProgress = 1; documentSetupStatus = "已准备完成，可以使用。"
            } catch is CancellationError {
                runtimeInstalled = DocumentRuntime.installed
                documentSetupStatus = "已取消。已下载并校验的组件会保留，重试无需重新下载。"
            } catch {
                runtimeInstalled = DocumentRuntime.installed
                documentSetupStatus = "准备失败：\(error.localizedDescription) 可重试；当前识别方式没有改变。"
            }
        }
    }
}
