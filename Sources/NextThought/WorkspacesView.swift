import AppKit
import SwiftUI
import UniformTypeIdentifiers
import NextThoughtCore
import Observation

/// 资料夹: the series ("系列", stored as workspaces): one per interview process, talk
/// or recurring meeting, each with its own materials, answer requirements, lessons,
/// question bank and reviews, shared by all of its meetings.
struct WorkspacesView: View {
    @Environment(AppState.self) private var state
    @AppStorage(AppNavigationBar.foldersSidebarKey, store: UserDefaults.standard) private var isSeriesListHidden = false
    @State private var editor = WorkspaceEditController.shared

    var body: some View {
        GeometryReader { proxy in
            let wide = proxy.size.width >= 820
            // Read the workspace names in this parent view so the list is rebuilt
            // immediately when the active series is renamed. The editor header
            // updates from the active workspace directly; keeping the list keyed
            // to the same snapshot prevents those two views from briefly showing
            // different names.
            let listRevision = state.workspaces.visible
                .map { "\($0.id.uuidString):\($0.name):\($0.documents.count):\($0.questionBank.count)" }
                .joined(separator: "|")
            let activeWorkspaceID = state.activeWorkspace.id
            let hasVisibleWorkspaces = !state.workspaces.visible.isEmpty
            HStack(alignment: .top, spacing: isSeriesListHidden ? 0 : 12) {
                if !isSeriesListHidden {
                    WorkspaceList(compact: !wide, editor: editor)
                        .frame(width: wide ? 270 : 210)
                        .id("\(activeWorkspaceID.uuidString)|\(listRevision)")
                        .transition(.move(edge: .leading).combined(with: .opacity))
                }
                if hasVisibleWorkspaces {
                    WorkspaceEditor(editor: editor)
                        .id(activeWorkspaceID)
                } else {
                    emptyWorkspaceState
                }
            }
            .padding(.horizontal, 16).padding(.bottom, 14).padding(.top, 4)
        }
        .confirmationDialog("有未保存的修改", isPresented: Binding(
            get: { editor.showUnsavedPrompt }, set: { editor.showUnsavedPrompt = $0 }), titleVisibility: .visible) {
            Button("保存") { editor.saveAndContinue(state) }
            Button("放弃更改", role: .destructive) { editor.discardAndContinue(state) }
            Button("继续编辑", role: .cancel) { editor.continueEditing() }
        } message: {
            Text("保存后继续刚才的操作；放弃更改会丢弃本次草稿。")
        }
        .confirmationDialog("恢复默认回答要求？", isPresented: Binding(
            get: { editor.showRestorePrompt }, set: { editor.showRestorePrompt = $0 }), titleVisibility: .visible) {
            Button("恢复模板", role: .destructive) { editor.confirmRestoreTemplate(state) }
            Button("取消", role: .cancel) { editor.showRestorePrompt = false }
        } message: {
            Text("当前系列的回答要求会被模板内容替换；其他系列和资料不会改变。")
        }
    }

    private var emptyWorkspaceState: some View {
        VStack(spacing: 10) {
            Image(systemName: "folder")
                .font(.system(size: 28))
                .foregroundStyle(Ink.tertiary)
            Text("还没有系列")
                .font(.system(size: 16, weight: .semibold))
            Text("新建系列后，资料、回答要求和会议记录会归入该系列。")
                .font(.system(size: 12))
                .foregroundStyle(Ink.secondary)
                .multilineTextAlignment(.center)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: 340)
            Menu {
                ForEach(SessionMode.builtIn) { mode in
                    Button { editor.requestCreateWorkspace(mode, state: state) } label: {
                        Label(mode.displayName, systemImage: mode.symbol)
                    }
                }
            } label: {
                Label("新建系列", systemImage: "plus")
            }
            .buttonStyle(ChipButtonStyle(prominent: true))
            .disabled(state.isRunning)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .card()
    }
}

enum WorkspaceEditTarget: Equatable {
    case basic
    case requirements
    case lesson(UUID)
    case question(UUID)
}

@MainActor
@Observable
final class WorkspaceEditController {
    static let shared = WorkspaceEditController()

    private enum Transition {
        case editorPage(WorkspaceEditorSection)
        case appSection(AppSection)
        case workspace(UUID)
        case editWorkspaceInfo(UUID)
        case createWorkspace(SessionMode)
        case duplicateWorkspace(UUID)
        case deleteWorkspace(UUID, onDeleted: (() -> Void)?)
        case deleteWorkspaces([UUID], onDeleted: (() -> Void)?)
        case mode(SessionMode)
        case editEntry(WorkspaceEditTarget)
        case completeEntryEditing
        case deleteLesson(UUID)
        case deleteQuestion(UUID)
        case restoreTemplate
        case beginNewMeeting
        case closeWindow
        case terminateApp
    }

    var draft: Workspace?
    var original: Workspace?
    var target: WorkspaceEditTarget?
    var isEntryEditing = false
    var showUnsavedPrompt = false
    var showRestorePrompt = false
    private var pendingTransition: Transition?
    private var allowNextClose = false
    private var allowNextTermination = false

    private init() {}

    var hasChanges: Bool { draft != original }

    func isEditing(_ target: WorkspaceEditTarget) -> Bool {
        self.target == target && draft != nil
    }

    func updateDraft(_ next: Workspace) {
        guard next.id == draft?.id else { return }
        draft = next
    }

    func begin(_ target: WorkspaceEditTarget, workspace: Workspace) {
        self.target = target
        original = workspace
        draft = workspace
        if isEntryTarget(target) { isEntryEditing = true }
    }

    func requestEditorPage(_ page: WorkspaceEditorSection, state: AppState) {
        request(.editorPage(page), state: state)
    }

    func requestAppSection(_ section: AppSection, state: AppState) {
        request(.appSection(section), state: state)
    }

    func requestWorkspace(_ id: UUID, state: AppState) {
        request(.workspace(id), state: state)
    }

    func requestEditWorkspaceInfo(_ id: UUID, state: AppState) {
        guard !state.isRunning, state.workspaces.workspace(id) != nil else { return }
        request(.editWorkspaceInfo(id), state: state)
    }

    func requestCreateWorkspace(_ mode: SessionMode, state: AppState) {
        request(.createWorkspace(mode), state: state)
    }

    func requestDuplicateWorkspace(_ id: UUID, state: AppState) {
        request(.duplicateWorkspace(id), state: state)
    }

    func requestDeleteWorkspace(_ id: UUID, state: AppState, onDeleted: (() -> Void)? = nil) {
        guard !state.isRunning, state.workspaces.workspace(id) != nil else { return }
        if let reason = state.workspaceDeletionDisabledReason(id) {
            state.errorMessage = reason
            return
        }
        request(.deleteWorkspace(id, onDeleted: onDeleted), state: state)
    }

    func requestDeleteWorkspaces(_ ids: [UUID], state: AppState, onDeleted: (() -> Void)? = nil) {
        let uniqueIDs = Array(Set(ids)).sorted { $0.uuidString < $1.uuidString }
        guard !uniqueIDs.isEmpty,
              uniqueIDs.allSatisfy({ state.workspaces.workspace($0) != nil }) else { return }
        if state.isRunning {
            state.errorMessage = String(localized: "会议中不能管理系列")
            return
        }
        if let reason = uniqueIDs.compactMap({ state.workspaceDeletionDisabledReason($0) }).first {
            state.errorMessage = reason
            return
        }
        request(.deleteWorkspaces(uniqueIDs, onDeleted: onDeleted), state: state)
    }

    func requestMode(_ mode: SessionMode, state: AppState) {
        request(.mode(mode), state: state)
    }

    func requestEntryEdit(_ target: WorkspaceEditTarget, state: AppState) {
        request(.editEntry(target), state: state)
    }

    func requestCompleteEntryEditing(state: AppState) {
        request(.completeEntryEditing, state: state)
    }

    func requestDeleteLesson(_ id: UUID, state: AppState) {
        request(.deleteLesson(id), state: state)
    }

    func requestDeleteQuestion(_ id: UUID, state: AppState) {
        request(.deleteQuestion(id), state: state)
    }

    func requestRestoreTemplate(state: AppState) {
        request(.restoreTemplate, state: state)
    }

