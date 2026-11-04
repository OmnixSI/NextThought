import AppKit
import Darwin
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

    /// A confirmation action must start at most one relaunch helper. This also
    /// protects against duplicate accessibility events or a rapid double click.
    @MainActor private static var isRelaunchInProgress = false

    /// Quits and opens the same app bundle again a moment later.
    @MainActor
    static func relaunch(language: AppLanguage) {
        guard !isRelaunchInProgress else { return }
        isRelaunchInProgress = true
        guard canRelaunch else { NSApp.terminate(nil); return }
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/sh")
        let currentPID = String(ProcessInfo.processInfo.processIdentifier)
        let waitForCurrentProcess = "while /bin/kill -0 \"$2\" 2>/dev/null; do /bin/sleep 0.1; done; "
        let command = language == .system
            ? "\(waitForCurrentProcess)/usr/bin/open -n \"$0\""
            : "\(waitForCurrentProcess)/usr/bin/open -n \"$0\" --args -AppleLanguages \"($1)\""
        process.arguments = ["-c", command, Bundle.main.bundlePath, language.rawValue, currentPID]
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
    private let compact: Bool
    @State private var choice = AppLanguage.current
    @State private var pending: AppLanguage?

    init(compact: Bool = false) {
        self.compact = compact
    }

    var body: some View {
        Group {
            if compact {
                controls
            } else {
                SettingsSection(title: "语言", icon: "globe") {
                    controls
                }
            }
        }
        .confirmationDialog("需要重新打开 App", isPresented: Binding(get: { pending != nil }, set: { if !$0 { pending = nil } }),
                            titleVisibility: .visible) {
            Button("重新打开") {
                guard let language = pending else { return }
                AppLanguage.apply(language)
                choice = language
                pending = nil
                AppLanguage.relaunch(language: language)
            }
            Button("取消", role: .cancel) { pending = nil }
        } message: {
            Text("切换语言后，App 会保存当前内容并自动重新打开。")
        }
    }

    private var controls: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 8) {
                if compact {
                    Image(systemName: "globe")
                        .font(.system(size: 13, weight: .regular))
                        .foregroundStyle(Ink.secondary)
                        .frame(width: 18)
                    Text("语言")
                        .font(.system(size: 13, weight: .semibold))
                }
                MenuPicker(selection: Binding(get: { choice }, set: { value in
                    if value != choice { pending = value }
                }), options: [(.system, String(localized: "跟随系统")),
                              (.simplifiedChinese, String(localized: "简体中文")),
                              (.english, "English")])
                    .accessibilityLabel("语言")
                    .accessibilityValue(Text(choice.title))
                    .disabled(state.isRunning)
                    .help(LocalizedStringKey(state.isRunning ? "会议进行中不能切换语言。" : "选择界面语言"))
                Hint("整个 App 的界面语言。切换后 App 会自动重新打开。建议回答跟着对话的语言走，不受这里影响。", presentation: .info)
                    .accessibilityLabel("语言说明")
            }
            if state.isRunning {
                Hint("会议进行中不能切换语言。", presentation: .inline)
            }
        }
    }
}

/// A localized note line (the settings `Note` shows its string verbatim).
struct Hint: View {
    var key: LocalizedStringKey
    var color: Color = Ink.secondary
    var presentation: NotePresentation
    @State private var showingInfo = false
    @State private var isHovering = false
    @FocusState private var infoFocused: Bool

    init(_ key: LocalizedStringKey, color: Color = Ink.secondary, presentation: NotePresentation = .inline) {
        self.key = key
        self.color = color
        self.presentation = presentation
    }

    var body: some View {
        Group {
            if presentation == .inline {
                Text(key).font(.system(size: 11.5)).foregroundStyle(color).fixedSize(horizontal: false, vertical: true)
            } else {
                Button { showingInfo.toggle() } label: {
                    Image(systemName: "info.circle")
                        .font(.system(size: 12))
                        .foregroundStyle(color)
                        .frame(width: Hit.minHeight, height: Hit.minHeight)
                        .background(isHovering || infoFocused ? Ink.hover : Color.clear, in: Circle())
                        .overlay(Circle().strokeBorder(infoFocused ? Ink.focus : Color.clear, lineWidth: 1.5))
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .focused($infoFocused)
                .onHover { isHovering = $0 }
                .help(key)
                .accessibilityLabel("查看说明")
                .accessibilityHint(Text(key))
                .popover(isPresented: $showingInfo, arrowEdge: .trailing) {
                    Text(key)
                        .font(.system(size: 12))
                        .foregroundStyle(.primary)
                        .frame(width: 280, alignment: .leading)
                        .fixedSize(horizontal: false, vertical: true)
                        .padding(12)
                }
            }
        }
    }
}
