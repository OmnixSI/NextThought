// Renders the app icon (gradient squircle + waveform/mic symbol) into an .iconset.
// Usage: swift scripts/make-icon.swift <output.iconset>
import AppKit

let output = URL(fileURLWithPath: CommandLine.arguments.dropFirst().first ?? "AppIcon.iconset")
try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)

func render(_ pixels: Int) -> Data? {
    let size = CGFloat(pixels)
    guard let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: pixels, pixelsHigh: pixels, bitsPerSample: 8, samplesPerPixel: 4,
                                     hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0) else { return nil }
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    // macOS icon grid: 824/1024 body with a soft shadow margin.
    let inset = size * 0.0977
    let body = NSRect(x: inset, y: inset, width: size - inset * 2, height: size - inset * 2)
    let path = NSBezierPath(roundedRect: body, xRadius: body.width * 0.225, yRadius: body.width * 0.225)
    let shadow = NSShadow()
    shadow.shadowColor = NSColor.black.withAlphaComponent(0.28)
    shadow.shadowBlurRadius = size * 0.02
    shadow.shadowOffset = NSSize(width: 0, height: -size * 0.01)
    shadow.set()
    NSGradient(colors: [NSColor(calibratedRed: 0.36, green: 0.42, blue: 1.0, alpha: 1),
                        NSColor(calibratedRed: 0.55, green: 0.27, blue: 0.95, alpha: 1)])?.draw(in: path, angle: -60)
    NSShadow().set()
    if let symbol = NSImage(systemSymbolName: "waveform.and.mic", accessibilityDescription: nil)?
        .withSymbolConfiguration(.init(pointSize: size * 0.42, weight: .semibold)) {
        let tinted = NSImage(size: symbol.size, flipped: false) { rect in
            symbol.draw(in: rect)
            NSColor.white.set()
            rect.fill(using: .sourceAtop)
            return true
        }
        let s = tinted.size
        tinted.draw(in: NSRect(x: (size - s.width) / 2, y: (size - s.height) / 2, width: s.width, height: s.height))
    }
    NSGraphicsContext.restoreGraphicsState()
    return rep.representation(using: .png, properties: [:])
}

for (points, scales) in [(16, [1, 2]), (32, [1, 2]), (128, [1, 2]), (256, [1, 2]), (512, [1, 2])] {
    for scale in scales {
        let name = scale == 1 ? "icon_\(points)x\(points).png" : "icon_\(points)x\(points)@2x.png"
        guard let data = render(points * scale) else { exit(1) }
        try data.write(to: output.appendingPathComponent(name))
    }
}
print("Wrote \(output.path)")
