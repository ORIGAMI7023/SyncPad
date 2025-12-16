// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "SyncPad",
    platforms: [
        .iOS(.v15),
        .macOS(.v12)
    ],
    products: [
        .library(
            name: "SyncPadShared",
            targets: ["SyncPadShared"]
        )
    ],
    targets: [
        .target(
            name: "SyncPadShared",
            path: "Shared"
        )
    ]
)
