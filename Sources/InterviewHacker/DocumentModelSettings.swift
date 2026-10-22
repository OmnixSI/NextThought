import SwiftUI
import InterviewHackerCore

struct DocumentModelSettings: View {
    @Environment(AppState.self) private var state
    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 14) {
            SettingsSection(title: "现在用什么读取资料", icon: "doc.text.viewfinder") {
                Label(currentSummary, systemImage: currentReady ? "checkmark.circle.fill" : "exclamationmark.circle")
                    .foregroundStyle(currentReady ? .green : .orange)
                Note("选一种即可。PDF、图片和纯文本可先用系统识别，无需下载文档模型。")
                provider(.system, title: "系统识别", badge: "Mac 自带 · 可直接用", ready: true,
                         detail: "离线、无需下载。适合普通文字；复杂表格和公式可能丢失结构，重要数字请核对。")
                provider(.paddle, title: "本机增强识别", badge: availability(.paddle), ready: state.documentAvailability.ready(.paddle),
                         detail: "适合扫描件、表格和公式。需下载识别与版面模型，在本机计算、占用内存；仍可能识别出错。")
                if state.documentOCRProvider == .paddle {
                    Picker("本机识别模型", selection: $state.localOCRModelID) {
                        ForEach(ModelCatalog.localOCRChoices, id: \.id) { descriptor in
                            Text(descriptor.name + " · " + descriptor.formattedSize + (state.installedDocumentModels.contains(descriptor.id) ? " · 已安装" : " · 未安装")).tag(descriptor.id)
                        }
                    }.onChange(of: state.localOCRModelID) { _, _ in
                        state.documentProviderSelectionRevision += 1; state.updateSettings()
                    }
                }
                provider(.codex, title: "Codex 文件代读", badge: availability(.codex), ready: state.documentAvailability.ready(.codex),
                         detail: "免 OCR 模型权重，读取原文件。需要已登录的 Codex、共用组件和网络；占账号额度，速度受网络影响。")
                if !currentReady, state.documentOCRProvider != .system {
                    Button("先用系统识别") { state.selectDocumentProvider(.system) }.buttonStyle(SecondaryButtonStyle())
                }
            }
            SettingsSection(title: "已经在这台 Mac 下载过？", icon: "internaldrive") {
                Note("先扫描本机已有的兼容文档模型与支持组件，校验后复用，保留原文件。无需重复下载，也不会自动启用 OCR 或语义检索。")
                HStack {
                    Button("扫描本机已有组件") { state.scanLocalDocumentAssets() }
                        .buttonStyle(SecondaryButtonStyle()).disabled(state.documentSetupBusy || !state.documentModelsChecked)
                    Button("选择其他目录…") { state.chooseLocalDocumentAssets() }
                        .disabled(state.documentSetupBusy || !state.documentModelsChecked)
                    if state.isScanningLocalAssets { ProgressView().controlSize(.small); Button("取消") { state.localAssetScanTask?.cancel() } }
                }
                if !state.localAssetScanStatus.isEmpty { Note(state.localAssetScanStatus) }
                Note("扫描范围受系统权限和文件索引影响；外置磁盘或未找到的目录可手动选择。只有版本与文件校验匹配的模型才能复用。")
            }
            if state.documentSetupFeature != nil || !state.documentSetupStatus.isEmpty {
                SettingsSection(title: "准备进度", icon: "arrow.down.circle") {
                    Note(state.documentSetupStatus)
                    if state.documentSetupFeature != nil {
                        ProgressView(value: state.documentSetupProgress)
                        Button("取消准备") { state.documentSetupTask?.cancel() }
                    }
                }
            }
            SettingsSection(title: "怎样从资料中找答案", icon: "magnifyingglass") {
                choice(title: "关键词查找", status: !state.semanticRetrievalEnabled ? "正在使用 · 无需下载" : "始终可用，增强失败时也会使用",
                       detail: "按问题里的关键词挑选片段，准备快、占用少；问法和原文不同，或答案跨多个段落时，可能漏掉相关内容。") {
                    Button(state.semanticRetrievalEnabled ? "使用关键词查找" : "正在使用") {
                        state.selectSemanticRetrieval(false)
                    }.disabled(!state.semanticRetrievalEnabled)
                }
                choice(title: "语义增强查找（可选）", status: semanticSummary,
                       detail: "增加按含义查找，能帮助匹配同义问法。需下载模型、先建立索引，额外占用存储和内存；也可能漏检，仍保留关键词作为兜底。") {
                    if state.semanticRetrievalEnabled && state.documentAvailability.ready(.semantic) {
                        Text(state.isBuildingSemanticIndex ? "正在准备索引…" : "组件已启用").foregroundStyle(.green)
                    } else {
                        featureButton(.semantic, title: "启用语义增强")
                    }
                }
                Note("两种方式都会挑选相关片段放进回答上下文，不会默认发送全部资料。全量发送会增加等待和用量，还可能超过模型的上下文上限；关闭向量检索不等于全文已经被读取。")
                if !state.semanticIndexStatus.isEmpty { Note(state.semanticIndexStatus) }
            }
            SettingsSection(title: "Word、Excel 与 PowerPoint", icon: "doc.richtext") {
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: 5) {
                        Text(state.runtimeInstalled ? "已安装支持组件 · 可导入 Office 文件" : "需要共用文档组件")
                            .foregroundStyle(state.runtimeInstalled ? .green : .secondary)
                        Note("会前后台提取正文、嵌图、备注和图表。共用组件最多下载约 186 MB。")
                        if !LegacyOfficeConverter.available {
                            Link("安装旧版 PPT 支持（LibreOffice）", destination: URL(string: "https://www.libreoffice.org/download/download-libreoffice/")!)
                        } else { Note("旧版 PPT 本地转换组件已就绪。") }
                    }
                    Spacer(minLength: 12)
                    if !state.runtimeInstalled { featureButton(.office, title: "安装支持") }
                }
            }
            AdvancedOptions("document-management") {
                SettingsSection(title: "组件管理与高级设置", icon: "gearshape") {
                    Note("下面列出实际安装状态；“已安装”表示文件已校验，识别方式是否启用以上方选择为准。")
                    ForEach(ModelCatalog.all, id: \.id) { descriptor in
                        ManagedModelRow(descriptor: descriptor, isInstalled: state.installedDocumentModels.contains(descriptor.id))
                    }
                    if !state.managedModelDownloadStatus.isEmpty { Note(state.managedModelDownloadStatus) }
                    HStack {
                        Text(state.runtimeInstalled ? "共用文档组件 · 已安装" : "共用文档组件 · 未安装")
                        Spacer()
                        Button(state.runtimeInstalled ? "校验／修复" : "安装") { state.installDocumentRuntime() }
                            .disabled(state.documentSetupBusy || !state.documentModelsChecked)
                        if state.runtimeInstalled {
                            Button("删除", role: .destructive) { state.deleteDocumentRuntime() }.disabled(state.documentSetupBusy)
                        }
                    }
                    Note(DocumentRuntimeInstaller.sizeDescription)
                    if state.isInstallingDocumentRuntime {
                        Button("取消下载组件") { state.runtimeDownloadTask?.cancel() }
                    }
                    if !state.documentRuntimeStatus.isEmpty { Note(state.documentRuntimeStatus) }
                    Toggle("本机识别失败时用系统识别补读，并标明降级内容", isOn: $state.ocrFallbackToVision)
                        .onChange(of: state.ocrFallbackToVision) { _, _ in state.updateSettings() }
                    Note("关闭补读后，识别失败会让该文件导入失败（严格模式）。不会自动改用在线 Codex。")
                    if state.documentOCRProvider == .codex {
                        TextField("Codex 模型", text: $state.codexOCRModel).onSubmit { state.updateSettings() }
                        Picker("推理强度", selection: $state.codexOCREffort) {
                            ForEach(["low", "medium", "high", "xhigh"], id: \.self) { Text($0).tag($0) }
                        }.onChange(of: state.codexOCREffort) { _, _ in state.updateSettings() }
                        HStack {
                            Text("每个文件超时（秒）")
                            TextField("600", value: $state.codexOCRTimeout, format: .number).onSubmit {
                                state.codexOCRTimeout = max(30, min(3600, state.codexOCRTimeout)); state.updateSettings()
                            }
                        }
                    }
                    Button("重建语义索引") { state.rebuildSemanticIndex() }
                        .disabled(!state.documentAvailability.ready(.semantic) || !state.semanticRetrievalEnabled || state.isBuildingSemanticIndex)
                }
            }
            Note("资料保留原件、Markdown 与定位信息，原件按内容去重。下载组件或模型不会自动上传资料，只有你选择在线代读或在线回答时才使用对应服务。")
        }
    }
    private var currentReady: Bool {
        switch state.documentOCRProvider {
        case .system: true
        case .paddle: state.documentAvailability.ready(.paddle)
        case .codex: state.documentAvailability.ready(.codex)
        }
    }
    private var currentSummary: String {
        let name: String = switch state.documentOCRProvider {
        case .system: "系统识别"
        case .paddle: "本机增强识别"
        case .codex: "Codex 文件代读"
        }
        return currentReady ? "当前使用：\(name)" : "已选择\(name)，但尚未就绪；请先准备，或改用系统识别。"
    }
    private var semanticSummary: String {
        if !state.semanticRetrievalEnabled { return availability(.semantic) + " · 尚未启用" }
        if !state.documentAvailability.ready(.semantic) { return "尚未就绪，当前仍使用关键词查找" }
        if state.isBuildingSemanticIndex { return "正在更新索引；未就绪部分使用关键词" }
        if state.semanticIndex.isEmpty { return "等待添加资料或建立索引；当前使用关键词" }
        return "已生效 · 按关键词与含义查找相关片段"
    }
    private func availability(_ feature: DocumentFeatureAvailability.Feature) -> String {
        if !state.documentModelsChecked { return "正在检查本机…" }
        if state.documentAvailability.ready(feature) { return "本应用已安装 · 可使用" }
        if feature == .codex, state.checkingCodexStatus { return "正在检查 Codex 登录…" }
        if feature == .codex, !state.codexReady { return state.codexInstalled ? "需要登录 Codex" : "未检测到 Codex" }
        return "尚未接入 · 可扫描或下载，最多 " + state.documentDownloadSize(feature)
    }
    private func provider(_ value: DocumentOCRProvider, title: String, badge: String, ready: Bool, detail: String) -> some View {
        choice(title: title, status: badge, detail: detail) {
            if state.documentOCRProvider == value && ready {
                Label("正在使用", systemImage: "checkmark.circle.fill").foregroundStyle(.green)
            } else if ready {
                Button("使用") { state.selectDocumentProvider(value) }.buttonStyle(SecondaryButtonStyle())
            } else if value == .codex && state.checkingCodexStatus {
                ProgressView().controlSize(.small)
            } else if value == .codex && !state.codexReady {
                Button("连接 Codex") { state.openSettings(.answers) }.buttonStyle(SecondaryButtonStyle())
            } else {
                if value == .paddle && state.documentOCRProvider != .paddle {
                    Button("选择模型") { state.documentOCRProvider = .paddle; state.documentProviderWasSelected = true; state.documentProviderSelectionRevision += 1; state.updateSettings() }.buttonStyle(SecondaryButtonStyle())
                } else { featureButton(value == .paddle ? .paddle : .codex, title: "准备并使用") }
            }
        }
    }
    private func featureButton(_ feature: DocumentFeatureAvailability.Feature, title: String) -> some View {
        Button(title) { state.prepareDocumentFeature(feature) }
            .buttonStyle(SecondaryButtonStyle()).disabled(state.documentSetupBusy || !state.documentModelsChecked)
    }
    private func choice<Action: View>(title: String, status: String, detail: String, @ViewBuilder action: () -> Action) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(alignment: .top, spacing: 12) {
                VStack(alignment: .leading, spacing: 4) {
                    Text(title).font(.system(size: 14, weight: .semibold))
                    Text(status).font(.system(size: 11, weight: .medium)).foregroundStyle(.secondary)
                }
                Spacer(minLength: 4)
                action().font(.system(size: 12))
            }
            Text(detail).font(.system(size: 12)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
        }.padding(12).background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 12))
    }
}

