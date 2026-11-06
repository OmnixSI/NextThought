// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "NextThought",
    // Interface strings are written in Simplified Chinese and translated through
    // Resources/Localization/Localizable.xcstrings (compiled by scripts/build-app.sh).
    defaultLocalization: "zh-Hans",
    platforms: [.macOS(.v14)],
    products: [
        .executable(name: "NextThought", targets: ["NextThought"]),
        .library(name: "NextThoughtCore", targets: ["NextThoughtCore"])
    ],
    dependencies: [
        .package(url: "https://github.com/k2-fsa/sherpa-onnx.git", exact: "1.13.8")
    ],
    targets: [
        .target(
            name: "NextThoughtCore",
            dependencies: [.product(name: "sherpa-onnx", package: "sherpa-onnx")],
            path: "Sources/NextThoughtCore"),
        .executableTarget(
            name: "NextThought",
            dependencies: ["NextThoughtCore"],
            path: "Sources/NextThought",
            swiftSettings: [.swiftLanguageMode(.v5)]),
        .testTarget(name: "NextThoughtCoreTests", dependencies: ["NextThoughtCore"], path: "Tests/NextThoughtCoreTests", exclude: ["Fixtures"])
    ]
)
