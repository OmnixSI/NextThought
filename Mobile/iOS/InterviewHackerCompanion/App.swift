import CompanionMobileCore
import SwiftUI
import UIKit

@main struct InterviewHackerCompanionApp: App {
    @StateObject private var client = NativeCompanionClient()
    @Environment(\.scenePhase) private var phase
    var body: some Scene {
        WindowGroup {
            CompanionView(client: client)
                .onOpenURL { url in NotificationCenter.default.post(name: .init("PairLink"), object: url.absoluteString) }
                .onChange(of: phase) { value in
                    UIApplication.shared.isIdleTimerDisabled = value == .active && client.pairing != nil
                    if value == .active { client.resume() }
                }
        }
    }
}
struct CompanionView: View {
    @ObservedObject var client: NativeCompanionClient
    @State private var link = "", error = "", scanning = false, enableBluetooth = true
    @State private var fontSize: CGFloat = 22
    var body: some View {
        NavigationStack {
            VStack(alignment: .leading, spacing: 12) {
                HStack { Circle().fill(client.connection.contains("已连接") ? .green : .orange).frame(width: 8, height: 8); Text(client.connection).font(.caption) }
                if client.pairing == nil {
                    Text("在 Mac 设置 → 手机伴侣中开启服务，然后扫描 App 配对码。").foregroundStyle(.secondary)
                    TextField("粘贴配对链接", text: $link, axis: .vertical).textInputAutocapitalization(.never).autocorrectionDisabled().lineLimit(2...4).textFieldStyle(.roundedBorder)
                    Toggle("启用蓝牙备用", isOn: $enableBluetooth)
                    HStack {
                        Button("扫描二维码") { scanning = true }
                        Button("粘贴") { link = UIPasteboard.general.string ?? "" }
                        Button("连接") { connect(link) }.buttonStyle(.borderedProminent)
                    }
                    Text("配对密钥仅保留在当前 App 进程。局域网优先，蓝牙及已启用的加密中转作为备用；当前版本面向前台使用。").font(.footnote).foregroundStyle(.secondary)
                } else {
                    Text(client.bluetoothStatus).font(.caption).foregroundStyle(.secondary)
                    if let s = client.snapshot {
                        Text(s.status + (s.answering ? " · 生成中" : "")).font(.caption).foregroundStyle(.secondary)
                        Text(s.question).font(.headline)
                        ScrollView { Text(s.answer.isEmpty ? (s.answering ? "正在生成…" : "等待回答…") : s.answer).font(.system(size: fontSize)).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading) }
                        if let sources = s.sources { Text(sources).font(.caption).foregroundStyle(.secondary).lineLimit(3) }
                        if let slide = s.slide { Text(slide).font(.caption).foregroundStyle(.secondary) }
                        if s.updateAvailable == true { Button("显示核查更新") { client.action("applyWeb") } }
                    } else { ProgressView("等待 Mac 当前状态…").frame(maxHeight: .infinity) }
                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack {
                            command("回答", "answer"); command("简短", "shorter"); command("举例", "example"); command("英文", "english")
                            command("上一条", "previous"); command("下一条", "next"); command("取消", "cancel")
                        }.buttonStyle(.bordered)
                    }
                }
                if !error.isEmpty { Text(error).font(.caption).foregroundStyle(.red) }
                Spacer(minLength: 0)
            }.padding()
            .navigationTitle("InterviewHacker").navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItemGroup(placement: .navigationBarTrailing) {
                    if client.pairing != nil {
                        Button("A−") { fontSize = max(14, fontSize - 2) }; Button("A+") { fontSize = min(40, fontSize + 2) }
                        Button("断开") { client.stop(); UIApplication.shared.isIdleTimerDisabled = false }
                    }
                }
            }
            .sheet(isPresented: $scanning) { QRScanner { value in scanning = false; connect(value) }.ignoresSafeArea().overlay(alignment: .topTrailing) { Button("取消") { scanning = false }.padding().buttonStyle(.borderedProminent) } }
            .onReceive(NotificationCenter.default.publisher(for: .init("PairLink"))) { event in if let value = event.object as? String { connect(value) } }
        }
    }
    func connect(_ value: String) {
        do { try client.start(link: value, enableBluetooth: enableBluetooth); link = ""; error = ""; UIApplication.shared.isIdleTimerDisabled = true }
        catch { error = error.localizedDescription }
    }
    func command(_ label: String, _ action: String) -> some View { Button(label) { client.action(action) } }
}
