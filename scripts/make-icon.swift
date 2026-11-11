// Renders the app icon into an .iconset: two dots gathering into a line.
//
// The mark needs no language: a thought forming (the dots anyone knows from a
// typing indicator) becomes the next line to say (the pill, which is also the shape
// of the cue island). The line is the app's one accent (`Ink.seal`); the ground is
// the black of the island, or paper with `paper`.
// Usage: swift scripts/make-icon.swift <output.iconset> [ink|paper]
import AppKit

let arguments = Array(CommandLine.arguments.dropFirst())
let output = URL(fileURLWithPath: arguments.first ?? "AppIcon.iconset")
let onPaper = arguments.dropFirst().first == "paper"
try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)

func color(_ hex: UInt32, _ alpha: CGFloat = 1) -> NSColor {
    NSColor(srgbRed: CGFloat((hex >> 16) & 0xff) / 255, green: CGFloat((hex >> 8) & 0xff) / 255,
            blue: CGFloat(hex & 0xff) / 255, alpha: alpha)
}

func pill(_ rect: NSRect) -> NSBezierPath {
    NSBezierPath(roundedRect: rect, xRadius: rect.height / 2, yRadius: rect.height / 2)
}

func render(_ pixels: Int) -> Data? {
    let size = CGFloat(pixels)
    guard let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: pixels, pixelsHigh: pixels, bitsPerSample: 8, samplesPerPixel: 4,
                                     hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0) else { return nil }
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    defer { NSGraphicsContext.restoreGraphicsState() }

    // macOS icon grid: an 824/1024 body with a soft shadow in the margin.
    let inset = size * 0.0977
    let body = NSRect(x: inset, y: inset, width: size - inset * 2, height: size - inset * 2)
    let width = body.width
    let tile = NSBezierPath(roundedRect: body, xRadius: width * 0.225, yRadius: width * 0.225)
    let shadow = NSShadow()
    shadow.shadowColor = NSColor.black.withAlphaComponent(0.3)
    shadow.shadowBlurRadius = size * 0.022
    shadow.shadowOffset = NSSize(width: 0, height: -size * 0.011)
    NSGraphicsContext.saveGraphicsState()
    shadow.set()
    NSGradient(colors: onPaper ? [color(0xfdfcf9), color(0xe9e5dc)] : [color(0x2b2a27), color(0x0c0c0b)])?.draw(in: tile, angle: -90)
    NSGraphicsContext.restoreGraphicsState()

    NSGraphicsContext.saveGraphicsState()
    tile.addClip()
    // Light along the top edge, the way a real tile catches it.
    NSGradient(colors: [NSColor.white.withAlphaComponent(onPaper ? 0.22 : 0.1), NSColor.white.withAlphaComponent(0)])?
        .draw(in: NSRect(x: body.minX, y: body.maxY - width * 0.3, width: width, height: width * 0.3), angle: -90)

    // Two dots that grow, then the line.
    let small = width * 0.078, large = width * 0.104, height = width * 0.13, length = width * 0.40, gap = width * 0.062
    var x = body.midX - (small + large + length + gap * 2) / 2
    let middle = body.midY
    let dot = onPaper ? color(0x1a1917) : color(0xfffaf2)
    dot.withAlphaComponent(onPaper ? 0.28 : 0.34).setFill()
    pill(NSRect(x: x, y: middle - small / 2, width: small, height: small)).fill()
    x += small + gap
    dot.withAlphaComponent(onPaper ? 0.62 : 0.72).setFill()
    pill(NSRect(x: x, y: middle - large / 2, width: large, height: large)).fill()
    x += large + gap
    let line = pill(NSRect(x: x, y: middle - height / 2, width: length, height: height))
    let glow = NSShadow()
    glow.shadowColor = color(0xe2573d, onPaper ? 0.35 : 0.6)
    glow.shadowBlurRadius = size * 0.06
    glow.shadowOffset = NSSize(width: 0, height: onPaper ? -size * 0.012 : 0)
    NSGraphicsContext.saveGraphicsState()
    glow.set()
    NSGradient(colors: [color(0xf47358), color(0xd2452c)])?.draw(in: line, angle: -90)
    NSGraphicsContext.restoreGraphicsState()
    // A sheen on the line's upper half, so it reads as a lit object.
    NSGraphicsContext.saveGraphicsState()
    line.addClip()
    NSGradient(colors: [NSColor.white.withAlphaComponent(0.28), NSColor.white.withAlphaComponent(0)])?
        .draw(in: NSRect(x: x, y: middle, width: length, height: height / 2), angle: -90)
    NSGraphicsContext.restoreGraphicsState()
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
