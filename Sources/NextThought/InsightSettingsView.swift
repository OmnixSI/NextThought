import AppKit
import NextThoughtCore
import SwiftUI

/// Settings → 回答 → 屏幕与讲稿: screen awareness, slide following and answer progress (local rules).
struct InsightSettings: View {
    @Environment(AppState.self) private var state
    @State private var regionPreview: NSImage?
    @State private var showingRegion = false

    var body: some View {
        @Bindable var insights = state.insights
        SettingsSection(title: "屏幕与讲稿", icon: "eye") {
            HStack(spacing: 8) {
                Toggle("会议中每隔几秒读一次屏幕", isOn: $insights.screenWatch)
                    .onChange(of: insights.screenWatch) { _, on in
                        insights.save()
                        state.startScreenWatch()
                        if on { state.updateLayout { $0.show(.insights) } }
                    }
                Note("可选窗口、显示器或区域（本应用窗口除外）。系统 OCR 先给快速文字；本机增强会在后台补读复杂画面。", presentation: .info)
                    .accessibilityLabel("屏幕读取说明")
            }
            if insights.screenWatch {
                MenuPicker(title: "读取来源",
                           selection: Binding(get: { state.screenTargetID }, set: {
                               insights.screenTarget = $0; insights.screenRegion = nil
                               insights.save(); state.startScreenWatch()
                           }),
                           options: state.captureTargets.isEmpty ? [("", String(localized: "主显示器"))]
                               : state.captureTargets.map { ($0.id, $0.title) })
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
            }
            Note("识别结果会发送给当前回答服务；遇到图表或位置关系时，可能附上当前屏幕图片。", presentation: .inline)
            if insights.screenWatch, state.answerBackend == .openAICompatible {
                Toggle("当前 API 模型支持图像输入", isOn: Binding(get: { state.apiVisionEnabled }, set: { state.apiVisionEnabled = $0; state.updateSettings() }))
            }
            if let error = insights.screenError { Note(error, color: .orange) }
            Divider()
            VStack(alignment: .leading, spacing: 3) {
                Toggle("跟随演示文稿", isOn: $insights.followSlides)
                    .disabled(!insights.screenWatch)
                    .onChange(of: insights.followSlides) { _, _ in insights.save() }
                Note(insights.screenWatch
                     ? "导入 PPT、Keynote 或 PDF 后，放映时认出正在讲第几页，给出这一页的要点与提问。需要“屏幕与系统音频录制”权限。"
                     : "先打开上面的屏幕读取，才能认出演示文稿翻到了哪一页。", presentation: .inline)
            }
            VStack(alignment: .leading, spacing: 3) {
                Toggle("回答进度", isOn: $insights.answerProgress)
                    .onChange(of: insights.answerProgress) { _, _ in insights.save() }
                Note("边听你说，勾掉回答里已经讲过的句子，提示下一句；按你说的话和每句回答的文字重合度判断。", presentation: .inline)
            }
        }
        .sheet(isPresented: $showingRegion) {
            if let image = regionPreview {
                ScreenRegionSheet(image: image) { rect in
                    if let rect { insights.screenRegion = [rect.minX, rect.minY, rect.width, rect.height]; insights.save(); state.startScreenWatch() }
                    showingRegion = false; regionPreview = nil
                }
            }
        }
    }
}
