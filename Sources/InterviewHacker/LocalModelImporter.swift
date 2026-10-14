import Foundation
import InterviewHackerCore

struct LocalModelImporter {
    static func importModel(from source: URL) throws -> URL {
        let directory = LocalWhisperService.modelsDirectory
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let destination = directory.appendingPathComponent("imported-\(UUID().uuidString)-\(source.deletingPathExtension().lastPathComponent).bin")
        let temporary = FileManager.default.temporaryDirectory.appendingPathComponent("interviewhacker-import-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: temporary, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: temporary) }
        do {
            if source.pathExtension.lowercased() == "pt" {
                guard let resources = Bundle.main.resourceURL else { throw AppFailure.modelMissing }
                let process = Process()
                process.executableURL = try pythonExecutable()
                process.arguments = [resources.appendingPathComponent("import-whisper-model.py").path, source.path, temporary.path, resources.appendingPathComponent("convert-pt-to-ggml.py").path]
                let pipe = Pipe()
                process.standardOutput = pipe
                process.standardError = pipe
                try process.run()
                let output = pipe.fileHandleForReading.readDataToEndOfFile()
                process.waitUntilExit()
                guard process.terminationStatus == 0 else {
                    throw AppFailure.transcription(String(String(data: output, encoding: .utf8)?.suffix(2_000) ?? "无法转换模型。请确认已安装 Python Whisper。"))
                }
                try FileManager.default.moveItem(at: temporary.appendingPathComponent("ggml-model.bin"), to: destination)
            } else {
                try FileManager.default.copyItem(at: source, to: destination)
            }
            let checksum = try WhisperModelFile.sha256(at: destination)
            try checksum.write(to: destination.appendingPathExtension("sha256"), atomically: true, encoding: .utf8)
            try WhisperModelFile.verify(at: destination)
            return destination
        } catch {
            try? FileManager.default.removeItem(at: destination)
            try? FileManager.default.removeItem(at: destination.appendingPathExtension("sha256"))
            throw error
        }
    }

    private static func pythonExecutable() throws -> URL {
        // Homebrew's whisper entry point names the environment that already has torch/whisper.
        for path in ["/opt/homebrew/bin/whisper", "/usr/local/bin/whisper"] {
            if let line = try? String(contentsOfFile: path, encoding: .utf8).components(separatedBy: .newlines).first,
               line.hasPrefix("#!/"), !line.contains(" ") {
                let interpreter = String(line.dropFirst(2))
                if FileManager.default.isExecutableFile(atPath: interpreter) { return URL(fileURLWithPath: interpreter) }
            }
        }
        for path in ["/opt/homebrew/bin/python3", "/usr/local/bin/python3"] where FileManager.default.isExecutableFile(atPath: path) {
            return URL(fileURLWithPath: path)
        }
        throw AppFailure.transcription("导入 .pt 需要本机安装 Python 版 openai-whisper。也可直接导入 .bin 或选择下载模型。")
    }
}
