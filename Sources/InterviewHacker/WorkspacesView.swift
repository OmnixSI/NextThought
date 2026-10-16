import SwiftUI
import UniformTypeIdentifiers
import InterviewHackerCore

/// “场景”: one workspace per interview / talk / meeting series, each with its own
/// inputs, materials, answer requirements, experience bank, question bank and reviews.
struct WorkspacesView: View {
    @Environment(AppState.self) private var state

    var body: some View {
        GeometryReader { proxy in
            let wide = proxy.size.width >= 820
            HStack(alignment: .top, spacing: 12) {
                WorkspaceList(compact: !wide)
                    .frame(width: wide ? 270 : 210)
                WorkspaceEditor()
                    .id(state.activeWorkspace.id)
            }
            .padding(.horizontal, 16).padding(.bottom, 14).padding(.top, 4)
        }
    }
}

// MARK: - List

private struct WorkspaceList: View {
    @Environment(AppState.self) private var state
    var compact: Bool
    @State private var deleting: Workspace?

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("场景").font(.system(size: 15, weight: .semibold))
                    Text("每套面试 / 报告 / 会议单独管理").font(.system(size: 11)).foregroundStyle(.secondary)
                }
                Spacer()
                Menu {
                    ForEach(SessionMode.allCases) { mode in
                        Button { state.createWorkspace(mode: mode) } label: { Label(mode.title, systemImage: mode.symbol) }
                    }
                    Divider()
                    Button("导入场景或备份…") { state.importData(applySettings: false) }
                } label: { Image(systemName: "plus") }
                    .menuStyle(.button).buttonStyle(GlassIconButtonStyle(size: 28)).menuIndicator(.hidden).fixedSize()
                    .help("新建场景")
            }
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 6) {
                    ForEach(SessionMode.allCases) { mode in
                        let items = state.workspaces.visible.filter { $0.mode == mode }
                        if !items.isEmpty {
                            Label(mode.title, systemImage: mode.symbol)
                                .font(.system(size: 11, weight: .semibold)).foregroundStyle(.secondary).padding(.top, 6)
                            ForEach(items) { workspace in row(workspace) }
                        }
                    }
                }
            }
        }
        .card()
        .frame(maxHeight: .infinity, alignment: .top)
        .confirmationDialog("删除场景“\(deleting?.name ?? "")”？", isPresented: Binding(get: { deleting != nil }, set: { if !$0 { deleting = nil } }), titleVisibility: .visible) {
            Button("删除", role: .destructive) { if let deleting { state.deleteWorkspace(deleting.id) }; deleting = nil }
            Button("取消", role: .cancel) { deleting = nil }
        } message: {
            Text("资料、经验库和题库会一起删除（会在“备份”文件夹留一份副本）。会话记录不受影响。")
        }
    }

    private func row(_ workspace: Workspace) -> some View {
        let active = workspace.id == state.activeWorkspace.id
        let sessions = state.archive.sessions.filter { $0.workspaceID == workspace.id }.count
        return VStack(alignment: .leading, spacing: 3) {
            HStack(spacing: 6) {
                Text(workspace.name).font(.system(size: 13, weight: active ? .semibold : .regular)).lineLimit(1)
                Spacer(minLength: 0)
                if active { Image(systemName: "checkmark.circle.fill").foregroundStyle(Brand.accent) }
            }
            if !compact {
                Text("\(workspace.documents.count) 份资料 · \(workspace.questionBank.count) 题 · \(sessions) 次会话")
                    .font(.system(size: 10.5)).foregroundStyle(.secondary).lineLimit(1)
            }
        }
        .padding(9)
        .background(active ? Brand.accent.opacity(0.14) : Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 11, style: .continuous))
        .contentShape(Rectangle())
        .onTapGesture { state.switchWorkspace(workspace.id) }
        .contextMenu {
            Button("使用这个场景") { state.switchWorkspace(workspace.id) }
            Button("复制一份") { state.duplicateWorkspace(workspace.id) }
            Button("导出场景（含资料与题库）…") { state.exportWorkspace(workspace.id, includeSessions: false) }
            Button("导出场景与会话记录…") { state.exportWorkspace(workspace.id, includeSessions: true) }
            Divider()
            Button("删除…", role: .destructive) { deleting = workspace }.disabled(state.workspaces.workspaces.count <= 1)
        }
    }
}

