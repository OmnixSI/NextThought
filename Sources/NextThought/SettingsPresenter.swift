import AppKit
import AVFoundation
import SwiftUI
import NextThoughtCore

// MARK: - 提词与教练

/// The cue, the live coach, screen sharing, the subtitle bar and shortcuts. Placement,
/// opacity, click-through and the preview are in 高级 → 提词.
struct PresenterSettings: View {
    @Environment(AppState.self) private var state
    @Environment(\.settingsNavigate) private var navigate
    @State private var calibration: Bool?

    var body: some View {
        @Bindable var state = state
        @Bindable var cue = CueIslandController.shared.model
        @Bindable var coach = state.coach
        @Bindable var presenter = PresenterMode.shared
        VStack(alignment: .leading, spacing: 0) {
            SettingsSection(title: "提词") {
                MenuPicker(title: "显示方式", selection: $cue.surface,
                           options: CueSurface.allCases.map { ($0, $0.title) })
                Toggle("会中一直展开提词岛，不用把鼠标移上去", isOn: $cue.alwaysOpen)
                if cue.surface == .island {
                    MenuPicker(title: "宽度", selection: $cue.width,
                               options: CueWidth.allCases.map { ($0, $0.title) })
                    // Only a second display makes the choice meaningful.
                    if NSScreen.screens.count > 1 {
                        MenuPicker(title: "摄像头所在的屏幕", selection: $cue.screenName,
                                   options: [("", String(localized: "自动（内建显示器）"))]
                                        + NSScreen.screens.map { ($0.localizedName, $0.localizedName) })
                    }
                    Toggle("不开会时，鼠标停在刘海上可以直接开始一场", isOn: Binding(
                        get: { CueIslandController.shared.model.launcher },
                        set: { CueIslandController.shared.model.launcher = $0 }))
                } else {
                    HStack {
                        Button("显示提词浮窗") { TeleprompterController.shared.showWindow() }.buttonStyle(SecondaryButtonStyle())
                        Spacer(minLength: 0)
                    }
                    Toggle("隐藏背景，只保留文字、边框与按钮", isOn: $state.teleprompterHideBackground)
                        .onChange(of: state.teleprompterHideBackground) { _, _ in state.saveAnswerSettings() }
                }
                HStack {
                    Text("字号")
                    Slider(value: $state.teleprompterFontSize, in: 14...36, step: 1)
                        .frame(maxWidth: 320)
                        .onChange(of: state.teleprompterFontSize) { _, _ in state.saveAnswerSettings() }
                    Text("\(Int(state.teleprompterFontSize))").font(.caption.monospacedDigit()).frame(width: 36)
                }
                Note("每个场景用要点还是逐句，在“场景”页设置。", presentation: .inline)
            }

            SettingsSection(title: "双屏演讲") {
                Toggle("外接屏放幻灯片，MacBook 看提示", isOn: $presenter.isEnabled)
                if presenter.otherScreens.count > 1 {
                    MenuPicker(title: "幻灯片所在的屏幕", selection: $presenter.slidesScreenName,
                               options: [("", String(localized: "自动"))] + presenter.otherScreens.map { ($0.localizedName, $0.localizedName) })
                }
                Note(String(localized: "会议开始后：提词岛在 MacBook 的摄像头下方一直展开，主窗口让开，不挡演讲者视图；NextThought 读取幻灯片所在的屏幕，跟着你翻页，显示这一页的要点和下一页的过渡；外接屏上不会出现 NextThought 的任何窗口。要跟随幻灯片，先把这份幻灯片（PPT、Keynote 或 PDF）放进这个系列的资料。主窗口可用 \(state.shortcutLabel(.toggleLiveWindow)) 叫回。"), presentation: .inline)
                Text(verbatim: presenter.summary).font(Typo.caption).foregroundStyle(Ink.secondary)
            }

            SettingsSection(title: "临场教练") {
                Toggle("会中轻提示：语速、说得太久、发言占比", isOn: $coach.isEnabled)
                Toggle("留意眼神是否离开镜头（使用摄像头）", isOn: $coach.watchesGaze)
                    .onChange(of: coach.watchesGaze) { _, on in
                        calibration = nil
                        if on { state.coach.startWatching() }
                    }
                if coach.watchesGaze {
                    if coach.cameraAccess == .denied || coach.cameraAccess == .restricted {
                        HStack(spacing: Space.s2) {
                            Note("摄像头还没有允许。", color: Ink.caution, presentation: .inline)
                            Button("去允许") { navigate(.general, "权限") }.buttonStyle(ChipButtonStyle())
                        }
                    }
                    MenuPicker(title: "灵敏度", selection: $coach.gazeSensitivity,
                               options: GazeSensitivity.allCases.map { ($0, $0.title) })
                    HStack(spacing: 10) {
                        Button("校准：看着摄像头 2 秒") {
                            calibration = nil
                            Task { calibration = await state.coach.calibrate() }
                        }
                        .buttonStyle(SecondaryButtonStyle())
                        .disabled(coach.isCalibrating || coach.cameraAccess != .authorized)
                        if coach.isCalibrating {
                            ProgressView().controlSize(.small)
                            Text("正在校准，请看着摄像头…").font(.callout).foregroundStyle(.secondary)
                        } else if let calibration {
                            Text(calibration ? "已校准" : "没有看清眼睛，请正对摄像头再试一次")
                                .font(.callout).foregroundStyle(.secondary)
                        } else if coach.isCalibrated {
                            Text("已校准过").font(.callout).foregroundStyle(.secondary)
                        }
                    }
                }
                Note("画面只在这台 Mac 上分析，不保存、不上传。", presentation: .inline)
            }

            SettingsSection(title: "屏幕共享") {
                Toggle("会议软件共享提词岛所在的屏幕时，自动收回刘海", isOn: Binding(
                    get: { CueIslandController.shared.model.avoidsSharing },
                    set: { CueIslandController.shared.model.avoidsSharing = $0 }))
                Toggle("尽力从屏幕共享中排除本应用窗口", isOn: $state.capturePrivacy)
                    .onChange(of: state.capturePrivacy) { _, _ in state.saveAnswerSettings(); state.updateSettings() }
                HStack {
                    Button("共享可见性测试") { state.runShareVisibilityTest() }.buttonStyle(SecondaryButtonStyle())
                    Spacer(minLength: 0)
                }
                Note(String(localized: "共享整块屏幕时，系统不再让应用把自己排除在外，提词岛只能靠自动收回；其他软件先点测试看一眼。\(state.shortcutLabel(.panicHide)) 可随时隐藏全部窗口。"), presentation: .inline)
            }

            SubtitleBarSettingsSection()

            ShortcutsSettingsRow()
        }
        .onDisappear {
            guard !state.isRunning else { return }
            state.coach.stopWatching()
        }
    }
}

