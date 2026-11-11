import SwiftUI
import NextThoughtCore

/// The subtitle bar: the latest sentence on a floating card, with the original
/// smaller beneath it when there is a translation. Nothing else: answers have
/// their own window.
struct SubtitleBarView: View {
    @Environment(AppState.self) private var state
    let model: SubtitleBarModel

    var body: some View {
        let fontSize = CGFloat(state.liveWindow.subtitleFontSize)
        let visible = model.phase == .visible
        let insets = SubtitleBarController.insets
        card(fontSize: fontSize)
            .frame(width: model.cardWidth)
            .floatingSurface(cornerRadius: Space.radiusLarge)
            .overlay {
                // Resting the pointer on the bar makes it draggable; the outline says so.
                RoundedRectangle(cornerRadius: Space.radiusLarge, style: .continuous)
                    .strokeBorder(Ink.secondary, lineWidth: 1.5)
                    .opacity(model.isEditing ? 1 : 0)
            }
            .overlay(alignment: .topTrailing) {
                if !state.capturePrivacy { sharedBadge.offset(x: -Space.s3, y: -9) }
            }
            .padding(EdgeInsets(top: insets.top, leading: insets.left, bottom: insets.bottom, trailing: insets.right))
            .fixedSize(horizontal: false, vertical: true)
            .opacity(visible ? (model.isDimmed ? 0.15 : 1) : 0)
            .animation(visible ? Motion.expand : Motion.phase, value: visible)
            .animation(Motion.hover, value: model.isDimmed)
            .animation(Motion.hover, value: model.isEditing)
            .foregroundStyle(Ink.ink)
            .preferredColorScheme(state.colorScheme)
    }

    private func card(fontSize: CGFloat) -> some View {
        VStack(spacing: Space.s1) {
            if let deadline = model.autoEndDeadline {
                autoEndLine(deadline: deadline, fontSize: fontSize)
            } else if let display = model.display {
                HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                    // The other side is the default speaker and needs no label.
                    if display.speaker != .remote {
                        Text(display.speaker.displayName)
                            .font(.system(size: max(12, fontSize * 0.5), weight: .medium))
                            .foregroundStyle(Ink.secondary)
                    }
                    Text(verbatim: display.primary)
                        .font(.system(size: fontSize, weight: .medium))
                        .lineLimit(2)
                        .multilineTextAlignment(.center)
                        .fixedSize(horizontal: false, vertical: true)
                }
                if !display.secondary.isEmpty {
                    Text(verbatim: display.secondary)
                        .font(.system(size: max(12, fontSize * 0.6)))
                        .foregroundStyle(Ink.secondary)
                        .lineLimit(1)
                        .multilineTextAlignment(.center)
                }
            } else {
                Text(verbatim: " ").font(.system(size: fontSize, weight: .medium))
            }
        }
        .padding(.horizontal, SubtitleBarController.cardPadding)
        .padding(.vertical, Space.s3)
        .frame(maxWidth: .infinity)
    }

    /// "好像没人说话了，N 秒后结束记录" with 继续记录: the reminder the live page shows as a banner.
    private func autoEndLine(deadline: Date, fontSize: CGFloat) -> some View {
        TimelineView(.periodic(from: .now, by: 1)) { context in
            let left = max(0, Int(deadline.timeIntervalSince(context.date).rounded(.up)))
            HStack(spacing: Space.s3) {
                Image(systemName: "moon.zzz").font(.system(size: max(13, fontSize * 0.6))).accessibilityHidden(true)
                Text("好像没人说话了，\(left) 秒后结束记录")
                    .font(.system(size: max(14, fontSize * 0.75), weight: .medium).monospacedDigit())
                    .contentTransition(.numericText(countsDown: true))
                    .animation(Motion.reduce ? nil : .default, value: left)
                    .lineLimit(1)
                Button("继续记录") { state.keepRecording() }
                    .buttonStyle(PrimaryButtonStyle())
                    .disabled(state.isChangingSession)
            }
            .foregroundStyle(Ink.caution)
            .accessibilityElement(children: .contain)
            .accessibilityIdentifier("subtitleBar.autoEnd")
        }
    }

    /// Shown while the window is not hidden from screen sharing: whoever watches the
    /// shared screen can read the subtitles.
    private var sharedBadge: some View {
        HStack(spacing: Space.s1) {
            StatusDot(tone: .caution, size: 6)
            Text("对方可见").font(Typo.caption)
        }
        .padding(.horizontal, Space.s2)
        .frame(height: 20)
        .background(Ink.surfaceRaised, in: Capsule())
        .overlay(Capsule().strokeBorder(Ink.hairline, lineWidth: 0.5))
    }
}
