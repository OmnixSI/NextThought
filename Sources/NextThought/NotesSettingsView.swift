import SwiftUI
import NextThoughtCore

/// Settings → 纪要: the notes model (separate from live answers), what happens after a
/// meeting, custom templates and how a meeting ends. The default template of each
/// scene is chosen on the 场景 page.
struct NotesSettingsView: View {
    @Environment(AppState.self) private var state
    @Environment(\.settingsNavigate) private var navigate
    @State private var editing: NotesTemplate?
    @State private var deleting: NotesTemplate?

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
            SettingsSection(title: "纪要模型", icon: "doc.text") {
                Note("会后整理不赶时间，追求准确和详细，所以用单独的模型：默认是能用到的最强模型，推理强度“高”，不开快速模式。", presentation: .inline)
                if state.answerBackend == .openAICompatible {
                    TextField("模型 ID（留空时与回答模型相同）", text: $state.notesModelID)
                        .textFieldStyle(PaperTextFieldStyle())
                        .onChange(of: state.notesModelID) { _, _ in state.saveNotesSettings() }
                } else {
                    MenuPicker(title: "模型", selection: $state.notesModelID, options: modelOptions)
                        .onChange(of: state.notesModelID) { _, _ in state.saveNotesSettings() }
                    MenuPicker(title: "推理强度", selection: $state.notesReasoning, options: effortOptions)
                        .onChange(of: state.notesReasoning) { _, _ in state.saveNotesSettings() }
                }
                MenuPicker(title: "纪要语言", selection: $state.notesLanguage,
                           options: [("auto", String(localized: "跟随界面语言")), ("zh", "简体中文"), ("en", "English")])
                    .onChange(of: state.notesLanguage) { _, _ in state.saveNotesSettings() }
                Note("不重新识别声音、不保存原始录音。整理分三步：结合资料校对转写、按模板生成带时间点的纪要，再核对原话依据和日期；在后台进行，失败后保留转写并可重试。", presentation: .inline)
            }

            SettingsSection(title: "会后整理", icon: "wand.and.stars") {
                Toggle("会议结束后自动整理", isOn: $state.autoSummarize)
                    .disabled(!state.recordSessions)
                    .onChange(of: state.autoSummarize) { _, _ in state.saveNotesSettings() }
                VStack(alignment: .leading, spacing: 3) {
                    Toggle("自动收集经验与答法候选", isOn: $state.autoCollectKnowledgeCandidates)
                        .disabled(!state.recordSessions || !state.autoSummarize)
                        .onChange(of: state.autoCollectKnowledgeCandidates) { _, _ in state.updateSettings() }
                    Note("整理时顺带挑出值得记住的经验和答法，逐项确认后才加入系列。", presentation: .inline)
                }
                if !state.recordSessions {
                    HStack(spacing: 8) {
                        Note("这两项需要先开启“记录每次会议”。", color: Ink.caution, presentation: .inline)
                        Button("去打开") { navigate(.data, "记录每次会议") }
                            .buttonStyle(ChipButtonStyle())
                            .accessibilityIdentifier("settings.notes.openRecording")
                    }
                } else if !state.autoSummarize {
                    Note("自动收集经验需要先开启上面的自动整理。", presentation: .inline)
                }
            }

            SettingsSection(title: "自定义模板", icon: "square.and.pencil") {
                Note("一个模板就是若干部分，每部分有名字和一句给 AI 的要求，例如“风险：列出可能导致延期的事，附原话”。可以从现有模板复制一份再改；每个场景用哪个模板，在“场景”页选。", presentation: .inline)
                ForEach(state.customNotesTemplates) { template in
                    HStack(spacing: 8) {
                        Text(verbatim: template.name).fontWeight(.medium)
                        Text("\(template.sections.count) 个部分").foregroundStyle(Ink.secondary)
                        Spacer()
                        Button("编辑") { editing = template }.buttonStyle(SecondaryButtonStyle())
                        Button(role: .destructive) { deleting = template } label: {
                            Text("删除").foregroundStyle(Ink.live)
                        }
                        .buttonStyle(GhostButtonStyle())
                        .accessibilityLabel(Text("删除模板“\(template.name)”"))
                    }
                }
                Menu("从模板复制…") {
                    ForEach(state.allNotesTemplates()) { template in
                        Button(template.name) { editing = state.duplicateNotesTemplate(template) }
                    }
                }
                .fixedSize()
            }

