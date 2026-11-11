import AppKit
import SwiftUI
import NextThoughtCore

/// 首页: start the next meeting, or look at the last two. While a meeting is being
/// prepared the same page shows the preparation page.
struct HomeView: View {
    @Environment(AppState.self) private var state

    var body: some View {
        if state.meetingDraft != nil {
            PrepareView()
        } else {
            overview
        }
    }

    @State private var scene: SessionMode = AppState.shared.lastScene

    private var overview: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                Text(Date.now.formatted(.dateTime.month(.wide).day().weekday(.wide))).overline()
                VStack(alignment: .leading, spacing: 2) {
                    Text(greeting).foregroundStyle(Ink.ink)
                    Text("接下来是什么场合？").foregroundStyle(Ink.tertiary.opacity(0.75))
                }
                .font(Typo.hero)
                .padding(.top, Space.s3)
                OnboardingResumeRow()
                StartComposer(scene: $scene).padding(.top, Space.s8)
                ContinueSeries().padding(.top, Space.s12)
                if state.screenshotPreview != nil {
                    HomeScreenshotCard().padding(.top, Space.s6)
                }
                RecentMeetings().padding(.top, Space.s10)
            }
            .frame(maxWidth: 760, alignment: .leading)
            .padding(.horizontal, Space.s8)
            .padding(.top, Space.s10)
            .padding(.bottom, Space.s10)
            .frame(maxWidth: .infinity)
        }
        // A scene picked in the first-run guide becomes the composer's scene.
        .onChange(of: state.lastScene) { _, mode in scene = mode }
        .background(alignment: .top) {
            // The page takes on the pigment of the scene that is picked.
            InkWash(tint: scene.theme.tint, animated: !state.reducedEffects)
                .frame(height: 380)
                .animation(.easeInOut(duration: 0.8), value: scene)
        }
    }

    private var greeting: LocalizedStringKey {
        let hour = Calendar.current.component(.hour, from: .now)
        if hour < 5 { return "夜深了。" }
        if hour < 12 { return "上午好。" }
        if hour < 18 { return "下午好。" }
        return "晚上好。"
    }
}

/// One quiet row after "稍后继续" in the first-run guide: back to the page it stopped on.
/// Gone once the guide is finished, or when it is dismissed here.
private struct OnboardingResumeRow: View {
    @Environment(AppState.self) private var state
    @AppStorage("onboardingGuideDeferredVersion") private var deferred = ""
    @AppStorage("onboardingGuideCompletedVersion") private var completed = ""
    @AppStorage("onboardingResumeRowDismissedVersion") private var dismissed = ""

    private var line: String {
        let names = state.onboardingPendingAreas.map { $0 == .sound ? String(localized: "声音") : String(localized: "回答服务") }
        return names.isEmpty ? String(localized: "继续完成设置") : String(localized: "继续完成设置（还差\(names.joined(separator: " / "))）")
    }