// MARK: - Editor

private enum EditorTab: String, CaseIterable, Identifiable {
    case materials = "资料与要求", knowledge = "经验库与题库", review = "复盘"
    var id: String { rawValue }
}

private struct WorkspaceEditor: View {
    @Environment(AppState.self) private var state
    @AppStorage("workspaceEditorTab") private var tabRaw = EditorTab.materials.rawValue

    var body: some View {
        @Bindable var state = state
        let tab = EditorTab(rawValue: tabRaw) ?? .materials
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 10) {
                Image(systemName: state.sessionMode.symbol)
                    .font(.system(size: 15, weight: .semibold)).foregroundStyle(.white)
                    .frame(width: 34, height: 34).background(Brand.gradient, in: RoundedRectangle(cornerRadius: 9, style: .continuous))
                TextField("场景名称", text: Binding(get: { state.activeWorkspace.name }, set: { state.activeWorkspace.name = $0 }))
                    .textFieldStyle(.plain).font(.system(size: 17, weight: .semibold))
                Spacer()
                MenuPicker(selection: Binding(get: { state.sessionMode }, set: { state.sessionMode = $0; state.rebuildContextIndex() }),
                           options: SessionMode.allCases.map { ($0, $0.title) })
                MenuPicker(selection: $state.answerStyle, options: AnswerStyle.allCases.map { ($0, $0.title) })
                Button { state.predictQuestions() } label: { Label("预测问题", systemImage: "wand.and.stars") }
                    .buttonStyle(ChipButtonStyle()).disabled(state.isAnswering)
                    .help("根据资料预测最可能被问到的问题，并给出要点")
            }
            PillTabs(items: EditorTab.allCases.map { (value: $0.rawValue, title: $0.rawValue) }, selection: $tabRaw)
            ScrollView {
                VStack(alignment: .leading, spacing: 14) {
                    switch tab {
                    case .materials: MaterialsTab()
                    case .knowledge: KnowledgeTab()
                    case .review: ReviewTab()
                    }
                }
                .padding(.bottom, 8)
            }
        }
        .card()
        .frame(maxHeight: .infinity, alignment: .top)
    }
}

private struct FieldEditor: View {
    let field: ModeField
    @Binding var text: String
    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(field.title).font(.system(size: 12, weight: .semibold))
            if field.long {
                ZStack(alignment: .topLeading) {
                    if text.isEmpty {
                        Text(field.placeholder).font(.system(size: 13)).foregroundStyle(.tertiary).padding(.horizontal, 11).padding(.vertical, 8).allowsHitTesting(false)
                    }
                    TextEditor(text: $text).font(.system(size: 13)).scrollContentBackground(.hidden).frame(minHeight: 60, maxHeight: 140).padding(5)
                }
                .background(Color.primary.opacity(0.05), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
            } else {
                TextField(field.placeholder, text: $text).textFieldStyle(.roundedBorder)
            }
        }
    }
}

