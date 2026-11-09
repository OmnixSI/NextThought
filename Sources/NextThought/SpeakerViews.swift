import NextThoughtCore
import SwiftUI

// Views for 区分对方的每个人: the small tag where the speaker changes, the row of people above the
// transcript, and the popover to name a person or say two are one. Quiet by design: no colors
// beyond the ink tokens; people differ by number and by a small shape (docs/redesign/01-visual-language.md).

/// What the transcript shows for a set of lines: who is on show, their numbers and names, and where
/// the speaker changes. Built from `SessionTranscriptLine`s so it serves both the meeting in
/// progress and a saved meeting.
struct SpeakerLayout {
    struct Person: Identifiable {
        let voice: Int
        /// 0 for the user (`isMe`), who is not numbered.
        let number: Int
        let lines: Int
        var isMe = false
        var id: Int { voice }
    }

    let voiceNumbers: [Int: Int]
    let lineNumbers: [UUID: Int]
    let names: [Int: String]
    let starts: Set<UUID>
    let people: [Person]
    /// Lines in a voice that is the user (not the lines of the microphone, which are always "我").
    let mine: Set<UUID>

    init(lines: [SessionTranscriptLine], voiceNames: [String: String]?, meVoices: Set<Int> = []) {
        let voiceNumbers = SpeakerLabels.voiceNumbers(lines, meVoices: meVoices)
        let lineNumbers = SpeakerLabels.numbers(lines, meVoices: meVoices)
        let mine = SpeakerLabels.meLines(lines, meVoices: meVoices)
        self.voiceNumbers = voiceNumbers
        self.lineNumbers = lineNumbers
        self.mine = mine
        names = SpeakerLabels.names(voiceNames, lines: lines, meVoices: meVoices)
        starts = voiceNumbers.isEmpty && mine.isEmpty ? [] : SpeakerLabels.runStarts(lines, lineNumbers, mine: mine)
        var counts: [Int: Int] = [:]
        for line in lines where line.speaker != .me { if let voice = line.voice { counts[voice, default: 0] += 1 } }
        let me = meVoices.filter { counts[$0] != nil }.sorted().map { Person(voice: $0, number: 0, lines: counts[$0] ?? 0, isMe: true) }
        people = me + voiceNumbers.map { Person(voice: $0.key, number: $0.value, lines: counts[$0.key] ?? 0) }
            .sorted { $0.number < $1.number }
    }

    /// At least two people are on show, or the user was recognised; otherwise the transcript stays as it always was.
    var isShown: Bool { !voiceNumbers.isEmpty || !mine.isEmpty }

    /// The user's name for a person, else "说话人 N".
    func label(_ number: Int) -> String {
        names[number] ?? String(localized: "说话人 \(number)")
    }

    /// "我" for a sentence in a voice that is the user.
    var meLabel: String { String(localized: "我") }

    /// "对方" or "现场" for a sentence that is not matched to a person on show.
    func fallbackLabel(_ speaker: SessionSpeaker) -> String {
        speaker == .room ? String(localized: "现场") : String(localized: "对方")
    }

    /// Whether a line is shown with the speaker tag column left empty (everything but the user's own lines).
    func hidesRoleLabel(for speaker: SessionSpeaker) -> Bool { isShown && speaker != .me }
}

/// A small shape for a person: circle, square, triangle, diamond, then around again. Hollow when
/// the sentence could not be matched to anyone.
struct SpeakerMark: View {
    let number: Int?
    var isMe = false
    var size: CGFloat = 7
    private static let symbols = ["circle.fill", "square.fill", "triangle.fill", "diamond.fill"]

    var body: some View {
        if isMe {
            Image(systemName: "person.fill").font(.system(size: size + 1)).foregroundStyle(Ink.ink)
        } else if let number {
            Image(systemName: Self.symbols[max(0, number - 1) % Self.symbols.count])
                .font(.system(size: size)).foregroundStyle(Ink.secondary)
        } else {
            Image(systemName: "circle").font(.system(size: size)).foregroundStyle(Ink.tertiary)
        }
    }
}

/// The name above a run of sentences, shown only where the speaker changes (as chat apps do).
struct SpeakerTag: View {
    let layout: SpeakerLayout
    let lineID: UUID
    let speaker: SessionSpeaker

