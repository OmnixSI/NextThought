# NextThought for Windows 0.9.0

首个可发布的 Windows 版本，对应 Mac 版 **3.4.0**（`f015e8c6`）。本版是**未签名**的按用户安装包，只支持
Windows 10 2004+ / Windows 11 的 x64。

## 中文

### 这一版有什么

- **会议记录**：系统音频/麦克风采音，X-ASR 中英流式识别＋整句精修，实时转写、说话人区分、声纹"我的声音"、
  会议中英互译（经回答服务，界面明示文本会发出本机）。
- **回答**：回答服务（多预设、API Key 保护、Codex 账号）、资料读取与语义检索、技能（全局/按系列）、
  联网搜索（需 Tavily Key，回答先出快答再换联网版）。
- **提词与教练**：提词岛、提词浮窗、字幕条、双屏演讲、屏幕共享排除本应用窗口、共享可见性测试、全局快捷键、
  临场教练（语速/发言占比，以及看镜头提醒：设置里开关默认关闭、灵敏度三档、2 秒校准；摄像头帧只在内存处理、
  不保存不上传；模型许可证、转换脚本与数值对齐见 `Windows/licenses/` 和 `Windows/validation/e10/`）。
- **纪要**：会议自动收尾、按类型自定义模板、导出 Markdown、系列与归档概览。
- **手机伴侣**：局域网/中转的配对链接、问题/回答/状态/编号与电脑一致、同一动作只执行一次。
- **数据**：本机数据目录、`.ntbackup` 导出/导入（可与 Mac 往返，密钥不入备份）、首次运行模型下载并 SHA-256 校验。
- **发布工程**：`Windows/release.ps1 -Version 0.9.0` 一条命令产出安装包、`SHA256SUMS.txt`、`update.json`；
  安装/卸载保留用户数据；应用内更新只接受 `windows-v*` 且同时带安装包和 `update.json` 的发布。
- 设置六页与 Mac 3.4.0 对齐（场景 / 回答与资料 / 提词与教练 / 纪要 / 通用 / 高级），中英文界面。

### 与 Mac 版的已知差别（第二批，`Windows/KNOWN_GAPS.md`）

1. 麦克风电平条的回声着色：Windows 目前只有文字层面的回声兜底，音频层回声监测在第二批接入。
2. 屏幕类型判断：Windows 用启发式，第二批接入本机模型判断。
3. 讲稿要点生成质量：Windows 的启发式生成只是近似，第二批按 Mac 模型路径替换（本版已在 presenter 里用回答模型生成，质量仍有差距）。
4. 助手独立窗口的拖动改列/拖动调权重：本版未实现，第二批补。
5. 截图提问对复杂画面的本机模型增强读取：本版只用 OCR/文本上下文，第二批接入。
6. 导出对话框的 PDF 分页预览：本版用同一投影源文本预览，第二批用 `Windows.Data.Pdf` 渲染页面图。
7. 回答服务直接读取原文件和图片：本版先转文本/OCR，第二批把原文件作为模型输入。

### 安装

下载 `NextThought-0.9.0-windows-x64-setup.exe` 运行。未签名，SmartScreen 提示时选"更多信息"→"仍要运行"。
按当前用户安装到 `%LOCALAPPDATA%\Programs\NextThought`；用户数据在 `%LOCALAPPDATA%\NextThought`，卸载保留。

### 已知限制（与 Mac 版的有意差异）

- 仅 x64；未签名；不支持 ARM64、代码签名、Native AOT、识别 GPU 加速、蓝牙备用通道。
- 没有系统语音识别与系统离线翻译；翻译经回答服务并提示文本外发。
- 没有 Mac 的屏幕录制权限引导（Windows 无对应权限）。
- `.doc`/`.odt`/`.rtf` 需先另存为新格式；旧版 PPT 转换需可选 LibreOffice 组件。
- 更新通道只有正式通道（`windows-v*`），通道选择本版不做。
- 安装包未签名，因此更新只做 SHA-256 校验（代码保留签名校验位置，待签名后启用）。

### 尚未在真实环境验证（发布前请照 RELEASE.md 第 6 节操作）

- K13 会议软件里对方是否看得到本应用窗口（需第二台设备）。
- K14 真实手机通过局域网和中转配对（需手机）。
- K15 多屏与不同缩放比例（需第二块屏幕）。
- K16 真实云端识别三家服务、真实联网搜索、真实回答服务账号（需账号）。
- K11 干净 Windows 沙盒安装与引导（本机无沙盒，留给人）；K12 音频设备切换已由 Agent 实测通过，拔出/睡眠唤醒留给人；K7 两小时长会已由 Agent 实测通过（120 采样、内存平稳、1,391 行转写）。
- 看镜头提醒：本机摄像头（BisonCam, NB Pro）可打开并采到帧，但摄像头前没有真人；「看镜头/移开」两态用录好的
  正面像走同一条 FaceLandmarker + GazeEstimator 处理路径验证（移开为虹膜点按眼宽外移模拟），尚未用真人在真实
  摄像头前实测。
- 三个语音识别归档（约 128–130 MB/个）与 613 MB 语义模型在开发机上仅校验了下载地址与固定大小，
  未整包下载；安装时应用会做完整 SHA-256 校验（详见 `Windows/validation/models.md`）。

