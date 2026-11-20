# 未决问题（OPEN_QUESTIONS）

记录清单未写、但 Windows 没有对应能力、需要人确认的做法；不自行发明替代方案。

| 编号 | 任务 | 问题 | Mac 做法 | 可选做法 | 状态 |
|---|---|---|---|---|---|
| — | — | — | — | — | 无 |
## A15 MacCodec 在 Windows 上编译

- 卡在哪：80 个 Core 文件里，4 个平台文件（SQLite3/Darwin/CoreGraphics/Sherpa）不能在 Windows 编译；7 个 CryptoKit 文件里的 HKDF/AES-GCM 无 Windows 实现；官方 swift-crypto 的 BoringSSL 需要 Clang 22，本机是 Clang 21。
- Mac 的做法：在 Mac 上用系统 CryptoKit/SQLite 构建同一个工具，`emit` 夹具后把产物提交到 `Windows/tests/Fixtures/mac/`。
- 可选做法：
  1. 安装 Clang 22 或更新 VS，再改用官方 swift-crypto；
  2. 在 Mac 上跑 `MacCodec emit`，把生成物提交，Windows 只负责 `check`；
  3. Windows 只编译不依赖平台文件的数据模型子集，另写纯 Swift 的 HKDF/AES-GCM 与 DocumentJob 替身。
- 当前仓库状态：`Windows/tools/MacCodec` 已有 Package、CryptoKit SHA-256 shim、junction 同步脚本；未产出 `emit` 夹具，A15 仍是「未做」。
