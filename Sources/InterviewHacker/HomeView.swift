import SwiftUI
import InterviewHackerCore

/// 首页: what to do before the next meeting.
struct HomeView: View {
    @Environment(AppState.self) private var state

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Space.s10) {
                VStack(alignment: .leading, spacing: Space.s2) {
                    Text(Date.now.formatted(.dateTime.month().day().weekday(.wide))).overline()
                    Text(greeting).font(Typo.display)
                }
                HStack(spacing: Space.s2) {
                    Button { state.beginNewMeeting() } label: { Label("新会议", systemImage: "plus") }
                        .buttonStyle(SecondaryButtonStyle())
                    Button { Task { await state.recordNow() } } label: { Label("立即记录", systemImage: "record.circle") }
                        .buttonStyle(SecondaryButtonStyle())
                }
            }
            .frame(maxWidth: Space.content, alignment: .leading)
            .padding(.horizontal, Space.s8)
            .padding(.top, Space.s12)
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
