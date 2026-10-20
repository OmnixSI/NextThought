import AppKit
import Foundation

/// Uses an open-source document converter that is already installed on the
/// machine, and falls back to the built-in conversion when there is none.
///
/// Two converters are recognized:
///
/// - **Microsoft MarkItDown** (MIT) — turns PDF, PowerPoint, Word, Excel, HTML,
///   CSV, JSON and images into Markdown. It is the closest thing to "one tool
///   for every format" and is what this pipeline is modelled on.
/// - **Pandoc** (GPL) — the classic document converter. It reads Word,
///   OpenDocument, HTML, RTF and CSV, but not PowerPoint, Excel or PDF.
///
/// Both run entirely locally; nothing is uploaded. The built-in converter stays
/// the default for PDF and plain text because it is much faster there, and any
/// failure falls back to it.
enum ExternalMarkdownConverter {
    struct Output {
        var markdown: String
        var sections: [String]
        /// Display name of the tool that produced the Markdown.
        var tool: String
    }

    /// The formats where an external converter is preferred over the built-in
    /// one: complex Office layouts that the built-in reader can only flatten.
    private static let preferredFormats: Set<String> = ["docx", "odt", "pptx", "xlsx"]

    /// Converts the file with an installed converter, or returns nil when there
    /// is no converter, the format is not supported by it, or it failed.
    static func convert(_ url: URL, format: String) -> Output? {
        guard preferredFormats.contains(format) else { return nil }

        if let markItDown = executable(named: "markitdown") {
            if let markdown = run(markItDown, arguments: [url.path], timeout: 120), !markdown.isEmpty {
                return Output(markdown: markdown, sections: sections(of: markdown), tool: "MarkItDown")
            }
        }
        if let pandoc = executable(named: "pandoc"), let inputFormat = pandocFormat(format) {
            let arguments = ["-f", inputFormat, "-t", "gfm", "--wrap=none", url.path]
            if let markdown = run(pandoc, arguments: arguments, timeout: 120), !markdown.isEmpty {
                return Output(markdown: markdown, sections: sections(of: markdown), tool: "Pandoc")
            }
        }
        return nil
    }

    /// The converters found on this machine, for the UI.
    static func availableTools() -> [String] {
        var result: [String] = []
        if executable(named: "markitdown") != nil { result.append("MarkItDown") }
        if executable(named: "pandoc") != nil { result.append("Pandoc") }
        return result
    }

    // MARK: - Detection

    private static func executable(named name: String) -> String? {
        var directories: [String] = []
        if let path = ProcessInfo.processInfo.environment["PATH"] {
            directories.append(contentsOf: path.components(separatedBy: ":"))
        }
        directories.append(contentsOf: ["/opt/homebrew/bin", "/usr/local/bin", "/usr/bin", "/bin"])
        for directory in directories where !directory.isEmpty {
            let candidate = URL(fileURLWithPath: directory).appendingPathComponent(name).path
            if FileManager.default.isExecutableFile(atPath: candidate) { return candidate }
        }
        return nil
    }

    /// Pandoc's input format for a file extension, or nil when it cannot read it.
    private static func pandocFormat(_ fileExtension: String) -> String? {
        switch fileExtension {
        case "docx": return "docx"
        case "odt": return "odt"
        case "rtf": return "rtf"
        case "html", "htm": return "html"
        case "txt": return "markdown"
        case "md", "markdown": return "markdown"
        case "csv": return "csv"
        default: return nil
        }
    }

    // MARK: - Running

    private static func run(_ executable: String, arguments: [String], timeout: TimeInterval) -> String? {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = arguments
        process.standardInput = FileHandle.nullDevice
        process.standardError = FileHandle.nullDevice
        let pipe = Pipe()
        process.standardOutput = pipe

        do { try process.run() } catch { return nil }

        // The output is read on another queue: a converter can write far more
        // than the pipe buffer holds, and reading only after the process exits
        // would deadlock.
        let box = DataBox()
        let finished = DispatchSemaphore(value: 0)
        DispatchQueue.global(qos: .userInitiated).async {
            box.data = pipe.fileHandleForReading.readDataToEndOfFile()
            finished.signal()
        }
        guard finished.wait(timeout: .now() + timeout) == .success else {
            process.terminate()
            return nil
        }
        process.waitUntilExit()
        guard process.terminationStatus == 0 else { return nil }
        guard box.data.count <= 40 * 1024 * 1024 else { return nil }
        let text = String(decoding: box.data, as: UTF8.self)
        return text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? nil : text
    }

    /// Splits Markdown into one chunk per heading.
    private static func sections(of markdown: String) -> [String] {
        var document = MarkdownDocument()
        for line in markdown.components(separatedBy: "\n") {
            if let heading = MarkdownDocument.markdownHeading(line) {
                document.heading(heading.title, level: heading.level)
            } else {
                document.paragraph(line)
            }
        }
        return document.sections
    }
}

/// A tiny box so the reader thread can hand the output back safely.
private final class DataBox: @unchecked Sendable {
    private let lock = NSLock()
    private var storage = Data()

    var data: Data {
        get { lock.lock(); defer { lock.unlock() }; return storage }
        set { lock.lock(); storage = newValue; lock.unlock() }
    }
}
