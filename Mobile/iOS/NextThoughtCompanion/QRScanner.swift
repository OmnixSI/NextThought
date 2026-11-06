import AVFoundation
import SwiftUI
import UIKit

struct QRScanner: UIViewControllerRepresentable {
    var onCode: (String) -> Void
    func makeUIViewController(context: Context) -> ScannerController { let c = ScannerController(); c.onCode = onCode; return c }
    func updateUIViewController(_ uiViewController: ScannerController, context: Context) {}
    static func dismantleUIViewController(_ controller: ScannerController, coordinator: ()) { controller.stop() }
}
final class ScannerController: UIViewController, AVCaptureMetadataOutputObjectsDelegate {
    var onCode: (String) -> Void = { _ in }
    private let session = AVCaptureSession()
    private let sessionQueue = DispatchQueue(label: "com.nextthought.companion.camera")
    private var stopped = false
    private var preview: AVCaptureVideoPreviewLayer?
    private var completed = false
    override func viewDidLoad() {
        super.viewDidLoad(); view.backgroundColor = .black
        AVCaptureDevice.requestAccess(for: .video) { [weak self] granted in
            DispatchQueue.main.async {
                guard let self, !self.stopped else { return }
                if granted { self.configure() } else { self.message("相机未授权；可返回并粘贴配对链接。") }
            }
        }
    }
    private func configure() {
        guard !stopped else { return }
        guard let device = AVCaptureDevice.default(for: .video), let input = try? AVCaptureDeviceInput(device: device), session.canAddInput(input) else { message("此设备无法使用相机，请粘贴配对链接。"); return }
        session.addInput(input)
        let output = AVCaptureMetadataOutput(); guard session.canAddOutput(output) else { message("无法启动扫码，请返回并粘贴配对链接。"); return }
        session.addOutput(output); output.setMetadataObjectsDelegate(self, queue: .main); output.metadataObjectTypes = [.qr]
        let layer = AVCaptureVideoPreviewLayer(session: session); layer.videoGravity = .resizeAspectFill; layer.frame = view.bounds; view.layer.addSublayer(layer); preview = layer
        sessionQueue.async { [session] in session.startRunning() }
    }
    override func viewDidLayoutSubviews() { super.viewDidLayoutSubviews(); preview?.frame = view.bounds }
    func stop() {
        stopped = true
        // Serial ordering prevents a delayed start from reopening the camera
        // after dismissal, even while the permission prompt is completing.
        sessionQueue.async { [session] in session.stopRunning() }
    }
    private func message(_ value: String) { let label = UILabel(frame: view.bounds.insetBy(dx: 24, dy: 80)); label.text = value; label.textColor = .white; label.numberOfLines = 0; view.addSubview(label) }
    func metadataOutput(_ output: AVCaptureMetadataOutput, didOutput metadataObjects: [AVMetadataObject], from connection: AVCaptureConnection) {
        guard !completed, let value = (metadataObjects.first as? AVMetadataMachineReadableCodeObject)?.stringValue else { return }
        completed = true; stop(); onCode(value)
    }
}
