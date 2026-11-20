# MacCodec（A15，未完成）

SwiftPM 可执行程序，目标是 `emit` Mac Core 样本、`check` Windows 数据/备份。

## 当前状态

- Package 已建；`Sources/CryptoKit` 是纯 Swift SHA-256 shim，替代 Windows 没有的 CryptoKit。
- 用 `sync_sources.ps1` 把只读的 `Sources/NextThoughtCore` 以 junction 放进 target。
- 构建（`.build` 不能放在中文路径下，SwiftPM 建符号链接会失败）：

```powershell
powershell -File Windows/tools/MacCodec/sync_sources.ps1
swift build --package-path Windows/tools/MacCodec --scratch-path C:\swift-maccodec
```

## 阻塞

- 官方 swift-crypto 的 BoringSSL C++ 要求 Clang 22；本机 VS 2026 是 Clang 21，编译失败。
- `CompanionCrypto.swift`（HKDF/AES-GCM）无 Windows 实现；`DocumentJobs`（SQLite3）、`ProcessLineReader`（Darwin）、`ScrollFollowInputClassifier`（CoreGraphics）、`ZipformerStreaming`（SherpaOnnx）也需排除，`WorkspaceDeletionTransaction` 又引用被排除的 `DocumentJob`。
- 需要：安装 Clang 22 / 更新 VS，或改在 Mac 上生成并提交 `emit` 夹具。详见 `Windows/OPEN_QUESTIONS.md`。