/// Plain-language tradeoffs shared with the first-run guide; no installation is required here.
struct DocumentChoiceSummary: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            summary("读取资料", "系统识别：Mac 自带、离线、无需下载；复杂表格和公式可能丢失结构。\n本机增强：另下约 1.42 GB（含共用组件），适合复杂版面；占存储与内存，仍可能识别错。\nCodex 代读：免 OCR 权重，另需约 186 MB 共用组件；联网、使用账号额度，耗时受网络影响。")
            summary("查找资料（向量检索可选）", "不启用：按关键词查找，无需下载或建索引，准备快；同义问法可能漏检。\n启用：模型加共用组件最多约 835 MB，先在本机建索引，额外占内存；能补充按含义查找，但仍可能漏检。")
            Text("两者都只把相关片段带入回答，并非“关闭向量就发送全文”。全量发送资料会增加等待与用量，也可能超过上下文上限。共用组件只需安装一次。")
                .font(.system(size: 12)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
        }
    }
    private func summary(_ title: String, _ detail: String) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(title).font(.headline)
            Text(detail).font(.system(size: 12)).foregroundStyle(.secondary).lineSpacing(7).fixedSize(horizontal: false, vertical: true)
        }.padding(14).frame(maxWidth: .infinity, alignment: .leading)
            .background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 12))
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
                VStack(alignment: .leading, spacing: 3) {
                    Text(descriptor.name).fontWeight(.medium)
                    Text(!state.documentModelsChecked ? "检查中…" : isInstalled ? "已安装 · 文件已校验" : "尚未接入 · 可扫描或下载")
                        .font(.caption).foregroundStyle(isInstalled ? .green : .secondary)
                }
                Spacer()
                if downloading { Button("取消") { state.cancelManagedModelDownload() } }
                else {
                    Button(isInstalled ? "校验／修复" : "准备") { state.downloadManagedModel(descriptor) }
                        .disabled(state.documentSetupBusy || !state.documentModelsChecked)
                    if isInstalled { Button("删除", role: .destructive) { state.deleteManagedModel(descriptor) }.disabled(state.documentSetupBusy) }
                }
            }
            Note("\(descriptor.formattedSize) · \(descriptor.license) · \(descriptor.summary)")
            Link("模型来源与版本", destination: URL(string: descriptor.homepage)!)
            if downloading {
                ProgressView(value: state.managedModelDownloadProgress)
                if !state.managedModelDownloadStatus.isEmpty { Note(state.managedModelDownloadStatus) }
            }
        }.padding(.vertical, 6)
    }
}
