import SwiftUI
import NextThoughtCore

/// How the app reads documents, in the words the settings page uses.
@MainActor
private extension AppState {
    var documentProviderIsReady: Bool {
        switch documentOCRProvider {
        case .system: true
        case .paddle: documentAvailability.ready(.paddle)
        case .codex: documentAvailability.ready(.codex)
        }
    }

    func documentFeatureStatus(_ feature: DocumentFeatureAvailability.Feature) -> String {
        if !documentModelsChecked { return String(localized: "正在检查本机…") }
        if documentAvailability.ready(feature) { return String(localized: "本应用已安装 · 可使用") }
        if feature == .codex, checkingCodexStatus { return String(localized: "正在检查 Codex 登录…") }
        if feature == .codex, !codexReady {
            return String(localized: codexInstalled ? "需要登录 Codex" : "未检测到 Codex")
        }
        return String(localized: "尚未接入 · 可扫描或下载，最多 \(documentDownloadSize(feature))")
    }
}

// MARK: - 回答与资料 → 资料

/// One choice: how documents are read. The details of components are in 高级 → 资料组件.
struct DocumentReadingSettings: View {
    @Environment(AppState.self) private var state
    @Environment(\.settingsNavigate) private var navigate

    var body: some View {
        SettingsSection(title: "资料") {
            MenuPicker(title: "读取资料的方式",
                       selection: Binding(get: { state.documentOCRProvider }, set: choose),
                       options: options)
                .accessibilityIdentifier("settings.documentProvider")
            if state.documentProviderIsReady {
                Note(help, presentation: .inline)
            } else {
                HStack(spacing: Space.s2) {
                    Note(String(localized: "已选的识别方式还没准备好，当前读取会失败。"), color: Ink.caution, presentation: .inline)
                    Button("先用系统识别") { state.selectDocumentProvider(.system) }.buttonStyle(ChipButtonStyle())
                }
            }
            if state.documentSetupFeature != nil || !state.documentSetupStatus.isEmpty {
                VStack(alignment: .leading, spacing: Space.s2) {
                    Note(state.documentSetupStatus, presentation: .inline)
                    if state.documentSetupFeature != nil {
                        HStack(spacing: Space.s3) {
                            ProgressView(value: state.documentSetupProgress)
                            Button("取消准备") { state.documentSetupTask?.cancel() }.buttonStyle(ChipButtonStyle())
                        }
                    }
                }
            }
            if !state.runtimeInstalled {
                HStack(spacing: Space.s3) {
                    VStack(alignment: .leading, spacing: 2) {
                        Text("导入 Word、Excel、PowerPoint 需要文档组件").font(Typo.label)
                        Note("最多下载约 186 MB，只需一次。", presentation: .inline)
                    }
                    Spacer(minLength: Space.s2)
                    Button("安装") { state.prepareDocumentFeature(.office) }
                        .buttonStyle(ChipButtonStyle())
                        .disabled(state.documentSetupBusy || !state.documentModelsChecked)
                }
            }
        }
    }

    private var options: [(DocumentOCRProvider, String)] {
        let paddle = state.documentAvailability.ready(.paddle)
            ? String(localized: "本机增强")
            : String(localized: "本机增强 · 需下载 \(state.documentDownloadSize(.paddle))")
        let codex = state.documentAvailability.ready(.codex) ? String(localized: "Codex 代读")
            : state.codexReady ? String(localized: "Codex 代读 · 需准备") : String(localized: "Codex 代读 · 需先连接 Codex")
        return [(.system, String(localized: "系统识别")), (.paddle, paddle), (.codex, codex)]
    }

    private var help: String {
        switch state.documentOCRProvider {
        case .system: String(localized: "Mac 自带，离线、无需下载。普通文字够用；复杂表格和公式可能丢失结构。")
        case .paddle: String(localized: "适合扫描件、表格和公式，在本机计算，占一些内存。")
        case .codex: String(localized: "读取原文件，不用下载识别模型。需要已登录的 Codex，占用账号额度。")
        }
    }

