import AppKit
import SwiftUI
import UniformTypeIdentifiers
import NextThoughtCore

// MARK: - Settings entry

/// Settings → 声音 → 术语和人名: one card that opens the glossary page. `extra` sits
/// under the button (the quick vocabulary field, which feeds recognition the same way).
struct GlossarySettingsEntry<Extra: View>: View {
    @Environment(AppState.self) private var state
    @State private var showing = false
    private let extra: Extra

    init(@ViewBuilder extra: () -> Extra) { self.extra = extra() }

    var body: some View {
        let global = GlossaryStore.shared.glossary.entries.count
        let series = state.activeWorkspace.glossary?.entries.count ?? 0
        SettingsSection(title: "术语和人名", icon: "character.book.closed") {
            Text("名字和术语在转写、回答、纪要里写法统一；记下听错的写法，搜索时一并找出来。")
                .font(.zoomed(size: 11.5)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            HStack(spacing: 10) {
                Text(summary(global: global, series: series)).font(Typo.caption).foregroundStyle(Ink.secondary)
                Spacer(minLength: 0)
                Button("管理…") { showing = true }.buttonStyle(SecondaryButtonStyle())
            }
            extra
        }
        .sheet(isPresented: $showing) {
            GlossaryView().environment(state)
        }
    }

    private func summary(global: Int, series: Int) -> String {
        if global + series == 0 { return String(localized: "还没有条目") }
        if series == 0 { return String(localized: "全局 \(global) 条") }
        return String(localized: "全局 \(global) 条 · 当前系列 \(series) 条")
    }
}

// MARK: - Glossary page

/// The glossary of names and terms: a list on the left, the selected entry on the right.
struct GlossaryView: View {
    @Environment(AppState.self) private var state
    @Environment(\.dismiss) private var dismiss

    private enum Mode { case edit, paste }

    @State private var scope: GlossaryScope = .global
    @State private var mode: Mode = .edit
    @State private var selectedID: UUID?
    @State private var draft = GlossaryEntry(canonical: "")
    @State private var variantInput = ""
    @State private var filter = ""
    @State private var pasteText = ""
    @State private var pasteKind: GlossaryKind = .term
    @State private var status = ""
    @FocusState private var canonicalFocused: Bool
    @FocusState private var variantsFocused: Bool

    private var entries: [GlossaryEntry] { state.glossaryEntries(in: scope) }

    private var shown: [GlossaryEntry] {
        let term = filter.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !term.isEmpty else { return entries }
        return entries.filter { entry in
            (entry.spellings + [entry.note ?? ""]).contains { $0.localizedCaseInsensitiveContains(term) }
        }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            header
            toolbar
            HStack(alignment: .top, spacing: Space.s4) {
                list.frame(width: 270)
                Group {
                    switch mode {
                    case .edit: editor
                    case .paste: pasteEditor
                    }
                }
                .frame(maxWidth: .infinity, alignment: .topLeading)
            }
            .frame(maxHeight: .infinity, alignment: .top)
        }
        .padding(Space.s6)
        .frame(minWidth: 780, idealWidth: 820, minHeight: 540, idealHeight: 580)
        .background(Ink.paper)
        .onDisappear { finishVariants(); state.tidyGlossaries() }
        .onChange(of: draft) { _, next in commit(next) }
        .onChange(of: scope) { _, _ in
            selectedID = nil; mode = .edit; filter = ""; status = ""
        }
    }

    // MARK: Header and toolbar

    private var header: some View {
        VStack(alignment: .leading, spacing: Space.s1) {
            HStack {
                Text("术语和人名").font(Typo.section)
                Spacer()
                Button("完成") {
                    if mode == .paste, !pasteText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { importPasted() }
                    finishVariants(); dismiss()
                }.buttonStyle(PrimaryButtonStyle())
                    .keyboardShortcut(.defaultAction)
            }
            Text("名字和术语在转写、回答、纪要里写法统一；记下听错的写法，搜索时一并找出来。原始转写不会被改写。")
                .font(Typo.caption).foregroundStyle(Ink.secondary)
        }
    }