/// Skills this scenario uses: global ones always, others ticked here.
private struct WorkspaceSkillsSection: View {
    @Environment(AppState.self) private var state
    private var library: SkillLibrary { SkillLibrary.shared }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Label("技能 \(state.activeSkillSnippets.count)", systemImage: "wand.and.stars").font(.system(size: 13, weight: .semibold))
                Spacer()
                Button { let added = library.importFiles(global: false); for skill in added { state.setSkill(skill.id, attached: true) } } label: {
                    Label("导入并用于此场景…", systemImage: "square.and.arrow.down")
                }.buttonStyle(ChipButtonStyle())
                Button("管理技能") { state.openSettings(.skills) }.buttonStyle(ChipButtonStyle())
            }
            if library.skills.isEmpty {
                Text("还没有技能。技能是一份 Markdown 说明（如 SKILL.md）：说话方式、答题套路、领域知识，回答时会遵循。").font(.caption).foregroundStyle(.secondary)
            } else {
                ForEach(library.skills) { skill in
                    let attached = skill.global || state.activeWorkspace.skillIDs.contains(skill.id)
                    Toggle(isOn: Binding(get: { attached }, set: { state.setSkill(skill.id, attached: $0) })) {
                        HStack(spacing: 6) {
                            Text(skill.name)
                            if skill.global { Text("所有场景").font(.system(size: 10, weight: .semibold)).foregroundStyle(Brand.accent) }
                            if !skill.enabled { Text("已停用").font(.system(size: 10)).foregroundStyle(.secondary) }
                            if !skill.summary.isEmpty { Text(skill.summary).font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(1) }
                        }
                    }
                    .toggleStyle(.checkbox)
                    .disabled(skill.global)
                }
            }
        }
    }
}

private struct MaterialsTab: View {
    @Environment(AppState.self) private var state
    @State private var importingCategory: String?
    @State private var dropTargeted = false

    private var allowedTypes: [UTType] { DocumentImporter.supportedExtensions.compactMap { UTType(filenameExtension: $0) } }

