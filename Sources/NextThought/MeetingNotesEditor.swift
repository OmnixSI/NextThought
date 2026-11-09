import SwiftUI
import NextThoughtCore

struct MeetingNotesEditor: View {
    let detail: SessionDetail
    @Environment(\.dismiss) private var dismiss
    @State private var notes: StructuredNotes?
    @State private var markdown: String
    @State private var saving = false
    @State private var error: String?
    @State private var confirmsDiscard = false
    @State private var showOriginal = false

    init(detail: SessionDetail) {
        self.detail = detail
        _notes = State(initialValue: detail.effectiveNotes)
        _markdown = State(initialValue: detail.userContent.editedMarkdown ?? detail.effectiveSummary?.markdown ?? "")
    }

    private var dirty: Bool { notes != detail.effectiveNotes || markdown != (detail.userContent.editedMarkdown ?? detail.effectiveSummary?.markdown ?? "") }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s4) {
            HStack {
                Text("编辑纪要").font(Typo.section)
                Spacer()
                Toggle("查看 AI 原稿", isOn: $showOriginal).toggleStyle(.button).font(Typo.caption)
            }
            Text("修改保存在独立的用户版本中。重新生成只更新 AI 原稿，时间引用仍然保留。")
                .font(Typo.caption).foregroundStyle(Ink.secondary)
            if showOriginal {
                ScrollView {
                    MarkdownText(text: detail.notes.map { NotesMarkdown.render($0, title: detail.meta.displayTitle, facts: "") } ?? detail.summary?.markdown ?? "", size: 14)
                        .frame(maxWidth: .infinity, alignment: .leading).padding(Space.s4)
                }.background(Ink.surface)
            } else if notes != nil {
                ScrollView {
                    VStack(alignment: .leading, spacing: Space.s4) {
                        ForEach(notes!.sections.indices, id: \.self) { section in
                            Text(verbatim: notes!.sections[section].title).font(Typo.section)
                            ForEach(notes!.sections[section].items.indices, id: \.self) { item in
                                VStack(alignment: .leading, spacing: Space.s2) {
                                    TextEditor(text: itemText(section, item))
                                        .font(Typo.body).scrollContentBackground(.hidden)
                                        .frame(minHeight: 66).padding(Space.s2).background(Ink.surface)
                                        .overlay(RoundedRectangle(cornerRadius: 4).strokeBorder(Ink.hairline, lineWidth: 1))
                                    HStack {
                                        if let time = notes!.sections[section].items[item].time {
                                            Label(time, systemImage: "text.quote").font(Typo.caption).foregroundStyle(Ink.secondary)
                                        }
                                        if notes!.sections[section].items[item].uncertain {
                                            Button("我已核实") { notes?.sections[section].items[item].uncertain = false }
                                                .buttonStyle(GhostButtonStyle(height: 28, horizontalPadding: 8))
                                        }
                                    }
                                }
                            }
                        }
                    }.padding(Space.s2)
                }
            } else {
                TextEditor(text: $markdown).font(Typo.body).scrollContentBackground(.hidden).padding(Space.s3).background(Ink.surface)
            }
            if let error { Text(error).font(Typo.caption).foregroundStyle(Ink.caution).textSelection(.enabled) }
            HStack {
                Text(dirty ? "更改尚未保存" : "没有未保存的更改").font(Typo.caption).foregroundStyle(Ink.secondary)
                Spacer()
                Button("取消") { if dirty { confirmsDiscard = true } else { dismiss() } }
                    .keyboardShortcut(.cancelAction).buttonStyle(SecondaryButtonStyle()).disabled(saving)
                Button { Task { await save() } } label: {
                    BusyActionLabel(title: "保存更改", busyTitle: "正在保存…", busy: saving)
                }
                .buttonStyle(PrimaryButtonStyle()).controlBusy(saving)
                .disabled(saving || showOriginal).keyboardShortcut(.defaultAction)
            }
        }
        .padding(Space.s6).frame(width: 720, height: 640)
        .interactiveDismissDisabled(dirty || saving)
        .alert("放弃尚未保存的更改？", isPresented: $confirmsDiscard) {
            Button("继续编辑", role: .cancel) {}
            Button("放弃更改", role: .destructive) { dismiss() }
        }
    }

    private func itemText(_ section: Int, _ item: Int) -> Binding<String> {
        Binding(get: { notes?.sections[section].items[item].text ?? "" }, set: { notes?.sections[section].items[item].text = $0 })
    }

    private func save() async {
        saving = true; error = nil
        let edited = notes, editedMarkdown = markdown, basedOn = detail.notes?.generatedAt
        do {
            _ = try await SessionArchive.shared.updateUserContent(for: detail.meta.id) { content in
                content.editedNotes = edited
                if edited == nil, var summary = content.editedSummary {
                    summary.markdown = editedMarkdown
                    content.editedSummary = summary
                    content.editedMarkdown = nil
                } else {
                    content.editedMarkdown = edited == nil ? editedMarkdown : nil
                }
                content.basedOnGeneratedAt = basedOn
            }
            dismiss()
        } catch { self.error = "更改尚未保存：\(error.localizedDescription)" }
        saving = false
    }
}

