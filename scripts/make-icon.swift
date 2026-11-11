// Renders the app icon into an .iconset: a cinnabar seal carved with 念 on ink.
//
// 念 means both "a thought" and "to read aloud" — the next thought, said out loud.
// The seal is the app's one accent (see DesignSystem.swift, `Ink.seal`); the ground
// is the black of the cue island.
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
    let bodyPath = NSBezierPath(roundedRect: body, xRadius: body.width * 0.225, yRadius: body.width * 0.225)
    let shadow = NSShadow()
    shadow.shadowColor = NSColor.black.withAlphaComponent(0.3)
    shadow.shadowBlurRadius = size * 0.022
    shadow.shadowOffset = NSSize(width: 0, height: -size * 0.011)
    shadow.set()
    let ground = onPaper ? [color(0xfbfaf6), color(0xece9e1)] : [color(0x2a2926), color(0x0d0d0c)]
    NSGradient(colors: ground)?.draw(in: bodyPath, angle: -90)
    NSShadow().set()

    // A hairline of light along the top edge, the way a real tile catches it.
    NSGraphicsContext.saveGraphicsState()
    bodyPath.addClip()
    NSGradient(colors: [NSColor.white.withAlphaComponent(onPaper ? 0.9 : 0.16), NSColor.white.withAlphaComponent(0)])?
        .draw(in: NSRect(x: body.minX, y: body.maxY - body.height * 0.5, width: body.width, height: body.height * 0.5), angle: -90)
    NSGraphicsContext.restoreGraphicsState()

    // The seal: a cinnabar block, slightly soft at the corners like carved stone.
    let side = body.width * 0.66
    let seal = NSRect(x: body.midX - side / 2, y: body.midY - side / 2, width: side, height: side)
    let sealPath = NSBezierPath(roundedRect: seal, xRadius: side * 0.13, yRadius: side * 0.13)
    let sealShadow = NSShadow()
    sealShadow.shadowColor = (onPaper ? color(0x7a1f10, 0.35) : NSColor.black.withAlphaComponent(0.55))
    sealShadow.shadowBlurRadius = size * 0.03
    sealShadow.shadowOffset = NSSize(width: 0, height: -size * 0.014)
    sealShadow.set()
    NSGradient(colors: [color(0xd9523a), color(0xb73a24)])?.draw(in: sealPath, angle: -90)
    NSShadow().set()

    // 念, cut out of the block in white (a "white-character" seal).
    let font = NSFont(name: "STSongti-SC-Black", size: side * 0.74)
        ?? NSFont(name: "Songti SC", size: side * 0.74)
        ?? NSFont.systemFont(ofSize: side * 0.74, weight: .heavy)
    let glyph = NSAttributedString(string: "念", attributes: [.font: font, .foregroundColor: color(0xfffaf2)])
    let line = CTLineCreateWithAttributedString(glyph)
    // Centre on the ink the glyph actually covers, not on its line box.
    let bounds = CTLineGetBoundsWithOptions(line, .useGlyphPathBounds)
    if let context = NSGraphicsContext.current?.cgContext {
        context.textPosition = CGPoint(x: seal.midX - bounds.midX, y: seal.midY - bounds.midY)
        CTLineDraw(line, context)
    }
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