            AutoEndSettingsSection()
        }
        .sheet(item: $editing) { template in
            NotesTemplateEditor(template: template)
                .environment(state)
        }
        .confirmationDialog("删除模板“\(deleting?.name ?? "")”？", isPresented: Binding(
            get: { deleting != nil },
            set: { if !$0 { deleting = nil } }
        ), titleVisibility: .visible) {
            Button("删除模板", role: .destructive) {
                if let deleting { state.deleteNotesTemplate(deleting.id) }
                deleting = nil
            }
            Button("取消", role: .cancel) { deleting = nil }
                .keyboardShortcut(.defaultAction)
        } message: {
            Text("删除后不能再选择此模板。使用它的默认设置将恢复为各类型的内置模板，已经生成的纪要不会被删除。")
        }
    }

    private var modelOptions: [(String, String)] {
        let automaticTitle = state.notesEngine.model.isEmpty ? String(localized: "自动选择可用模型")
            : String(localized: "自动 · \(state.notesEngine.model)")
        var options: [(String, String)] = [("", automaticTitle)]
        options += state.codexModels.map { ($0.id, $0.name) }
        if !state.notesModelID.isEmpty, !state.codexModels.contains(where: { $0.id == state.notesModelID }) {
            options.append((state.notesModelID, state.notesModelID))
        }
        return options
    }

    private var effortOptions: [(String, String)] {
        let model = state.notesEngine.model
        let efforts = state.codexModels.first { $0.id == model }?.efforts ?? ["low", "medium", "high"]
        var options = efforts.map { value in
            let title: String = switch value {
            case "low": String(localized: "低")
            case "medium": String(localized: "中")
            case "high": String(localized: "高")
            case "xhigh": String(localized: "更高")
            default: value
            }
            return (value, title)
        }
        if !efforts.contains(state.notesReasoning) { options.append((state.notesReasoning, state.notesReasoning)) }
        return options
    }
}

/// Edits a custom template: its name and, per section, a name and one requirement.
private enum NotesTemplateEditorField: Hashable {
    case name
    case sectionTitle(String)
    case addSection
}

private struct NotesTemplateEditor: View {
    @Environment(AppState.self) private var state
    @Environment(\.dismiss) private var dismiss
    @State var template: NotesTemplate
    @State private var attemptedSave = false
    @State private var saveFailure: String?
    @FocusState private var focusedField: NotesTemplateEditorField?