    var body: some View {
        let version = state.currentGuideVersion
        if deferred == version, completed != version, dismissed != version {
            HStack(spacing: Space.s2) {
                Button { state.openOnboarding() } label: {
                    HStack(spacing: Space.s2) {
                        Circle().fill(Ink.seal).frame(width: 6, height: 6)
                        Text(verbatim: line).font(Typo.label).foregroundStyle(Ink.ink).lineLimit(1)
                        Image(systemName: "arrow.right").font(.system(size: 10, weight: .semibold)).foregroundStyle(Ink.tertiary)
                        Spacer(minLength: 0)
                    }
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel(line)
                .accessibilityHint("回到上次停下的引导页")
                Button { dismissed = version } label: {
                    Image(systemName: "xmark").font(.system(size: 10, weight: .semibold)).foregroundStyle(Ink.tertiary)
                        .frame(width: 24, height: 24).contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .help("不再提示")
                .accessibilityLabel("不再提示继续设置")
            }
            .padding(.horizontal, Space.s3).padding(.vertical, 6)
            .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            .padding(.top, Space.s6)
        }
    }
}

/// The one raised sheet on the page: name the meeting (or don't), pick the scene,
/// start. Typing a title picks the scene for you.
private struct StartComposer: View {
    @Environment(AppState.self) private var state
    @Binding var scene: SessionMode
    @State private var title = ""
    /// The user chose a scene by hand: stop guessing it from the title.
    @State private var scenePinned = false
    @FocusState private var titleFocused: Bool
    @Namespace private var chip

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: Space.s3) {
                SceneGlyph(mode: scene, size: 38)
                    .id(scene).transition(.scale(scale: 0.8).combined(with: .opacity))
                TextField("给这场会起个名字，比如“字节二面”", text: $title)
                    .textFieldStyle(.plain)
                    .font(.system(size: 19, weight: .medium))
                    .focused($titleFocused)
                    .onSubmit { open(startNow: false) }
                    .onChange(of: title) { _, text in
                        guard !scenePinned else { return }
                        let guess = MeetingTypeGuesser.guess(text)
                        if guess != .general, guess != scene { scene = guess }
                    }
                    .accessibilityIdentifier("home.meetingTitle")
            }
            .padding(.horizontal, Space.s6).padding(.top, Space.s6).padding(.bottom, Space.s4)

            FlowLayout(spacing: 4, lineSpacing: 4) {
                ForEach(SessionMode.builtIn) { mode in sceneChip(mode) }
            }
            .padding(.horizontal, Space.s6 - 4)

            VStack(alignment: .leading, spacing: Space.s2) {
                Text(verbatim: scene.theme.promise)
                    .font(.system(size: 17, weight: .medium, design: .serif))
                    .foregroundStyle(Ink.ink)
                HStack(spacing: Space.s4) {
                    ForEach(scene.theme.moments, id: \.self) { moment in
                        HStack(spacing: 6) {
                            Circle().fill(scene.theme.tint).frame(width: 4, height: 4)
                            Text(verbatim: moment).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
                        }
                    }
                }
            }
            .id(scene)
            .transition(.blurReplace)
            .padding(.horizontal, Space.s6).padding(.top, Space.s6)

            HStack(spacing: Space.s2) {
                HStack(spacing: 5) {
                    ShortcutHint(text: "⌘⇧N")
                    Text("不挑场合，立即记录").font(Typo.caption).foregroundStyle(Ink.secondary)
                }
                Spacer(minLength: Space.s3)
                if state.isChangingSession { ProgressView().controlSize(.small) }
                Button { open(startNow: false) } label: { Text("先准备资料") }
                    .buttonStyle(SecondaryButtonStyle())
                    .accessibilityIdentifier("home.prepareRecording")
                Button { open(startNow: true) } label: {
                    HStack(spacing: 7) {
                        Circle().fill(Ink.seal).frame(width: 7, height: 7)
                        Text("开始")
                    }
                }
                .buttonStyle(PrimaryButtonStyle())
                .disabled(state.isChangingSession)
                .help("以“\(scene.displayName)”场景立即开始")
                .accessibilityIdentifier("home.startScene")
            }
            .padding(.horizontal, Space.s6).padding(.top, Space.s6).padding(.bottom, Space.s4 + 2)
        }
        .background {
            RoundedRectangle(cornerRadius: Space.radiusLarge, style: .continuous)
                .fill(Ink.surface)
                .shadow(color: Ink.contactShadow, radius: 1, x: 0, y: 1)
                .shadow(color: Ink.ambientShadow, radius: 28, x: 0, y: 16)
        }
        .overlay {
            RoundedRectangle(cornerRadius: Space.radiusLarge, style: .continuous)
                .strokeBorder(titleFocused ? scene.theme.tint.opacity(0.45) : Ink.hairline, lineWidth: titleFocused ? 1 : 0.5)
        }
        .animation(Motion.snappy, value: scene)
        .animation(Motion.hover, value: titleFocused)
    }

    private func sceneChip(_ mode: SessionMode) -> some View {
        let selected = mode == scene
        return Button {
            scene = mode
            scenePinned = true
        } label: {
            Text(verbatim: mode.displayName).font(Typo.label)
            .foregroundStyle(selected ? mode.theme.tint : Ink.secondary)
            .padding(.horizontal, 11)
            .frame(height: 30)
            .background {
                if selected {
                    Capsule().fill(mode.theme.tint.opacity(0.13))
                        .matchedGeometryEffect(id: "selection", in: chip)
                }
            }
            .contentShape(Capsule())
        }
        .buttonStyle(SceneChipStyle())
        .help(mode.defaultsSummary)
        .accessibilityAddTraits(selected ? .isSelected : [])
        .accessibilityIdentifier("home.scene.\(mode.rawValue)")
    }

    private func open(startNow: Bool) {
        state.beginNewMeeting(scene: scene)
        // An unsaved series edit asks first; the draft appears once that is settled.
        guard state.meetingDraft != nil else { return }
        let name = title.trimmingCharacters(in: .whitespacesAndNewlines)
        if !name.isEmpty { state.updateDraftTitle(name) }
        title = ""; scenePinned = false
        if startNow { Task { await state.startPreparedMeeting() } }
    }
}

