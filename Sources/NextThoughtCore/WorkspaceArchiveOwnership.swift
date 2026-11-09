import Foundation

/// A series owns its extracted files. Originals remain shared by content hash.
public enum WorkspaceArchiveOwnership {
    public static func repair(_ source: Workspace, dataRoot: URL) throws -> Workspace {
        var workspace = source
        let materials = dataRoot.appendingPathComponent("Materials", isDirectory: true).standardizedFileURL
        let owner = materials.appendingPathComponent(workspace.id.uuidString, isDirectory: true)
        for index in workspace.documents.indices {
            let document = workspace.documents[index]
            let existing: URL? = document.markdownPath.flatMap { path in
                let url = (path.hasPrefix("/") ? URL(fileURLWithPath: path) : dataRoot.appendingPathComponent(path)).standardizedFileURL.resolvingSymlinksInPath()
                return url.path.hasPrefix(materials.resolvingSymlinksInPath().path + "/") ? url : nil
            }
            if let existing, existing.deletingLastPathComponent() == owner,
               FileManager.default.fileExists(atPath: existing.path) { continue }
            try FileManager.default.createDirectory(at: owner, withIntermediateDirectories: true)
            let target = owner.appendingPathComponent(document.id.uuidString + ".md")
            if !FileManager.default.fileExists(atPath: target.path) {
                let data: Data
                if let existing, FileManager.default.fileExists(atPath: existing.path) { data = try Data(contentsOf: existing) }
                else { data = Data((document.content?.markdown ?? document.pages.joined(separator: "\n\n")).utf8) }
                try data.write(to: target, options: .atomic)
            }
            let metadata = target.deletingPathExtension().appendingPathExtension("json")
            if !FileManager.default.fileExists(atPath: metadata.path) {
                if let existing, let data = try? Data(contentsOf: existing.deletingPathExtension().appendingPathExtension("json")) {
                    try data.write(to: metadata, options: .atomic)
                } else if let content = document.content { try JSONEncoder().encode(content).write(to: metadata, options: .atomic) }
            }
            workspace.documents[index].markdownPath = "Materials/\(workspace.id.uuidString)/\(target.lastPathComponent)"
        }
        return workspace
    }
}
