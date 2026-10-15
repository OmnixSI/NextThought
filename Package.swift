// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "InterviewHacker",
    platforms: [.macOS(.v14)],
    products: [
        .executable(name: "InterviewHacker", targets: ["InterviewHacker"]),
        .library(name: "InterviewHackerCore", targets: ["InterviewHackerCore"])
    ],
    dependencies: [
        .package(url: "https://github.com/k2-fsa/sherpa-onnx.git", exact: "1.13.8")
    ],
    targets: [
        .target(
            name: "InterviewHackerCore",
            dependencies: [.product(name: "sherpa-onnx", package: "sherpa-onnx")],
            path: "Sources/InterviewHackerCore"),
        .executableTarget(
            name: "InterviewHacker",
            dependencies: ["InterviewHackerCore"],
            path: "Sources/InterviewHacker",
            swiftSettings: [.swiftLanguageMode(.v5)]),
        .testTarget(name: "InterviewHackerCoreTests", dependencies: ["InterviewHackerCore"], path: "Tests/InterviewHackerCoreTests")
    ]
)
