import Foundation

/// What a reader left behind with a file: things to check, and things that are only said.
///
/// A page whose text was read but whose pictures were not is complete for answering:
/// that is a note. A page that failed, a blank slide, a diagram without its words or a
/// picture read by the fallback may be wrong: those are warnings, and only they mark a
/// file as needing a look.
public enum DocumentNotice {
    /// The same line said twice says nothing more.
    public static func unique(_ warnings: [String]) -> [String] {
        var seen = Set<String>()
        return warnings.filter { seen.insert($0).inserted }
    }

    public static func isNote(_ warning: String) -> Bool {
        notePhrases.contains { warning.contains($0) }
    }

    /// Lines that need a look.
    public static func warnings(in lines: [String]) -> [String] { unique(lines).filter { !isNote($0) } }

    /// The notes as one quiet line, or nil without any: "p.7、15、17、21 的图片没有读取…".
    public static func note(for lines: [String]) -> String? {
        let notes = unique(lines).filter(isNote)
        guard !notes.isEmpty else { return nil }
        var pages: [Int] = [], slides: [Int] = [], other = false
        for line in notes {
            if let page = number(after: "p.", in: line) { pages.append(page) }
            else if let slide = number(after: "幻灯片 ", in: line) ?? number(after: "Slide ", in: line) { slides.append(slide) }
            else { other = true }
        }
        var places: [String] = []
        if !pages.isEmpty { places.append("p." + list(pages)) }
        if !slides.isEmpty { places.append("幻灯片 " + list(slides)) }
        if places.isEmpty || other { return "部分图片里的文字没有读取，正文已完整提取。" }
        return places.joined(separator: "、") + " 的图片没有读取，正文已完整提取。"
    }

    private static let notePhrases = [
        "页内图像中的文字", "inside page images have not been read",
        "的图片没有读取", "图片里的文字没有读取",
    ]

    private static func number(after prefix: String, in line: String) -> Int? {
        guard line.hasPrefix(prefix) else { return nil }
        return Int(line.dropFirst(prefix.count).prefix { $0.isNumber })
    }

    private static func list(_ numbers: [Int]) -> String {
        Array(Set(numbers)).sorted().map(String.init).joined(separator: "、")
    }
}
