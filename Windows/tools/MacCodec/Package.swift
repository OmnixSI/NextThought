// swift-tools-version: 6.4
import PackageDescription

let package = Package(
    name: "MacCodec",
    targets: [
        .target(name: "CryptoKit", path: "Sources/CryptoKit"),
        .executableTarget(name: "MacCodec", dependencies: ["NextThoughtCore"], path: "Sources/MacCodec"),
        .target(name: "NextThoughtCore", dependencies: ["CryptoKit"], path: "Sources/NextThoughtCore", exclude: [
            "CompanionCrypto.swift", "DocumentJobs.swift", "ProcessLineReader.swift",
            "ScrollFollowInputClassifier.swift", "ZipformerStreaming.swift"
        ])
    ]
)
