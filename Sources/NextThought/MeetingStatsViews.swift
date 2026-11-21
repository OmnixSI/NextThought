import SwiftUI
import NextThoughtCore

extension MeetingStats {
    /// "5 小时 12 分", "42 分钟", "不到 1 分钟".
    @MainActor var durationText: String {
        let (hours, minutes) = hoursAndMinutes
        if hours > 0 { return String(localized: "\(hours) 小时 \(minutes) 分") }
        if minutes > 0 { return String(localized: "\(minutes) 分钟") }
        return meetings > 0 && seconds > 0 ? String(localized: "不到 1 分钟") : String(localized: "0 分钟")
    }
}

/// One figure with what it counts under it.
private struct StatFigure: View {
    let value: String
    let label: LocalizedStringKey
    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(verbatim: value).font(Font.zoomed(size: 20, weight: .medium, design: .serif).monospacedDigit()).foregroundStyle(Ink.ink)
                .lineLimit(1).minimumScaleFactor(0.7)
            Text(label).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

/// 首页的统计: this month and all time. Time is recording time, start to end of each meeting.
struct HomeStats: View {
    @Environment(AppState.self) private var state
    @State private var allTime = false

    var body: some View {
        let metas = state.archive.sessions
        if !metas.isEmpty {
            let stats = allTime ? MeetingStats.of(metas) : MeetingStats.of(metas, in: MeetingStats.month(of: .now))
            VStack(alignment: .leading, spacing: 0) {
                HStack {
                    Text("用了多少").overline()
                    Spacer()
                    InkSegmented(items: [(value: false, title: "本月"), (value: true, title: "累计")], selection: $allTime, height: 26)
                        .fixedSize()
                }
                .padding(.bottom, Space.s2)
                Rectangle().fill(Ink.hairline).frame(height: 0.5)
                HStack(alignment: .top, spacing: Space.s4) {
                    StatFigure(value: "\(stats.meetings)", label: "场会议")
                    StatFigure(value: stats.durationText, label: "记录时长")
                    StatFigure(value: "\(stats.questions)", label: "听到的提问")
                    StatFigure(value: "\(stats.answers)", label: "给出的回答")
                }
                .padding(.horizontal, Space.s2).padding(.vertical, Space.s4)
                .contentTransition(.numericText())
                .animation(Motion.snappy, value: allTime)
                Rectangle().fill(Ink.hairline).frame(height: 0.5)
                Text("时长按每场从开始记录到结束计算；暂停的时间也算在内。")
                    .font(Typo.caption).foregroundStyle(Ink.tertiary).padding(.top, Space.s2)
            }
            .accessibilityElement(children: .contain)
            .accessibilityIdentifier("home.stats")
        }
    }
}

/// One line under a series' name: how long it has been going.
struct SeriesStatsLine: View {
    @Environment(AppState.self) private var state
    let seriesID: UUID

    var body: some View {
        let stats = MeetingStats.of(state.archive.sessions, series: seriesID)
        if stats.meetings > 0 {
            Text("共 \(stats.meetings) 场 · \(stats.durationText) · 提问 \(stats.questions) 次 · 回答 \(stats.answers) 次")
                .font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
        }
    }
}
