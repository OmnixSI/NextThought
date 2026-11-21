# 未决问题（OPEN_QUESTIONS）

记录清单未写明、但 Windows 没有对应能力、需要人确认的做法。这里只记录事实与决定，不自行发明替代方案。

| 编号 | 问题 | 决定 / 状态 |
|---|---|---|
| H2 | Swift 方法名与 C# 方法名映射 | 已决定：允许重命名，不建映射层；C# 方法名 = Swift 方法名首字母大写，Windows 独有方法保持原名；排在最后做 |
| A15 | MacCodec 夹具 | 已决定：Mac 上 emit 并提交产物；Windows 只做 check；不安装 Clang、不写加密/平台替身 |
| E9 | 持续读屏 | 已决定：批准；单帧、默认关闭、界面可见正在截屏 |
| E10 | 看镜头 | 等待模型方案答复 |
| E12 | 我的声音 | 已决定：复用现有 SpeakerEmbedder，不导出 MeetingVoiceTagger |
| F13 | 蓝牙备用通道 | 本版不做 |
| H5 | 旧 WPF 工程与命令行工程是否删除 | 等待答复，保持不动 |
| 视觉 | Mac 截图与逐元素对照 | **已到位（2026-11-21）**：93 张 Mac 3.4.0 浅/深参考图在 `Windows/validation/mac-screens/`，已生成对照索引并更新各 `compare.md`；剩余视觉差异记录在对应 compare 文档，「已验证」仍需人逐页确认 |
| 硬件 | 需要其他设备的实机测试 | 步骤写在 `Windows/FINAL_REPORT.md` 与各任务证据中，等待安排 |
| CI | Mac `Verify` workflow 的 `archive-ux` 自检失败 | 2026-11-21 起 `Verify`（self-hosted Mac）在 self-test `archive-ux` 报 `NSCocoaErrorDomain Code=4 "The file doesn't exist."`；发生在 Mac 应用自检路径，不在 Windows 代码内，`Sources/` 只读，需要维护者在该 Mac runner 上单独排查 |
| A16 | Mac 端回读 Windows 导出的备份 | Windows 侧已用 Mac 备份夹具完成双向核对（`eda5575e`）；Mac 打开 Windows 导出的 `.ntbackup` 最后一步仍需人在 Mac 上执行 |

## H2 方法重命名

- 决定：允许重命名，不建映射层。
- 规则：C# 方法名等于 Swift 方法名把首字母改成大写，例如 `beginNewMeeting` → `BeginNewMeeting`、`setDraftCarry` → `SetDraftCarry`。只改 Swift 里有对应物的方法；Windows 独有方法保持原名。
- 统计口径：按首字母不区分大小写重新统计 179 个 Swift 方法名；现有「完全匹配 0」是大小写不同造成的误判。
- 执行方式：一个文件一个提交，用 IDE / Roslyn 重命名，不手工替换字符串；每个提交后全部测试通过。排在第三部分第 8 步。
- 状态：已执行完成（2026-11-21）：Roslyn 语义扫描全树 0 个需改名的方法，`AppState.Notes.cs` 已建立并对齐 AppState+Notes.swift，详见 `Windows/H2_RENAME_REPORT.md`。

## A15 MacCodec

- 决定：由人在 Mac 上运行 `emit` 并提交产物；Windows 只做 `check`。
- 不安装或升级 Clang，不写 HKDF/AES-GCM 替身，不写 `DocumentJob` 替身。
- Windows 负责：
  1. `Windows/tools/MacCodec` 改成 macOS 可直接构建：清单按相对路径引用 `Sources/NextThoughtCore`，macOS 使用系统 CryptoKit/SQLite，不依赖 junction 同步脚本；Windows 构建方式保留，用条件编译或各自清单分开。不修改 `Sources/` 和仓库根 `Package.swift`。
  2. `emit` 覆盖 A15 列出的每种文件：会议 meta、转写、译文、提问、纪要、`user-content.json`、系列（含一条被替换过的系列条目）、技能、模板、术语表、备份清单，以及 A11 三个修订号样本和期望值。
  3. `Windows/MAC_FIXTURE_REQUEST.md` 增加 Mac 执行命令、产物输出到 `Windows/tests/Fixtures/mac/`、提交方式。
  4. Windows `check`：不依赖 7 个加密文件和 4 个平台文件的类型都要解码校验；跳过项逐条列出原因，不静默跳过。
