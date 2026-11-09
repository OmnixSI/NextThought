import Foundation
import NextThoughtCore

enum SessionFileExporter {
    static func write(content: String, format: SessionExportFormat, to url: URL) throws {
        let data: Data
        switch format {
        case .markdown, .srt, .vtt:
            data = Data(content.utf8)
        case .docx:
            try SessionDocumentRenderer.writeDocx(content, to: url)
            return
        case .pdf:
            data = try SessionDocumentRenderer.pdfData(for: content)
        }
        try data.write(to: url, options: .atomic)
    }
}