private struct SceneChipStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View { Chip(configuration: configuration) }
    private struct Chip: View {
        let configuration: ButtonStyleConfiguration
        @State private var hovering = false
        var body: some View {
            configuration.label
                .background(hovering ? Ink.hover : .clear, in: Capsule())
                .scaleEffect(configuration.isPressed ? 0.96 : 1)
                .onHover { hovering = $0 }
                .animation(Motion.hover, value: hovering)
                .animation(Motion.snappy, value: configuration.isPressed)
        }
    }
}

/// The series used most recently: one click prepares the next meeting of each.
private struct ContinueSeries: View {
    @Environment(AppState.self) private var state
    private var series: [Workspace] {
        Array(state.workspaces.visible.sorted {
            (state.lastSession(inSeries: $0.id)?.startedAt ?? $0.updatedAt)
                > (state.lastSession(inSeries: $1.id)?.startedAt ?? $1.updatedAt)
        }.prefix(3))
    }
    var body: some View {
        if !series.isEmpty {
            VStack(alignment: .leading, spacing: 0) {
                HStack {
                    Text("接着上一场").overline()
                    Spacer()
                    Button("全部资料夹") { state.section = .library }.buttonStyle(.tapText).font(Typo.caption)
                }
                .padding(.bottom, Space.s2)
                ForEach(series) { item in
                    Rectangle().fill(Ink.hairline).frame(height: 0.5)
                    Button {
                        if let previous = state.lastSession(inSeries: item.id) { state.prepareNextMeeting(after: previous.id) }
                        else {
                            state.beginNewMeetingAfterWorkspaceEdit()
                            state.continueDraftSeries(item.id)
                        }
                    } label: {
                        HStack(spacing: Space.s3) {
                            Circle().fill(item.effectiveMode.theme.tint).frame(width: 7, height: 7)
                            Text(verbatim: item.name).font(Typo.body).foregroundStyle(Ink.ink).lineLimit(1)
                            Text(verbatim: item.typeDisplayName).font(Typo.caption).foregroundStyle(Ink.secondary)
                            Spacer(minLength: Space.s3)
                            Text("\(state.sessionCount(inSeries: item.id)) 场记录 · \(item.documents.count) 份资料")
                                .font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
                            Image(systemName: "arrow.right").font(.system(size: 10, weight: .semibold)).foregroundStyle(Ink.tertiary)
                        }
                        .padding(.horizontal, Space.s2).padding(.vertical, Space.s3)
                        .contentShape(Rectangle())
                    }
                    .buttonStyle(InteractiveRowButtonStyle())
                    .help("准备下一场")
                    .accessibilityIdentifier("home.continueProject.\(item.id.uuidString)")
                }
                Rectangle().fill(Ink.hairline).frame(height: 0.5)
            }
        }
    }
}

/// A screenshot taken outside a meeting: answer it here; the answer shows in the
/// teleprompter.
private struct HomeScreenshotCard: View {
    @Environment(AppState.self) private var state

    var body: some View {
        if let image = state.screenshotPreview {
            VStack(alignment: .leading, spacing: Space.s3) {
                Text("截图").overline()
                Image(nsImage: image).resizable().scaledToFit().frame(maxHeight: 160)
                    .clipShape(RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
                HStack(spacing: Space.s2) {
                    Button("分析截图") {
                        state.answerScreenshot()
                        TeleprompterController.shared.show()
                    }
                    .buttonStyle(PrimaryButtonStyle())
                    Button("取消") { state.clearScreenshot() }.buttonStyle(SecondaryButtonStyle())
                }
            }
            .paperCard()
        }
    }
}

/// The two latest meetings with what came out of them.
private struct RecentMeetings: View {
    @Environment(AppState.self) private var state
    @FocusState private var focusedMeetingID: UUID?