- 夹具未提交前 A15 状态为「已实现、未验证」；依赖夹具的测试先写好并标记等待夹具，不用假数据。A11/A16 同样处理。
- 状态：已完成（2026-11-21）：人在 Mac 上 emit 并提交 `Windows/tests/Fixtures/mac/`，Windows `MacCodecCheck` 通过（纯数据家族）；CryptoKit/SQLite/Darwin/CoreGraphics/Sherpa 五类按 README 原因跳过。

## E9 持续读屏

- 决定：批准，但三个条件必须同时满足：默认关闭、只截单帧、界面上让用户看见正在截屏。
- Platform 新增只读接口：截一帧交给 Windows.Media.Ocr 返回文字；文字只在内存，不写资料库、不落盘、不进备份；沿用现有截图方式，不弹出选择器或边框。
- 行为照 Mac：`insightScreenWatch` 默认关闭；`insightScreenInterval` 默认 1 秒、最小 1 秒；失败后至少 20 秒再试；只在录音、未暂停、未停止时读取；演示模式不读；来源可选窗口/显示器/区域，自己的窗口不出现在来源且不被读到；`insightFollowSlides` 默认开，仅读屏打开时可用；设置说明用 Mac 原句，只替换系统名词；文字接入屏幕分类、幻灯片跟随、节流。
- 状态：已决定并关闭，开始实现。

## E10 看镜头

- 决定：批准使用摄像头，但放到最后一批；不接受「手动校准占位」。
- 模型方案（待答复）：
  - 首选：MediaPipe Face Landmarker（`face_landmarker.task`），Apache-2.0，约 3.7 MB，输出 478 个 3D 关键点；其中 468–477 为虹膜/瞳孔点，33–133 为眼睛轮廓，满足 `GazeEstimator` 需要的眼眶与瞳孔输入。来源：Google MediaPipe 官方模型卡；随包分发时附 Apache-2.0 NOTICE。运行时现有 `Microsoft.ML.OnnxRuntime`，需要把 `.task` 的 TFLite 图转换成 ONNX（tf2onnx），转换脚本和期望 SHA-256 放进 `Windows/tools/`，模型文件按现有模型清单校验后分发。
  - 备选：MediaPipe Iris（`iris_landmark.tflite`）+ BlazeFace 人脸检测，Apache-2.0，合计约 2 MB，虹膜模型输出 71 个关键点（含瞳孔/虹膜和眼睛轮廓）。同样需要 TFLite→ONNX。
  - 不选：InsightFace 2D106（无虹膜点）、OpenCV Haar（只有人脸框）、dlib 68（无瞳孔）。
- 需要确认：是否接受 MediaPipe 模型随包分发、是否允许引入 TFLite→ONNX 的转换产物；确认后按 Mac `LiveCoach.swift` 的 `requestCameraAndWatch`、`calibrate`、`GazeCamera`、`SettingsPresenter.swift`、Core `GazeEstimator.swift` 实现，Windows.Media.Capture 640×480，帧只在内存，设置默认关闭并带灵敏度。
- 状态：等待模型方案答复。

## E12 我的声音

- 决定：不需要新模型，直接用现有 `Windows/src/NextThought.Platform/SpeakerEmbedder.cs`（WeSpeaker ResNet34-LM，与 Mac 同一模型）；不导出 `MeetingVoiceTagger`。
- 流程照 Mac `MyVoice.swift`、`MyVoiceViews.swift`、`SpeakerViews.swift` 的 `MyVoiceSettings`、Core `VoiceProfile.swift`：录三段、取平均、存本机；界面/提示/失败提示照 Mac。
- 声纹只在本机使用：不导入 Mac 声纹，也不把 Windows 声纹当成 Mac 声纹写出；保存用 Windows 自己的模型标识；两边模型文件格式不同，向量是否一致未验证，不假设互通。
- 隐私说明用 Mac 原句：全部在本机运行，声纹只是一组数字。
- 状态：已决定并关闭，开始实现。