    var body: some View {
        @Bindable var state = state
        let profile = ModeProfile.for(state.sessionMode)
        // Inputs specific to the mode.
        VStack(alignment: .leading, spacing: 10) {
            Label("\(state.sessionMode.title)信息", systemImage: "list.bullet.rectangle").font(.system(size: 13, weight: .semibold))
            ForEach(profile.fields) { field in
                FieldEditor(field: field, text: Binding(get: { state.activeWorkspace.fields[field.id] ?? "" },
                                                        set: { state.activeWorkspace.fields[field.id] = $0 }))
            }
            FieldEditor(field: ModeField("notes", "其他背景", "任何希望回答时参考的事实：经历、数字、对方偏好…", long: true), text: $state.profile)
        }
        Divider()
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Label("回答要求", systemImage: "text.badge.checkmark").font(.system(size: 13, weight: .semibold))
                Spacer()
                Button("恢复\(state.sessionMode.title)模板") { state.applyTemplatePreferences() }.buttonStyle(ChipButtonStyle())
            }
            TextEditor(text: $state.answerPreferences)
                .font(.system(size: 13)).frame(minHeight: 70, maxHeight: 160)
                .scrollContentBackground(.hidden)
                .padding(6).background(Color.primary.opacity(0.05), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
            Text("用什么语言、多长、先说什么；与资料正文分开处理，文件内容不能改写这里的要求。").font(.caption).foregroundStyle(.secondary)
        }
        Divider()
        WorkspaceSkillsSection()
        Divider()
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Label("资料 \(state.documents.count)/\(AppState.maxDocuments)", systemImage: "doc.on.doc").font(.system(size: 13, weight: .semibold))
                Spacer()
                if state.isImportingDocuments || state.isIndexingDocuments { ProgressView().controlSize(.small) }
            }
            ForEach(categories(profile), id: \.self) { category in
                categorySection(category)
            }
            Text("支持 PDF（含扫描件 OCR）、Word、PowerPoint、Excel、Markdown、TXT、RTF、HTML 与图片。文件在本机解析；回答时只把与问题相关的片段交给模型，并标注来源。也可以把文件直接拖到这里（归入“其他”）。")
                .font(.caption).foregroundStyle(.secondary)
        }
        .padding(8)
        .background(RoundedRectangle(cornerRadius: 12).strokeBorder(style: StrokeStyle(lineWidth: 1.2, dash: [6])).foregroundStyle(dropTargeted ? Color.accentColor : Color.clear))
        .onDrop(of: [.fileURL], isTargeted: $dropTargeted) { providers in loadDropped(providers); return true }
        .fileImporter(isPresented: Binding(get: { importingCategory != nil }, set: { if !$0 { importingCategory = nil } }),
                      allowedContentTypes: allowedTypes, allowsMultipleSelection: true) { result in
            let category = importingCategory
            importingCategory = nil
            switch result {
            case .success(let urls): state.importDocuments(urls, category: category)
            case .failure(let error): state.errorMessage = error.localizedDescription
            }
        }
        if !state.selectedExcerpts.isEmpty {
            DisclosureGroup("最近回答用到的资料片段") {
                ForEach(Array(state.selectedExcerpts.enumerated()), id: \.offset) { _, item in
                    VStack(alignment: .leading, spacing: 4) {
                        Text("\(item.source) · p.\(item.page)").font(.system(size: 12, weight: .semibold))
                        Text(item.text).font(.callout).textSelection(.enabled)
                    }.padding(.vertical, 6)
                }
            }
        }
    }

    /// The mode's slots plus any category already used by a file.
    private func categories(_ profile: ModeProfile) -> [String] {
        var result = profile.documentCategories
        for document in state.documents {
            let category = document.category ?? "其他"
            if !result.contains(category) { result.append(category) }
        }
        return result
    }

    private func categorySection(_ category: String) -> some View {
        let items = state.documents.filter { ($0.category ?? "其他") == category }
        return VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text(category).font(.system(size: 12, weight: .semibold))
                Text("\(items.count)").font(.caption.monospacedDigit()).foregroundStyle(.tertiary)
                Spacer()
                Button { importingCategory = category } label: { Label("添加", systemImage: "plus") }
                    .buttonStyle(ChipButtonStyle()).disabled(state.isImportingDocuments)
            }
            ForEach(items) { document in
                HStack(spacing: 8) {
                    Toggle("", isOn: Binding(get: { state.documents.first(where: { $0.id == document.id })?.enabled ?? false },
                                             set: { state.setDocumentEnabled(document.id, enabled: $0) }))
                        .labelsHidden().toggleStyle(.switch).controlSize(.mini).help("是否用于回答")
                    Image(systemName: icon(for: document.name)).foregroundStyle(Color.accentColor)
                    VStack(alignment: .leading, spacing: 1) {
                        Text(document.name).font(.system(size: 12.5, weight: .medium)).lineLimit(1)
                        Text("\(document.pages.count) 页 · \(document.characterCount) 字符").font(.caption2).foregroundStyle(.secondary)
                    }
                    Spacer()
                    Menu {
                        ForEach(categories(ModeProfile.for(state.sessionMode)), id: \.self) { target in
                            Button(target) { state.setDocumentCategory(document.id, category: target) }
                        }
                    } label: { Image(systemName: "folder") }
                        .menuStyle(.button).buttonStyle(.borderless).menuIndicator(.hidden).fixedSize().help("移动到其他分类")
                    Button { state.removeDocument(document.id) } label: { Image(systemName: "trash") }
                        .buttonStyle(.borderless).foregroundStyle(.red).help("移除")
                }
                .padding(7).background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
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
            if urls.isEmpty { state.errorMessage = "拖入的文件类型不受支持。" } else { state.importDocuments(urls, category: "其他") }
        }
    }
}

// MARK: - Knowledge

private struct KnowledgeTab: View {
    @Environment(AppState.self) private var state
    @State private var newLesson = ""
    @State private var newQuestion = ""
    @State private var newAnswer = ""
    @State private var search = ""

