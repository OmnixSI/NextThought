import SwiftUI

struct AppNavigationBar: View {
    static let meetingsSidebarKey = "meetingsContextSidebarHidden"
    static let foldersSidebarKey = "foldersContextSidebarHidden"

    @Environment(AppState.self) private var state
    @AppStorage(AppNavigationBar.meetingsSidebarKey) private var meetingsSidebarHidden = false
    @AppStorage(AppNavigationBar.foldersSidebarKey) private var foldersSidebarHidden = false
    private var updater: Updater { Updater.shared }

    private var showsContextSidebarToggle: Bool {
        state.section == .meetings || state.section == .library
    }

    private var contextSidebarHidden: Bool {
        state.section == .meetings ? meetingsSidebarHidden : foldersSidebarHidden
    }

    var body: some View {
        HStack(spacing: 8) {
            HStack(spacing: 8) {
                Image(systemName: "waveform")
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundStyle(Ink.ink)
                    .frame(width: 28, height: 28)
                    .background(Ink.hover, in: RoundedRectangle(cornerRadius: 8, style: .continuous))
                Text("InterviewHacker")
                    .font(.system(size: 13, weight: .semibold))
                    .lineLimit(1)
            }
            .padding(.trailing, 12)

            ForEach(AppSection.primary) { section in
                navigationButton(section)
            }

            Spacer(minLength: 12)

            Group {
                if showsContextSidebarToggle {
                    Button {
                        if state.section == .meetings {
                            meetingsSidebarHidden.toggle()
                        } else if state.section == .library {
                            foldersSidebarHidden.toggle()
                        }
                    } label: {
                        Label(contextSidebarHidden ? "显示侧栏" : "隐藏侧栏", systemImage: "sidebar.left")
                            .font(Typo.label)
                            .lineLimit(1)
                            .padding(.horizontal, 10)
                            .frame(height: 36)
                            .background(contextSidebarHidden ? Ink.hover : Color.clear,
                                        in: RoundedRectangle(cornerRadius: 8, style: .continuous))
                            .contentShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                    }
                    .buttonStyle(NavigationTopButtonStyle(selected: contextSidebarHidden))
                    .accessibilityLabel(contextSidebarHidden ? "显示侧栏" : "隐藏侧栏")
                    .accessibilityIdentifier("appNavigation.toggleContextSidebar")
                    .help(contextSidebarHidden ? "显示当前栏目侧栏" : "隐藏当前栏目侧栏")
                    .disabled(state.isRunning)
                } else {
                    Color.clear.frame(height: 36).accessibilityHidden(true)
                }
            }
            .frame(width: 108, alignment: .trailing)

            if updater.phase == .ready, let available = updater.available {
                Button { updater.installAndRelaunch() } label: {
                    Label("重启以更新", systemImage: "arrow.down.circle")
                }
                .buttonStyle(NavigationTopButtonStyle())
                .disabled(state.isRunning)
                .help(state.isRunning ? "停止辅助后再安装 \(available.displayVersion)" : "安装 \(available.displayVersion) 并重新打开")
            }

            navigationButton(.settings)
        }
        .padding(.horizontal, 18)
        .frame(maxWidth: .infinity, minHeight: 54, maxHeight: 54, alignment: .leading)
        .background(Ink.paper)
        .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 1) }
    }

    private func navigationButton(_ section: AppSection) -> some View {
        let selected = state.section == section
        return Button { state.select(section) } label: {
            Label(section.title, systemImage: section.symbol)
                .font(Typo.label)
                .lineLimit(1)
                .padding(.horizontal, 12)
                .frame(height: 36)
                .background(selected ? Ink.hover : Color.clear,
                            in: RoundedRectangle(cornerRadius: 8, style: .continuous))
                .contentShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        }
        .buttonStyle(NavigationTopButtonStyle(selected: selected))
        .accessibilityLabel(section.title)
        .accessibilityAddTraits(selected ? .isSelected : [])
        .accessibilityIdentifier("appNavigation.\(section.rawValue)")
        .help(Text(section.title))
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
                .background(configuration.isPressed ? Ink.hover : Color.clear,
                            in: RoundedRectangle(cornerRadius: 8, style: .continuous))
                .onHover { hovering = $0 }
                .animation(Motion.hover, value: hovering)
        }
    }
}
