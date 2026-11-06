import SwiftUI
import NextThoughtCore

/// Settings → 纪要: the notes model (separate from live answers), when to write the
/// notes, their language, the template of each meeting type and custom templates.
struct NotesSettingsView: View {
    @Environment(AppState.self) private var state
    @State private var editing: NotesTemplate?

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
        SettingsCardRow {
            SettingsSection(title: "纪要模型", icon: "doc.text") {
                HStack(spacing: 6) {
                    Text("会后纪要使用独立模型。")
                        .font(.system(size: 11.5)).foregroundStyle(.secondary)
                    Hint("会后整理纪要不赶时间，追求准确和详细，所以用单独的模型：默认是能用到的最强模型，推理强度“高”，不开快速模式。", presentation: .info)
                        .accessibilityLabel("纪要模型说明")
                }
                if state.answerBackend == .openAICompatible {
                    TextField("模型 ID（留空时与回答模型相同）", text: $state.notesModelID)
                        .textFieldStyle(.roundedBorder)
                        .onChange(of: state.notesModelID) { _, _ in state.saveNotesSettings() }
                } else {
                    MenuPicker(title: "模型", selection: $state.notesModelID, options: modelOptions)
                        .onChange(of: state.notesModelID) { _, _ in state.saveNotesSettings() }
                    MenuPicker(title: "推理强度", selection: $state.notesReasoning, options: effortOptions)
                        .onChange(of: state.notesReasoning) { _, _ in state.saveNotesSettings() }
                }
                Toggle("会议结束后自动整理", isOn: $state.autoSummarize)
                    .disabled(!state.recordSessions)
                    .onChange(of: state.autoSummarize) { _, _ in state.saveNotesSettings() }
                if !state.recordSessions { Hint("请先在“数据”中开启会话记录。", presentation: .inline) }
                MenuPicker(title: "纪要语言", selection: $state.notesLanguage,
                           options: [("auto", String(localized: "跟随界面语言")), ("zh", "简体中文"), ("en", "English")])
                    .onChange(of: state.notesLanguage) { _, _ in state.saveNotesSettings() }
                Hint("不重新识别声音、不保存原始录音；整理在后台进行，失败后保留转写并可重试。", presentation: .inline)
                HStack(spacing: 6) {
                    Text("整理流程").font(.system(size: 11.5)).foregroundStyle(.secondary)
                    Hint("整理分三步：结合资料校对转写、按模板生成带时间点的纪要，再核对原话依据和日期。", presentation: .info)
                        .accessibilityLabel("纪要整理流程说明")
                }
            }
        } right: {
            SettingsSection(title: "各类型的默认模板", icon: "list.bullet.rectangle") {
                ForEach(SessionMode.builtIn) { mode in
                    HStack(spacing: 12) {
                        Text(verbatim: mode.displayName).frame(width: 150, alignment: .leading)
                        MenuPicker(selection: Binding(get: { state.defaultNotesTemplateID(for: mode) },
                                                      set: { state.setDefaultNotesTemplate($0, for: mode) }),
                                   options: state.allNotesTemplates().map { ($0.id, $0.name) })
                        Spacer(minLength: 0)
                    }
                }
                Hint("纪要页的“模板”菜单可以给单场会议换个模板重新整理。")
            }
        }
            SettingsSection(title: "自定义模板", icon: "square.and.pencil") {
                HStack(spacing: 6) {
                    Text("从现有模板复制后，可以编辑各部分。")
                        .font(.system(size: 11.5)).foregroundStyle(.secondary)
                    Hint("一个模板就是若干部分，每部分有名字和一句给 AI 的要求，例如“风险：列出可能导致延期的事，附原话”。可以从现有模板复制一份再改。", presentation: .info)
                        .accessibilityLabel("自定义模板说明")
                }
                ForEach(state.customNotesTemplates) { template in
                    HStack(spacing: 8) {
                        Text(verbatim: template.name).fontWeight(.medium)
                        Text("\(template.sections.count) 个部分").foregroundStyle(Ink.secondary)
                        Spacer()
                        Button("编辑") { editing = template }.buttonStyle(ChipButtonStyle())
                        Button("删除") { state.deleteNotesTemplate(template.id) }.buttonStyle(ChipButtonStyle())
                    }
                }
                Menu("从模板复制…") {
                    ForEach(state.allNotesTemplates()) { template in
                        Button(template.name) { editing = state.duplicateNotesTemplate(template) }
                    }
                }
                .fixedSize()
            }
        }
        .sheet(item: $editing) { template in
            NotesTemplateEditor(template: template)
                .environment(state)
        }
    }

    private var modelOptions: [(String, String)] {
        var options: [(String, String)] = [("", String(localized: "自动 · \(state.notesEngine.model)"))]
        options += state.codexModels.map { ($0.id, $0.name) }
        if !state.notesModelID.isEmpty, !state.codexModels.contains(where: { $0.id == state.notesModelID }) {
            options.append((state.notesModelID, state.notesModelID))
        }
        return options
    }

    private var effortOptions: [(String, String)] {
        let model = state.notesEngine.model
        let efforts = state.codexModels.first { $0.id == model }?.efforts ?? ["low", "medium", "high"]
        var options = efforts.map { ($0, $0) }
        if !efforts.contains(state.notesReasoning) { options.append((state.notesReasoning, state.notesReasoning)) }
        return options
    }
}

