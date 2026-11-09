import AppKit
import AVFoundation

final class MediaDelegate: NSObject, NSApplicationDelegate, AVAudioPlayerDelegate {
    private var window: NSWindow!
    private var imageView: NSImageView!
    private var status: NSTextField!
    private var player: AVAudioPlayer?
    private let resources = Bundle.main.resourceURL!
    private let sounds = ["zh", "en", "mixed"]

    func applicationDidFinishLaunching(_ notification: Notification) {
        window = NSWindow(contentRect: NSRect(x: 40, y: 160, width: 940, height: 750), styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.title = "NextThought 测试媒体 · 合成问题"
        let content = NSStackView()
        content.orientation = .vertical
        content.spacing = 10
        content.edgeInsets = NSEdgeInsets(top: 12, left: 12, bottom: 12, right: 12)
        content.translatesAutoresizingMaskIntoConstraints = false
        window.contentView!.addSubview(content)
        NSLayoutConstraint.activate([
            content.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor),
            content.trailingAnchor.constraint(equalTo: window.contentView!.trailingAnchor),
            content.topAnchor.constraint(equalTo: window.contentView!.topAnchor),
            content.bottomAnchor.constraint(equalTo: window.contentView!.bottomAnchor)
        ])
        let audioButtons = NSStackView()
        let labels = ["播放中文问题", "Play English question", "播放中英混合问题"]
        for index in 0..<labels.count {
            let button = NSButton(title: labels[index], target: self, action: #selector(play(_:)))
            button.tag = index
            audioButtons.addArrangedSubview(button)
        }
        audioButtons.addArrangedSubview(NSButton(title: "停止播放", target: self, action: #selector(stop)))
        content.addArrangedSubview(audioButtons)
        let imageButtons = NSStackView()
        imageButtons.addArrangedSubview(NSButton(title: "显示代码题", target: self, action: #selector(showCode)))
        imageButtons.addArrangedSubview(NSButton(title: "显示数学题", target: self, action: #selector(showMath)))
        content.addArrangedSubview(imageButtons)
        status = NSTextField(labelWithString: "就绪 · 仅播放合成测试录音，无麦克风采集")
        status.font = .systemFont(ofSize: 13)
        content.addArrangedSubview(status)
        imageView = NSImageView()
        imageView.imageScaling = .scaleProportionallyUpOrDown
        imageView.image = NSImage(contentsOf: resources.appendingPathComponent("binary-search.png"))
        imageView.setContentHuggingPriority(.defaultLow, for: .vertical)
        content.addArrangedSubview(imageView)
        imageView.widthAnchor.constraint(equalTo: content.widthAnchor, constant: -24).isActive = true
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    @objc private func play(_ button: NSButton) {
        player?.stop()
        do {
            player = try AVAudioPlayer(contentsOf: resources.appendingPathComponent(sounds[button.tag] + ".wav"))
            player?.delegate = self
            player?.prepareToPlay()
            player?.play()
            status.stringValue = "播放：\(sounds[button.tag]) · \(String(format: "%.2f", player!.duration)) 秒 · \(Date().formatted(date: .omitted, time: .standard))"
        } catch {
            status.stringValue = "播放失败：\(error.localizedDescription)"
        }
    }

    func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        status.stringValue = "播放完成 · \(Date().formatted(date: .omitted, time: .standard))"
    }

    @objc private func stop() { player?.stop(); status.stringValue = "已停止播放" }
    @objc private func showCode() { imageView.image = NSImage(contentsOf: resources.appendingPathComponent("binary-search.png")) }
    @objc private func showMath() { imageView.image = NSImage(contentsOf: resources.appendingPathComponent("math.png")) }
}

let app = NSApplication.shared
let delegate = MediaDelegate()
app.setActivationPolicy(.regular)
app.delegate = delegate
app.run()
