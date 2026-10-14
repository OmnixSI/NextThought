import SwiftUI
import UniformTypeIdentifiers
import InterviewHackerCore

struct MaterialsView: View {
    @EnvironmentObject var state: AppState
    @State private var importing = false
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                HStack {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("本次会话资料").font(.title2.weight(.semibold))
                        Text("文件在本机解析；回答时只把检索到的片段交给 Codex。")
                            .font(.callout).foregroundStyle(.secondary)
                    }
                    Spacer()
                    Button("添加文件", systemImage: "plus") { importing = true }
                        .buttonStyle(.borderedProminent).disabled(state.isImportingDocuments)
                }
                Picker("场景", selection: $state.sessionMode) {
                    ForEach(SessionMode.allCases) { Text($0.title).tag($0) }
                }.pickerStyle(.segmented).onChange(of: state.sessionMode) { _, _ in state.updateSettings() }
                GroupBox("回答要求 / Prompt") {
                    TextEditor(text: $state.answerPreferences)
                        .font(.body).frame(minHeight: 100)
                        .onChange(of: state.answerPreferences) { _, _ in state.updateSettings() }
                    Text("例如：用英文回答；先给结论；只结合本次课件；涉及经历只能引用我提供的事实。此处与文件正文分开处理。")
                        .font(.caption).foregroundStyle(.secondary)
                }
                Picker("回答样式", selection: $state.answerStyle) {
                    ForEach(AnswerStyle.allCases) { Text($0.title).tag($0) }
                }.onChange(of: state.answerStyle) { _, _ in state.updateSettings() }
                GroupBox("背景说明（沿用原资料框）") {
                    TextEditor(text: $state.profile).font(.body).frame(minHeight: 80)
                        .onChange(of: state.profile) { _, value in state.setProfile(value) }
                }
                if state.isImportingDocuments || state.isIndexingDocuments { ProgressView("正在解析并建立本地资料索引…") }
                if state.documents.isEmpty {
                    ContentUnavailableView("添加简历、JD、会议议程或课件", systemImage: "doc.on.doc", description: Text("支持多份 TXT、Markdown、CSV、JSON、RTF、文字 PDF。扫描 PDF 和 Office 文件请先转换。"))
                }
                ForEach(state.documents) { document in
                    HStack(alignment: .top, spacing: 12) {
                        Toggle("用于回答", isOn: Binding(get: { state.documents.first(where: { $0.id == document.id })?.enabled ?? false }, set: { state.setDocumentEnabled(document.id, enabled: $0) }))
                            .labelsHidden().help("是否允许这份资料参与回答")
                        VStack(alignment: .leading, spacing: 4) {
                            Text(document.name).font(.headline).textSelection(.enabled)
                            Text("\(document.pages.count) 页 · \(document.characterCount) 字符").font(.caption).foregroundStyle(.secondary)
                        }
                        Spacer()
                        Button("移除", role: .destructive) { state.removeDocument(document.id) }.buttonStyle(.borderless)
                    }.padding(12).background(.quaternary.opacity(0.3), in: RoundedRectangle(cornerRadius: 12))
                }
                Text("最多 12 份文件；每份 20 MiB / 200 页 / 20 万字符，总计 60 万字符。文件仅留在当前进程内存，退出后需重新导入；背景说明和回答要求保存在本机。更换会议时请检查已启用资料，防止引用上一场内容。")
                    .font(.caption).foregroundStyle(.secondary)
                if !state.selectedExcerpts.isEmpty {
                    DisclosureGroup("最近回答提交的资料片段（不代表模型已核验引用）") {
                        ForEach(Array(state.selectedExcerpts.enumerated()), id: \.offset) { _, item in
                            VStack(alignment: .leading, spacing: 4) {
                                Text("\(item.source) · p.\(item.page)").font(.headline)
                                Text(item.text).font(.callout).textSelection(.enabled)
                            }.padding(.vertical, 6)
                        }
                    }
                }
            }.padding(20)
        }
        .fileImporter(isPresented: $importing, allowedContentTypes: [.plainText, .pdf, .rtf, .commaSeparatedText, .json, UTType(filenameExtension: "md") ?? .plainText], allowsMultipleSelection: true) { result in
            switch result {
            case .success(let urls): state.importDocuments(urls)
            case .failure(let error): state.errorMessage = error.localizedDescription
            }
        }
    }
}
