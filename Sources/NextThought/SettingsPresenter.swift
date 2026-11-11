import AppKit
import AVFoundation
import SwiftUI
import NextThoughtCore

// MARK: - Presenter

struct PresenterSettings: View {
    @Environment(AppState.self) private var state
    @State private var calibration: Bool?
    @State private var startedPreview = false

    var body: some View {
        @Bindable var state = state
        @Bindable var cue = CueIslandController.shared.model
        @Bindable var coach = state.coach
        VStack(alignment: .leading, spacing: 12) {
        SettingsSection(title: "提词", icon: "rectangle.topthird.inset.filled") {
            MenuPicker(title: "显示方式", selection: $cue.surface,
                       options: CueSurface.allCases.map { ($0, $0.title) })
            if cue.surface == .island {
                MenuPicker(title: "宽度", selection: $cue.width,
                           options: CueWidth.allCases.map { ($0, $0.title) })
                MenuPicker(title: "摄像头所在的屏幕", selection: $cue.screenName,
                           options: [("", String(localized: "自动（内建显示器）"))]
                                + NSScreen.screens.map { ($0.localizedName, $0.localizedName) })
                HStack {
                    if cue.preview {
                        Button("结束预览") { CueIslandController.shared.hide() }.buttonStyle(SecondaryButtonStyle())
                    } else {
                        Button("预览提词岛") {
                            cue.preview = true
                            startedPreview = true
                            CueIslandController.shared.show()
                        }.buttonStyle(PrimaryButtonStyle())
                    }
                }
            } else {
                Button("显示提词浮窗") { TeleprompterController.shared.showWindow() }.buttonStyle(PrimaryButtonStyle())
                HStack {
                    Text("移动到")
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
                Toggle("隐藏背景，只保留文字、边框与按钮", isOn: $state.teleprompterHideBackground)
                    .onChange(of: state.teleprompterHideBackground) { _, _ in state.saveAnswerSettings() }
                Toggle("鼠标穿透（点击穿过提词浮窗）", isOn: Binding(get: { state.clickThrough }, set: { _ in state.toggleClickThrough() }))
                Note("手机显示与遥控在 设置 → 通用 → 手机伴侣。隐藏背景时，不透明度不会影响文字与按钮。")
            }
            HStack {
                Text("字号")
                Slider(value: $state.teleprompterFontSize, in: 14...36, step: 1)
                    .frame(maxWidth: 320)
                    .onChange(of: state.teleprompterFontSize) { _, _ in state.saveAnswerSettings() }
                Text("\(Int(state.teleprompterFontSize))").font(.caption.monospacedDigit()).frame(width: 36)
            }
            if CueIslandController.shared.model.surface == .island {
                Toggle("不开会时，鼠标停在刘海上可以直接开始一场", isOn: Binding(
                    get: { CueIslandController.shared.model.launcher },
                    set: { CueIslandController.shared.model.launcher = $0 }))
            }
            Note("提词岛不抢键盘焦点；鼠标停在上面才会展开会议控制和按钮，其余时候点击会穿过它。每个场景用要点还是逐句，在“场景”页设置。", presentation: .inline)
        }

        SettingsSection(title: "临场教练", icon: "figure.wave") {
            Toggle("会中轻提示：语速、说得太久、发言占比", isOn: $coach.isEnabled)
            Toggle("留意眼神是否离开镜头（使用摄像头）", isOn: $coach.watchesGaze)
                .onChange(of: coach.watchesGaze) { _, on in
                    calibration = nil
                    if on { state.coach.startWatching() }
                }
            if coach.watchesGaze {
                HStack(spacing: 8) {
                    StatusDot(tone: cameraTone(coach.cameraAccess))
                    Text("摄像头权限").foregroundStyle(.secondary)
                    Text(cameraText(coach.cameraAccess))
                }
                .font(.callout)
                HStack(spacing: 8) {
                    StatusDot(tone: gazeTone(coach.gaze?.state))
                    Text("当前").foregroundStyle(.secondary)
                    Text(gazeText(coach.gaze?.state))
                }
                .font(.callout)
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
            Note("画面只在这台 Mac 上分析成几个特征点，不保存、不上传。提词岛就在摄像头下方，读提词时视线偏移很小，对方看到的是你在看镜头。", presentation: .inline)
        }

        SettingsSection(title: "屏幕共享", icon: "eye.slash") {
            Toggle("会议软件共享提词岛所在的屏幕时，自动收回刘海", isOn: Binding(
                get: { CueIslandController.shared.model.avoidsSharing },
                set: { CueIslandController.shared.model.avoidsSharing = $0 }))
            Toggle("尽力从屏幕共享中排除本应用窗口", isOn: $state.capturePrivacy)
                .onChange(of: state.capturePrivacy) { _, _ in state.saveAnswerSettings(); state.updateSettings() }
            Note(String(localized: "共享某个窗口或另一块屏幕时，提词岛不在画面里。共享它所在的整块屏幕时，macOS 15.4 起系统不再让应用把自己排除在外，所以提词岛会在识别到 Zoom、Teams、腾讯会议、飞书、钉钉或 Webex 的整屏共享后自动收起；其他软件请先用下面的测试看一眼。\(state.shortcutLabel(.panicHide)) 可随时隐藏全部窗口。"), presentation: .inline)
            Button("共享可见性测试") { state.runShareVisibilityTest() }.buttonStyle(SecondaryButtonStyle())
        }
                SubtitleBarSettingsSection()
        }
        .onDisappear {
            guard !state.isRunning else { return }
            state.coach.stopWatching()
            if startedPreview || CueIslandController.shared.model.preview {
                CueIslandController.shared.hide()
                startedPreview = false
            }
        }
    }

    private func cameraText(_ status: AVAuthorizationStatus) -> String {
        switch status {
        case .authorized: String(localized: "已允许")
        case .notDetermined: String(localized: "等待允许")
        default: String(localized: "未允许，请到 设置 → 数据与权限 → 权限 打开")
        }
    }
    private func cameraTone(_ status: AVAuthorizationStatus) -> StatusDot.Tone {
        switch status {
        case .authorized: .ready
        case .notDetermined: .idle
        default: .caution
        }
    }
    private func gazeText(_ gaze: GazeReading.State?) -> String {
        switch gaze {
        case .onCamera: String(localized: "正在看镜头")
        case .away: String(localized: "视线离开了镜头")
        case .noFace: String(localized: "没有检测到人脸")
        case nil: String(localized: "正在启动…")
        }
    }
    private func gazeTone(_ gaze: GazeReading.State?) -> StatusDot.Tone {
        switch gaze {
        case .onCamera: .ready
        case .away: .caution
        default: .idle
        }
    }
}