    func requestNewMeeting(state: AppState) {
        request(.beginNewMeeting, state: state)
    }

    func save(_ state: AppState) {
        guard let target, let draft else { return }
        let keepEntryMode = isEntryTarget(target)
        guard commit(draft, target: target, state: state) else { return }
        clearDraft(keepEntryMode: keepEntryMode)
    }

    func cancelDraft() {
        guard let target else { return }
        clearDraft(keepEntryMode: isEntryTarget(target))
    }

    func saveAndContinue(_ state: AppState) {
        let next = pendingTransition
        pendingTransition = nil
        showUnsavedPrompt = false
        guard let target, let draft else { return }
        let keepEntryMode = isEntryEditing && keepsEntryMode(next)
        guard commit(draft, target: target, state: state) else { return }
        clearDraft(keepEntryMode: keepEntryMode)
        if let next { perform(next, state: state) }
    }

    func discardAndContinue(_ state: AppState) {
        let next = pendingTransition
        pendingTransition = nil
        showUnsavedPrompt = false
        clearDraft(keepEntryMode: isEntryEditing && keepsEntryMode(next))
        if let next { perform(next, state: state) }
    }

    func continueEditing() {
        pendingTransition = nil
        showUnsavedPrompt = false
    }

    func confirmRestoreTemplate(_ state: AppState) {
        showRestorePrompt = false
        state.applyTemplatePreferences()
    }

    func shouldCloseWindow() -> Bool {
        if allowNextClose {
            allowNextClose = false
            return true
        }
        guard hasChanges else {
            clearDraft(keepEntryMode: false)
            return true
        }
        pendingTransition = .closeWindow
        showUnsavedPrompt = true
        return false
    }

    func shouldTerminateApplication() -> Bool {
        if allowNextTermination {
            allowNextTermination = false
            return true
        }
        guard hasChanges else {
            clearDraft(keepEntryMode: false)
            return true
        }
        pendingTransition = .terminateApp
        showUnsavedPrompt = true
        return false
    }

    private func request(_ next: Transition, state: AppState) {
        if hasChanges {
            pendingTransition = next
            showUnsavedPrompt = true
            return
        }
        clearDraft(keepEntryMode: isEntryEditing && keepsEntryMode(next))
        perform(next, state: state)
    }

    private func commit(_ draft: Workspace, target: WorkspaceEditTarget, state: AppState) -> Bool {
        if target == .basic && draft.name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            state.errorMessage = String(localized: "系列名称不能为空。")
            return false
        }
        guard hasChanges else { return true }
        do {
            try state.saveWorkspaceEdit(draft, target: target)
            return true
        } catch {
            state.errorMessage = String(localized: "保存失败：") + " \(error.localizedDescription)"
            return false
        }
    }

    private func clearDraft(keepEntryMode: Bool) {
        draft = nil
        original = nil
        target = nil
        if !keepEntryMode { isEntryEditing = false }
    }

    private func perform(_ next: Transition, state: AppState) {
        switch next {
        case .editorPage(let page):
            UserDefaults.standard.set(page.rawValue, forKey: "workspaceEditorFocusSection")
        case .appSection(let section):
            if state.isRunning && section == .settings {
                SettingsWindow.show()
            } else {
                state.section = section
            }
        case .workspace(let id):
            state.switchWorkspace(id)
        case .editWorkspaceInfo(let id):
            guard !state.isRunning, state.workspaces.workspace(id) != nil else { return }
            state.switchWorkspace(id)
            UserDefaults.standard.set(WorkspaceEditorSection.basic.rawValue, forKey: "workspaceEditorFocusSection")
            state.section = .library
            begin(.basic, workspace: state.activeWorkspace)
        case .createWorkspace(let mode):
            state.createWorkspace(mode: mode)
        case .duplicateWorkspace(let id):
            state.duplicateWorkspace(id)
        case .deleteWorkspace(let id, let onDeleted):
            Task {
                if await state.deleteWorkspace(id) { onDeleted?() }
            }
        case .deleteWorkspaces(let ids, let onDeleted):
            Task {
                for id in ids {
                    guard await state.deleteWorkspace(id) else { return }
                }
                onDeleted?()
            }
        case .mode(let mode):
            state.sessionMode = mode
            state.rebuildContextIndex()
        case .editEntry(let target):
            begin(target, workspace: state.activeWorkspace)
        case .completeEntryEditing:
            isEntryEditing = false
        case .deleteLesson(let id):
            state.deleteLesson(id)
        case .deleteQuestion(let id):
            state.deleteBankItem(id)
        case .restoreTemplate:
            showRestorePrompt = true
        case .beginNewMeeting:
            state.beginNewMeetingAfterWorkspaceEdit()
        case .closeWindow:
            allowNextClose = true
            DispatchQueue.main.async {
                NSApp.windows.first(where: MainWindowLayout.isMainWindow)?.performClose(nil)
            }
        case .terminateApp:
            allowNextTermination = true
            DispatchQueue.main.async { NSApp.terminate(nil) }
        }
    }

    private func isEntryTarget(_ target: WorkspaceEditTarget) -> Bool {
        switch target {
        case .lesson, .question: true
        case .basic, .requirements: false
        }
    }

    private func keepsEntryMode(_ transition: Transition?) -> Bool {
        switch transition {
        case .editEntry, .deleteLesson, .deleteQuestion: true
        case .editorPage(.knowledge): true
        default: false
        }
    }
}

// MARK: - List

private struct WorkspaceList: View {
    @Environment(AppState.self) private var state
    var compact: Bool
    var editor: WorkspaceEditController
    @State private var deleting: Workspace?

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("资料夹").font(Typo.section)
                    Text("\(state.workspaces.visible.count) 个系列").font(Typo.caption).foregroundStyle(Ink.secondary)
                }
                Spacer()
                Menu {
                    ForEach(SessionMode.builtIn) { mode in
                        Button { editor.requestCreateWorkspace(mode, state: state) } label: { Label(mode.displayName, systemImage: mode.symbol) }
                    }
                    Divider()
                    Button("导入系列或备份…") { state.importData(applySettings: false) }
                } label: { Image(systemName: "plus") }
                    .menuStyle(.button).buttonStyle(GlassIconButtonStyle(size: 28)).menuIndicator(.hidden).fixedSize()
                    .help("新建系列")
            }
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 3) {
                    if state.workspaces.visible.isEmpty {
                        Text("还没有系列")
                            .font(.system(size: 11))
                            .foregroundStyle(Ink.secondary)
                            .padding(.horizontal, 9)
                            .padding(.vertical, 8)
                    }
                    ForEach(SessionMode.allCases) { mode in
                        let items = state.workspaces.visible.filter { $0.mode == mode }
                        if !items.isEmpty {
                            Label(mode.displayName, systemImage: mode.symbol)
                                .overline().padding(.top, 6)
                            ForEach(items) { workspace in row(workspace) }
                        }
                    }
                }
            }
        }
        .card()
        .frame(maxHeight: .infinity, alignment: .top)
        .confirmationDialog("删除系列“\(deleting?.name ?? "")”？", isPresented: Binding(get: { deleting != nil }, set: { if !$0 { deleting = nil } }), titleVisibility: .visible) {
            Button("删除系列", role: .destructive) {
                if let deleting { editor.requestDeleteWorkspace(deleting.id, state: state) }
                deleting = nil
            }
            Button("取消", role: .cancel) { deleting = nil }
        } message: {
            Text("系列及其会议记录将被永久删除，无法恢复。")
        }
    }

    private func row(_ workspace: Workspace) -> some View {
        let active = workspace.id == state.activeWorkspace.id
        let sessions = state.archive.sessions.filter { $0.workspaceID == workspace.id }.count
        return Button { editor.requestWorkspace(workspace.id, state: state) } label: {
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Text(workspace.name).font(.system(size: compact ? 12.5 : 13, weight: active ? .semibold : .regular)).lineLimit(1)
                    Spacer(minLength: 0)
                    if active { Image(systemName: "checkmark.circle.fill").foregroundStyle(Brand.accent) }
                }
                Text("\(workspace.documents.count) 份资料 · \(workspace.questionBank.count) 题 · \(sessions) 场会议")
                    .font(.system(size: compact ? 10 : 10.5)).foregroundStyle(.secondary).lineLimit(1).minimumScaleFactor(0.85)
            }
            .padding(.horizontal, 9).padding(.vertical, 7)
            .background(active ? Brand.accent.opacity(0.14) : Color.clear, in: RoundedRectangle(cornerRadius: 9, style: .continuous))
            .contentShape(Rectangle())
        }
        .buttonStyle(InteractiveRowButtonStyle())
        .accessibilityLabel("\(workspace.name)，\(workspace.documents.count) 份资料，\(workspace.questionBank.count) 道题，\(sessions) 场会议")
        .accessibilityHint(active ? "当前系列" : "切换到这个系列")
        .accessibilityAddTraits(active ? .isSelected : [])
        .accessibilityIdentifier("library.series.\(workspace.id.uuidString)")
        .contextMenu {
            Button("使用这个系列") { editor.requestWorkspace(workspace.id, state: state) }
            Button("复制一份") { editor.requestDuplicateWorkspace(workspace.id, state: state) }
            Button("导出系列（含资料与题库）…") { state.exportWorkspace(workspace.id, includeSessions: false) }
            Button("导出系列与会议记录…") { state.exportWorkspace(workspace.id, includeSessions: true) }
            Divider()
            Button("删除…", role: .destructive) { deleting = workspace }
                .disabled(state.workspaceDeletionDisabledReason(workspace.id) != nil)
                .help(state.workspaceDeletionDisabledReason(workspace.id) ?? "永久删除系列和会议记录")
        }
    }
}

