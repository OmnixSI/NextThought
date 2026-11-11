import AppKit
import SwiftUI
import NextThoughtCore

// MARK: - Shared rows

/// A switch with one gray help line under it, for settings whose label alone is not enough.
struct SettingToggle: View {
    let title: String
    var help: String?
    @Binding var isOn: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            Toggle(LocalizedStringKey(title), isOn: $isOn)
            if let help { Note(help, presentation: .inline) }
        }
    }
}

// MARK: - 场景

/// What each scene sets up when a meeting starts. One row per scene, only the
/// choices a scene really changes; everything else follows from the scene itself.
struct SceneSettingsPage: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            SettingsSection(title: "各场景开会时的设置") {
                VStack(spacing: 0) {
                    ForEach(SessionMode.builtIn) { mode in
                        if mode != SessionMode.builtIn.first { Rectangle().fill(Ink.hairline).frame(height: 0.5) }
                        SceneRow(mode: mode).padding(.vertical, Space.s3)
                    }
                }
                Note("开始一场会时按这里的设置来；会中仍可随时切换。")
            }
        }
    }
}

private struct SceneRow: View {
    @Environment(AppState.self) private var state
    let mode: SessionMode
    private var scenes: SceneSettings { SceneSettings.shared }
    private var customised: Bool {
        scenes.isCustomised(mode)
            || state.defaultNotesTemplateID(for: mode) != MeetingTypeDefaults.for(mode).template.rawValue
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            HStack(spacing: Space.s2) {
                Circle().fill(mode.theme.tint).frame(width: 8, height: 8).accessibilityHidden(true)
                Text(verbatim: mode.displayName).font(Typo.label)
                Spacer(minLength: Space.s2)
                if customised {
                    Button("恢复默认") {
                        scenes.reset(mode)
                        state.setDefaultNotesTemplate(MeetingTypeDefaults.for(mode).template.rawValue, for: mode)
                    }
                    .buttonStyle(ChipButtonStyle())
                    .accessibilityIdentifier("settings.scene.reset." + mode.rawValue)
                }
            }
            HStack(alignment: .top, spacing: Space.s3) {
                column("会中", picker: SceneMenu(
                    selection: Binding(get: { scenes.liveMode(for: mode) }, set: { scenes.setLiveMode($0, for: mode) }),
                    options: [(.prompting, String(localized: "自动回答")), (.notesOnly, String(localized: "只记录"))],
                    accessibilityTitle: String(localized: "会中") + " · " + mode.displayName))
                column("回答", picker: SceneMenu(
                    selection: Binding(get: { scenes.style(for: mode) }, set: { scenes.setStyle($0, for: mode) }),
                    options: [(.concise, String(localized: "简洁")), (.detailed, String(localized: "详细"))],
                    accessibilityTitle: String(localized: "回答") + " · " + mode.displayName))
                column("纪要模板", picker: SceneMenu(
                    selection: Binding(get: { state.defaultNotesTemplateID(for: mode) },
                                       set: { state.setDefaultNotesTemplate($0, for: mode) }),
                    options: state.allNotesTemplates().map { ($0.id, $0.name) },
                    accessibilityTitle: String(localized: "纪要模板") + " · " + mode.displayName))
                    .accessibilityIdentifier("settings.template." + mode.rawValue)
                column("提词", picker: SceneMenu(
                    selection: Binding(get: { scenes.cueStyle(for: mode) }, set: { scenes.setCueStyle($0, for: mode) }),
                    options: [(.keyPoints, String(localized: "要点")), (.script, String(localized: "逐句"))],
                    accessibilityTitle: String(localized: "提词") + " · " + mode.displayName))
            }
        }
    }

    private func column<Picker: View>(_ title: String, picker: Picker) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(LocalizedStringKey(title)).font(Typo.caption).foregroundStyle(Ink.secondary)
            picker.frame(maxWidth: .infinity, alignment: .leading)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

/// A compact value menu that sits at the left of its column.
private struct SceneMenu<Value: Hashable>: View {
    @Binding var selection: Value
    let options: [(Value, String)]
    var accessibilityTitle: String

    var body: some View {
        Menu {
            ForEach(options.indices, id: \.self) { index in
                Button { selection = options[index].0 } label: {
                    if options[index].0 == selection {
                        Label { Text(verbatim: options[index].1) } icon: { Image(systemName: "checkmark") }
                    } else { Text(verbatim: options[index].1) }
                }
            }
        } label: {
            HStack(spacing: 6) {
                Text(verbatim: options.first { $0.0 == selection }?.1 ?? "").lineLimit(1)
                Image(systemName: "chevron.down").font(.system(size: 10, weight: .medium)).foregroundStyle(Ink.secondary)
            }
        }
        .menuStyle(.button).buttonStyle(SecondaryButtonStyle()).menuIndicator(.hidden).fixedSize()
        .accessibilityLabel(Text(verbatim: accessibilityTitle))
        .accessibilityValue(Text(verbatim: options.first { $0.0 == selection }?.1 ?? ""))
    }
}

// MARK: - 回答与资料

/// 回答服务, 联网搜索, how documents are read, and skills. Reasoning strength, saved
/// providers, the search service, components and the rest are in 高级.
struct AnswerSettings: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            SettingsSection(title: "回答服务") {
                AnswerServiceChooser()
            }
            WebSearchSettings()
            DocumentReadingSettings()
            SkillSettings()
        }
    }
}

/// One choice at the top, API or Codex, and only the fields of the chosen path below it.
private struct AnswerServiceChooser: View {
    @Environment(AppState.self) private var state
    private enum Provider: String, CaseIterable, Identifiable {
        case api = "API", codex = "Codex"
        var id: String { rawValue }
    }
    @State private var shown: Provider?
    private var current: Provider { shown ?? (state.answerBackend == .openAICompatible ? .api : .codex) }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s4) {
            Picker("回答服务", selection: Binding(get: { current }, set: choose)) {
                ForEach(Provider.allCases) { Text(verbatim: $0.rawValue).tag($0) }
            }
            .pickerStyle(.segmented).labelsHidden().frame(maxWidth: 220)
            .accessibilityLabel("回答服务")
            .accessibilityIdentifier("settings.answerProvider")
            switch current {
            case .api: APIConnectView(showsMoreOptions: false)
            case .codex: CodexEngineSection(showsAdvanced: false)
            }
        }
    }

    /// Switches only when the chosen path can already answer; otherwise it shows that
    /// path's setup and leaves the working one in use.
    private func choose(_ next: Provider) {
        shown = next
        switch next {
        case .api:
            let local = APIProviders.isLocal(baseURL: state.apiBaseURL)
            if !state.apiModel.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, !state.apiKey.isEmpty || local {
                state.selectAnswerBackend(.openAICompatible)
            }
        case .codex:
            if state.codexReady { state.selectAnswerBackend(.codexAppServer) }
        }
    }
}
