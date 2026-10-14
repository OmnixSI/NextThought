import Foundation
import CryptoKit

public struct WhisperModel: Identifiable, Equatable, Sendable {
    public let id: String
    public let bytes: Int64
    public let sha256: String
    public var filename: String { "ggml-\(id).bin" }
    public var downloadURL: URL { URL(string: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/\(filename)")! }
    public var sizeLabel: String { ByteCountFormatter.string(fromByteCount: bytes, countStyle: .binary) }
    public var englishOnly: Bool { id.contains(".en") }
    public var summary: String {
        let family: String
        if id.hasPrefix("tiny") { family = "体积最小、速度优先，复杂语音更易出错" }
        else if id.hasPrefix("base") { family = "轻量快速，适合清晰语音和低资源设备" }
        else if id.hasPrefix("small") { family = "兼顾速度与识别质量，比 base 更占资源" }
        else if id.hasPrefix("medium") { family = "侧重识别质量，加载和转写较慢" }
        else if id.contains("turbo") { family = "大模型加速版，适合会议；实际速度取决于设备" }
        else { family = "完整大模型，资源占用高、转写较慢；v1/v2 是较早版本" }
        let precision = id.contains("-q5_") ? "；Q5 更省空间与内存，精度可能略降" : id.contains("-q8_") ? "；Q8 较接近原精度，体积介于 Q5 与原版之间" : "；原精度，下载与内存占用更大"
        return (englishOnly ? "仅英语，不适合中文。" : "支持中文、英文等多语言。") + family + precision
    }
    public static let defaultID = "large-v3-turbo-q5_0"
    // Sizes and SHA-256 values from the upstream Hugging Face LFS manifest (2026-10-14).
    public static let catalog: [WhisperModel] = [
        .init(id: "base-q5_1", bytes: 59707625, sha256: "422f1ae452ade6f30a004d7e5c6a43195e4433bc370bf23fac9cc591f01a8898"),
        .init(id: "base-q8_0", bytes: 81768585, sha256: "c577b9a86e7e048a0b7eada054f4dd79a56bbfa911fbdacf900ac5b567cbb7d9"),
        .init(id: "base", bytes: 147951465, sha256: "60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe"),
        .init(id: "base.en-q5_1", bytes: 59721011, sha256: "4baf70dd0d7c4247ba2b81fafd9c01005ac77c2f9ef064e00dcf195d0e2fdd2f"),
        .init(id: "base.en-q8_0", bytes: 81781811, sha256: "a4d4a0768075e13cfd7e19df3ae2dbc4a68d37d36a7dad45e8410c9a34f8c87e"),
        .init(id: "base.en", bytes: 147964211, sha256: "a03779c86df3323075f5e796cb2ce5029f00ec8869eee3fdfb897afe36c6d002"),
        .init(id: "large-v1", bytes: 3094623691, sha256: "7d99f41a10525d0206bddadd86760181fa920438b6b33237e3118ff6c83bb53d"),
        .init(id: "large-v2-q5_0", bytes: 1080732091, sha256: "3a214837221e4530dbc1fe8d734f302af393eb30bd0ed046042ebf4baf70f6f2"),
        .init(id: "large-v2-q8_0", bytes: 1656129691, sha256: "fef54e6d898246a65c8285bfa83bd1807e27fadf54d5d4e81754c47634737e8c"),
        .init(id: "large-v2", bytes: 3094623691, sha256: "9a423fe4d40c82774b6af34115b8b935f34152246eb19e80e376071d3f999487"),
        .init(id: "large-v3-q5_0", bytes: 1081140203, sha256: "d75795ecff3f83b5faa89d1900604ad8c780abd5739fae406de19f23ecd98ad1"),
        .init(id: "large-v3-turbo-q5_0", bytes: 574041195, sha256: "394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2"),
        .init(id: "large-v3-turbo-q8_0", bytes: 874188075, sha256: "317eb69c11673c9de1e1f0d459b253999804ec71ac4c23c17ecf5fbe24e259a1"),
        .init(id: "large-v3-turbo", bytes: 1624555275, sha256: "1fc70f774d38eb169993ac391eea357ef47c88757ef72ee5943879b7e8e2bc69"),
        .init(id: "large-v3", bytes: 3095033483, sha256: "64d182b440b98d5203c4f9bd541544d84c605196c4f7b845dfa11fb23594d1e2"),
        .init(id: "medium-q5_0", bytes: 539212467, sha256: "19fea4b380c3a618ec4723c3eef2eb785ffba0d0538cf43f8f235e7b3b34220f"),
        .init(id: "medium-q8_0", bytes: 823369779, sha256: "42a1ffcbe4167d224232443396968db4d02d4e8e87e213d3ee2e03095dea6502"),
        .init(id: "medium", bytes: 1533763059, sha256: "6c14d5adee5f86394037b4e4e8b59f1673b6cee10e3cf0b11bbdbee79c156208"),
        .init(id: "medium.en-q5_0", bytes: 539225533, sha256: "76733e26ad8fe1c7a5bf7531a9d41917b2adc0f20f2e4f5531688a8c6cd88eb0"),
        .init(id: "medium.en-q8_0", bytes: 823382461, sha256: "43fa2cd084de5a04399a896a9a7a786064e221365c01700cea4666005218f11c"),
        .init(id: "medium.en", bytes: 1533774781, sha256: "cc37e93478338ec7700281a7ac30a10128929eb8f427dda2e865faa8f6da4356"),
        .init(id: "small-q5_1", bytes: 190085487, sha256: "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb"),
        .init(id: "small-q8_0", bytes: 264464607, sha256: "49c8fb02b65e6049d5fa6c04f81f53b867b5ec9540406812c643f177317f779f"),
        .init(id: "small", bytes: 487601967, sha256: "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b"),
        .init(id: "small.en-q5_1", bytes: 190098681, sha256: "bfdff4894dcb76bbf647d56263ea2a96645423f1669176f4844a1bf8e478ad30"),
        .init(id: "small.en-q8_0", bytes: 264477561, sha256: "67a179f608ea6114bd3fdb9060e762b588a3fb3bd00c4387971be4d177958067"),
        .init(id: "small.en", bytes: 487614201, sha256: "c6138d6d58ecc8322097e0f987c32f1be8bb0a18532a3f88f734d1bbf9c41e5d"),
        .init(id: "tiny-q5_1", bytes: 32152673, sha256: "818710568da3ca15689e31a743197b520007872ff9576237bda97bd1b469c3d7"),
        .init(id: "tiny-q8_0", bytes: 43537433, sha256: "c2085835d3f50733e2ff6e4b41ae8a2b8d8110461e18821b09a15c40c42d1cca"),
        .init(id: "tiny", bytes: 77691713, sha256: "be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21"),
        .init(id: "tiny.en-q5_1", bytes: 32166155, sha256: "c77c5766f1cef09b6b7d47f21b546cbddd4157886b3b5d6d4f709e91e66c7c2b"),
        .init(id: "tiny.en-q8_0", bytes: 43550795, sha256: "5bc2b3860aa151a4c6e7bb095e1fcce7cf12c7b020ca08dcec0c6d018bb7dd94"),
        .init(id: "tiny.en", bytes: 77704715, sha256: "921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f"),
    ]
    public static func named(_ id: String) -> WhisperModel? { catalog.first { $0.id == id } }
    public static func matching(_ url: URL) -> WhisperModel? { catalog.first { $0.filename == url.lastPathComponent } }
}

public enum WhisperModelFile {
    public static func sha256(at url: URL) throws -> String {
        let file = try FileHandle(forReadingFrom: url)
        defer { try? file.close() }
        var hash = SHA256()
        while let data = try file.read(upToCount: 8 * 1024 * 1024), !data.isEmpty { hash.update(data: data) }
        return hash.finalize().map { String(format: "%02x", $0) }.joined()
    }
    public static func verify(at url: URL, expected: WhisperModel? = nil) throws {
        let file = try FileHandle(forReadingFrom: url)
        defer { try? file.close() }
        let size = try file.seekToEnd()
        try file.seek(toOffset: 0)
        guard size > 1_000_000, try file.read(upToCount: 4) == Data([0x6c, 0x6d, 0x67, 0x67]) else {
            throw ValidationError.invalidFormat
        }
        if let expected {
            guard size == UInt64(expected.bytes), try sha256(at: url) == expected.sha256 else { throw ValidationError.checksum }
        } else {
            let checksumURL = url.appendingPathExtension("sha256")
            guard let checksum = try? String(contentsOf: checksumURL, encoding: .utf8).trimmingCharacters(in: .whitespacesAndNewlines),
                  checksum.count == 64, try sha256(at: url) == checksum else { throw ValidationError.checksum }
        }
    }
    public enum ValidationError: LocalizedError {
        case invalidFormat, checksum
        public var errorDescription: String? {
            switch self {
            case .invalidFormat: return "模型不是有效的 Whisper GGML 文件，或文件不完整。请选择 .bin，或导入完整的 .pt 模型。"
            case .checksum: return "模型完整性校验失败，请重新下载或重新导入原始文件。"
            }
        }
    }
}
