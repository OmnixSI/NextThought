import SwiftUI
import InterviewHackerCore

struct DocumentModelSettings: View {
    @Environment(AppState.self) private var state
    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
            SettingsSection(title: "文档处理", icon: "doc.text.viewfinder") {
                Picker("识别接口", selection: $state.documentOCRProvider) {
                    ForEach(DocumentOCRProvider.allCases) { Text($0.title).tag($0) }
                }.onChange(of: state.documentOCRProvider) { _, _ in state.documentProviderWasSelected = true; state.updateSettings() }
                Toggle("本地识别出错时由系统 OCR 补读并标记（关闭为严格模式）", isOn: $state.ocrFallbackToVision)
                    .onChange(of: state.ocrFallbackToVision) { _, _ in state.updateSettings() }
                if state.documentOCRProvider == .codex {
                    TextField("Codex 模型", text: $state.codexOCRModel).onSubmit { state.updateSettings() }
                    Picker("推理强度", selection: $state.codexOCREffort) { ForEach(["low", "medium", "high", "xhigh"], id: \.self) { Text($0).tag($0) } }.onChange(of: state.codexOCREffort) { _, _ in state.updateSettings() }
                    HStack { Text("每文件超时（秒）"); TextField("600", value: $state.codexOCRTimeout, format: .number).onSubmit { state.codexOCRTimeout=max(30,min(3600,state.codexOCRTimeout)); state.updateSettings() } }
                    Note("默认 gpt-6-luna / medium / 标准速度，每文件 10 分钟。使用现有 Codex 登录，原文件临时副本经专属工具读取，任务结束删除。")
                }
                HStack {
                    Text(state.runtimeInstalled ? "文档运行包已安装" : "Office、Paddle 和向量检索需要文档运行包")
                    Spacer()
                    Button(state.isInstallingDocumentRuntime ? "正在下载…" : "安装／修复运行包") { state.installDocumentRuntime() }
                        .disabled(state.isInstallingDocumentRuntime)
                }
                Note(DocumentRuntimeInstaller.sizeDescription)
                if state.isInstallingDocumentRuntime { Button("取消下载运行包") { state.runtimeDownloadTask?.cancel() } }
                if state.runtimeInstalled { Button("删除运行包", role: .destructive) { state.deleteDocumentRuntime() } }
                if !state.documentRuntimeStatus.isEmpty { Note(state.documentRuntimeStatus) }
                ForEach([ModelCatalog.paddleOCR, ModelCatalog.layout], id: \.id) { descriptor in
                    ManagedModelRow(descriptor: descriptor, isInstalled: state.installedDocumentModels.contains(descriptor.id))
                }
            }
            SettingsSection(title: "语义检索", icon: "sparkle.magnifyingglass") {
                ManagedModelRow(descriptor: ModelCatalog.qwen3Embedding, isInstalled: state.embeddingModelInstalled)
                Toggle("关键词 + 语义检索", isOn: $state.semanticRetrievalEnabled)
                    .onChange(of: state.semanticRetrievalEnabled) { _, _ in state.updateSettings(); state.rebuildContextIndex() }
                Button("重建向量索引") { state.rebuildSemanticIndex() }.disabled(!state.embeddingModelInstalled || state.isBuildingSemanticIndex)
                if !state.semanticIndexStatus.isEmpty { Note(state.semanticIndexStatus) }
            }
            Note("资料只长期保留 Markdown 与定位信息。模型和运行包单独下载，可随时删除。")
        }
    }
}
struct ManagedModelRow: View {
    @Environment(AppState.self) private var state
    let descriptor: ManagedModelDescriptor
    let isInstalled: Bool
    var unavailableReason: String? = nil
    private var downloading: Bool { state.isDownloadingManagedModel && state.managedModelDownloadID == descriptor.id }
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Label(descriptor.name, systemImage: isInstalled ? "checkmark.circle.fill" : "arrow.down.circle")
                Spacer()
                if downloading { Button("取消") { state.cancelManagedModelDownload() } }
                else {
                    Button(isInstalled ? "校验／修复" : "下载") { state.downloadManagedModel(descriptor) }.disabled(state.isDownloadingManagedModel)
                    if isInstalled { Button("删除", role: .destructive) { state.deleteManagedModel(descriptor) } }
                }
            }
            Note("\(descriptor.formattedSize) · \(descriptor.license) · \(descriptor.summary)")
            Link("固定版本来源", destination: URL(string: descriptor.homepage)!)
            if downloading { ProgressView(value: state.managedModelDownloadProgress) }
            if !state.managedModelDownloadStatus.isEmpty { Note(state.managedModelDownloadStatus) }
        }
    }
}
