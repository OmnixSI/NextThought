import AppKit
import SwiftUI

struct ScreenRegionSheet: View {
    let image: NSImage
    let complete: (CGRect?) -> Void
    @State private var selection: CGRect?
    var body: some View {
        VStack(spacing: 12) {
            Text("选择持续读取的区域").font(.headline)
            RegionSelectionCanvas(image: image, selection: $selection)
            HStack {
                Text("框选演示内容；后续画面均读取该区域。").font(.caption).foregroundStyle(.secondary)
                Spacer()
                Button("取消") { complete(nil) }
                Button("使用此区域") { complete(selection) }.disabled(selection == nil).buttonStyle(.borderedProminent)
            }
        }.padding().frame(minWidth: 720, minHeight: 500)
    }
}
