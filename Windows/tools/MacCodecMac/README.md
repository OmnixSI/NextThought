# MacCodecMac（A15，macOS 专用）

Windows 的构建清单仍在 `Windows/tools/MacCodec/Package.swift`；本目录只给 macOS 用，把仓库根目录当作本地包依赖，直接用应用发布的 `NextThoughtCore` 库（含 sherpa-onnx 依赖），不需要 Windows 的 junction 同步脚本。

```bash
swift run --package-path Windows/tools/MacCodecMac MacCodec emit --output Windows/tests/Fixtures/mac
```

`emit` 用 Core 的真实类型和 `JSONLines.encoder()` 生成 A15 列出的样本文件，读回比对后再写盘，结束时自动跑 `check`；`check` 用 Swift 类型逐个解码。夹具说明见 `Windows/tests/Fixtures/mac/README.md`。生成后把 `Windows/tests/Fixtures/mac/` 提交到 `windows/parity`。