    var body: some View {
        let number = layout.lineNumbers[lineID]
        let isMe = layout.mine.contains(lineID)
        HStack(spacing: 5) {
            SpeakerMark(number: number, isMe: isMe)
            Text(verbatim: isMe ? layout.meLabel : (number.map { layout.label($0) } ?? layout.fallbackLabel(speaker)))
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(number == nil && !isMe ? Ink.secondary : Ink.ink)
                .lineLimit(1)
        }
    }
}

/// Above the transcript once people are told apart: one capsule per person; a click opens the editor.
struct SpeakerStrip: View {
    let layout: SpeakerLayout
    let rename: (Int, String?) -> Void
    let merge: (Int, Int) -> Void
    /// Says a voice is (true) or is not (false) the user.
    let mark: (Int, Bool) -> Void
    @State private var editing: Int?

    var body: some View {
        if layout.isShown {
            HStack(alignment: .center, spacing: Space.s3) {
                FlowLayout(spacing: Space.s2, lineSpacing: Space.s2) {
                    ForEach(layout.people) { person in
                        Button { editing = person.voice } label: {
                            HStack(spacing: 5) {
                                SpeakerMark(number: person.number, isMe: person.isMe)
                                Text(verbatim: person.isMe ? layout.meLabel : layout.label(person.number))
                                    .font(Typo.caption).foregroundStyle(Ink.ink).lineLimit(1)
                            }
                            .padding(.horizontal, Space.s2)
                            .frame(height: 24)
                            .background(Ink.sunken, in: Capsule())
                            .contentShape(Capsule())
                        }
                        .buttonStyle(.plain)
                        .help(person.isMe ? "这是你自己的声音" : "改名字，或和另一个人合并")
                        .popover(isPresented: Binding(get: { editing == person.voice }, set: { if !$0 { editing = nil } }),
                                 arrowEdge: .bottom) {
                            if person.isMe {
                                MeEditor(lines: person.lines) { mark(person.voice, false); editing = nil } close: { editing = nil }
                            } else {
                                SpeakerEditor(layout: layout, voice: person.voice, rename: rename, merge: merge,
                                              markAsMe: { mark(person.voice, true) }) { editing = nil }
                            }
                        }
                    }
                }
                Spacer(minLength: 0)
                Text("按声音区分").font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize()
            }
        }
    }
}

/// Name one person, or say they are the same as another.
struct SpeakerEditor: View {
    let layout: SpeakerLayout
    let voice: Int
    let rename: (Int, String?) -> Void
    let merge: (Int, Int) -> Void
    let markAsMe: () -> Void
    let close: () -> Void
    @State private var draft = ""

    private var number: Int { layout.voiceNumbers[voice] ?? 0 }
    private var person: SpeakerLayout.Person? { layout.people.first { $0.voice == voice } }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack(spacing: Space.s2) {
                SpeakerMark(number: number)
                Text(verbatim: layout.label(number)).font(Typo.label)
                Spacer(minLength: Space.s4)
                if let person { Text("\(person.lines) 句").font(Typo.caption).foregroundStyle(Ink.secondary) }
            }
            TextField("输入名字", text: $draft)
                .textFieldStyle(.roundedBorder)
                .onSubmit { save() }
            Button { markAsMe(); close() } label: { Label("这是我", systemImage: "person.fill") }
                .buttonStyle(ChipButtonStyle())
            let others = layout.people.filter { $0.voice != voice && !$0.isMe }
            if !others.isEmpty {
                VStack(alignment: .leading, spacing: Space.s2) {
                    Text("其实是同一个人").overline()
                    FlowLayout(spacing: Space.s2, lineSpacing: Space.s2) {
                        ForEach(others) { other in
                            Button("并入 \(layout.label(other.number))") {
                                merge(voice, other.voice)
                                close()
                            }
                            .buttonStyle(ChipButtonStyle())
                        }
                    }
                }
            }
            HStack(spacing: Space.s2) {
                Spacer()
                Button("取消") { close() }.buttonStyle(GhostButtonStyle(height: 28))
                Button("完成") { save() }.buttonStyle(PrimaryButtonStyle(height: 28))
            }
        }
        .padding(Space.s4)
        .frame(width: 300)
        .onAppear { draft = layout.names[number] ?? "" }
    }

    private func save() {
        let name = draft.trimmingCharacters(in: .whitespacesAndNewlines)
        if name != (layout.names[number] ?? "") { rename(voice, name.isEmpty ? nil : name) }
        close()
    }
}

