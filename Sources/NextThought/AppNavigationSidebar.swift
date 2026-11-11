import SwiftUI

struct AppNavigationBar: View {
    static let meetingsSidebarKey = "meetingsContextSidebarHidden"
    static let foldersSidebarKey = "foldersContextSidebarHidden"

    @Environment(AppState.self) private var state
    @AppStorage(AppNavigationBar.meetingsSidebarKey, store: AppPreferences.standard) private var meetingsSidebarHidden = false
    @AppStorage(AppNavigationBar.foldersSidebarKey, store: AppPreferences.standard) private var foldersSidebarHidden = false
    private var updater: Updater { Updater.shared }

    private var showsContextSidebarToggle: Bool {
        state.section == .meetings || state.section == .library
    }

    private var contextSidebarHidden: Bool {
        state.section == .meetings ? meetingsSidebarHidden : foldersSidebarHidden
    }

    @Namespace private var selection

    /// The window's title row: the three places in the middle, the tools on the right.
    var body: some View {
        ZStack {
            HStack(spacing: 2) {
                ForEach(AppSection.primary) { section in
                    navigationButton(section)
                }
            }
            .padding(3)
            .background(Ink.sunken, in: Capsule())

            HStack(spacing: 4) {
                // Room for the window's traffic-light buttons.
                Color.clear.frame(width: 64, height: 1)
                Spacer(minLength: 12)
                if showsContextSidebarToggle {
                    Button {
                        if state.section == .meetings {
                            meetingsSidebarHidden.toggle()
                        } else if state.section == .library {
                            foldersSidebarHidden.toggle()
                        }
                    } label: {
                        Image(systemName: "sidebar.left")
                            .font(.system(size: 14)).frame(width: 32, height: 32)
                            .contentShape(Circle())
                    }
                    .buttonStyle(NavigationTopButtonStyle(selected: contextSidebarHidden))
                    .accessibilityLabel(contextSidebarHidden ? "显示侧栏" : "隐藏侧栏")
                    .accessibilityIdentifier("appNavigation.toggleContextSidebar")
                    .help(contextSidebarHidden ? "显示当前栏目侧栏" : "隐藏当前栏目侧栏")
                    .disabled(state.isRunning)
                }
                if updater.phase == .ready, let available = updater.available {
                    Button { updater.installAndRelaunch() } label: {
                        Label("重启以更新", systemImage: "arrow.down.circle").font(Typo.label)
                            .padding(.horizontal, 10).frame(height: 32)
                    }
                    .buttonStyle(NavigationTopButtonStyle())
                    .disabled(state.isRunning)
                    .help(state.isRunning ? "停止辅助后再安装 \(available.displayVersion)" : "安装 \(available.displayVersion) 并重新打开")
                }
                // The gear opens the quick panel; ⌘, opens the full settings page.
                Button { state.toggleQuickSettings() } label: {
                    Image(systemName: "gearshape")
                        .font(.system(size: 14)).frame(width: 32, height: 32)
                        .contentShape(Circle())
                }
                .buttonStyle(NavigationTopButtonStyle())
                .quickSettingsPopover()
                .accessibilityLabel("设置")
                .accessibilityIdentifier("appNavigation.settings")
                .help("设置（全部设置 ⌘,）")
            }
        }
        .padding(.horizontal, 14)
        .frame(maxWidth: .infinity, minHeight: 52, maxHeight: 52)
    }

    private func navigationButton(_ section: AppSection) -> some View {
        let selected = state.section == section
        return Button { state.select(section) } label: {
            Text(section.title)
                .font(Typo.label)
                .lineLimit(1)
                .foregroundStyle(selected ? Ink.ink : Ink.secondary)
                .padding(.horizontal, 16)
                .frame(height: 28)
                .background {
                    if selected {
                        Capsule().fill(Ink.surfaceRaised)
                            .shadow(color: Ink.contactShadow, radius: 1.5, x: 0, y: 1)
                            .matchedGeometryEffect(id: "selection", in: selection)
                    }
                }
                .contentShape(Capsule())
        }
        .buttonStyle(.plain)
        .animation(Motion.snappy, value: state.section)
        .accessibilityLabel(section.title)
        .accessibilityAddTraits(selected ? .isSelected : [])
        .accessibilityIdentifier("appNavigation.\(section.rawValue)")
    }
}

private struct NavigationTopButtonStyle: ButtonStyle {
    var selected = false

    func makeBody(configuration: Configuration) -> some View {
        TopButtonBody(configuration: configuration, selected: selected)
    }

    private struct TopButtonBody: View {
        @Environment(\.isEnabled) private var isEnabled
        @State private var hovering = false
        let configuration: ButtonStyleConfiguration
        let selected: Bool

        var body: some View {
            configuration.label
                .foregroundStyle(isEnabled ? (selected || hovering ? Ink.ink : Ink.secondary) : Ink.tertiary)
                .background(configuration.isPressed || hovering ? Ink.hover : Color.clear, in: Capsule())
                .onHover { hovering = $0 }
                .animation(Motion.hover, value: hovering)
        }
    }
}

/// Native selection rows expose one accessible destination per row.
struct AppNavigationSidebar: View {
    @Environment(AppState.self) private var state
    var body: some View {
        List(selection: Binding<AppSection?>(
            get: { state.section },
            set: { if let section = $0 { state.select(section) } }
        )) {
            ForEach(AppSection.allCases) { section in
                Label(section.title, systemImage: section.symbol)
                    .font(Typo.label)
                    .frame(minHeight: 32)
                    .tag(section)
                    .accessibilityLabel(section.title)
                    .accessibilityIdentifier("appNavigation.\(section.rawValue)")
            }
        }
        .listStyle(.sidebar)
        .navigationTitle("NextThought")
    }
}
