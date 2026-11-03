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
            HStack(spacing: 8) {
                Toggle("会话中每隔几秒读一次屏幕", isOn: $insights.screenWatch)
                    .onChange(of: insights.screenWatch) { _, on in
                        insights.save()
                        state.startScreenWatch()
                        if on { state.updateLayout { $0.show(.insights) } }
                    }
                Note("可选窗口、显示器或区域（本应用窗口除外）。系统 OCR 先给快速文字；本机增强会在后台补读复杂画面。", presentation: .info)
                    .accessibilityLabel("屏幕读取说明")
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
                Note(state.screenContextService.status + " · " + state.screenTargetTitle, presentation: .inline)
                HStack {
                    MenuPicker(title: "间隔", selection: $insights.screenInterval,
                               options: [(1.0, String(localized: "1 秒")), (2.0, String(localized: "2 秒")),
                                         (4.0, String(localized: "4 秒")), (8.0, String(localized: "8 秒")),
                                         (15.0, String(localized: "15 秒"))])
                    .onChange(of: insights.screenInterval) { _, _ in insights.save() }
                }
                HStack(spacing: 8) {
                    Toggle("跟随演示文稿：认出正在讲第几页，给出这一页的讲稿提示", isOn: $insights.followSlides)
                        .onChange(of: insights.followSlides) { _, _ in insights.save() }
                    Note("导入 PPT、Keynote 或 PDF 后，放映时会识别当前页并关联这一页的要点与提问。", presentation: .info)
                        .accessibilityLabel("讲稿跟随说明")
                }
            }
            Note("识别结果会发送给当前回答引擎；遇到图表或位置关系时，可能附上当前屏幕图片。", presentation: .inline)
            if state.answerBackend == .openAICompatible {
                Toggle("当前 API 模型支持图像输入", isOn: Binding(get: { state.apiVisionEnabled }, set: { state.apiVisionEnabled = $0; state.updateSettings() }))
            }
            Note("讲稿跟随需要“屏幕与系统音频录制”权限。", presentation: .inline)
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
            HStack(spacing: 8) {
                Toggle("边听你说，勾掉已经讲过的句子，提示下一句", isOn: $insights.answerProgress)
                    .onChange(of: insights.answerProgress) { _, _ in insights.save() }
                Note("按你说的话和回答里每一句的文字重合度判断。", presentation: .info)
                    .accessibilityLabel("回答进度说明")
            }
        }
    }
}
