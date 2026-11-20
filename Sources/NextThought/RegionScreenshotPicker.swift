import AppKit
import SwiftUI

struct RegionScreenshotPicker: View {
    let image: NSImage
    let displays: [ScreenshotDisplay]
    let selectedDisplayID: UInt32?
    let onDisplaySelected: (UInt32) -> Void
    let onCancel: () -> Void
    let onCapture: (CGRect) -> Void

    @State private var selectedRect: CGRect?

    private var displaySelection: Binding<UInt32> {
        Binding(
            get: { selectedDisplayID ?? displays.first?.id ?? 0 },
            set: onDisplaySelected)
    }

    var body: some View {
        VStack(spacing: 12) {
            HStack {
                Label("拖动选择截图区域", systemImage: "viewfinder")
                    .font(.zoomed(.headline))
                Spacer()
                Picker("显示器", selection: displaySelection) {
                    ForEach(displays) { display in
                        Text("\(display.title) · \(display.width)×\(display.height)")
                            .tag(display.id)
                    }
                }
                .frame(maxWidth: 310)
            }

            RegionSelectionCanvas(image: image, selection: $selectedRect)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(.black.opacity(0.08), in: RoundedRectangle(cornerRadius: 12))

            HStack {
                Text("截图会裁切到选中范围。请把题目、代码或公式完整框入。")
                    .font(.zoomed(.caption))
                    .foregroundStyle(.secondary)
                Spacer()
                Button("取消", action: onCancel)
                Button("使用此区域") {
                    if let selectedRect { onCapture(selectedRect) }
                }
                .buttonStyle(.borderedProminent)
                .disabled(selectedRect == nil)
            }
        }
        .padding(16)
        .frame(minWidth: 720, minHeight: 540)
        .onChange(of: selectedDisplayID) { _, _ in selectedRect = nil }
    }
}

struct RegionSelectionCanvas: View {
    let image: NSImage
    @Binding var selection: CGRect?

    @State private var dragOrigin: CGPoint?

    var body: some View {
        GeometryReader { geometry in
            let imageRect = fittedRect(imageSize: image.size, in: geometry.size)
            ZStack(alignment: .topLeading) {
                Image(nsImage: image)
                    .resizable()
                    .frame(width: imageRect.width, height: imageRect.height)
                    .position(x: imageRect.midX, y: imageRect.midY)

                if let selection {
                    Rectangle()
                        .fill(Color.accentColor.opacity(0.16))
                        .overlay(Rectangle().stroke(Color.accentColor, lineWidth: 2))
                        .frame(width: selection.width * imageRect.width, height: selection.height * imageRect.height)
                        .position(
                            x: imageRect.minX + selection.midX * imageRect.width,
                            y: imageRect.minY + selection.midY * imageRect.height)
                }
            }
            .frame(width: geometry.size.width, height: geometry.size.height)
            .contentShape(Rectangle())
            .gesture(
                DragGesture(minimumDistance: 1)
                    .onChanged { value in
                        let start = dragOrigin ?? clamp(value.startLocation, to: imageRect)
                        if dragOrigin == nil { dragOrigin = start }
                        let end = clamp(value.location, to: imageRect)
                        selection = normalizedRect(from: start, to: end, inside: imageRect)
                    }
                    .onEnded { _ in dragOrigin = nil })
        }
    }

    private func fittedRect(imageSize: NSSize, in container: CGSize) -> CGRect {
        let scale = min(container.width / max(imageSize.width, 1), container.height / max(imageSize.height, 1))
        let size = CGSize(width: imageSize.width * scale, height: imageSize.height * scale)
        return CGRect(x: (container.width - size.width) / 2, y: (container.height - size.height) / 2, width: size.width, height: size.height)
    }

    private func clamp(_ point: CGPoint, to rect: CGRect) -> CGPoint {
        CGPoint(x: min(max(point.x, rect.minX), rect.maxX), y: min(max(point.y, rect.minY), rect.maxY))
    }

    private func normalizedRect(from start: CGPoint, to end: CGPoint, inside rect: CGRect) -> CGRect {
        let selected = CGRect(x: start.x, y: start.y, width: end.x - start.x, height: end.y - start.y).standardized
        return CGRect(
            x: (selected.minX - rect.minX) / rect.width,
            y: (selected.minY - rect.minY) / rect.height,
            width: selected.width / rect.width,
            height: selected.height / rect.height)
    }
}