struct MeetingEvidenceSidebar: View {
    let detail: SessionDetail
    let lineID: UUID?
    let onClose: () -> Void
    let onOpenTranscript: (Int) -> Void
    @Environment(AppState.self) private var state
    @State private var saving = false
    @State private var error: String?

    private var index: Int? { SessionTranscriptLocator.index(lineID: lineID, startedAt: detail.meta.startedAt, lines: detail.lines) }

    var body: some View {
        let effectiveLines = detail.effectiveLines
        VStack(alignment: .leading, spacing: Space.s4) {
            HStack {
                Text("原话依据").font(Typo.section)
                Spacer()
                Button(action: onClose) { Image(systemName: "xmark") }.buttonStyle(GlassIconButtonStyle(size: 28)).accessibilityLabel("关闭原话依据")
            }
            if let index {
                ScrollView {
                    VStack(alignment: .leading, spacing: Space.s4) {
                        ForEach(max(0, index - 2)...min(detail.lines.count - 1, index + 2), id: \.self) { row in
                            VStack(alignment: .leading, spacing: Space.s1) {
                                Text(verbatim: NotesTime.text(Int(max(0, detail.lines[row].date.timeIntervalSince(detail.meta.startedAt)))))
                                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                                Text(verbatim: effectiveLines[row].text).font(Typo.body).lineSpacing(5).textSelection(.enabled)
                            }.padding(Space.s2).background(row == index ? Ink.hover : .clear, in: RoundedRectangle(cornerRadius: 6))
                        }
                        if let notes = detail.effectiveNotes {
                            ForEach(notes.sections) { section in
                                ForEach(section.items.filter { item in
                                    guard item.uncertain, let time = item.time, let seconds = NotesTime.seconds(time) else { return false }
                                    return SessionTranscriptLocator.index(offsetSeconds: seconds, startedAt: detail.meta.startedAt, lines: detail.lines) == index
                                }) { item in
                                    VStack(alignment: .leading, spacing: Space.s2) {
                                        Text(verbatim: item.text).font(Typo.caption).textSelection(.enabled)
                                        HStack {
                                            Text("待确认").font(Typo.caption).foregroundStyle(Ink.caution)
                                            Spacer()
                                            Button("我已核实") { Task { await confirm(item.id, in: notes) } }
                                                .buttonStyle(SecondaryButtonStyle()).disabled(saving)
                                        }
                                    }.padding(Space.s3).background(Ink.surface)
                                }
                            }
                        }
                    }
                }
                Button("在完整转写中查看") { onOpenTranscript(index) }.buttonStyle(SecondaryButtonStyle())
                Button(detail.userContent.highlights.contains(where: { $0.lineID == lineID }) ? "取消重点" : "标为重点") {
                    state.toggleHighlight(lineID: detail.lines[index].id, text: effectiveLines[index].text,
                                          offsetSeconds: Int(max(0, detail.lines[index].date.timeIntervalSince(detail.meta.startedAt))), sessionID: detail.meta.id)
                }.buttonStyle(GhostButtonStyle(height: 32))
            } else {
                Text("此原话无法定位。已有笔记仍保留，请查看完整转写核实。")
                    .font(Typo.body).foregroundStyle(Ink.caution)
            }
            if let error { Text(error).font(Typo.caption).foregroundStyle(Ink.caution) }
        }
        .padding(Space.s4).frame(width: 300).frame(maxHeight: .infinity, alignment: .topLeading)
        .background(Ink.surface)
        .overlay(alignment: .leading) { Rectangle().fill(Ink.hairline).frame(width: 1) }
    }

    private func confirm(_ id: UUID, in notes: StructuredNotes) async {
        saving = true
        var edited = notes
        for section in edited.sections.indices {
            for item in edited.sections[section].items.indices where edited.sections[section].items[item].id == id {
                edited.sections[section].items[item].uncertain = false
            }
        }
        let snapshot = edited, generated = detail.notes?.generatedAt
        do {
            _ = try await SessionArchive.shared.updateUserContent(for: detail.meta.id) { content in content.editedNotes = snapshot; content.basedOnGeneratedAt = generated }
        } catch { self.error = "核实状态尚未保存：\(error.localizedDescription)" }
        saving = false
    }
}
