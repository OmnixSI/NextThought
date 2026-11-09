import SwiftUI
import NextThoughtCore

// Views for 我的声音 that are used in more than one place: the sentence-by-sentence recorder
// (settings → 语音识别 and the first-run guide) and the guide's own page. The state lives in
// `MyVoice.shared`; nothing here records, stores or uploads anything by itself.

/// The recorder: the sentence to read, a progress bar while recording, and cancel / start.
/// Shown while `MyVoice.shared.isSettingUp`.
struct MyVoiceSetupPanel: View {
    var buttonHeight: CGFloat = 28

    var body: some View {
        let voice = MyVoice.shared
        let index = min(voice.takes.count, MyVoice.prompts.count - 1)
        VStack(alignment: .leading, spacing: Space.s3) {
            Text("第 \(index + 1) / \(MyVoice.prompts.count) 句，点“开始”后用平时开会的声音读出来")
                .font(Typo.caption).foregroundStyle(Ink.secondary)
            Text(verbatim: voice.currentPrompt)
                .font(Typo.body).foregroundStyle(Ink.ink)
                .fixedSize(horizontal: false, vertical: true)
                .padding(Space.s3)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(Ink.sunken, in: RoundedRectangle(cornerRadius: 8))
            if voice.isRecording {
                ProgressView(value: voice.progress).progressViewStyle(.linear)
            }
            HStack(spacing: Space.s2) {
                Spacer(minLength: 0)
                Button("取消") { voice.cancelSetup() }.buttonStyle(GhostButtonStyle(height: buttonHeight))
                Button(LocalizedStringKey(voice.isRecording ? "正在听…" : (voice.isWorking ? "处理中…" : "开始"))) {
                    Task { await voice.recordTake() }
                }
                .buttonStyle(PrimaryButtonStyle(height: buttonHeight))
                .disabled(voice.isRecording || voice.isWorking)
            }
        }
    }
}

/// The first-run guide's page “你的声音”: one clear action (read four sentences), skippable.
struct OnboardingVoicePage: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let voice = MyVoice.shared
        VStack(alignment: .leading, spacing: Space.s6) {
            VStack(alignment: .leading, spacing: Space.s2) {
                Text("录入你的声音").font(Typo.display).fixedSize(horizontal: false, vertical: true)
                Text("读 4 句话，约 40 秒。之后会议里你说的话会标成“我”，也更容易分清其他人。")
                    .font(Typo.body).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
            }
            VStack(alignment: .leading, spacing: Space.s3) { content(voice) }
                .padding(Space.s4)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(Ink.surface, in: RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 1))
            if let message = voice.message {
                Text(verbatim: message).font(Typo.caption).foregroundStyle(Ink.caution).fixedSize(horizontal: false, vertical: true)
            }
            Text("只保存一串数字，不保存录音；只存在这台 Mac 上，不上传，可随时删除。")
                .font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize(horizontal: false, vertical: true)
        }
        // Recording a voice is asking to have it used (the same as in settings).
        .onChange(of: voice.isSaved) { _, saved in
            if saved, !state.speakers.enabled { state.speakers.setEnabled(true) }
        }
        // The first recording asks for the microphone; pick up the answer.
        .onChange(of: voice.isRecording) { _, _ in state.microphoneAccess = .current() }
        .onChange(of: voice.message) { _, _ in state.microphoneAccess = .current() }
        .onDisappear { if voice.isSettingUp { voice.cancelSetup() } }
    }

    @ViewBuilder private func content(_ voice: MyVoice) -> some View {
        if voice.isSettingUp {
            MyVoiceSetupPanel(buttonHeight: Space.controlLarge)
        } else if voice.usableProfile != nil {
            HStack(spacing: Space.s3) {
                StatusDot(tone: .ready)
                Text("已录入").font(Typo.body)
                Spacer(minLength: 0)
                Button("重新录入") { voice.beginSetup() }
                    .buttonStyle(GhostButtonStyle(height: 28)).disabled(state.isRunning || !voice.modelAvailable)
            }
        } else if !voice.modelAvailable {
            Text("这个版本没有附带声纹模型，跳过也不影响使用。")
                .font(Typo.body).foregroundStyle(Ink.caution).fixedSize(horizontal: false, vertical: true)
        } else {
            if voice.isSaved {
                Text("这份声音来自旧版本的模型，需要重新录入。")
                    .font(Typo.caption).foregroundStyle(Ink.caution).fixedSize(horizontal: false, vertical: true)
            }
            if state.microphoneAccess == .denied {
                HStack(alignment: .top, spacing: Space.s3) {
                    Text("麦克风还没有允许，录不了。允许后回到这里继续。")
                        .font(Typo.body).foregroundStyle(Ink.caution).fixedSize(horizontal: false, vertical: true)
                    Spacer(minLength: Space.s2)
                    Button("打开系统设置") { Task { await state.requestMicrophoneAccess() } }.buttonStyle(SecondaryButtonStyle())
                }
            } else {
                HStack(spacing: Space.s3) {
                    Button("开始录入") { voice.beginSetup() }
                        .buttonStyle(PrimaryButtonStyle(height: Space.controlLarge))
                        .disabled(state.isRunning)
                    if state.microphoneAccess == .undetermined {
                        Text("第一次录音时，macOS 会询问麦克风。").font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                    Spacer(minLength: 0)
                }
            }
        }
    }
}