    private var nameIsEmpty: Bool { template.name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("编辑模板").font(Typo.section)
            VStack(alignment: .leading, spacing: 6) {
                Text("模板名称").font(Typo.label)
                TextField("例如：每周组会", text: $template.name)
                    .textFieldStyle(PaperTextFieldStyle())
                    .accessibilityLabel("模板名称")
                    .accessibilityHint(attemptedSave && nameIsEmpty ? Text("请填写模板名称。") : Text(""))
                    .accessibilityIdentifier("notes.template.name")
                    .focused($focusedField, equals: .name)
                if attemptedSave && nameIsEmpty {
                    validationMessage("请填写模板名称。")
                }
            }
            ScrollViewReader { scroll in
                ScrollView {
                    VStack(alignment: .leading, spacing: 10) {
                        ForEach(template.sections) { section in
                            SectionEditor(section: binding(for: section.id), validatesTitle: attemptedSave,
                                          focusedField: $focusedField) {
                                template.sections.removeAll { $0.id == section.id }
                            }
                            .id(section.id)
                        }
                    }
                }
                .onChange(of: focusedField) { _, field in
                    if case let .sectionTitle(id)? = field { scroll.scrollTo(id, anchor: .center) }
                }
            }
            if attemptedSave && template.sections.isEmpty {
                validationMessage("请至少添加一个部分。")
            }
            if let saveFailure {
                Text(verbatim: saveFailure).font(Typo.caption).foregroundStyle(Ink.caution)
                    .fixedSize(horizontal: false, vertical: true)
            }
            HStack {
                Button("添加一部分") {
                    let section = NotesSectionSpec(id: UUID().uuidString, title: "", instruction: "")
                    template.sections.append(section)
                    focusedField = .sectionTitle(section.id)
                }
                .buttonStyle(SecondaryButtonStyle())
                .focused($focusedField, equals: .addSection)
                Spacer()
                Button("取消", role: .cancel) { dismiss() }
                    .buttonStyle(GhostButtonStyle()).keyboardShortcut(.cancelAction)
                Button("保存", action: save)
                    .buttonStyle(PrimaryButtonStyle()).keyboardShortcut(.defaultAction)
            }
        }
        .padding(20)
        .frame(width: 600, height: 560)
    }

    private func validationMessage(_ message: LocalizedStringKey) -> some View {
        Text(message).font(Typo.caption).foregroundStyle(Ink.caution)
            .fixedSize(horizontal: false, vertical: true)
    }

    private func save() {
        attemptedSave = true
        saveFailure = nil
        guard !nameIsEmpty else { focusedField = .name; return }
        guard !template.sections.isEmpty else { focusedField = .addSection; return }
        if let section = template.sections.first(where: { $0.title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }) {
            focusedField = .sectionTitle(section.id)
            return
        }
        // updateNotesTemplate ignores a missing template. Keep this draft open
        // unless the existing settings API actually accepts the full edit.
        guard state.customNotesTemplates.contains(where: { $0.id == template.id }) else {
            saveFailure = String(localized: "此模板已不存在，你的编辑仍保留在这里。请取消后重新选择模板。")
            return
        }
        state.updateNotesTemplate(template)
        guard state.customNotesTemplates.first(where: { $0.id == template.id }) == template else {
            saveFailure = String(localized: "未能保存模板，你的编辑仍保留在这里。请重试。")
            return
        }
        dismiss()
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
    let validatesTitle: Bool
    let focusedField: FocusState<NotesTemplateEditorField?>.Binding
    let onDelete: () -> Void

    private var titleIsEmpty: Bool { section.title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 8) {
                Text("部分名称").font(Typo.label)
                Spacer(minLength: 8)
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
                Button(role: .destructive, action: onDelete) {
                    Image(systemName: "minus.circle").foregroundStyle(Ink.live)
                }
                    .buttonStyle(GhostButtonStyle())
                    .accessibilityLabel(titleIsEmpty ? Text("删除这一部分") : Text("删除部分“\(section.title)”"))
                    .help("删除这一部分")
            }
            TextField("例如：下一步实验", text: $section.title)
                .textFieldStyle(PaperTextFieldStyle())
                .accessibilityLabel("部分名称")
                .accessibilityHint(validatesTitle && titleIsEmpty ? Text("请填写部分名称；已写的要求会保留。") : Text(""))
                .accessibilityIdentifier("notes.template.section.\(section.id).title")
                .focused(focusedField, equals: .sectionTitle(section.id))
            if validatesTitle && titleIsEmpty {
                Text("请填写部分名称；已写的要求会保留。")
                    .font(Typo.caption).foregroundStyle(Ink.caution)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Text("给 AI 的要求").font(Typo.label)
            TextField("例如：列出下一步实验、对照与所需数据", text: $section.instruction, axis: .vertical)
                .lineLimit(1...3)
                .textFieldStyle(PaperTextFieldStyle())
                .accessibilityLabel("给 AI 的要求")
            Toggle("这是“不足”的部分（答得不好、没讨论透、风险）", isOn: $section.isGap)
                .font(Typo.caption)
        }
        .paperCard(padding: 12)
    }
}