    var body: some View {
        let workspace = state.activeWorkspace
        VStack(alignment: .leading, spacing: 8) {
            Toggle("回答时参考这里的经验和题库（自我迭代）", isOn: Binding(get: { state.activeWorkspace.useKnowledge },
                                                              set: { state.activeWorkspace.useKnowledge = $0; state.rebuildContextIndex() }))
            Text("每次会话结束生成摘要后，AI 复盘会把“经验沉淀”和对方问到的问题（附改进后的回答）加入这里；下次回答同类问题时优先采用。可以随时编辑、置顶或删除。")
                .font(.caption).foregroundStyle(.secondary)
        }
        Divider()
        VStack(alignment: .leading, spacing: 8) {
            Label("经验库 \(workspace.lessons.count)", systemImage: "lightbulb").font(.system(size: 13, weight: .semibold))
            ForEach(workspace.lessons.sorted { ($0.pinned ? 1 : 0, $0.createdAt) > ($1.pinned ? 1 : 0, $1.createdAt) }) { lesson in
                HStack(alignment: .top, spacing: 8) {
                    Button { var next = lesson; next.pinned.toggle(); state.updateLesson(next) } label: {
                        Image(systemName: lesson.pinned ? "pin.fill" : "pin").foregroundStyle(lesson.pinned ? Brand.accent : .secondary)
                    }.buttonStyle(.borderless).help(lesson.pinned ? "取消置顶" : "置顶：总是交给模型")
                    TextField("", text: Binding(get: { lesson.text }, set: { var next = lesson; next.text = $0; state.updateLesson(next) }), axis: .vertical)
                        .textFieldStyle(.plain).font(.system(size: 13))
                    Button { state.deleteLesson(lesson.id) } label: { Image(systemName: "xmark") }.buttonStyle(.borderless).foregroundStyle(.secondary)
                }
                .padding(8).background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
            }
            HStack {
                TextField("添加一条经验，例如：被问到项目时先报规模和数字", text: $newLesson).textFieldStyle(.roundedBorder)
                    .onSubmit { state.addLesson(newLesson); newLesson = "" }
                Button("添加") { state.addLesson(newLesson); newLesson = "" }.buttonStyle(ChipButtonStyle(prominent: true)).disabled(newLesson.isEmpty)
            }
        }
        Divider()
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Label("题库 \(workspace.questionBank.count)", systemImage: "questionmark.folder").font(.system(size: 13, weight: .semibold))
                Spacer()
                TextField("搜索题库", text: $search).textFieldStyle(.roundedBorder).frame(maxWidth: 220)
            }
            ForEach(bank(workspace)) { item in BankRow(item: item) }
            VStack(alignment: .leading, spacing: 6) {
                TextField("新问题（例如背过的八股题）", text: $newQuestion).textFieldStyle(.roundedBorder)
                TextField("准备好的回答", text: $newAnswer, axis: .vertical).lineLimit(2...6).textFieldStyle(.roundedBorder)
                HStack {
                    Spacer()
                    Button("加入题库") { state.addBankItem(question: newQuestion, answer: newAnswer); newQuestion = ""; newAnswer = "" }
                        .buttonStyle(ChipButtonStyle(prominent: true)).disabled(newQuestion.isEmpty || newAnswer.isEmpty)
                }
            }
        }
    }

    private func bank(_ workspace: Workspace) -> [QuestionBankItem] {
        let query = search.trimmingCharacters(in: .whitespaces)
        let items = workspace.questionBank.sorted { ($0.timesAsked, $0.updatedAt) > ($1.timesAsked, $1.updatedAt) }
        guard !query.isEmpty else { return items }
        return items.filter { $0.question.localizedCaseInsensitiveContains(query) || $0.answer.localizedCaseInsensitiveContains(query) }
    }
}

private struct BankRow: View {
    @Environment(AppState.self) private var state
    let item: QuestionBankItem
    @State private var expanded = false

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .top) {
                Button { withAnimation(.easeOut(duration: 0.15)) { expanded.toggle() } } label: {
                    Image(systemName: expanded ? "chevron.down" : "chevron.right").foregroundStyle(.secondary)
                }.buttonStyle(.borderless)
                Text(item.question).font(.system(size: 13, weight: .medium)).lineLimit(expanded ? nil : 1)
                Spacer()
                if item.timesAsked > 0 {
                    Text("被问 \(item.timesAsked) 次").font(.system(size: 10)).foregroundStyle(.secondary)
                        .padding(.horizontal, 6).padding(.vertical, 2).background(Color.primary.opacity(0.06), in: Capsule())
                }
                Button { state.deleteBankItem(item.id) } label: { Image(systemName: "trash") }.buttonStyle(.borderless).foregroundStyle(.red)
            }
            if expanded {
                TextField("问题", text: Binding(get: { item.question }, set: { var next = item; next.question = $0; state.updateBankItem(next) }), axis: .vertical)
                    .textFieldStyle(.roundedBorder)
                TextField("回答", text: Binding(get: { item.answer }, set: { var next = item; next.answer = $0; state.updateBankItem(next) }), axis: .vertical)
                    .lineLimit(3...12).textFieldStyle(.roundedBorder)
            } else {
                Text(item.answer).font(.system(size: 12)).foregroundStyle(.secondary).lineLimit(2).padding(.leading, 22)
            }
        }
        .padding(8).background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }
}

