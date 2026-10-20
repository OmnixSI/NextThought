import SwiftUI
import InterviewHackerCore

/// The two models the app manages itself: the document model that reads whole
/// pages and the embedding model that lets the material search understand
/// meaning, not only words.
///
/// Both are downloaded on request into the data directory, applied as soon as
/// they are there and removed with one click; the weights never ship with the
/// app and nothing is uploaded.
struct DocumentModelSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
            SettingsSection(title: "文档识别模型", icon: "doc.text.viewfinder") {
                ManagedModelRow(
                    descriptor: ModelCatalog.unlimitedOCR, isInstalled: state.unlimitedOCRInstalled,
                    unavailableReason: UnlimitedOCRService.unavailableReason)
                Toggle("用 Unlimited-OCR 读扫描件和图片", isOn: $state.unlimitedOCREnabled)
                    .disabled(!state.unlimitedOCRInstalled)
                    .onChange(of: state.unlimitedOCREnabled) { _, _ in state.updateSettings() }
                MenuPicker(
                    title: "每个文档最多读", selection: $state.unlimitedOCRMaxPages,
                    options: [5, 10, 20, 50, 100].map { ($0, "\($0) 页") })
                    .disabled(!state.unlimitedOCREnabled)
                    .onChange(of: state.unlimitedOCRMaxPages) { _, _ in state.updateSettings() }
                Toggle("模型出错时回退到系统 OCR", isOn: $state.ocrFallbackToVision)
                    .disabled(!state.unlimitedOCREnabled)
                    .onChange(of: state.ocrFallbackToVision) { _, _ in state.updateSettings() }
                Note(ModelCatalog.unlimitedOCR.note)
            }

            SettingsSection(title: "向量检索模型", icon: "sparkle.magnifyingglass") {
                ManagedModelRow(
                    descriptor: ModelCatalog.qwen3Embedding, isInstalled: state.embeddingModelInstalled,
                    unavailableReason: state.embeddingModelInstalled ? nil : "尚未下载模型。")
                Toggle("资料检索同时看语义（关键词 + 向量）", isOn: $state.semanticRetrievalEnabled)
                    .disabled(!state.embeddingModelInstalled)
                    .onChange(of: state.semanticRetrievalEnabled) { _, _ in
                        state.updateSettings()
                        if state.semanticRetrievalEnabled { state.rebuildSemanticIndex() }
                    }
                HStack {
                    Button("重建向量索引") { state.rebuildSemanticIndex() }
                        .buttonStyle(SecondaryButtonStyle())
                        .disabled(!state.embeddingModelInstalled || state.isBuildingSemanticIndex)
                    if state.isBuildingSemanticIndex {
                        ProgressView().controlSize(.small)
                    }
                }
                if !state.semanticIndexStatus.isEmpty { Note(state.semanticIndexStatus) }
                Note(ModelCatalog.qwen3Embedding.note)
            }

            Note("模型文件在 \(AppPaths.models.path)，可以随时整个文件夹删掉；向量索引在 \(AppPaths.index.path)。")
        }
    }
}

/// One downloadable model: what it is for, whether it is installed, and the
/// buttons to download or remove it.
struct ManagedModelRow: View {
    @Environment(AppState.self) private var state
    let descriptor: ManagedModelDescriptor
    let isInstalled: Bool
    /// Why the model cannot be used even though it is there.
    var unavailableReason: String?
    @State private var confirmDelete = false

    private var isDownloading: Bool {
        state.isDownloadingManagedModel && state.managedModelDownloadID == descriptor.id
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Label(
                    descriptor.name,
                    systemImage: isInstalled ? "checkmark.circle.fill" : "arrow.down.circle")
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(isInstalled ? Color.green : Color.orange)
                Spacer()
                if isDownloading {
                    Button("取消") { state.cancelManagedModelDownload() }.buttonStyle(SecondaryButtonStyle())
                } else if isInstalled {
                    Button("删除") { confirmDelete = true }
                        .buttonStyle(.borderless).foregroundStyle(.red)
                        .disabled(state.isDownloadingManagedModel)
                } else {
                    Button("下载") { state.downloadManagedModel(descriptor) }
                        .buttonStyle(PrimaryButtonStyle())
                        .disabled(state.isDownloadingManagedModel)
                }
            }
            Note(descriptor.summary)
            if isDownloading {
                ProgressView(value: state.managedModelDownloadProgress).controlSize(.small)
            }
            if isDownloading || !state.managedModelDownloadStatus.isEmpty {
                Note(state.managedModelDownloadStatus)
            }
            if let unavailableReason {
                Note(unavailableReason, color: .orange)
            } else {
                Note(
                    "\(isInstalled ? "已安装" : "需要下载") · \(descriptor.formattedSize) · \(descriptor.license) · 需要 \(descriptor.minimumOS)"
                )
            }
            HStack {
                Link("模型来源", destination: URL(string: descriptor.homepage)!)
                    .font(.system(size: 11)).foregroundStyle(Color.accentColor)
                Spacer()
                Text(descriptor.repository).font(.system(size: 10)).foregroundStyle(.secondary)
            }
        }
        .confirmationDialog(
            "删除 \(descriptor.name)？", isPresented: $confirmDelete, titleVisibility: .visible
        ) {
            Button("删除", role: .destructive) { state.deleteManagedModel(descriptor) }
            Button("取消", role: .cancel) { }
        } message: {
            Text("模型文件在 \(AppPaths.models.path)/\(descriptor.id)，删掉后可以随时重新下载。")
        }
    }
}
