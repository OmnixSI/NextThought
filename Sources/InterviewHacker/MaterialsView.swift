import SwiftUI
import UniformTypeIdentifiers
import InterviewHackerCore

struct MaterialsView: View {
    @Environment(AppState.self) private var state
    @State private var importing = false
    @State private var presetName = ""
    @State private var presetIncludesFiles = true
    @State private var dropTargeted = false

    private var allowedTypes: [UTType] {
        DocumentImporter.supportedExtensions.compactMap { UTType(filenameExtension: $0) }
    }

    var body: some View {
        @Bindable var state = state
        ScrollView {
            VStack(alignment: .leading, spacing: 14) {
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("本次会话资料与要求").font(.title3.weight(.semibold))
                        Text("文件在本机解析；回答时只把与问题相关的片段交给模型。").font(.callout).foregroundStyle(.secondary)
                    }
                    Spacer()
                    Menu {
                        if state.presets.isEmpty { Text("还没有预设") }
                        ForEach(state.presets) { preset in
                            Button("\(preset.name) · \(preset.mode.title)\(preset.documents.isEmpty ? "" : " · \(preset.documents.count) 份文件")") { state.applyPreset(preset) }
                        }
                        if !state.presets.isEmpty {
                            Divider()
                            Menu("删除预设") {
                                ForEach(state.presets) { preset in Button(preset.name, role: .destructive) { state.deletePreset(preset.id) } }
                            }
                        }
                    } label: { Label("预设", systemImage: "square.stack") }
                    .fixedSize()
                }

                VStack(alignment: .leading, spacing: 10) {
                    Picker("场景", selection: $state.sessionMode) {
                        ForEach(SessionMode.allCases) { Text($0.title).tag($0) }
                    }.pickerStyle(.segmented).onChange(of: state.sessionMode) { _, _ in state.updateSettings() }
                    Picker("回答样式", selection: $state.answerStyle) {
                        ForEach(AnswerStyle.allCases) { Text($0.title).tag($0) }
                    }.pickerStyle(.segmented).onChange(of: state.answerStyle) { _, _ in state.updateSettings() }
                }.card()

                VStack(alignment: .leading, spacing: 8) {
                    HStack {
                        Label("回答要求 / Prompt", systemImage: "text.badge.checkmark").font(.system(size: 13, weight: .semibold))
                        Spacer()
                        Button("填入\(state.sessionMode.title)模板") { state.applyTemplatePreferences() }.buttonStyle(ChipButtonStyle())
                    }
                    TextEditor(text: $state.answerPreferences)
                        .font(.system(size: 13)).frame(minHeight: 90)
                        .scrollContentBackground(.hidden)
                        .padding(6).background(Color.primary.opacity(0.05), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                        .onChange(of: state.answerPreferences) { _, _ in state.updateSettings() }
                    Text("例如：用英文回答；先给结论；只结合本次课件；涉及经历只能引用我提供的事实。此处与文件正文分开处理，文件内容不能改写这里的要求。")
                        .font(.caption).foregroundStyle(.secondary)
                }.card()

                VStack(alignment: .leading, spacing: 8) {
                    Label("背景说明", systemImage: "person.text.rectangle").font(.system(size: 13, weight: .semibold))
                    TextEditor(text: $state.profile)
                        .font(.system(size: 13)).frame(minHeight: 70)
                        .scrollContentBackground(.hidden)
                        .padding(6).background(Color.primary.opacity(0.05), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                        .onChange(of: state.profile) { _, value in state.setProfile(value) }
                    Text("例如：我应聘的岗位、会议目标、课程名称；面试时可写几段真实经历要点。").font(.caption).foregroundStyle(.secondary)
                }.card()

                VStack(alignment: .leading, spacing: 10) {
                    HStack {
                        Label("文件 \(state.documents.count)/12", systemImage: "doc.on.doc").font(.system(size: 13, weight: .semibold))
                        Spacer()
                        Button("预测可能的问题", systemImage: "wand.and.stars") { state.predictQuestions() }
                            .buttonStyle(ChipButtonStyle())
                            .disabled(state.isAnswering)
                            .help("根据资料生成最可能被问到的问题和回答要点")
                        Button("添加文件", systemImage: "plus") { importing = true }
                            .buttonStyle(ChipButtonStyle(prominent: true)).disabled(state.isImportingDocuments)
                    }
                    if state.isImportingDocuments || state.isIndexingDocuments { ProgressView("正在解析（含 OCR）并建立本地索引…").controlSize(.small) }
                    if state.documents.isEmpty {
                        VStack(spacing: 6) {
                            Image(systemName: "tray.and.arrow.down").font(.title2).foregroundStyle(.secondary)
                            Text("拖入简历、JD、会议议程或课件").font(.callout)
                            Text("支持 PDF（含扫描件 OCR）、Word、PowerPoint、Excel、Markdown、TXT、RTF、HTML 与图片").font(.caption).foregroundStyle(.secondary)
                        }
                        .frame(maxWidth: .infinity).padding(.vertical, 22)
                        .background(RoundedRectangle(cornerRadius: 12).strokeBorder(style: StrokeStyle(lineWidth: 1.5, dash: [6])).foregroundStyle(dropTargeted ? Color.accentColor : Color.secondary.opacity(0.4)))
                    }
                    ForEach(state.documents) { document in
                        HStack(alignment: .center, spacing: 10) {
                            Toggle("用于回答", isOn: Binding(get: { state.documents.first(where: { $0.id == document.id })?.enabled ?? false }, set: { state.setDocumentEnabled(document.id, enabled: $0) }))
                                .labelsHidden().toggleStyle(.switch).controlSize(.small).help("是否允许这份资料参与回答")
                            Image(systemName: icon(for: document.name)).foregroundStyle(Color.accentColor)
                            VStack(alignment: .leading, spacing: 2) {
                                Text(document.name).font(.system(size: 13, weight: .medium)).lineLimit(1).textSelection(.enabled)
                                Text("\(document.pages.count) 页 · \(document.characterCount) 字符").font(.caption).foregroundStyle(.secondary)
                            }
                            Spacer()
                            Button { state.removeDocument(document.id) } label: { Image(systemName: "trash") }
                                .buttonStyle(.borderless).foregroundStyle(.red).help("移除")
                        }
                        .padding(8).background(Color.primary.opacity(0.045), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                    }
                    Text("最多 12 份；每份 20 MiB / 200 页 / 20 万字符，总计 60 万字符。文件默认只在当前运行内存中；保存为预设后可在下次一键恢复。")
                        .font(.caption).foregroundStyle(.secondary)
                }
                .card()
                .onDrop(of: [.fileURL], isTargeted: $dropTargeted) { providers in
                    loadDropped(providers); return true
                }

                VStack(alignment: .leading, spacing: 8) {
                    Label("保存为预设", systemImage: "square.and.arrow.down").font(.system(size: 13, weight: .semibold))
                    HStack {
                        TextField("例如：字节后端二面 / 周会 / 操作系统课", text: $presetName).textFieldStyle(.roundedBorder)
                        Toggle("包含文件", isOn: $presetIncludesFiles).toggleStyle(.checkbox)
                        Button("保存") { state.savePreset(named: presetName, includeDocuments: presetIncludesFiles); presetName = "" }
                            .buttonStyle(ChipButtonStyle(prominent: true))
                            .disabled(presetName.trimmingCharacters(in: .whitespaces).isEmpty)
                    }
                    Text("预设保存场景、样式、回答要求、背景说明，以及（可选）已解析的文件文字，存放在本机 Application Support。").font(.caption).foregroundStyle(.secondary)
                }.card()

                if !state.selectedExcerpts.isEmpty {
                    DisclosureGroup("最近回答使用的资料片段（不代表模型已核验引用）") {
                        ForEach(Array(state.selectedExcerpts.enumerated()), id: \.offset) { _, item in
                            VStack(alignment: .leading, spacing: 4) {
                                Text("\(item.source) · p.\(item.page)").font(.system(size: 12, weight: .semibold))
                                Text(item.text).font(.callout).textSelection(.enabled)
                            }.padding(.vertical, 6)
                        }
                    }.card()
                }
            }
            .padding(16)
        }
        .fileImporter(isPresented: $importing, allowedContentTypes: allowedTypes, allowsMultipleSelection: true) { result in
            switch result {
            case .success(let urls): state.importDocuments(urls)
            case .failure(let error): state.errorMessage = error.localizedDescription
            }
        }
    }

    private func icon(for name: String) -> String {
        switch (name as NSString).pathExtension.lowercased() {
        case "pdf": "doc.richtext"
        case "pptx": "rectangle.on.rectangle.angled"
        case "xlsx", "csv": "tablecells"
        case "png", "jpg", "jpeg", "heic", "tiff": "photo"
        default: "doc.text"
        }
    }

    private func loadDropped(_ providers: [NSItemProvider]) {
        Task { @MainActor in
            var urls: [URL] = []
            for provider in providers {
                guard let item = try? await provider.loadItem(forTypeIdentifier: UTType.fileURL.identifier) else { continue }
                let url = (item as? URL) ?? (item as? Data).flatMap { URL(dataRepresentation: $0, relativeTo: nil) }
                if let url, DocumentImporter.supportedExtensions.contains(url.pathExtension.lowercased()) { urls.append(url) }
            }
            if urls.isEmpty { state.errorMessage = "拖入的文件类型不受支持。" } else { state.importDocuments(urls) }
        }
    }
}