// MARK: - Editor

enum WorkspaceEditorSection: String, CaseIterable, Identifiable {
    case basic, requirements, documents, skills, knowledge, review

    var id: String { rawValue }

    var title: String {
        switch self {
        case .basic: "基本信息"
        case .requirements: "回答要求"
        case .documents: "资料"
        case .skills: "技能"
        case .knowledge: "经验库与题库"
        case .review: "复盘"
        }
    }

}

private struct WorkspaceEditor: View {
    @Environment(AppState.self) private var state
    var editor: WorkspaceEditController
    @AppStorage("workspaceEditorFocusSection", store: UserDefaults.standard) private var sectionRaw = WorkspaceEditorSection.basic.rawValue
    @Namespace private var sectionIndicator

    var body: some View {
        @Bindable var state = state
        let section = WorkspaceEditorSection(rawValue: sectionRaw) ?? .basic
        let active = state.activeWorkspace
        let shownWorkspace = editor.draft.flatMap { $0.id == active.id ? $0 : nil } ?? active
        VStack(alignment: .leading, spacing: 12) {
            ViewThatFits(in: .horizontal) {
                HStack(spacing: 10) {
                    workspaceName(shownWorkspace)
                    Spacer(minLength: 8)
                    editorActions
                }
                VStack(alignment: .leading, spacing: 8) {
                    workspaceName(shownWorkspace)
                    editorActions
                }
            }
            ScrollView(.horizontal) {
                HStack(spacing: 2) {
                    ForEach(WorkspaceEditorSection.allCases) { item in
                        let selected = item == section
                        Button {
                            editor.requestEditorPage(item, state: state)
                        } label: {
                            HStack(spacing: 5) {
                                Text(LocalizedStringKey(item.title)).lineLimit(1)
                                if let count = sectionCount(item) {
                                    Text(count).font(.system(size: 10)).foregroundStyle(Ink.tertiary)
                                }
                            }
                            .font(.system(size: 11, weight: selected ? .semibold : .regular))
                            .foregroundStyle(selected ? Ink.ink : Ink.secondary)
                            .padding(.horizontal, 7).padding(.vertical, 10)
                            .overlay(alignment: .bottom) {
                                if selected {
                                    Capsule()
                                        .fill(Ink.ink)
                                        .frame(height: 2)
                                        .matchedGeometryEffect(id: "selected-section", in: sectionIndicator)
                                }
                            }
                            .contentShape(Rectangle())
                        }
                        .buttonStyle(.plain)
                        .animation(Motion.hover, value: selected)
                        .accessibilityAddTraits(selected ? .isSelected : [])
                    }
                }
            }
            .scrollIndicators(.hidden)
            .background(alignment: .bottom) {
                Rectangle().fill(Ink.hairline).frame(height: 1)
            }

            if editor.draft?.id == active.id, let target = editor.target {
                switch target {
                case .basic, .requirements: editActionsBar(target)
                case .lesson, .question: entryEditActionsBar(target)
                }
            }

            ScrollView {
                VStack(alignment: .leading, spacing: 14) {
                    sectionContent(section)
                        .id(section)
                        .transition(.opacity)
                }
                .frame(maxWidth: .infinity, alignment: .topLeading)
                .padding(.bottom, 8)
                .animation(Motion.phase, value: section)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        }
        .card()
        .frame(maxHeight: .infinity, alignment: .top)
    }

    @ViewBuilder
    private func sectionContent(_ section: WorkspaceEditorSection) -> some View {
        switch section {
        case .basic: WorkspaceBasicTab(editor: editor)
        case .requirements: WorkspaceRequirementsTab(editor: editor).frame(maxWidth: 640, alignment: .leading)
        case .documents: WorkspaceDocumentsTab()
        case .skills: WorkspaceSkillsSection()
        case .knowledge: KnowledgeTab(editor: editor).frame(maxWidth: 640, alignment: .leading)
        case .review: ReviewTab()
        }
    }

    private func sectionCount(_ section: WorkspaceEditorSection) -> String? {
        switch section {
        case .basic, .requirements, .skills, .knowledge: nil
        case .documents: "\(state.documents.count)"
        case .review: "\(state.archive.sessions.filter { $0.workspaceID == state.activeWorkspace.id }.count)"
        }
    }
}

private extension WorkspaceEditor {
    func workspaceName(_ workspace: Workspace) -> some View {
        HStack(spacing: 10) {
            Image(systemName: workspace.mode.symbol)
                .font(.system(size: 15, weight: .regular)).foregroundStyle(Ink.secondary)
                .frame(width: 24, height: 24)
            Text(workspace.name)
                .font(Typo.section).lineLimit(1).truncationMode(.tail)
                .frame(maxWidth: .infinity, alignment: .leading)
                .accessibilityLabel("系列名称：\(workspace.name)")
        }
    }

    var editorActions: some View {
        HStack(spacing: 8) {
            MenuPicker(selection: Binding(get: { state.sessionMode }, set: { editor.requestMode($0, state: state) }),
                       options: typeOptions)
            MenuPicker(selection: Binding(get: { state.answerStyle }, set: { state.answerStyle = $0 }),
                       options: AnswerStyle.allCases.map { ($0, $0.title) }, localizesOptions: true)
            Button { state.predictQuestions() } label: { Label("预测问题", systemImage: "wand.and.stars") }
                .buttonStyle(ChipButtonStyle()).disabled(state.isAnswering)
                .help("根据资料预测最可能被问到的问题，并给出要点")
        }
    }

