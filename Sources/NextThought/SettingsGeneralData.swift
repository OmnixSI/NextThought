import AppKit
import AVFoundation
import SwiftUI
import NextThoughtCore

// MARK: - 通用

struct GeneralSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            SettingsSection(title: "语言与外观", icon: "paintbrush") {
                LanguageSettingsSection(compact: true)
                Divider()
                MenuPicker(title: "外观", selection: Binding(get: { state.appearance }, set: { state.setAppearance($0) }),
                           options: [("system", String(localized: "跟随系统")),
                                     ("light", String(localized: "浅色")), ("dark", String(localized: "深色"))])
                    .accessibilityIdentifier("settings.appearance")
            }
            SettingsSection(title: "快捷键", icon: "keyboard") {
                ShortcutSettingsView()
                ForEach(state.shortcutErrors, id: \.self) { Note($0, color: .orange) }
            }
            CompanionSection()
            UpdateSettings()
        }
    }
}

// MARK: - 数据与权限

struct DataSettings: View {
    @Environment(AppState.self) private var state
    @State private var includeSecrets = false
    @State private var applySettings = true
    @State private var backups: [DataBackup.BackupFile] = []
    @State private var restoring: DataBackup.BackupFile?

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
            SettingsSection(title: "记录每次会议", icon: "tray.full") {
                VStack(alignment: .leading, spacing: 3) {
                    Toggle("记录每次会议", isOn: $state.recordSessions)
                        .disabled(state.isRunning)
                        .onChange(of: state.recordSessions) { _, _ in state.updateSettings() }
                    Note("保存转录、识别到的问题、会中回答和当时的系列资料。转录边说边存在本机，不保存原始音频。生成纪要与复盘会把本次转录发送给当前回答服务，并使用其额度。", presentation: .inline)
                }
            }

            SettingsSection(title: "备份与恢复", icon: "externaldrive") {
                HStack {
                    Button("导出全部数据…") { state.exportAllData(includeSecrets: includeSecrets) }.buttonStyle(PrimaryButtonStyle()).disabled(state.isDataBusy || state.isRunning)
                    Button("导入…") { state.importData(applySettings: applySettings) }.buttonStyle(SecondaryButtonStyle()).disabled(state.isDataBusy || state.isRunning)
                    Button("立即备份") { state.backupNow(); refreshSoon() }.buttonStyle(SecondaryButtonStyle()).disabled(state.isDataBusy)
                }
                Note("导入会合并，不删除现有数据。备份包含项目资料、经验积累、会议记录、个人笔记、模板和设置；先预览再恢复，项目按修改时间合并，记录与模板冲突由你选择。", presentation: .inline)
                if let latest = backups.first {
                    Note("最近备份：" + latest.date.formatted(date: .abbreviated, time: .shortened), presentation: .inline)
                }
                AdvancedOptions("data-transfer", title: "导入导出选项") {
                    Toggle("包含 API Key 与访问令牌", isOn: $includeSecrets)
                    Toggle("同时导入设置（回答服务、识别、快捷键等）", isOn: $applySettings)
                    if includeSecrets { Note("导出文件里的密钥是明文，请妥善保管，不要发给别人。", color: .orange) }
                }
                if state.isDataBusy { ProgressView().controlSize(.small) }
                if !state.dataStatus.isEmpty {
                    OperationFeedback(message: state.dataStatus, tone: state.dataStatusTone)
                    if state.dataStatusCanRetryBackup && state.dataStatusTone == .failure {
                        Button("重试备份") { state.backupNow() }
                            .buttonStyle(SecondaryButtonStyle()).disabled(state.isDataBusy || state.isRunning)
                    }
                }
                if let issue = state.backupIssue {
                    OperationFeedback(message: issue, tone: .failure)
                    Button("重试备份") { state.backupNow() }
                        .buttonStyle(SecondaryButtonStyle()).disabled(state.isDataBusy || state.isRunning)
                }
                if let warning = state.recoveryPointWarning { OperationFeedback(message: warning, tone: .warning) }
            }

