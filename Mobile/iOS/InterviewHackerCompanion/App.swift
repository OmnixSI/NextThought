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
    @State private var link = ""
    @State private var error = ""
    @State private var scanning = false
    @State private var enableBluetooth = true
    @State private var showsLink = false
    @State private var fontScale: CGFloat = 1
    @ScaledMetric(relativeTo: .body) private var answerSize: CGFloat = 22
    var body: some View {
        NavigationStack {
            VStack(alignment: .leading, spacing: 12) {
                HStack(spacing: 8) {
                    Image(systemName: client.connection.contains("已连接") ? "checkmark.circle.fill" : "antenna.radiowaves.left.and.right")
                        .foregroundStyle(client.connection.contains("已连接") ? .green : .secondary)
                    Text(client.connection).font(.subheadline)
                }.accessibilityElement(children: .combine)
                if client.pairing == nil { pairingContent }
                else { answerContent }
                if !error.isEmpty {
                    Label(error, systemImage: "exclamationmark.circle").font(.footnote).foregroundStyle(.red)
                }
            }
            .padding(.horizontal, 16).padding(.top, 12)
            .background(Color(uiColor: .systemGroupedBackground))
            .safeAreaInset(edge: .bottom, spacing: 0) {
                if client.pairing != nil { controls }
            }
            .navigationTitle("InterviewHacker").navigationBarTitleDisplayMode(.inline)
            .tint(.indigo)
            .toolbar {
                ToolbarItemGroup(placement: .navigationBarTrailing) {
                    if client.pairing != nil {
                        Menu {
                            Button("增大回答字号") { fontScale = min(1.8, fontScale + 0.1) }
                            Button("减小回答字号") { fontScale = max(0.65, fontScale - 0.1) }
                            Button("恢复默认字号") { fontScale = 1 }
                        } label: { Image(systemName: "textformat.size") }.accessibilityLabel("回答字号")
                        Button("断开") { client.stop(); UIApplication.shared.isIdleTimerDisabled = false }
                    }
                }
            }
            .sheet(isPresented: $scanning) {
                QRScanner { value in scanning = false; connect(value) }.ignoresSafeArea()
                    .overlay(alignment: .topTrailing) { Button("取消") { scanning = false }.padding().buttonStyle(.borderedProminent) }
            }
            .onReceive(NotificationCenter.default.publisher(for: .init("PairLink"))) { event in
                if let value = event.object as? String { connect(value) }
            }
        }
    }
    private var pairingContent: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                VStack(alignment: .leading, spacing: 8) {
                    Image(systemName: "iphone.and.arrow.forward").font(.system(size: 32)).foregroundStyle(.indigo).accessibilityHidden(true)
                    Text("把当前回答放到手机上").font(.title2.weight(.semibold))
                    Text("在 Mac 的高级设置 → 更多 → 手机伴侣中开启服务，选择“已安装手机 App”。").foregroundStyle(.secondary)
                }
                Button { scanning = true } label: {
                    Label("扫描二维码", systemImage: "qrcode.viewfinder").frame(maxWidth: .infinity, minHeight: 36)
                }.buttonStyle(.borderedProminent)
                DisclosureGroup("使用配对链接", isExpanded: $showsLink) {
                    VStack(alignment: .leading, spacing: 12) {
                        TextField("粘贴 Mac 的 App 配对链接", text: $link, axis: .vertical)
                            .textInputAutocapitalization(.never).autocorrectionDisabled().lineLimit(2...4).textFieldStyle(.roundedBorder)
                        HStack {
                            Button("粘贴") { link = UIPasteboard.general.string ?? "" }.frame(minHeight: 44)
                            Spacer()
                            Button("连接") { connect(link) }.buttonStyle(.bordered).disabled(link.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                        }
                    }.padding(.top, 8)
                }
                Toggle("启用蓝牙备用", isOn: $enableBluetooth)
                Text("局域网优先；蓝牙和已启用的加密中转作为备用。配对密钥仅保留在当前 App 进程，当前版本需保持前台使用。")
                    .font(.footnote).foregroundStyle(.secondary)
            }.padding(.vertical, 12)
        }
    }
    @ViewBuilder private var answerContent: some View {
        Text(client.bluetoothStatus).font(.footnote).foregroundStyle(.secondary)
        if let s = client.snapshot {
            HStack(spacing: 8) {
                Text(s.status + (s.answering ? " · 生成中" : "")).font(.footnote).foregroundStyle(.secondary)
                Spacer(minLength: 0)
                command("上一条", "previous", symbol: "chevron.left")
                Text(s.position.isEmpty ? "当前" : s.position).font(.footnote.monospacedDigit()).foregroundStyle(.secondary)
                command("下一条", "next", symbol: "chevron.right")
            }
            ScrollView {
                VStack(alignment: .leading, spacing: 16) {
                    if !s.question.isEmpty { Text(s.question).font(.headline).textSelection(.enabled) }
                    Text(s.answer.isEmpty ? (s.answering ? "正在生成…" : "等待 Mac 的下一条回答…") : s.answer)
                        .font(.system(size: answerSize * fontScale)).lineSpacing(5).textSelection(.enabled)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(16).background(Color(uiColor: .secondarySystemGroupedBackground), in: RoundedRectangle(cornerRadius: 16))
                    if let sources = s.sources, !sources.isEmpty {
                        DisclosureGroup("本次来源") { Text(sources).font(.footnote).foregroundStyle(.secondary).textSelection(.enabled) }
                    }
                    if let slide = s.slide, !slide.isEmpty { Label(slide, systemImage: "rectangle.on.rectangle").font(.footnote).foregroundStyle(.secondary) }
                    if s.updateAvailable == true { Button("显示核查更新") { client.action("applyWeb") }.buttonStyle(.bordered).disabled(client.pendingActions.contains("applyWeb")) }
                }.padding(.bottom, 16)
            }
        } else {
            ProgressView("等待 Mac 当前状态…").frame(maxWidth: .infinity, maxHeight: .infinity)
        }
    }
    private var controls: some View {
        VStack(alignment: .leading, spacing: 8) {
            if !client.operationStatus.isEmpty {
                Text(client.operationStatus).font(.footnote).foregroundStyle(.secondary).accessibilityAddTraits(.updatesFrequently)
            }
            if client.canRetryAction { Button("重试操作") { client.retryAction() }.buttonStyle(.bordered) }
            let answering = client.snapshot?.answering == true
            Button { client.action(answering ? "cancel" : "answer") } label: {
                Label(answering ? "停止生成" : "回答", systemImage: answering ? "stop.fill" : "sparkles")
                    .frame(maxWidth: .infinity, minHeight: 36)
            }.buttonStyle(.borderedProminent).disabled(!answering && client.pendingActions.contains("answer"))
            HStack(spacing: 8) {
                command("更简短", "shorter")
                command("举例", "example")
                command("英文版", "english")
            }
        }.padding(.horizontal, 16).padding(.vertical, 10).background(.regularMaterial)
    }
    func connect(_ value: String) {
        do { try client.start(link: value, enableBluetooth: enableBluetooth); link = ""; error = ""; UIApplication.shared.isIdleTimerDisabled = true }
        catch { error = error.localizedDescription; showsLink = true }
    }
    func command(_ label: String, _ action: String, symbol: String? = nil) -> some View {
        Button { client.action(action) } label: {
            if let symbol { Image(systemName: symbol).frame(minWidth: 32, minHeight: 32).accessibilityLabel(label) }
            else { Text(label).frame(maxWidth: .infinity, minHeight: 32) }
        }.buttonStyle(.bordered).disabled(client.pendingActions.contains(action))
    }
}
