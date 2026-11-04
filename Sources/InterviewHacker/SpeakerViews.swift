import InterviewHackerCore
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
        let number: Int
        let lines: Int
        var id: Int { voice }
    }

    let voiceNumbers: [Int: Int]
    let lineNumbers: [UUID: Int]
    let names: [Int: String]
    let starts: Set<UUID>
    let people: [Person]

    init(lines: [SessionTranscriptLine], voiceNames: [String: String]?) {
        let voiceNumbers = SpeakerLabels.voiceNumbers(lines)
        let lineNumbers = SpeakerLabels.numbers(lines)
        self.voiceNumbers = voiceNumbers
        self.lineNumbers = lineNumbers
        names = SpeakerLabels.names(voiceNames, lines: lines)
        starts = voiceNumbers.isEmpty ? [] : SpeakerLabels.runStarts(lines, lineNumbers)
        var counts: [Int: Int] = [:]
        for line in lines where line.speaker != .me { if let voice = line.voice { counts[voice, default: 0] += 1 } }
        people = voiceNumbers.map { Person(voice: $0.key, number: $0.value, lines: counts[$0.key] ?? 0) }
            .sorted { $0.number < $1.number }
    }

    /// At least two people are on show; otherwise the transcript stays as it always was.
    var isShown: Bool { !voiceNumbers.isEmpty }

    /// The user's name for a person, else "说话人 N".
    func label(_ number: Int) -> String {
        names[number] ?? String(localized: "说话人 \(number)")
    }

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
    var size: CGFloat = 7
    private static let symbols = ["circle.fill", "square.fill", "triangle.fill", "diamond.fill"]

    var body: some View {
        if let number {
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
        HStack(spacing: 5) {
            SpeakerMark(number: number)
            Text(verbatim: number.map { layout.label($0) } ?? layout.fallbackLabel(speaker))
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(number == nil ? Ink.secondary : Ink.ink)
                .lineLimit(1)
        }
    }
}

/// Above the transcript once people are told apart: one capsule per person; a click opens the editor.
struct SpeakerStrip: View {
    let layout: SpeakerLayout
    let rename: (Int, String?) -> Void
    let merge: (Int, Int) -> Void
    @State private var editing: Int?

    var body: some View {
        if layout.isShown {
            HStack(alignment: .center, spacing: Space.s3) {
                FlowLayout(spacing: Space.s2, lineSpacing: Space.s2) {
                    ForEach(layout.people) { person in
                        Button { editing = person.voice } label: {
                            HStack(spacing: 5) {
                                SpeakerMark(number: person.number)
                                Text(verbatim: layout.label(person.number)).font(Typo.caption).foregroundStyle(Ink.ink).lineLimit(1)
                            }
                            .padding(.horizontal, Space.s2)
                            .frame(height: 24)
                            .background(Ink.sunken, in: Capsule())
                            .contentShape(Capsule())
                        }
                        .buttonStyle(.plain)
                        .help("改名字，或和另一个人合并")
                        .popover(isPresented: Binding(get: { editing == person.voice }, set: { if !$0 { editing = nil } }),
                                 arrowEdge: .bottom) {
                            SpeakerEditor(layout: layout, voice: person.voice, rename: rename, merge: merge) { editing = nil }
                        }
                    }
                }
                Spacer(minLength: 0)
                Text("按声音区分（实验）").font(Typo.caption).foregroundStyle(Ink.secondary).fixedSize()
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
            let others = layout.people.filter { $0.voice != voice }
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

/// Settings → 语音识别: the switch for 区分对方的每个人.
struct SpeakerSettingsSection: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let speakers = state.speakers
        SettingsSection(title: "区分说话人", icon: "person.2.wave.2") {
            Toggle("区分对方的每个人（实验）", isOn: Binding(get: { speakers.enabled }, set: { speakers.setEnabled($0) }))
                .disabled(!speakers.modelAvailable)
            Note("只在本机计算，声纹不上传，也不保存到会议之外；下一场会议起生效。", presentation: .inline)
            if !speakers.modelAvailable {
                Note("这个版本没有附带声纹模型。", color: Ink.caution, presentation: .inline)
            }
            if let failure = speakers.lastFailure {
                Note(String(localized: "声纹模型无法使用：\(failure)"), color: Ink.caution, presentation: .inline)
            }
        }
    }
}