            SettingsSection(title: "自动备份", icon: "clock.arrow.circlepath") {
                Toggle("会议结束后自动创建恢复点", isOn: $state.autoBackup)
                    .onChange(of: state.autoBackup) { _, _ in state.updateSettings() }
                if backups.isEmpty {
                    Note("还没有自动备份。", presentation: .inline)
                } else {
                    ForEach(backups.prefix(10)) { backup in
                        HStack {
                            Image(systemName: "archivebox").foregroundStyle(.secondary)
                            Text(backup.date.formatted(date: .abbreviated, time: .shortened))
                            Text(reasonTitle(backup.reason)).foregroundStyle(.secondary)
                            Text(ByteCountFormatter.string(fromByteCount: Int64(backup.size), countStyle: .file)).foregroundStyle(.tertiary)
                            Spacer()
                            Button("恢复…") { restoring = backup }.buttonStyle(ChipButtonStyle()).disabled(state.isRunning)
                            Button { NSWorkspace.shared.activateFileViewerSelecting([backup.url]) } label: { Image(systemName: "folder") }
                                .buttonStyle(.borderless).accessibilityLabel("在访达中显示")
                        }
                    }
                }
                HStack {
                    Button("打开数据文件夹") {
                        try? FileManager.default.createDirectory(at: AppPaths.root, withIntermediateDirectories: true)
                        NSWorkspace.shared.open(AppPaths.root)
                    }.buttonStyle(SecondaryButtonStyle())
                    Spacer(minLength: 0)
                }
                Note(String(localized: "数据保存在 \(AppPaths.root.path)。恢复点最多保留最近 10 份、软预算 2 GiB，不含模型、索引或密钥。"), presentation: .inline)
            }

            PermissionSettingsSection()
            MaintenanceSettingsSection()
        }
        .onAppear { backups = DataBackup.listBackups() }
        .onReceive(NotificationCenter.default.publisher(for: DataBackup.didChangeNotification)) { _ in
            backups = DataBackup.listBackups()
        }
        .onReceive(NotificationCenter.default.publisher(for: NSWindow.didBecomeKeyNotification)) { notification in
            if (notification.object as? NSWindow)?.identifier == SettingsWindow.identifier {
                backups = DataBackup.listBackups()
            }
        }
        .confirmationDialog("从这份备份恢复？", isPresented: Binding(get: { restoring != nil }, set: { if !$0 { restoring = nil } }), titleVisibility: .visible) {
            Button("查看恢复预览") { if let restoring { state.importArchive(restoring.url, applySettings: false) }; restoring = nil; refreshSoon() }
            Button("取消", role: .cancel) { restoring = nil }
        } message: {
            Text("先查看新增、相同和冲突内容；确认所选内容后才会应用。冲突默认保留本机，应用前创建恢复点。")
        }
    }

    private func refreshSoon() {
        Task { try? await Task.sleep(for: .seconds(2)); backups = DataBackup.listBackups() }
    }

    private func reasonTitle(_ reason: String) -> String {
        switch reason {
        case "daily": String(localized: "旧每日备份")
        case "session": String(localized: "会议结束")
        case "update": String(localized: "更新前")
        case "import": String(localized: "导入前")
        case "manual": String(localized: "手动")
        default: String(localized: String.LocalizationValue(reason))
        }
    }
}

/// The one place permissions are repaired (besides the prompt a feature shows when it needs one).
private struct PermissionSettingsSection: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let camera = state.coach.cameraAccess
        SettingsSection(title: "权限", icon: "lock.shield") {
            VStack(spacing: Space.s2) {
                SettingsStatusRow(
                    tone: state.capturePermissionGranted ? .ready : .caution,
                    title: "录屏与系统音频",
                    detail: state.capturePermissionGranted ? "已允许 · 可以听到会议里对方的声音" : "未允许 · 听会议声音和读屏幕都需要它",
                    actionTitle: state.capturePermissionGranted ? nil : "打开系统设置",
                    action: { state.openCapturePermissionSettings() })
                SettingsStatusRow(
                    tone: state.microphoneAccess == .granted ? .ready : (state.microphoneAccess == .undetermined ? .idle : .caution),
                    title: "麦克风",
                    detail: state.microphoneAccess == .granted ? "已允许 · 你说的话标记为“我”"
                        : state.microphoneAccess == .undetermined ? "第一次使用时才会询问" : "未允许 · 只记录对方的声音",
                    actionTitle: state.microphoneAccess == .granted ? nil : (state.microphoneAccess == .undetermined ? "允许" : "打开系统设置"),
                    action: {
                        if state.microphoneAccess == .undetermined { Task { await state.requestMicrophoneAccess() } }
                        else { state.openMicrophoneSettings() }
                    })
                SettingsStatusRow(
                    tone: camera == .authorized ? .ready : (camera == .notDetermined ? .idle : .caution),
                    title: "摄像头",
                    detail: camera == .authorized ? "已允许 · 临场教练用它留意眼神，画面不保存、不上传"
                        : camera == .notDetermined ? "打开“留意眼神”时才会询问" : "未允许 · 只影响临场教练的眼神提示",
                    actionTitle: camera == .authorized || camera == .notDetermined ? nil : "打开系统设置",
                    action: {
                        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Camera") { NSWorkspace.shared.open(url) }
                    })
            }
            HStack {
                Note("系统权限绑定在应用签名上；绑定本机签名后，以后更新不必重新授权。", presentation: .inline)
                Spacer(minLength: 8)
                Button("刷新") { Task { await state.refreshSetupStatus() } }.buttonStyle(ChipButtonStyle())
            }
        }
    }
}

