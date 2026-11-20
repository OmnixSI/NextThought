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

## E9 持续 ScreenWatch 自动跟页
- 卡在哪：Windows 只有一次性窗口/区域截图 + 资料库 OCR（`MaterialStore.ImportAsync`）路径；没有可复用的“截屏→系统 OCR 文本”公共接口，持续观察会污染资料库。
- Mac 做法：`startScreenWatch` 定时截屏、走屏幕分类/`SlideFollower` 并节流。
- 可选做法：在 Platform 暴露 `ScreenCapture + Windows.Media.Ocr` 的单帧只读 API，再由 AppState 定时调用；需要确认截屏频率与隐私提示。
- 当前实现：讲话标题跟页、截图 OCR 后跟页、讲稿要点缓存已完成；持续观察未做。

## E10 摄像头看镜头提醒
- 卡在哪：`GazeEstimator` 只有纯几何输入，没有摄像头帧来源；Windows MediaCapture 的预览帧与 Mac AVCapture 生命周期不同，需要单独实现摄像头请求/预览/帧回调。
- Mac 做法：`requestCameraAndWatch` + `calibrate`，把眼睛 landmarks 喂给 GazeEstimator。
- 可选做法：用 Windows.Media.Capture.Frames 取灰度帧并接 OpenCV/Mediapipe；或先用系统摄像头预览 + 手动校准占位。
- 当前实现：会议结束的教练小结（语速/占比/独白）已写 `coach-summary.json` 并展示；看镜头未做。

## E12 我的声音录入
- 卡在哪：`VoiceProfile` 需要 3 段录音的说话人 embedding；仓库里没有可用的 speaker embedding 模型/导出接口，`MeetingVoiceTagger` 的 embedding 不对外。
- Mac 做法：`MyVoice.swift` 录 3 段、`VoiceEnrollment.Make` 取平均、存本机。
- 可选做法：暴露 `MeetingVoiceTagger` 的 embedding 或集成 wespeaker onnx；需要确认模型分发与隐私说明。
- 当前实现：说话人命名/「这是我」/持久化/合并已完成；录音入库未做。