    private var toolbar: some View {
        HStack(spacing: Space.s2) {
            if !state.workspaces.workspaces.isEmpty {
                InkSegmented(items: [(value: GlossaryScope.global, title: LocalizedStringKey("全局")), (value: GlossaryScope.series, title: LocalizedStringKey("当前系列"))],
                             selection: Binding(get: { scope }, set: { next in finishVariants(); scope = next }))
                    .fixedSize()
            }
            HStack(spacing: 6) {
                Image(systemName: "magnifyingglass").font(.zoomed(size: 11)).foregroundStyle(Ink.secondary)
                TextField("查找", text: $filter).textFieldStyle(.plain).font(.zoomed(size: 12.5))
            }
            .padding(.horizontal, 10).frame(height: Hit.minHeight).frame(maxWidth: 200)
            .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            Spacer(minLength: 0)
            if !status.isEmpty { Text(verbatim: status).font(Typo.caption).foregroundStyle(Ink.secondary) }
            Menu {
                Button("批量粘贴…") { mode = .paste; selectedID = nil }
                Divider()
                Button("从文件导入…") { importFile() }
                Button("导出为文件…") { exportFile() }
                Button("复制为文字") { copyText() }
                Divider()
                Button("整理重复项") { state.tidyGlossaries(); status = String(localized: "已整理") }
            } label: {
                Image(systemName: "ellipsis")
            }
            .menuStyle(.button).buttonStyle(GlassIconButtonStyle()).menuIndicator(.hidden).fixedSize()
            .accessibilityLabel("更多")
        }
    }

    // MARK: List