    private func choose(_ value: DocumentOCRProvider) {
        guard value != state.documentOCRProvider else { return }
        let feature: DocumentFeatureAvailability.Feature = value == .paddle ? .paddle : .codex
        if value == .system || state.documentAvailability.ready(feature) {
            state.selectDocumentProvider(value)
        } else if value == .codex, !state.codexReady {
            navigate(.answers, "回答服务")
        } else {
            state.prepareDocumentFeature(feature)
        }
    }
}

// MARK: - 高级 → 资料组件

struct DocumentComponentsAdvanced: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: Space.s3) {
            Note("资料原件保留在本机。只有选择在线代读或在线回答时，资料才会发送给对应服务。", presentation: .inline)
            if state.documentOCRProvider == .paddle {
                Picker("本机识别模型", selection: $state.localOCRModelID) {
                    ForEach(ModelCatalog.localOCRChoices, id: \.id) { descriptor in
                        let installedLabel = String(localized: state.installedDocumentModels.contains(descriptor.id) ? "已安装" : "未安装")
                        let localizedName = String(localized: String.LocalizationValue(descriptor.name))
                        Text(localizedName + " · " + descriptor.formattedSize + " · " + installedLabel).tag(descriptor.id)
                    }
                }.onChange(of: state.localOCRModelID) { _, _ in
                    state.documentProviderSelectionRevision += 1; state.updateSettings()
                }
            }
            Divider()
            Text("查找方式").font(Typo.label)
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
            Note("两种方式都只把相关片段放进回答，不会默认发送全部资料。", presentation: .inline)
            if !state.semanticIndexStatus.isEmpty { Note(state.semanticIndexStatus, presentation: .inline) }
            Button("重建语义索引") { state.rebuildSemanticIndex() }
                .buttonStyle(SecondaryButtonStyle())
                .disabled(!state.documentAvailability.ready(.semantic) || !state.semanticRetrievalEnabled || state.isBuildingSemanticIndex)
            Divider()
            Text("Word、Excel 与 PowerPoint").font(Typo.label)
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 5) {
                    Text(LocalizedStringKey(state.runtimeInstalled ? "已安装支持组件 · 可导入 Office 文件" : "需要共用文档组件"))
                        .foregroundStyle(state.runtimeInstalled ? .green : .secondary)
                    Note("会前会在后台提取正文、嵌图、备注和图表。")
                    if !LegacyOfficeConverter.available {
                        Link("安装旧版 PPT 支持（LibreOffice）", destination: URL(string: "https://www.libreoffice.org/download/download-libreoffice/")!)
                    } else { Note("旧版 PPT 本地转换组件已就绪。", presentation: .inline) }
                }
                Spacer(minLength: 12)
                if !state.runtimeInstalled { featureButton(.office, title: "安装支持") }
            }
            Divider()
            reuseExisting
            Divider()
            Note("下面列出实际安装状态；“已安装”表示文件已校验，识别方式是否启用以上方选择为准。")
            ForEach(ModelCatalog.all, id: \.id) { descriptor in
                ManagedModelRow(descriptor: descriptor, isInstalled: state.installedDocumentModels.contains(descriptor.id))
            }
            if !state.managedModelDownloadStatus.isEmpty { Note(state.managedModelDownloadStatus, presentation: .inline) }
            HStack {
                Text(LocalizedStringKey(state.runtimeInstalled ? "共用文档组件 · 已安装" : "共用文档组件 · 未安装"))
                Spacer()
                Button(state.runtimeInstalled ? "校验／修复" : "安装") { state.installDocumentRuntime() }
                    .disabled(state.documentSetupBusy || !state.documentModelsChecked)
                if state.runtimeInstalled {
                    Button("删除", role: .destructive) { state.deleteDocumentRuntime() }.disabled(state.documentSetupBusy)
                }
            }
            Note(DocumentRuntimeInstaller.sizeDescription, presentation: .inline)
            if state.isInstallingDocumentRuntime {
                Button("取消下载组件") { state.runtimeDownloadTask?.cancel() }
            }
            if !state.documentRuntimeStatus.isEmpty { Note(state.documentRuntimeStatus, presentation: .inline) }
            Divider()
            VStack(alignment: .leading, spacing: 3) {
                Toggle("本机识别失败时用系统识别补读，并标明降级内容", isOn: $state.ocrFallbackToVision)
                    .onChange(of: state.ocrFallbackToVision) { _, _ in state.updateSettings() }
                Note("关闭后，识别失败会让该文件导入失败；不会自动切到在线 Codex。", presentation: .inline)
            }
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
        }
    }

    /// Reuses components already downloaded on this Mac instead of fetching them again.
    @ViewBuilder private var reuseExisting: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("已经在这台 Mac 下载过？").font(Typo.label)
            Note("扫描并校验本机已有的兼容文档模型与支持组件后复用，保留原文件；不会自动启用识别或语义检索。", presentation: .inline)
            HStack {
                Button("扫描本机已有组件") { state.scanLocalDocumentAssets() }
                    .buttonStyle(SecondaryButtonStyle()).disabled(state.documentSetupBusy || !state.documentModelsChecked)
                Button("选择其他目录…") { state.chooseLocalDocumentAssets() }
                    .disabled(state.documentSetupBusy || !state.documentModelsChecked)
                if state.isScanningLocalAssets { ProgressView().controlSize(.small); Button("取消") { state.localAssetScanTask?.cancel() } }
            }
            if !state.documentModelsChecked { Note("正在检查组件状态，完成后可以扫描。") }
            else if state.documentSetupBusy && !state.isScanningLocalAssets { Note("正在准备其他组件，完成后可以扫描。") }
            if !state.localAssetScanStatus.isEmpty { Note(state.localAssetScanStatus, presentation: .inline) }
            Note("外置磁盘或未找到的目录可手动选择。只有版本与文件校验匹配的模型才能复用。", presentation: .inline)
        }
    }

    private var semanticSummary: String {
        if !state.semanticRetrievalEnabled { return state.documentFeatureStatus(.semantic) + String(localized: " · 尚未启用") }
        if !state.documentAvailability.ready(.semantic) { return "尚未就绪，当前仍使用关键词查找" }
        if state.isBuildingSemanticIndex { return "正在更新索引；未就绪部分使用关键词" }
        if state.semanticIndex.isEmpty { return "等待添加资料或建立索引；当前使用关键词" }
        return "已生效 · 按关键词与含义查找相关片段"
    }

    private func featureButton(_ feature: DocumentFeatureAvailability.Feature, title: String) -> some View {
        Button(LocalizedStringKey(title)) { state.prepareDocumentFeature(feature) }
            .buttonStyle(SecondaryButtonStyle()).disabled(state.documentSetupBusy || !state.documentModelsChecked)
    }

    private func choice<Action: View>(title: String, status: String, detail: String, @ViewBuilder action: () -> Action) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .top, spacing: 12) {
                VStack(alignment: .leading, spacing: 3) {
                    Text(LocalizedStringKey(title)).font(.system(size: 13, weight: .semibold))
                    Text(LocalizedStringKey(status)).font(.system(size: 11, weight: .medium)).foregroundStyle(.secondary)
                }
                Spacer(minLength: 4)
                action().font(.system(size: 12))
            }
            Text(LocalizedStringKey(detail)).font(.system(size: 12)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
        }.padding(.vertical, Space.s2)
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
                    Text(LocalizedStringKey(descriptor.name)).fontWeight(.medium)
                    Text(LocalizedStringKey(!state.documentModelsChecked ? "检查中…" : isInstalled ? "已安装 · 文件已校验" : "尚未接入 · 可扫描或下载"))
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
            let localizedSummary = String(localized: String.LocalizationValue(descriptor.summary))
            Note("\(descriptor.formattedSize) · \(descriptor.license) · \(localizedSummary)")
            Link("模型来源与版本", destination: URL(string: descriptor.homepage)!)
            if downloading {
                ProgressView(value: state.managedModelDownloadProgress)
                if !state.managedModelDownloadStatus.isEmpty { Note(state.managedModelDownloadStatus, presentation: .inline) }
            }
        }.padding(.vertical, 6)
    }
}
