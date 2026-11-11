import SwiftUI
import NextThoughtCore

/// One draft owner for live and archived editors. Leaving a view never discards unsaved text.
struct MeetingUserNotes: View {
    let sessionID: UUID
    var highlights: [SessionHighlight] = []
    var onOpenHighlight: ((SessionHighlight) -> Void)?
    @State private var loaded = false
    @State private var loadError: String?
    private var archive: SessionArchive { .shared }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack {
                Text("我的笔记").font(Typo.section)
                Spacer(minLength: Space.s2)
                if archive.savingUserContent.contains(sessionID) {
                    ProgressView().controlSize(.mini)
                    Text("正在保存…").font(Typo.caption).foregroundStyle(Ink.secondary)
                } else if archive.userContentDrafts[sessionID] != nil {
                    Text("尚未保存").font(Typo.caption).foregroundStyle(Ink.caution)
                } else if loaded && loadError == nil {
                    Label("已保存在本机", systemImage: "checkmark").font(Typo.caption).foregroundStyle(Ink.secondary)
                }
            }
            if let error = loadError ?? archive.userSaveErrors[sessionID] {
                HStack(alignment: .top) {
                    Text(error).font(Typo.caption).foregroundStyle(Ink.caution).textSelection(.enabled)
                    Spacer(minLength: 4)
                    Button("重试") { Task { await load(); await archive.persistPersonalNotes(for: sessionID) } }
                        .buttonStyle(SecondaryButtonStyle())
                }
            }
            Text("记下自己的想法、别人的反馈和待查的问题。AI 重新整理不会覆盖这里。")
                .font(Typo.caption).foregroundStyle(Ink.secondary)
            TextEditor(text: Binding(get: { archive.personalNotes(for: sessionID) }, set: { archive.editPersonalNotes($0, for: sessionID) }))
                .font(Typo.body)
                .scrollContentBackground(.hidden)
                .padding(Space.s2)
                .background(Ink.surface, in: RoundedRectangle(cornerRadius: Space.radiusSmall))
                .overlay(RoundedRectangle(cornerRadius: Space.radiusSmall).strokeBorder(Ink.hairline, lineWidth: 1))
                .frame(minHeight: 160, maxHeight: .infinity)
                .disabled(!loaded || loadError != nil)
                .accessibilityLabel("个人笔记，自动保存在本机")
                .accessibilityIdentifier("meeting.personalNotes")
            if !highlights.isEmpty {
                Text("重点 · \(highlights.count)").overline()
                ForEach(highlights) { highlight in
                    Button { onOpenHighlight?(highlight) } label: {
                        HStack(alignment: .top, spacing: Space.s2) {
                            Image(systemName: "star.fill").foregroundStyle(Ink.caution)
                            Text(verbatim: "[\(NotesTime.text(highlight.offsetSeconds))] \(highlight.quote)")
                                .lineLimit(3).frame(maxWidth: .infinity, alignment: .leading)
                        }
                        .font(Typo.caption)
                    }
                    .buttonStyle(.plain)
                    .disabled(onOpenHighlight == nil)
                }
            }
        }
        .task(id: sessionID) { await load() }
        .onDisappear { Task { await archive.persistPersonalNotes(for: sessionID) } }
    }

    private func load() async {
        do { let content = try await archive.userContent(sessionID); archive.primeUserContent(content, for: sessionID); loaded = true; loadError = nil }
        catch { loaded = true; loadError = "无法读取已有笔记：\(error.localizedDescription)" }
    }
}

@MainActor
extension AppState {
    func toggleHighlight(lineID: UUID, text: String, offsetSeconds: Int, sessionID: UUID) {
        Task {
            do {
                _ = try await archive.updateUserContent(for: sessionID) { content in
                    if content.highlights.contains(where: { $0.lineID == lineID }) { content.highlights.removeAll { $0.lineID == lineID } }
                    else { content.highlights.append(.init(lineID: lineID, offsetSeconds: offsetSeconds, quote: text)) }
                }
            } catch { errorMessage = "重点尚未保存：\(error.localizedDescription)" }
        }
    }
}
