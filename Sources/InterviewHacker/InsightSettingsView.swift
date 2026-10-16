import InterviewHackerCore
import SwiftUI

/// Screen awareness, slide following and answer progress (local rules).
struct InsightSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var insights = state.insights
        SettingsSection(title: "屏幕感知", icon: "eye") {
            Toggle("会话中每隔几秒读一次屏幕", isOn: $insights.screenWatch)
                .onChange(of: insights.screenWatch) { _, on in
                    insights.save()
                    state.startScreenWatch()
                    if on { state.updateLayout { $0.show(.insights) } }
                }
            if insights.screenWatch {
                HStack {
                    Text("间隔")
                    Picker("间隔", selection: $insights.screenInterval) {
                        Text("2 秒").tag(2.0)
                        Text("4 秒").tag(4.0)
                        Text("8 秒").tag(8.0)
                        Text("15 秒").tag(15.0)
                    }
                    .labelsHidden().pickerStyle(.segmented).frame(width: 260)
                    .onChange(of: insights.screenInterval) { _, _ in insights.save() }
                }
                Toggle("跟随演示文稿：认出正在讲第几页，给出这一页的讲稿提示", isOn: $insights.followSlides)
                    .onChange(of: insights.followSlides) { _, _ in insights.save() }
            }
            Note("在本机用系统文字识别读取主显示器（不含 InterviewHacker 自己的窗口），文字不上传。屏幕文字和最近的对话有重合时才交给回答引擎（例如共享屏幕上的题目、代码、文档）。")
            Note("讲稿跟随：把 PPT / Keynote / PDF 导入当前场景的资料，放映时自动认出当前页，提前写好这一页的要点、过渡句和听众可能问的问题，并勾掉你已经讲过的要点；回答提问时会关联当前页。需要“屏幕与系统音频录制”权限。")
            if let error = insights.screenError { Note(error, color: .orange) }
        }
        SettingsSection(title: "回答进度", icon: "checkmark.circle") {
            Toggle("边听你说，勾掉已经讲过的句子，提示下一句", isOn: $insights.answerProgress)
                .onChange(of: insights.answerProgress) { _, _ in insights.save() }
            Note("按你说的话和回答里每一句的文字重合度判断。")
        }
    }
}
