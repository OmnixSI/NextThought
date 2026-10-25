import AppKit
import SwiftUI

/// The interface language of the whole app (03-language.md).
///
/// macOS picks the localization when the process starts, so a change is written to
/// the app's own `AppleLanguages` default and the app reopens itself. "跟随系统"
/// removes the override.
enum AppLanguage: String, CaseIterable, Identifiable {
    case system
    case simplifiedChinese = "zh-Hans"
    case english = "en"

    var id: String { rawValue }

    var title: LocalizedStringKey {
        switch self {
        case .system: "跟随系统"
        case .simplifiedChinese: "简体中文"
        case .english: "English"
        }
    }

    private static let preferenceKey = "appLanguagePreference"

    /// The user's choice (not the resolved language).
    static var current: AppLanguage {
        AppLanguage(rawValue: UserDefaults.standard.string(forKey: preferenceKey) ?? "") ?? .system
    }

    /// Whether the interface is currently shown in English (resolved by the system
    /// from the bundle's localizations and the preferred languages).
    static var interfaceIsEnglish: Bool {
        (Bundle.main.preferredLocalizations.first ?? "zh-Hans").hasPrefix("en")
    }

    /// Stores the choice; it takes effect on the next launch.
    static func apply(_ language: AppLanguage) {
        let defaults = UserDefaults.standard
        defaults.set(language.rawValue, forKey: preferenceKey)
        if language == .system {
            defaults.removeObject(forKey: "AppleLanguages")
        } else {
            defaults.set([language.rawValue], forKey: "AppleLanguages")
        }
        Diagnostics.record("language.changed", ["language": language.rawValue])
    }

    /// Whether this process runs from an app bundle that can be reopened.
    static var canRelaunch: Bool { Bundle.main.bundlePath.hasSuffix(".app") }

    /// Quits and opens the same app bundle again a moment later.
    @MainActor
    static func relaunch() {
        guard canRelaunch else { NSApp.terminate(nil); return }
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/sh")
        // The path is passed as $0, so it never needs quoting.
        process.arguments = ["-c", "sleep 1; /usr/bin/open \"$0\"", Bundle.main.bundlePath]
        do {
            try process.run()
        } catch {
            Diagnostics.record("language.relaunch_failed")
        }
        NSApp.terminate(nil)
    }
}

/// Settings → 基础 → 语言.
struct LanguageSettingsSection: View {
    @Environment(AppState.self) private var state
    @State private var choice = AppLanguage.current
    @State private var pending: AppLanguage?

    var body: some View {
        SettingsSection(title: "语言", icon: "globe") {
            Picker("语言", selection: Binding(get: { choice }, set: { value in
                if value != choice { pending = value }
            })) {
                ForEach(AppLanguage.allCases) { language in
                    Text(language.title).tag(language)
                }
            }
            .pickerStyle(.segmented)
            .labelsHidden()
            .disabled(state.isRunning)
            Hint(state.isRunning ? "会议进行中不能切换语言。" : "整个 App 的界面语言。切换后 App 会自动重新打开。建议回答跟着对话的语言走，不受这里影响。")
        }
        .confirmationDialog("需要重新打开 App", isPresented: Binding(get: { pending != nil }, set: { if !$0 { pending = nil } }),
                            titleVisibility: .visible) {
            Button("重新打开") {
                guard let language = pending else { return }
                AppLanguage.apply(language)
                choice = language
                pending = nil
                AppLanguage.relaunch()
            }
            Button("取消", role: .cancel) { pending = nil }
        } message: {
            Text("切换语言后，App 会保存当前内容并自动重新打开。")
        }
    }
}

/// A localized note line (the settings `Note` shows its string verbatim).
struct Hint: View {
    var key: LocalizedStringKey
    var color: Color = Ink.secondary
    init(_ key: LocalizedStringKey, color: Color = Ink.secondary) { self.key = key; self.color = color }
    var body: some View {
        Text(key).font(.system(size: 11.5)).foregroundStyle(color).fixedSize(horizontal: false, vertical: true)
    }
}
