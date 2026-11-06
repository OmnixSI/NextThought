import SwiftUI
import NextThoughtCore

// The first-run guide's materials page. Document components (text recognition, page layout, the
// embedding model) that are already on this Mac are found by the same scan as settings → 资料 and
// switched on with one button. If none are there, the built-in route (system recognition and
// keyword search) is already in use and nothing needs to be downloaded; the larger components
// are offered, not pushed.

@MainActor
extension AppState {
    /// An installed text-recognition model, the selected one first.
    var installedOCRChoice: ManagedModelDescriptor? {
        ModelCatalog.localOCRChoices.first { $0.id == localOCRModelID && installedDocumentModels.contains($0.id) }
            ?? ModelCatalog.localOCRChoices.first { installedDocumentModels.contains($0.id) }
    }

    /// What is really ready on this Mac: the shared runtime and every model a feature needs.
    var onboardingMaterialsOffer: OnboardingMaterialsOffer {
        OnboardingMaterialsOffer(
            ocrReady: documentModelsChecked && runtimeInstalled && installedOCRChoice != nil
                && installedDocumentModels.contains(ModelCatalog.layout.id),
            semanticReady: documentAvailability.ready(.semantic))
    }

    /// Ready components that are not switched on yet. Someone who chose another reader (Codex) is left alone.
    var onboardingMaterialsPending: [OnboardingMaterialsOffer.Item] {
        onboardingMaterialsOffer.pending(usingOCR: documentOCRProvider != .system, usingSemantic: semanticRetrievalEnabled)
    }

    /// “用上这些”: switch on what is ready, through the same calls as settings → 资料.
    func useFoundDocumentComponents() {
        let offer = onboardingMaterialsOffer
        if offer.ocrReady {
            if let model = installedOCRChoice, model.id != localOCRModelID { localOCRModelID = model.id }
            selectDocumentProvider(.paddle)
        }
        if offer.semanticReady { selectSemanticRetrieval(true) }
    }
}

struct OnboardingMaterialsPage: View {
    @Environment(AppState.self) private var state
    @State private var showDownloads = false

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s6) {
            VStack(alignment: .leading, spacing: Space.s2) {
                Text("有资料，就放进来").font(Typo.display).fixedSize(horizontal: false, vertical: true)
                Text("议程、简历、产品文档都可以。会前读一遍，会中的回答才贴合这场会议。可以跳过。")
                    .font(Typo.body).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
            }
            VStack(alignment: .leading, spacing: Space.s3) {
                status
                Button("扫描本机已有组件") { state.scanLocalDocumentAssets() }
                    .buttonStyle(SecondaryButtonStyle())
                    .disabled(!state.documentModelsChecked || state.documentSetupBusy || DemoMode.isEnabled)
                Text("仅在点击后查找本机文件；系统可能询问文件夹访问权限。")
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                if !state.localAssetScanStatus.isEmpty {
                    Text(verbatim: state.localAssetScanStatus).font(Typo.caption).foregroundStyle(Ink.secondary)
                }
                if !state.onboardingMaterialsOffer.missing.isEmpty, state.documentModelsChecked {
                    downloads
                }
                Text("之后在“准备”页把文件拖进来，或到资料夹添加。需要时也可以到设置 → 资料里安装增强组件。")
                    .font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
            }
            DisclosureGroup("了解各种读取方式的取舍") {
                DocumentChoiceSummary().padding(.top, Space.s2)
            }
            .font(Typo.label)
        }
        .onChange(of: state.documentSetupFeature) { _, feature in
            if feature != nil { showDownloads = true }
        }
    }

    // MARK: Status

    private func name(_ item: OnboardingMaterialsOffer.Item) -> String {
        switch item {
        case .ocr: String(localized: "文字识别（OCR）")
        case .semantic: String(localized: "语义检索")
        }
    }

    @ViewBuilder private var status: some View {
        let offer = state.onboardingMaterialsOffer
        if !state.documentModelsChecked || state.isScanningLocalAssets {
            HStack(spacing: Space.s3) {
                ProgressView().controlSize(.small)
                Text("正在查找本机已有的组件…").font(Typo.body)
                Spacer(minLength: 0)
                if state.isScanningLocalAssets {
                    Button("取消") { state.localAssetScanTask?.cancel() }.buttonStyle(GhostButtonStyle(height: 28))
                }
            }
        } else if !offer.found.isEmpty {
            let names = offer.found.map(name).joined(separator: "、")
            HStack(alignment: .center, spacing: Space.s3) {
                StatusDot(tone: .ready)
                Text(verbatim: String(localized: "找到本机已有的：\(names)")).font(Typo.body)
                    .fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: 0)
            }
            if state.onboardingMaterialsPending.isEmpty {
                Text("已经用上，之后导入的资料会按它们来读。").font(Typo.caption).foregroundStyle(Ink.secondary)
            } else {
                Button("用上这些") { state.useFoundDocumentComponents() }
                    .buttonStyle(PrimaryButtonStyle(height: Space.controlLarge))
            }
        }
        if state.onboardingMaterialsPending.isEmpty, state.documentOCRProvider == .system, !state.semanticRetrievalEnabled,
           state.documentModelsChecked, !state.isScanningLocalAssets {
            HStack(spacing: Space.s2) {
                StatusDot(tone: .ready)
                Text("资料用系统识别和关键词查找，不需要额外下载。").font(Typo.body)
            }
        }
    }

    // MARK: Downloads

    private func feature(_ item: OnboardingMaterialsOffer.Item) -> DocumentFeatureAvailability.Feature {
        item == .ocr ? .paddle : .semantic
    }

    private func detail(_ item: OnboardingMaterialsOffer.Item) -> String {
        switch item {
        case .ocr: String(localized: "读扫描件、表格和公式更准")
        case .semantic: String(localized: "问法和原文不同，也能找到相关段落")
        }
    }

    @ViewBuilder private var downloads: some View {
        DisclosureGroup("下载增强组件（可选）", isExpanded: $showDownloads) {
            VStack(alignment: .leading, spacing: Space.s3) {
                ForEach(state.onboardingMaterialsOffer.missing, id: \.self) { item in
                    downloadRow(item)
                }
                if !state.documentSetupStatus.isEmpty {
                    Text(verbatim: state.documentSetupStatus).font(Typo.caption).foregroundStyle(Ink.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            .padding(.top, Space.s2)
        }
        .font(Typo.label)
    }

    private func downloadRow(_ item: OnboardingMaterialsOffer.Item) -> some View {
        let target = feature(item)
        let downloading = state.documentSetupFeature == target
        return HStack(alignment: .center, spacing: Space.s3) {
            VStack(alignment: .leading, spacing: 2) {
                Text(verbatim: name(item)).font(Typo.body)
                Text(verbatim: detail(item)).font(Typo.caption).foregroundStyle(Ink.secondary)
            }
            Spacer(minLength: Space.s2)
            if downloading {
                ProgressView(value: state.documentSetupProgress).frame(width: 90)
                Button("取消") { state.documentSetupTask?.cancel() }.buttonStyle(GhostButtonStyle(height: 28))
            } else {
                Button("下载 · 约 \(state.documentDownloadSize(target))") { state.prepareDocumentFeature(target) }
                    .buttonStyle(SecondaryButtonStyle())
                    .disabled(state.documentSetupBusy || !state.documentModelsChecked)
            }
        }
        .padding(.horizontal, Space.s3).padding(.vertical, Space.s2)
        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
    }
}
