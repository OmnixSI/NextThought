# MacCodecMac（A15，macOS 专用）

Windows 的构建清单仍在 `Windows/tools/MacCodec/Package.swift`；本目录只给 macOS 用，按相对路径直接引用仓库 `Sources/NextThoughtCore`，使用系统 CryptoKit / SQLite，不需要 Windows 的 junction 同步脚本。

```bash
swift run --package-path Windows/tools/MacCodecMac emit --output Windows/tests/Fixtures/mac
```

`emit` 只生成 A15 列出的样本文件；`check` 只检查它们是否存在。生成后把 `Windows/tests/Fixtures/mac/` 提交到 `windows/parity`。
