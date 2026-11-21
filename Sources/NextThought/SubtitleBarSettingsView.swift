import SwiftUI

/// Settings card for the subtitle bar, shown beside the answer overlay settings.
struct SubtitleBarSettingsSection: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var live = state.liveWindow
        SettingsSection(title: "字幕条", icon: "captions.bubble") {
            HStack {
                Text("字号")
                Slider(value: $live.subtitleFontSize, in: LiveWindowController.fontSizeRange, step: 1)
                    .frame(maxWidth: 320)
                    .onChange(of: live.subtitleFontSize) { _, _ in live.saveSubtitleFontSize() }
                Text("\(Int(live.subtitleFontSize))").font(Font.zoomed(.caption).monospacedDigit()).frame(width: 36)
            }
            Button("恢复默认位置") { SubtitleBarController.shared.resetPosition() }
                .buttonStyle(SecondaryButtonStyle())
            Note("只在有人说话时出现，说完几秒后淡出。鼠标划过时变透明，停一下就能拖动。")
        }
    }
}
