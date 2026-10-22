import AppKit
import InterviewHackerCore
import SwiftUI

/// Screen awareness, slide following and answer progress (local rules).
struct InsightSettings: View {
    @Environment(AppState.self) private var state
    @State private var regionPreview: NSImage?
    @State private var showingRegion = false

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
                Picker("读取来源", selection: Binding(get: { state.screenTargetID }, set: { insights.screenTarget = $0; insights.screenRegion = nil; insights.save(); state.startScreenWatch() })) {
                    if state.captureTargets.isEmpty { Text("主显示器").tag("") }
                    ForEach(state.captureTargets) { target in Text(target.title).tag(target.id) }
                }
                HStack {
                    Button("刷新来源") { Task { await state.refreshTargets() } }
                    Button("立即识别") { Task { await state.readScreen() } }.disabled(insights.isReadingScreen)
                    Button("框选区域") {
                        Task {
                            do {
                                let image = try await ScreenshotService.captureForReading(targetID: state.screenTargetID)
                                regionPreview = NSImage(cgImage: image, size: NSSize(width: image.width, height: image.height)); showingRegion = true
                            } catch { insights.screenError = error.localizedDescription }
                        }
                    }
                    if insights.screenRegion != nil { Button("读取完整来源") { insights.screenRegion = nil; insights.save(); state.startScreenWatch() } }
                }.buttonStyle(SecondaryButtonStyle())
                Note(state.screenContextService.status + " · " + state.screenTargetTitle)
                HStack {
                    Text("间隔")
                    Picker("间隔", selection: $insights.screenInterval) {
                        Text("1 秒").tag(1.0)
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
            Note("读取选定窗口、显示器或区域，不含本应用窗口。系统 OCR 提供快速文字；选择本机增强时，复杂画面在独立队列补读。提问涉及当前画面时会把识别文字交给回答引擎，图表或位置关系必要时附当前图片。")
            if state.answerBackend == .openAICompatible {
                Toggle("当前 API 模型支持图像输入", isOn: Binding(get: { state.apiVisionEnabled }, set: { state.apiVisionEnabled = $0; state.updateSettings() }))
            }
            Note("讲稿跟随：把 PPT / Keynote / PDF 导入当前场景的资料，放映时自动认出当前页，提前写好这一页的要点、过渡句和听众可能问的问题，并勾掉你已经讲过的要点；回答提问时会关联当前页。需要“屏幕与系统音频录制”权限。")
            if let error = insights.screenError { Note(error, color: .orange) }
        }
        .sheet(isPresented: $showingRegion) {
            if let image = regionPreview {
                ScreenRegionSheet(image: image) { rect in
                    if let rect { insights.screenRegion = [rect.minX, rect.minY, rect.width, rect.height]; insights.save(); state.startScreenWatch() }
                    showingRegion = false; regionPreview = nil
                }
            }
        }
        SettingsSection(title: "回答进度", icon: "checkmark.circle") {
            Toggle("边听你说，勾掉已经讲过的句子，提示下一句", isOn: $insights.answerProgress)
                .onChange(of: insights.answerProgress) { _, _ in insights.save() }
            Note("按你说的话和回答里每一句的文字重合度判断。")
        }
    }
}