/// Everyday maintenance, in plain sight: the guide, one-click setup, checks and a few switches.
private struct MaintenanceSettingsSection: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "维护", icon: "wrench.and.screwdriver") {
            HStack(spacing: 8) {
                Button("重新打开新手引导") { state.openOnboarding(); MainWindowLayout.bringToFront() }
                    .buttonStyle(SecondaryButtonStyle())
                Button { Task { await state.runQuickEnvironmentSetup() } } label: {
                    Label("一键配置", systemImage: "wand.and.stars")
                }
                .buttonStyle(PrimaryButtonStyle())
                .disabled(state.isRunningEnvironmentSetup)
                .help("检查回答服务、选择模型、准备语音识别、刷新音频来源并重建资料索引。缺少本地语音模型时会在后台下载，不影响使用")
                Button { Task { await state.rebuildEnvironment() } } label: {
                    Label("一键重建环境", systemImage: "arrow.triangle.2.circlepath")
                }
                .buttonStyle(SecondaryButtonStyle())
                .disabled(state.isRunningEnvironmentSetup)
                .help("重新读取配置、重建资料索引与数据目录，不改变你的选择")
                if state.isRunningEnvironmentSetup { ProgressView().controlSize(.small) }
                Spacer(minLength: 0)
            }
            Note("一键配置会检查并配好回答服务、语音模型、音频来源和资料索引；哪一步没成功会直接写在下面。", presentation: .inline)
            if !state.environmentSetupSteps.isEmpty {
                VStack(alignment: .leading, spacing: 6) {
                    ForEach(state.environmentSetupSteps) { step in
                        HStack(alignment: .top, spacing: 8) {
                            Image(systemName: step.status.symbol)
                                .font(.system(size: 12))
                                .foregroundStyle(stepColor(step.status))
                                .frame(width: 16)
                            VStack(alignment: .leading, spacing: 1) {
                                Text(step.title).font(.system(size: 12.5, weight: .medium))
                                if !step.detail.isEmpty {
                                    Text(step.detail).font(.system(size: 11)).foregroundStyle(.secondary)
                                        .fixedSize(horizontal: false, vertical: true)
                                }
                            }
                            Spacer(minLength: 0)
                        }
                    }
                }
            }
            if !state.environmentSetupSummary.isEmpty {
                Note(state.environmentSetupSummary, color: state.environmentSetupSummary.hasPrefix("有 ") ? .orange : .green)
            }
            Divider()
            Text("使用前检查").font(Typo.label)
            SetupChecklist(exclude: ["capture", "mic", "signing"])
            Divider()
            SettingToggle(title: "减少视觉特效", help: "关闭液态玻璃的实时折射，改用平面半透明效果，更流畅、更省电。系统“降低透明度”开启时会自动使用。", isOn: $state.reducedEffects)
                .onChange(of: state.reducedEffects) { _, value in AppPreferences.standard.set(value, forKey: "reducedEffects") }
            SettingToggle(title: "工作台置顶", help: "只在 NextThought 处于前台时生效；切换到其他 App 后会降回普通层级。", isOn: $state.keepOnTop)
                .onChange(of: state.keepOnTop) { _, _ in state.updateSettings() }
            Divider()
            SignatureStatus()
        }
    }

    /// Status colours of the one-click setup steps: green for finished, orange
    /// for a step that needs attention, brand colour while it runs.
    @MainActor private func stepColor(_ status: EnvironmentSetup.Step.Status) -> Color {
        switch status {
        case .done: .green
        case .failed: .orange
        case .running: Brand.accent
        case .pending, .skipped: .secondary
        }
    }
}