    var body: some View {
        let recent = Array(state.archive.sessions.filter { $0.id != state.archive.activeID }.prefix(4))
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                Text("最近记录").overline()
                Spacer()
                Button("全部记录") { state.section = .meetings }.buttonStyle(.tapText).font(Typo.caption)
            }
            Rectangle().fill(Ink.hairline).frame(height: 0.5).padding(.top, Space.s2)
            if recent.isEmpty {
                Text("开过的会都在这里：原话、纪要和下一步，随时找得回来。")
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                    .padding(.vertical, Space.s3)
            }
            ForEach(recent) { meta in
                Button {
                    state.historySelection = meta.id
                    state.section = .meetings
                } label: {
                    HStack(alignment: .center, spacing: Space.s3) {
                        Circle().fill(meta.mode.theme.tint).frame(width: 7, height: 7)
                        VStack(alignment: .leading, spacing: 2) {
                            Text(meta.displayTitle).font(Typo.body).foregroundStyle(Ink.ink).lineLimit(2)
                            Text(MeetingOutcome.line(for: meta)).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
                        }
                        Spacer(minLength: Space.s3)
                        Text(MeetingOutcome.when(meta.startedAt)).font(Typo.caption).foregroundStyle(Ink.secondary)
                        Image(systemName: "chevron.right")
                            .font(.system(size: 9, weight: .semibold))
                            .foregroundStyle(Ink.tertiary)
                    }
                    .padding(.horizontal, Space.s2)
                    .padding(.vertical, Space.s3)
                    .contentShape(Rectangle())
                }
                .buttonStyle(InteractiveRowButtonStyle())
                .focused($focusedMeetingID, equals: meta.id)
                .overlay {
                    if focusedMeetingID == meta.id {
                        RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous)
                            .strokeBorder(Ink.focus, lineWidth: 1.5)
                            .padding(1)
                    }
                }
                .accessibilityLabel("打开会议：\(meta.displayTitle)，\(MeetingOutcome.line(for: meta))，\(MeetingOutcome.when(meta.startedAt))")
                .accessibilityHint("打开会议纪要")
                .accessibilityIdentifier("home.recentMeeting.\(meta.id.uuidString)")
                Rectangle().fill(Ink.hairline).frame(height: 0.5)
            }
        }
    }
}

/// Short texts about a recorded meeting, shared by the home page and the meeting list.
@MainActor
enum MeetingOutcome {
    /// "3 个待办" / "2 道题待改进" / "面试 · 52 分钟".
    static func line(for meta: SessionMeta) -> String {
        if SessionArchive.shared.summarizing.contains(meta.id) {
            return String(localized: "正在整理纪要")
        }
        if meta.summaryState == .failed || meta.summaryState == .pending {
            return String(localized: "纪要没有整理好 · 可以重试")
        }
        if let improve = meta.improveCount, improve > 0 {
            return String(localized: "\(improve) 道题待改进")
        }
        if let actions = meta.actionItemCount, actions > 0 {
            return String(localized: "\(actions) 个待办")
        }
        let type = meta.typeName ?? meta.mode.displayName
        let minutes = Int(meta.duration / 60)
        return minutes > 0 ? String(localized: "\(type) · \(minutes) 分钟") : type
    }

    /// "今天 10:00" / "昨天" / "周四" / "10月2日".
    static func when(_ date: Date, now: Date = .now) -> String {
        let calendar = Calendar.current
        let time = date.formatted(date: .omitted, time: .shortened)
        if calendar.isDateInToday(date) { return String(localized: "今天 \(time)") }
        if calendar.isDateInYesterday(date) { return String(localized: "昨天 \(time)") }
        if now.timeIntervalSince(date) < 6 * 86_400 { return date.formatted(.dateTime.weekday(.wide)) }
        return date.formatted(.dateTime.month().day())
    }
}
