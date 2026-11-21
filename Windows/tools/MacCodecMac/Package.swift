// swift-tools-version: 6.0
// A15: macOS-only manifest. Windows keeps Windows/tools/MacCodec/Package.swift.
// Build: swift run --package-path Windows/tools/MacCodecMac MacCodec emit --output Windows/tests/Fixtures/mac
// NextThoughtCore comes from the repository root package, so the fixtures are
// encoded by exactly the code the app ships.
import PackageDescription

let package = Package(
    name: "MacCodecMac",
    platforms: [.macOS(.v14)],
    dependencies: [
        .package(name: "NextThought", path: "../../..")
    ],
    targets: [
        .executableTarget(
            name: "MacCodec",
            dependencies: [.product(name: "NextThoughtCore", package: "NextThought")],
            path: "Sources/MacCodec"
        )
    ]
)