/// 快捷键: one row that opens the editor in place.
private struct ShortcutsSettingsRow: View {
    @Environment(AppState.self) private var state
    @Environment(\.settingsSearchDestination) private var destination
    @State private var open = false

    var body: some View {
        SettingsSection(title: "快捷键", headed: false) {
            Button { withAnimation(Motion.expand) { open.toggle() } } label: {
                HStack {
                    Text("快捷键…").font(Typo.label)
                    Spacer(minLength: 0)
                    Image(systemName: "chevron.right").font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(Ink.secondary).rotationEffect(.degrees(open ? 90 : 0))
                }
                .padding(.vertical, Space.s1).contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .accessibilityValue(Text(open ? "已展开" : "已折叠"))
            .accessibilityIdentifier("settings.shortcuts")
            if open {
                ShortcutSettingsView()
                ForEach(state.shortcutErrors, id: \.self) { Note($0, color: .orange) }
            }
        }
        .onChange(of: destination, initial: true) { _, target in if target == "快捷键" { open = true } }
    }
}

// MARK: - 高级 → 提词

struct CueAdvanced: View {
    @Environment(AppState.self) private var state
    @State private var startedPreview = false

    var body: some View {
        @Bindable var state = state
        @Bindable var cue = CueIslandController.shared.model
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack {
                if cue.preview {
                    Button("结束预览") { CueIslandController.shared.hide() }.buttonStyle(SecondaryButtonStyle())
                } else {
                    Button("预览提词岛") {
                        cue.preview = true
                        startedPreview = true
                        CueIslandController.shared.show()
                    }.buttonStyle(SecondaryButtonStyle())
                }
                Text("提词岛不抢键盘焦点；鼠标停在上面才会展开按钮。").font(Typo.caption).foregroundStyle(Ink.secondary)
                Spacer(minLength: 0)
            }
            Divider()
            Text("提词浮窗").font(Typo.label)
            HStack {
                Text("移到")
                ForEach(TeleprompterController.Placement.allCases) { placement in
                    Button(placement.title) { TeleprompterController.shared.move(to: placement) }.buttonStyle(ChipButtonStyle())
                        .help("显示提词浮窗并移动到\(placement.title)")
                }
            }
            HStack {
                Text("不透明度")
                Slider(value: $state.teleprompterOpacity, in: 0.35...1).disabled(state.teleprompterHideBackground)
                    .frame(maxWidth: 320)
                    .onChange(of: state.teleprompterOpacity) { _, _ in state.saveAnswerSettings() }
                Text("\(Int(state.teleprompterOpacity * 100))%").font(.caption.monospacedDigit()).frame(width: 36)
            }
            Toggle("鼠标穿透（点击穿过提词浮窗）", isOn: Binding(get: { state.clickThrough }, set: { _ in state.toggleClickThrough() }))
            Note("隐藏背景时，不透明度不会影响文字与按钮。", presentation: .inline)
        }
        .onDisappear {
            if startedPreview || CueIslandController.shared.model.preview {
                CueIslandController.shared.hide()
                startedPreview = false
            }
        }
    }
}