    func editActionsBar(_ target: WorkspaceEditTarget) -> some View {
        HStack(spacing: 8) {
            Label(target == .basic ? "正在编辑系列信息" : "正在编辑回答要求", systemImage: "pencil")
                .font(.system(size: 12, weight: .medium))
                .foregroundStyle(Ink.secondary)
            Spacer()
            Button("保存") { editor.save(state) }
                .buttonStyle(ChipButtonStyle(prominent: true))
                .keyboardShortcut(.defaultAction)
            Button("取消") { editor.cancelDraft() }
                .buttonStyle(ChipButtonStyle())
                .keyboardShortcut(.cancelAction)
        }
        .padding(.horizontal, 10).padding(.vertical, 7)
        .background(Color.primary.opacity(0.035), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }

    func entryEditActionsBar(_ target: WorkspaceEditTarget) -> some View {
        let title: String
        switch target {
        case .lesson: title = "正在编辑这条经验"
        case .question: title = "正在编辑这道题"
        case .basic, .requirements: title = "正在编辑条目"
        }
        return HStack(spacing: 8) {
            Label(title, systemImage: "pencil")
                .font(.system(size: 12, weight: .medium)).foregroundStyle(Ink.secondary)
            Spacer()
            Button("保存") { editor.save(state) }
                .buttonStyle(ChipButtonStyle(prominent: true)).keyboardShortcut(.defaultAction)
            Button("取消") { editor.cancelDraft() }
                .buttonStyle(ChipButtonStyle()).keyboardShortcut(.cancelAction)
        }
        .padding(.horizontal, 10).padding(.vertical, 7)
        .background(Color.primary.opacity(0.035), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }

    /// Built-in types, plus the custom type when this series uses one.
    var typeOptions: [(SessionMode, String)] {
        var options = SessionMode.builtIn.map { ($0, $0.displayName) }
        if state.activeWorkspace.mode == .custom {
            options.append((.custom, state.activeWorkspace.typeDisplayName))
        }
        return options
    }
}

private struct FieldEditor: View {
    let field: ModeField
    @Binding var text: String
    var isEditing = false
    var showsLabel = true
    var controlHeight: CGFloat? = nil

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            if showsLabel { Text(LocalizedStringKey(field.title)).font(.system(size: 12, weight: .semibold)) }
            if isEditing, field.long {
                WorkspaceTextArea(text: $text, title: field.title, placeholder: field.placeholder,
                                  minHeight: controlHeight ?? 104, maxHeight: controlHeight ?? 190)
            } else if isEditing {
                WorkspaceSingleLineField(text: $text, title: field.title, placeholder: field.placeholder,
                                          minimumHeight: controlHeight ?? 38)
            } else {
                if text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    Text("尚未填写，可在编辑系列信息时补充")
                        .foregroundStyle(Ink.tertiary)
                        .frame(maxWidth: .infinity, minHeight: field.long ? 54 : 28, alignment: .topLeading)
                } else {
                    Text(text)
                        .foregroundStyle(Ink.ink)
                        .fixedSize(horizontal: false, vertical: true)
                        .frame(maxWidth: .infinity, minHeight: field.long ? 54 : 28, alignment: .topLeading)
                        .textSelection(.enabled)
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

private struct WorkspaceSingleLineField: View {
    @Binding var text: String
    let title: String
    let placeholder: String
    var minimumHeight: CGFloat = 38
    var onSubmit: () -> Void = {}
    @FocusState private var isFocused: Bool

    var body: some View {
        TextField(placeholder, text: $text, prompt: Text(LocalizedStringKey(placeholder)).foregroundColor(Ink.secondary))
            .textFieldStyle(.plain)
            .font(.system(size: 13))
            .padding(.horizontal, 10).padding(.vertical, 8)
            .frame(maxWidth: .infinity, minHeight: minimumHeight, alignment: .topLeading)
            .background(Color.primary.opacity(0.035), in: RoundedRectangle(cornerRadius: 9, style: .continuous))
            .overlay {
                RoundedRectangle(cornerRadius: 9, style: .continuous)
                    .strokeBorder(isFocused ? Ink.focus : Color.primary.opacity(0.16), lineWidth: isFocused ? 1.5 : 1)
            }
            .focused($isFocused)
            .onSubmit(onSubmit)
            .accessibilityLabel(Text(LocalizedStringKey(title)))
            .accessibilityHint(Text(LocalizedStringKey(placeholder)))
    }
}

/// Uses the same responsive column width for the series name and its core fields.
private struct WorkspaceAdaptiveColumns: Layout {
    var columnSpacing: CGFloat = 14
    var rowSpacing: CGFloat = 12
    var minimumColumnWidth: CGFloat = 340
    var maximumColumnWidth: CGFloat = 420

    private var twoColumnThreshold: CGFloat { minimumColumnWidth * 2 + columnSpacing }

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        guard !subviews.isEmpty else { return .zero }
        let width = proposal.width.flatMap { $0.isFinite ? max(0, $0) : nil } ?? preferredWidth(for: subviews)
        let usesColumns = width >= twoColumnThreshold
        let columnWidth = usesColumns
            ? min(maximumColumnWidth, max(0, (width - columnSpacing) / 2))
            : width
        let sizes = subviews.map { $0.sizeThatFits(ProposedViewSize(width: columnWidth, height: nil)) }
        let height: CGFloat
        if usesColumns {
            height = sizes.map(\.height).max() ?? 0
        } else {
            height = sizes.reduce(0) { $0 + $1.height } + rowSpacing * CGFloat(max(0, sizes.count - 1))
        }
        return CGSize(width: width, height: height)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        guard !subviews.isEmpty else { return }
        let usesColumns = bounds.width >= twoColumnThreshold
        let columnWidth = usesColumns
            ? min(maximumColumnWidth, max(0, (bounds.width - columnSpacing) / 2))
            : bounds.width

        if usesColumns {
            for (index, subview) in subviews.enumerated() {
                subview.place(
                    at: CGPoint(x: bounds.minX + CGFloat(index) * (columnWidth + columnSpacing), y: bounds.minY),
                    proposal: ProposedViewSize(width: columnWidth, height: nil)
                )
            }
        } else {
            var y = bounds.minY
            for subview in subviews {
                let size = subview.sizeThatFits(ProposedViewSize(width: columnWidth, height: nil))
                subview.place(at: CGPoint(x: bounds.minX, y: y), proposal: ProposedViewSize(width: columnWidth, height: nil))
                y += size.height + rowSpacing
            }
        }
    }

    private func preferredWidth(for subviews: Subviews) -> CGFloat {
        guard subviews.count > 1 else { return maximumColumnWidth * 2 + columnSpacing }
        let idealWidths = subviews.prefix(2).map { $0.sizeThatFits(.unspecified).width }
        return max(twoColumnThreshold, min(maximumColumnWidth * 2 + columnSpacing,
                                           idealWidths.reduce(0, +) + columnSpacing))
    }
}

private struct WorkspaceTextArea: View {
    @Binding var text: String
    let title: String
    var placeholder: String? = nil
    var minHeight: CGFloat = 100
    var maxHeight: CGFloat = 210
    @FocusState private var isFocused: Bool

    var body: some View {
        ZStack(alignment: .topLeading) {
            if text.isEmpty, let placeholder {
                Text(LocalizedStringKey(placeholder))
                    .font(.system(size: 13))
                    .foregroundStyle(Ink.secondary)
                    .padding(.horizontal, 12).padding(.vertical, 10)
                    .allowsHitTesting(false)
            }
            TextEditor(text: $text)
                .font(.system(size: 13))
                .scrollContentBackground(.hidden)
                .padding(5)
                .focused($isFocused)
        }
        .frame(maxWidth: .infinity, minHeight: minHeight, maxHeight: maxHeight)
        .background(Color.primary.opacity(0.035), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
        .overlay {
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .strokeBorder(isFocused ? Ink.focus : Color.primary.opacity(0.16), lineWidth: isFocused ? 1.5 : 1)
        }
        .accessibilityLabel(Text(LocalizedStringKey(title)))
        .accessibilityHint(Text(LocalizedStringKey(placeholder ?? "多行文字")))
    }
}

/// Keeps section titles on the same text and icon baselines throughout the workspace editor.
private struct WorkspaceSectionHeading: View {
    let title: LocalizedStringKey
    let icon: String

    init(_ title: LocalizedStringKey, icon: String) {
        self.title = title
        self.icon = icon
    }

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 8) {
            Image(systemName: icon)
                .foregroundStyle(Ink.secondary)
                .frame(width: 20, alignment: .center)
                .accessibilityHidden(true)
            Text(title)
        }
        .font(.system(size: 13, weight: .semibold))
        .frame(minHeight: 18, alignment: .leading)
    }
}

/// Skills this scenario uses: global ones always, others ticked here.
private struct WorkspaceSkillsSection: View {
    @Environment(AppState.self) private var state
    private var library: SkillLibrary { SkillLibrary.shared }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                WorkspaceSectionHeading("技能 \(state.activeSkillSnippets.count)", icon: "wand.and.stars")
                Spacer()
                Button { let added = library.importFiles(global: false); for skill in added { state.setSkill(skill.id, attached: true) } } label: {
                    Label("导入并用于此系列…", systemImage: "square.and.arrow.down")
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
                            if skill.global { Text("所有系列").font(.system(size: 10, weight: .semibold)).foregroundStyle(Brand.accent) }
                            if !skill.enabled { Text("已停用").font(.system(size: 10)).foregroundStyle(.secondary) }
                            if !skill.summary.isEmpty { Text(skill.summary).font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(1) }
                        }
                    }
                    .toggleStyle(.wide)
                    .disabled(skill.global)
                }
            }
        }
    }
}