    private var list: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            Button { startNew() } label: {
                Label("新增", systemImage: "plus").frame(maxWidth: .infinity, alignment: .leading)
            }
            .buttonStyle(GhostButtonStyle())
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 2) {
                    if entries.isEmpty {
                        Text(scope == .global ? "还没有条目。点“新增”，或用“批量粘贴”一次加入多条。" : "这个系列还没有自己的条目。")
                            .font(Typo.caption).foregroundStyle(Ink.secondary).padding(Space.s2)
                    } else if shown.isEmpty {
                        Text("没有匹配的条目。").font(Typo.caption).foregroundStyle(Ink.secondary).padding(Space.s2)
                    }
                    ForEach(shown) { entry in
                        Button { select(entry) } label: { row(entry) }
                            .buttonStyle(InteractiveRowButtonStyle())
                            .accessibilityAddTraits(entry.id == selectedID ? .isSelected : [])
                    }
                }
            }
        }
        .paperCard(padding: Space.s2)
        .frame(maxHeight: .infinity, alignment: .top)
    }

    private func row(_ entry: GlossaryEntry) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(spacing: 6) {
                Text(verbatim: entry.canonical).font(Typo.label).lineLimit(1)
                if entry.kind == .person {
                    Text("人名").font(.zoomed(size: 10.5)).foregroundStyle(Ink.secondary)
                        .padding(.horizontal, 5).padding(.vertical, 1)
                        .background(Ink.sunken, in: Capsule())
                }
                Spacer(minLength: 0)
            }
            if !entry.variants.isEmpty {
                Text(verbatim: "← " + entry.variants.joined(separator: "、"))
                    .font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
            }
        }
        .padding(.horizontal, Space.s2).padding(.vertical, 6)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(entry.id == selectedID ? Ink.hover : Color.clear,
                    in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
    }

    // MARK: Editor

    @ViewBuilder private var editor: some View {
        if selectedID == nil {
            VStack(spacing: Space.s2) {
                Text("选一条来编辑，或新增一条。").font(Typo.body).foregroundStyle(Ink.secondary)
            }
            .frame(maxWidth: .infinity, minHeight: 200)
            .paperCard()
        } else {
            VStack(alignment: .leading, spacing: Space.s4) {
                field("规范写法") {
                    TextField("正确的写法，例如 Kubernetes", text: $draft.canonical)
                        .textFieldStyle(.plain).fieldSurface().focused($canonicalFocused)
                }
                InkSegmented(items: [(value: GlossaryKind.term, title: "术语"), (value: GlossaryKind.person, title: "人名")],
                             selection: $draft.kind)
                    .fixedSize()
                field("听错的写法") {
                    VStack(alignment: .leading, spacing: Space.s2) {
                        if !draft.variants.isEmpty {
                            FlowLayout(spacing: 6, lineSpacing: 6) {
                                ForEach(draft.variants, id: \.self) { variant in variantTag(variant) }
                            }
                        }
                        TextField("识别出来的错误写法，回车或逗号分隔后加入", text: $variantInput)
                            .textFieldStyle(.plain).fieldSurface()
                            .focused($variantsFocused)
                            .onSubmit { addVariants() }
                            .onChange(of: variantsFocused) { _, focused in if !focused { finishVariants() } }
                            .onChange(of: variantInput) { _, text in
                                if text.contains(where: { ",，、;；".contains($0) }) { addVariants() }
                            }
                    }
                }
                field("备注") {
                    TextField("可选，例如“我们的数据库项目”", text: Binding(get: { draft.note ?? "" },
                                                                         set: { draft.note = $0.isEmpty ? nil : $0 }))
                        .textFieldStyle(.plain).fieldSurface()
                }
                HStack {
                    if duplicatesAnother {
                        Text("已有同名条目，关闭这个页面时会合并。").font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                    Spacer(minLength: 0)
                    Button("删除", role: .destructive) { deleteSelected() }.buttonStyle(GhostButtonStyle())
                }
            }
            .paperCard()
        }
    }

    private func field<Content: View>(_ title: LocalizedStringKey, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: Space.s1) {
            Text(title).overline()
            content()
        }
    }

    private func variantTag(_ variant: String) -> some View {
        HStack(spacing: 4) {
            Text(verbatim: variant).font(Typo.caption)
            Button { draft.variants.removeAll { $0 == variant } } label: {
                Image(systemName: "xmark").font(.zoomed(size: 8, weight: .semibold))
            }
            .buttonStyle(TapButtonStyle(minWidth: 24, minHeight: 24)).foregroundStyle(Ink.secondary)
            .accessibilityLabel("移除")
        }
        .padding(.horizontal, 8).padding(.vertical, 3)
        .background(Ink.sunken, in: Capsule())
    }

    private var duplicatesAnother: Bool {
        let key = Glossary.looseKey(draft.canonical)
        guard !key.isEmpty else { return false }
        return entries.contains { $0.id != draft.id && Glossary.looseKey($0.canonical) == key }
    }

    // MARK: Paste

    private var pasteEditor: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            Text("批量粘贴").font(Typo.label)
            Text("每行一条：规范写法 | 变体1, 变体2 | 类型 | 备注。后两项可以省略，类型写“人名”或“术语”。")
                .font(Typo.caption).foregroundStyle(Ink.secondary)
            TextEditor(text: $pasteText)
                .font(Typo.code).scrollContentBackground(.hidden)
                .padding(Space.s2)
                .frame(minHeight: 220)
                .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            HStack(spacing: Space.s2) {
                Text("没写类型的当作").font(Typo.caption).foregroundStyle(Ink.secondary)
                InkSegmented(items: [(value: GlossaryKind.term, title: "术语"), (value: GlossaryKind.person, title: "人名")],
                             selection: $pasteKind)
                    .fixedSize()
                Spacer(minLength: 0)
                Button("取消") { mode = .edit; pasteText = "" }.buttonStyle(GhostButtonStyle())
                Button("加入") { importPasted() }
                    .buttonStyle(PrimaryButtonStyle())
                    .disabled(pasteText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
        }
        .paperCard()
    }

    // MARK: Actions

    private func select(_ entry: GlossaryEntry) {
        finishVariants()
        mode = .edit
        selectedID = entry.id
        draft = entry
        variantInput = ""
        status = ""
    }

    private func startNew() {
        finishVariants()
        let entry = GlossaryEntry(canonical: "")
        mode = .edit
        selectedID = entry.id
        draft = entry
        variantInput = ""
        status = ""
        canonicalFocused = true
    }

    /// Saves what is being edited. An entry without a canonical spelling is not saved.
    private func commit(_ entry: GlossaryEntry) {
        guard entry.id == selectedID,
              !entry.canonical.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }
        var items = entries
        if let index = items.firstIndex(where: { $0.id == entry.id }) {
            guard items[index] != entry else { return }
            items[index] = entry
        } else {
            items.append(entry)
        }
        state.setGlossaryEntries(items, in: scope)
    }

    private func addVariants() {
        let parts = variantInput.components(separatedBy: CharacterSet(charactersIn: ",，、;；\n"))
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { $0.count >= 2 && $0 != draft.canonical }
        for part in parts where !draft.variants.contains(part) { draft.variants.append(part) }
        variantInput = ""
    }

    private func finishVariants() {
        guard selectedID != nil else { return }
        addVariants()
        commit(draft)
    }

    private func deleteSelected() {
        guard let id = selectedID else { return }
        state.setGlossaryEntries(entries.filter { $0.id != id }, in: scope)
        selectedID = nil
    }

    private func importPasted() {
        let added = state.importGlossaryText(pasteText, defaultKind: pasteKind, in: scope)
        status = added > 0 ? String(localized: "已加入 \(added) 项") : String(localized: "没有新的内容")
        pasteText = ""
        mode = .edit
    }

    private func copyText() {
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(state.exportGlossaryText(in: scope), forType: .string)
        status = String(localized: "已复制")
    }

    private func exportFile() {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.plainText]
        panel.nameFieldStringValue = String(localized: "术语和人名.txt")
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            try state.exportGlossaryText(in: scope).write(to: url, atomically: true, encoding: .utf8)
            status = String(localized: "已导出")
        } catch {
            status = error.localizedDescription
        }
    }

    private func importFile() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.plainText]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        guard panel.runModal() == .OK, let url = panel.url,
              let text = try? String(contentsOf: url, encoding: .utf8) else { return }
        let added = state.importGlossaryText(text, defaultKind: .term, in: scope)
        status = added > 0 ? String(localized: "已加入 \(added) 项") : String(localized: "没有新的内容")
    }
}

