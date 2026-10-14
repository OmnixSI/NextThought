import AppKit

let output = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)
try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)

func render(_ name: String, title: String, body: String, code: String) throws {
    let image = NSImage(size: NSSize(width: 1_600, height: 1_100))
    image.lockFocus()
    NSColor(calibratedWhite: 0.97, alpha: 1).setFill()
    NSBezierPath(rect: NSRect(x: 0, y: 0, width: 1_600, height: 1_100)).fill()
    (title as NSString).draw(in: NSRect(x: 80, y: 965, width: 1_440, height: 70), withAttributes: [
        .font: NSFont.systemFont(ofSize: 48, weight: .bold), .foregroundColor: NSColor.black
    ])
    (body as NSString).draw(in: NSRect(x: 80, y: 790, width: 1_440, height: 160), withAttributes: [
        .font: NSFont.systemFont(ofSize: 34), .foregroundColor: NSColor.black
    ])
    NSColor.white.setFill()
    NSBezierPath(roundedRect: NSRect(x: 60, y: 120, width: 1_480, height: 640), xRadius: 20, yRadius: 20).fill()
    (code as NSString).draw(in: NSRect(x: 95, y: 145, width: 1_400, height: 585), withAttributes: [
        .font: NSFont.monospacedSystemFont(ofSize: 33, weight: .regular), .foregroundColor: NSColor.black
    ])
    ("InterviewHacker • Synthetic validation fixture • No private data" as NSString).draw(
        at: NSPoint(x: 80, y: 60), withAttributes: [.font: NSFont.systemFont(ofSize: 23), .foregroundColor: NSColor.darkGray])
    image.unlockFocus()
    let bitmap = NSBitmapImageRep(data: image.tiffRepresentation!)!
    try bitmap.representation(using: .png, properties: [:])!.write(to: output.appendingPathComponent(name))
}

try render("binary-search.png", title: "二分查找：指出错误并修复", body: "输入：values = [1, 3]，target = 3。\n下列代码为什么可能死循环？给出修复和时间、空间复杂度。", code: """
def search(values, target):
    left = 0
    right = len(values) - 1
    while left <= right:
        mid = (left + right) // 2
        if values[mid] == target:
            return mid
        if values[mid] < target:
            left = mid
        else:
            right = mid - 1
    return -1
""")

try render("math.png", title: "课程问题：定积分", body: "计算下面的定积分，并给出必要的计算步骤。\n这是完整题目，无其他条件。", code: """
               1
              ∫   x² dx
               0

Question: Find the exact value.
Show the antiderivative and endpoint calculation.
""")