## F13 蓝牙备用通道

- 决定：本版不做。不修改 `Mobile/`，不实现 Windows 蓝牙外设端。
- 事实更正：手机端代码在本仓库 `Mobile/` 目录下；手机端已经按服务标识扫描，只是在发现设备后多了一道名字必须是 `IH-` 加配对码前 8 位的检查（`Mobile/Shared/Sources/CompanionMobileCore/BluetoothCentral.swift` 第 53 行，`Mobile/Android/.../BluetoothTransport.kt` 第 31 行）。
- 另记：Mac 广播名是 `NT-` 开头（`Sources/NextThought/CompanionBluetooth.swift` 第 71 行），同样过不了这道检查；这是 Mac 与手机端之间的问题，留给以后单独处理，这里只记录，不改。
- 设置页蓝牙说明保持「暂不可用」，文字与事实一致。
- 状态：本版不做。

## 视觉与硬件验证

- 不做逐像素对比，做逐元素并排对照：每个页面把 Mac 截图里的元素、顺序、文案、分组、间距资源、颜色资源、圆角/字号级别列成清单，逐条在 Windows 截图核对；结论写 `Windows/validation/screens/<视图名>/compare.md`。
- Mac 截图由人补拍浅/深两套到 `Windows/validation/mac-screens/`；到位前用 `docs/screenshots/v3/` 和 Swift 视图源码做标准。
- 2026-11-21：浅/深两套已放进 `Windows/validation/mac-screens/`（93 张，目录说明见其中 README.md），来自 3.4.0 演示模式。
- 本机可做的实机检查自己做并留记录（日期、版本、步骤、结果、证据），可写「已验证」；需要其他设备的只写步骤。

## 2026-11-21 收尾：仍未解决（每条一句话）

1. Mac 侧 `Verify` workflow 的 `archive-ux` self-test 仍失败（NSCocoaErrorDomain Code=4），需 Mac 维护者修复；Windows 侧无法复现、不阻塞 Windows 验证。
2. 在真实 Mac 上导入 `Windows/tests/Fixtures/win-export/all-scenarios-from-windows.ntbackup` 并逐场打开核对——需 Mac 设备（人）。
3. A15 五类非纯数据家族（CryptoKit HKDF/AES-GCM、SQLite DocumentJob、Darwin ProcessLineReader、CoreGraphics ScrollFollow、Sherpa Zipformer）Windows 无对应实现，按夹具 README 跳过，需 Mac 侧确认可接受。
4. E10 看镜头：真机摄像头能打开、录图三态通过，但尚未用真人在真实摄像头前复测（需一个人站在摄像头前）。
5. 截图提问只把 OCR 文本作为屏幕上下文，未把图像作为模型输入——是否本版必须需产品决定（Mac 参考实现会附图像）。
6. 导出对话框的 PDF 分页预览未实现（Windows 无 PDF 渲染控件，预览用同一投影源文本，导出文件本身一致）——需产品决定。
7. 所有记录右栏用归档概览卡、未内嵌 MeetingPage（导航架构差异）——需产品/架构决定是否重构。
8. 显示器友好名称、AddToGlossarySheet 剪贴板自动预填等小差异受 Windows/Avalonia 12 API 限制——需平台决定。
9. 38 份 `compare.md` 仍列有差异（动效近似、字体度量偏移、平台机制、Mac 专属控件等），逐条见 `Windows/validation/screens/*/compare.md`；其中多数属平台差异，需逐项确认「本版不做」还是继续修。
10. 更新通道选择按 §2.8 本版不做（只有正式通道，见 `SETTINGS_PARITY.md` 12 个内部键）；K10 应用内更新真机验证留给人。
11. 连续实采 2 小时、真实音频设备切换、睡眠/唤醒、Windows 沙盒首次引导、伴侣手机真机配对（K7/K12/K11/K8）需其他设备/环境。
12. Mac 截图由人补拍最终确认（现有 93 张 3.4.0 参考图已用于对照）。