private struct WorkspaceBasicTab: View {
    @Environment(AppState.self) private var state
    var editor: WorkspaceEditController
    @State private var showsAdditionalInfo = false
    @State private var showsStructuredFields = false

    var body: some View {
        let workspace = displayedWorkspace
        let profile = ModeProfile.for(workspace.effectiveMode)
        let modeDisplayName = workspace.mode == .custom ? workspace.typeDisplayName : workspace.mode.displayName
        let coreFields = Array(profile.fields.prefix(2))
        let additionalFields = Array(profile.fields.dropFirst(2))
        let filledAdditionalCount = additionalFields.filter {
            !(workspace.fields[$0.id] ?? "").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        }.count
        let hasBackground = !workspace.notes.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty

        VStack(alignment: .leading, spacing: 14) {
            ViewThatFits(in: .horizontal) {
                HStack(alignment: .top, spacing: 12) {
                    sectionTitle(modeDisplayName)
                    Spacer(minLength: 8)
                    editButton
                }
                VStack(alignment: .leading, spacing: 8) {
                    sectionTitle(modeDisplayName)
                    editButton
                }
            }

            if isEditing {
                WorkspaceAdaptiveColumns {
                    WorkspaceSingleLineField(text: nameBinding, title: "系列名称", placeholder: "系列名称")
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
            }

            WorkspaceAdaptiveColumns {
                ForEach(coreFields) { field in
                    fieldEditor(field, editing: isEditing,
                                controlHeight: coreFields.contains(where: \.long) ? 104 : 38)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
            }

            DisclosureGroup(isExpanded: $showsAdditionalInfo) {
                VStack(alignment: .leading, spacing: 12) {
                    FieldEditor(field: ModeField("notes", "其他背景", "任何希望回答时参考的事实：经历、数字、对方偏好…", long: true),
                                text: notesBinding, isEditing: isEditing, showsLabel: false)
                        .frame(maxWidth: 760, alignment: .leading)

                    if !additionalFields.isEmpty {
                        DisclosureGroup(isExpanded: $showsStructuredFields) {
                            VStack(alignment: .leading, spacing: 12) {
                                ForEach(additionalFields) { field in
                                    fieldEditor(field, editing: isEditing)
                                        .frame(maxWidth: 760, alignment: .leading)
                                }
                            }
                            .padding(.top, 8)
                        } label: {
                            HStack(spacing: 7) {
                                Text("更多信息")
                                    .font(.system(size: 12, weight: .semibold))
                                Text("\(filledAdditionalCount)/\(additionalFields.count) 项已填写")
                                    .font(.system(size: 11)).foregroundStyle(.secondary)
                            }
                        }
                        .padding(.top, 2)
                    }
                }
                .padding(.top, 8)
            } label: {
                HStack(spacing: 7) {
                    Text("其他背景")
                        .font(.system(size: 12, weight: .semibold))
                    Text(hasBackground ? "已填写" : "可选")
                        .font(.system(size: 11)).foregroundStyle(.secondary)
                }
            }
            .tint(Ink.secondary)
        }
    }

    private var isEditing: Bool {
        editor.isEditing(.basic) && editor.draft?.id == state.activeWorkspace.id
    }

    private var displayedWorkspace: Workspace {
        if isEditing, let draft = editor.draft { return draft }
        return state.activeWorkspace
    }

    private var nameBinding: Binding<String> {
        Binding(get: { displayedWorkspace.name }, set: { value in
            guard var draft = editor.draft, draft.id == state.activeWorkspace.id else { return }
            draft.name = value
            editor.updateDraft(draft)
        })
    }

    private var notesBinding: Binding<String> {
        Binding(get: { displayedWorkspace.notes }, set: { value in
            guard var draft = editor.draft, draft.id == state.activeWorkspace.id else { return }
            draft.notes = value
            editor.updateDraft(draft)
        })
    }

    private func fieldBinding(_ id: String) -> Binding<String> {
        Binding(get: { displayedWorkspace.fields[id] ?? "" }, set: { value in
            guard var draft = editor.draft, draft.id == state.activeWorkspace.id else { return }
            draft.fields[id] = value
            editor.updateDraft(draft)
        })
    }

    private func sectionTitle(_ modeDisplayName: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("\(modeDisplayName) 信息").font(.system(size: 15, weight: .semibold))
            Text("先填写核心信息，其他内容可以稍后补充。")
                .font(.system(size: 12)).foregroundStyle(.secondary)
        }
    }

    @ViewBuilder
    private var editButton: some View {
        if isEditing {
            Label("正在编辑", systemImage: "pencil")
                .font(.system(size: 12, weight: .medium)).foregroundStyle(Ink.secondary)
        } else {
            Button("编辑系列信息", systemImage: "pencil") {
                editor.begin(.basic, workspace: state.activeWorkspace)
            }
            .buttonStyle(ChipButtonStyle())
            .accessibilityHint("编辑系列名称、背景和此类型的基本信息")
        }
    }

    private func fieldEditor(_ field: ModeField, editing: Bool, controlHeight: CGFloat? = nil) -> some View {
        FieldEditor(field: field, text: fieldBinding(field.id), isEditing: editing,
                    controlHeight: controlHeight)
    }
}

private struct WorkspaceRequirementsTab: View {
    @Environment(AppState.self) private var state
    var editor: WorkspaceEditController

    var body: some View {
        let modeDisplayName = state.sessionMode == .custom ? state.activeWorkspace.typeDisplayName : state.sessionMode.displayName
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                WorkspaceSectionHeading("回答要求", icon: "text.badge.checkmark")
                Spacer()
                if isEditing {
                    Label("正在编辑", systemImage: "pencil").font(.caption).foregroundStyle(Ink.secondary)
                } else {
                    Button("编辑回答要求", systemImage: "pencil") {
                        editor.begin(.requirements, workspace: state.activeWorkspace)
                    }
                    .buttonStyle(ChipButtonStyle())
                    .accessibilityHint("编辑此系列的回答要求")
                }
                Button("恢复 \(modeDisplayName) 模板") { editor.requestRestoreTemplate(state: state) }
                    .buttonStyle(ChipButtonStyle())
            }
            if isEditing {
                WorkspaceTextArea(text: preferencesBinding, title: "回答要求", placeholder: "例如：先给结论，再补充依据和例子。",
                                   minHeight: 150, maxHeight: 280)
                Text("用什么语言、多长、先说什么；与资料正文分开处理，文件内容不能改写这里的要求。").font(.caption).foregroundStyle(.secondary)
            } else {
                let value = state.activeWorkspace.preferences.trimmingCharacters(in: .whitespacesAndNewlines)
                if value.isEmpty {
                    Text("尚未填写回答要求，点击编辑以添加。")
                        .font(.system(size: 13)).foregroundStyle(Ink.tertiary)
                        .frame(maxWidth: .infinity, minHeight: 70, alignment: .topLeading)
                } else {
                    Text(value)
                        .font(.system(size: 13)).foregroundStyle(Ink.ink)
                        .fixedSize(horizontal: false, vertical: true)
                        .frame(maxWidth: .infinity, minHeight: 70, alignment: .topLeading)
                        .textSelection(.enabled)
                }
            }
        }
    }

    private var isEditing: Bool {
        editor.isEditing(.requirements) && editor.draft?.id == state.activeWorkspace.id
    }

    private var preferencesBinding: Binding<String> {
        Binding(get: { editor.draft?.preferences ?? state.activeWorkspace.preferences }, set: { value in
            guard var draft = editor.draft, draft.id == state.activeWorkspace.id else { return }
            draft.preferences = value
            editor.updateDraft(draft)
        })
    }
}

private struct WorkspaceDocumentsTab: View {
    @Environment(AppState.self) private var state
    @State private var importingCategory: String?
    @State private var isFileImporterPresented = false
    @State private var dropTargeted = false

    private var allowedTypes: [UTType] { DocumentImporter.supportedExtensions.compactMap { UTType(filenameExtension: $0) } }

