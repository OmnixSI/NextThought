import SwiftUI
import InterviewHackerCore

/// Settings → 纪要 → 纪要格式: which view each meeting type gets beside its text notes
/// (fishbone, outline, viewpoint matrix), and the switch that turns the extra pass off.
struct NotesFormatSettingsView: View {
    private var prefs: NotesFormatPrefs { NotesFormatPrefs.shared }

    var body: some View {
        SettingsSection(title: "纪要格式", icon: "list.bullet.indent") {
            HStack(spacing: 6) {
                Text("文字纪要始终保留，格式是多出来的一种看法。")
                    .font(.system(size: 11.5)).foregroundStyle(.secondary)
                Hint("讲座 / 课程默认用要点大纲，组会用因果鱼骨图，小组讨论用观点对照，其余只有文字纪要。每个节点带原话的时间点，点一下回到转写；没有原话依据的节点会被去掉。", presentation: .info)
                    .accessibilityLabel("纪要格式说明")
            }
            Toggle("会后生成结构图", isOn: Binding(get: { prefs.enabled }, set: { prefs.setEnabled($0) }))
            ForEach(SessionMode.builtIn) { mode in
                HStack(spacing: 12) {
                    Text(verbatim: mode.displayName).frame(width: 150, alignment: .leading)
                    MenuPicker(selection: Binding(get: { prefs.format(for: mode) }, set: { prefs.setFormat($0, for: mode) }),
                               options: NotesFormat.allCases.map { ($0, $0.displayName) })
                    Spacer(minLength: 0)
                }
            }
            .disabled(!prefs.enabled)
            Hint("结构图会多一次模型调用。已整理的会议在纪要页的“模板”菜单里换格式。")
        }
    }
}

/// The "换一种格式重新整理" entry of the template menu on a meeting's notes page: rewrites the
/// structured view only, the text notes stay.
struct NotesFormatMenuItems: View {
    @Environment(AppState.self) private var state
    let id: UUID
    let detail: SessionDetail?

    var body: some View {
        let current = detail?.summary?.structured?.format ?? .standard
        Menu("换一种格式重新整理") {
            ForEach(NotesFormat.allCases) { format in
                Button {
                    state.regenerateStructuredNotes(for: id, format: format)
                } label: {
                    if format == current {
                        Label(format.displayName, systemImage: "checkmark")
                    } else {
                        Text(verbatim: format.displayName)
                    }
                }
            }
        }
        .disabled(NotesFormatPrefs.shared.generating.contains(id) || (detail?.lines.isEmpty ?? true))
    }
}