/// The popover of the user's own voice: it was recognised (or marked); one button takes that back.
struct MeEditor: View {
    let lines: Int
    let notMe: () -> Void
    let close: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack(spacing: Space.s2) {
                SpeakerMark(number: nil, isMe: true)
                Text("我").font(Typo.label)
                Spacer(minLength: Space.s4)
                Text("\(lines) 句").font(Typo.caption).foregroundStyle(Ink.secondary)
            }
            Text("这个声音被当作你自己。认错了的话，可以取消。").font(Typo.caption).foregroundStyle(Ink.secondary)
                .fixedSize(horizontal: false, vertical: true)
            HStack(spacing: Space.s2) {
                Spacer()
                Button("不是我") { notMe() }.buttonStyle(GhostButtonStyle(height: 28))
                Button("好") { close() }.buttonStyle(PrimaryButtonStyle(height: 28))
            }
        }
        .padding(Space.s4)
        .frame(width: 280)
    }
}

/// Settings → 语音识别: the switch for 区分对方的每个人.
struct SpeakerSettingsSection: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let speakers = state.speakers
        SettingsSection(title: "区分说话人", icon: "person.2.wave.2") {
            Toggle("区分对方的每个人", isOn: Binding(get: { speakers.enabled }, set: { speakers.setEnabled($0) }))
                .disabled(!speakers.modelAvailable)
            Note("会议里区分出的声纹只在本机计算，不上传，会议结束就忘；下一场会议起生效。", presentation: .inline)
            if !speakers.modelAvailable {
                Note("这个版本没有附带声纹模型。", color: Ink.caution, presentation: .inline)
            }
            if let failure = speakers.lastFailure {
                Note(String(localized: "声纹模型无法使用：\(failure)"), color: Ink.caution, presentation: .inline)
            }
            MyVoiceSettings()
        }
    }
}

/// 我的声音: read four sentences once; later meetings show the voice that matches as "我".
struct MyVoiceSettings: View {
    @Environment(AppState.self) private var state
    @State private var confirmRemove = false

    var body: some View {
        let voice = MyVoice.shared
        VStack(alignment: .leading, spacing: Space.s3) {
            Text("我的声音").font(Typo.label)
            if voice.isSettingUp {
                setup(voice)
            } else {
                summary(voice)
            }
            if let message = voice.message {
                Text(verbatim: message).font(Typo.caption).foregroundStyle(Ink.caution).fixedSize(horizontal: false, vertical: true)
            }
        }
        .padding(.top, Space.s2)
        .onChange(of: voice.isSaved) { _, saved in
            // Recording a voice is asking to have it used.
            if saved, !state.speakers.enabled { state.speakers.setEnabled(true) }
        }
        .confirmationDialog("删除已录入的声音？", isPresented: $confirmRemove) {
            Button("删除", role: .destructive) { voice.remove() }
            Button("取消", role: .cancel) {}
        } message: {
            Text("删除后，会议里不会再自动认出你；也可以随时重新录入。")
        }
    }

    @ViewBuilder private func summary(_ voice: MyVoice) -> some View {
        if let profile = voice.profile {
            HStack(spacing: Space.s3) {
                Text("已录入 · \(profile.createdAt.formatted(.dateTime.year().month().day()))")
                    .font(Typo.body).foregroundStyle(profile.isUsable ? Ink.ink : Ink.caution)
                Spacer(minLength: 0)
                Button("重新录入") { voice.beginSetup() }
                    .buttonStyle(GhostButtonStyle(height: 28)).disabled(state.isRunning || !voice.modelAvailable)
                Button("删除") { confirmRemove = true }.buttonStyle(GhostButtonStyle(height: 28))
            }
            if !profile.isUsable {
                Note("这份声音来自旧版本的模型，需要重新录入。", color: Ink.caution, presentation: .inline)
            }
        } else {
            HStack(spacing: Space.s3) {
                Text("还没有录入").font(Typo.body).foregroundStyle(Ink.secondary)
                Spacer(minLength: 0)
                Button("录入我的声音") { voice.beginSetup() }
                    .buttonStyle(SecondaryButtonStyle()).disabled(state.isRunning || !voice.modelAvailable)
            }
        }
        Note("读 4 句话，约 40 秒。只保存一串数字，不保存录音；只存在这台 Mac 上，不上传、不进备份，可随时删除。录入后，线下会议里你的发言会标为“我”。",
             presentation: .inline)
        if state.isRunning {
            Note("会议进行中不能录入。", presentation: .inline)
        }
    }

    @ViewBuilder private func setup(_ voice: MyVoice) -> some View {
        MyVoiceSetupPanel()
    }
}