    var body: some View {
        let profile = ModeProfile.for(state.sessionMode)
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                WorkspaceSectionHeading("资料 \(state.documents.count)", icon: "doc.on.doc")
                Spacer()
                if state.isImportingDocuments || state.isIndexingDocuments { ProgressView().controlSize(.small) }
                Menu {
                    Button("自动归类") { presentImporter(category: nil) }
                    Divider()
                    ForEach(categories(profile), id: \.self) { category in
                        Button("导入到 \(localizedCategory(category))") { presentImporter(category: category) }
                    }
                } label: {
                    Label(LocalizedStringKey("导入资料"), systemImage: "square.and.arrow.down")
                }
                .buttonStyle(ChipButtonStyle(prominent: true))
                .help("一次可以选多个文件或整个文件夹：系统转成 Markdown、在后台归档，并按内容自动归类")
            }
            if !state.importStatus.isEmpty {
                Text(state.importStatus).font(.caption).foregroundStyle(.secondary)
            }
            PreparationQueueSummary()
            ForEach(categories(profile), id: \.self) { category in
                categorySection(category)
            }
            Text("支持 PDF（含扫描件 OCR）、Word、PowerPoint、Excel、Markdown、TXT、RTF、HTML 与图片。可以一次拖入多个文件甚至整个文件夹：系统会把它们转成结构化 Markdown、在后台静默归档，并按内容自动归类。回答时只把与问题相关的片段交给模型，并标注来源。")
                .font(.caption).foregroundStyle(.secondary)
        }
        .padding(8)
        .background(RoundedRectangle(cornerRadius: 12).strokeBorder(style: StrokeStyle(lineWidth: 1.2, dash: [6])).foregroundStyle(dropTargeted ? Color.accentColor : Color.clear))
        .onDrop(of: [.fileURL], isTargeted: $dropTargeted) { providers in loadDropped(providers); return true }
        .fileImporter(isPresented: $isFileImporterPresented, allowedContentTypes: allowedTypes, allowsMultipleSelection: true) { result in
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
                        Text(item.citation).font(.system(size: 12, weight: .semibold))
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

    private func presentImporter(category: String?) {
        importingCategory = category
        isFileImporterPresented = true
    }

    private func localizedCategory(_ category: String) -> String {
        String(localized: String.LocalizationValue(category))
    }

    private func categorySection(_ category: String) -> some View {
        let items = state.documents.filter { ($0.category ?? "其他") == category }
        return VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text(localizedCategory(category)).font(.system(size: 12, weight: .semibold))
                Text("\(items.count)").font(.caption.monospacedDigit()).foregroundStyle(.tertiary)
                Spacer()
            }
            ForEach(items) { document in
                HStack(spacing: 8) {
                    Toggle("用于回答", isOn: Binding(get: { state.documents.first(where: { $0.id == document.id })?.enabled ?? false },
                                                       set: { state.setDocumentEnabled(document.id, enabled: $0) }))
                        .toggleStyle(.inline)
                        .help("开启后，回答时会参考这份资料。")
                    Image(systemName: icon(for: document.name)).foregroundStyle(Color.accentColor)
                    VStack(alignment: .leading, spacing: 1) {
                        Text(document.name).font(.system(size: 12.5, weight: .medium)).lineLimit(1)
                        Text("\(document.pages.count) 块 · \(document.characterCount) 字符").font(.caption2).foregroundStyle(.secondary)
                    }
                    if let content = document.content, !content.warnings.isEmpty {
                        Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.orange)
                            .help(content.warnings.joined(separator: "\n"))
                    }
                    if let format = document.sourceFormat {
                        Text(format.uppercased())
                            .font(.system(size: 9, weight: .bold)).foregroundStyle(.secondary)
                            .padding(.horizontal, 4).padding(.vertical, 1)
                            .background(Color.primary.opacity(0.08), in: RoundedRectangle(cornerRadius: 4))
                            .help("已转成 Markdown 归档")
                    }
                    Spacer()
                    if let path = document.markdownPath, let archived = MaterialArchive.resolve(path), FileManager.default.fileExists(atPath: archived.path) {
                        Button { NSWorkspace.shared.open(archived) } label: { Image(systemName: "doc.richtext") }
                            .buttonStyle(.tap).accessibilityLabel("打开归档 Markdown").help("打开归档的 Markdown")
                    }
                    Menu {
                        ForEach(categories(ModeProfile.for(state.sessionMode)), id: \.self) { target in
                            Button {
                                state.setDocumentCategory(document.id, category: target)
                            } label: {
                                if target == (document.category ?? "其他") {
                                    Label(localizedCategory(target), systemImage: "checkmark")
                                } else {
                                    Text(localizedCategory(target))
                                }
                            }
                        }
                    } label: { Label("移动", systemImage: "folder") }
                        .menuStyle(.button).buttonStyle(TapButtonStyle(minWidth: Hit.minHeight, minHeight: Hit.minHeight)).menuIndicator(.hidden).fixedSize()
                        .accessibilityLabel("移动资料分类")
                        .help("选择这份资料所属的分类")
                    Button(role: .destructive) { state.removeDocument(document.id) } label: {
                        Label("移除", systemImage: "trash")
                    }
                    .buttonStyle(TapButtonStyle(minWidth: Hit.minHeight, minHeight: Hit.minHeight)).foregroundStyle(.red).help("从当前系列移除这份资料")
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
                guard let url else { continue }
                // A dropped folder is walked, so a whole set of materials can be
                // dropped in at once.
                urls.append(contentsOf: DocumentImporter.expand(url))
            }
            if urls.isEmpty {
                state.errorMessage = "拖入的文件类型不受支持。"
            } else {
                // No category: the files are filed automatically.
                state.importDocuments(urls)
            }
        }
    }
}

// MARK: - Knowledge

private struct KnowledgeTab: View {
    @Environment(AppState.self) private var state
    var editor: WorkspaceEditController
    @State private var newLesson = ""
    @State private var newQuestion = ""
    @State private var newAnswer = ""
    @State private var search = ""
    @State private var lessonToDelete: UUID?