// MARK: - Review

private struct ReviewTab: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let workspace = state.activeWorkspace
        let sessions = state.archive.sessions.filter { $0.workspaceID == workspace.id }
        let summarized = sessions.filter { $0.summaryState == .ready }
        HStack(spacing: 10) {
            stat("会话", "\(sessions.count)")
            stat("问题", "\(sessions.reduce(0) { $0 + $1.questionCount })")
            stat("经验", "\(workspace.lessons.count)")
            stat("题库", "\(workspace.questionBank.count)")
        }
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Label("阶段复盘", systemImage: "chart.line.uptrend.xyaxis").font(.system(size: 13, weight: .semibold))
                if let date = workspace.stageReviewAt {
                    Text("· \(date.formatted(date: .abbreviated, time: .shortened))").font(.caption).foregroundStyle(.tertiary)
                }
                Spacer()
                if state.stageReviewing.contains(workspace.id) { ProgressView().controlSize(.small) }
                Button { state.generateStageReview(for: workspace.id) } label: {
                    Label(workspace.stageReview == nil ? "生成阶段复盘" : "重新生成", systemImage: "sparkles")
                }
                .buttonStyle(ChipButtonStyle(prominent: workspace.stageReview == nil))
                .disabled(summarized.isEmpty || state.stageReviewing.contains(workspace.id))
                .help(summarized.isEmpty ? "需要至少一次带摘要的会话" : "综合这个场景的 \(summarized.count) 次会话：高频问题、薄弱点、进步和下一步准备")
            }
            if let review = workspace.stageReview {
                MarkdownText(text: review, size: 13, lineSpacing: 3)
                    .padding(10).background(Brand.accent.opacity(0.06), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
            } else {
                Text("综合这个场景下所有会话的摘要，找出高频问题、反复出现的薄弱点和已经改进的地方，并给出下一步准备清单；新的经验会加入经验库。")
                    .font(.caption).foregroundStyle(.secondary)
            }
        }
        Divider()
        VStack(alignment: .leading, spacing: 6) {
            Label("会话记录 \(sessions.count)", systemImage: "clock").font(.system(size: 13, weight: .semibold))
            if sessions.isEmpty {
                Text("在这个场景下开始辅助后，记录会出现在这里。").font(.caption).foregroundStyle(.secondary)
            }
            ForEach(sessions) { meta in
                HStack(spacing: 8) {
                    VStack(alignment: .leading, spacing: 2) {
                        Text(meta.displayTitle).font(.system(size: 13, weight: .medium)).lineLimit(1)
                        Text("\(meta.startedAt.formatted(date: .abbreviated, time: .shortened)) · \(Int(meta.duration / 60)) 分钟 · \(meta.questionCount) 个问题")
                            .font(.system(size: 11)).foregroundStyle(.secondary)
                    }
                    Spacer()
                    switch meta.summaryState {
                    case .ready: Image(systemName: "sparkles").foregroundStyle(Brand.accent)
                    case .pending: ProgressView().controlSize(.mini)
                    case .failed: Image(systemName: "exclamationmark.circle").foregroundStyle(.orange)
                    case .none: EmptyView()
                    }
                    Button("查看") { state.historySelection = meta.id; state.historyWorkspaceFilter = workspace.id; state.section = .history }
                        .buttonStyle(ChipButtonStyle())
                }
                .padding(8).background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
            }
        }
    }

    private func stat(_ title: String, _ value: String) -> some View {
        VStack(spacing: 2) {
            Text(value).font(.system(size: 18, weight: .semibold).monospacedDigit())
            Text(title).font(.system(size: 11)).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity).padding(.vertical, 8)
        .background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }
}