### 校验

安装包 SHA-256 见发布页的 `SHA256SUMS.txt` 与 `update.json`。安装/升级/卸载/应用内更新的自动测试由
`Windows/release.ps1` 在发布前执行。

---

## English

The first publishable Windows release, matching Mac **3.4.0** (`f015e8c6`). This is an **unsigned**
per-user build for Windows 10 2004+/Windows 11 x64 only.

### What is in it

- **Meetings**: system-audio/microphone capture, X-ASR streaming zh/en recognition plus offline
  refinement, live transcript, speaker separation, "my voice" voiceprint, meeting zh/en translation
  through the answer service (the UI says the text leaves the machine).
- **Answers**: answer service presets and protected API keys, Codex accounts, documents with
  semantic retrieval, skills (global or per-series), web search (Tavily key; a fast answer is shown
  first and replaced by the web answer).
- **Prompter and coach**: cue island, teleprompter window, subtitle bar, two-screen presenting,
  screen-share exclusion of the app's own windows, share-visibility test, global shortcuts, live
  coach (pace and talk share, plus the gaze reminder: off by default, three sensitivity levels, a
  2-second calibration; camera frames stay in memory and are never saved or uploaded; model
  licences, conversion script and numeric alignment are in `Windows/licenses/` and
  `Windows/validation/e10/`).
- **Notes**: automatic meeting wrap-up, per-type custom templates, Markdown export, series and
  archive overview.
- **Phone companion**: LAN/relay pairing link, questions/answers/status/item numbers match the PC,
  each action runs once.
- **Data**: local data directory, `.ntbackup` export/import round-trips with the Mac (keys are never
  in the backup), first-run models downloaded with pinned SHA-256 checks.
- **Release engineering**: `Windows/release.ps1 -Version 0.9.0` produces the installer,
  `SHA256SUMS.txt` and `update.json`; install/uninstall keep user data; in-app updates accept only
  `windows-v*` releases that ship both the installer and `update.json`.
- The six settings pages mirror Mac 3.4.0 (场景 / 回答与资料 / 提词与教练 / 纪要 / 通用 / 高级), in
  Chinese and English.

### Known differences from the Mac version (second batch, `Windows/KNOWN_GAPS.md`)

1. Echo coloring on the microphone level meter is a second-batch item (Windows has the text-level echo fallback only).
2. Screen-type classification uses heuristics; the on-device model version is second batch.
3. Talking-point generation is approximate; the presenter now asks the answer model, but Mac-quality tuning is second batch.
4. Assistant drag-to-resize columns and drag-to-weight are not in this version.
5. Model-enhanced reading of complex screenshots is second batch (OCR/text context only here).
6. PDF paginated preview in the export sheet is second batch (`Windows.Data.Pdf`); the sheet previews the same projection text.
7. Answer services reading original files/images directly is second batch (text/OCR first).

### Install

Run `NextThought-0.9.0-windows-x64-setup.exe`. It is unsigned: on the SmartScreen prompt choose
"More info" → "Run anyway". It installs per user into `%LOCALAPPDATA%\Programs\NextThought`;
user data lives in `%LOCALAPPDATA%\NextThought` and is kept on uninstall.

### Known limitations (intentional differences from the Mac)

- x64 only, unsigned; no ARM64, code signing, Native AOT, GPU recognition or Bluetooth fallback.
- No system speech recognition and no offline translator; translation goes through the answer
  service with the UI stating the text leaves the machine.
- No Mac-style screen-recording permission guide (Windows has no equivalent permission).
- `.doc`/`.odt`/`.rtf` must be saved in a modern format first; legacy PowerPoint conversion needs
  the optional LibreOffice component.
- One update channel only (`windows-v*`); the channel selector is not part of this release.
- The installer is unsigned, so updates verify SHA-256 only (the code keeps the signature
  verification slot for when signing is available).

### Not yet verified on real hardware (run RELEASE.md section 6 before publishing)

- K13 whether meeting participants can see the app's windows (needs a second device).
- K14 a real phone over LAN and relay (needs a phone).
- K15 multiple screens and different scale factors (needs a second display).
- K16 real cloud recognition providers, real web search and a real answer-service account (needs
  accounts).
- K11 installation/first-run in a clean Windows sandbox, K12 audio-device switching/unplug/sleep
  (left to a person where this machine has no hardware).
- The gaze reminder: the on-device camera (BisonCam, NB Pro) opens and delivers frames, but no
  person stood in front of it; the "looking" and "looking away" states were verified with recorded
  frontal images through the same FaceLandmarker + GazeEstimator path (looking away simulated by
  moving the iris points outside 0.35× the eye width), not with a real person at a real camera.
- The three speech-recognition archives (about 128–130 MB each) and the 613 MB semantic model were
  only reachability- and size-checked on the development machine, not fully downloaded; the app
  verifies the full SHA-256 at install time (see `Windows/validation/models.md`).

### Verify

The installer SHA-256 is in `SHA256SUMS.txt` and `update.json` on the release page. The install,
upgrade, uninstall and in-app update tests run through `Windows/release.ps1` before a release.
