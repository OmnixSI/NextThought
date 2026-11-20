// swift-tools-version: 6.4
// A15: macOS-only manifest. Windows keeps Windows/tools/MacCodec/Package.swift.
// Build: swift run --package-path Windows/tools/MacCodecMac emit --output Windows/tests/Fixtures/mac
import PackageDescription

let package = Package(
    name: "MacCodecMac",
    targets: [
        .target(
            name: "NextThoughtCore",
            path: "../../../Sources/NextThoughtCore",
            linkerSettings: [.linkedLibrary("sqlite3"), .linkedFramework("CoreGraphics")]
        ),
        .executableTarget(
            name: "MacCodec",
            dependencies: ["NextThoughtCore"],
            path: "Sources/MacCodec"
        )
    ]
)