/// Edits a custom template: its name and, per section, a name and one requirement.
private struct NotesTemplateEditor: View {
    @Environment(AppState.self) private var state
    @Environment(\.dismiss) private var dismiss
    @State var template: NotesTemplate

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("编辑模板").font(Typo.section)
            TextField("模板名称", text: $template.name).textFieldStyle(.roundedBorder)
            ScrollView {
                VStack(alignment: .leading, spacing: 10) {
                    ForEach(template.sections) { section in
                        SectionEditor(section: binding(for: section.id)) {
                            template.sections.removeAll { $0.id == section.id }
                        }
                    }
                }
            }
            HStack {
                Button("添加一部分") {
                    template.sections.append(NotesSectionSpec(id: UUID().uuidString, title: "", instruction: ""))
                }
                .buttonStyle(SecondaryButtonStyle())
                Spacer()
                Button("取消") { dismiss() }.buttonStyle(SecondaryButtonStyle())
                Button("保存") {
                    template.sections.removeAll { $0.title.trimmingCharacters(in: .whitespaces).isEmpty }
                    state.updateNotesTemplate(template)
                    dismiss()
                }
                .buttonStyle(PrimaryButtonStyle())
                .disabled(template.name.trimmingCharacters(in: .whitespaces).isEmpty)
            }
        }
        .padding(20)
        .frame(width: 600, height: 560)
    }

    /// Looks the section up by id, so removing one never leaves a dangling binding.
    private func binding(for id: String) -> Binding<NotesSectionSpec> {
        Binding(get: { template.sections.first { $0.id == id } ?? NotesSectionSpec(id: id, title: "", instruction: "") },
                set: { value in
                    if let index = template.sections.firstIndex(where: { $0.id == id }) { template.sections[index] = value }
                })
    }
}

private struct SectionEditor: View {
    @Binding var section: NotesSectionSpec
    let onDelete: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 8) {
                TextField("部分名称，例如：风险", text: $section.title).textFieldStyle(.roundedBorder)
                switch section.kind {
                case .items, .actions:
                    Toggle("待办格式（谁 · 什么时候）", isOn: Binding(get: { section.kind == .actions },
                                                          set: { section.kind = $0 ? .actions : .items }))
                        .toggleStyle(.inline)
                        .fixedSize()
                case .oneLine:
                    Text("一句话").font(Typo.caption).foregroundStyle(Ink.secondary)
                case .questions:
                    Text("逐题回放").font(Typo.caption).foregroundStyle(Ink.secondary)
                }
                Button(action: onDelete) { Image(systemName: "minus.circle") }
                    .buttonStyle(.borderless)
                    .help("删除这一部分")
            }
            TextField("给 AI 的要求，例如：列出可能导致延期的事，附原话", text: $section.instruction, axis: .vertical)
                .lineLimit(1...3)
                .textFieldStyle(.roundedBorder)
            Toggle("这是“不足”的部分（答得不好、没讨论透、风险）", isOn: $section.isGap)
                .font(Typo.caption)
        }
        .paperCard(padding: 12)
    }
}
