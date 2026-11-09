// Original vector artwork; no platform symbol is embedded in the asset.
import AppKit
import Foundation
let output = URL(fileURLWithPath: CommandLine.arguments.dropFirst().first ?? "Mobile/iOS/NextThoughtCompanion/Assets.xcassets/AppIcon.appiconset/AppIcon.png")
let size = 1024
let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: size, pixelsHigh: size, bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
NSGraphicsContext.saveGraphicsState(); NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
NSColor(deviceRed: 81/255, green: 70/255, blue: 200/255, alpha: 1).setFill()
NSRect(x: 0,y: 0,width: size,height: size).fill()
let scale = NSAffineTransform(); scale.scale(by: 8); scale.concat()
let bubble = NSBezierPath(); bubble.move(to: NSPoint(x:42,y:96)); bubble.line(to:NSPoint(x:86,y:96))
bubble.curve(to:NSPoint(x:98,y:84),controlPoint1:NSPoint(x:98,y:96),controlPoint2:NSPoint(x:98,y:96))
bubble.line(to:NSPoint(x:98,y:54)); bubble.curve(to:NSPoint(x:86,y:42),controlPoint1:NSPoint(x:98,y:42),controlPoint2:NSPoint(x:98,y:42))
bubble.line(to:NSPoint(x:60,y:42)); bubble.line(to:NSPoint(x:43,y:30)); bubble.line(to:NSPoint(x:43,y:42)); bubble.line(to:NSPoint(x:42,y:42))
bubble.curve(to:NSPoint(x:30,y:54),controlPoint1:NSPoint(x:30,y:42),controlPoint2:NSPoint(x:30,y:42)); bubble.line(to:NSPoint(x:30,y:84))
bubble.curve(to:NSPoint(x:42,y:96),controlPoint1:NSPoint(x:30,y:96),controlPoint2:NSPoint(x:30,y:96));bubble.close()
NSColor.white.setStroke(); bubble.lineWidth=6;bubble.lineCapStyle = .round;bubble.lineJoinStyle = .round;bubble.stroke()
for (x,low,high):(CGFloat,CGFloat,CGFloat) in [(48,61,75),(64,54,82),(80,61,75)] { let line=NSBezierPath();line.move(to:NSPoint(x:x,y:low));line.line(to:NSPoint(x:x,y:high));line.lineWidth=6;line.lineCapStyle = .round;line.stroke() }
NSGraphicsContext.restoreGraphicsState()
// Strip the alpha channel for the App Store's opaque icon requirement.
let rgb = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: size, pixelsHigh: size, bitsPerSample: 8, samplesPerPixel: 3, hasAlpha: false, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
for y in 0..<size { for x in 0..<size {
    let source = rep.bitmapData! + y * rep.bytesPerRow + x * 4
    let target = rgb.bitmapData! + y * rgb.bytesPerRow + x * 3
    for channel in 0..<3 { target[channel] = source[channel] }
} }
try rgb.representation(using: .png,properties: [:])!.write(to:output)
print(output.path)
