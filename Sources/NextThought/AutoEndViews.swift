import SwiftUI
import NextThoughtCore

/// 会中页的一条提示：好像没人说话了，倒计时后自动结束。只在提醒中出现；有人说话会自己消失。
struct AutoEndBanner: View {
    @Environment(AppState.self) private var state

    var body: some View {
        Group {
            if let deadline = state.autoEnd.deadline {
                TimelineView(.periodic(from: .now, by: 1)) { context in
                    let left = max(0, Int(deadline.timeIntervalSince(context.date).rounded(.up)))
                    HStack(spacing: Space.s3) {
                        Image(systemName: "moon.zzz")
                            .font(.system(size: 13)).foregroundStyle(Ink.secondary)
                            .accessibilityHidden(true)
                        Text("好像没人说话了，\(left) 秒后结束记录")
                            .font(Typo.body).foregroundStyle(Ink.ink).lineLimit(1)
                        Spacer(minLength: Space.s3)
                        Button("继续记录") { state.keepRecording() }
                            .buttonStyle(PrimaryButtonStyle())
                        Button("现在结束") { state.endMeetingAfterSilence() }
                            .buttonStyle(SecondaryButtonStyle())
                    }
                    .disabled(state.isChangingSession)
                    .padding(.horizontal, Space.s4)
                    .padding(.vertical, Space.s2)
                    .background(Ink.sunken)
                    .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 1) }
                    .accessibilityElement(children: .contain)
                    .accessibilityIdentifier("liveSession.autoEndBanner")
                }
                .transition(.opacity)
            }
        }
        .animation(Motion.expand, value: state.autoEnd.deadline != nil)
    }
}

/// 设置 → 纪要 → 会议自动收尾：会议没人说话时怎么办。
struct AutoEndSettingsSection: View {
    @AppStorage(MeetingAutoEnd.modeKey, store: UserDefaults.standard) private var mode = MeetingEndWatch.Mode.prompt.rawValue
    @AppStorage(MeetingAutoEnd.minutesKey, store: UserDefaults.standard) private var minutes = MeetingEndWatch.Config.defaultMinutes

    var body: some View {
        SettingsSection(title: "会议自动收尾", icon: "moon.zzz") {
            MenuPicker(title: "会议没人说话时", selection: $mode, options: [
                (MeetingEndWatch.Mode.prompt.rawValue, "提醒后结束"),
                (MeetingEndWatch.Mode.end.rawValue, "直接结束"),
                (MeetingEndWatch.Mode.off.rawValue, "不自动结束"),
            ], localizesOptions: true)
            // The wait time has a good default (see MeetingEndWatch.Config); changing it is for experts.
            AdvancedOptions("auto-end-minutes", title: "等待时间") {
                MenuPicker(title: "多久没人说话算结束", selection: $minutes,
                           options: MeetingEndWatch.Config.minuteChoices.map { ($0, String(localized: "\($0) 分钟")) })
                    .disabled(mode == MeetingEndWatch.Mode.off.rawValue)
            }
            Hint("只数识别到的文字，背景音乐不算；结束时会截掉末尾的空白。", presentation: .inline)
        }
    }
}