    var body: some View {
        let workspace = state.activeWorkspace
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                WorkspaceSectionHeading("经验库与题库", icon: "text.book.closed")
                Spacer()
                if editor.isEntryEditing {
                    Button("完成") { editor.requestCompleteEntryEditing(state: state) }
                        .buttonStyle(ChipButtonStyle(prominent: true))
                        .accessibilityHint("退出经验和题目的编辑模式")
                } else {
                    Button("编辑条目", systemImage: "pencil") {
                        editor.isEntryEditing = true
                    }
                    .buttonStyle(ChipButtonStyle())
                    .disabled(workspace.lessons.isEmpty && workspace.questionBank.isEmpty)
                    .accessibilityHint(workspace.lessons.isEmpty && workspace.questionBank.isEmpty
                                       ? "先新增经验或题目后即可编辑"
                                       : "进入编辑模式后选择一条经验或题目")
                }
            }
            if workspace.lessons.isEmpty && workspace.questionBank.isEmpty {
                Text("目前没有可编辑的经验或题目；先新增条目后即可进入编辑模式。")
                    .font(.caption).foregroundStyle(.secondary)
            } else if editor.isEntryEditing {
                Label("编辑模式：选择一条经验或题目打开编辑器；一次只编辑一条。", systemImage: "pencil")
                    .font(.caption).foregroundStyle(Ink.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Toggle("回答时参考这里的经验和题库（自我迭代）", isOn: Binding(get: { state.activeWorkspace.useKnowledge },
                                                              set: { state.activeWorkspace.useKnowledge = $0; state.rebuildContextIndex() }))
            Text("每次会话结束生成摘要后，AI 复盘会把“经验沉淀”和对方问到的问题（附改进后的回答）加入这里；下次回答同类问题时优先采用。可以随时编辑、置顶或删除。")
                .font(.caption).foregroundStyle(.secondary)
        }
        Divider()
        VStack(alignment: .leading, spacing: 8) {
            WorkspaceSectionHeading("经验库 \(workspace.lessons.count)", icon: "lightbulb")
            ForEach(workspace.lessons.sorted { ($0.pinned ? 1 : 0, $0.createdAt) > ($1.pinned ? 1 : 0, $1.createdAt) }) { lesson in
                ExperienceLessonRow(lesson: lesson, editor: editor) { lessonToDelete = lesson.id }
            }
            HStack(alignment: .center, spacing: 8) {
                WorkspaceSingleLineField(text: $newLesson, title: "添加经验", placeholder: "例如：被问到项目时先报规模和数字") {
                    state.addLesson(newLesson); newLesson = ""
                }
                Button("添加") { state.addLesson(newLesson); newLesson = "" }.buttonStyle(ChipButtonStyle(prominent: true)).disabled(newLesson.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                    .help(newLesson.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? "请先填写经验" : "添加经验")
            }
        }
        .confirmationDialog("删除这条经验？", isPresented: Binding(
            get: { lessonToDelete != nil }, set: { if !$0 { lessonToDelete = nil } }), titleVisibility: .visible) {
            Button("删除经验", role: .destructive) {
                if let id = lessonToDelete { editor.requestDeleteLesson(id, state: state) }
                lessonToDelete = nil
            }
            Button("取消", role: .cancel) { lessonToDelete = nil }
        } message: {
            Text("这条经验会从当前系列的经验库中移除。")
        }
        Divider()
        VStack(alignment: .leading, spacing: 8) {
            ViewThatFits(in: .horizontal) {
                HStack {
                    WorkspaceSectionHeading("题库 \(workspace.questionBank.count)", icon: "questionmark.folder")
                    Spacer(minLength: 8)
                    WorkspaceSingleLineField(text: $search, title: "搜索题库", placeholder: "搜索题库")
                        .frame(minWidth: 150, maxWidth: 240)
                }
                VStack(alignment: .leading, spacing: 8) {
                    WorkspaceSectionHeading("题库 \(workspace.questionBank.count)", icon: "questionmark.folder")
                    WorkspaceSingleLineField(text: $search, title: "搜索题库", placeholder: "搜索题库")
                }
            }
            if !search.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && bank(workspace).isEmpty {
                Text("没有匹配的问题或回答。").font(.caption).foregroundStyle(.secondary)
            }
            ForEach(bank(workspace)) { item in BankRow(item: item, editor: editor) }
            VStack(alignment: .leading, spacing: 6) {
                WorkspaceTextArea(text: $newQuestion, title: "新问题", placeholder: "新问题（例如背过的八股题）", minHeight: 68, maxHeight: 110)
                WorkspaceTextArea(text: $newAnswer, title: "准备好的回答", placeholder: "准备好的回答", minHeight: 104, maxHeight: 180)
                HStack {
                    Spacer()
                    Button("加入题库") { state.addBankItem(question: newQuestion, answer: newAnswer); newQuestion = ""; newAnswer = "" }
                        .buttonStyle(ChipButtonStyle(prominent: true)).disabled(newQuestion.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || newAnswer.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                        .help(newQuestion.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || newAnswer.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? "请先填写问题和回答" : "保存到题库")
                }
            }
        }
    }

    private func bank(_ workspace: Workspace) -> [QuestionBankItem] {
        let query = search.trimmingCharacters(in: .whitespaces)
        var items = workspace.questionBank.sorted { ($0.timesAsked, $0.updatedAt) > ($1.timesAsked, $1.updatedAt) }
        if !query.isEmpty {
            items = items.filter { $0.question.localizedCaseInsensitiveContains(query) || $0.answer.localizedCaseInsensitiveContains(query) }
        }
        if editor.draft?.id == workspace.id, case .question(let selectedID) = editor.target,
           let selected = workspace.questionBank.first(where: { $0.id == selectedID }),
           !items.contains(where: { $0.id == selectedID }) {
            items.insert(selected, at: 0)
        }
        return items
    }
}

private struct ExperienceLessonRow: View {
    @Environment(AppState.self) private var state
    let lesson: WorkspaceLesson
    let editor: WorkspaceEditController
    let onDelete: () -> Void

    var body: some View {
        let target = WorkspaceEditTarget.lesson(lesson.id)
        let isSelected = editor.isEditing(target) && editor.draft?.id == state.activeWorkspace.id
        HStack(alignment: .top, spacing: 8) {
            Button {
                var next = lesson
                next.pinned.toggle()
                state.updateLesson(next)
            } label: {
                Image(systemName: lesson.pinned ? "pin.fill" : "pin")
                    .foregroundStyle(lesson.pinned ? Brand.accent : .secondary)
                    .frame(width: 18, height: 24)
            }
            .buttonStyle(.tap)
            .help(lesson.pinned ? "取消置顶" : "置顶：总是交给模型")
            .accessibilityLabel(lesson.pinned ? "取消置顶这条经验" : "置顶这条经验")

            VStack(alignment: .leading, spacing: 7) {
                if isSelected {
                    WorkspaceTextArea(text: textBinding, title: "经验内容", placeholder: "填写经验", minHeight: 70, maxHeight: 120)
                } else if editor.isEntryEditing {
                    Button { editor.requestEntryEdit(target, state: state) } label: {
                        lessonText
                            .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                    .accessibilityLabel("选择经验编辑：\(lesson.text.isEmpty ? "未填写经验" : lesson.text)")
                    .accessibilityHint("打开这条经验的编辑器")
                    .help("选择这条经验进行编辑")
                } else {
                    lessonText.textSelection(.enabled)
                }
            }
            Button(action: onDelete) {
                Image(systemName: "xmark").frame(width: 22, height: 24)
            }
            .buttonStyle(.tap).foregroundStyle(.secondary)
            .help("删除这条经验")
            .accessibilityLabel("删除这条经验")
        }
        .padding(8)
        .background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }

    private var lessonText: some View {
        Group {
            if lesson.text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                Text("尚未填写经验").foregroundStyle(Ink.tertiary)
            } else {
                Text(lesson.text).foregroundStyle(Ink.ink)
            }
        }
        .font(.system(size: 13))
        .fixedSize(horizontal: false, vertical: true)
        .frame(maxWidth: .infinity, minHeight: 36, alignment: .topLeading)
    }

    private var textBinding: Binding<String> {
        Binding(get: {
            editor.draft?.lessons.first(where: { $0.id == lesson.id })?.text ?? lesson.text
        }, set: { value in
            guard var draft = editor.draft, draft.id == state.activeWorkspace.id,
                  let index = draft.lessons.firstIndex(where: { $0.id == lesson.id }) else { return }
            draft.lessons[index].text = value
            editor.updateDraft(draft)
        })
    }

}

private struct BankRow: View {
    @Environment(AppState.self) private var state
    let item: QuestionBankItem
    let editor: WorkspaceEditController
    @State private var expanded = false
    @State private var confirmDelete = false

    var body: some View {
        let target = WorkspaceEditTarget.question(item.id)
        let isSelected = editor.isEditing(target) && editor.draft?.id == state.activeWorkspace.id
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .top) {
                Button { withAnimation(.easeOut(duration: 0.15)) { expanded.toggle() } } label: {
                    Image(systemName: expanded ? "chevron.down" : "chevron.right").foregroundStyle(.secondary)
                }.buttonStyle(.tap)
                    .accessibilityLabel(expanded ? "收起问题和回答" : "展开问题和回答")
                    .help(expanded ? "收起问题和回答" : "展开查看问题和回答")
                if editor.isEntryEditing && !isSelected {
                    Button { editor.requestEntryEdit(target, state: state) } label: {
                        questionText
                            .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                    .accessibilityLabel("选择题目编辑：\(item.question.isEmpty ? "未填写问题" : item.question)")
                    .accessibilityHint("打开这道题的编辑器")
                    .help("选择这道题进行编辑")
                } else {
                    questionText
                }
                Spacer()
                if item.timesAsked > 0 {
                    Text("被问 \(item.timesAsked) 次").font(.system(size: 10)).foregroundStyle(.secondary)
                        .padding(.horizontal, 6).padding(.vertical, 2).background(Color.primary.opacity(0.06), in: Capsule())
                }
                Button { confirmDelete = true } label: { Image(systemName: "trash") }
                    .buttonStyle(.tap).foregroundStyle(.red)
                    .help("删除这道题").accessibilityLabel("删除这道题")
            }
            if isSelected {
                WorkspaceTextArea(text: questionBinding,
                                  title: "问题", placeholder: "问题", minHeight: 68, maxHeight: 110)
                WorkspaceTextArea(text: answerBinding,
                                  title: "回答", placeholder: "回答", minHeight: 104, maxHeight: 180)
            } else {
                Group {
                    if item.answer.isEmpty {
                        Text("尚未填写回答").foregroundStyle(Ink.tertiary)
                    } else {
                        Text(item.answer).foregroundStyle(Ink.secondary).textSelection(.enabled)
                    }
                }
                .font(.system(size: 12))
                .lineLimit(expanded ? nil : 2)
                .padding(.leading, 22)
            }
            if expanded, !isSelected, let history = item.answerHistory, !history.isEmpty {
                VStack(alignment: .leading, spacing: 6) {
                    Text("旧答法")
                        .font(.system(size: 11, weight: .medium)).foregroundStyle(Ink.secondary)
                    ForEach(history.reversed()) { revision in
                        HStack(alignment: .top, spacing: 8) {
                            Text(revision.answer)
                                .font(.system(size: 12)).foregroundStyle(Ink.secondary)
                                .textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading)
                            Button("恢复") { state.restoreBankAnswer(itemID: item.id, revisionID: revision.id) }
                            .buttonStyle(.plain).font(.system(size: 11, weight: .medium))
                            .help("恢复这版答法；当前答法会留在旧答法记录中")
                        }
                    }
                }
                .padding(.leading, 22).padding(.top, 4)
            }
        }
        .padding(8).background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
        .confirmationDialog("删除这道题？", isPresented: $confirmDelete, titleVisibility: .visible) {
            Button("删除题目", role: .destructive) { editor.requestDeleteQuestion(item.id, state: state) }
            Button("取消", role: .cancel) { }
        } message: {
            Text("问题和准备好的回答会从当前系列的题库中移除。")
        }
    }