private extension View {
    /// A sunken single-line field, as in Settings.
    func fieldSurface() -> some View {
        self.font(.zoomed(size: 13))
            .padding(.horizontal, 10).frame(height: 32)
            .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
    }
}

// MARK: - Search marker

/// A small line under a search hit that was found through a recorded misheard spelling.
struct MisheardMark: View {
    let form: String

    var body: some View {
        Label {
            Text("匹配到听错的写法：\(form)")
        } icon: {
            Image(systemName: "text.magnifyingglass")
        }
        .font(.zoomed(size: 11)).foregroundStyle(Ink.secondary)
        .help("这是“术语和人名”里记下的听错写法，搜索时一并找出来")
    }
}

/// The find field above a transcript.
struct TranscriptFindField: View {
    @Binding var query: String
    /// Number of matching lines while a search is active.
    var matches: Int?

    var body: some View {
        HStack(spacing: 6) {
            Image(systemName: "magnifyingglass").font(.zoomed(size: 11)).foregroundStyle(Ink.secondary)
            TextField("在转写里查找（听错的写法也会找出来）", text: $query).textFieldStyle(.plain).font(.zoomed(size: 12.5))
            if let matches {
                Text("\(matches) 句").font(Typo.caption).foregroundStyle(Ink.secondary)
            }
            if !query.isEmpty {
                Button { query = "" } label: { Image(systemName: "xmark.circle.fill") }
                    .buttonStyle(TapButtonStyle(minWidth: 30, minHeight: 28)).font(.zoomed(size: 11)).foregroundStyle(Ink.secondary)
                    .accessibilityLabel("清空搜索")
            }
        }
        .padding(.horizontal, 10).frame(height: 30)
        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
    }
}

// MARK: - Add from a meeting

/// What the transcript's “加入术语表…” menu item passes to the sheet.
struct GlossaryDraft: Identifiable {
    let id = UUID()
    let line: String
}

/// “加入术语表”: the line it came from, the correct spelling and the misheard one.
/// SwiftUI text cannot report its selection, so the misheard spelling is taken from the clipboard
/// when it is part of the line (select it, copy it, then open this sheet).
struct AddToGlossarySheet: View {
    @Environment(AppState.self) private var state
    @Environment(\.dismiss) private var dismiss
    let line: String
    @State private var canonical = ""
    @State private var heard = ""
    @State private var kind: GlossaryKind = .term
    @State private var seriesOnly = false

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s4) {
            Text("加入术语表").font(Typo.section)
            Text(verbatim: line).font(Typo.caption).foregroundStyle(Ink.secondary)
                .lineLimit(4).textSelection(.enabled)
            Text("在这句里选中听错的词并复制（⌘C），再打开本窗口，会自动填入。")
                .font(.zoomed(size: 11)).foregroundStyle(Ink.secondary)
            VStack(alignment: .leading, spacing: Space.s1) {
                Text("规范写法").overline()
                TextField("正确的写法", text: $canonical).textFieldStyle(.roundedBorder)
            }
            VStack(alignment: .leading, spacing: Space.s1) {
                Text("听错的写法").overline()
                TextField("多个用逗号分隔", text: $heard).textFieldStyle(.roundedBorder)
            }
            HStack(spacing: Space.s3) {
                InkSegmented(items: [(value: GlossaryKind.term, title: "术语"), (value: GlossaryKind.person, title: "人名")],
                             selection: $kind)
                    .fixedSize()
                if !state.workspaces.workspaces.isEmpty {
                    Toggle("只用于当前系列", isOn: $seriesOnly).toggleStyle(.inline)
                }
            }
            HStack {
                Spacer()
                Button("取消") { dismiss() }.buttonStyle(GhostButtonStyle())
                Button("加入") { add() }
                    .buttonStyle(PrimaryButtonStyle())
                    .disabled(canonical.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                    .keyboardShortcut(.defaultAction)
            }
        }
        .padding(Space.s6)
        .frame(width: 440)
        .background(Ink.paper)
        .onAppear { prefillFromClipboard() }
    }

    private func prefillFromClipboard() {
        guard heard.isEmpty, let copied = NSPasteboard.general.string(forType: .string) else { return }
        let text = copied.trimmingCharacters(in: .whitespacesAndNewlines)
        if text.count >= 2, text.count <= 40, !text.contains("\n"), line.contains(text) { heard = text }
    }

    private func add() {
        let variants = heard.components(separatedBy: CharacterSet(charactersIn: ",，、;；"))
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
        state.addToGlossary(canonical: canonical, variants: variants, kind: kind, in: seriesOnly ? .series : .global)
        dismiss()
    }
}
