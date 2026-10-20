import AppKit
import Foundation
import InterviewHackerCore

/// Reads user-selected local files and turns them into archived Markdown.
///
/// Only files the user picked are read. No macros, scripts, links or remote
/// resources are executed or fetched. A folder can be dropped in at once: every
/// supported file is converted on its own, a file that cannot be read only
/// fails that file, and each one is filed into the right material slot
/// automatically.
enum DocumentImporter {
    /// One converted material.
    struct Imported {
        var id = UUID()
        /// File name as shown in the material list, with its extension.
        var fileName: String
        /// File name without the extension, used for the archived file.
        var name: String
        var markdown: String
        /// One chunk per heading, used for retrieval and for citing a section.
        var sections: [String]
        var format: String
        /// Which converter produced the Markdown.
        var converter: String
        /// The material slot this file most likely belongs to.
        var suggestedCategory: String?
        var characterCount: Int { markdown.count }
    }

    /// The result of a bulk import: what worked and what did not.
    struct Outcome {
        var documents: [Imported] = []
        /// Files that could not be read, with the reason.
        var failures: [(name: String, message: String)] = []
    }

    static let supportedExtensions = MarkdownConverter.supportedExtensions

    /// Expands a dropped file or folder into the materials inside it.
    ///
    /// A dropped folder is walked, so a whole set of materials can be dropped in
    /// at once instead of being picked one by one.
    static func expand(_ url: URL, depth: Int = 3) -> [URL] {
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: url.path, isDirectory: &isDirectory) else { return [] }
        guard isDirectory.boolValue else {
            return supportedExtensions.contains(url.pathExtension.lowercased()) ? [url] : []
        }
        guard depth > 0 else { return [] }
        let contents = (try? FileManager.default.contentsOfDirectory(at: url, includingPropertiesForKeys: [.isDirectoryKey], options: [.skipsHiddenFiles])) ?? []
        var result: [URL] = []
        for child in contents.sorted(by: { $0.lastPathComponent < $1.lastPathComponent }) {
            result.append(contentsOf: expand(child, depth: depth - 1))
        }
        return result
    }

    /// Converts the given files. Runs off the main thread.
    ///
    /// - Parameter categories: The material slots of the current mode, used to
    ///   file every file automatically.
    /// - Parameter useDocumentModel: Read scanned pages and images with the
    ///   document model instead of the system OCR.
    static func read(_ urls: [URL], categories: [String], useDocumentModel: Bool = false) async throws
        -> Outcome
    {
        try await Task.detached(priority: .userInitiated) { () -> Outcome in
            var outcome = Outcome()
            for url in urls.prefix(60) {
                try Task.checkCancellation()
                let accessed = url.startAccessingSecurityScopedResource()
                defer { if accessed { url.stopAccessingSecurityScopedResource() } }

                do {
                    let attributes = try url.resourceValues(forKeys: [.fileSizeKey, .isRegularFileKey])
                    guard attributes.isRegularFile == true, let size = attributes.fileSize, size <= 20 * 1024 * 1024 else {
                        throw AppFailure.capture("仅支持不超过 20 MiB 的普通文件。")
                    }
                    let result = try await MarkdownConverter.convert(url, useDocumentModel: useDocumentModel)
                    guard result.sections.contains(where: { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }) else {
                        throw AppFailure.capture("没有可提取的文字。")
                    }
                    guard result.characterCount <= 200_000 else {
                        throw AppFailure.capture("文字超过 20 万字，请按章节拆分。")
                    }
                    outcome.documents.append(Imported(
                        fileName: url.lastPathComponent,
                        name: result.name,
                        markdown: result.markdown,
                        sections: result.sections,
                        format: result.format,
                        converter: result.converter,
                        suggestedCategory: DocumentClassifier.classify(name: url.lastPathComponent, content: result.markdown, categories: categories)))
                } catch is CancellationError {
                    throw CancellationError()
                } catch {
                    // One unreadable file must not stop the rest of the import.
                    outcome.failures.append((url.lastPathComponent, error.localizedDescription))
                }
            }
            return outcome
        }.value
    }
}
