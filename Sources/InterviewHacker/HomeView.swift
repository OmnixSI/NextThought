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
            Text("开始一场会议").overline()
            Text("要开会了？").font(Typo.section)
            Text("新会议：放进资料、检查设备，再开始。立即记录：马上开始，结束后自动整理纪要。")
                .font(Typo.body).foregroundStyle(Ink.secondary)
                .fixedSize(horizontal: false, vertical: true)
            HStack(spacing: Space.s2) {
                Button { state.beginNewMeeting() } label: {
                    HStack(spacing: 6) {
                        Text("新会议")
                        Image(systemName: "arrow.right").font(.system(size: 12, weight: .medium))
                    }
                }
                .buttonStyle(PrimaryButtonStyle(height: Space.controlLarge))
                Button { Task { await state.recordNow() } } label: {
                    HStack(spacing: 6) {
                        Image(systemName: "record.circle")
                        Text("立即记录")
                        ShortcutHint(text: "⌘⇧N")
                    }
                }
                .buttonStyle(SecondaryButtonStyle())
                .disabled(state.isChangingSession)
                if state.isChangingSession {
                    ProgressView().controlSize(.small)
                }
            }
            .padding(.top, Space.s2)
        }
        .paperCard(padding: Space.s6)
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
            Text("最近").overline()
            Rectangle().fill(Ink.hairline).frame(height: 1).padding(.top, Space.s2)
            if recent.isEmpty {
                Text("开过的会议会出现在这里；结束后自动整理纪要。")
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