    private var questionText: some View {
        Group {
            if item.question.isEmpty {
                Text("尚未填写问题").foregroundStyle(Ink.tertiary)
            } else {
                Text(item.question).foregroundStyle(Ink.ink)
            }
        }
        .font(.system(size: 13, weight: .medium)).lineLimit(expanded ? nil : 1)
    }

    private var questionBinding: Binding<String> {
        Binding(get: {
            editor.draft?.questionBank.first(where: { $0.id == item.id })?.question ?? item.question
        }, set: { value in
            guard var draft = editor.draft, draft.id == state.activeWorkspace.id,
                  let index = draft.questionBank.firstIndex(where: { $0.id == item.id }) else { return }
            draft.questionBank[index].question = value
            editor.updateDraft(draft)
        })
    }

    private var answerBinding: Binding<String> {
        Binding(get: {
            editor.draft?.questionBank.first(where: { $0.id == item.id })?.answer ?? item.answer
        }, set: { value in
            guard var draft = editor.draft, draft.id == state.activeWorkspace.id,
                  let index = draft.questionBank.firstIndex(where: { $0.id == item.id }) else { return }
            draft.questionBank[index].answer = value
            editor.updateDraft(draft)
        })
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
                WorkspaceSectionHeading("阶段复盘", icon: "chart.line.uptrend.xyaxis")
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
            if summarized.isEmpty {
                Text("需要至少一次已生成摘要的会话，才能生成阶段复盘。")
                    .font(.caption).foregroundStyle(.secondary)
            } else if state.stageReviewing.contains(workspace.id) {
                Text("正在生成阶段复盘…").font(.caption).foregroundStyle(.secondary)
            }
            if let review = workspace.stageReview {
                MarkdownText(text: review, size: 13, lineSpacing: 3)
                    .padding(10).background(Brand.accent.opacity(0.06), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
            } else {
                Text("综合这个系列的会话摘要，整理高频问题、反复出现的薄弱点、进步和下一步准备；经验与答法会先进入待审阅区。")
                    .font(.caption).foregroundStyle(.secondary)
            }
        }
        KnowledgeReviewSection(workspaceID: workspace.id)
        Divider()
        VStack(alignment: .leading, spacing: 6) {
            WorkspaceSectionHeading("会议 \(sessions.count)", icon: "clock")
            if sessions.isEmpty {
                Text("这个系列的会议会出现在这里。").font(.caption).foregroundStyle(.secondary)
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
                    case .pending where state.archive.summarizing.contains(meta.id): ProgressView().controlSize(.mini)
                    case .pending: Image(systemName: "exclamationmark.circle").foregroundStyle(.orange).help("纪要整理已中断 · 可以重试")
                    case .failed: Image(systemName: "exclamationmark.circle").foregroundStyle(.orange)
                    case .none: EmptyView()
                    }
                    Button("查看") { state.historySelection = meta.id; state.historyWorkspaceFilter = workspace.id; state.select(.meetings) }
                        .buttonStyle(ChipButtonStyle())
                }
                .padding(8).background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
            }
        }
    }

    private func stat(_ title: String, _ value: String) -> some View {
        VStack(spacing: 2) {
            Text(value).font(.system(size: 18, weight: .semibold).monospacedDigit())
            Text(LocalizedStringKey(title)).font(.system(size: 11)).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity).padding(.vertical, 8)
        .background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }
}

/// Keep failed and partial imports visible even when no job is still running.
private struct PreparationQueueSummary: View {
    @Environment(AppState.self) private var state
    @State private var expanded = false
    private var jobs: [DocumentJob] { state.documentPreparation.jobs.filter { $0.workspaceID == state.activeWorkspace.id } }
    private var attentionCount: Int { jobs.filter { [.failed, .warning].contains($0.stage) }.count }
    private var summary: String {
        var parts = ["会前准备", "\(jobs.filter { $0.stage == .ready }.count) 份已就绪"]
        let pending = jobs.filter { !$0.stage.terminal }.count
        let cancelled = jobs.filter { $0.stage == .cancelled }.count
        if pending > 0 { parts.append("\(pending) 份待完成") }
        if attentionCount > 0 { parts.append("\(attentionCount) 份需处理") }
        if cancelled > 0 { parts.append("\(cancelled) 份已取消") }
        return parts.joined(separator: " · ")
    }
    var body: some View {
        let preparation = state.documentPreparation
        VStack(alignment: .leading, spacing: 8) {
            if !jobs.isEmpty {
                if attentionCount > 0 {
                    Label("\(attentionCount) 份资料需要处理或核对，展开下方列表查看。", systemImage: "exclamationmark.triangle")
                        .font(.system(size: 12)).foregroundStyle(.orange)
                        .fixedSize(horizontal: false, vertical: true)
                }
                DisclosureGroup(summary, isExpanded: $expanded) {
                    ForEach(jobs.suffix(100)) { job in
                        HStack(alignment: .top, spacing: 12) {
                            VStack(alignment: .leading, spacing: 4) {
                                Text(job.fileName).font(.system(size: 12, weight: .semibold)).textSelection(.enabled)
                                Text(job.progressText).font(.system(size: 12)).foregroundStyle(job.stage == .failed ? .red : job.stage == .warning ? .orange : .secondary)
                                if let total = job.total, total > 0, !job.stage.terminal { ProgressView(value: Double(job.completed), total: Double(total)) }
                                if !job.message.isEmpty { Text(job.message).font(.system(size: 12)).foregroundStyle(.secondary).textSelection(.enabled).fixedSize(horizontal: false, vertical: true) }
                            }
                            Spacer(minLength: 8)
                            if [.failed, .cancelled, .warning].contains(job.stage) {
                                Button("重试") { preparation.retry(job.id) }.buttonStyle(ChipButtonStyle())
                            } else if !job.stage.terminal {
                                Button("取消") { preparation.cancel(job.id) }.buttonStyle(ChipButtonStyle())
                            }
                        }.padding(.vertical, 6)
                    }
                }.font(.system(size: 12, weight: .medium))
            }
            if let error = preparation.error {
                Label(error, systemImage: "exclamationmark.circle").font(.system(size: 12)).foregroundStyle(.red).textSelection(.enabled)
            }
        }
        .onAppear { expanded = attentionCount > 0 }
        .onChange(of: attentionCount) { old, count in if count > old { expanded = true } }
        .onChange(of: state.activeWorkspace.id) { _, _ in expanded = attentionCount > 0 }
    }
}
