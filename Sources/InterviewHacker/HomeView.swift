import AppKit
import SwiftUI
import InterviewHackerCore

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

    private var overview: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                Text(Date.now.formatted(.dateTime.month(.wide).day().weekday(.wide))).overline()
                Text(greeting).font(Typo.display).padding(.top, Space.s2)
                StartCard().padding(.top, Space.s10)
                ContinueResearch().padding(.top, Space.s8)
                if state.screenshotPreview != nil {
                    HomeScreenshotCard().padding(.top, Space.s6)
                }
                RecentMeetings().padding(.top, Space.s10)
            }
            .frame(maxWidth: Space.content, alignment: .leading)
            .padding(.horizontal, Space.s8)
            .padding(.top, Space.s12)
            .padding(.bottom, Space.s10)
            .frame(maxWidth: .infinity)
        }
    }

    private var greeting: LocalizedStringKey {
        let hour = Calendar.current.component(.hour, from: .now)
        if hour < 12 { return "上午好" }
        if hour < 18 { return "下午好" }
        return "晚上好"
    }
}

/// Until the calendar is connected, the "next meeting" card offers the two ways in.
private struct StartCard: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            Text("组会 · 研究讨论 · 课程").overline()
            Text("专注讨论，留下下一步").font(Typo.section)
            Text("开始记录：先选择本场资料和声音。赶时间时可立即记录，AI 与资料都能稍后补充。")
                .font(Typo.body).foregroundStyle(Ink.secondary)
                .fixedSize(horizontal: false, vertical: true)
            HStack(spacing: Space.s2) {
                Button { state.beginNewMeeting() } label: {
                    HStack(spacing: 6) {
                        Text("开始记录")
                        Image(systemName: "arrow.right").font(.system(size: 12, weight: .medium))
                    }
                }
                .buttonStyle(PrimaryButtonStyle(height: Space.controlLarge))
                .accessibilityIdentifier("home.prepareRecording")
                Button { Task { await state.recordNow() } } label: {
                    HStack(spacing: 6) {
                        Image(systemName: "record.circle")
                        Text("立即记录")
                        ShortcutHint(text: "⌘⇧N")
                    }
                }
                .buttonStyle(SecondaryButtonStyle())
                .disabled(state.isChangingSession)
                .accessibilityIdentifier("home.quickRecording")
                if state.isChangingSession {
                    ProgressView().controlSize(.small)
                }
            }
            .padding(.top, Space.s2)
            Menu("换个场景") {
                ForEach([SessionMode.labMeeting, .academic, .lesson, .discussion, .general]) { mode in
                    Button(mode.displayName) {
                        state.beginNewMeeting()
                        state.setDraftType(mode)
                    }
                }
            }
            .menuStyle(.borderlessButton).fixedSize().font(Typo.caption).foregroundStyle(Ink.secondary)
        }
        .paperCard(padding: Space.s6)
    }
}

private struct ContinueResearch: View {
    @Environment(AppState.self) private var state
    private var series: [Workspace] {
        Array(state.workspaces.visible.sorted {
            (state.lastSession(inSeries: $0.id)?.startedAt ?? $0.updatedAt)
                > (state.lastSession(inSeries: $1.id)?.startedAt ?? $1.updatedAt)
        }.prefix(3))
    }
    var body: some View {
        if !series.isEmpty {
            VStack(alignment: .leading, spacing: Space.s3) {
                HStack {
                    Text("继续项目").overline()
                    Spacer()
                    Button("查看全部") { state.section = .library }.buttonStyle(.tapText).font(Typo.caption)
                }
                ForEach(series) { item in
                    Button {
                        if let previous = state.lastSession(inSeries: item.id) { state.prepareNextMeeting(after: previous.id) }
                        else {
                            state.beginNewMeetingAfterWorkspaceEdit()
                            state.continueDraftSeries(item.id)
                        }
                    } label: {
                        HStack(spacing: Space.s3) {
                            Image(systemName: item.mode.symbol).foregroundStyle(Ink.secondary).frame(width: 20)
                            VStack(alignment: .leading, spacing: 2) {
                                Text(verbatim: item.name).font(Typo.label).foregroundStyle(Ink.ink).lineLimit(1)
                                Text("\(state.sessionCount(inSeries: item.id)) 场记录 · \(item.documents.count) 份资料")
                                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                            }
                            Spacer()
                            Text("准备下一场").font(Typo.caption).foregroundStyle(Ink.secondary)
                            Image(systemName: "arrow.right").font(.caption).foregroundStyle(Ink.secondary)
                        }
                        .padding(Space.s3).contentShape(Rectangle())
                    }
                    .buttonStyle(InteractiveRowButtonStyle())
                    .accessibilityIdentifier("home.continueProject.\(item.id.uuidString)")
                }
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
        let recent = Array(state.archive.sessions.filter { $0.id != state.archive.activeID }.prefix(2))
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                Text("最近记录").overline()
                Spacer()
                Button("查看全部") { state.section = .meetings }.buttonStyle(.tapText).font(Typo.caption)
            }
            Rectangle().fill(Ink.hairline).frame(height: 1).padding(.top, Space.s2)
            if recent.isEmpty {
                Text("记录会保存在这里。可以找回原话，整理导师反馈和下一步实验。")
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                    .padding(.vertical, Space.s3)
            }
            ForEach(recent) { meta in
                Button {
                    state.historySelection = meta.id
                    state.section = .meetings
                } label: {
                    HStack(alignment: .center, spacing: Space.s3) {
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
                Rectangle().fill(Ink.hairline).frame(height: 1)
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
