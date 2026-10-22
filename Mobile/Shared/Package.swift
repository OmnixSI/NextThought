// swift-tools-version: 6.0
import PackageDescription
let package = Package(name: "CompanionMobileCore", platforms: [.iOS(.v16), .macOS(.v14)], products: [.library(name: "CompanionMobileCore", targets: ["CompanionMobileCore"])], targets: [
    .target(name: "CompanionMobileCore", swiftSettings: [.swiftLanguageMode(.v5)]),
    .testTarget(name: "CompanionMobileCoreTests", dependencies: ["CompanionMobileCore"])
])
